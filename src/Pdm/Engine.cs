namespace Pdm;

// The PDM operations, for one logged-in user on this PC. Each method reports what it did through `say`.
public sealed class Engine(Settings s, Index index, Vault vault, string email, string name, bool sharedEditor, Action<string> say)
{
    readonly string root = s.LocalRoot;
    readonly string machine = Environment.MachineName;
    DocReader? reader;
    DocReader Reader => reader ??= new DocReader();
    readonly object sayLock = new();
    static readonly ParallelOptions Several = new() { MaxDegreeOfParallelism = 12 }; // locks taken or released at once

    // Where the time went, for the page's Details.
    public Timings Time { get; } = new();

    // The index as read earlier in the same run (the page hands it from one request to the next), kept in step
    // with this engine's own changes, so a 130-file check-in reads the whole index once rather than 130 times.
    // Lock checks that matter still read the file's row fresh, and every write is conditional.
    public List<FileRecord>? Known { get; set; }

    async Task<List<FileRecord>> AllAsync() => Known ??= await Time.Of("index reads", () => index.AllAsync());

    // Keeps Known in step with a record this engine wrote.
    void Remember(FileRecord f)
    {
        if (Known == null) return;
        lock (Known) { Known.RemoveAll(r => r.FileId == f.FileId); Known.Add(f); }
    }

    void Say(string line) { lock (sayLock) say(line); } // for work done in parallel

    // Downloads what's new or changed in the vault. Files I don't hold end up read-only.
    // only: just these vault paths (the page asks a few at a time to show progress).
    public async Task GetAsync(string? folder, IEnumerable<string>? only = null)
    {
        if (!Directory.Exists(root)) { say($"{root} doesn't exist on this PC. Map it first (pdm.exe map <folder>, then restart)."); return; }
        var prefix = PrefixOf(folder);
        var wanted = only?.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var state = LocalState.Load(root);
        int fetched = 0, current = 0;
        foreach (var f in (await AllAsync()).Where(f => f.Path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) && (wanted == null || wanted.Contains(f.Path))).OrderBy(f => f.Path))
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
                    await Time.Of("downloads", () => vault.DownloadAsync(f.Path, f.VersionId, local));
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

    // Takes the locks on the given files, several at a time; fetches the latest version first, then makes them writable.
    public async Task CheckOutAsync(IEnumerable<string> paths)
    {
        var all = await AllAsync();
        var state = LocalState.Load(root);
        var wanted = new List<FileRecord>();
        foreach (var p in paths)
        {
            var key = Paths.KeyOf(root, p);
            var f = all.FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (f == null) { say($"Not in the vault: {key}"); continue; }
            if (Paths.IsShared(f.Path) && !sharedEditor) { say($"{f.Path} is in a shared folder; you can't check it out."); continue; }
            wanted.Add(f);
        }
        try
        {
            await Parallel.ForEachAsync(wanted, Several, async (f, _) =>
            {
                try { await CheckOutOneAsync(f, state); }
                catch (Exception ex) { Say($"Couldn't check out {f.Path}: {ex.Message}"); }
            });
        }
        finally { state.Save(root); } // what was fetched stays recorded, even if something failed
    }

    async Task CheckOutOneAsync(FileRecord f, LocalState state)
    {
        var local = Paths.LocalOf(root, f.Path);
        LocalState.Entry? had;
        lock (state) had = state.Files.GetValueOrDefault(f.Path);
        if (File.Exists(local) && Time.Of("file checks", () => LocalState.Differs(had, local)) && !Mine(f))
        {
            Say($"{f.Path} was changed on this PC without a check-out. Move it aside first.");
            return;
        }
        try
        {
            await Time.Of("index writes", () => index.LockAsync(f.FileId, email, name, machine));
        }
        catch (LockTakenException ex)
        {
            Say($"Can't check out {f.Path}: {ex.Holder.LockedByName} has it on {ex.Holder.LockMachine}, since {Local(ex.Holder.LockedAt)}.");
            return;
        }
        (f.LockedBy, f.LockedByName, f.LockedAt, f.LockMachine) = (email, name, Index.Now(), machine);
        if (had == null || had.VersionId != f.VersionId || !File.Exists(local))
        {
            await Time.Of("downloads", () => vault.DownloadAsync(f.Path, f.VersionId, local));
            lock (state) state.Record(f.Path, local, f.FileId, f.VersionId, f.ETag);
        }
        Paths.SetReadOnly(local, false);
        Say($"Checked out {f.Path}");
    }

    // Checks in several files. Unchanged ones I hold are only released, all at once and several at a time;
    // the rest go one by one in the order given (parts before assemblies before drawings).
    public async Task CheckInManyAsync(IEnumerable<string> paths, string note)
    {
        var all = await AllAsync();
        var state = LocalState.Load(root);
        var release = new List<(FileRecord F, string Local)>();
        var rest = new List<string>();
        foreach (var p in paths)
        {
            var key = Paths.KeyOf(root, p);
            var local = Paths.LocalOf(root, key);
            var f = all.FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
            if (f != null && Mine(f) && File.Exists(local) && !Time.Of("file checks", () => state.IsChanged(f.Path, local))) release.Add((f, local));
            else rest.Add(p);
        }
        await Parallel.ForEachAsync(release, Several, async (r, _) =>
        {
            try
            {
                await Time.Of("index writes", () => index.UnlockAsync(r.F.FileId, email, machine));
                r.F.LockedBy = r.F.LockedByName = r.F.LockedAt = r.F.LockMachine = null;
                Paths.SetReadOnly(r.Local, true);
                Say($"No changes in {r.F.Path}; released.");
            }
            catch (Exception ex) { Say($"Couldn't check in {r.F.Path}: {ex.Message}"); }
        });
        foreach (var p in rest)
            try { await CheckInAsync(p, note); }
            catch (Exception ex) { say($"Couldn't check in {Paths.KeyOf(root, p)}: {ex.Message}"); }
    }

    // Uploads my changed file as a new version, updates the index and releases the lock.
    // A file that isn't in the vault yet is added.
    public async Task CheckInAsync(string path, string note)
    {
        var key = Paths.KeyOf(root, path);
        var local = Paths.LocalOf(root, key);
        if (!File.Exists(local)) { say($"No such file: {local}"); return; }
        if (Paths.IsIgnored(key)) { say($"{key} isn't a vault file."); return; }

        var all = await AllAsync();
        var existing = all.FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
        var state = LocalState.Load(root);

        if (existing != null)
        {
            var id = existing.FileId;
            existing = await Time.Of("index reads", () => index.GetAsync(id)) ?? existing; // fresh lock state
            if (!Mine(existing)) { say($"You don't have {key} checked out on this PC."); return; }
            if (!Time.Of("file checks", () => state.IsChanged(existing.Path, local)))
            {
                await Time.Of("index writes", () => index.UnlockAsync(existing.FileId, email, machine));
                existing.LockedBy = existing.LockedByName = existing.LockedAt = existing.LockMachine = null;
                Remember(existing);
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
        Time.Of("Document Manager", () => { Describe(record, local, all); return 0; });

        var clashes = await Time.Of("PartNo checks", () => index.PartNoClashesAsync(record));
        if (clashes.Count > 0)
        {
            say($"Not checked in: PartNo already used elsewhere: {string.Join("; ", clashes)}");
            return;
        }

        try { (record.VersionId, record.ETag) = await Time.Of("uploads", () => vault.UploadAsync(local, record.Path, record.FileId)); }
        catch (Exception ex) when (ex is IOException || ex.InnerException is IOException)
        {
            say($"Couldn't read {record.Path}: close it in SOLIDWORKS first, then check in again.");
            return;
        }
        record.Size = new FileInfo(local).Length;
        if (existing != null) await Time.Of("index writes", () => index.CheckInAsync(record, email, machine));
        else await Time.Of("index writes", () => index.PutAsync(record));
        Remember(record);
        await Time.Of("PartNo writes", () => index.UpdatePartNosAsync(record, existing?.PartNos ?? new()));

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
        var f = (await AllAsync()).FirstOrDefault(r => r.Path.Equals(key, StringComparison.OrdinalIgnoreCase));
        if (f == null) { say($"Not in the vault: {key}"); return; }
        if (!Mine(f)) { say($"You don't have {f.Path} checked out on this PC."); return; }

        var local = Paths.LocalOf(root, f.Path);
        var state = LocalState.Load(root);
        if (!File.Exists(local) || state.IsChanged(f.Path, local) || !state.Files.TryGetValue(f.Path, out var had) || had.VersionId != f.VersionId)
            await vault.DownloadAsync(f.Path, f.VersionId, local);
        await index.UnlockAsync(f.FileId, email, machine);
        f.LockedBy = f.LockedByName = f.LockedAt = f.LockMachine = null;
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
