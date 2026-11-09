namespace Pdm;

// Admin: moves vault files to new paths, from a list ("old -> new", one per line; vault keys or P:\ paths).
// Each move keeps the file's id (so references still match), its PartNos and its history (S3 versions before the
// move stay at the old key); the S3 object is copied inside the bucket, nothing is uploaded. Every line is checked
// first and nothing moves if any line has a problem. Download latest on each PC then follows the moves.
// On this PC the local copy is moved too, or, if it's already at the new path, state.json is re-keyed.
public sealed class Mover(Settings s, Index index, Vault vault, string email, string name, Action<string> say)
{
    readonly string root = s.LocalRoot;
    readonly string machine = Environment.MachineName;

    sealed record Line(int No, string From, string To, FileRecord? File, List<string> Problems, string Here);

    public async Task<int> RunAsync(string listFile, string note, bool dryRun)
    {
        note = note.Trim();
        if (note == "") { say("Give a note (--note \"...\"): it's recorded with each move."); return 2; }
        var all = await index.AllAsync();
        var byPath = all.ToDictionary(r => r.Path, StringComparer.OrdinalIgnoreCase);
        var state = LocalState.Load(root);
        var lines = Parse(listFile);
        if (lines.Count == 0) { say($"No moves in {listFile}."); return 2; }

        for (int i = 0; i < lines.Count; i++)
        {
            var l = lines[i];
            if (l.Problems.Count > 0) continue;
            byPath.TryGetValue(l.From, out var f);
            var p = l.Problems;
            if (f == null) p.Add("not in the vault");
            else if (f.Removed) p.Add($"removed from the vault ({f.RemovedByName}); restore it first");
            else if (f.LockedBy != null && !(f.LockedBy == email && f.LockMachine == machine)) p.Add($"checked out by {f.LockedByName} on {f.LockMachine}");
            if (l.From.Equals(l.To, StringComparison.OrdinalIgnoreCase)) p.Add("old and new paths are the same (a change of capitals only isn't a move)");
            if (!Path.GetExtension(l.From).Equals(Path.GetExtension(l.To), StringComparison.OrdinalIgnoreCase)) p.Add("the file type would change");
            if (Paths.IsReleaseFile(l.From) || Paths.IsReleaseFile(l.To)) p.Add("release files stay as issued");
            if (Paths.IsIgnored(l.To)) p.Add("the new path isn't a vault path");
            if (byPath.TryGetValue(l.To, out var there)) p.Add($"the new path is already in the vault{(there.Removed ? " (a removed file)" : "")}");
            if (lines.Count(o => o.From.Equals(l.From, StringComparison.OrdinalIgnoreCase)) > 1) p.Add("the old path is listed more than once");
            if (lines.Count(o => o.To.Equals(l.To, StringComparison.OrdinalIgnoreCase)) > 1) p.Add("the new path is listed more than once");
            lines[i] = l with { File = f, Here = f == null ? "" : HereCheck(f, l, state, p) };
        }

        var bad = lines.Where(l => l.Problems.Count > 0).ToList();
        say($"{lines.Count} moves in {listFile}; {lines.Count - bad.Count} ready, {bad.Count} with problems.");
        foreach (var l in lines)
            say(l.Problems.Count > 0
                ? $"  line {l.No}: {l.From} -> {l.To}: NOT OK: {string.Join("; ", l.Problems)}"
                : $"  {l.From} -> {l.To}  ({l.Here}{(l.File!.PartNos.Count > 0 ? $"; {l.File.PartNos.Select(x => x.PartNo).Distinct(StringComparer.OrdinalIgnoreCase).Count()} PartNo(s) go with it" : "")})");
        if (bad.Count > 0) { say(dryRun ? "Dry run: fix these lines; nothing would move." : "Stopped: nothing moved. Fix these lines first."); return 1; }
        if (dryRun) { say("Dry run: nothing moved."); return 0; }

        int done = 0;
        try
        {
            foreach (var l in lines)
                if (await MoveOneAsync(l, note, state)) done++;
        }
        finally { state.Save(root); }
        say($"Move done: {done} of {lines.Count} moved{(done < lines.Count ? "; see the lines above for the rest" : "")}. Download latest on each PC moves its copies.");
        return done == lines.Count ? 0 : 1;
    }

    // A round list (CSV: action,old_path,new_path,...) gives the "moved" and "moved+changed" rows; any other file
    // has one "old -> new" per line.
    List<Line> Parse(string listFile)
    {
        var result = new List<Line>();
        var round = Round.IsRound(listFile) ? Round.Read(listFile) : null;
        var text = round?.Select(r => r.Action is "moved" or "moved+changed" ? $"{r.Old} -> {r.New}" : "").ToArray() ?? File.ReadAllLines(listFile);
        for (int i = 0; i < text.Length; i++)
        {
            var t = text[i].Trim();
            if (t == "" || t.StartsWith('#')) continue;
            var parts = t.Split("->");
            if (parts.Length != 2) { result.Add(new(i + 1, t, "", null, ["write it as: old path -> new path"], "")); continue; }
            var problems = new List<string>();
            string Key(string p)
            {
                p = p.Trim().Trim('"');
                if (!Path.IsPathRooted(p)) return p.Replace('\\', '/').TrimStart('/');
                try { return Paths.KeyOf(root, p); } catch (ArgumentException) { problems.Add($"{p} isn't under {root}"); return p; }
            }
            result.Add(new(round != null ? i + 2 : i + 1, Key(parts[0]), Key(parts[1]), null, problems, ""));
        }
        return result;
    }

    // What happens to this PC's copy; adds a problem when it can't be done safely.
    string HereCheck(FileRecord f, Line l, LocalState state, List<string> p)
    {
        var from = Paths.LocalOf(root, l.From);
        var to = Paths.LocalOf(root, l.To);
        bool oldHere = File.Exists(from), newHere = File.Exists(to);
        bool oldChanged = oldHere && Changed(f, l.From, from, state);
        bool newChanged = newHere && Vault.Md5(to) != f.ETag;
        if (oldHere && newHere)
        {
            if (oldChanged) p.Add("both paths are on this PC and the copy at the old path has changes");
            return $"already at the new path on this PC{(newChanged ? ", changed" : "")}; the old copy is deleted";
        }
        if (oldHere) return oldChanged ? "moves on this PC, with its changes (the load uploads them)" : "moves on this PC";
        if (newHere) return $"already at the new path on this PC{(newChanged ? ", changed (the load uploads it)" : "")}";
        return "not on this PC";
    }

    static bool Changed(FileRecord f, string key, string local, LocalState state) =>
        state.Files.TryGetValue(key, out var e) ? LocalState.Differs(e, local) : Vault.Md5(local) != f.ETag;

    async Task<bool> MoveOneAsync(Line l, string note, LocalState state)
    {
        var f = l.File!;
        bool held = f.LockedBy == email && f.LockMachine == machine; // checked out for the round: stays checked out
        try { await index.LockAsync(f.FileId, email, name, machine); }
        catch (LockTakenException ex) { say($"  Not moved: {l.From}: {ex.Holder.LockedByName} checked it out on {ex.Holder.LockMachine} just now."); return false; }
        var m = new MoveEntry(l.From, l.To, Index.Now(), name, note, f.ETag);
        try
        {
            var (versionId, etag) = await vault.CopyAsync(l.From, f.VersionId, l.To);
            if (etag != f.ETag) throw new InvalidOperationException($"the copy in the vault doesn't match (ETag {etag}, expected {f.ETag})");
            await index.MoveAsync(f, m with { Note = $"{note} (moved from {Folder(l.From)})" }, versionId, email, machine, keepLock: held);
            await index.UpdatePartNosAsync(f, []); // the register entries now point at the new path
        }
        catch (Exception ex)
        {
            if (!held) try { await index.UnlockAsync(f.FileId, email, machine); } catch { }
            say($"  Not moved: {l.From}: {ex.Message}");
            return false;
        }
        say($"  Moved {l.From} -> {l.To}{(held ? " (still checked out to you)" : "")}");
        MoveHere(f, l, state, held);
        return true;
    }

    // This PC's copy: to the new path, and state.json re-keyed so an unchanged copy counts as the vault's.
    void MoveHere(FileRecord f, Line l, LocalState state, bool held)
    {
        var from = Paths.LocalOf(root, l.From);
        var to = Paths.LocalOf(root, l.To);
        state.Files.TryGetValue(l.From, out var entry);
        try
        {
            if (File.Exists(from))
            {
                Paths.SetReadOnly(from, false);
                if (File.Exists(to)) File.Delete(from); // checked as the vault's copy before anything moved
                else { Directory.CreateDirectory(Path.GetDirectoryName(to)!); File.Move(from, to); }
                Paths.RemoveEmptyFolders(root, from);
            }
            state.Files.Remove(l.From);
            if (!File.Exists(to)) return;
            if (Vault.Md5(to) == f.ETag) { state.Record(l.To, to, f.FileId, f.VersionId, f.ETag); Paths.SetReadOnly(to, !held); }
            else if (entry != null) state.Files[l.To] = entry; // changed: still counts as changed, and the load uploads it
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            say($"  Moved in the vault, but not on this PC (open in SOLIDWORKS?): {ex.Message}. Download latest moves it later.");
        }
    }

    static string Folder(string key) => key.Contains('/') ? key[..key.LastIndexOf('/')] : "";
}

// Dev's round list: action,old_path,new_path,who,why (paths relative to the vault root). Actions: new, changed,
// moved, moved+changed, retired.
public static class Round
{
    public sealed record Row(string Action, string Old, string New);

    public static bool IsRound(string file) =>
        file.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) && File.ReadLines(file).FirstOrDefault()?.StartsWith("action,", StringComparison.OrdinalIgnoreCase) == true;

    public static List<Row> Read(string file)
    {
        var lines = File.ReadAllLines(file);
        var head = Split(lines[0]).Select(h => h.Trim().ToLowerInvariant()).ToList();
        int a = head.IndexOf("action"), o = head.IndexOf("old_path"), n = head.IndexOf("new_path");
        if (a < 0 || o < 0 || n < 0) throw new InvalidOperationException($"{file} needs the columns action, old_path and new_path.");
        return lines.Skip(1).Select(l => Split(l)).Select(c => new Row(At(c, a).ToLowerInvariant(), At(c, o), At(c, n))).ToList();
        static string At(List<string> c, int i) => i < c.Count ? c[i].Trim() : "";
    }

    // The files a round holds while it runs: everything with an old path (changed, moved, moved+changed, retired).
    public static List<string> Held(string file) =>
        Read(file).Where(r => r.Old != "" && r.Action is "changed" or "moved" or "moved+changed" or "retired").Select(r => r.Old).ToList();

    static List<string> Split(string line)
    {
        var cells = new List<string>(); var cur = new System.Text.StringBuilder(); bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"') { if (q && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; } else q = !q; }
            else if (c == ',' && !q) { cells.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(c);
        }
        cells.Add(cur.ToString());
        return cells;
    }
}
