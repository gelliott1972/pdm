using System.Text.Json;

namespace Pdm;

// Needs drawing, "drawing older than its model" (Grant, 21 Nov): marks the rows where the model was only re-saved.
// The model as it is now is compared with the version the drawing was made from (the model's newest version in the
// vault from before the drawing's check-in): references, configurations, custom properties, mass properties and
// components, read with the Document Manager (DocReader.Fingerprint). The same in all of those: "re-saved only".
// Anything that can't be read or found leaves the row unmarked, never guessed.
// Each answer (or "can't tell") is kept on this PC (P:\.pdm\resaved.json), by model version and the drawing's check-in time, so each is read once.
public static class Resaved
{
    static readonly SemaphoreSlim One = new(1, 1); // one check at a time (the page can ask twice)

    public static async Task<List<NeedsDrawing.Older>> MarkAsync(List<NeedsDrawing.Older> rows, List<FileRecord> records, Vault vault, string root, Action<string>? log = null)
    {
        if (rows.Count == 0) return rows;
        await One.WaitAsync();
        try
        {
            var cachePath = Path.Combine(root, ".pdm", "resaved.json");
            var cache = Load(cachePath);
            int before = cache.Count;
            var byId = records.ToDictionary(r => r.FileId);
            var versions = new Dictionary<string, List<(string VersionId, DateTime At)>>(StringComparer.Ordinal);
            DocReader? reader = null;
            bool readerFailed = false;
            var work = Path.Combine(Path.GetTempPath(), "pdm-resaved-" + Guid.NewGuid().ToString("N")[..8]);
            var result = new List<NeedsDrawing.Older>();
            try
            {
                foreach (var o in rows)
                {
                    bool same = false;
                    try
                    {
                        if (byId.GetValueOrDefault(o.ModelId) is not { } m) continue;
                        // The model's versions before a time never change, so the answer for this model version and
                        // the drawing's check-in time stands.
                        var key = $"{m.FileId}|{m.VersionId}|{o.DrawingAt}";
                        if (cache.TryGetValue(key, out var known)) { same = known == true; continue; }
                        var old = await MadeFromAsync(m, o.DrawingAt, vault, versions);
                        if (old == null) continue;
                        if (readerFailed) continue;
                        try { reader ??= new DocReader(); }
                        catch (Exception ex) { readerFailed = true; log?.Invoke($"Needs drawing: can't read models here ({ex.Message}); re-saves not marked."); continue; }
                        var ext = Path.GetExtension(m.Path);
                        var a = Path.Combine(work, $"{m.FileId}-{Short(old.Value.VersionId)}{ext}");
                        var b = Path.Combine(work, $"{m.FileId}-{Short(m.VersionId)}{ext}");
                        if (!File.Exists(a)) await vault.DownloadAsync(old.Value.Key, old.Value.VersionId, a);
                        if (!File.Exists(b)) await vault.DownloadAsync(m.Path, m.VersionId, b);
                        var fa = reader.Fingerprint(a);
                        var fb = reader.Fingerprint(b);
                        File.Delete(a);
                        if (fa == null || fb == null) { cache[key] = null; continue; } // can't tell (no mass properties saved, say): unmarked
                        same = fa.SequenceEqual(fb, StringComparer.Ordinal);
                        cache[key] = same;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Amazon.Runtime.AmazonServiceException or HttpRequestException)
                    {
                        log?.Invoke($"Needs drawing: couldn't check {o.Model} for {o.Drawing}: {ex.Message}");
                    }
                    finally { result.Add(same ? o with { ResavedOnly = true } : o); }
                }
            }
            finally
            {
                try { if (Directory.Exists(work)) Directory.Delete(work, true); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
                if (cache.Count != before) Save(cachePath, cache);
            }
            return result;
        }
        finally { One.Release(); }
    }

    // The model's version the drawing was made from: its newest version from before the drawing's check-in, at the
    // path it had then (a move copies it to a new key, so the paths it was moved from count too).
    static async Task<(string Key, string VersionId)?> MadeFromAsync(FileRecord m, string drawingAt, Vault vault, Dictionary<string, List<(string VersionId, DateTime At)>> versions)
    {
        if (!DateTime.TryParse(drawingAt, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var at)) return null;
        (string Key, string VersionId, DateTime At)? best = null;
        foreach (var key in m.Moves.Select(x => x.From).Append(m.Path).Distinct(StringComparer.Ordinal))
        {
            if (!versions.TryGetValue(key, out var list)) versions[key] = list = await vault.VersionsAsync(key);
            foreach (var v in list.Where(v => v.At <= at))
            {
                if (best == null || v.At > best.Value.At) best = (key, v.VersionId, v.At);
                break; // newest first
            }
        }
        return best is { } b && b.VersionId != m.VersionId ? (b.Key, b.VersionId) : null;
    }

    // A version id, short and safe in a file name.
    static string Short(string v) => string.Concat(v.Where(char.IsLetterOrDigit).Take(12));

    static Dictionary<string, bool?> Load(string path)
    {
        try { return File.Exists(path) ? JsonSerializer.Deserialize<Dictionary<string, bool?>>(File.ReadAllText(path)) ?? new() : new(); }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException) { return new(); }
    }

    static void Save(string path, Dictionary<string, bool?> cache)
    {
        try { Directory.CreateDirectory(Path.GetDirectoryName(path)!); File.WriteAllText(path, JsonSerializer.Serialize(cache)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
}
