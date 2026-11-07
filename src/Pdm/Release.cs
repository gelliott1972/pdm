using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Pdm;

// The release step (docs\briefs\release-0A.md): a set of drawings and a revision code; each drawing is checked,
// exported to PDF with Dev's SwCmd, and its revision checked (R4). The dry run stops there; the real run (later, from
// the page) files the PDFs into the scene's _Review folder and records the set on each PDF's index entry.
public sealed partial class Release(Settings s, List<FileRecord> all, Action<string> say)
{
    public sealed class Item
    {
        public required FileRecord Drawing { get; init; }
        public required string PdfKey { get; init; }       // where the real run files it, e.g. APEX/SC08/_Review/08A06-HBA-01_0A.pdf
        public List<FileRecord> Models { get; } = new();    // everything the drawing references, all the way down
        public List<string> Problems { get; } = new();      // any of these stops this drawing
        public List<string> Notes { get; } = new();         // worth knowing, doesn't stop it
        public string? Pdf { get; set; }                    // the exported PDF in the work folder
        public bool Pass => Problems.Count == 0;
        public string Name => Path.GetFileName(Drawing.Path);
    }

    // Where shared (APEX/GNRIC, _Shared) drawings in a scene's set are filed: "scene" (the set's scene) or "own"
    // (their own scene, e.g. APEX/GNRIC/_Review). Grant decides on Desk A5; either works.
    public string SharedTo { get; set; } = "scene";
    // The role whose SwCmd lock this run works under (--as), so it can run while that role holds SOLIDWORKS.
    public string? SwAs { get; set; }
    public string SwCmd { get; set; } =@"C:\code\pdm\tools\SwCmd\bin\SwCmd.exe";

    // R1/R2: official releases are numbers (1, 2, ...); checking versions before them add a letter (0A, 0B, 1A, ...).
    [GeneratedRegex("^(0[A-Z]|[1-9][0-9]*[A-Z]?)$")] private static partial Regex CodePattern();
    public static bool CodeOk(string code) => CodePattern().IsMatch(code);
    public static bool Official(string code) => code.All(char.IsDigit);
    static int BaseNumber(string code) => int.Parse(Official(code) ? code : code[..^1]);

    // A scene is the first two folders of the vault path (Admin 7 Nov, 4a): APEX/SC08/... -> APEX/SC08.
    public static string SceneOf(string key) { var p = key.Split('/'); return p.Length > 2 ? p[0] + "/" + p[1] : p[0]; }
    public static string PdfName(string drawingKey, string code) => Path.GetFileNameWithoutExtension(drawingKey) + "_" + code + ".pdf";

    // 1 and 2 of the brief: the set's drawings and the checks that need no SOLIDWORKS.
    public List<Item> Plan(string folderKey, string code, bool withShared)
    {
        folderKey = folderKey.Trim('/') + "/";
        var byId = all.ToDictionary(r => r.FileId);
        var inFolder = all.Where(r => r.Path.StartsWith(folderKey, StringComparison.OrdinalIgnoreCase)).ToList();
        var drawings = inFolder.Where(IsDrawing).ToList();
        var setScene = SceneOf(folderKey + "x");

        if (withShared)
        {
            // Shared drawings whose model is used by anything in the folder.
            var used = new HashSet<string>(inFolder.SelectMany(r => Closure(r, byId)).Select(r => r.FileId));
            drawings.AddRange(all.Where(r => IsDrawing(r) && Paths.IsShared(r.Path) && !drawings.Contains(r)
                && r.Refs.Any(used.Contains)));
        }

        var items = new List<Item>();
        foreach (var d in drawings.OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase))
        {
            var scene = Paths.IsShared(d.Path) && SharedTo == "scene" ? setScene : SceneOf(d.Path);
            var item = new Item { Drawing = d, PdfKey = $"{scene}/_Review/{PdfName(d.Path, code)}" };
            item.Models.AddRange(Closure(d, byId).Where(r => r.FileId != d.FileId));
            Check(item);
            items.Add(item);
        }
        return items;
    }

    static bool IsDrawing(FileRecord r) => r.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase);

    static IEnumerable<FileRecord> Closure(FileRecord start, Dictionary<string, FileRecord> byId)
    {
        var seen = new HashSet<string>(); var todo = new Stack<FileRecord>([start]);
        while (todo.Count > 0)
        {
            var f = todo.Pop();
            if (!seen.Add(f.FileId)) continue;
            yield return f;
            foreach (var id in f.Refs) if (byId.TryGetValue(id, out var r)) todo.Push(r);
        }
    }

    void Check(Item item)
    {
        var d = item.Drawing;
        if (d.LockedBy != null) item.Problems.Add($"checked out by {d.LockedByName} since {d.LockedAt}");
        if (Stale(d) is { } why) item.Problems.Add($"this PC's copy {why}");
        foreach (var m in d.MissingRefs) item.Problems.Add($"refers to {m}, which isn't in the vault");
        foreach (var m in item.Models)
        {
            if (Stale(m) is { } mwhy) item.Problems.Add($"{m.Path}: this PC's copy {mwhy}");
            foreach (var x in m.MissingRefs) item.Problems.Add($"{m.Path} refers to {x}, which isn't in the vault");
            if (m.LockedBy != null) item.Notes.Add($"{m.Path} is checked out by {m.LockedByName}; the PDF shows the vault's version");
        }
        if (all.Any(r => r.Path.Equals(item.PdfKey, StringComparison.OrdinalIgnoreCase)))
            item.Problems.Add($"already released: {item.PdfKey} is in the vault");
    }

    // Null when this PC has exactly the vault's latest version.
    string? Stale(FileRecord f)
    {
        var local = Paths.LocalOf(s.LocalRoot, f.Path);
        if (!File.Exists(local)) return "is missing (Download latest first)";
        try { return Vault.Md5(local) == f.ETag ? null : "isn't the vault's latest (Download latest, or check in your changes)"; }
        catch (IOException ex) { return $"can't be read ({ex.Message})"; }
    }

    // 3 and 4 of the brief: export each drawing that passed the checks, then check the revision (R4) and the PDF.
    // copyRoot (dry run): the set is copied there first and exported from the copies.
    public async Task ExportAndCheckAsync(List<Item> items, string code, string work, string? copyRoot)
    {
        var pdfDir = Path.Combine(work, "pdf");
        Directory.CreateDirectory(pdfDir);
        var n = 0;
        foreach (var item in items)
        {
            n++;
            if (!item.Pass) { say($"({n} of {items.Count}) {item.Name}: skipped, {item.Problems.Count} problem(s) before export"); continue; }
            var drawing = Paths.LocalOf(s.LocalRoot, item.Drawing.Path);
            if (copyRoot != null)
            {
                foreach (var f in item.Models.Append(item.Drawing))
                {
                    var to = Path.Combine(copyRoot, f.Path.Replace('/', '\\'));
                    if (File.Exists(to)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    File.Copy(Paths.LocalOf(s.LocalRoot, f.Path), to);
                    File.SetAttributes(to, FileAttributes.Normal);
                }
                drawing = Path.Combine(copyRoot, item.Drawing.Path.Replace('/', '\\'));
            }
            say($"({n} of {items.Count}) {item.Name}: reading the drawing…");
            var info = await RunAsync(["drawing-info", drawing, "--json"], TimeSpan.FromMinutes(n == 1 ? 5 : 3));
            if (info.Code >= 2) { item.Problems.Add("SwCmd drawing-info failed: " + Last(info.Output)); continue; }
            JsonObject? json = null;
            try { json = JsonNode.Parse(info.Out) as JsonObject; } catch (JsonException) { }
            if (json == null) { item.Problems.Add("SwCmd drawing-info gave no JSON: " + Last(info.Output)); continue; }
            CheckRevision(item, json, code);

            var pdf = Path.Combine(pdfDir, Path.GetFileName(item.PdfKey));
            if (File.Exists(pdf)) File.Delete(pdf);
            say($"({n} of {items.Count}) {item.Name}: exporting the PDF…");
            var export = await RunAsync(["export-pdf", drawing, pdf], TimeSpan.FromMinutes(3));
            if (export.Code >= 2 || !File.Exists(pdf)) { item.Problems.Add("SwCmd export-pdf failed: " + Last(export.Output)); continue; }
            if (export.Code == 1) item.Notes.Add("export-pdf reported problems: " + Last(export.Output));
            item.Pdf = pdf;
            CheckPdf(item, json, code);
        }
    }

    // R4: the title-block REV on every sheet = the code; numbered release: newest revision-table row = the number;
    // lettered version: the table lists official numbers only, so its newest row is the base number (none before 1).
    void CheckRevision(Item item, JsonObject json, string code)
    {
        var sheets = json["sheets"] as JsonArray ?? [];
        if (sheets.Count == 0) item.Problems.Add("no sheets found");
        foreach (var sh in sheets)
        {
            var rev = ((string?)sh?["rev"] ?? "").Trim();
            if (!rev.Equals(code, StringComparison.Ordinal))
                item.Problems.Add($"sheet {(string?)sh?["name"]}: title block REV is \"{rev}\", not {code}");
        }
        var rows = (json["revTable"] as JsonArray ?? []).Select(r => ((string?)r?["rev"] ?? "").Trim()).Where(r => r != "").ToList();
        var want = BaseNumber(code);
        if (want == 0)
        {
            if (rows.Count > 0) item.Problems.Add($"revision table has row(s) {string.Join(", ", rows)}, but {code} comes before the first official release, so it should be empty");
        }
        else if (rows.Count == 0) item.Problems.Add($"revision table is empty or missing; its newest row should be {want}");
        else if (rows[^1] != want.ToString()) item.Problems.Add($"revision table's newest row is {rows[^1]}, not {want}");
        foreach (var p in json["problems"] as JsonArray ?? []) item.Notes.Add("SwCmd: " + (string?)p);
    }

    // Reads the PDF back: one page per sheet, and the code on every page.
    void CheckPdf(Item item, JsonObject json, string code)
    {
        var pages = PdfText(item.Pdf!, out var why);
        if (pages == null) { item.Notes.Add($"PDF not read back: {why}"); return; }
        var sheets = (json["sheets"] as JsonArray ?? []).Count;
        if (pages.Count != sheets) item.Problems.Add($"PDF has {pages.Count} page(s) but the drawing has {sheets} sheet(s)");
        var token = new Regex($"(?<![A-Za-z0-9]){Regex.Escape(code)}(?![A-Za-z0-9])");
        for (var i = 0; i < pages.Count; i++)
            if (!token.IsMatch(pages[i])) item.Problems.Add($"PDF page {i + 1}: {code} not found in its text");
        if (Official(code)) item.Notes.Add($"PDF read-back for a number ({code}) only proves the number appears somewhere on each page");
    }

    // Text per page via PyMuPDF (release\pdftext.py next to the app). No .NET PDF reader is used: the usual one isn't
    // available on NuGet at the moment.
    List<string>? PdfText(string pdf, out string why)
    {
        why = "";
        var script = Path.Combine(AppContext.BaseDirectory, "release", "pdftext.py");
        try
        {
            var psi = new ProcessStartInfo("python") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true, StandardOutputEncoding = Encoding.UTF8 };
            foreach (var a in new[] { "-I", script, pdf }) psi.ArgumentList.Add(a);
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEnd(); var err = p.StandardError.ReadToEnd();
            p.WaitForExit();
            if (p.ExitCode != 0) { why = Last(err); return null; }
            return JsonSerializer.Deserialize<List<string>>(output);
        }
        catch (Exception ex) { why = $"no Python with PyMuPDF on this PC ({ex.Message})"; return null; }
    }

    // Out is stdout (drawing-info --json prints only its JSON there); Output is both, in order, for messages.
    record Run(int Code, string Out, string Output);

    async Task<Run> RunAsync(string[] args, TimeSpan timeout)
    {
        if (!File.Exists(SwCmd)) return new Run(2, "", $"SwCmd isn't built on this PC ({SwCmd})");
        var psi = new ProcessStartInfo(SwCmd) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        if (SwAs != null) { psi.ArgumentList.Add("--as"); psi.ArgumentList.Add(SwAs); }
        var output = new StringBuilder(); var stdout = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) { output.AppendLine(e.Data); stdout.AppendLine(e.Data); } };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        p.Start(); p.BeginOutputReadLine(); p.BeginErrorReadLine();
        using var cts = new CancellationTokenSource(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: false); } catch { } // SwCmd only; SOLIDWORKS is left alone
            lock (output) return new Run(3, stdout.ToString(), output + $"timed out after {timeout.TotalMinutes:0} minutes");
        }
        p.WaitForExit();
        lock (output) return new Run(p.ExitCode, stdout.ToString(), output.ToString());
    }

    static string Last(string output)
    {
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return lines.Length == 0 ? "(no output)" : string.Join(" / ", lines.TakeLast(3));
    }

    // The pass/fail list, as text and JSON, in the work folder.
    public string Report(List<Item> items, string folderKey, string code, bool dry, string work)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{(dry ? "DRY RUN" : "RELEASE")} {code}: {folderKey} ({items.Count} drawings), {DateTime.Now:d MMM yyyy HH:mm}");
        sb.AppendLine($"Passed {items.Count(i => i.Pass)}, failed {items.Count(i => !i.Pass)}.");
        foreach (var i in items)
        {
            sb.AppendLine();
            sb.AppendLine($"{(i.Pass ? "PASS" : "FAIL")}  {i.Drawing.Path}  ->  {i.PdfKey}");
            foreach (var p in i.Problems) sb.AppendLine($"      problem: {p}");
            foreach (var x in i.Notes) sb.AppendLine($"      note: {x}");
        }
        var text = sb.ToString();
        Directory.CreateDirectory(work);
        File.WriteAllText(Path.Combine(work, "report.txt"), text);
        var json = items.Select(i => new
        {
            drawing = i.Drawing.Path, fileId = i.Drawing.FileId, versionId = i.Drawing.VersionId, pdfKey = i.PdfKey, pdf = i.Pdf,
            pass = i.Pass, problems = i.Problems, notes = i.Notes,
            models = i.Models.Select(m => new { path = m.Path, fileId = m.FileId, versionId = m.VersionId })
        });
        File.WriteAllText(Path.Combine(work, "report.json"), JsonSerializer.Serialize(new { folder = folderKey, code, dry, items = json }, new JsonSerializerOptions { WriteIndented = true }));
        return text;
    }
}
