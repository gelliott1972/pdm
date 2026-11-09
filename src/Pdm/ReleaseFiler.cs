using System.Diagnostics;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pdm;

// The release step's real run (docs\briefs\release-0A.md, steps 5 and 6, and Grant's release packages, 9 Nov). From one
// or more dry-run work folders (a first run and its top-ups) it files, in one step:
//   - each checked PDF into the scene's _Review folder, a new vault file with its set info on the index entry (code,
//     sets, when, who, the drawing and model versions it was made from, and its drawing-list line);
//   - per set (each folder of the release is a set, e.g. "LS-08X-06 0A"): a ZIP of the set's PDFs and its Excel drawing
//     list, and the list on its own, into <Scene>/_Review/_Sets;
//   - the scene's drawing register, <Scene>/<scene>_Drawing_Register.xlsx, rewritten.
// The newest check of each drawing wins. A drawing whose drawing or models changed in the vault since its check is left
// out and listed. A shared drawing is in every set whose models use it; its PDF is filed once. Everything is built from
// the set info, never by hand. By default nothing is filed unless every set is complete (--incomplete files what's ready;
// packages are then made only for complete sets). The dry run writes the packages and register to a work folder only.
public sealed class ReleaseFiler(Settings s, Index index, Vault vault, string name, Action<string> say)
{
    public sealed record Model(string Path, string FileId, string VersionId);
    public sealed record Item(string Drawing, string FileId, string VersionId, string PdfKey, string? Pdf, bool Pass,
        List<string> Problems, List<Model> Models, List<string>? Accepted = null);
    sealed record Report(string Folder, string Code, bool Dry, List<Item> Items);

    // One drawing-list line, as recorded on the PDF's set info.
    public sealed record Line(string DrawingNo, string Title, string Rev, int Sheets, string Size, string PartNo, string Status,
        string Issued, string Pdf, string Notes);
    sealed record SetDef(string Name, string Folder, string Scene, HashSet<string> Drawings);
    // A PDF of this release: about to be filed (Work set) or filed before (Record set).
    sealed class Issue
    {
        public required string DrawingId { get; init; }
        public required string DrawingPath { get; init; }
        public required string PdfKey { get; init; }
        public required Line Line { get; set; }
        public List<string> Sets { get; } = new();
        public Item? Item { get; init; }
        public string? Run { get; init; }
        public string By { get; init; } = "";          // who issued it (the set info's "by")
        public FileRecord? Record { get; set; }
    }

    static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };
    static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public async Task<int> RunAsync(IList<string> works, bool dryRun, bool incomplete)
    {
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
        var folders = reports.SelectMany(r => r.R.Folder.Split(" + ")).Select(f => f.Trim().Trim('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();

        var all = await index.AllAsync();
        var byId = all.ToDictionary(r => r.FileId);
        var byPath = all.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // The sets: each folder, with the drawings in it now (its own and the shared drawings its models use).
        var plan = new Release(s, all.Where(r => !r.Removed).ToList(), _ => { });
        var sets = folders.Select(f => new SetDef(f.Split('/')[^1], f, Release.SceneOf(f + "/x"),
            plan.Plan(f, code, withShared: true).Select(x => x.Drawing.FileId).ToHashSet())).ToList();
        say($"Release {code}: {sets.Count} set(s): {string.Join(", ", sets.Select(x => $"{x.Name} {code} ({x.Drawings.Count} drawings)"))}. "
            + $"From {reports.Count} dry run(s): {string.Join(", ", reports.Select(r => r.Run))}.");

        // PDFs of this code filed before (a run that filed part of the release).
        var before = new Dictionary<string, Issue>();
        foreach (var r in all.Where(r => !r.Removed && r.ReleaseSet != null && Paths.IsPdf(r.Path)))
            if (FromRecord(r) is { } iss && Code(r) == code) before[iss.DrawingId] = iss;

        // The newest check of each drawing.
        var newest = new Dictionary<string, (Item I, string Work, string Run)>();
        foreach (var (work, run, r) in reports) foreach (var i in r.Items) newest[i.FileId] = (i, work, run);

        var issues = new Dictionary<string, Issue>();
        var notFiled = new Dictionary<string, string>();   // drawing id -> why
        DocReader? dm = null;
        foreach (var (id, (i, work, run)) in newest.OrderBy(x => x.Value.I.Drawing, StringComparer.OrdinalIgnoreCase))
        {
            if (before.ContainsKey(id)) continue;
            if (!i.Pass) { notFiled[id] = $"failed its check in {run}: {string.Join("; ", i.Problems)}"; continue; }
            var why = WhyNot(i, byId, byPath);
            if (why == null && !sets.Any(x => x.Drawings.Contains(id))) why = "isn't in any of the sets now";
            if (why != null) { notFiled[id] = why + $" (checked in {run})"; continue; }
            try
            {
                dm ??= new DocReader();
                issues[id] = new Issue { DrawingId = id, DrawingPath = i.Drawing, PdfKey = i.PdfKey, Item = i, Run = run, By = name, Line = LineOf(dm, i, work, code) };
            }
            catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException)
            { notFiled[id] = $"its drawing-list line couldn't be read: {ex.Message}"; }
        }
        var dupes = issues.Values.GroupBy(x => x.PdfKey, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        foreach (var g in dupes) foreach (var x in g) { notFiled[x.DrawingId] = $"{g.Key} would come from {g.Count()} drawings"; issues.Remove(x.DrawingId); }

        // Which sets each PDF is in; which sets are complete.
        var everything = issues.Values.Concat(before.Values).ToDictionary(x => x.DrawingId);
        foreach (var x in issues.Values) x.Sets.AddRange(sets.Where(t => t.Drawings.Contains(x.DrawingId)).Select(t => $"{t.Name} {code}"));
        var missing = sets.ToDictionary(t => t.Name, t => t.Drawings.Where(d => !everything.ContainsKey(d)).ToList());
        foreach (var (id, why) in notFiled.OrderBy(x => KeyOf(byId, x.Key)))
            say($"  Not filed: {KeyOf(byId, id)}: {why}");
        foreach (var t in sets)
        {
            foreach (var d in missing[t.Name].Where(d => !notFiled.ContainsKey(d))) say($"  Not filed: {KeyOf(byId, d)}: in set {t.Name} but in none of the dry runs");
            say(missing[t.Name].Count == 0
                ? $"  Set {t.Name} {code}: complete, {t.Drawings.Count} drawings ({t.Drawings.Count(d => issues.ContainsKey(d))} to file now)."
                : $"  Set {t.Name} {code}: INCOMPLETE, {missing[t.Name].Count} of {t.Drawings.Count} drawings have no PDF: {string.Join(", ", missing[t.Name].Select(d => System.IO.Path.GetFileName(KeyOf(byId, d))))}.");
        }
        var complete = sets.Where(t => missing[t.Name].Count == 0).ToList();
        var allComplete = complete.Count == sets.Count;
        var date = DateTime.Now.ToString("yyyy-MM-dd");

        var outDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-release",
            $"{DateTime.Now:yyyyMMdd-HHmmss}_{code}_{(dryRun ? "filing-dry" : "filed")}");
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
                foreach (var x in issues.Values.OrderBy(x => x.PdfKey, StringComparer.OrdinalIgnoreCase))
                {
                    var rec = await FilePdfAsync(x, code, at, state);
                    if (rec == null) { foreach (var t in sets.Where(t => t.Drawings.Contains(x.DrawingId))) missing[t.Name].Add(x.DrawingId); continue; }
                    x.Record = rec; pdfsFiled++;
                    filed.Add(new { pdfKey = rec.Path, fileId = rec.FileId, versionId = rec.VersionId, drawing = x.DrawingPath });
                }
                complete = sets.Where(t => missing[t.Name].Count == 0).ToList();
            }
            else foreach (var x in issues.Values.OrderBy(x => x.PdfKey, StringComparer.OrdinalIgnoreCase))
                say($"  would file {x.PdfKey}  (from {x.DrawingPath}, {x.Item!.Models.Count} models; sets {string.Join(", ", x.Sets)})");

            var packages = new List<(string Key, string Local, string Note, string Info)>();
            // The dry run also previews an incomplete set's package (marked as such, never filed).
            foreach (var t in dryRun ? sets : complete)
            {
                var preview = !complete.Contains(t);
                var members = t.Drawings.Where(everything.ContainsKey).Select(d => everything[d]).OrderBy(x => x.Line.DrawingNo, StringComparer.OrdinalIgnoreCase).ToList();
                var setDir = System.IO.Path.Combine(outDir, "_Sets");
                Directory.CreateDirectory(setDir);
                var listName = $"{t.Name}_{code}_drawing-list{(preview ? "_INCOMPLETE-preview" : "")}.xlsx";
                var zipName = $"{t.Name}_{code}_{date}{(preview ? "_INCOMPLETE-preview" : "")}.zip";
                var listLocal = System.IO.Path.Combine(setDir, listName);
                WriteList(listLocal, t, code, date, members, preview ? missing[t.Name].Count : 0);
                if (preview) { say($"  Preview only (set incomplete, not filed): {listName}, {zipName}"); await WriteZipAsync(System.IO.Path.Combine(setDir, zipName), listLocal, members, outDir); continue; }
                var zipLocal = System.IO.Path.Combine(setDir, zipName);
                await WriteZipAsync(zipLocal, listLocal, members, outDir);
                var info = JsonSerializer.Serialize(new { kind = "package", code, set = $"{t.Name} {code}", folder = t.Folder, at, by = name, pdfs = members.Select(m => m.PdfKey) });
                foreach (var (n, l) in new[] { (zipName, zipLocal), (listName, listLocal) })
                    packages.Add(($"{t.Scene}/_Review/_Sets/{n}", l, $"Release {code} ({t.Name} {code}): {(n.EndsWith(".zip") ? "ZIP of the set's PDFs and drawing list" : "drawing list")}", info));
                say($"  {(dryRun ? "would file" : "filing")} {t.Scene}/_Review/_Sets/{zipName} ({members.Count} PDFs + drawing list) and {listName}");
            }
            foreach (var t in sets.Except(complete)) say($"  No package for {t.Name} {code}: the set isn't complete.");

            // The register of each scene the sets are in.
            foreach (var scene in sets.Select(t => t.Scene).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                var key = Paths.RegisterKey(scene);
                var local = System.IO.Path.Combine(outDir, System.IO.Path.GetFileName(key));
                var rows = WriteRegister(local, scene, all, issues.Values.Where(x => dryRun || x.Record != null).ToList(), date);
                var info = JsonSerializer.Serialize(new { kind = "register", code, sets = sets.Select(t => $"{t.Name} {code}"), at, by = name });
                packages.Add((key, local, $"Drawing register after release {code} ({string.Join(", ", sets.Select(t => t.Name))})", info));
                say($"  {(dryRun ? "would rewrite" : "rewriting")} {key} ({rows} drawings)");
            }

            if (dryRun)
            {
                foreach (var p in packages.Where(p => !Paths.IsRegister(p.Key) && byPath.ContainsKey(p.Key))) say($"  NOTE: {p.Key} is already in the vault; the real run won't replace it.");
                say($"Dry run: {issues.Count} PDFs would be filed ({before.Count} filed before), {packages.Count(p => !Paths.IsRegister(p.Key))} package files and the register; nothing written to the vault. Packages and register: {outDir}");
                return allComplete ? 0 : 1;
            }
            foreach (var p in packages)
            {
                if (Paths.IsRegister(p.Key) && filed.Count == 0) { say($"  {p.Key} not rewritten: nothing new was filed."); continue; }
                var rec = await FileOutputAsync(p.Key, p.Local, p.Note, p.Info, at, state, byPath);
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
        say($"Filing done: {pdfsFiled} of {issues.Count} PDFs filed, {filed.Count - pdfsFiled} package/register files. Download latest on each PC fetches them. Record: {outDir}");
        return pdfsFiled == issues.Count && allComplete ? 0 : 1;
    }

    static string KeyOf(Dictionary<string, FileRecord> byId, string id) => byId.TryGetValue(id, out var r) ? r.Path : id;
    static string? Code(FileRecord r) { try { return (string?)JsonNode.Parse(r.ReleaseSet!)?["code"]; } catch (JsonException) { return null; } }

    // A PDF filed before: its drawing-list line and sets from its set info (records without them are left out).
    static Issue? FromRecord(FileRecord r)
    {
        try
        {
            var j = JsonNode.Parse(r.ReleaseSet!) as JsonObject;
            if (j?["line"] is not JsonObject line || j["drawing"] is not JsonObject d) return null;
            var iss = new Issue { DrawingId = (string)d["fileId"]!, DrawingPath = (string)d["path"]!, PdfKey = r.Path, Record = r, By = (string?)j["by"] ?? "", Line = line.Deserialize<Line>(Read)! };
            iss.Sets.AddRange((j["sets"] as JsonArray ?? []).Select(x => (string)x!));
            return iss;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or NullReferenceException) { return null; }
    }

    // Files one PDF; null when it couldn't be.
    async Task<FileRecord?> FilePdfAsync(Issue x, string code, string at, LocalState state)
    {
        var i = x.Item!;
        var local = Paths.LocalOf(s.LocalRoot, x.PdfKey);
        try
        {
            if (File.Exists(local) && Vault.Md5(local) != Vault.Md5(i.Pdf!)) { say($"  Not filed: {x.PdfKey}: a different file is already at {local}"); return null; }
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(local)!);
            if (!File.Exists(local)) File.Copy(i.Pdf!, local);
            var set = new
            {
                code, sets = x.Sets, set = string.Join(" + ", x.Sets), run = x.Run, at, by = name,
                drawing = new { path = i.Drawing, fileId = i.FileId, versionId = i.VersionId },
                models = i.Models.Select(m => new { path = m.Path, fileId = m.FileId, versionId = m.VersionId }),
                accepted = i.Accepted is { Count: > 0 } ? i.Accepted : null,
                line = x.Line
            };
            var rec = new FileRecord
            {
                FileId = Guid.NewGuid().ToString(), Path = x.PdfKey, UpdatedAt = at, UpdatedBy = name,
                Note = $"Release {code} ({string.Join(", ", x.Sets)}): {System.IO.Path.GetFileName(i.Drawing)}",
                Size = new FileInfo(local).Length, ReleaseSet = JsonSerializer.Serialize(set)
            };
            (rec.VersionId, rec.ETag) = await vault.UploadAsync(local, rec.Path, rec.FileId);
            await index.AddAsync(rec);
            state.Record(rec.Path, local, rec.FileId, rec.VersionId, rec.ETag);
            Paths.SetReadOnly(local, true);
            say($"  Filed {rec.Path}");
            return rec;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException)
        {
            say($"  Not filed: {x.PdfKey}: {ex.Message}");
            return null;
        }
    }

    // Files a package file (new; never replaced) or the register (a new version of it).
    async Task<FileRecord?> FileOutputAsync(string key, string built, string note, string info, string at, LocalState state, Dictionary<string, FileRecord> byPath)
    {
        var local = Paths.LocalOf(s.LocalRoot, key);
        byPath.TryGetValue(key, out var existing);
        try
        {
            if (existing != null && !Paths.IsRegister(key)) { say($"  Not filed: {key} is already in the vault; release packages stay as issued."); return null; }
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
            var m = i.Models.FirstOrDefault(x => System.IO.Path.GetFileName(x.Path).Equals(tb.Model, StringComparison.OrdinalIgnoreCase));
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

    static readonly string[] ListHeaders = ["Drawing No", "Title (Description)", "REV", "Sheets", "Size", "Part No", "Status", "Issue date", "PDF file name", "Notes"];

    // Grant, 9 Nov: a title line and the issue line above the header; date and issuer from the set info.
    static void WriteList(string path, SetDef t, string code, string date, List<Issue> members, int missing)
    {
        static string Show(string iso) => DateTime.TryParse(iso, out var d) ? d.ToString("dd-MMM-yyyy", System.Globalization.CultureInfo.InvariantCulture) : iso;
        var dates = string.Join(", ", members.Select(m => m.Line.Issued).Distinct().OrderBy(x => x, StringComparer.Ordinal).Select(Show));
        var by = string.Join(", ", members.Select(m => m.By).Where(x => x != "").Distinct());
        string[] above = [$"{t.Name} Drawing List - Issue {code}", $"Issue date: {dates}   Issued by: {by}   Status: {StatusOf(code)}"];
        Xlsx.Write(path, $"{t.Name} {code}", ListHeaders,
            members.Select(m => new[] { m.Line.DrawingNo, m.Line.Title, m.Line.Rev, m.Line.Sheets.ToString(), m.Line.Size, m.Line.PartNo, m.Line.Status, m.Line.Issued, m.Line.Pdf, m.Line.Notes }),
            [16, 42, 6, 7, 6, 16, 14, 12, 22, 50], Template("drawing-list.xlsx"), above);
    }

    // A layout Grant has saved (release\templates next to the app), if there is one.
    static string Template(string name) => System.IO.Path.Combine(AppContext.BaseDirectory, "release", "templates", name);

    // The set's PDFs (the checked files; for PDFs filed before, the vault's copy) and its drawing list.
    async Task WriteZipAsync(string zipPath, string listPath, List<Issue> members, string outDir)
    {
        if (File.Exists(zipPath)) File.Delete(zipPath);
        using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        zip.CreateEntryFromFile(listPath, System.IO.Path.GetFileName(listPath), CompressionLevel.Optimal);
        foreach (var m in members)
        {
            var src = m.Item?.Pdf;
            if (src == null)
            {
                var rec = m.Record!;
                src = Paths.LocalOf(s.LocalRoot, rec.Path);
                if (!File.Exists(src) || Vault.Md5(src) != rec.ETag)
                {
                    src = System.IO.Path.Combine(outDir, "vault-copies", System.IO.Path.GetFileName(rec.Path));
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(src)!);
                    await vault.DownloadAsync(rec.Path, rec.VersionId, src);
                }
            }
            zip.CreateEntryFromFile(src, m.Line.Pdf, CompressionLevel.Optimal);
        }
    }

    static readonly string[] RegisterHeaders = ["Drawing No", "Title (Description)", "Latest REV", "Status", "Issue date", "Sets", "PDF file name", "Part No", "Vault folder"];

    // Every drawing of the scene once (its own drawings in the vault, and the shared drawings issued into it), with its
    // latest issue. Built from the vault's set info plus this run's PDFs.
    int WriteRegister(string path, string scene, List<FileRecord> all, List<Issue> now, string date)
    {
        var issued = new List<(string DrawingId, string DrawingPath, string At, Line Line, List<string> Sets)>();
        foreach (var r in all.Where(r => !r.Removed && r.ReleaseSet != null && Paths.IsPdf(r.Path) && Release.SceneOf(r.Path).Equals(scene, StringComparison.OrdinalIgnoreCase)))
            if (FromRecord(r) is { } iss) issued.Add((iss.DrawingId, iss.DrawingPath, r.UpdatedAt, iss.Line, iss.Sets));
        foreach (var x in now.Where(x => Release.SceneOf(x.PdfKey).Equals(scene, StringComparison.OrdinalIgnoreCase)))
            issued.Add((x.DrawingId, x.DrawingPath, "9999", x.Line, x.Sets));
        var latest = issued.GroupBy(x => x.DrawingId).ToDictionary(g => g.Key, g => g.OrderBy(x => x.At, StringComparer.Ordinal).Last());
        var drawings = all.Where(r => !r.Removed && r.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)
                && r.Path.StartsWith(scene + "/", StringComparison.OrdinalIgnoreCase))
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
    static string? WhyNot(Item i, Dictionary<string, FileRecord> byId, Dictionary<string, FileRecord> byPath)
    {
        if (i.Pdf == null || !File.Exists(i.Pdf)) return "its PDF isn't in the work folder";
        if (!Paths.IsPdf(i.PdfKey) || !Paths.InReleaseFolder(i.PdfKey)) return $"{i.PdfKey} isn't a PDF in a release folder";
        if (byPath.TryGetValue(i.PdfKey, out var there)) return $"already filed: {i.PdfKey} is in the vault{(there.Removed ? " (removed)" : "")}";
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
