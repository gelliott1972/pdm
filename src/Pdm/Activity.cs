using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;

namespace Pdm;

// The activity log (Grant, 9 Nov: what was uploaded each day): one small JSON object per action, append-only, in the
// vault bucket at .pdm/activity/<UTC date>/<UTC time>_<id>.json. Written by the app (check-in, add, remove, restore)
// and the admin commands (load, move, release-file). Old app versions never touch it, so nothing can be lost; it needs
// no other AWS resource or permission (the engineer role may already write and list the vault bucket).
// Kinds: new, changed, removed, restored, moved, released (a release PDF), package (a set's ZIP or list), register.
public sealed record ActivityEvent(string At, string By, string Kind, string Path, string FileId,
    string? Email = null, string? VersionId = null, string? Note = null, int? Sheets = null, string? From = null, string? Source = null);

public static class Activity
{
    public const string Prefix = ".pdm/activity/";
    // The page's Activity tab is Grant's only (Grant, 9 Nov: hard-coded for now).
    public const string AdminEmail = "grant@candelic.com";
    public static bool IsAdmin(string email) => email.Equals(AdminEmail, StringComparison.OrdinalIgnoreCase);

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull };

    public static DateTime Utc(string at) => DateTime.Parse(at, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    public static string KeyOf(ActivityEvent e, string id)
    {
        var t = Utc(e.At);
        return $"{Prefix}{t:yyyy-MM-dd}/{t:yyyyMMdd'T'HHmmss'Z'}_{id}.json";
    }

    // Writes one event. A failure never stops the action itself; it's said and the action stands.
    public static async Task LogAsync(Vault vault, ActivityEvent e, Action<string>? say = null, string? id = null)
    {
        try { await vault.PutTextAsync(KeyOf(e, id ?? Guid.NewGuid().ToString("N")), JsonSerializer.Serialize(e, Json)); }
        catch (Exception ex) when (ex is Amazon.Runtime.AmazonServiceException or HttpRequestException or IOException)
        { say?.Invoke($"  (not recorded in the activity log: {ex.Message})"); }
    }

    // The events of the given UTC dates. Events never change, so those read before come from a cache on this PC.
    public static async Task<List<ActivityEvent>> ReadAsync(Vault vault, IEnumerable<DateTime> utcDates, string cacheFile)
    {
        Dictionary<string, ActivityEvent> cache;
        try { cache = File.Exists(cacheFile) ? JsonSerializer.Deserialize<Dictionary<string, ActivityEvent>>(File.ReadAllText(cacheFile), Json) ?? new() : new(); }
        catch (JsonException) { cache = new(); }
        var lists = await Task.WhenAll(utcDates.Distinct().Select(d => vault.ListKeysAsync($"{Prefix}{d:yyyy-MM-dd}/")));
        var keys = lists.SelectMany(l => l).ToList();
        var fetched = new ConcurrentDictionary<string, ActivityEvent>();
        await Parallel.ForEachAsync(keys.Where(k => !cache.ContainsKey(k)), new ParallelOptions { MaxDegreeOfParallelism = 48 }, async (k, _) =>
        {
            try { if (JsonSerializer.Deserialize<ActivityEvent>(await vault.GetTextAsync(k), Json) is { } e) fetched[k] = e; }
            catch (JsonException) { }
        });
        if (!fetched.IsEmpty)
        {
            foreach (var (k, e) in fetched) cache[k] = e;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cacheFile)!);
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache, Json));
        }
        return keys.Where(cache.ContainsKey).Select(k => cache[k]).ToList();
    }

    // One event as the page shows it, with its NZ day.
    public sealed record Shown(string Day, string At, string By, string Kind, string Path, string Note, int? Sheets, string? From, bool Drawing);
    public sealed record Days(string From, string To, List<Shown> Events);

    static readonly TimeZoneInfo Nz = TimeZoneInfo.FindSystemTimeZoneById("New Zealand Standard Time");
    public static string NzDay(string at) => TimeZoneInfo.ConvertTimeFromUtc(Utc(at), Nz).ToString("yyyy-MM-dd");
    public static DateTime NzToday() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Nz).Date;

    // The events of NZ days [before - days, before) (before: yyyy-MM-dd; default tomorrow), oldest first.
    public static async Task<Days> QueryAsync(Vault vault, string cacheFile, string before, int days)
    {
        days = Math.Clamp(days, 1, 366);
        var to = DateTime.TryParse(before, out var b) ? b.Date : TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, Nz).Date.AddDays(1);
        var from = to.AddDays(-days);
        var utcDates = Enumerable.Range(-1, days + 2).Select(i => from.AddDays(i)); // NZ is ahead of UTC: the day before too
        var events = await ReadAsync(vault, utcDates, cacheFile);
        var f = from.ToString("yyyy-MM-dd"); var t = to.ToString("yyyy-MM-dd");
        return new Days(f, t, events.Select(e => (e, day: NzDay(e.At))).Where(x => string.CompareOrdinal(x.day, f) >= 0 && string.CompareOrdinal(x.day, t) < 0)
            .OrderBy(x => x.e.At, StringComparer.Ordinal)
            .Select(x => new Shown(x.day, x.e.At, x.e.By == "" ? "(who not recorded)" : x.e.By, x.e.Kind, x.e.Path, x.e.Note ?? "", x.e.Sheets, x.e.From,
                x.e.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase))).ToList());
    }

    // The page's per-day, per-person lines, as text (the admin command's view).
    public static IEnumerable<string> Summary(Days d)
    {
        // Nouns take an s; the rest ("changed", "moved") don't.
        string KindOf(Shown e) => e.Kind == "new" && e.Drawing ? "new drawing" : e.Kind == "new" ? "new file" : e.Kind is "package" or "register" ? "release package file" : e.Kind == "released" ? "release PDF" : e.Kind;
        static string Count(int n, string w) => $"{n} {w}{(n != 1 && (w.EndsWith("drawing") || w.EndsWith("file") || w.EndsWith("PDF") || w == "sheet") ? "s" : "")}";
        foreach (var day in d.Events.GroupBy(e => e.Day).OrderByDescending(g => g.Key))
        {
            yield return day.Key;
            foreach (var who in day.GroupBy(e => e.By).OrderBy(g => g.Key))
            {
                var sheets = who.Where(e => KindOf(e) == "new drawing").Sum(e => e.Sheets ?? 0);
                yield return $"  {who.Key}: " + string.Join(", ", who.GroupBy(KindOf).OrderBy(g => g.Key == "new drawing" ? 0 : 1)
                    .Select(g => Count(g.Count(), g.Key) + (g.Key == "new drawing" && sheets > 0 ? $" ({Count(sheets, "sheet")})" : "")));
            }
        }
    }

    // What a vault path is, for the log: release PDFs, packages and the register have their own kinds.
    public static string KindOf(string path, bool isNew) =>
        Paths.IsRegister(path) ? "register"
        : Paths.InReleaseFolder(path) ? (Paths.IsPdf(path) ? "released" : "package")
        : isNew ? "new" : "changed";
}

// One-off fill of the activity log from what the vault already knows (admin, SSO, reads only apart from the log):
// every S3 version of every file (time; who and note only for each file's current version), moves, removals.
// Events get fixed ids (from the version or the record), so running it again rewrites the same objects.
public sealed class ActivityBackfill(Settings s, Index index, Vault vault, Action<string> say)
{
    public async Task<int> RunAsync(bool dryRun, DateTime? since)
    {
        var all = await index.AllAsync();
        var versions = (await vault.AllVersionsAsync()).Where(v => !v.Key.StartsWith(".pdm/")).ToList();
        say($"{all.Count} index records, {versions.Count} file versions in the vault.");

        // Which file each key belongs to: its current path, or an old path it was moved from.
        var byKey = new Dictionary<string, FileRecord>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in all) { byKey[r.Path] = r; foreach (var m in r.Moves) byKey.TryAdd(m.From, r); }
        var reader = new Lazy<DocReader?>(() => { try { return new DocReader(); } catch (InvalidOperationException) { return null; } });

        var events = new List<(string Id, ActivityEvent E)>();
        foreach (var g in versions.GroupBy(v => v.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (!byKey.TryGetValue(g.Key, out var rec)) continue; // not a vault file (leftovers)
            var first = true;
            foreach (var v in g.OrderBy(v => v.At))
            {
                // The copy a move makes at the new path is the move, not a new file.
                var move = rec.Moves.FirstOrDefault(m => m.To.Equals(g.Key, StringComparison.OrdinalIgnoreCase) && Math.Abs((Activity.Utc(m.At) - v.At).TotalMinutes) < 5);
                if (move != null) { first = false; continue; }
                var current = v.VersionId == rec.VersionId;
                var isNew = first && !rec.Moves.Any(m => m.To.Equals(g.Key, StringComparison.OrdinalIgnoreCase));
                first = false;
                var at = current && rec.UpdatedAt != "" ? rec.UpdatedAt : v.At.ToString("yyyy-MM-ddTHH:mm:ssZ");
                int? sheets = null;
                if (isNew && g.Key.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)) sheets = await SheetsAsync(g.Key, v.VersionId, current ? rec : null, reader.Value);
                events.Add(($"bf-{Safe(v.VersionId)}", new ActivityEvent(at, current ? rec.UpdatedBy : "", Activity.KindOf(g.Key, isNew), g.Key, rec.FileId,
                    VersionId: v.VersionId, Note: current ? rec.Note : null, Sheets: sheets, Source: current ? "backfill" : "backfill: who not recorded")));
            }
        }
        foreach (var r in all)
        {
            foreach (var m in r.Moves)
                events.Add(($"bf-mv-{r.FileId}-{Safe(m.At)}", new ActivityEvent(m.At, m.By, "moved", m.To, r.FileId, Note: m.Note, From: m.From, Source: "backfill")));
            if (r.RemovedAt != null)
                events.Add(($"bf-rm-{r.FileId}", new ActivityEvent(r.RemovedAt, r.RemovedByName ?? "", "removed", r.Path, r.FileId, Email: r.RemovedBy, Note: r.RemovedReason, Source: "backfill")));
        }
        if (since != null) events.RemoveAll(e => Activity.Utc(e.E.At) < since.Value);

        foreach (var day in events.GroupBy(e => Activity.Utc(e.E.At).ToString("yyyy-MM-dd")).OrderBy(g => g.Key).TakeLast(21))
            say($"  {day.Key}: " + string.Join("; ", day.GroupBy(e => e.E.By == "" ? "(who not recorded)" : e.E.By)
                .Select(p => $"{p.Key} " + string.Join(", ", p.GroupBy(e => e.E.Kind).Select(k => $"{k.Count()} {k.Key}")))));
        if (dryRun) { say($"Dry run: {events.Count} events would be written to {Activity.Prefix}; nothing written."); return 0; }
        var n = 0;
        await Parallel.ForEachAsync(events, new ParallelOptions { MaxDegreeOfParallelism = 12 }, async (e, _) =>
        {
            await vault.PutTextAsync(Activity.KeyOf(e.E, e.Id), JsonSerializer.Serialize(e.E, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }));
            if (Interlocked.Increment(ref n) % 200 == 0) say($"  written {n} of {events.Count}");
        });
        say($"Backfill done: {events.Count} events written to {Activity.Prefix}.");
        return 0;
    }

    static string Safe(string s) => new(s.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '_').ToArray());

    // A new drawing's sheet count: from this PC's copy when it's that version, else that version downloaded.
    async Task<int?> SheetsAsync(string key, string versionId, FileRecord? current, DocReader? dm)
    {
        if (dm == null) return null;
        var local = Paths.LocalOf(s.LocalRoot, key);
        var temp = (string?)null;
        try
        {
            if (current == null || !File.Exists(local) || Vault.Md5(local) != current.ETag)
            {
                temp = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdm-bf-{Guid.NewGuid():N}.SLDDRW");
                await vault.DownloadAsync(key, versionId, temp);
                local = temp;
            }
            return dm.Sheets(local);
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException or System.Runtime.InteropServices.COMException or Amazon.Runtime.AmazonServiceException) { return null; }
        finally { if (temp != null) try { File.Delete(temp); } catch (IOException) { } }
    }
}
