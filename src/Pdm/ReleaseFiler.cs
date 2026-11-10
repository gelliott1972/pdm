using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pdm;

// The release step's real run (docs\briefs\release-0A.md, steps 5 and 6, and Grant's release packages, 9 Nov). From one
// or more dry-run work folders (a first run and its top-ups) it files, in one step:
//   - one folder per set (Grant, 9 Nov): <Scene>/_Review/<Set>_<code>/, e.g. APEX/SC08/_Review/LS-08X-06_0A/, holding
//     the set's PDFs, its Excel drawing list and a ZIP of both. A drawing in several sets (a shared drawing) has a copy
//     in each set's folder, so each folder is complete like its ZIP. Each PDF is a vault file with its set info on the
//     index entry (code, sets, when, who, the drawing and model versions it was made from, and its drawing-list line);
//   - the scene's drawing register, <Scene>/<scene>_Drawing_Register.xlsx, rewritten.
// The newest check of each drawing wins. A drawing whose drawing or models changed in the vault since its check is left
// out and listed. Everything is built from the set info, never by hand. By default nothing is filed unless every set is
// complete (--incomplete files what's ready; packages are then made only for complete sets). The dry run writes the
// packages and register to a work folder only. A drawing filed before in this code (another set's run) is copied into
// a new set's folder from the vault.
// Re-issue (--reissue "reason" --only a,b; Grant, 9 Nov): the named drawings' PDFs, filed before and checked again in a
// "release --reissue" top-up, are filed as new versions of the same release files (in every set folder that has them;
// the old ones stay in the vault's history), with new versions of their sets' packages and the register.
// release-folders (RelayoutAsync): moves a release filed in the old layout (PDFs straight in _Review, packages in
// _Review/_Sets) into the per-set folders.
public sealed class ReleaseFiler(Settings s, Index index, Vault vault, string name, Action<string> say, string email = "")
{
    public sealed record Model(string Path, string FileId, string VersionId);
    public sealed record Item(string Drawing, string FileId, string VersionId, string PdfKey, string? Pdf, bool Pass,
        List<string> Problems, List<Model> Models, List<string>? Accepted = null);
    sealed record Report(string Folder, string Code, bool Dry, List<Item> Items, string? Reissue = null);

    // One drawing-list line, as recorded on the PDF's set info. Notes stay in the set info only: the list has no
    // Notes column (Grant, 9 Nov: "too much info").
    public sealed record Line(string DrawingNo, string Title, string Rev, int Sheets, string Size, string PartNo, string Status,
        string Issued, string Pdf, string Notes);
    sealed record SetDef(string Name, string Folder, string Scene, HashSet<string> Drawings)
    {
        public string Label(string code) => $"{Name} {code}";
        public string Dir(string code) => Release.SetFolder(Scene, Name, code);
    }
    // One drawing's PDF in this release: about to be filed (from a dry run) or filed before (from the set info).
    sealed class Issue
    {
        public required string DrawingId { get; init; }
        public required string DrawingPath { get; init; }
        public required string PdfName { get; init; }    // e.g. 08A06-HBA-01_0A.pdf, the same in every set's folder
        public required Line Line { get; set; }
        public List<string> Sets { get; } = new();      // every set of this release the drawing is in ("LS-08X-06 0A")
        public Item? Item { get; init; }
        public string? Run { get; init; }
        public string By { get; set; } = "";            // who issued it (the set info's "by")
        public Dictionary<string, FileRecord> Records { get; } = new();          // filed copies, by the set whose folder holds them
        public Dictionary<string, FileRecord> Previous { get; init; } = new();  // a re-issue: the copies these become the next versions of
    }
    string? reissue;

    static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };
    const StringComparison Nc = StringComparison.OrdinalIgnoreCase;

    public async Task<int> RunAsync(IList<string> works, bool dryRun, bool incomplete, string? reissueReason = null, string? only = null)
    {
        reissue = string.IsNullOrWhiteSpace(reissueReason) || reissueReason.StartsWith("--") ? null : reissueReason.Trim();
        if ((reissueReason != null) != (reissue != null) || (reissue != null) != (only != null))
        { say("A re-issue takes both --reissue \"reason\" and --only naming the drawings."); return 2; }
        static string NameOf(string p) => System.IO.Path.GetFileNameWithoutExtension(p.Trim().Replace('/', '\\'));
        var names = only == null ? null : (File.Exists(only) ? File.ReadAllLines(only) : only.Split(',')).Select(NameOf).Where(n => n != "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        // The dry runs, oldest first, so a later check of a drawing replaces an earlier one.
        var reports = new List<(string Work, string Run, Report R)>();
        foreach (var w in works.Select(Path.GetFullPath))
        {
            var rp = Path.Combine(w, "report.json");
            if (!File.Exists(rp)) { say($"No report.json in {w}: give the work folder(s) of release dry runs."); return 2; }
            reports.Add((w, Path.GetFileName(w.TrimEnd('\\')), JsonSerializer.Deserialize<Report>(File.ReadAllText(rp), Read)!));
        }
        reports = reports.OrderBy(r => r.Run, StringComparer.Ordinal).ToList();
        var code = reports[0].R.Code;
        if (reports.Any(r => r.R.Code != code)) { say("The work folders are for different codes; give one release's folders."); return 2; }
        if (reissue != null && reports.Where(r => r.R.Reissue == null).Select(r => r.Run).FirstOrDefault() is { } plain)
        { say($"{plain} isn't a re-issue check: re-check the drawings with pdm release ... --reissue \"reason\" --only ... first."); return 2; }
        if (reissue == null && reports.Where(r => r.R.Reissue != null).Select(r => r.Run).FirstOrDefault() is { } ri)
        { say($"{ri} is a re-issue check: file it with --reissue \"reason\" --only ..."); return 2; }
        var folders = reports.SelectMany(r => r.R.Folder.Split(" + ")).Select(f => f.Trim().Trim('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        var all = await index.AllAsync();
        var byId = all.ToDictionary(r => r.FileId);
        var byPath = all.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // The sets: each folder, with the drawings in it now (its own and the shared drawings its models use).
        var plan = new Release(s, all.Where(r => !r.Removed).ToList(), _ => { });
        var sets = folders.Select(f => new SetDef(f.Split('/')[^1], f, Release.SceneOf(f + "/x"),
            plan.Plan(f, code, withShared: true).Select(x => x.Drawing.FileId).ToHashSet())).ToList();
        say($"Release {code}: {sets.Count} set(s): {string.Join(", ", sets.Select(x => $"{x.Label(code)} ({x.Drawings.Count} drawings, folder {x.Dir(code)})"))}. "
            + $"From {reports.Count} dry run(s): {string.Join(", ", reports.Select(r => r.Run))}.");

        // PDFs of this code filed before (a run that filed part of the release, or another set's run).
        var before = BeforeOf(all, code, out var flat);
        if (flat.Count > 0)
        {
            say($"Nothing filed: {flat.Count} {code} PDF(s) are still in the old layout, straight in _Review (e.g. {flat[0].Path}). "
                + $"Move them into their sets' folders first: pdm release-folders {Release.SceneOf(flat[0].Path)} {code} --dry, then without --dry.");
            return 2;
        }

        // The newest check of each drawing.
        var newest = new Dictionary<string, (Item I, string Work, string Run)>();
        foreach (var (work, run, r) in reports) foreach (var i in r.Items) newest[i.FileId] = (i, work, run);

        // A re-issue: each named drawing must have a filed PDF of this code and a passed re-check; nothing else is filed.
        var previous = new Dictionary<string, Issue>();
        if (names != null)
        {
            foreach (var n in names)
            {
                var id = newest.Keys.FirstOrDefault(k => NameOf(newest[k].I.Drawing).Equals(n, Nc));
                var was = before.Values.FirstOrDefault(b => NameOf(b.DrawingPath).Equals(n, Nc));
                if (id == null) { say($"Nothing filed: {n} isn't in the re-issue check(s)."); return 2; }
                if (was == null || was.DrawingId != id) { say($"Nothing filed: {n} has no filed {code} PDF to re-issue."); return 2; }
                var gone = was.Records.Keys.Concat(was.Sets).Distinct().Where(x => !sets.Any(t => t.Label(code) == x)).ToList();
                if (gone.Count > 0) { say($"Nothing filed: {n}'s PDF is also in {string.Join(", ", gone)}; re-check it with that set's folder too."); return 2; }
                previous[id] = was; before.Remove(id);
            }
            foreach (var k in newest.Keys.Where(k => !previous.ContainsKey(k)).ToList()) newest.Remove(k);
            say($"Re-issue of {previous.Count} drawing(s) as new versions of {previous.Values.Sum(v => v.Records.Count)} PDF(s): "
                + $"{string.Join(", ", previous.Values.SelectMany(v => v.Records.Values).Select(r => r.Path).Order(StringComparer.OrdinalIgnoreCase))}. Reason: {reissue}");
        }

        var issues = new Dictionary<string, Issue>();
        var notFiled = new Dictionary<string, string>();   // drawing id -> why
        DocReader? dm = null;
        foreach (var (id, (i, work, run)) in newest.OrderBy(x => x.Value.I.Drawing, StringComparer.OrdinalIgnoreCase))
        {
            if (before.ContainsKey(id)) continue;
            if (!i.Pass) { notFiled[id] = $"failed its check in {run}: {string.Join("; ", i.Problems)}"; continue; }
            // Admin 10 Nov: checked again here, so a PDF checked before the white check existed can't be filed.
            if (i.Pdf != null && File.Exists(i.Pdf) && !(i.Accepted ?? []).Any(a => a.StartsWith(Release.WhitePrefix))) // a missing PDF: WhyNot says so
            {
                try
                {
                    var white = Release.WhiteProblems(i.Pdf!);
                    if (white.Count > 0) { notFiled[id] = $"{string.Join("; ", white)} (checked in {run}; fix and re-check it)"; continue; }
                }
                catch (Exception ex) { notFiled[id] = $"PDF white check not run: {ex.Message}"; continue; }
            }
            var pdfName = System.IO.Path.GetFileName(i.PdfKey);
            var inSets = sets.Where(t => t.Drawings.Contains(id)).ToList();
            var why = inSets.Count == 0 ? "isn't in any of the sets now"
                : WhyNot(i, byId, byPath, inSets.Select(t => $"{t.Dir(code)}/{pdfName}").ToList(), previous.ContainsKey(id));
            if (why != null) { notFiled[id] = why + $" (checked in {run})"; continue; }
            try
            {
                dm ??= new DocReader();
                var line = LineOf(dm, i, work, code);
                if (previous.ContainsKey(id)) line = line with { Notes = string.Join("; ", new[] { line.Notes, $"Re-issued {code}: {reissue}" }.Where(x => x != "")) };
                issues[id] = new Issue { DrawingId = id, DrawingPath = i.Drawing, PdfName = pdfName, Item = i, Run = run, By = name, Line = line,
                    Previous = previous.TryGetValue(id, out var p) ? p.Records : new() };
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            { notFiled[id] = $"its drawing-list line couldn't be read: {ex.Message}"; }
        }
        var dupes = issues.Values.GroupBy(x => x.PdfName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        foreach (var g in dupes) foreach (var x in g) { notFiled[x.DrawingId] = $"{g.Key} would come from {g.Count()} drawings"; issues.Remove(x.DrawingId); }

        // Which sets each PDF is in; which sets are complete.
        var everything = issues.Values.Concat(before.Values).ToDictionary(x => x.DrawingId);
        foreach (var x in issues.Values) x.Sets.AddRange(sets.Where(t => t.Drawings.Contains(x.DrawingId)).Select(t => t.Label(code)));
        // A drawing filed before whose PDF isn't in this set's folder yet (a later set of the same code that uses a shared
        // drawing): copied there from the vault, the same file. Not in a re-issue, which only re-files.
        var copies = reissue != null ? [] : sets.SelectMany(t => t.Drawings.Where(d => before.TryGetValue(d, out var b) && !b.Records.ContainsKey(t.Label(code)))
            .Select(d => (X: before[d], T: t))).ToList();
        foreach (var (x, t) in copies) if (!x.Sets.Contains(t.Label(code))) x.Sets.Add(t.Label(code));
        var missing = sets.ToDictionary(t => t.Name, t => t.Drawings.Where(d => !everything.ContainsKey(d)).ToList());
        foreach (var (id, why) in notFiled.OrderBy(x => KeyOf(byId, x.Key)))
            say($"  Not filed: {KeyOf(byId, id)}: {why}");
        foreach (var t in sets)
        {
            foreach (var d in missing[t.Name].Where(d => !notFiled.ContainsKey(d))) say($"  Not filed: {KeyOf(byId, d)}: in set {t.Name} but in none of the dry runs");
            say(missing[t.Name].Count == 0
                ? $"  Set {t.Label(code)}: complete, {t.Drawings.Count} drawings ({t.Drawings.Count(d => issues.ContainsKey(d))} to file now"
                    + $"{(copies.Count(c => c.T == t) is > 0 and var nc ? $", {nc} filed before copied into its folder" : "")})."
                : $"  Set {t.Label(code)}: INCOMPLETE, {missing[t.Name].Count} of {t.Drawings.Count} drawings have no PDF: {string.Join(", ", missing[t.Name].Select(d => System.IO.Path.GetFileName(KeyOf(byId, d))))}.");
        }
        var complete = sets.Where(t => missing[t.Name].Count == 0).ToList();
        var allComplete = complete.Count == sets.Count;
        var date = DateTime.Now.ToString("yyyy-MM-dd");
        var expected = issues.Values.Sum(x => sets.Count(t => t.Drawings.Contains(x.DrawingId))) + copies.Count;

        var outDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-release",
            $"{DateTime.Now:yyyyMMdd-HHmmss}_{code}_{(reissue != null ? "reissue-" : "")}{(dryRun ? "filing-dry" : "filed")}");
        if (reissue != null && issues.Count != previous.Count)
        {
            say($"Nothing filed: {previous.Count - issues.Count} of the {previous.Count} re-issued drawings can't be filed (above).");
            return 1;
        }
        if (!dryRun && !allComplete && !incomplete)
        {
            say($"Nothing filed: not every set is complete. Fix the drawings above and top them up, or run again with --incomplete to file the {issues.Count} PDFs that are ready (packages only for complete sets).");
            return 1;
        }

        // Build the packages and the register (into the out folder), then file.
        Directory.CreateDirectory(outDir);
        var at = Index.Now();
        var state = LocalState.Load(s.LocalRoot);
        var filed = new List<object>();
        var pdfsFiled = 0;
        try
        {
            if (!dryRun)
            {
                foreach (var x in issues.Values.OrderBy(x => x.PdfName, StringComparer.OrdinalIgnoreCase))
                    foreach (var t in sets.Where(t => t.Drawings.Contains(x.DrawingId)))
                    {
                        var rec = await FilePdfAsync(x, t, code, at, state);
                        if (rec == null) { missing[t.Name].Add(x.DrawingId); continue; }
                        x.Records[t.Label(code)] = rec; pdfsFiled++;
                        filed.Add(new { pdfKey = rec.Path, fileId = rec.FileId, versionId = rec.VersionId, drawing = x.DrawingPath });
                    }
                foreach (var (x, t) in copies)
                {
                    var from = x.Records.Values.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).First();
                    var rec = await CopyPdfAsync(from, $"{t.Dir(code)}/{x.PdfName}", t.Label(code), x.Sets, code, at, state, outDir,
                        $"Release {code} ({t.Label(code)}): {System.IO.Path.GetFileName(x.DrawingPath)}, the PDF filed for {SetOfKey(from.Path, code)}");
                    if (rec == null) { missing[t.Name].Add(x.DrawingId); continue; }
                    x.Records[t.Label(code)] = rec; pdfsFiled++;
                    filed.Add(new { pdfKey = rec.Path, fileId = rec.FileId, versionId = rec.VersionId, drawing = x.DrawingPath, copyOf = from.FileId });
                }
                complete = sets.Where(t => missing[t.Name].Count == 0).ToList();
            }
            else
            {
                foreach (var x in issues.Values.OrderBy(x => x.PdfName, StringComparer.OrdinalIgnoreCase))
                    foreach (var t in sets.Where(t => t.Drawings.Contains(x.DrawingId)))
                        say($"  would file {t.Dir(code)}/{x.PdfName}{(x.Previous.ContainsKey(t.Label(code)) ? " (new version)" : "")}  (from {x.DrawingPath}, {x.Item!.Models.Count} models; sets {string.Join(", ", x.Sets)})");
                foreach (var (x, t) in copies)
                    say($"  would copy {x.PdfName} (filed before, {string.Join(", ", x.Records.Keys)}) into {t.Dir(code)}");
            }

            var packages = new List<(string Key, string Local, string Note, string Info)>();
            // The dry run also previews an incomplete set's package (marked as such, never filed).
            // A re-issue rebuilds only the sets its PDFs are in, as new versions of their package files.
            foreach (var t in (dryRun ? sets : complete).Where(t => reissue == null || issues.Keys.Any(t.Drawings.Contains)))
            {
                var label = t.Label(code);
                var preview = !complete.Contains(t);
                var members = t.Drawings.Where(everything.ContainsKey).Select(d => everything[d]).OrderBy(x => x.Line.DrawingNo, StringComparer.OrdinalIgnoreCase).ToList();
                var setDir = System.IO.Path.Combine(outDir, $"{t.Name}_{code}");
                Directory.CreateDirectory(setDir);
                var listName = $"{t.Name}_{code}_drawing-list{(preview ? "_INCOMPLETE-preview" : "")}.xlsx";
                var zipName = $"{t.Name}_{code}_{date}{(preview ? "_INCOMPLETE-preview" : "")}.zip";
                if (reissue != null && PackageOf(all, label) is { } zipBefore) zipName = System.IO.Path.GetFileName(zipBefore);
                var listLocal = System.IO.Path.Combine(setDir, listName);
                WriteList(listLocal, t.Name, code, members, preview ? missing[t.Name].Count : 0);
                var entries = new List<(string Name, string Source)>();
                foreach (var m in members)
                    entries.Add((m.Line.Pdf, m.Item?.Pdf ?? await SourceOfAsync(m.Records.GetValueOrDefault(label) ?? m.Records.Values.First(), outDir)));
                var zipLocal = System.IO.Path.Combine(setDir, zipName);
                WriteZip(zipLocal, listLocal, entries);
                if (preview) { say($"  Preview only (set incomplete, not filed): {listName}, {zipName}"); continue; }
                var info = JsonSerializer.Serialize(new { kind = "package", code, set = label, folder = t.Folder, at, by = name, pdfs = members.Select(m => $"{t.Dir(code)}/{m.PdfName}"), reissue = Reissued(issues.Values) });
                foreach (var (n, l) in new[] { (zipName, zipLocal), (listName, listLocal) })
                    packages.Add(($"{t.Dir(code)}/{n}", l, $"Release {code} ({label}): {(n.EndsWith(".zip") ? "ZIP of the set's PDFs and drawing list" : "drawing list")}", info));
                say($"  {(dryRun ? "would file" : "filing")} {t.Dir(code)}/{zipName} ({members.Count} PDFs + drawing list) and {listName}{(reissue != null ? " (new versions)" : "")}");
            }
            foreach (var t in sets.Except(complete)) say($"  No package for {t.Label(code)}: the set isn't complete.");

            // The register of each scene the sets are in.
            var now = issues.Values.Where(x => dryRun || x.Records.Count > 0).Concat(copies.Select(c => c.X)).Distinct().ToList();
            foreach (var scene in sets.Select(t => t.Scene).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var key = Paths.RegisterKey(scene);
                var local = System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(key));
                var rows = WriteRegister(local, scene, all, now.Where(x => sets.Any(t => t.Scene.Equals(scene, Nc) && t.Drawings.Contains(x.DrawingId))).ToList());
                var info = JsonSerializer.Serialize(new { kind = "register", code, sets = sets.Select(t => t.Label(code)), at, by = name, reissue = Reissued(issues.Values) });
                packages.Add((key, local, $"Drawing register after release {code} ({string.Join(", ", sets.Select(t => t.Name))})", info));
                say($"  {(dryRun ? "would rewrite" : "rewriting")} {key} ({rows} drawings)");
            }

            if (dryRun)
            {
                foreach (var p in packages.Where(p => !Paths.IsRegister(p.Key) && byPath.ContainsKey(p.Key)))
                    say(reissue != null ? $"  {p.Key}: the real run files a new version of it." : $"  NOTE: {p.Key} is already in the vault; the real run won't replace it.");
                foreach (var t in sets.Where(t => reissue != null && !issues.Keys.Any(t.Drawings.Contains))) say($"  Set {t.Label(code)}: none of the re-issued drawings is in it; its package is left as it is.");
                say($"Dry run: {expected} PDFs would be filed ({issues.Count} drawings{(copies.Count > 0 ? $", {copies.Count} copies of PDFs filed before" : "")}; {before.Count} drawings filed before), "
                    + $"{packages.Count(p => !Paths.IsRegister(p.Key))} package files and the register; nothing written to the vault. Packages and register: {outDir}");
                return allComplete ? 0 : 1;
            }
            foreach (var p in packages)
            {
                if (Paths.IsRegister(p.Key) && filed.Count == 0) { say($"  {p.Key} not rewritten: nothing new was filed."); continue; }
                var rec = await FileOutputAsync(p.Key, p.Local, p.Note, p.Info, at, state, byPath, replace: reissue != null);
                if (rec != null) filed.Add(new { key = rec.Path, fileId = rec.FileId, versionId = rec.VersionId });
            }
        }
        finally
        {
            if (!dryRun)
            {
                state.Save(s.LocalRoot);
                File.WriteAllText(System.IO.Path.Combine(outDir, $"filed-{DateTime.Now:yyyyMMdd-HHmmss}.json"), JsonSerializer.Serialize(new { at, by = name, works = reports.Select(r => r.Work), filed }, Indented));
            }
        }
        say($"Filing done: {pdfsFiled} of {expected} PDFs filed, {filed.Count - pdfsFiled} package/register files. Download latest on each PC fetches them. Record: {outDir}");
        return pdfsFiled == expected && allComplete ? 0 : 1;
    }

    // One-off (Grant, 9 Nov: one folder per release set): a release filed in the old layout, its PDFs straight in
    // <Scene>/_Review and its packages in <Scene>/_Review/_Sets, moved into the sets' folders. Each PDF moves (same file
    // id; its history stays, the older versions at the old key) into its first set's folder and is copied (a new vault
    // file, the same bytes) into each other set's folder. Each set's ZIP and drawing list move into its folder and get a
    // new version: the list without the Notes column, the ZIP with that list and the same PDFs. Nothing is re-exported;
    // the register isn't rewritten (nothing in it changes). Every check is made first; nothing moves if one fails.
    public async Task<int> RelayoutAsync(string scene, string code, bool dryRun)
    {
        scene = scene.Replace('\\', '/').Trim('/');
        var all = await index.AllAsync();
        var byPath = all.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        static string Parent(string key) => key.Contains('/') ? key[..key.LastIndexOf('/')] : "";
        var live = all.Where(r => !r.Removed && r.ReleaseSet != null && Code(r) == code).ToList();
        var pdfs = live.Where(r => Paths.IsPdf(r.Path) && Parent(r.Path).Equals($"{scene}/_Review", Nc)).OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList();
        var pkgs = live.Where(r => Parent(r.Path).Equals($"{scene}/_Review/_Sets", Nc)).OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList();
        if (pdfs.Count == 0 && pkgs.Count == 0) { say($"Nothing to move: no {code} release files in {scene}/_Review or {scene}/_Review/_Sets."); return 0; }

        var problems = new List<string>();
        JsonObject Info(FileRecord r) => (JsonObject)JsonNode.Parse(r.ReleaseSet!)!;
        // The sets, from their packages (ZIP and list), and each set's PDFs, from the PDFs' set info.
        var setPkgs = new Dictionary<string, List<FileRecord>>();
        foreach (var r in pkgs)
        {
            var j = Info(r);
            if ((string?)j["kind"] != "package" || j["set"] is null) { problems.Add($"{r.Path}: not a set package"); continue; }
            (setPkgs.TryGetValue((string)j["set"]!, out var l) ? l : setPkgs[(string)j["set"]!] = new()).Add(r);
        }
        var labels = setPkgs.Keys.Order(StringComparer.Ordinal).ToList();
        string SetName(string label) => label.EndsWith(" " + code) ? label[..^(code.Length + 1)] : label;
        string Dir(string label) => Release.SetFolder(scene, SetName(label), code);
        var issues = new List<(FileRecord R, Issue X)>();
        foreach (var r in pdfs)
        {
            if (FromRecord(r) is not { } x) { problems.Add($"{r.Path}: its set info can't be read"); continue; }
            if (x.Sets.Count == 0) problems.Add($"{r.Path}: its set info names no set");
            foreach (var l in x.Sets.Where(l => !labels.Contains(l))) problems.Add($"{r.Path}: in set {l}, which has no package in {scene}/_Review/_Sets");
            issues.Add((r, x));
        }
        foreach (var l in labels)
        {
            var zip = setPkgs[l].FirstOrDefault(r => r.Path.EndsWith(".zip", Nc));
            if (zip == null || setPkgs[l].Count != 2 || !setPkgs[l].Any(r => r.Path.EndsWith(".xlsx", Nc))) { problems.Add($"set {l}: expected one ZIP and one drawing list in _Sets, found {string.Join(", ", setPkgs[l].Select(r => r.Path))}"); continue; }
            var listed = (Info(zip)["pdfs"] as JsonArray ?? []).Select(p => (string)p!).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var mine = issues.Where(i => i.X.Sets.Contains(l)).Select(i => i.R.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (!listed.SetEquals(mine)) problems.Add($"set {l}: its ZIP's set info lists {listed.Count} PDFs, the PDFs' set info {mine.Count} ({string.Join(", ", listed.Except(mine).Concat(mine.Except(listed)).Take(5))})");
        }
        foreach (var r in pdfs.Concat(pkgs).Where(r => r.LockedBy != null)) problems.Add($"{r.Path}: checked out by {r.LockedByName}");

        // The plan: moves, copies, package moves; no new path may be in the vault already.
        var moves = issues.Select(i => (R: i.R, X: i.X, To: $"{Dir(i.X.Sets.Order(StringComparer.Ordinal).First())}/{System.IO.Path.GetFileName(i.R.Path)}", Label: i.X.Sets.Order(StringComparer.Ordinal).First())).ToList();
        var copies = issues.SelectMany(i => i.X.Sets.Order(StringComparer.Ordinal).Skip(1).Select(l => (R: i.R, X: i.X, To: $"{Dir(l)}/{System.IO.Path.GetFileName(i.R.Path)}", Label: l))).ToList();
        var pkgMoves = labels.SelectMany(l => setPkgs[l].Select(r => (R: r, To: $"{Dir(l)}/{System.IO.Path.GetFileName(r.Path)}", Label: l))).ToList();
        foreach (var to in moves.Select(m => m.To).Concat(copies.Select(c => c.To)).Concat(pkgMoves.Select(p => p.To)))
            if (byPath.ContainsKey(to)) problems.Add($"{to} is already in the vault");

        say($"Release {code} in {scene}: {labels.Count} set(s): {string.Join(", ", labels.Select(l => $"{l} ({issues.Count(i => i.X.Sets.Contains(l))} PDFs, into {Dir(l)})"))}.");
        say($"  {moves.Count} PDFs move (same file, history kept), {copies.Count} are copied into a second set's folder ({Mb(copies.Sum(c => c.R.Size))}), "
            + $"{pkgMoves.Count} package files move and get a new version.");
        if (problems.Count > 0)
        {
            foreach (var p in problems) say($"  NOT OK: {p}");
            say("Nothing moved. Fix these first.");
            return 1;
        }

        // The new lists and ZIPs, built first (also in the dry run, to look at).
        var outDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-release",
            $"{DateTime.Now:yyyyMMdd-HHmmss}_{code}_folders{(dryRun ? "-dry" : "")}");
        Directory.CreateDirectory(outDir);
        var built = new Dictionary<string, (string List, string Zip, FileRecord ListRec, FileRecord ZipRec, List<Issue> Members)>();
        foreach (var l in labels)
        {
            var members = issues.Where(i => i.X.Sets.Contains(l)).Select(i => i.X).OrderBy(x => x.Line.DrawingNo, StringComparer.OrdinalIgnoreCase).ToList();
            var zipRec = setPkgs[l].First(r => r.Path.EndsWith(".zip", Nc));
            var listRec = setPkgs[l].First(r => r.Path.EndsWith(".xlsx", Nc));
            var dir = Path.Combine(outDir, $"{SetName(l)}_{code}");
            Directory.CreateDirectory(dir);
            var list = Path.Combine(dir, System.IO.Path.GetFileName(listRec.Path));
            var zip = Path.Combine(dir, System.IO.Path.GetFileName(zipRec.Path));
            WriteList(list, SetName(l), code, members, 0);
            var entries = new List<(string, string)>();
            foreach (var i in issues.Where(i => i.X.Sets.Contains(l)).OrderBy(i => i.X.Line.DrawingNo, StringComparer.OrdinalIgnoreCase))
                entries.Add((i.X.Line.Pdf, await SourceOfAsync(i.R, outDir)));
            WriteZip(zip, list, entries);
            // The PDFs in the new ZIP are exactly those in the ZIP filed before.
            var oldZip = await SourceOfAsync(zipRec, outDir);
            var (a, b) = (ZipPdfs(oldZip), ZipPdfs(zip));
            if (a.Count != b.Count || a.Any(kv => !b.TryGetValue(kv.Key, out var h) || h != kv.Value))
            { say($"  NOT OK: the new ZIP of {l} doesn't hold the same PDFs as the filed one. Nothing moved."); return 1; }
            built[l] = (list, zip, listRec, zipRec, members);
            say($"  {l}: new {System.IO.Path.GetFileName(list)} ({members.Count} lines, no Notes column, {Kb(new FileInfo(list).Length)}) and "
                + $"{System.IO.Path.GetFileName(zip)} ({Mb(new FileInfo(zip).Length)}; its {b.Count} PDFs identical to the filed ZIP's)");
        }
        var download = copies.Sum(c => c.R.Size) + built.Values.Sum(b => new FileInfo(b.List).Length + new FileInfo(b.Zip).Length);
        say($"  Download latest on a PC that has the {code} files now: {copies.Count} copied PDFs and {built.Count * 2} new package versions, {Mb(download)}; "
            + $"the {moves.Count + pkgMoves.Count} moved files are moved on the PC, not downloaded.");
        if (dryRun)
        {
            foreach (var m in moves) say($"  would move {m.R.Path} -> {m.To}");
            foreach (var c in copies) say($"  would copy {c.R.Path} -> {c.To}");
            foreach (var p in pkgMoves) say($"  would move {p.R.Path} -> {p.To}, then file its new version");
            say($"Dry run: nothing moved. New lists and ZIPs: {outDir}");
            return 0;
        }

        var at = Index.Now();
        var state = LocalState.Load(s.LocalRoot);
        var done = new List<object>();
        int ok = 0, want = moves.Count + copies.Count + pkgMoves.Count * 2;
        const string Why = "One folder per release set (Grant, 9 Nov)";
        try
        {
            foreach (var m in moves)
            {
                var info = Info(m.R); info["set"] = m.Label;
                var from = m.R.Path;
                if (await MoveAsync(m.R, m.To, $"{Why}: moved from {Parent(from)}", info.ToJsonString(), at, state))
                { ok++; m.X.Records[m.Label] = m.R; done.Add(new { moved = from, to = m.To, fileId = m.R.FileId, versionId = m.R.VersionId }); }
            }
            foreach (var c in copies)
            {
                var rec = await CopyPdfAsync(c.R, c.To, c.Label, c.X.Sets, code, at, state, outDir,
                    $"{Why}: copy for {c.Label} of {c.R.Path}");
                if (rec != null) { ok++; done.Add(new { copied = c.R.Path, to = c.To, fileId = rec.FileId, versionId = rec.VersionId }); }
            }
            foreach (var p in pkgMoves)
            {
                var from = p.R.Path;
                if (!await MoveAsync(p.R, p.To, $"{Why}: moved from {Parent(from)}", null, at, state)) continue;
                ok++; done.Add(new { moved = from, to = p.To, fileId = p.R.FileId, versionId = p.R.VersionId });
                var b = built[p.Label];
                var j = Info(p.R);
                j["at"] = at; j["by"] = name;
                j["pdfs"] = new JsonArray(b.Members.Select(x => (JsonNode)JsonValue.Create($"{Dir(p.Label)}/{x.PdfName}")!).ToArray());
                j["remade"] = $"{Why}; drawing list without the Notes column (Grant, 9 Nov); PDFs unchanged";
                var isZip = p.To.EndsWith(".zip", Nc);
                byPath[p.To] = p.R;
                var rec = await FileOutputAsync(p.To, isZip ? b.Zip : b.List, $"{Why}; drawing list without the Notes column. Release {code} ({p.Label}): "
                    + (isZip ? "ZIP of the set's PDFs and drawing list" : "drawing list"), j.ToJsonString(), at, state, byPath, replace: true);
                if (rec != null) { ok++; done.Add(new { key = rec.Path, fileId = rec.FileId, versionId = rec.VersionId }); }
            }
        }
        finally
        {
            state.Save(s.LocalRoot);
            File.WriteAllText(Path.Combine(outDir, $"filed-{DateTime.Now:yyyyMMdd-HHmmss}.json"), JsonSerializer.Serialize(new { at, by = name, scene, code, done }, Indented));
        }
        say($"Done: {ok} of {want} steps ({moves.Count} moves, {copies.Count} copies, {pkgMoves.Count} package moves and their new versions). Download latest on each PC follows them. Record: {outDir}");
        return ok == want ? 0 : 1;
    }

    static string Mb(long bytes) => $"{bytes / 1048576.0:0.0} MB";
    static string Kb(long bytes) => $"{bytes / 1024.0:0} KB";

    static string KeyOf(Dictionary<string, FileRecord> byId, string id) => byId.TryGetValue(id, out var r) ? r.Path : id;

    // What a re-issue changed, for the packages' and register's set info.
    object? Reissued(IEnumerable<Issue> now) => reissue == null ? null
        : new { reason = reissue, pdfs = now.SelectMany(x => x.Previous.Values).Select(r => new { pdfKey = r.Path, previousVersionId = r.VersionId }) };

    // A set's ZIP as filed before (its set info), e.g. APEX/SC08/_Review/LS-08X-06_0A/LS-08X-06_0A_2026-11-09.zip.
    static string? PackageOf(List<FileRecord> all, string set) => all.Where(r => !r.Removed && r.ReleaseSet != null
            && r.Path.EndsWith(".zip", Nc) && Paths.InReleaseFolder(r.Path))
        .FirstOrDefault(r => { try { var j = JsonNode.Parse(r.ReleaseSet!); return (string?)j?["kind"] == "package" && (string?)j["set"] == set; } catch (JsonException) { return false; } })?.Path;
    static string? Code(FileRecord r) { try { return (string?)JsonNode.Parse(r.ReleaseSet!)?["code"]; } catch (JsonException) { return null; } }

    // The set whose folder holds a release file: APEX/SC08/_Review/LS-08X-06_0A/x.pdf -> "LS-08X-06 0A"; null when the
    // file isn't in a set's folder (the old layout, straight in _Review).
    static string? SetOfKey(string key, string code)
    {
        var p = key.Split('/');
        if (p.Length < 4 || !Paths.InReleaseFolder(key)) return null;
        var f = p[^2];
        return f.Length > code.Length + 1 && f.EndsWith("_" + code, Nc) && !f.StartsWith('_') ? $"{f[..^(code.Length + 1)]} {code}" : null;
    }

    // The PDFs of this code filed before, one per drawing with its copies by set (oldest first, so the newest line wins).
    // flat: PDFs of this code still in the old layout.
    static Dictionary<string, Issue> BeforeOf(List<FileRecord> all, string code, out List<FileRecord> flat)
    {
        var result = new Dictionary<string, Issue>();
        flat = new();
        foreach (var r in all.Where(r => !r.Removed && r.ReleaseSet != null && Paths.IsPdf(r.Path) && Code(r) == code).OrderBy(r => r.UpdatedAt, StringComparer.Ordinal))
        {
            if (FromRecord(r) is not { } x) continue;
            if (SetOfKey(r.Path, code) is not { } label) { flat.Add(r); continue; }
            if (!result.TryGetValue(x.DrawingId, out var iss)) result[x.DrawingId] = iss = x;
            else { iss.Line = x.Line; iss.By = x.By; foreach (var t in x.Sets.Where(t => !iss.Sets.Contains(t))) iss.Sets.Add(t); }
            iss.Records[label] = r;
        }
        return result;
    }

    // A PDF filed before: its drawing-list line and sets from its set info (records without them are left out).
    static Issue? FromRecord(FileRecord r)
    {
        try
        {
            var j = JsonNode.Parse(r.ReleaseSet!) as JsonObject;
            if (j?["line"] is not JsonObject line || j["drawing"] is not JsonObject d) return null;
            var iss = new Issue { DrawingId = (string)d["fileId"]!, DrawingPath = (string)d["path"]!, PdfName = System.IO.Path.GetFileName(r.Path), By = (string?)j["by"] ?? "", Line = line.Deserialize<Line>(Read)! };
            iss.Sets.AddRange((j["sets"] as JsonArray ?? []).Select(x => (string)x!));
            return iss;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NullReferenceException) { return null; }
    }

    // A filed file's bytes on this PC: its P:\ copy when that is the vault's, else downloaded into the out folder.
    async Task<string> SourceOfAsync(FileRecord r, string outDir)
    {
        var local = Paths.LocalOf(s.LocalRoot, r.Path);
        if (File.Exists(local) && Vault.Md5(local) == r.ETag) return local;
        var to = System.IO.Path.Combine(outDir, "vault-copies", r.FileId, System.IO.Path.GetFileName(r.Path));
        if (File.Exists(to) && Vault.Md5(to) == r.ETag) return to;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(to)!);
        await vault.DownloadAsync(r.Path, r.VersionId, to);
        if (Vault.Md5(to) != r.ETag) throw new IOException($"the vault's copy of {r.Path} doesn't match its MD5");
        return to;
    }

    // Files one drawing's PDF into one set's folder (a new file, or the next version of a re-issued one); null when it couldn't be.
    async Task<FileRecord?> FilePdfAsync(Issue x, SetDef t, string code, string at, LocalState state)
    {
        var i = x.Item!;
        var key = $"{t.Dir(code)}/{x.PdfName}";
        var prev = x.Previous.GetValueOrDefault(t.Label(code));
        var local = Paths.LocalOf(s.LocalRoot, key);
        try
        {
            if (prev == null && File.Exists(local) && Vault.Md5(local) != Vault.Md5(i.Pdf!)) { say($"  Not filed: {key}: a different file is already at {local}"); return null; }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(local)!);
            if (prev != null && File.Exists(local)) { Paths.SetReadOnly(local, false); File.Delete(local); }
            if (!File.Exists(local)) File.Copy(i.Pdf!, local);
            var set = new
            {
                code, sets = x.Sets, set = t.Label(code), run = x.Run, at, by = name,
                drawing = new { path = i.Drawing, fileId = i.FileId, versionId = i.VersionId },
                models = i.Models.Select(m => new { path = m.Path, fileId = m.FileId, versionId = m.VersionId }),
                accepted = i.Accepted is { Count: > 0 } ? i.Accepted : null,
                line = x.Line,
                reissue = prev == null ? null : new { reason = reissue, previousVersionId = prev.VersionId, previousAt = prev.UpdatedAt }
            };
            var note = $"{(prev != null ? $"Re-issued {code}: {reissue}. " : "")}Release {code} ({t.Label(code)}): {System.IO.Path.GetFileName(i.Drawing)}";
            var rec = prev ?? new FileRecord { FileId = Guid.NewGuid().ToString(), Path = key };
            rec.UpdatedAt = at; rec.UpdatedBy = name; rec.Note = note; rec.Size = new FileInfo(local).Length; rec.ReleaseSet = JsonSerializer.Serialize(set);
            (rec.VersionId, rec.ETag) = await vault.UploadAsync(local, rec.Path, rec.FileId);
            if (prev == null) await index.AddAsync(rec); else await index.PutAsync(rec);
            state.Record(rec.Path, local, rec.FileId, rec.VersionId, rec.ETag);
            Paths.SetReadOnly(local, true);
            say($"  Filed {rec.Path}{(prev != null ? " (new version)" : "")}");
            await Activity.LogAsync(vault, new ActivityEvent(at, name, "released", rec.Path, rec.FileId, Email: email, VersionId: rec.VersionId, Note: rec.Note), say);
            return rec;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException)
        {
            say($"  Not filed: {key}: {ex.Message}");
            return null;
        }
    }

    // A PDF filed before, copied into another set's folder: a new vault file with the same bytes and the same set info
    // (its own set, the drawing's sets now, and which file it was copied from).
    async Task<FileRecord?> CopyPdfAsync(FileRecord from, string key, string label, List<string> sets, string code, string at, LocalState state, string outDir, string note)
    {
        var local = Paths.LocalOf(s.LocalRoot, key);
        try
        {
            var src = await SourceOfAsync(from, outDir);
            if (File.Exists(local) && Vault.Md5(local) != from.ETag) { say($"  Not copied: {key}: a different file is already at {local}"); return null; }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(local)!);
            if (!File.Exists(local)) File.Copy(src, local);
            var j = (JsonObject)JsonNode.Parse(from.ReleaseSet!)!;
            j["set"] = label;
            j["sets"] = new JsonArray(sets.Select(x => (JsonNode)JsonValue.Create(x)!).ToArray());
            j["copyOf"] = new JsonObject { ["path"] = from.Path, ["fileId"] = from.FileId, ["versionId"] = from.VersionId, ["at"] = at };
            var rec = new FileRecord { FileId = Guid.NewGuid().ToString(), Path = key, UpdatedAt = at, UpdatedBy = name, Note = note, Size = new FileInfo(local).Length, ReleaseSet = j.ToJsonString() };
            (rec.VersionId, rec.ETag) = await vault.UploadAsync(local, rec.Path, rec.FileId);
            if (rec.ETag != from.ETag) throw new IOException($"the copy's MD5 ({rec.ETag}) isn't the filed PDF's ({from.ETag})");
            await index.AddAsync(rec);
            state.Record(rec.Path, local, rec.FileId, rec.VersionId, rec.ETag);
            Paths.SetReadOnly(local, true);
            say($"  Filed {rec.Path} (copy of {from.Path})");
            await Activity.LogAsync(vault, new ActivityEvent(at, name, "released", rec.Path, rec.FileId, Email: email, VersionId: rec.VersionId, Note: note), say);
            return rec;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException)
        {
            say($"  Not copied: {key}: {ex.Message}");
            return null;
        }
    }

    // Moves a release file to a new path in the vault (as pdm move does: same file id, the S3 object copied inside the
    // bucket, the move recorded; the versions before it stay at the old key), and this PC's copy with it.
    // info: new set info for it, if it changes. Release files are never checked out, so no lock is taken.
    async Task<bool> MoveAsync(FileRecord f, string to, string note, string? info, string at, LocalState state)
    {
        var from = f.Path;
        try
        {
            var fresh = await index.GetAsync(f.FileId);
            if (fresh == null || !fresh.Path.Equals(from, Nc) || fresh.VersionId != f.VersionId || fresh.Removed || fresh.LockedBy != null)
            { say($"  Not moved: {from}: it changed in the vault just now"); return false; }
            var (versionId, etag) = await vault.CopyAsync(from, f.VersionId, to);
            if (etag != f.ETag) throw new IOException($"the copy in the vault doesn't match (ETag {etag}, expected {f.ETag})");
            var m = new MoveEntry(from, to, at, name, note, f.ETag);
            f.Path = to; f.VersionId = versionId; f.UpdatedAt = at; f.UpdatedBy = name; f.Note = note; f.Moves.Add(m);
            if (info != null) f.ReleaseSet = info;
            await index.PutAsync(f);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or Amazon.Runtime.AmazonServiceException)
        {
            say($"  Not moved: {from}: {ex.Message}");
            return false;
        }
        say($"  Moved {from} -> {to}");
        await Activity.LogAsync(vault, new ActivityEvent(at, name, "moved", to, f.FileId, Email: email, VersionId: f.VersionId, Note: note, From: from), say);
        var localFrom = Paths.LocalOf(s.LocalRoot, from);
        var localTo = Paths.LocalOf(s.LocalRoot, to);
        try
        {
            if (File.Exists(localFrom) && Vault.Md5(localFrom) == f.ETag && !File.Exists(localTo))
            {
                Paths.SetReadOnly(localFrom, false);
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(localTo)!);
                File.Move(localFrom, localTo);
                state.Files.Remove(from);
                state.Record(to, localTo, f.FileId, f.VersionId, f.ETag);
                Paths.SetReadOnly(localTo, true);
                Paths.RemoveEmptyFolders(s.LocalRoot, localFrom);
            }
            else say($"  {from}: this PC's copy wasn't moved (missing or not the vault's); Download latest sorts it out.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            say($"  Moved in the vault, but not on this PC: {ex.Message}. Download latest moves it later.");
        }
        return true;
    }

    // Files a package file (new; never replaced unless replace: a re-issue or the move into set folders) or the register (a new version of it).
    async Task<FileRecord?> FileOutputAsync(string key, string built, string note, string info, string at, LocalState state, Dictionary<string, FileRecord> byPath, bool replace)
    {
        var local = Paths.LocalOf(s.LocalRoot, key);
        byPath.TryGetValue(key, out var existing);
        try
        {
            if (existing != null && !Paths.IsRegister(key) && !replace) { say($"  Not filed: {key} is already in the vault; release packages stay as issued."); return null; }
            if (reissue != null) note = $"Re-issued: {reissue}. {note}";
            if (existing?.LockedBy != null) { say($"  Not filed: {key} is checked out by {existing.LockedByName}; the register wasn't rewritten."); return null; }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(local)!);
            if (File.Exists(local)) { Paths.SetReadOnly(local, false); File.Delete(local); }
            File.Copy(built, local);
            var rec = existing ?? new FileRecord { FileId = Guid.NewGuid().ToString(), Path = key };
            rec.UpdatedAt = at; rec.UpdatedBy = name; rec.Note = note; rec.Size = new FileInfo(local).Length; rec.ReleaseSet = info;
            (rec.VersionId, rec.ETag) = await vault.UploadAsync(local, rec.Path, rec.FileId);
            if (existing == null) await index.AddAsync(rec); else await index.PutAsync(rec);
            state.Record(rec.Path, local, rec.FileId, rec.VersionId, rec.ETag);
            Paths.SetReadOnly(local, true);
            say($"  Filed {rec.Path}{(existing != null ? " (new version)" : "")}");
            await Activity.LogAsync(vault, new ActivityEvent(at, name, Activity.KindOf(rec.Path, existing == null), rec.Path, rec.FileId, Email: email, VersionId: rec.VersionId, Note: note), say);
            return rec;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException)
        {
            say($"  Not filed: {key}: {ex.Message}");
            return null;
        }
    }

    // The drawing-list line: what the title block shows (the model and configuration its properties come from, read
    // from the exact files the PDF was made from), the sheets and their sizes from the PDF itself.
    static Line LineOf(DocReader dm, Item i, string work, string code)
    {
        string Copy(string key) => System.IO.Path.Combine(work, "copy", key.Replace('/', '\\'));
        var drawing = Copy(i.Drawing);
        if (!File.Exists(drawing)) throw new IOException($"no copy of the drawing in {work}");
        string title = "", partNo = "";
        var notes = new List<string>();
        if (dm.TitleBlockModel(drawing) is { } tb)
        {
            var m = i.Models.FirstOrDefault(x => System.IO.Path.GetFileName(x.Path).Equals(tb.Model, Nc));
            if (m != null && File.Exists(Copy(m.Path)))
            {
                title = dm.ModelProp(Copy(m.Path), tb.Config, "Description") ?? "";
                partNo = dm.ModelProp(Copy(m.Path), tb.Config, "PartNo") ?? "";
            }
            else notes.Add($"Title block model {tb.Model} not found");
        }
        else notes.Add("No model shown on sheet 1");
        if (title == "") notes.Add("No description on the model");
        var pages = PdfPages(i.Pdf!);
        // Grant's exceptions in plain words; the full reasons are in the set info.
        foreach (var a in i.Accepted ?? [])
            notes.Add(a.StartsWith("checked out by ") ? $"Issued from the vault version; drawing checked out to {a["checked out by ".Length..a.IndexOf(" since ")]} at the time"
                : a.StartsWith(Release.WhitePrefix) ? "White text or lines in the PDF accepted by Grant"
                : "Revision checked visually by Grant");
        notes = notes.Distinct().ToList();
        return new Line(System.IO.Path.GetFileNameWithoutExtension(i.Drawing), title, code, pages.Count,
            string.Join(", ", pages.Select(p => SizeOf(p[0], p[1])).Distinct()), partNo, StatusOf(code),
            DateTime.Now.ToString("yyyy-MM-dd"), System.IO.Path.GetFileName(i.PdfKey), string.Join("; ", notes));
    }

    // Lettered codes (0A, 1A ...) are checking issues; numbers are formal releases.
    static string StatusOf(string code) => Release.Official(code) ? "Issued" : "Internal Check";

    static string SizeOf(double w, double h)
    {
        var (a, b) = (Math.Min(w, h), Math.Max(w, h));
        foreach (var (n, x, y) in new[] { ("A0", 841, 1189), ("A1", 594, 841), ("A2", 420, 594), ("A3", 297, 420), ("A4", 210, 297) })
            if (Math.Abs(a - x) <= 3 && Math.Abs(b - y) <= 3) return n;
        return $"{w:0} x {h:0} mm";
    }

    static List<double[]> PdfPages(string pdf)
    {
        var script = System.IO.Path.Combine(AppContext.BaseDirectory, "release", "pdfpages.py");
        var psi = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
        foreach (var a in new[] { "-I", script, pdf }) psi.ArgumentList.Add(a);
        using var p = Process.Start(psi)!;
        var output = p.StandardOutput.ReadToEnd(); var err = p.StandardError.ReadToEnd();
        p.WaitForExit();
        if (p.ExitCode != 0) throw new IOException($"PDF pages not read: {err.Trim().Split('\n')[^1]}");
        return JsonSerializer.Deserialize<List<double[]>>(output)!;
    }

    // No Notes column (Grant, 9 Nov: "too much info"); notes stay in the set info.
    static readonly string[] ListHeaders = ["Drawing No", "Title (Description)", "REV", "Sheets", "Size", "Part No", "Status", "Issue date", "PDF file name"];

    // Grant, 9 Nov: a title line and the issue line above the header; date and issuer from the set info.
    static void WriteList(string path, string setName, string code, List<Issue> members, int missing)
    {
        static string Show(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture) : iso;
        var dates = string.Join(", ", members.Select(m => m.Line.Issued).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(Show));
        var by = string.Join(", ", members.Select(m => m.By).Where(x => x != "").Distinct());
        string[] above = [$"{setName} Drawing List - Issue {code}", $"Issue date: {dates}   Issued by: {by}   Status: {StatusOf(code)}"];
        Xlsx.Write(path, $"{setName} {code}", ListHeaders,
            members.Select(m => new[] { m.Line.DrawingNo, m.Line.Title, m.Line.Rev, m.Line.Sheets.ToString(), m.Line.Size, m.Line.PartNo, m.Line.Status, m.Line.Issued, m.Line.Pdf }),
            [16, 42, 6, 7, 6, 16, 14, 12, 22], Template("drawing-list.xlsx"), above);
    }

    // A layout Grant has saved (release\templates next to the app), if there is one.
    static string Template(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "release", "templates", name);

    // A set's ZIP: its drawing list and its PDFs (name in the ZIP, file on this PC).
    static void WriteZip(string zipPath, string listPath, IEnumerable<(string Name, string Source)> pdfs)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(listPath, System.IO.Path.GetFileName(listPath), CompressionLevel.Optimal);
        foreach (var (n, src) in pdfs) zip.CreateEntryFromFile(src, n, CompressionLevel.Optimal);
    }

    // The PDFs in a ZIP: name -> MD5.
    static Dictionary<string, string> ZipPdfs(string zipPath)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in zip.Entries.Where(e => e.Name.EndsWith(".pdf", Nc)))
        {
            using var st = e.Open();
            result[e.FullName] = Convert.ToHexString(System.Security.Cryptography.MD5.HashData(st)).ToLowerInvariant();
        }
        return result;
    }

    static readonly string[] RegisterHeaders = ["Drawing No", "Title (Description)", "Latest REV", "Status", "Issue date", "Sets", "PDF file name", "Part No", "Vault folder"];

    // Every drawing of the scene once (its own drawings in the vault, and the shared drawings issued into it), with its
    // latest issue. Built from the vault's set info plus this run's PDFs.
    int WriteRegister(string path, string scene, List<FileRecord> all, List<Issue> now)
    {
        var issued = new List<(string DrawingId, string DrawingPath, string At, Line Line, List<string> Sets)>();
        foreach (var r in all.Where(r => !r.Removed && r.ReleaseSet != null && Paths.IsPdf(r.Path) && Release.SceneOf(r.Path).Equals(scene, Nc)))
            if (FromRecord(r) is { } iss) issued.Add((iss.DrawingId, iss.DrawingPath, r.UpdatedAt, iss.Line, iss.Sets));
        foreach (var x in now) issued.Add((x.DrawingId, x.DrawingPath, "9999", x.Line, x.Sets));
        var latest = issued.GroupBy(x => x.DrawingId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.At, StringComparer.Ordinal).Last());
        var drawings = all.Where(r => !r.Removed && r.Path.EndsWith(".slddrw", Nc)
                && r.Path.StartsWith(scene + "/", Nc))
            .Select(r => (Id: r.FileId, r.Path)).Concat(latest.Values.Select(l => (Id: l.DrawingId, Path: l.DrawingPath)))
            .GroupBy(x => x.Id).Select(g => g.First()).ToList();
        var byId = all.ToDictionary(r => r.FileId);
        var rows = drawings.Select(d =>
        {
            var p = byId.TryGetValue(d.Id, out var rec) ? rec.Path : d.Path;
            var folder = p.Contains('/') ? p[..p.LastIndexOf('/')] : "";
            if (!latest.TryGetValue(d.Id, out var l)) return new[] { System.IO.Path.GetFileNameWithoutExtension(p), "", "", "Not issued", "", "", "", "", folder };
            return new[] { l.Line.DrawingNo, l.Line.Title, l.Line.Rev, l.Line.Status, l.Line.Issued, string.Join(", ", l.Sets), l.Line.Pdf, l.Line.PartNo, folder };
        }).OrderBy(r => r[0], StringComparer.OrdinalIgnoreCase).ToList();
        Xlsx.Write(path, $"{scene.Split('/')[^1]} register", RegisterHeaders, rows, [16, 42, 10, 14, 12, 26, 22, 16, 32], Template("drawing-register.xlsx"));
        return rows.Count;
    }

    // Null when this PDF can be filed as checked: everything it was made from is still the vault's latest.
    // keys: where it goes (one per set it's in).
    static string? WhyNot(Item i, Dictionary<string, FileRecord> byId, Dictionary<string, FileRecord> byPath, List<string> keys, bool reissued = false)
    {
        if (i.Pdf == null || !File.Exists(i.Pdf)) return "its PDF isn't in the work folder";
        foreach (var k in keys)
        {
            if (!Paths.IsPdf(k) || !Paths.InReleaseFolder(k)) return $"{k} isn't a PDF in a release folder";
            if (byPath.TryGetValue(k, out var there) && (!reissued || there.Removed)) return $"already filed: {k} is in the vault{(there.Removed ? " (removed)" : "")}";
        }
        if (!byId.TryGetValue(i.FileId, out var d) || d.Removed) return "the drawing is no longer in the vault";
        if (d.VersionId != i.VersionId) return $"the drawing has changed in the vault since the dry run ({d.UpdatedBy}, {d.UpdatedAt}); run the dry run again";
        // A check-out Grant accepted in the dry run (--accept-checkout) is fine while it's still the same person's.
        if (d.LockedBy != null && !(i.Accepted ?? []).Any(a => a.StartsWith($"checked out by {d.LockedByName} since ", StringComparison.Ordinal)))
            return $"the drawing is checked out by {d.LockedByName}";
        foreach (var m in i.Models)
        {
            if (!byId.TryGetValue(m.FileId, out var r) || r.Removed) return $"{m.Path} is no longer in the vault";
            if (r.VersionId != m.VersionId) return $"{r.Path} has changed in the vault since the dry run ({r.UpdatedBy}, {r.UpdatedAt}); run the dry run again";
        }
        return null;
    }
}
