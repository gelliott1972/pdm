// PDM Desk: a small server on 127.0.0.1 that shows Grant the tickets and session status in C:\code\pdm\desk\
// and writes his answers back into the same files. Starting it again just opens the page.
using System.Diagnostics;
using System.Net;
using System.Text.Json.Nodes;
using Desk;

// settings.json beside the exe (or DESK_SETTINGS); PDM_DESK and PDM_DESK_PORT (for tests) override it. No file: as before.
var settings = Settings.Load();
var Port = settings.Port;
var url = $"http://127.0.0.1:{Port}/";
var root = settings.DataFolder;
var fileRoots = settings.Roots().ToArray(); // the only folders the Desk serves files from
var openPage = !args.Contains("--no-browser");

// Remote-ready, switched off (stage 2, item 8). The Desk listens on each of listenAddresses and always on 127.0.0.1
// (Start again and the start scripts use it). Fail closed: an address that isn't this PC only, while auth.mode is "none",
// or any auth mode but "none" (the only one built), and the Desk refuses to start, saying why in desk\desk-error.txt.
var listen = new List<IPAddress> { IPAddress.Loopback };
string? refuse = settings.AuthMode == "none" ? null : $"auth.mode \"{settings.AuthMode}\" isn't built; the only mode is \"none\" (this PC only)";
foreach (var a in settings.ListenAddresses)
{
    if (a.Equals("localhost", StringComparison.OrdinalIgnoreCase)) continue;
    if (!IPAddress.TryParse(a, out var ip)) { refuse ??= $"listenAddresses has \"{a}\", which isn't an IP address"; continue; }
    if (!IPAddress.IsLoopback(ip) && settings.AuthMode == "none")
        refuse ??= $"listenAddresses has {a}, which other computers could reach, and auth.mode is \"none\", so anyone reaching it could answer tickets";
    else if (!listen.Contains(ip)) listen.Add(ip);
}
var errorFile = Path.Combine(root, "desk-error.txt");
if (refuse != null)
{
    try { Directory.CreateDirectory(root); File.WriteAllText(errorFile, $"{Store.Now()} The Desk didn't start: {refuse}.{Environment.NewLine}Fix {(settings.File != null ? Path.GetFileName(settings.File) : "the settings")} beside PdmDesk.exe and start it again.{Environment.NewLine}"); } catch { }
    return 2;
}

if (await AlreadyRunning()) { if (openPage) Open(); return 0; }
try { File.Delete(errorFile); } catch { } // an old refusal no longer applies

var store = new Store(root);
store.Ensure();
var notices = new Notices(store, settings); // the owning session is told of each answer (desk\notices\<session>\)
notices.Prune();

var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = [], ContentRootPath = AppContext.BaseDirectory });
builder.Logging.ClearProviders();
builder.WebHost.ConfigureKestrel(k => { foreach (var ip in listen) k.Listen(ip, Port); }); // 127.0.0.1 unless settings say more
var app = builder.Build();

// Every request: the Host must be one of allowedHosts (127.0.0.1/localhost), changes need the X-Desk header (another
// web site can't send that header without a preflight, which this server never allows), then Authorise. No endpoint
// does its own check.
// The endpoints that drive SOLIDWORKS or the PDM app on this PC: Authorise allows them only from this PC.
string[] LocalOnly = ["/api/sw/start", "/api/sw/step", "/api/sw/action", "/api/sw/finish", "/api/sw/pause", "/api/sw/handback"];
var allowedHosts = settings.AllowedHosts.Select(h => h.ToLowerInvariant()).ToHashSet();
app.Use(async (ctx, next) =>
{
    if (!allowedHosts.Contains(ctx.Request.Host.Host.ToLowerInvariant())) { ctx.Response.StatusCode = 403; return; }
    if (HttpMethods.IsPost(ctx.Request.Method) && ctx.Request.Headers["X-Desk"] != "1") { ctx.Response.StatusCode = 403; return; }
    var (user, refusal) = Authorise(ctx);
    if (user == null) { ctx.Response.StatusCode = 403; await ctx.Response.WriteAsJsonAsync(new { error = refusal }); return; }
    ctx.Items["user"] = user;
    ctx.Response.Headers.CacheControl = "no-store";
    try { await next(); }
    catch (Exception ex) when (!ctx.Response.HasStarted)
    {
        ctx.Response.StatusCode = 500;
        await ctx.Response.WriteAsJsonAsync(new { error = NoPaths($"The Desk hit an error: {ex.GetType().Name}: {ex.Message}") });
    }
});

// The one authorisation point: the user, or why not. In mode "none" (the only one built) it is settings' user
// ("grant") and nothing is refused, except that the this-PC-only actions need a connection from this PC.
(string? User, string? Refusal) Authorise(HttpContext ctx)
{
    if (settings.AuthMode != "none") return (null, "Not signed in.");
    var path = ctx.Request.Path.Value ?? "";
    if (LocalOnly.Contains(path, StringComparer.OrdinalIgnoreCase))
    {
        var from = ctx.Connection.RemoteIpAddress;
        if (from?.IsIPv4MappedToIPv6 == true) from = from.MapToIPv4();
        if (from == null || !IPAddress.IsLoopback(from)) return (null, "This only works at the Desk's own PC.");
    }
    return (settings.User, null);
}
string Who(HttpRequest req) => req.HttpContext.Items["user"] as string ?? settings.User;

// The Desk's name (settings deskName) is filled in on the server, so the page never shows the old name first.
var page = Named(ReadPage("index.html"));
var review = Named(ReadPage("review.html"));
// The pages' version: an open tab reloads itself when it changes (after an install or a settings change), so it never
// runs old code or old settings.
var pageVersion = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(page + review + settings.Text)))[..12];
string Named(string html) => html
    .Replace("{{deskNameJs}}", System.Text.Json.JsonSerializer.Serialize(settings.DeskName)) // a JS string; < > & ' escaped
    .Replace("{{deskName}}", WebUtility.HtmlEncode(settings.DeskName));
app.MapGet("/", () => Results.Content(page, "text/html; charset=utf-8"));
// The draft Review screen (Grant, 15 Nov): one draft at a time, the PDF large, Accept or Redo, then the next.
app.MapGet("/review", () => Results.Content(review, "text/html; charset=utf-8"));
app.MapGet("/api/ping", () => Results.Ok(new { desk = true }));

var prefixes = settings.Prefixes();
app.MapGet("/api/state", () =>
{
    var problems = new List<string>(settings.Problems);
    problems.AddRange(notices.Problems());
    var tickets = Tagged(store.Tickets, problems);
    var status = Tagged(store.Status, problems);
    var notes = Tagged(store.Inbox, problems)
        .OrderByDescending(n => (string?)n["createdAt"] ?? "").Take(5).ToList();
    var activity = Tagged(store.Activity, problems);
    var swLock = store.ReadOptional(Path.Combine(store.Sw, "lock.json"), problems);
    // A one-command lock whose SwCmd process has gone is stale (SwCmd takes it over): show SOLIDWORKS as free.
    if (swLock != null && (string?)swLock["kind"] == "command" && (int?)swLock["pid"] is int pid && !Running(pid)) swLock = null;
    var sw = new { @lock = swLock, pause = store.ReadOptional(Path.Combine(store.Sw, "pause.json"), problems) };
    // The Desk's name and session groups from settings (labels and order; headings only when the file sets groups).
    var desk = new { name = settings.DeskName, groups = settings.SessionGroups, grouped = settings.GroupsFromFile, gates = settings.Gates };
    // The id rule (app\desk.md): a ticket from a session with a prefix in settings has an id starting with it.
    // Only told, never enforced, and only for tickets still in play (existing tickets are never renamed).
    var warnings = new List<string>();
    foreach (var t in tickets)
    {
        var id = (string?)t["id"] ?? (string?)t["_file"] ?? "";
        if ((string?)t["status"] is "done" or "dismissed") continue;
        if ((string?)t["owner"] is { } owner && prefixes.TryGetValue(owner, out var px) && !id.StartsWith(px, StringComparison.OrdinalIgnoreCase))
            warnings.Add($"Ticket {id} is from {owner}, but its id doesn't start with \"{px}\".");
    }
    // No local paths to the browser: a PDF or drawing is only flagged (the PDF is served by id), attachments lose their
    // path. sw-job file paths stay: they are vault paths Grant reads on the card, and those jobs are this-PC-only.
    foreach (var t in tickets)
    {
        foreach (var k in new[] { "pdf", "drawing" })
            if (t[k] is JsonValue v && v.TryGetValue<string>(out var s) && s != "") t[k] = true;
        foreach (var a in (t["attachments"] as JsonArray ?? []).OfType<JsonObject>()) a.Remove("path");
    }
    problems = problems.Select(NoPaths).ToList();
    // Answers a session hasn't picked up for 10 minutes or more: its notice watch isn't running (26 Nov).
    var noticeLag = notices.Lag();
    return Results.Ok(new { tickets, status, activity, sw, notes, problems, warnings, desk, noticeLag, now = Store.Now(), version = pageVersion });
});

// The board (stage 2, item 5): where each screen type is up to, read from the register in settings; polled by the
// main page every 10 seconds. Without register and steps in settings it is simply not shown.
var board = new Board(settings);
app.MapGet("/api/board", () =>
{
    if (!board.Enabled) return Results.Ok(new { enabled = false });
    var b = board.Build(store.All(store.Tickets, []));
    b["enabled"] = true;
    return Results.Ok(b);
});

// Gate tickets (stage 2, item 6): posted by apex-admin, one per screen and gate. Grant's answer is written to the ticket
// only; apex-admin updates the register (workflow.csv) and sets the ticket done. The Desk never writes the register.
app.MapPost("/api/gate", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    var choice = ((string?)body?["choice"] ?? "").Trim();
    var note = ((string?)body?["note"] ?? "").Trim();
    int? code = body?["code"] is JsonValue cv && cv.TryGetValue<int>(out var cn) ? cn : null;
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if (note.Length > 4000) return Fail("The note is too long (4000 characters at most).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if ((string?)t["type"] != "gate") return Fail("Only gate tickets take this.");
        var status = (string?)t["status"] ?? "open";
        if (status is not ("open" or "discuss" or "answered" or "done")) return Fail($"This ticket is {status}.");
        var gateKey = (string?)t["gate"];
        if (settings.Gate(gateKey) is not { } gate) return Fail($"The gate \"{gateKey}\" isn't in the Desk's settings.");
        var outcome = settings.Outcome(gateKey, choice, code);
        if (outcome == null)
            return Fail((string?)gate["input"] == "code" ? "Type the code as a whole number" + ((gate["codes"] as JsonArray)?.Count > 0 ? ", one of the codes listed." : ".")
                : "That answer isn't on this gate any more. Reload and try again.");
        if (outcome.Value.NoteRequired && note == "") return Fail("This answer needs a note: say why.");
        // Asked on older steel: a passing (or not yet known) answer needs a confirm.
        var (screenPin, steel) = board.Current((string?)t["screen"]);
        if (outcome.Value.Passes != false && (bool?)body?["confirmed"] != true && Board.Utc(steel) > Board.Utc((string?)t["pin"]))
            return Fail("The steel has changed since this was asked. Confirm to answer anyway.");
        if (t["answer"] is JsonObject old) // a changed answer keeps the earlier one, with the same fields
        {
            if (t["history"] is not JsonArray history) { history = []; t["history"] = history; }
            var h = (JsonObject)old.DeepClone();
            h["answeredAt"] = (string?)t["answeredAt"];
            history.Add(h);
        }
        var answer = new JsonObject { ["choice"] = choice };
        if (code != null && choice == "code") answer["code"] = code;
        answer["note"] = note;
        answer["pin"] = (string?)t["pin"];
        answer["screenPin"] = screenPin;
        answer["by"] = Who(req);
        t["answer"] = answer;
        t["answeredAt"] = Store.Now();
        t["status"] = "answered";
        store.Write(file, t);
        notices.Tell(id!, t, Who(req));
    }
    return Results.Ok(new { ok = true });
});

// A ticket's attachment by ticket id and key (stage 2, items 6 and 8): only a .pdf, .png or .jpg under a fileRoots
// folder; the path itself never goes to the browser.
app.MapGet("/api/attachment", (string? id, string? key) =>
{
    if (!Store.IdOk(id) || string.IsNullOrEmpty(key)) return Results.NotFound();
    var t = store.Read(store.TicketFile(id!), out _);
    var a = (t?["attachments"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(x => (string?)x["key"] == key);
    if ((string?)a?["path"] is not { Length: > 0 } path || !Path.IsPathFullyQualified(path)) return Results.NotFound();
    string full;
    try { full = Path.GetFullPath(path); } catch { return Results.NotFound(); }
    var type = Path.GetExtension(full).ToLowerInvariant() switch { ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg", _ => null };
    if (type == null || !fileRoots.Any(r => full.StartsWith(r, StringComparison.OrdinalIgnoreCase)) || !File.Exists(full)) return Results.NotFound();
    return Results.File(full, type, enableRangeProcessing: true);
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
        if (status is not ("open" or "answered" or "done" or "discuss")) return Fail($"This ticket is {status}.");
        if (choice != "" &&!(t["options"] as JsonArray ?? []).Any(o => (string?)o?["key"] == choice))
            return Fail("That option isn't on the ticket any more. Reload and try again.");
        if (t["answer"] is JsonObject old) // a changed answer keeps the earlier one
        {
            if (t["history"] is not JsonArray history) { history = []; t["history"] = history; }
            history.Add(new JsonObject { ["choice"] = (string?)old["choice"], ["note"] = (string?)old["note"], ["answeredAt"] = (string?)t["answeredAt"] });
        }
        t["answer"] = new JsonObject { ["choice"] = choice, ["note"] = note };
        t["answeredAt"] = Store.Now();
        t["status"] = "answered";
        store.Write(file, t);
        notices.Tell(id!, t, Who(req));
    }
    return Results.Ok(new { ok = true });
});

// "Other, let's discuss": Grant's comment goes on the ticket's thread and the status becomes "discuss". The owner
// session replies by appending to the thread; picking an option later (/api/answer) closes it. The thread is kept.
app.MapPost("/api/discuss", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    var text = ((string?)body?["text"] ?? "").Trim();
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if (text == "") return Fail("Type your comment first.");
    if (text.Length > 4000) return Fail("The comment is too long (4000 characters at most).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if ((string?)t["type"] is not ("decision" or "gate")) return Fail("Only decision and gate tickets can be discussed.");
        var status = (string?)t["status"] ?? "open";
        if (status is not ("open" or "answered" or "done" or "discuss")) return Fail($"This ticket is {status}.");
        if (t["thread"] is not JsonArray thread) { thread = []; t["thread"] = thread; }
        thread.Add(new JsonObject { ["from"] = Who(req), ["text"] = text, ["at"] = Store.Now() });
        t["status"] = "discuss";
        store.Write(file, t);
        notices.Tell(id!, t, Who(req), text);
    }
    return Results.Ok(new { ok = true });
});

// Draft reviews (Grant's D8, 15 Nov; docs\briefs\desk-drafts.md): one ticket per Draw draft. Accept, Redo (with what's
// wrong) or a review comment (thread, status discuss). Draw posts the next version on the same ticket.
app.MapPost("/api/draft", async (HttpRequest req) =>
{
    var body = await Body(req);
    var id = (string?)body?["id"];
    var action = (string?)body?["action"] ?? "";
    var text = ((string?)body?["text"] ?? "").Trim();
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    if (action is not ("accept" or "redo" or "comment" or "undo")) return Fail("Unknown action.");
    if (action is "redo" or "comment" && text == "") return Fail(action == "redo" ? "Say what's wrong, then press Redo." : "Type your comment first.");
    if (text.Length > 4000) return Fail("The note is too long (4000 characters at most).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if (action == "undo")
        {
            // Undo of the answer just given on the Review screen: the draft waits for Grant again. Only while it's still
            // "answered" on the same version (once Draw has taken it, or posted a new version, it's Draw's).
            if ((string?)t["type"] != "draft-review") return Fail("Only draft-review tickets take this.");
            if ((string?)t["status"] != "answered" || t["answer"] == null) return Fail(t["status"]?.ToString() == "done" ? "Draw has already taken this answer, so it can't be undone here. Use Change answer, or tell Draw." : "There's no answer to undo.");
            if ((int?)body?["version"] is int v && v != ((int?)t["version"] ?? 1)) return Fail("Draw has posted a new version since, so there's nothing to undo.");
            t.Remove("answer"); t.Remove("answeredAt");
            t["status"] = (t["thread"] as JsonArray)?.Count > 0 ? "discuss" : "open";
        }
        else if (!DraftAnswer(t, action, text, Who(req), out var refused)) return Fail(refused);
        store.Write(file, t);
        if (action != "undo") notices.Tell(id!, t, Who(req), action == "comment" ? text : null);
    }
    return Results.Ok(new { ok = true });
});

// Accept all remaining: every open or discussed draft of one batch.
app.MapPost("/api/draft/accept-batch", async (HttpRequest req) =>
{
    var batch = (string?)(await Body(req))?["batch"] ?? "";
    if (batch == "") return Fail("Which batch?");
    int n = 0;
    lock (store.Lock)
    {
        foreach (var file in Directory.GetFiles(store.Tickets, "*.json"))
        {
            var t = store.Read(file, out _);
            if (t == null || (string?)t["type"] != "draft-review" || (string?)t["batch"] != batch) continue;
            if ((string?)t["status"] is not (null or "open" or "discuss")) continue;
            if (DraftAnswer(t, "accept", "", Who(req), out _)) { store.Write(file, t); notices.Tell(Path.GetFileNameWithoutExtension(file), t, Who(req)); n++; }
        }
    }
    return Results.Ok(new { ok = true, accepted = n });
});

// The draft's PDF, shown on the page. Only a PDF under a fileRoots folder (default: the repo's scratch and draw
// folders), named on a draft-review ticket.
app.MapGet("/api/pdf", (string? id) =>
{
    if (!Store.IdOk(id)) return Results.NotFound();
    var t = store.Read(store.TicketFile(id!), out _);
    if (t == null || (string?)t["type"] != "draft-review" || (string?)t["pdf"] is not { Length: > 0 } pdf) return Results.NotFound();
    string full;
    try { full = Path.GetFullPath(pdf); } catch { return Results.NotFound(); }
    var allowed = fileRoots;
    if (!Path.IsPathFullyQualified(pdf) || !allowed.Any(a => full.StartsWith(a, StringComparison.OrdinalIgnoreCase))
        || !full.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) || !File.Exists(full)) return Results.NotFound();
    return Results.File(full, "application/pdf", enableRangeProcessing: true);
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

// Snooze (Grant, via apex-admin, 28 Nov): an open ticket leaves the list and the counts for 1 to 24 hours, then comes back
// as it was. hours 0 wakes it now. Only snoozedUntil, snoozedAt and snoozedBy are written; the answer and status stay.
app.MapPost("/api/snooze", async (HttpRequest req) =>
{
    var b = await Body(req);
    var id = (string?)b?["id"];
    if (!Store.IdOk(id)) return Fail("Unknown ticket.");
    var hours = b?["hours"] is JsonValue hv && hv.TryGetValue<int>(out var h) ? h : -1;
    if (hours is < 0 or > 24) return Fail("Snooze for 1 to 24 hours (0 wakes it).");
    lock (store.Lock)
    {
        var file = store.TicketFile(id!);
        var t = store.Read(file, out var why);
        if (t == null) return Fail($"Couldn't read the ticket ({why}).");
        if ((string?)t["status"] is not (null or "open" or "started" or "discuss")) return Fail("Only an open ticket can be snoozed.");
        if (hours == 0) { t.Remove("snoozedUntil"); t["wokenAt"] = Store.Now(); }
        else
        {
            t["snoozedUntil"] = DateTime.UtcNow.AddHours(hours).ToString("yyyy-MM-ddTHH:mm:ssZ");
            t["snoozedAt"] = Store.Now();
            t["snoozedBy"] = Who(req);
        }
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
            // A check-in that uploaded nothing: PDM released it unchanged, or it wasn't checked out. Say so plainly,
            // so it doesn't look as if changes were dropped (SW6, 7 Nov).
            var nothingNew = type == "check-in" && outcome == null
                && !said.Any(l => l.StartsWith("Checked in ", StringComparison.Ordinal) || l.StartsWith("Added ", StringComparison.Ordinal));
            results.Add(new JsonObject
            {
                ["path"] = k, ["ok"] = outcome == null, ["why"] = outcome ?? "", ["log"] = string.Join(" / ", said),
                ["nothingNew"] = nothingNew
            });
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
            if (t["history"] is not JsonArray history) { history = []; t["history"] = history; }
            history.Add(new JsonObject { ["choice"] = (string?)old["choice"], ["note"] = (string?)old["note"], ["answeredAt"] = (string?)t["answeredAt"] });
        }
        t["answer"] = new JsonObject { ["choice"] = choice, ["note"] = note };
        t["answeredAt"] = Store.Now();
        t["status"] = "answered";
        store.Write(file, t);
        notices.Tell(id!, t, Who(req));
    }
    await HandBack(); // Done or Problem ends Grant's turn with SOLIDWORKS
    return Results.Ok(new { ok = true });
});

// ---- Pause automatic work / Hand back (Desk v2): pause.json asks the sessions to stop at a safe point; SwCmd refuses
// their commands (exit 4) while it exists, but not Grant's.
app.MapPost("/api/sw/pause", (HttpRequest req) =>
{
    Directory.CreateDirectory(store.Sw);
    if (!File.Exists(pauseFile)) store.Write(pauseFile, new JsonObject { ["requestedAt"] = Store.Now(), ["by"] = Who(req) });
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
return 0;

// Each object carries its file name as _file, so the page answers the file it was shown.
List<JsonObject> Tagged(string dir, List<string> problems) => store.All(dir, problems);

static async Task<JsonNode?> Body(HttpRequest req)
{
    try { return await JsonNode.ParseAsync(req.Body); } catch { return null; }
}

static IResult Fail(string why) => Results.BadRequest(new { error = NoPaths(why) });

// A message without local folders: "C:\a\b\file.json" becomes "file.json" (error text can carry paths).
static string NoPaths(string s) => System.Text.RegularExpressions.Regex.Replace(s, @"[A-Za-z]:\\(?:(?:(?!\.\s)[^\\/:*?""<>|()'\r\n])*\\)*", "");

// Grant's answer on a draft-review ticket. accept / redo set "answered" (an earlier answer on the same version moves to
// history, as Change answer does); comment adds to the thread and sets "discuss". Draw sets "done", or posts a new version.
static bool DraftAnswer(JsonObject t, string action, string text, string user, out string why)
{
    why = "";
    if ((string?)t["type"] != "draft-review") { why = "Only draft-review tickets take this."; return false; }
    var status = (string?)t["status"] ?? "open";
    if (status is not ("open" or "discuss" or "answered" or "done")) { why = $"This ticket is {status}."; return false; }
    if (action == "comment")
    {
        if (t["thread"] is not JsonArray thread) { thread = []; t["thread"] = thread; }
        thread.Add(new JsonObject { ["from"] = user, ["text"] = text, ["at"] = Store.Now() });
        t["status"] = "discuss";
        return true;
    }
    if (t["answer"] is JsonObject old)
    {
        if (t["history"] is not JsonArray history) { history = []; t["history"] = history; }
        history.Add(new JsonObject { ["version"] = (int?)t["version"] ?? 1, ["choice"] = (string?)old["choice"], ["note"] = (string?)old["note"], ["answeredAt"] = (string?)t["answeredAt"] });
    }
    t["answer"] = new JsonObject { ["choice"] = action, ["note"] = text };
    t["answeredAt"] = Store.Now();
    t["status"] = "answered";
    return true;
}

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

static string ReadPage(string name)
{
    using var s = typeof(Store).Assembly.GetManifestResourceStream(name)!;
    return new StreamReader(s).ReadToEnd();
}
