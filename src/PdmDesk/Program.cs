// PDM Desk: a small server on 127.0.0.1 that shows Grant the tickets and session status in C:\code\pdm\desk\
// and writes his answers back into the same files. Starting it again just opens the page.
using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Desk;

const int Port = 5180;
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
    return Results.Ok(new { root, tickets, status, notes, problems, now = Store.Now() });
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
