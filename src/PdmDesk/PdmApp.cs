// The PDM app's own local page API (http://localhost:5170/), used for sw-job PDM steps: the Desk asks the running PDM
// app, which acts as the engineer who is logged in there. The Desk itself has no AWS access.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Desk;

public class PdmApp
{
    public string Url { get; } = Environment.GetEnvironmentVariable("PDM_PAGE_URL") is { Length: > 0 } u ? u.TrimEnd('/') + "/" : "http://localhost:5170/";
    readonly HttpClient http = new() { Timeout = TimeSpan.FromMinutes(15) };

    public const string NotReady = "Open PDM and log in, then press Next again.";

    public static readonly string[] Types = ["download-latest", "check-out", "check-in"];

    // A ticket path as a vault key: "P:\APEX\x.SLDPRT" or "APEX/x.SLDPRT" -> "APEX/x.SLDPRT". Null if it isn't one.
    public static string? KeyOf(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var p = path.Trim().Replace('\\', '/');
        if (p.StartsWith("P:/", StringComparison.OrdinalIgnoreCase)) p = p[3..];
        else if (p.Contains(':') || p.StartsWith('/')) return null; // another drive or an absolute path
        var parts = p.Split('/');
        if (parts.Any(x => x is "" or "." or ".." || x.Equals(".pdm", StringComparison.OrdinalIgnoreCase))) return null;
        return string.Join('/', parts);
    }

    async Task<(HttpStatusCode? Code, JsonNode? Body, string Error)> Call(HttpMethod method, string path, object? body)
    {
        try
        {
            using var req = new HttpRequestMessage(method, Url + path);
            req.Headers.Add("X-PDM", "1");
            // A plain body with a length (JsonContent would send it chunked).
            if (body != null) req.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(body), System.Text.Encoding.UTF8, "application/json");
            using var r = await http.SendAsync(req);
            var text = await r.Content.ReadAsStringAsync();
            JsonNode? node = null;
            try { node = JsonNode.Parse(text); } catch { }
            return (r.StatusCode, node, node is JsonObject o ? (string?)o["error"] ?? "" : ""); // /api/files answers with a list
        }
        catch (HttpRequestException) { return (null, null, "PDM isn't running on this PC."); }
        catch (TaskCanceledException) { return (null, null, "PDM didn't answer in time."); }
    }

    // Who is logged in, or why not.
    public async Task<(string? Name, string Why)> Me()
    {
        var (code, body, err) = await Call(HttpMethod.Post, "api/me", new { });
        if (code == null) return (null, err);
        if (code == HttpStatusCode.Unauthorized) return (null, "PDM is open but nobody is logged in.");
        if (code != HttpStatusCode.OK) return (null, $"PDM said: {(err == "" ? code.ToString() : err)}");
        return ((string?)body?["name"] ?? (string?)body?["email"] ?? "?", "");
    }

    public async Task<(Dictionary<string, JsonObject>? Files, string Why)> Files()
    {
        var (code, body, err) = await Call(HttpMethod.Post, "api/files", new { });
        if (code != HttpStatusCode.OK || body is not JsonArray list) return (null, code == null ? err : $"PDM couldn't list the vault ({(err == "" ? code.ToString() : err)}).");
        var d = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in list.OfType<JsonObject>())
            if ((bool?)f["isNew"] != true && (string?)f["path"] is { } p) d[p] = f;
        return (d, "");
    }

    public async Task<(List<string>? Log, string Why)> Act(string type, List<string> keys, string note)
    {
        var (path, body) = type switch
        {
            "download-latest" => ("api/get", (object)new { paths = keys }),
            "check-out" => ("api/checkout", new { paths = keys }),
            _ => ("api/checkin", new { paths = keys, note })
        };
        var (code, resp, err) = await Call(HttpMethod.Post, path, body);
        if (code != HttpStatusCode.OK) return (null, code == null ? err : code == HttpStatusCode.Unauthorized ? "PDM is open but nobody is logged in." : $"PDM said: {(err == "" ? code.ToString() : err)}");
        return ((resp?["log"] as JsonArray ?? []).Select(x => (string?)x ?? "").ToList(), "");
    }

    // Did the file end up as the action wanted? Null = yes, otherwise why not.
    public static string? Outcome(string type, JsonObject? f)
    {
        if (f == null) return "not in the vault any more";
        bool mine = (bool?)f["mine"] == true, newer = (bool?)f["newer"] == true, missing = (bool?)f["missing"] == true;
        var holder = (string?)f["lockedByName"];
        return type switch
        {
            "check-out" => mine ? null : holder != null ? $"checked out by {holder}" : "not checked out",
            "check-in" => !mine ? null : (bool?)f["changed"] == true ? "still checked out to you, with changes" : "still checked out to you",
            _ => mine ? null : newer || missing ? "not downloaded" : null
        };
    }
}
