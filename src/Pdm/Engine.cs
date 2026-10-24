namespace Pdm;

// The PDM operations, for one logged-in user on this PC. Each method reports what it did through `say`.
public sealed class Engine(Settings s, Index index, Vault vault, string email, string name, bool sharedEditor, Action<string> say)
{
    readonly string root = s.LocalRoot;
    readonly string machine = Environment.MachineName;
    DocReader? reader;
    DocReader Reader => reader ??= new DocReader();

    // Downloads what's new or changed in the vault. Files I don't hold end up read-only.
    public async Task GetAsync(string? folder)
    {
        if (!Directory.Exists(root)) { say($"{root} doesn't exist on this PC. Map it first (pdm.exe map <folder>, then restart)."); return; }
        var prefix = PrefixOf(folder);
        var state = LocalState.Load(root);
        int fetched = 0, current = 0;
        foreach (var f in (await index.AllAsync()).Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Path))
        {
            var local = Paths.LocalOf(root, f.Path);
            bool mine = Mine(f);
            state.Files.TryGetValue(f.Path, out var had);
            bool upToDate = had != null && had.VersionId == f.VersionId && File.Exists(local);

            if (mine) { current++; continue; } // my working copy; never overwrite it
            if (upToDate && !state.IsChanged(f.Path, local)) { Paths.SetReadOnly(local, true); current++; continue; }
            if (File.Exists(local) && state.IsChanged(f.Path, local) && !(had == null && Vault.Md5(local) == f.ETag))
            {
                if (had == null) say($"Skipped {f.Path}: a different file is already there. Move it aside and get again.");
                else say($"Skipped {f.Path}: changed on this PC without a check-out. Undo or move it aside, then get again.");
                continue;
            }
            try
            {
                if (!(File.Exists(local) && Vault.Md5(local) == f.ETag))
                {
                    await vault.DownloadAsync(f.Path, f.VersionId, local);
                    say($"Got {f.Path}");
                    fetched++;
                }
                else current++;
                state.Record(f.Path, local, f.FileId, f.VersionId, f.ETag);
                Paths.SetReadOnly(local, true);
            }
            catch (IOException ex)
            {
                say($"Couldn't update {f.Path} (open in SOLIDWORKS?): {ex.Message}");
            }
        }
        state.Save(root);
        say($"Get done: {fetched} downloaded, {current} already up to date.");
    }

    // What I hold, what others hold, and what has changed in the vault since my last get.
    public async Task StatusAsync(string? folder)
    {
        var prefix = PrefixOf(folder);
        var state = LocalState.Load(root);
        var files = (await index.AllAsync()).Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).OrderBy(f => f.Path).ToList();

        var mine = files.Where(Mine).ToList();
        say($"Checked out by me ({mine.Count}):");
        foreach (var f in mine)
            say($"  {f.Path}{(state.IsChanged(f.Path, Paths.LocalOf(root, f.Path)) ? "  [changed]" : "")}");

        var others = files.Where(f => f.LockedBy != null && !Mine(f)).ToList();
        say($"Checked out by others ({others.Count}):");
        foreach (var f in others) say($"  {f.Path}  {f.LockedByName} on {f.LockMachine}, since {Local(f.LockedAt)}");

        var newer = files.Where(f => !Mine(f) && (!state.Files.TryGetValue(f.Path, out var e) || e.VersionId != f.VersionId)).ToList();
        say($"Newer in the vault ({newer.Count}):");
        foreach (var f in newer) say($"  {f.Path}  {f.UpdatedBy}, {Local(f.UpdatedAt)}{(f.Note != "" ? "  \"" + f.Note + "\"" : "")}");
    }

    // Takes the locks on the given files, one by one; fetches the latest version first, then makes them writable.
    public async Task CheckOutAsync(IEnumerable<string> paths)
    {
        var all = await index.AllAsync();
        var state = LocalState.Load(root);
        foreach (var p in paths)
        {
            var key = Paths.KeyOf(root, p);
            var f = all.FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (f == null) { say($"Not in the vault: {key}"); continue; }
            if (Paths.IsShared(f.Path) && !sharedEditor) { say($"{f.Path} is in a shared folder; you can't check it out."); continue; }

            var local = Paths.LocalOf(root, f.Path);
            state.Files.TryGetValue(f.Path, out var had);
            if (File.Exists(local) && state.IsChanged(f.Path, local) && !Mine(f))
            {
                say($"{f.Path} was changed on this PC without a check-out. Move it aside first.");
                continue;
            }
            try
            {
                await index.LockAsync(f.FileId, email, name, machine);
            }
            catch (LockTakenException ex)
            {
                say($"Can't check out {f.Path}: {ex.Holder.LockedByName} has it on {ex.Holder.LockMachine}, since {Local(ex.Holder.LockedAt)}.");
                continue;
            }
            if (had == null || had.VersionId != f.VersionId || !File.Exists(local))
            {
                await vault.DownloadAsync(f.Path, f.VersionId, local);
                state.Record(f.Path, local, f.FileId, f.VersionId, f.ETag);
            }
            Paths.SetReadOnly(local, false);
            say($"Checked out {f.Path}");
        }
        state.Save(root);
    }

    // Uploads my changed file as a new version, updates the index and releases the lock.
    // A file that isn't in the vault yet is added.
    public async Task CheckInAsync(string path, string note)
    {
        var key = Paths.KeyOf(root, path);
        var local = Paths.LocalOf(root, key);
        if (!File.Exists(local)) { say($"No such file: {local}"); return; }
        if (Paths.IsIgnored(key)) { say($"{key} isn't a vault file."); return; }

        var all = await index.AllAsync();
        var existing = all.FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
        var state = LocalState.Load(root);

        if (existing != null)
        {
            existing = await index.GetAsync(existing.FileId) ?? existing; // fresh lock state
            if (!Mine(existing)) { say($"You don't have {key} checked out on this PC."); return; }
            if (!state.IsChanged(existing.Path, local))
            {
                await index.UnlockAsync(existing.FileId, email, machine);
                Paths.SetReadOnly(local, true);
                say($"No changes in {existing.Path}; released.");
                return;
            }
        }
        if (Paths.IsShared(key) && !sharedEditor) { say($"{key} is in a shared folder; you can't check it in."); return; }

        var record = new FileRecord
        {
            FileId = existing?.FileId ?? Guid.NewGuid().ToString(),
            Path = existing?.Path ?? key,
            UpdatedAt = Index.Now(), UpdatedBy = name, Note = note,
            LockedBy = existing?.LockedBy
        };
        Describe(record, local, all);

        var clashes = await index.PartNoClashesAsync(record);
        if (clashes.Count > 0)
        {
            say($"Not checked in: PartNo already used elsewhere: {string.Join("; ", clashes)}");
            return;
        }

        try { (record.VersionId, record.ETag) = await vault.UploadAsync(local, record.Path, record.FileId); }
        catch (Exception ex) when (ex is IOException || ex.InnerException is IOException)
        {
            say($"Couldn't read {record.Path}: close it in SOLIDWORKS first, then check in again.");
            return;
        }
        record.Size = new FileInfo(local).Length;
        if (existing != null) await index.CheckInAsync(record, email, machine);
        else await index.PutAsync(record);
        await index.UpdatePartNosAsync(record, existing?.PartNos ?? new());

        state.Record(record.Path, local, record.FileId, record.VersionId, record.ETag);
        state.Save(root);
        Paths.SetReadOnly(local, true);
        say($"{(existing == null ? "Added" : "Checked in")} {record.Path}");
        foreach (var m in record.MissingRefs) say($"  Note: refers to {m}, which isn't in the vault.");
    }

    // Releases my lock and puts back the vault's copy.
    public async Task UndoAsync(string path)
    {
        var key = Paths.KeyOf(root, path);
        var f = (await index.AllAsync()).FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (f == null) { say($"Not in the vault: {key}"); return; }
        if (!Mine(f)) { say($"You don't have {f.Path} checked out on this PC."); return; }

        var local = Paths.LocalOf(root, f.Path);
        var state = LocalState.Load(root);
        if (!File.Exists(local) || state.IsChanged(f.Path, local) || !state.Files.TryGetValue(f.Path, out var had) || had.VersionId != f.VersionId)
            await vault.DownloadAsync(f.Path, f.VersionId, local);
        await index.UnlockAsync(f.FileId, email, machine);
        state.Record(f.Path, local, f.FileId, f.VersionId, f.ETag);
        state.Save(root);
        Paths.SetReadOnly(local, true);
        say($"Undone {f.Path}: released, vault copy restored.");
    }

    // Fills in references and PartNos for a file, read from disk with the Document Manager.
    public void Describe(FileRecord record, string local, List<FileRecord> all)
    {
        record.Refs.Clear(); record.MissingRefs.Clear(); record.PartNos.Clear();
        if (!DocReader.IsSolidWorks(local)) return;
        var byPath = all.ToDictionary(r => r.Path, r => r.FileId, StringComparer.OrdinalIgnoreCase);
        var info = Reader.Read(local);
        foreach (var r in info.References)
        {
            string? refKey = null;
            try { refKey = Paths.KeyOf(root, r); } catch (ArgumentException) { }
            if (refKey != null && byPath.TryGetValue(refKey, out var id)) record.Refs.Add(id);
            else record.MissingRefs.Add(r);
        }
        // A drawing carries its model's PartNo (SPEC), so only parts and assemblies own one.
        if (!local.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)) record.PartNos.AddRange(info.PartNos);
    }

    // My lock means mine on this PC: the same person's check-out on another PC is that PC's working copy.
    public bool Mine(FileRecord f) => f.LockedBy == email && f.LockMachine == machine;

    string PrefixOf(string? folder) =>
        string.IsNullOrWhiteSpace(folder) ? "" : Paths.KeyOf(root, folder).TrimEnd('/') + "/";

    static string Local(string? utc) =>
        DateTime.TryParse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var t)
            ? t.ToLocalTime().ToString("d MMM HH:mm") : utc ?? "";
}
