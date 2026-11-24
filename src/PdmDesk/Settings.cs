// The Desk's settings (APEX Desk stage 2, item 1): settings.json beside the exe, or the file named by DESK_SETTINGS.
// Read once at start; a change needs a Desk restart. A missing file or field means today's value. An unreadable file
// means all the defaults, and the reason goes in the page's problems line. PDM_DESK and PDM_DESK_PORT (for tests)
// override the file. The Desk holds no project names in code: groups, steps and gates come from here.
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Desk;

public sealed class Settings
{
    public string DeskName { get; private set; } = "PDM Desk";
    public string DataFolder { get; private set; } = @"C:\code\pdm\desk";
    public int Port { get; private set; } = 5180;
    public List<string> ListenAddresses { get; private set; } = ["127.0.0.1"];
    public List<string> AllowedHosts { get; private set; } = ["127.0.0.1", "localhost"];
    public string AuthMode { get; private set; } = "none";
    public string User { get; private set; } = "grant";
    public List<string> FileRoots { get; private set; } = [@"C:\code\pdm\scratch\", @"C:\code\pdm\draw\"];
    public string? Workflow { get; private set; }
    public string? ScreenTypes { get; private set; }
    public string? Screens { get; private set; }
    public JsonArray SessionGroups { get; private set; } = DefaultGroups();
    public bool GroupsFromFile { get; private set; }     // the page draws group headings only when the file sets groups
    public JsonArray? Steps { get; private set; }
    public JsonArray? Gates { get; private set; }
    // Answer notices: a ticket owner -> the session told (an owner with no session on this PC, e.g. revit-fiona -> apex-admin).
    // An owner not listed is told under its own name.
    public Dictionary<string, string> NoticeRoutes { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string? File { get; private set; }          // the file read, if there was one
    public string Text { get; private set; } = "";      // its text, for the page version (open tabs reload after a change)
    public List<string> Problems { get; } = [];          // shown on the page, after "Couldn't read "

    // Today's sessions, in today's order and with today's labels.
    static JsonArray DefaultGroups() => new(new JsonObject
    {
        ["name"] = "PDM",
        ["sessions"] = new JsonArray(
            S("admin", "Admin"), S("dev", "Dev"), S("sw", "SW"), S("app", "App"), S("draw", "Draw"))
    });
    static JsonObject S(string id, string label) => new() { ["id"] = id, ["label"] = label };

    public static Settings Load()
    {
        var s = new Settings();
        var file = Environment.GetEnvironmentVariable("DESK_SETTINGS") is { Length: > 0 } f ? f : Path.Combine(AppContext.BaseDirectory, "settings.json");
        var name = Path.GetFileName(file);
        if (System.IO.File.Exists(file))
        {
            JsonObject? o = null;
            try
            {
                using var fs = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                s.Text = new StreamReader(fs).ReadToEnd();
                var node = JsonNode.Parse(s.Text, documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                o = node as JsonObject;
                if (o == null) s.Problems.Add($"{name} (not a JSON object); the Desk is using its built-in settings");
            }
            catch (JsonException ex) { s.Problems.Add($"{name} (not valid JSON, line {ex.LineNumber + 1}); the Desk is using its built-in settings"); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { s.Problems.Add($"{name} ({ex.GetType().Name}); the Desk is using its built-in settings"); }
            if (o != null) { s.File = file; s.Apply(o, name); }
            else s.Text = "";
        }
        else if (Environment.GetEnvironmentVariable("DESK_SETTINGS") is { Length: > 0 })
            s.Problems.Add($"{name} (DESK_SETTINGS names a file that isn't there); the Desk is using its built-in settings");

        // The test variables win over the file.
        if (Environment.GetEnvironmentVariable("PDM_DESK") is { Length: > 0 } d) s.DataFolder = d;
        if (int.TryParse(Environment.GetEnvironmentVariable("PDM_DESK_PORT"), out var p)) s.Port = p;
        return s;
    }

    void Apply(JsonObject o, string name)
    {
        string? Str(string key)
        {
            if (o[key] is not { } n) return null;
            if (n.GetValueKind() == JsonValueKind.String && ((string?)n)?.Trim() is { Length: > 0 } v) return v;
            Problems.Add($"{name}: \"{key}\" isn't text; using the built-in value");
            return null;
        }
        List<string>? List(string key)
        {
            if (o[key] is not { } n) return null;
            if (n is JsonArray a && a.All(x => x?.GetValueKind() == JsonValueKind.String) && a.Count > 0)
                return a.Select(x => ((string?)x)!.Trim()).Where(x => x != "").ToList();
            Problems.Add($"{name}: \"{key}\" isn't a list of text; using the built-in value");
            return null;
        }
        JsonArray? Array(string key)
        {
            if (o[key] is not { } n) return null;
            if (n is JsonArray a) return (JsonArray)a.DeepClone();
            Problems.Add($"{name}: \"{key}\" isn't a list; left out");
            return null;
        }

        DeskName = Str("deskName") ?? DeskName;
        DataFolder = Str("dataFolder") ?? DataFolder;
        if (o["port"] is { } port)
        {
            if (port.GetValueKind() == JsonValueKind.Number && port.AsValue().TryGetValue<int>(out var pv) && pv is > 0 and < 65536) Port = pv;
            else Problems.Add($"{name}: \"port\" isn't a port number; using {Port}");
        }
        ListenAddresses = List("listenAddresses") ?? ListenAddresses;
        AllowedHosts = List("allowedHosts") ?? AllowedHosts;
        if (o["auth"] is JsonObject auth && auth["mode"] is { } mode)
        {
            if (mode.GetValueKind() == JsonValueKind.String) AuthMode = ((string?)mode ?? "").Trim();
            else Problems.Add($"{name}: \"auth.mode\" isn't text; using \"none\"");
        }
        User = Str("user") ?? User;
        if (List("fileRoots") is { } roots) FileRoots = roots;
        if (o["register"] is JsonObject reg)
        {
            string? R(string key) => reg[key] is { } n && n.GetValueKind() == JsonValueKind.String && ((string?)n)?.Trim() is { Length: > 0 } v ? v : null;
            Workflow = R("workflow"); ScreenTypes = R("screenTypes"); Screens = R("screens");
        }
        if (Array("sessionGroups") is { } groups) { SessionGroups = groups; GroupsFromFile = true; }
        Steps = Array("steps");
        Gates = Array("gates");
        if (o["noticeRoutes"] is { } routes)
        {
            if (routes is JsonObject ro && ro.All(kv => kv.Value?.GetValueKind() == JsonValueKind.String))
                foreach (var kv in ro) NoticeRoutes[kv.Key.Trim()] = ((string?)kv.Value)!.Trim();
            else Problems.Add($"{name}: \"noticeRoutes\" isn't a list of owner: session pairs; left out");
        }
    }

    // Session id -> ticket id prefix, for the sessions that have one in settings (the id rule in app\desk.md).
    public Dictionary<string, string> Prefixes()
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var g in SessionGroups.OfType<JsonObject>())
            foreach (var x in (g["sessions"] as JsonArray ?? []).OfType<JsonObject>())
                if (x["id"] is JsonValue idv && idv.TryGetValue<string>(out var id) && x["prefix"] is JsonValue pv && pv.TryGetValue<string>(out var p) && p.Trim() != "")
                    map[id.Trim()] = p.Trim();
        return map;
    }

    public JsonObject? Gate(string? key) => (Gates ?? []).OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == key);

    // A gate's answer checked against settings: null when the gate or the outcome isn't there. Two kinds of gate:
    // a list of "outcomes" (each with passes / noteRequired), or "input": "code", a whole-number code (WDI, APEX-A78)
    // whose meanings come from "codes" [{ code, label, passes }]. While that list is empty any whole number is taken,
    // and whether it passes is unknown (Passes null).
    public (bool? Passes, bool NoteRequired, string Label)? Outcome(string? gate, string? choice, int? code)
    {
        if (Gate(gate) is not { } g) return null;
        if ((string?)g["input"] == "code")
        {
            if (choice != "code" || code is not >= 0) return null;
            var codes = (g["codes"] as JsonArray ?? []).OfType<JsonObject>().ToList();
            if (codes.Count == 0) return (null, false, $"Code {code}");
            var c = codes.FirstOrDefault(x => x["code"] is JsonValue v && v.TryGetValue<int>(out var n) && n == code);
            if (c == null) return null;
            var label = (string?)c["label"];
            return (c["passes"] is JsonValue pv && pv.TryGetValue<bool>(out var p) ? p : null, (bool?)c["noteRequired"] == true,
                $"Code {code}" + (string.IsNullOrWhiteSpace(label) ? "" : $" ({label})"));
        }
        var o = (g["outcomes"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == choice);
        if (o == null) return null;
        return ((bool?)o["passes"] == true, (bool?)o["noteRequired"] == true, (string?)o["label"] ?? choice!);
    }

    // A folder from fileRoots as a full path ending in a separator, so "C:\a\b" doesn't also allow "C:\a\bc".
    public IEnumerable<string> Roots() => FileRoots.Select(r =>
    {
        try { var full = Path.GetFullPath(r); return full.EndsWith('\\') ? full : full + '\\'; } catch { return null; }
    }).Where(r => r != null)!;
}
