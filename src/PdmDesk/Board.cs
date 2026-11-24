// The board, "Where things are up to" (APEX Desk stage 2, item 5): read from the project register named in settings
// (workflow.csv, screen-types.csv, screens.csv). The Desk only reads those files, opened with sharing, and never
// writes them. Parsed once per change (size and modified time); if a file is missing or won't parse, the last good
// board is kept and the problem is said, by file name only. Steps, their order and kinds come from settings.
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Desk;

public sealed partial class Board(Settings settings)
{
    static readonly string[] States = ["done", "running", "waiting", "blocked", "not started"];

    sealed record Step(string Key, string Label, string? Kind);
    sealed record Cell(string State, string Pin, string Date, string By, string Note);
    sealed record Row(string Ls, string Description, List<string> Vms, Dictionary<string, Cell> Cells, string Pin, string SteelSince, string VaultPin);
    sealed record Parsed(List<Row> Rows, List<(string Ls, string Description)> NotStarted, List<string> Problems);

    readonly object gate = new();
    string stamp = "";
    Parsed? last;           // the last good read
    string? readProblem;    // why the latest read failed, while the last good one is shown

    public bool Enabled => settings.Workflow != null && settings.ScreenTypes != null && settings.Steps is { Count: > 0 };

    List<Step> Steps() => (settings.Steps ?? []).OfType<JsonObject>()
        .Select(o => new Step((string?)o["key"] ?? "", (string?)o["label"] ?? (string?)o["key"] ?? "", (string?)o["kind"]))
        .Where(s => s.Key != "").ToList();

    // Open tickets per screen are counted by the caller (they change more often than the register).
    public JsonObject Build(IEnumerable<JsonObject> tickets)
    {
        var steps = Steps();
        Parsed? p; string? failed;
        lock (gate) { Refresh(steps); p = last; failed = readProblem; }
        var problems = new List<string>();
        if (failed != null) problems.Add(failed + (p != null ? "; showing the last good board" : ""));
        var open = tickets.Where(t => (string?)t["status"] is null or "open" or "started" or "discuss")
            .GroupBy(t => (string?)t["screen"] ?? "").ToDictionary(g => g.Key, g => g.Count());
        var rows = new JsonArray();
        if (p != null)
        {
            problems.AddRange(p.Problems);
            foreach (var r in p.Rows)
            {
                var cells = new JsonObject();
                // Out of date: a step that depends on the model, done/running/waiting, at a pin earlier than the steel
                // the screen has now (steel_since; the pin row's pin when that is blank). Compared as times.
                var reference = r.SteelSince != "" ? r.SteelSince : r.Pin;
                var refTime = Utc(reference);
                foreach (var s in steps)
                {
                    if (!r.Cells.TryGetValue(s.Key, out var c)) continue;
                    var bad = !States.Contains(c.State) || (c.Pin != "" && Utc(c.Pin) == null) || (s.Kind == "pin" && r.VaultPin != "" && r.Pin != "" && Utc(r.VaultPin) != Utc(r.Pin));
                    var older = !bad && c.Pin != "" && refTime != null && Utc(c.Pin) < refTime;
                    var cell = new JsonObject
                    {
                        ["state"] = bad ? "?" : c.State, ["pin"] = c.Pin, ["date"] = c.Date, ["by"] = c.By, ["note"] = c.Note,
                        ["outOfDate"] = older && s.Kind == null && c.State is "done" or "running" or "waiting",
                    };
                    if (older && s.Kind == "gate") cell["olderSteel"] = true; // a gate keeps its state, marked (question 1)
                    cells[s.Key] = cell;
                }
                rows.Add(new JsonObject
                {
                    ["ls"] = r.Ls, ["description"] = r.Description, ["pin"] = r.Pin, ["steelSince"] = r.SteelSince,
                    ["vms"] = new JsonArray(r.Vms.Select(v => (JsonNode?)v).ToArray()), ["openTickets"] = open.GetValueOrDefault(r.Ls),
                    ["cells"] = cells
                });
            }
        }
        return new JsonObject
        {
            ["steps"] = new JsonArray(steps.Select(s => (JsonNode?)new JsonObject { ["key"] = s.Key, ["label"] = s.Label, ["kind"] = s.Kind }).ToArray()),
            ["dividers"] = new JsonArray((settings.Steps ?? []).OfType<JsonObject>().Where(o => (bool?)o["ruleBefore"] == true).Select(o => (JsonNode?)(string?)o["key"]).ToArray()),
            ["rows"] = rows,
            ["notStarted"] = new JsonArray((p?.NotStarted ?? []).Select(n => (JsonNode?)new JsonObject { ["ls"] = n.Ls, ["description"] = n.Description }).ToArray()),
            ["problems"] = new JsonArray(problems.Select(x => (JsonNode?)x).ToArray()),
        };
    }

    void Refresh(List<Step> steps)
    {
        var files = new[] { settings.Workflow!, settings.ScreenTypes!, settings.Screens };
        var now = string.Join("|", files.Select(f => f == null ? "-" : File.Exists(f) ? $"{new FileInfo(f).Length}/{File.GetLastWriteTimeUtc(f).Ticks}" : "missing"))
            + "|" + string.Join(",", steps.Select(s => s.Key + ":" + s.Kind));
        if (now == stamp && (last != null || readProblem != null)) return;
        try { last = Parse(steps); readProblem = null; stamp = now; }
        catch (BoardException ex) { readProblem = ex.Message; stamp = ""; } // retried on the next poll
    }

    sealed class BoardException(string m) : Exception(m);

    Parsed Parse(List<Step> steps)
    {
        var problems = new List<string>();
        var wf = Table(settings.Workflow!, ["ls", "step", "state", "pin", "steel_since", "date", "by", "note"]);
        var st = Table(settings.ScreenTypes!, ["ls", "description", "vault_version"]);
        var sc = settings.Screens != null ? Table(settings.Screens, ["vm", "ls"]) : [];
        var keys = steps.Select(s => s.Key).ToHashSet();
        var wfName = Path.GetFileName(settings.Workflow!);

        var byLs = new Dictionary<string, Dictionary<string, Cell>>();
        var pins = new Dictionary<string, (string Pin, string Steel)>();
        foreach (var r in wf)
        {
            var ls = r["ls"]; var step = r["step"];
            if (ls == "") continue;
            if (!keys.Contains(step)) { problems.Add($"{wfName}: {ls} has an unknown step \"{step}\""); continue; }
            var c = new Cell(r["state"], r["pin"], r["date"], r["by"], r["note"]);
            if (!States.Contains(c.State)) problems.Add($"{wfName}: {ls} {step} has an unknown state \"{c.State}\"");
            if (c.Pin != "" && Utc(c.Pin) == null) problems.Add($"{wfName}: {ls} {step} pin \"{c.Pin}\" isn't a UTC time");
            if (!byLs.TryGetValue(ls, out var cells)) byLs[ls] = cells = [];
            if (cells.ContainsKey(step)) problems.Add($"{wfName}: {ls} {step} is listed twice; the last row counts");
            cells[step] = c;
            if (steps.First(s => s.Key == step).Kind == "pin")
            {
                if (r["steel_since"] != "" && Utc(r["steel_since"]) == null) problems.Add($"{wfName}: {ls} steel_since \"{r["steel_since"]}\" isn't a UTC time");
                pins[ls] = (c.Pin, r["steel_since"]);
            }
        }

        var stName = Path.GetFileName(settings.ScreenTypes!);
        var rows = new List<Row>();
        var notStarted = new List<(string, string)>();
        var known = new HashSet<string>();
        foreach (var r in st)
        {
            var ls = r["ls"]; if (ls == "" || !known.Add(ls)) continue;
            if (!byLs.TryGetValue(ls, out var cells)) { notStarted.Add((ls, r["description"])); continue; }
            var (pin, steel) = pins.GetValueOrDefault(ls, ("", ""));
            // vault_version is free text that begins "vault as of 2026-11-23 05:05:47Z": only its leading time is read.
            var vault = VaultAsOf().Match(r["vault_version"]) is { Success: true } m ? m.Groups[1].Value + "T" + m.Groups[2].Value + "Z" : "";
            if (vault != "" && pin != "" && Utc(vault) != Utc(pin)) problems.Add($"{ls}: the pin row ({pin}) and {stName}'s vault_version ({vault}) disagree");
            var vms = sc.Where(x => x["ls"] == ls).Select(x => x["vm"]).Where(v => v != "").ToList();
            rows.Add(new Row(ls, r["description"], vms, cells, pin, Utc(steel) != null ? steel : "", vault));
        }
        foreach (var ls in byLs.Keys.Where(k => !known.Contains(k)))
            problems.Add($"{wfName}: {ls} isn't in {stName}; not shown");
        return new Parsed(rows, notStarted, problems);
    }

    [GeneratedRegex(@"^\s*vault as of (\d{4}-\d{2}-\d{2})[ T](\d{2}:\d{2}:\d{2})Z", RegexOptions.IgnoreCase)]
    private static partial Regex VaultAsOf();

    // A UTC time written as yyyy-MM-ddTHH:mm:ssZ, or null.
    public static DateTime? Utc(string? s) =>
        s != null && DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t) ? t : null;

    // A CSV file as rows of named columns (only the columns asked for, which must all be in the header).
    static List<Dictionary<string, string>> Table(string path, string[] need)
    {
        var name = Path.GetFileName(path);
        string text;
        try
        {
            using var f = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            text = new StreamReader(f, Encoding.UTF8, detectEncodingFromByteOrderMarks: true).ReadToEnd();
        }
        catch (FileNotFoundException) { throw new BoardException($"{name} is missing"); }
        catch (DirectoryNotFoundException) { throw new BoardException($"{name} is missing"); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new BoardException($"{name} can't be read just now (open in another program?)"); }
        List<List<string>> rows;
        try { rows = Csv(text); }
        catch (FormatException ex) { throw new BoardException($"{name} won't parse ({ex.Message})"); }
        if (rows.Count == 0) throw new BoardException($"{name} is empty");
        var head = rows[0].Select(h => h.Trim()).ToList();
        var missing = need.Where(n => !head.Contains(n)).ToList();
        if (missing.Count > 0) throw new BoardException($"{name} has no column {string.Join(", ", missing)}");
        var idx = need.ToDictionary(n => n, n => head.IndexOf(n));
        return rows.Skip(1).Where(r => r.Any(c => c.Trim() != ""))
            .Select(r => idx.ToDictionary(kv => kv.Key, kv => kv.Value < r.Count ? r[kv.Value].Trim() : "")).ToList();
    }

    // RFC 4180 CSV: quoted fields, doubled quotes, commas and line breaks inside quotes; a UTF-8 byte-order mark is skipped.
    public static List<List<string>> Csv(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false, any = false;
        for (int i = 0; i < text.Length; i++)
        {
            var ch = text[i];
            if (quoted)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = false;
                }
                else field.Append(ch);
                continue;
            }
            switch (ch)
            {
                case '"' when field.Length == 0: quoted = true; any = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); any = true; break;
                case '\r': break;
                case '\n': row.Add(field.ToString()); field.Clear(); rows.Add(row); row = []; any = false; break;
                default: field.Append(ch); any = true; break;
            }
        }
        if (quoted) throw new FormatException("a quoted field isn't closed");
        if (any || field.Length > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
