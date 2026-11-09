using System.Text.Json;

namespace Pdm;

// The release step's real run (docs\briefs\release-0A.md, steps 5 and 6): files the PDFs that a dry run passed into
// the scene's _Review folder, each as a new vault file, with the set recorded on its index entry (code, set, when, who,
// and the drawing and model versions the PDF was made from). It works from the dry run's work folder, so what is filed
// is exactly what was checked. A drawing whose drawing or models changed in the vault since the dry run is left out and
// listed (run the dry run again for those).
public sealed class ReleaseFiler(Settings s, Index index, Vault vault, string name, Action<string> say)
{
    public sealed record Model(string Path, string FileId, string VersionId);
    public sealed record Item(string Drawing, string FileId, string VersionId, string PdfKey, string? Pdf, bool Pass,
        List<string> Problems, List<Model> Models);
    sealed record Report(string Folder, string Code, bool Dry, List<Item> Items);

    static readonly JsonSerializerOptions Read = new() { PropertyNameCaseInsensitive = true };

    public async Task<int> RunAsync(string work, bool dryRun)
    {
        work = Path.GetFullPath(work);
        var reportPath = Path.Combine(work, "report.json");
        if (!File.Exists(reportPath)) { say($"No report.json in {work}: give the work folder of a release dry run."); return 2; }
        var report = JsonSerializer.Deserialize<Report>(File.ReadAllText(reportPath), Read)!;
        var run = Path.GetFileName(work.TrimEnd('\\'));
        var all = await index.AllAsync();
        var byId = all.ToDictionary(r => r.FileId);
        var byPath = all.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var passed = report.Items.Where(i => i.Pass).ToList();
        say($"Release {report.Code}: {report.Folder}. The dry run ({run}) passed {passed.Count} of {report.Items.Count} drawings; failed ones aren't filed.");
        var ready = new List<Item>();
        foreach (var i in passed)
        {
            var why = WhyNot(i, byId, byPath);
            if (why != null) say($"  Not filed: {i.Drawing}: {why}");
            else ready.Add(i);
        }
        var dupes = ready.GroupBy(i => i.PdfKey, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList();
        foreach (var g in dupes) say($"  Not filed: {g.Key} would come from {g.Count()} drawings ({string.Join(", ", g.Select(i => i.Drawing))})");
        ready.RemoveAll(i => dupes.Any(g => g.Key.Equals(i.PdfKey, StringComparison.OrdinalIgnoreCase)));

        if (dryRun)
        {
            foreach (var i in ready) say($"  would file {i.PdfKey}  (from {i.Drawing}, {i.Models.Count} models)");
            say($"Dry run: {ready.Count} PDFs would be filed; nothing written.");
            return ready.Count == passed.Count ? 0 : 1;
        }

        var at = Index.Now();
        var state = LocalState.Load(s.LocalRoot);
        var filed = new List<object>();
        try
        {
            foreach (var i in ready)
            {
                var local = Paths.LocalOf(s.LocalRoot, i.PdfKey);
                try
                {
                    if (File.Exists(local) && Vault.Md5(local) != Vault.Md5(i.Pdf!)) { say($"  Not filed: {i.PdfKey}: a different file is already at {local}"); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(local)!);
                    if (!File.Exists(local)) File.Copy(i.Pdf!, local);
                    var set = new
                    {
                        code = report.Code, set = report.Folder, run, at, by = name,
                        drawing = new { path = i.Drawing, fileId = i.FileId, versionId = i.VersionId },
                        models = i.Models.Select(m => new { path = m.Path, fileId = m.FileId, versionId = m.VersionId })
                    };
                    var rec = new FileRecord
                    {
                        FileId = Guid.NewGuid().ToString(), Path = i.PdfKey, UpdatedAt = at, UpdatedBy = name,
                        Note = $"Release {report.Code} ({report.Folder}): {Path.GetFileName(i.Drawing)}",
                        Size = new FileInfo(local).Length, ReleaseSet = JsonSerializer.Serialize(set)
                    };
                    (rec.VersionId, rec.ETag) = await vault.UploadAsync(local, rec.Path, rec.FileId);
                    await index.AddAsync(rec);
                    state.Record(rec.Path, local, rec.FileId, rec.VersionId, rec.ETag);
                    Paths.SetReadOnly(local, true);
                    filed.Add(new { pdfKey = rec.Path, fileId = rec.FileId, versionId = rec.VersionId, drawing = i.Drawing });
                    say($"  Filed {rec.Path}");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException)
                {
                    say($"  Not filed: {i.PdfKey}: {ex.Message}");
                }
            }
        }
        finally
        {
            state.Save(s.LocalRoot);
            File.WriteAllText(Path.Combine(work, $"filed-{DateTime.Now:yyyyMMdd-HHmmss}.json"), JsonSerializer.Serialize(new { at, by = name, filed }, new JsonSerializerOptions { WriteIndented = true }));
        }
        say($"Filing done: {filed.Count} of {passed.Count} passing PDFs filed into the vault. Download latest on each PC fetches them.");
        return filed.Count == passed.Count ? 0 : 1;
    }

    // Null when this PDF can be filed as checked: everything it was made from is still the vault's latest.
    static string? WhyNot(Item i, Dictionary<string, FileRecord> byId, Dictionary<string, FileRecord> byPath)
    {
        if (i.Pdf == null || !File.Exists(i.Pdf)) return "its PDF isn't in the work folder";
        if (!Paths.IsPdf(i.PdfKey) || !Paths.InReleaseFolder(i.PdfKey)) return $"{i.PdfKey} isn't a PDF in a release folder";
        if (byPath.TryGetValue(i.PdfKey, out var there)) return $"already filed: {i.PdfKey} is in the vault{(there.Removed ? " (removed)" : "")}";
        if (!byId.TryGetValue(i.FileId, out var d) || d.Removed) return "the drawing is no longer in the vault";
        if (d.VersionId != i.VersionId) return $"the drawing has changed in the vault since the dry run ({d.UpdatedBy}, {d.UpdatedAt}); run the dry run again";
        if (d.LockedBy != null) return $"the drawing is checked out by {d.LockedByName}";
        foreach (var m in i.Models)
        {
            if (!byId.TryGetValue(m.FileId, out var r) || r.Removed) return $"{m.Path} is no longer in the vault";
            if (r.VersionId != m.VersionId) return $"{r.Path} has changed in the vault since the dry run ({r.UpdatedBy}, {r.UpdatedAt}); run the dry run again";
        }
        return null;
    }
}
