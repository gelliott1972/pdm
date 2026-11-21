using System.Text.RegularExpressions;

namespace Pdm;

// "Needs drawing" (Grant, 21 Nov; on the page for everyone, for James): read from the index only.
// 1) New, no drawing: parts and assemblies that no live drawing references (the drawings' references, so a family
//    drawing such as GNRIC-BRP-10 covers GNRIC-BRP.SLDPRT), less the files the rules leave out.
// 2) Drawing older than its model: a model the drawing references was checked in after the drawing. A row can be
//    marked seen (a re-save that doesn't need the drawing changed); the mark is kept on the drawing's index record,
//    for that version of the model, so a later change to the model shows again, and a new version of the drawing
//    clears it.
// The rules are a short text file in the vault (.pdm/needs-drawing.txt) that the admin edits on the page; until
// there is one, Defaults.
public static class NeedsDrawing
{
    public const string RulesKey = ".pdm/needs-drawing.txt";
    public const string Defaults = """
        # Needs drawing: what the "New, no drawing" list leaves out. One pattern per line, then why; case doesn't matter.
        # A file name (MDL-*) matches in any folder; a folder ends with / (APEX/PANELS/) and covers everything in it.
        # * stands for any part of one name.
        MDL-*                kits
        _Shared/             fasteners and weldment profiles
        *.SLDLFP             weldment profiles
        APEX/PANELS/         panels
        APEX/*/SKUS/         panel SKUs
        LS-*.SLDASM          whole-screen assemblies (LS-*, +STRUCTURE) and their walls and roofs
        *_WITH_SCREEN*       whole-screen assemblies with the screen
        *-SKEL.SLDPRT        skeletons
        *-KBA-*              K-beam
        *-KBP-*              K-beam
        NONPART-*            not parts (grout)
        # "Drawing older than its model" shows model check-ins from this NZ day on (older ones were before the list).
        from 2026-11-21
        """;

    public sealed record Rule(string Pattern, string Why, int Count);
    public sealed record NoDrawing(string Path, string At, string By, string Note);
    public sealed record Older(string Drawing, string DrawingId, string DrawingAt, string DrawingBy,
        string Model, string ModelId, string ModelVersion, string ModelAt, string ModelBy, string ModelNote, SeenEntry? Seen);
    public sealed record Result(List<NoDrawing> NoDrawing, List<Older> Older, List<Rule> Rules, string? From, string Text, int Models, int Drawings);

    public static Result Compute(List<FileRecord> records, string root, string? rulesText)
    {
        var text = string.IsNullOrWhiteSpace(rulesText) ? Defaults : rulesText;
        var (rules, from) = Parse(text);
        var live = records.Where(r => !r.Removed && !Paths.IsReleaseFile(r.Path)).ToList();
        var byId = live.ToDictionary(r => r.FileId);
        var byPath = live.GroupBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        static bool IsModel(string p) => p.EndsWith(".sldprt", StringComparison.OrdinalIgnoreCase) || p.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);
        var drawings = live.Where(r => r.Path.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)).ToList();

        // What each drawing references: its references, and any recorded as missing that are in the vault now
        // (a model added after the drawing; see Engine.RelinkAsync).
        List<FileRecord> Uses(FileRecord d) => d.Refs.Select(id => byId.GetValueOrDefault(id))
            .Concat(d.MissingRefs.Select(m => Paths.KeyOrNull(root, m) is { } k ? byPath.GetValueOrDefault(k) : null))
            .OfType<FileRecord>().Where(m => IsModel(m.Path)).DistinctBy(m => m.FileId).ToList();
        var uses = drawings.ToDictionary(d => d.FileId, Uses);

        // 1) New, no drawing.
        var drawn = uses.Values.SelectMany(u => u).Select(m => m.FileId).ToHashSet();
        var counts = new int[rules.Count];
        var noDrawing = new List<NoDrawing>();
        var models = live.Where(r => IsModel(r.Path)).ToList();
        foreach (var m in models.Where(m => !drawn.Contains(m.FileId)).OrderBy(m => m.Path, StringComparer.OrdinalIgnoreCase))
        {
            var i = rules.FindIndex(r => r.Regex.IsMatch(r.Pattern.Contains('/') ? m.Path : Path.GetFileName(m.Path)));
            if (i >= 0) { counts[i]++; continue; }
            noDrawing.Add(new NoDrawing(m.Path, m.UpdatedAt, m.UpdatedBy, m.Note));
        }

        // 2) Drawing older than its model. A model whose last change was a move (same content) doesn't count.
        var fromUtc = from is { } day ? Activity.NzDayStartUtc(day) : (string?)null;
        var older = new List<Older>();
        foreach (var d in drawings)
            foreach (var m in uses[d.FileId])
            {
                if (string.CompareOrdinal(m.UpdatedAt, d.UpdatedAt) <= 0) continue;
                if (fromUtc != null && string.CompareOrdinal(m.UpdatedAt, fromUtc) < 0) continue;
                if (m.Moves.Count > 0 && m.Moves[^1].At == m.UpdatedAt) continue;
                var seen = d.Seen.LastOrDefault(x => x.Model == m.FileId && x.Version == m.VersionId);
                older.Add(new Older(d.Path, d.FileId, d.UpdatedAt, d.UpdatedBy, m.Path, m.FileId, m.VersionId, m.UpdatedAt, m.UpdatedBy, m.Note, seen));
            }
        older = older.OrderByDescending(o => o.ModelAt, StringComparer.Ordinal).ThenBy(o => o.Drawing, StringComparer.OrdinalIgnoreCase).ToList();

        return new Result(noDrawing, older, rules.Select((r, i) => new Rule(r.Pattern, r.Why, counts[i])).ToList(), from, text, models.Count, drawings.Count);
    }

    sealed record Parsed(string Pattern, string Why, Regex Regex);

    // Lines: "# comment", "from yyyy-mm-dd", or "pattern  why".
    static (List<Parsed>, string?) Parse(string text)
    {
        var rules = new List<Parsed>();
        string? from = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (line == "" || line.StartsWith('#')) continue;
            var m = Regex.Match(line, @"^from\s+(\d{4}-\d{2}-\d{2})$", RegexOptions.IgnoreCase);
            if (m.Success) { from = m.Groups[1].Value; continue; }
            var parts = line.Split((char[])[' ', '\t'], 2, StringSplitOptions.RemoveEmptyEntries);
            var pattern = parts[0].Replace('\\', '/');
            // A name: the whole file name. A folder (ends with /): the start of the path. Else the whole path.
            var re = "^" + Regex.Escape(pattern).Replace(@"\*", "[^/]*") + (pattern.EndsWith('/') ? "" : "$");
            rules.Add(new Parsed(pattern, parts.Length > 1 ? parts[1].Trim() : "", new Regex(re, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)));
        }
        return (rules, from);
    }

    // Marks a row seen, or (undo) not seen. The drawing is read fresh; false if it changed meanwhile.
    public static async Task<string?> MarkAsync(Index index, string drawingId, string modelId, string modelVersion, string by, bool undo)
    {
        var d = await index.GetAsync(drawingId);
        if (d == null || d.Removed) return "That drawing isn't in the vault now.";
        var seen = d.Seen.Where(x => !(x.Model == modelId && x.Version == modelVersion)).ToList();
        if (!undo) seen.Add(new SeenEntry(modelId, modelVersion, by, Index.Now()));
        return await index.SetSeenAsync(d, seen) ? null : $"{d.Path} changed meanwhile: press Reload and try again.";
    }

    // The admin command's view.
    public static IEnumerable<string> Summary(Result r)
    {
        yield return $"{r.Models} parts and assemblies, {r.Drawings} drawings in the vault (live).";
        yield return $"New, no drawing: {r.NoDrawing.Count}";
        foreach (var n in r.NoDrawing) yield return $"  {n.Path}  ({n.By}, {Engine.Local(n.At)}{(n.Note != "" ? ": " + n.Note : "")})";
        yield return $"Left out by the rules: {r.Rules.Sum(x => x.Count)}";
        foreach (var x in r.Rules) yield return $"  {x.Count,4}  {x.Pattern}  {x.Why}";
        var open = r.Older.Count(o => o.Seen == null);
        yield return $"Drawing older than its model{(r.From != null ? $" (model check-ins from {r.From} NZ)" : "")}: {r.Older.Count} ({open} not marked seen)";
        foreach (var o in r.Older)
            yield return $"  {Path.GetFileName(o.Drawing)} ({Engine.Local(o.DrawingAt)}) < {Path.GetFileName(o.Model)} ({o.ModelBy}, {Engine.Local(o.ModelAt)}{(o.ModelNote != "" ? ": " + o.ModelNote : "")})"
                + (o.Seen != null ? $"  [seen by {o.Seen.By}, {Engine.Local(o.Seen.At)}]" : "");
    }
}
