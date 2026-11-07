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
    await next();
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
    var sw = new { @lock = store.ReadOptional(Path.Combine(store.Sw, "lock.json"), problems), pause = store.ReadOptional(Path.Combine(store.Sw, "pause.json"), problems) };
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
var sw = new SwTool();

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
        var count = (t["steps"] as JsonArray)?.Count ?? 0;
        var step = (int?)body?["step"] ?? 0;
        t["step"] = Math.Clamp(step, 0, Math.Max(0, count - 1));
        store.Write(file, t);
    }
    return Results.Ok(new { ok = true });
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
    return Results.Ok(new { ok = true });
});

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
