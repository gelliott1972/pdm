using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Pdm;

// Make PDFs only, no release (Grant, 20 Nov): PDFs of vault drawings that change nothing. No revision change, no
// release, no check-out or check-in, nothing written to P:\ or the vault. Grant's PC only (SwCmd and the SOLIDWORKS
// lock are there). Each run: Documents\PDM PDFs\<yyyy-MM-dd>_PDF_Run[_2...]\, one PDF per drawing named
// <run date>_<drawing>_<rev>P.pdf (P = preview; a draft 0A* shows as 0AP, the PDF keeps the *; no revision: _P),
// and list.xlsx beside them. The drawings and their models are copied out of P:\ first and exported from the copies.
public sealed class PdfRun(Settings s, List<FileRecord> all, Action<string> say)
{
    public sealed class Item
    {
        public required FileRecord Drawing { get; init; }
        public List<FileRecord> Models { get; } = new();
        public List<FileRecord> NotLatest { get; } = new(); // this PC's copy isn't the vault's latest (drawing or a model)
        public string? Skip { get; set; }                   // why no PDF was made
        public string Rev { get; set; } = "";               // as the title block prints it, e.g. 0A*
        public string? Pdf { get; set; }                    // the PDF made, in the run folder
        public List<string> Notes { get; } = new();
        public string Name => Path.GetFileNameWithoutExtension(Drawing.Path);
    }

    public string SwCmd { get; set; } = @"C:\code\pdm\tools\SwCmd\bin\SwCmd.exe";
    public const string Role = "grant"; // Grant presses the button: his SOLIDWORKS role

    static bool IsDrawing(FileRecord r) => r.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase);

    // The drawings named: a drawing itself, a folder (ending /) for every drawing under it, and a part or assembly for
    // its drawing (same name).
    public List<Item> Plan(IEnumerable<string> keys)
    {
        var live = all.Where(r => !r.Removed).ToList();
        var byId = live.ToDictionary(r => r.FileId);
        var drawings = new List<FileRecord>();
        foreach (var k in keys)
        {
            if (k.EndsWith('/')) drawings.AddRange(live.Where(r => IsDrawing(r) && r.Path.StartsWith(k, StringComparison.OrdinalIgnoreCase)));
            else if (live.FirstOrDefault(r => r.Path.Equals(k, StringComparison.OrdinalIgnoreCase)) is { } f)
            {
                if (IsDrawing(f)) drawings.Add(f);
                else drawings.AddRange(live.Where(r => IsDrawing(r) && r.Refs.Contains(f.FileId)
                    && Path.GetFileNameWithoutExtension(r.Path).Equals(Path.GetFileNameWithoutExtension(f.Path), StringComparison.OrdinalIgnoreCase)));
            }
            else say($"Not in the vault: {k}");
        }
        var items = new List<Item>();
        foreach (var d in drawings.DistinctBy(r => r.FileId).OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase))
        {
            var item = new Item { Drawing = d };
            item.Models.AddRange(Closure(d, byId).Where(r => r.FileId != d.FileId));
            foreach (var m in d.MissingRefs) item.Notes.Add($"refers to {m}, which isn't in the vault");
            foreach (var f in item.Models.Prepend(d))
            {
                var local = Paths.LocalOf(s.LocalRoot, f.Path);
                if (!File.Exists(local)) { item.NotLatest.Add(f); continue; }
                try { if (Vault.Md5(local) != f.ETag) item.NotLatest.Add(f); }
                catch (IOException) { item.NotLatest.Add(f); }
            }
            items.Add(item);
        }
        return items;
    }

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

    // Files (drawings and models) whose copy on this PC isn't the vault's latest, once each, for the page's box.
    public static List<object> NotLatest(List<Item> items, Settings s, Func<FileRecord, bool> mine) =>
        items.SelectMany(i => i.NotLatest).DistinctBy(f => f.FileId).OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase)
            .Select(f => (object)new
            {
                path = f.Path,
                why = !File.Exists(Paths.LocalOf(s.LocalRoot, f.Path)) ? "not on this PC"
                    : mine(f) ? "checked out to you, with changes" : "older than the vault's",
                usedBy = items.Where(i => i.NotLatest.Contains(f)).Select(i => i.Name).ToList()
            }).ToList();

    // Documents\PDM PDFs\2026-11-20_PDF_Run, then _2, _3 ... for later runs the same day.
    public static string NewRunFolder(DateTime day)
    {
        var root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PDM PDFs");
        var name = $"{day:yyyy-MM-dd}_PDF_Run";
        var path = Path.Combine(root, name);
        for (var n = 2; Directory.Exists(path); n++) path = Path.Combine(root, $"{name}_{n}");
        Directory.CreateDirectory(path);
        return path;
    }

    // Copies each drawing and its models out of P:\ (one copy per file), so SOLIDWORKS exports from copies and P:\ is
    // only read. A file that's missing on this PC stops its drawing. Run while nothing else touches P:\ (the page does
    // one request at a time).
    public void Copy(List<Item> items, string copyRoot)
    {
        foreach (var item in items.Where(i => i.Skip == null))
        {
            var missing = item.Models.Prepend(item.Drawing).Where(f => !File.Exists(Paths.LocalOf(s.LocalRoot, f.Path))).ToList();
            if (missing.Count > 0) { item.Skip = "not on this PC: " + string.Join(", ", missing.Select(f => f.Path)); continue; }
            try
            {
                foreach (var f in item.Models.Prepend(item.Drawing))
                {
                    var to = Path.Combine(copyRoot, f.Path.Replace('/', '\\'));
                    if (File.Exists(to)) continue;
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    File.Copy(Paths.LocalOf(s.LocalRoot, f.Path), to);
                    File.SetAttributes(to, FileAttributes.Normal);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { item.Skip = "couldn't copy it from P:\\: " + ex.Message; }
        }
    }

    // Takes SOLIDWORKS for the run: null when it's ours (and whether we took the lock, so we give it back), or why not.
    public async Task<(string? Why, bool Took)> TakeSolidWorksAsync(int count)
    {
        if (!File.Exists(SwCmd)) return ($"SwCmd isn't built on this PC ({SwCmd})", false);
        var lk = await RunAsync(["lock", "--purpose", $"PDM Make PDFs ({count} drawing{(count == 1 ? "" : "s")})"], TimeSpan.FromSeconds(30));
        if (lk.Code != 0) return ($"SOLIDWORKS is in use, so nothing was done: {Last(lk.Output).Replace("IN USE: ", "").Replace(" No lock taken.", "").Replace(", a round", "").TrimEnd('.')}. Try again when it is free.", false);
        var took = !lk.Output.Contains("renewed", StringComparison.OrdinalIgnoreCase);
        var st = await RunAsync(["status"], TimeSpan.FromMinutes(2));
        var open = st.Output.Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.StartsWith("Open documents:"));
        if (st.Code == 0 && open != null && int.TryParse(open["Open documents:".Length..].Trim(), out var n) && n > 0)
        {
            if (took) await GiveBackAsync();
            return ($"SOLIDWORKS has {n} document{(n == 1 ? "" : "s")} open, so nothing was done. In SOLIDWORKS: File > Close All (save first if you need to), then press Make PDFs again.", false);
        }
        if (st.Code != 0) { if (took) await GiveBackAsync(); return ($"SOLIDWORKS didn't answer, so nothing was done: {Last(st.Output)}", false); }
        return (null, took);
    }

    public async Task GiveBackAsync() => await RunAsync(["unlock"], TimeSpan.FromSeconds(30));

    // Exports each drawing from its copy: reads the title block's REV, then the PDF under its final name.
    public async Task ExportAsync(List<Item> items, string copyRoot, string runFolder, DateTime day, Action<int, int, string> progress)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var todo = items.Where(i => i.Skip == null).ToList();
        var n = 0;
        foreach (var item in todo)
        {
            n++;
            progress(n, todo.Count, item.Name);
            var drawing = Path.Combine(copyRoot, item.Drawing.Path.Replace('/', '\\'));
            var info = await RunAsync(["drawing-info", drawing, "--json"], TimeSpan.FromMinutes(n == 1 ? 5 : 3));
            JsonObject? json = null;
            try { json = JsonNode.Parse(info.Out) as JsonObject; } catch (JsonException) { }
            if (info.Code >= 2 || json == null) { item.Skip = "SOLIDWORKS couldn't read the drawing: " + Last(info.Output); continue; }
            var revs = (json["sheets"] as JsonArray ?? []).Select(sh => ((string?)sh?["rev"] ?? "").Trim()).ToList();
            item.Rev = revs.FirstOrDefault() ?? "";
            if (revs.Distinct().Count() > 1) item.Notes.Add($"sheets show different revisions ({string.Join(", ", revs)}); the name uses sheet 1's");

            var name = FileName(day, item.Name, item.Rev);
            for (var k = 2; !used.Add(name); k++) name = FileName(day, item.Name, item.Rev, k);
            var pdf = Path.Combine(runFolder, name);
            var export = await RunAsync(["export-pdf", drawing, pdf], TimeSpan.FromMinutes(3));
            if (export.Code >= 2 || !File.Exists(pdf)) { item.Skip = "the PDF wasn't made: " + Last(export.Output); continue; }
            if (export.Code == 1) item.Notes.Add("SOLIDWORKS reported: " + Last(export.Output));
            item.Pdf = pdf;
        }
    }

    // 2026-11-20_08X04-BKP-07_0AP.pdf: the * of a draft and anything a file name can't hold are left out.
    public static string FileName(DateTime day, string drawing, string rev, int copy = 1)
    {
        var bad = Path.GetInvalidFileNameChars();
        var r = new string(rev.Where(c => c != '*' && !bad.Contains(c) && !char.IsWhiteSpace(c)).ToArray());
        return $"{day:yyyy-MM-dd}_{drawing}_{r}P{(copy > 1 ? "_" + copy : "")}.pdf";
    }

    public static bool Draft(string rev) => rev.Contains('*');

    // list.xlsx beside the PDFs: one row per drawing, made or not.
    public void WriteList(List<Item> items, string runFolder, bool usedLocal)
    {
        var nz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
        string When(string at) => at == "" ? "" : TimeZoneInfo.ConvertTimeFromUtc(Activity.Utc(at), nz).ToString("yyyy-MM-dd HH:mm");
        var rows = items.Select(i =>
        {
            var local = usedLocal && i.NotLatest.Count > 0;
            var result = i.Pdf != null ? (local ? "made from the local copy, not vault latest: " + string.Join(", ", i.NotLatest.Select(f => Path.GetFileName(f.Path))) : "made")
                : "skipped: " + i.Skip;
            if (i.Notes.Count > 0) result += " (" + string.Join("; ", i.Notes) + ")";
            return new[]
            {
                i.Name, i.Pdf == null ? "" : Path.GetFileName(i.Pdf), i.Rev, i.Pdf == null && i.Rev == "" ? "" : Draft(i.Rev) ? "yes" : "no",
                i.Drawing.Path, When(i.Drawing.UpdatedAt), i.Drawing.UpdatedBy, i.Drawing.VersionId, i.Drawing.Note, result
            };
        });
        Xlsx.Write(Path.Combine(runFolder, "list.xlsx"), "PDFs",
            ["Drawing", "PDF", "Revision", "Draft", "Vault path", "Version time (NZ)", "Checked in by", "S3 version id", "Note", "Result"],
            rows, [18, 34, 9, 7, 52, 17, 16, 34, 40, 50]);
    }

    record Run(int Code, string Out, string Output);

    async Task<Run> RunAsync(string[] args, TimeSpan timeout)
    {
        if (!File.Exists(SwCmd)) return new Run(2, "", $"SwCmd isn't built on this PC ({SwCmd})");
        var psi = new ProcessStartInfo(SwCmd) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        psi.ArgumentList.Add("--as"); psi.ArgumentList.Add(Role);
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

    // A run after the page's choice. Start (inside a page request, so nothing else touches P:\ meanwhile): take SOLIDWORKS,
    // copy the files out of P:\, make the run folder. Finish (in the background): export, list, give SOLIDWORKS back,
    // delete the copies.
    public sealed class Job
    {
        public required List<Item> Items { get; init; }
        public required bool UseLocal { get; init; }
        public required bool Took { get; init; }
        public required string Work { get; init; }
        public required string RunFolder { get; init; }
        public required DateTime Day { get; init; }
        public string CopyRoot => Path.Combine(Work, "copy");
    }

    // Null when nothing was done (why is said).
    public async Task<Job?> StartAsync(List<Item> items, bool useLocal)
    {
        foreach (var i in items.Where(i => i.NotLatest.Count > 0 && !useLocal))
            i.Skip = "not the vault's latest on this PC: " + string.Join(", ", i.NotLatest.Select(f => f.Path));
        var count = items.Count(i => i.Skip == null);
        if (count == 0) { say(items.Count == 0 ? "No drawings to make PDFs of." : "No PDFs made: " + string.Join("; ", items.Select(i => $"{i.Name}: {i.Skip}"))); return null; }
        var (why, took) = await TakeSolidWorksAsync(count);
        if (why != null) { say(why); return null; }
        var day = DateTime.Now;
        var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-pdf", $"{day:yyyyMMdd-HHmmss}");
        try
        {
            var job = new Job { Items = items, UseLocal = useLocal, Took = took, Work = work, RunFolder = NewRunFolder(day), Day = day };
            Copy(items, job.CopyRoot);
            return job;
        }
        catch (Exception)
        {
            if (took) await GiveBackAsync();
            try { Directory.Delete(work, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
            throw;
        }
    }

    public async Task FinishAsync(Job job, Action<int, int, string> progress)
    {
        var items = job.Items;
        try
        {
            await ExportAsync(items, job.CopyRoot, job.RunFolder, job.Day, progress);
            WriteList(items, job.RunFolder, job.UseLocal);
        }
        finally
        {
            if (job.Took) await GiveBackAsync();
            try { Directory.Delete(job.Work, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
        var made = items.Count(i => i.Pdf != null);
        say($"Made {made} PDF{(made == 1 ? "" : "s")} in {job.RunFolder}");
        foreach (var i in items.Where(i => i.Skip != null)) say($"Skipped {i.Name}: {i.Skip}");
        foreach (var i in items.Where(i => i.Pdf != null && job.UseLocal && i.NotLatest.Count > 0))
            say($"  Note: {i.Name} was made from this PC's copy, not the vault's latest ({string.Join(", ", i.NotLatest.Select(f => Path.GetFileName(f.Path)))})");
        foreach (var i in items.Where(i => i.Pdf != null && i.Notes.Count > 0)) say($"  Note: {i.Name}: {string.Join("; ", i.Notes)}");
    }
}
