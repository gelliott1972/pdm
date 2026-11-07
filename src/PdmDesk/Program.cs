// PDM Desk: a small server on 127.0.0.1 that shows Grant the tickets and session status in C:\code\pdm\desk\
// and writes his answers back into the same files. Starting it again just opens the page.
using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Desk;

var Port = int.TryParse(Environment.GetEnvironmentVariable("PDM_DESK_PORT"), out var pp) ? pp : 5180; // the variable is for tests
var url = $"http://127.0.0.1:{Port}/";
var root = Environment.GetEnvironmentVariable("PDM_DESK") is { Length: > 0 } r ? r : @"C:\code\pdm\desk";
var openPage = !args.Contains("--no-browser");

if (await AlreadyRunning()) { if (openPage) Open(); return; }

var store = new Store(root);
store.Ensure();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, Port)); // this PC only
var app = builder.Build();

// Only this PC's own pages: the Host must be 127.0.0.1/localhost, and changes need the X-Desk header
// (another web site can't send that header without a preflight, which this server never allows).
app.Use(async (ctx, next) =>
{
    if (ctx.Request.Host.Host is not ("127.0.0.1" or "localhost")) { ctx.Response.StatusCode = 403; return; }
    if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Headers["X-Desk"] != "1") { ctx.Response.StatusCode = 403; return; }
    ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = $"The Desk hit an error: {ex.GetType().Name}: {ex.Message}" });
    }
});

var page = ReadPage();
app.MapGet("/", () => Results.Content(page, "text/html; charset=utf-8"));
app.MapGet("/api/ping", () => Results.Ok(new { desk = true }));

app.MapGet("/api/state", () =>
{
    var problems = new List<string>();
    var tickets = Tagged(store.Tickets, problems);
    var status = Tagged(store.Status, problems);
    var notes = Tagged(store.Inbox, problems)
        .OrderByDescending(n => (string?)n["createdAt"] ?? "").Take(5).ToList();
    var activity = Tagged(store.Activity, problems);
    var swLock = store.ReadOptional(Path.Combine(store.Sw, "lock.json"), problems);
    // A one-command lock whose SwCmd process has gone is stale (SwCmd takes it over): show SOLIDWORKS as free.
    if (swLock != null && (string?)swLock["kind"] == "command" && (int?)swLock["pid"] is int pid && !Running(pid)) swLock = null;
    var sw = new { @lock = swLock, pause = store.ReadOptional(Path.Combine(store.Sw, "pause.json"), problems) };
    return Results.Ok(new { root, tickets, status, activity, sw, notes, problems, now = Store.Now() });
});

app.MapPost("/api/answer", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    var choice = ((string?)body?["choice"] ?? "").Trim();
    var note = ((string?)body?["note"] ?? "").Trim();
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if (choice == "" && note == "") return Fail("Pick an option or type a note.");
    if (note.Length > 4000) return Fail("The note is too long (4000 characters at most).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if ((string?)t["type"] != "decision") return Fail("Only decision tickets take an answer.");
        var status = (string?)t["status"] ?? "open";
        if (status is not ("open" or "answered" or "done")) return Fail($"This ticket is {status}.");
        if (choice != "" && !(t["options"] as JsonArray ?? []).Any(o => (string?)o?["key"] == choice))
            return Fail("That option isn't on the ticket any more. Reload and try again.");
        if (t["answer"] is JsonObject old) // a changed answer keeps the earlier one
        {
            var history = t["history"] as JsonArray ?? [];
            history.Add(new JsonObject { ["choice"] = (string?)old["choice"], ["note"] = (string?)old["note"], ["answeredAt"] = (string?)t["answeredAt"] });
            t["history"] = history;
        }
        t["answer"] = new JsonObject { ["choice"] = choice, ["note"] = note };
        t["answeredAt"] = Store.Now();
        t["status"] = "answered";
        store.Write(file, t);
    }
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/dismiss", async (HttpRequest req) =>
{
    var id = (string?)(await Body(req))?["id"];
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if ((string?)t["type"] != "info") return Fail("Only info tickets can be dismissed.");
        t["status"] = "dismissed";
        t["answeredAt"] = Store.Now();
        store.Write(file, t);
    }
    return Results.Ok(new { ok = true });
});

app.MapPost("/api/note", async (HttpRequest req) =>
{
    var text = ((string?)(await Body(req))?["text"] ?? "").Trim();
    if (text == "") return Fail("Type a note first.");
    if (text.Length > 4000) return Fail("The note is too long (4000 characters at most).");
    var id = $"{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid().ToString("N")[..4]}";
    store.Write(Path.Combine(store.Inbox, id + ".json"), new JsonObject { ["id"] = id, ["text"] = text, ["createdAt"] = Store.Now() });
    return Results.Ok(new { ok = true, id });
});

// ---- sw-job tickets (phase 2): Start opens the files in SOLIDWORKS, Next/Back move through the steps, Done/Problem answer.
var sw = new SwTool { DeskDir = root };
var pauseFile = Path.Combine(store.Sw, "pause.json");

app.MapPost("/api/sw/start", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if ((bool?)body?["confirmed"] != true) return Fail("Confirm first: Start closes everything in SOLIDWORKS without saving.");
    var file = store.TicketFile(id!);
    JsonObject? t;
    lock (store.Lock) t = store.Read(file, out _);
    if (t == null || (string?)t["type"] != "sw-job") return Fail("Unknown SOLIDWORKS job.");
    if ((string?)t["status"] is not (null or "open" or "started")) return Fail($"This job is {(string?)t["status"]}.");
    var files = (t["files"] as JsonArray ?? []).Select(f => ((string?)f?["path"], (string?)f?["configuration"])).ToList();
    if (files.Count == 0) return Fail("The job lists no files.");
    var bad = files.Select(f => SwTool.Refusal(f.Item1)).FirstOrDefault(r => r != null);
    if (bad != null) return Fail($"Not started: {bad}.");
    if (!sw.TryBegin()) return Fail("SOLIDWORKS is busy with another job. Wait for it to finish.");

    var log = new System.Text.StringBuilder();
    var ok = true; var message = "";
    try
    {
        // Grant takes SOLIDWORKS first (lock.json as "grant"); a session holding it means Pause, then Start again.
        var held = await sw.Lock(id!);
        if (held.Code == 5) return Fail($"SOLIDWORKS is in use: {Last(held.Output)}. Press Pause automatic work, wait for \"You have SOLIDWORKS\", then Start again.");
        if (held.Code != 0) return Fail($"Couldn't take SOLIDWORKS: {Last(held.Output)}");
        // The first call may have to start SOLIDWORKS, so it gets longer (Dev: about 5 minutes).
        var first = true;
        TimeSpan Limit() { var l = first ? TimeSpan.FromMinutes(5) : TimeSpan.FromMinutes(3); first = false; return l; }
        var problems = 0;
        if ((bool?)t["closeAll"] != false)
        {
            var r = await sw.CloseAll(Limit());
            log.AppendLine("> close-all").Append(r.Output);
            if (r.Code >= 2) { ok = false; message = r.TimedOut ? "Closing documents timed out." : "Closing documents failed; nothing was opened."; }
            else if (r.Code == 1) problems++;
        }
        var opened = 0;
        foreach (var (path, cfg) in files)
        {
            if (!ok) break;
            var r = await sw.Open(path!, cfg, Limit());
            log.AppendLine($"> open {path}{(string.IsNullOrWhiteSpace(cfg) ? "" : $" --config {cfg}")}").Append(r.Output);
            if (r.Code >= 2) { ok = false; message = r.TimedOut ? $"Opening {Path.GetFileName(path)} timed out." : $"Couldn't open {Path.GetFileName(path)}."; }
            else { opened++; if (r.Code == 1) problems++; }
        }
        if (ok) message = $"Opened {opened} file{(opened == 1 ? "" : "s")}" + (problems > 0 ? $"; SOLIDWORKS reported problems (see details)." : ".");
        lock (store.Lock)
        {
            t = store.Read(file, out var why);
            if (t == null) return Fail($"Opened, but couldn't update the ticket ({why}).");
            var text = log.ToString();
            if (text.Length > 12000) text = "…" + text[^12000..];
            t["run"] = new JsonObject { ["at"] = Store.Now(), ["ok"] = ok, ["problems"] = problems, ["message"] = message, ["output"] = text };
            t["startedAt"] = Store.Now();
            t["status"] = "started";
            t["step"] ??= 0;
            store.Write(file, t);
        }
    }
    finally { sw.End(); }
    return Results.Ok(new { ok, message });
});

app.MapPost("/api/sw/step", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null || (string?)t["type"] != "sw-job") return Fail("Unknown SOLIDWORKS job.");
        if ((string?)t["status"] is not (null or "open" or "started")) return Fail($"This job is {(string?)t["status"]}.");
        var steps = t["steps"] as JsonArray ?? [];
        var count = steps.Count;
        var step = Math.Clamp((int?)body?["step"] ?? 0, 0, Math.Max(0, count - 1));
        var now = Math.Clamp((int?)t["step"] ?? 0, 0, Math.Max(0, count - 1));
        // Moving on past a step with a PDM action needs that action done first (Next on such a step runs it).
        for (var i = now; i < step; i++)
            if (steps[i] is JsonObject so && so["action"] is JsonObject && (bool?)(t["actions"] as JsonObject)?[i.ToString()]?["ok"] != true)
                return Fail($"Step {i + 1}'s PDM action hasn't succeeded yet.");
        t["step"] = step;
        store.Write(file, t);
    }
    return Results.Ok(new { ok = true });
});

// A step's PDM action (download-latest, check-out, check-in) through the running PDM app, as its logged-in engineer.
// Moves to the next step only if every file ended up as asked; the result is kept in the ticket's "actions".
var pdm = new PdmApp();
var pdmBusy = new SemaphoreSlim(1, 1);
app.MapPost("/api/sw/action", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    var file = store.TicketFile(id!);
    JsonObject? t;
    lock (store.Lock) t = store.Read(file, out _);
    if (t == null || (string?)t["type"] != "sw-job") return Fail("Unknown SOLIDWORKS job.");
    if ((string?)t["status"] is not (null or "open" or "started")) return Fail($"This job is {(string?)t["status"]}.");
    var steps = t["steps"] as JsonArray ?? [];
    var n = (int?)t["step"] ?? 0;
    if ((int?)body?["step"] != n) return Fail("The job moved on; reload the page.");
    if (n < 0 || n >= steps.Count || steps[n] is not JsonObject so || so["action"] is not JsonObject action) return Fail("This step has no PDM action.");
    var type = (string?)action["type"] ?? "";
    if (!PdmApp.Types.Contains(type)) return Fail($"Unknown PDM action \"{type}\".");
    var note = ((string?)action["note"] ?? "").Trim();
    if (type == "check-in" && note == "") return Fail("This check-in step has no note; the owner session needs to add one.");
    if (type == "check-in" && (bool?)body?["confirmed"] != true) return Fail("Confirm the check-in first.");
    var raw = (action["paths"] as JsonArray ?? []).Select(p => (string?)p).ToList();
    if (raw.Count == 0) return Fail("The step lists no files.");
    var keys = new List<string>();
    foreach (var p in raw)
    {
        var k = PdmApp.KeyOf(p);
        if (k == null) return Fail($"Not done: {p} isn't a vault file path under P:\\.");
        keys.Add(k);
    }

    if (!pdmBusy.Wait(0)) return Fail("A PDM step is already running.");
    try
    {
        var (who, why) = await pdm.Me();
        if (who == null) return Fail($"{why} {PdmApp.NotReady}");
        var (before, why2) = await pdm.Files();
        if (before == null) return Fail($"{why2} {PdmApp.NotReady}");
        var unknown = keys.Where(k => !before.ContainsKey(k)).ToList();
        if (unknown.Count > 0) return Fail($"Not done: not in the vault: {string.Join(", ", unknown)}.");

        var (log, why3) = await pdm.Act(type, keys, note);
        if (log == null) return Fail($"{why3} {PdmApp.NotReady}");
        var (after, why4) = await pdm.Files();
        var results = new JsonArray();
        var ok = after != null;
        foreach (var k in keys)
        {
            var name = Path.GetFileName(k);
            var outcome = after == null ? "couldn't check the result: " + why4 : PdmApp.Outcome(type, after.GetValueOrDefault(k));
            var said = log.Where(l => l.Contains(name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (outcome != null) ok = false;
            results.Add(new JsonObject { ["path"] = k, ["ok"] = outcome == null, ["why"] = outcome ?? "", ["log"] = string.Join(" / ", said) });
        }
        var result = new JsonObject
        {
            ["at"] = Store.Now(), ["type"] = type, ["ok"] = ok, ["by"] = who, ["files"] = results,
            ["log"] = new JsonArray(log.Select(l => (JsonNode?)l).ToArray())
        };
        lock (store.Lock)
        {
            t = store.Read(file, out var whyRead);
            if (t == null) return Fail($"Done in PDM, but couldn't update the ticket ({whyRead}).");
            var actions = t["actions"] as JsonObject ?? new JsonObject();
            actions[n.ToString()] = result;
            t["actions"] = actions;
            if (ok && n < steps.Count - 1) t["step"] = n + 1;
            store.Write(file, t);
        }
        return Results.Ok(new { ok, result = result.DeepClone() });
    }
    finally { pdmBusy.Release(); }
});

app.MapPost("/api/sw/finish", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    var choice = (string?)body?["choice"] ?? "";
    var note = ((string?)body?["note"] ?? "").Trim();
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if (choice is not ("done" or "problem")) return Fail("Done or Problem only.");
    if (choice == "problem" && note == "") return Fail("Say what the problem is.");
    if (note.Length > 4000) return Fail("The note is too long (4000 characters at most).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null || (string?)t["type"] != "sw-job") return Fail("Unknown SOLIDWORKS job.");
        if ((string?)t["status"] is not (null or "open" or "started" or "answered" or "done")) return Fail($"This job is {(string?)t["status"]}.");
        if (t["answer"] is JsonObject old)
        {
            var history = t["history"] as JsonArray ?? [];
            history.Add(new JsonObject { ["choice"] = (string?)old["choice"], ["note"] = (string?)old["note"], ["answeredAt"] = (string?)t["answeredAt"] });
            t["history"] = history;
        }
        t["answer"] = new JsonObject { ["choice"] = choice, ["note"] = note };
        t["answeredAt"] = Store.Now();
        t["status"] = "answered";
        store.Write(file, t);
    }
    await HandBack(); // Done or Problem ends Grant's turn with SOLIDWORKS
    return Results.Ok(new { ok = true });
});

// ---- Pause automatic work / Hand back (Desk v2): pause.json asks the sessions to stop at a safe point; SwCmd refuses
// their commands (exit 4) while it exists, but not Grant's.
app.MapPost("/api/sw/pause", () =>
{
    Directory.CreateDirectory(store.Sw);
    if (!File.Exists(pauseFile)) store.Write(pauseFile, new JsonObject { ["requestedAt"] = Store.Now(), ["by"] = "grant" });
    return Results.Ok(new { ok = true });
});
app.MapPost("/api/sw/handback", async () =>
{
    var r = await HandBack();
    return r == null ? Results.Ok(new { ok = true }) : Fail(r);
});

async Task<string?> HandBack()
{
    try { File.Delete(pauseFile); } catch (IOException) { }
    var r = await sw.Unlock();
    return r.Code == 0 ? null : $"Couldn't release SOLIDWORKS: {Last(r.Output)}";
}

static bool Running(int pid)
{
    try { using var p = Process.GetProcessById(pid); return !p.HasExited; } catch { return false; }
}

static string Last(string output)
{
    var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(l => !l.StartsWith("Attached to", StringComparison.Ordinal)).ToArray();
    return lines.Length == 0 ? "no details" : string.Join(" / ", lines.TakeLast(2));
}

if (openPage) app.Lifetime.ApplicationStarted.Register(Open);
app.Run();

// Each object carries its file name as _file, so the page answers the file it was shown.
List<JsonObject> Tagged(string dir, List<string> problems) => store.All(dir, problems);

static async Task<JsonNode?> Body(HttpRequest req)
{
    try { return await JsonNode.ParseAsync(req.Body); } catch { return null; }
}

static IResult Fail(string why) => Results.BadRequest(new { error = why });

void Open()
{
    try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
}

async Task<bool> AlreadyRunning()
{
    try
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(1) };
        var s = await http.GetStringAsync(url + "api/ping");
        return s.Contains("desk");
    }
    catch { return false; }
}

static string ReadPage()
{
    using var s = typeof(Store).Assembly.GetManifestResourceStream("index.html")!;
    return new StreamReader(s).ReadToEnd();
}
