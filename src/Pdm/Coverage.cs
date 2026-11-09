using System.Text.Json;
using System.Text.RegularExpressions;

namespace Pdm;

// Activity tab 2) and 3): drawing progress folder by folder, and a finish forecast (Grant's calls, 9 Nov):
// - Each item counts once, in the folder its file lives in: per screen its top level, ShaftA, ShaftB, ShaftC and _Shared
//   (deeper folders roll up into these), with a screen total; GNRIC and other folders as their own lines. Folders with
//   no items are left out.
// - Items: the PartNos (per configuration) of the files in the folder, one item per PartNo. Every numbered assembly
//   counts, STA-01 included. Left out: files without a PartNo, FAST-* and MDL-*, the K-beam (KBA, KBP, KB-SKEL), and
//   PartNos that exist only as As Welded configurations (the B versions).
// - Drawn: a vault drawing whose title block shows that PartNo, or named by it. Title blocks are read from this PC's
//   copies with the Document Manager, cached by file date and size.
// - Forecast: James's pace, his new drawings per working day (Mon-Fri, NZ) over the last 10 working days before today.
//   NZ public holidays are not taken out.
public static partial class Coverage
{
    public const string Who = "James";
    public const string WhoEmail = "007jimdesign@gmail.com";
    const int RateDays = 10;

    public sealed record Line(string Name, int Items, int Drawn, List<string> Undrawn, double? Days, string? Finish, bool Total = false);
    public sealed record Group(string Name, List<Line> Lines);
    public sealed record Result(List<Group> Groups, string Who, int RateDrawings, string RateFrom, string RateTo, int RateDays, double Rate,
        string? TitleBlocks, string Rules);

    [GeneratedRegex(@"^(FAST-|MDL-)|-(KBA|KBP)-|-KB-SKEL", RegexOptions.IgnoreCase)] private static partial Regex LeftOut();
    [GeneratedRegex(@"^LS-", RegexOptions.IgnoreCase)] private static partial Regex Screen();

    public static async Task<Result> ComputeAsync(Settings s, List<FileRecord> records, Vault vault, string activityCache, string titleBlockCache)
    {
        var all = records.Where(r => !r.Removed && !Paths.IsReleaseFile(r.Path)).ToList();

        // Drawn PartNos: drawing names, and what their title blocks show.
        var drawings = all.Where(r => r.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)).ToList();
        var drawn = drawings.Select(r => Path.GetFileNameWithoutExtension(r.Path)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var byId = records.ToDictionary(r => r.FileId);
        var tbWhy = TitleBlocks(s, drawings, byId, titleBlockCache, drawn);

        // The pace: the 10 working days before today (NZ), and James's new drawings on them.
        var today = Activity.NzToday();
        var days = new List<DateTime>();
        for (var d = today.AddDays(-1); days.Count < RateDays; d = d.AddDays(-1)) if (Working(d)) days.Add(d);
        var from = days[^1]; var to = days[0];
        var events = await Activity.ReadAsync(vault, Enumerable.Range(-1, (to - from).Days + 3).Select(i => from.AddDays(i)), activityCache);
        var fromS = from.ToString("yyyy-MM-dd"); var toS = to.ToString("yyyy-MM-dd");
        var made = events.Where(e => e.Kind == "new" && e.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)
                && (e.By == Who || string.Equals(e.Email, WhoEmail, StringComparison.OrdinalIgnoreCase)))
            .Where(e => Activity.NzDay(e.At) is var day && string.CompareOrdinal(day, fromS) >= 0 && string.CompareOrdinal(day, toS) <= 0)
            .Select(e => e.Path.ToUpperInvariant()).Distinct().Count();
        var rate = (double)made / RateDays;

        // Items per folder: (group, line) -> PartNos.
        var items = new Dictionary<(string Group, string Line), HashSet<string>>();
        foreach (var f in all)
        {
            if (LeftOut().IsMatch(Path.GetFileName(f.Path))) continue;
            var pns = f.PartNos.Where(p => !string.IsNullOrWhiteSpace(p.PartNo) && !LeftOut().IsMatch(p.PartNo))
                .GroupBy(p => p.PartNo.Trim(), StringComparer.OrdinalIgnoreCase)
                .Where(g => !g.All(p => p.Config.Contains("As Welded", StringComparison.OrdinalIgnoreCase))).Select(g => g.Key).ToList();
            if (pns.Count == 0) continue;
            var where = Where(f.Path);
            if (!items.TryGetValue(where, out var set)) items[where] = set = new(StringComparer.OrdinalIgnoreCase);
            set.UnionWith(pns);
        }

        Line LineOf(string name, IEnumerable<string> pns, bool total = false)
        {
            var list = pns.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var undrawn = list.Where(p => !drawn.Contains(p)).OrderBy(p => p, StringComparer.OrdinalIgnoreCase).ToList();
            var (n, fin) = Forecast(undrawn.Count, rate, today);
            return new Line(name, list.Count, list.Count - undrawn.Count, undrawn, n, fin, total);
        }
        static int Order(string line) => line == "top level" ? 0 : line.StartsWith("Shaft", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        var groups = items.GroupBy(kv => kv.Key.Group)
            .OrderBy(g => Screen().IsMatch(g.Key.Split('/')[^1]) ? 0 : 1).ThenBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var lines = g.OrderBy(kv => Order(kv.Key.Line)).ThenBy(kv => kv.Key.Line, StringComparer.OrdinalIgnoreCase)
                    .Select(kv => LineOf(kv.Key.Line, kv.Value)).ToList();
                if (lines.Count > 1) lines.Add(LineOf("screen total", g.SelectMany(kv => kv.Value), true));
                return new Group(g.Key, lines);
            }).ToList();

        return new Result(groups, Who, made, fromS, toS, RateDays, Math.Round(rate, 1), tbWhy,
            "Each item counts once, in the folder its file is in. Items: PartNos, assemblies included (STA-01 too); left out: FAST-*, MDL-*, "
            + "K-beam (KBA, KBP, KB-SKEL), As Welded B versions, files without a PartNo. Drawn: a vault drawing whose title block shows the PartNo, "
            + $"or named by it. Pace: {Who}'s new drawings over the last {RateDays} working days before today; NZ public holidays not taken out.");
    }

    // Where a file counts: (APEX/SC08/LS-08X-04, ShaftA) for a screen's folders (deeper ones roll up, files at the screen's
    // top are "top level"); otherwise its own folder, e.g. (APEX/GNRIC, "").
    static (string, string) Where(string key)
    {
        var parts = key.Split('/');
        for (var i = 0; i < parts.Length - 1; i++)
            if (Screen().IsMatch(parts[i]))
                return (string.Join('/', parts[..(i + 1)]), i + 1 < parts.Length - 1 ? parts[i + 1] : "top level");
        return (string.Join('/', parts[..^1]), "");
    }

    // Adds the PartNos the drawings' title blocks show. Null when all were read; else why some weren't.
    static string? TitleBlocks(Settings s, List<FileRecord> drawings, Dictionary<string, FileRecord> byId, string cacheFile, HashSet<string> drawn)
    {
        Dictionary<string, string> cache; // "path|ticks|size" -> PartNo ("" when none)
        try { cache = File.Exists(cacheFile) ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(cacheFile)) ?? new() : new(); }
        catch (JsonException) { cache = new(); }
        DocReader? dm = null; string? dmWhy = null; int missing = 0, failed = 0; var changed = false;
        foreach (var d in drawings)
        {
            var local = Paths.LocalOf(s.LocalRoot, d.Path);
            var fi = new FileInfo(local);
            if (!fi.Exists) { missing++; continue; }
            var key = $"{d.Path}|{fi.LastWriteTimeUtc.Ticks}|{fi.Length}";
            if (!cache.TryGetValue(key, out var pn))
            {
                if (dm == null && dmWhy == null) { try { dm = new DocReader(); } catch (Exception ex) { dmWhy = ex.Message; } }
                if (dm == null) { failed++; continue; }
                try
                {
                    pn = "";
                    // The title block's model, found by name among the drawing's references.
                    if (dm.TitleBlockModel(local) is { } tb
                        && d.Refs.Select(id => byId.GetValueOrDefault(id)).FirstOrDefault(r => r != null && Path.GetFileName(r.Path).Equals(tb.Model, StringComparison.OrdinalIgnoreCase)) is { } model)
                        pn = dm.ModelProp(Paths.LocalOf(s.LocalRoot, model.Path), tb.Config, "PartNo")?.Trim() ?? "";
                    cache[key] = pn; changed = true;
                }
                catch (Exception) { failed++; continue; }
            }
            if (pn != "") drawn.Add(pn);
        }
        if (changed)
        {
            foreach (var k in cache.Keys.Where(k => !drawings.Any(d => k.StartsWith(d.Path + "|", StringComparison.Ordinal))).ToList()) cache.Remove(k);
            Directory.CreateDirectory(Path.GetDirectoryName(cacheFile)!);
            File.WriteAllText(cacheFile, JsonSerializer.Serialize(cache));
        }
        var why = new List<string>();
        if (missing > 0) why.Add($"{missing} drawing(s) not on this PC (Download latest)");
        if (failed > 0) why.Add($"{failed} title block(s) not read{(dmWhy != null ? $" ({dmWhy})" : "")}");
        return why.Count == 0 ? null : "Counted by name only for " + string.Join("; ", why);
    }

    static bool Working(DateTime d) => d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

    // Working days left at this pace, and the day it would be done on (counting from today).
    static (double?, string?) Forecast(int left, double rate, DateTime today)
    {
        if (left == 0) return (0, null);
        if (rate <= 0) return (null, null);
        var need = left / rate;
        var d = today; var n = (int)Math.Ceiling(need);
        while (n > 0) { d = d.AddDays(1); if (Working(d)) n--; }
        return (Math.Round(need, 1), d.ToString("yyyy-MM-dd"));
    }

    // The admin command's view.
    public static IEnumerable<string> Summary(Result r)
    {
        yield return $"Pace: {r.Who} made {r.RateDrawings} new drawings in the {r.RateDays} working days {r.RateFrom} to {r.RateTo}: {r.Rate} a day";
        if (r.TitleBlocks != null) yield return r.TitleBlocks;
        foreach (var g in r.Groups)
        {
            yield return g.Name;
            foreach (var l in g.Lines)
                yield return $"  {(l.Name == "" ? "(all)" : l.Name)}: {l.Drawn} of {l.Items} drawn ({(l.Items == 0 ? "-" : $"{100 * l.Drawn / l.Items}%")}); "
                    + (l.Days == 0 ? "done" : l.Days == null ? "no recent drawings: can't estimate" : $"about {l.Days} working days (around {l.Finish})")
                    + (l.Total || l.Undrawn.Count == 0 ? "" : $"  not drawn: {string.Join(", ", l.Undrawn.Take(12))}{(l.Undrawn.Count > 12 ? " …" : "")}");
        }
        yield return r.Rules;
    }
}
