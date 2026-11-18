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
        var indexed = await index.AllAsync();
        // Two records at one path (someone added it while another load or check-in did): say which, and stop.
        if (Index.Duplicates(indexed) is { Count: > 0 } twice)
        {
            foreach (var g in twice)
                say($"TWO RECORDS: {g.Key}: {string.Join("; ", g.Select(r => $"id {r.FileId}, {r.UpdatedBy} {Engine.Local(r.UpdatedAt)}{(r.Removed ? ", removed" : "")}"))}");
            say("Stopped: the index has more than one record at the path(s) above. Tell App; nothing was read or written.");
            return;
        }
        var existing = indexed.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
        var byId = indexed.ToDictionary(r => r.FileId);
        var files = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Select(f => (Local: f, Key: Paths.KeyOf(root, f)))
            .Where(f => !Paths.IsIgnored(f.Key) && !Junk.Contains(Path.GetFileName(f.Local)))
            .OrderBy(f => f.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Pass 1: every file gets its id (kept if already in the index) and goes to S3 if new or changed.
        var state = LocalState.Load(root);
        var records = new List<(FileRecord Record, string Local, bool Upload)>();
        var removedHere = new List<string>();
        var refused = new List<(string Path, string Why)>();
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
            if (upload && old != null && Refuse(old, state) is { } why) refused.Add((old.Path, why));
            // A file this PC got from the vault that has since moved: it isn't new. Get first (the D13 rehearsal, 18 Nov).
            if (old == null && state.Files.TryGetValue(key, out var got) && byId.TryGetValue(got.FileId, out var now) && !now.Path.Equals(key, StringComparison.OrdinalIgnoreCase))
                refused.Add((key, $"this PC got it from the vault, which now has it at {now.Path}{(now.Removed ? " (removed)" : "")}. Get first (pdm get); loading would add it back as a new file."));
            records.Add((record, local, upload));
        }

        // Pass 2: references and PartNos, now that every file has an id.
        var candidates = records.Select(r => r.Record).ToList();
        foreach (var (record, local, _) in records) engine.Describe(record, local, candidates);

        // Keep SOLIDWORKS files, weldment profiles and what they refer to; list the rest.
        var referenced = candidates.SelectMany(r => r.Refs).ToHashSet();
        var skipped = records.Where(r => !Paths.IsSolidWorksFile(r.Record.Path) && !Paths.IsWeldmentProfile(r.Record.Path)
            && !referenced.Contains(r.Record.FileId)).ToList();
        records = records.Except(skipped).ToList();
        foreach (var r in skipped) say($"  skipped (not a SOLIDWORKS file, and nothing refers to it): {r.Record.Path}");
        var all = records.Select(r => r.Record).ToList();

        refused.RemoveAll(x => skipped.Any(r => r.Record.Path.Equals(x.Path, StringComparison.OrdinalIgnoreCase)));
        var refusedPaths = refused.Select(x => x.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        int toUpload = records.Count(r => r.Upload && !refusedPaths.Contains(r.Record.Path));
        var uploadMb = records.Where(r => r.Upload && !refusedPaths.Contains(r.Record.Path)).Sum(r => r.Record.Size) / 1048576.0;
        say($"{records.Count} files to load from {root} ({skipped.Count} skipped); {toUpload} new or changed ({uploadMb:0.0} MB to upload), {records.Count - toUpload - refused.Count} already in the vault"
            + (refused.Count > 0 ? $", {refused.Count} refused." : "."));
        foreach (var r in records.Where(r => r.Upload && !refusedPaths.Contains(r.Record.Path))) say($"  {(r.Record.VersionId == "" ? "new" : "changed")}  {r.Record.Path}");
        if (releasedHere > 0) say($"{releasedHere} files checked out to you here are checked in by the load (changed or not).");
        foreach (var k in removedHere) say($"  skipped (removed from the vault; Restore it first if it should come back): {k}");
        // A file already in the vault goes up as changed only if this login holds its check-out on this PC and this PC
        // has the vault's latest; otherwise the load could put an old copy back over someone's work (plates-0804, 13 Nov).
        foreach (var (path, why) in refused) say($"REFUSED: {path}: {why}");

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
        if (refused.Count > 0) { say($"Stopped: {refused.Count} file(s) refused (REFUSED lines above). Nothing was uploaded."); return; }

        // A load can take minutes. Each record is written only if it's still as read at the start (Grant's D13, 15 Nov):
        // a check-out taken meanwhile is kept; a file checked in, moved or removed meanwhile is left as the vault has it.
        int n = 0, keptLocks = 0;
        var leftAlone = new List<string>();
        foreach (var (record, local, upload) in records)
        {
            existing.TryGetValue(record.Path, out var old);
            if (upload && old != null && await index.GetAsync(record.FileId) is var cur && !SameAsRead(cur, old))
            {
                leftAlone.Add($"{record.Path}: {ChangedHow(cur, old)} during the load, so this copy wasn't uploaded");
                continue;
            }
            if (old == null && await index.AtPathAsync(record.Path) is { } taken)
            {
                leftAlone.Add($"{record.Path}: {taken.UpdatedBy} added a file at this path during the load, so this copy wasn't uploaded");
                continue;
            }
            if (upload)
            {
                Paths.SetReadOnly(local, false);
                (record.VersionId, record.ETag) = await vault.UploadAsync(local, record.Path, record.FileId);
                if (++n % 10 == 0) say($"  uploaded {n} of {toUpload}");
            }
            var was = old;
            bool written = false, gaveUp = false;
            for (int tries = 0; tries < 5 && !written && !gaveUp; tries++)
            {
                if (await index.PutIfUnchangedAsync(record, was)) { written = true; break; }
                var now = await index.GetAsync(record.FileId);
                if (now == null || was == null || now.VersionId != was.VersionId || now.Path != was.Path || now.RemovedAt != null)
                {
                    leftAlone.Add($"{record.Path}: {ChangedHow(now, was)} during the load; the vault's record was kept"
                        + (upload ? $" (this PC's copy did go up as S3 version {record.VersionId}, under the newer one's record: tell App)" : ""));
                    gaveUp = true; break;
                }
                // Only the check-out or release changed: keep the vault's, write the rest.
                if (now.LockedBy != was.LockedBy || now.LockMachine != was.LockMachine) keptLocks++;
                (record.LockedBy, record.LockedByName, record.LockedAt, record.LockMachine) = (now.LockedBy, now.LockedByName, now.LockedAt, now.LockMachine);
                record.ReleaseSet = now.ReleaseSet;
                was = now;
            }
            if (!written)
            {
                if (!gaveUp) leftAlone.Add($"{record.Path}: kept changing during the load; not written");
                continue;
            }
            if (upload) await Activity.LogAsync(vault, new ActivityEvent(record.UpdatedAt, adminName, Activity.KindOf(record.Path, old == null), record.Path, record.FileId,
                Email: engine.Email, VersionId: record.VersionId, Note: record.Note), say);
            await index.UpdatePartNosAsync(record, old?.PartNos ?? new());
            state.Record(record.Path, local, record.FileId, record.VersionId, record.ETag);
            if (record.LockedBy == null) Paths.SetReadOnly(local, true);
        }
        state.Save(root);
        say($"Load done: {n} uploaded, {records.Count - leftAlone.Count} index records written. This PC's copy is recorded as current.");
        if (keptLocks > 0) say($"{keptLocks} check-out(s) taken during the load were kept.");
        foreach (var x in leftAlone) say($"LEFT ALONE: {x}");
    }

    static bool SameAsRead(FileRecord? now, FileRecord was) => now != null && now.VersionId == was.VersionId && now.Path == was.Path
        && now.RemovedAt == null && now.LockedBy == was.LockedBy && now.LockMachine == was.LockMachine && now.ReleaseSet == was.ReleaseSet;

    static string ChangedHow(FileRecord? now, FileRecord? was) =>
        now == null ? "it left the index"
        : was == null ? "a record with its id appeared"
        : now.RemovedAt != null ? $"it was removed by {now.RemovedByName ?? now.RemovedBy ?? "someone"}"
        : now.Path != was.Path ? $"it was moved to {now.Path}"
        : now.VersionId != was.VersionId ? $"a newer version was checked in by {now.UpdatedBy}"
        : now.LockedBy != was.LockedBy || now.LockMachine != was.LockMachine ? (now.LockedBy == null ? "its check-out was ended" : $"it was checked out by {now.LockedByName} on {now.LockMachine}")
        : "its release changed";

    // Why a file that differs from the vault mustn't go up as changed, or null if it may.
    string? Refuse(FileRecord old, LocalState state)
    {
        bool mine = engine.Mine(old);
        if (old.LockedBy != null && !mine) return $"changed here but checked out by {old.LockedByName} on {old.LockMachine}. It's theirs to check in.";
        state.Files.TryGetValue(old.Path, out var had);
        var by = $"{old.UpdatedBy}, {Engine.Local(old.UpdatedAt)}";
        if (had != null && had.ETag != "" && !had.ETag.Equals(old.ETag, StringComparison.OrdinalIgnoreCase)) // content, not version id: a move makes a new version of the same content
            return $"the vault has a newer version than this PC last got (checked in by {by}). Get it first (pdm get); uploading would put the old copy back over it.";
        if (!mine && had == null) return $"this PC never got the vault's version (checked in by {by}). Get it first (pdm get), or check it out.";
        if (!mine) return "changed here but not checked out to you on this PC. Check it out first (pdm checkout), or get the vault's copy back (pdm get).";
        return null;
    }
}
