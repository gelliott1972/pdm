namespace Pdm;

// Admin: loads the local root (P:\) into the vault and the index. Safe to run again:
// unchanged files aren't re-uploaded, file ids and locks are kept.
// Takes SOLIDWORKS files, plus any other file a SOLIDWORKS file refers to (e.g. an equations .txt);
// anything else under P:\ (old PDF, CSV, STEP exports) is skipped and listed.
public sealed class Loader(Settings s, Index index, Vault vault, Engine engine, string adminName, Action<string> say)
{
    static readonly HashSet<string> Junk = new(StringComparer.OrdinalIgnoreCase) { "desktop.ini", "Thumbs.db" };

    // note: recorded on every new or changed file (default "Initial load" for new ones; changed ones keep their old note).
    public async Task RunAsync(bool dryRun, string? note = null)
    {
        var root = s.LocalRoot;
        var existing = (await index.AllAsync()).ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (Local: f, Key: Paths.KeyOf(root, f)))
            .Where(f => !Paths.IsIgnored(f.Key) && !Junk.Contains(Path.GetFileName(f.Local)))
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pass 1: every file gets its id (kept if already in the index) and goes to S3 if new or changed.
        var records = new List<(FileRecord Record, string Local, bool Upload)>();
        var removedHere = new List<string>();
        int releasedHere = 0;
        foreach (var (local, key) in files)
        {
            existing.TryGetValue(key, out var old);
            if (old?.Removed == true) { removedHere.Add(key); continue; } // a removed file stays removed; Restore brings it back
            var md5 = Vault.Md5(local);
            bool upload = old == null || old.ETag != md5;
            var record = new FileRecord
            {
                FileId = old?.FileId ?? Guid.NewGuid().ToString(),
                Path = old?.Path ?? key,
                VersionId = old?.VersionId ?? "", ETag = md5, Size = new FileInfo(local).Length,
                UpdatedAt = old != null && !(upload && note != null) ? old.UpdatedAt : Index.Now(),
                UpdatedBy = old != null && !(upload && note != null) ? old.UpdatedBy : adminName,
                Note = old != null && !(upload && note != null) ? old.Note : note ?? "Initial load",
                LockedBy = old?.LockedBy, LockedByName = old?.LockedByName, LockedAt = old?.LockedAt, LockMachine = old?.LockMachine,
                Moves = old?.Moves ?? new(), ReleaseSet = old?.ReleaseSet
            };
            if (old != null && engine.Mine(old)) // checked out for a round by this admin login on this PC: the load checks it in
            {
                record.LockedBy = record.LockedByName = record.LockedAt = record.LockMachine = null;
                releasedHere++;
            }
            records.Add((record, local, upload));
        }

        // Pass 2: references and PartNos, now that every file has an id.
        var candidates = records.Select(r => r.Record).ToList();
        foreach (var (record, local, _) in records) engine.Describe(record, local, candidates);

        // Keep SOLIDWORKS files and what they refer to; list the rest.
        var referenced = candidates.SelectMany(r => r.Refs).ToHashSet();
        var skipped = records.Where(r => !Paths.IsSolidWorksFile(r.Record.Path) && !referenced.Contains(r.Record.FileId)).ToList();
        records = records.Except(skipped).ToList();
        foreach (var r in skipped) say($"  skipped (not a SOLIDWORKS file, and nothing refers to it): {r.Record.Path}");
        var all = records.Select(r => r.Record).ToList();

        int toUpload = records.Count(r => r.Upload);
        var uploadMb = records.Where(r => r.Upload).Sum(r => r.Record.Size) / 1048576.0;
        say($"{records.Count} files to load from {root} ({skipped.Count} skipped); {toUpload} new or changed ({uploadMb:0.0} MB to upload), {records.Count - toUpload} already in the vault.");
        foreach (var r in records.Where(r => r.Upload)) say($"  {(r.Record.VersionId == "" ? "new" : "changed")}  {r.Record.Path}");
        if (releasedHere > 0) say($"{releasedHere} files checked out to you here are checked in by the load (changed or not).");
        foreach (var k in removedHere) say($"  skipped (removed from the vault; Restore it first if it should come back): {k}");
        // A changed file someone has checked out is theirs to check in: the load never uploads over a check-out.
        var held = records.Where(r => r.Upload && r.Record.LockedBy != null).ToList();
        foreach (var r in held) say($"CHECK: {r.Record.Path} is changed here but checked out by {r.Record.LockedByName} on {r.Record.LockMachine}");

        var dupes = all.SelectMany(r => r.PartNos.Select(p => (p.PartNo, r.Path)))
            .GroupBy(x => x.PartNo, StringComparer.OrdinalIgnoreCase).Where(g => g.Select(x => x.Path).Distinct().Count() > 1).ToList();
        foreach (var g in dupes) say($"PartNo {g.Key} is on more than one file: {string.Join(", ", g.Select(x => x.Path).Distinct())}");
        // 0804 R8: one PartNo on a configuration and its As Welded child(ren). Allowed; the register points at the parent.
        var shared = all.SelectMany(r => r.PartNos.GroupBy(p => p.PartNo, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1).Select(g => (File: r, Group: g.ToList()))).ToList();
        if (shared.Count > 0) say($"{shared.Count} PartNo(s) on more than one configuration of the same file; the register points at the parent:");
        foreach (var (file, group) in shared)
        {
            var parent = Index.RegisterEntries(new FileRecord { PartNos = group })[0];
            var others = group.Where(p => p != parent).Select(p => p.Config == "" ? "(file)" : p.Config);
            var odd = group.Count(p => !Index.IsAsWelded(p.Config)) != 1 ? "  CHECK: not one parent with As Welded children" : "";
            say($"  {parent.PartNo}: {file.Path} / {(parent.Config == "" ? "(file)" : parent.Config)} (also on {string.Join(", ", others)}){odd}");
        }
        say($"{all.Sum(r => r.PartNos.Count)} PartNo values, {all.Sum(r => r.Refs.Count)} references inside the vault, {all.Sum(r => r.MissingRefs.Count)} outside it.");
        foreach (var r in all.Where(r => r.MissingRefs.Count > 0))
            foreach (var m in r.MissingRefs) say($"  {r.Path} refers to {m} (not in the vault)");

        if (dryRun) { say("Dry run: nothing uploaded or written."); return; }
        if (dupes.Count > 0) { say("Stopped: fix the duplicate PartNos first."); return; }
        if (held.Count > 0) { say("Stopped: changed files are checked out (CHECK lines above). Nothing was uploaded."); return; }

        var state = LocalState.Load(root);
        int n = 0;
        foreach (var (record, local, upload) in records)
        {
            if (upload)
            {
                Paths.SetReadOnly(local, false);
                (record.VersionId, record.ETag) = await vault.UploadAsync(local, record.Path, record.FileId);
                if (++n % 10 == 0) say($"  uploaded {n} of {toUpload}");
            }
            existing.TryGetValue(record.Path, out var old);
            await index.PutAsync(record);
            if (upload) await Activity.LogAsync(vault, new ActivityEvent(record.UpdatedAt, adminName, Activity.KindOf(record.Path, old == null), record.Path, record.FileId,
                Email: engine.Email, VersionId: record.VersionId, Note: record.Note), say);
            await index.UpdatePartNosAsync(record, old?.PartNos ?? new());
            state.Record(record.Path, local, record.FileId, record.VersionId, record.ETag);
            if (record.LockedBy == null) Paths.SetReadOnly(local, true);
        }
        state.Save(root);
        say($"Load done: {n} uploaded, {records.Count} index records written. This PC's copy is recorded as current.");
    }
}
