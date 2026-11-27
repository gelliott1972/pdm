using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon;
using Amazon.CognitoIdentityProvider.Model;
using Amazon.DynamoDBv2;
using Amazon.S3;

namespace Pdm;

// The local web page: http://localhost:<port>/. One request at a time, so the engine and state.json never race.
// Only answers requests to localhost that carry the X-PDM header (a plain web page elsewhere can't send it).
public sealed class Page(Settings s)
{
    sealed class NewPasswordNeeded : Exception;

    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    Session? session;
    readonly Updater updater = new();
    AmazonS3Client? s3;
    AmazonDynamoDBClient? db;
    // The index as read by the current run of requests (one check-in or check-out from the page), so it's read once.
    string runId = "";
    string? minVersion; // read at login, on Reload list and about hourly
    DateTime lastCheck;
    // A get, check-out, check-in, undo, remove or restore running, or just finished (the page sends several in a row):
    // the tray's Update and Exit ask first.
    int working; DateTime lastWork;
    List<FileRecord>? runIndex;

    public async Task<int> RunAsync(bool openBrowser)
    {
        var url = $"http://localhost:{s.PagePort}/";
        // One copy per PC: starting it again just opens the page of the one running.
        using var single = new Mutex(true, @"Local\PDM-page", out var first);
        if (!first) { Console.WriteLine("PDM is already running; opening its page."); Tray.Open(url); return 0; }
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        try { listener.Start(); }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"Can't start the page on {url}: {ex.Message}. Is pdm already running?");
            System.Windows.Forms.MessageBox.Show($"PDM can't start its page on {url}:\n{ex.Message}\n\nDetails are in {Tray.LogFile}", "PDM");
            return 1;
        }
        Console.WriteLine($"PDM {Updater.Version} page: {url}  (tray icon: Open PDM page, Exit)");
        Tray.Start(url, () => updater.ReadyVersion, () => working > 0 || DateTime.UtcNow - lastWork < TimeSpan.FromSeconds(15), updater.ApplyAndRestart);
        if (openBrowser) Tray.Open(url);

        while (true)
        {
            var ctx = await listener.GetContextAsync();
            try { await HandleAsync(ctx); }
            catch (Exception ex)
            {
                Console.WriteLine($"Error on {ctx.Request.Url?.AbsolutePath}: {ex}");
                await Send(ctx, 500, new { error = ex.Message });
            }
        }
    }

    async Task HandleAsync(HttpListenerContext ctx)
    {
        var req = ctx.Request;
        var path = req.Url!.AbsolutePath;
        if (req.Url.Host is not ("localhost" or "127.0.0.1")) { await Send(ctx, 403, new { error = "localhost only" }); return; }

        if (req.HttpMethod == "GET" && path == "/")
        {
            var html = await File.ReadAllBytesAsync(Path.Combine(AppContext.BaseDirectory, "page", "index.html"));
            ctx.Response.ContentType = "text/html; charset=utf-8";
            await ctx.Response.OutputStream.WriteAsync(html);
            ctx.Response.Close();
            return;
        }
        if (!path.StartsWith("/api/") || req.Headers["X-PDM"] != "1") { await Send(ctx, 404, new { error = "not found" }); return; }

        var body = req.HasEntityBody ? JsonDocument.Parse(await new StreamReader(req.InputStream).ReadToEndAsync()).RootElement : default;
        string Str(string k) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()! : "";
        List<string> Strs(string k) => body.ValueKind == JsonValueKind.Object && body.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Array
            ? v.EnumerateArray().Select(x => x.GetString() ?? "").Where(x => x != "").ToList() : new();

        if (path == "/api/login")
        {
            await LoginAsync(ctx, Str("email"), Str("password"), Str("newPassword"));
            return;
        }
        if (path == "/api/logout")
        {
            Close();
            await Send(ctx, 200, new { });
            return;
        }
        if (session == null) { await Send(ctx, 401, new { error = "Not logged in." }); return; }
        await session.RefreshAsync();

        var log = new List<string>();
        // The admin's page also counts a check-out made here by the admin CLI (pdm checkout, as pdm-admin) as his own
        // (Grant, 21 Nov): Get keeps his copy, and the page can check it in or undo it. Nobody else's login changes.
        var engine = new Engine(s, new Index(db!, s), new Vault(s3!, s), session.Email, session.Name, session.SharedEditor, log.Add,
            alsoMine: Activity.IsAdmin(session.Email) ? Program.AdminCliLogin : null);
        var run = Str("run");
        engine.Known = run != "" && run == runId ? runIndex : null;
        if ((path is "/api/get" or "/api/checkout" or "/api/checkin" or "/api/undo" or "/api/remove" or "/api/restore") && Vault.TooOld(Updater.Version, minVersion))
        {
            await Send(ctx, 409, new { error = TooOldText() });
            return;
        }
        var mutating = path is "/api/get" or "/api/checkout" or "/api/checkin" or "/api/undo" or "/api/remove" or "/api/restore";
        // One PDM command at a time on this PC, the command line included (PcLock.cs, 27 Nov).
        string? busyWhy = null;
        using var pcLock = mutating ? PcLock.Take(s.LocalRoot, "the PDM page (" + path[5..] + ")", TimeSpan.FromSeconds(30), out busyWhy) : null;
        if (mutating && pcLock == null) { await Send(ctx, 409, new { error = busyWhy }); return; }
        if (mutating) Interlocked.Increment(ref working);
        try { await HandleLoggedInAsync(ctx, path, body, Str, Strs, engine, run, log); }
        finally { if (mutating) { lastWork = DateTime.UtcNow; Interlocked.Decrement(ref working); } }
    }

    async Task HandleLoggedInAsync(HttpListenerContext ctx, string path, JsonElement body, Func<string, string> Str, Func<string, List<string>> Strs,
        Engine engine, string run, List<string> log)
    {
        switch (path)
        {
            case "/api/me":
                // The page asks every 30 s while it's in view; about once an hour that also looks for an update.
                if (DateTime.UtcNow - lastCheck > TimeSpan.FromHours(1)) await CheckAsync();
                await Send(ctx, 200, Me());
                return;
            case "/api/check":
                // Reload list: look for an update and re-read the vault's minimum version now.
                await CheckAsync();
                await Send(ctx, 200, Me());
                return;
            case "/api/password":
                try { await session!.User.ChangePasswordAsync(Str("old"), Str("new")); }
                catch (Exception ex) { await Send(ctx, 400, new { error = PasswordError(ex) }); return; }
                await Send(ctx, 200, new { });
                return;
            case "/api/restart":
                await Send(ctx, 200, new { });
                if (updater.ReadyVersion != null) Tray.Hide();
                updater.ApplyAndRestart(); // exits this process when an update is ready
                return;
            case "/api/files":
                await Send(ctx, 200, await FilesAsync(engine, log));
                return;
            case "/api/activity":
                if (!Activity.IsAdmin(session!.Email)) { await Send(ctx, 403, new { error = "Activity is for the admin only." }); return; }
                await Send(ctx, 200, await Activity.QueryAsync(new Vault(s3!, s), Path.Combine(s.LocalRoot, ".pdm", "activity-cache.json"), Str("before"), body.ValueKind == JsonValueKind.Object && body.TryGetProperty("days", out var dv) && dv.TryGetInt32(out var dn) ? dn : 14));
                return;
            case "/api/coverage":
                if (!Activity.IsAdmin(session!.Email)) { await Send(ctx, 403, new { error = "Activity is for the admin only." }); return; }
                await Send(ctx, 200, await Coverage.ComputeAsync(s, await new Index(db!, s).AllAsync(), new Vault(s3!, s),
                    Path.Combine(s.LocalRoot, ".pdm", "activity-cache.json"), Path.Combine(s.LocalRoot, ".pdm", "titleblock-cache.json")));
                return;
            case "/api/needs-drawing":
                // Needs drawing (Grant, 21 Nov): for everyone. Reads the index and the rules only.
                await Send(ctx, 200, NeedsDrawing.Compute(await new Index(db!, s).AllAsync(), s.LocalRoot, await new Vault(s3!, s).GetTextOrNullAsync(NeedsDrawing.RulesKey)));
                return;
            case "/api/needs-drawing-resaved":
            {
                // The rows of "drawing older than its model" where the model was only re-saved (Resaved.cs). Asked after
                // the lists, as it can take a minute the first time on a PC (it reads both versions of each model).
                var all = await new Index(db!, s).AllAsync();
                var vault = new Vault(s3!, s);
                var r = NeedsDrawing.Compute(all, s.LocalRoot, await vault.GetTextOrNullAsync(NeedsDrawing.RulesKey));
                var marked = await Resaved.MarkAsync(r.Older, all, vault, s.LocalRoot, Console.WriteLine);
                await Send(ctx, 200, new { resaved = marked.Where(o => o.ResavedOnly).Select(o => $"{o.DrawingId}|{o.ModelId}|{o.ModelVersion}").ToList() });
                return;
            }
            case "/api/needs-drawing-seen":
            {
                var why = await NeedsDrawing.MarkAsync(new Index(db!, s), Str("drawing"), Str("model"), Str("version"), session!.Name,
                    body.ValueKind == JsonValueKind.Object && body.TryGetProperty("undo", out var u) && u.ValueKind == JsonValueKind.True);
                await Send(ctx, why == null ? 200 : 409, why == null ? new { } : new { error = why });
                return;
            }
            case "/api/needs-drawing-rules":
                if (!Activity.IsAdmin(session!.Email)) { await Send(ctx, 403, new { error = "Only the admin changes the rules." }); return; }
                await new Vault(s3!, s).PutTextAsync(NeedsDrawing.RulesKey, Str("text").Replace("\r\n", "\n"), "text/plain; charset=utf-8");
                Console.WriteLine($"Needs drawing rules changed by {session.Name}.");
                await Send(ctx, 200, new { });
                return;
            case "/api/outputs":
            {
                // Outputs tab (brief docs\briefs\outputs.md, stage 2): for everyone, read only. Removed sets aren't shown.
                if (s.OutputsBucket == "") { await Send(ctx, 200, new { setUp = false, sets = Array.Empty<object>() }); return; }
                var sets = (await new Outputs(s3!, s.OutputsBucket).ListAsync()).Where(x => x.RemovedAt == null).ToList();
                await Send(ctx, 200, new
                {
                    setUp = true, root = OutputsRoot(),
                    sets = sets.Select(x => new
                    {
                        x.Screen, x.Pin, x.Kind, x.Note, x.AddedBy, x.AddedAt, folder = OutputFolder(x),
                        files = x.Files.Select(f => new { f.Name, f.Size, here = HaveOutput(x, f) })
                    })
                });
                return;
            }
            case "/api/outputs-download":
            case "/api/outputs-open":
            {
                // One file (file given) or the whole set, into Documents\PDM Outputs\<screen>\<pin>\<kind>\ (never P:\).
                if (s.OutputsBucket == "") { await Send(ctx, 409, new { error = "Outputs aren't set up." }); return; }
                var outputs = new Outputs(s3!, s.OutputsBucket);
                string screen = Str("screen"), pin = Str("pin"), kind = Str("kind");
                if (Outputs.CheckScreen(ref screen) != null || Outputs.CheckPin(ref pin) != null || Outputs.CheckKind(ref kind) != null)
                { await Send(ctx, 400, new { error = "Not a set." }); return; }
                var set = (await outputs.ListAsync(screen)).FirstOrDefault(x => x.Pin == pin && x.Kind == kind && x.RemovedAt == null);
                if (set == null) { await Send(ctx, 404, new { error = $"No set {screen} {pin} {kind} (removed meanwhile?)." }); return; }
                var folder = OutputFolder(set);
                if (path == "/api/outputs-open")
                {
                    if (!Directory.Exists(folder)) { await Send(ctx, 409, new { error = "Nothing downloaded from this set yet." }); return; }
                    try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true }); } catch (Exception) { }
                    await Send(ctx, 200, new { folder });
                    return;
                }
                var file = Str("file");
                var want = set.Files.Where(f => file == "" || f.Name == file).ToList();
                if (want.Count == 0) { await Send(ctx, 404, new { error = $"{file} isn't in that set." }); return; }
                int got = 0, had = 0;
                foreach (var f in want)
                {
                    if (HaveOutput(set, f)) { had++; continue; }
                    var local = Path.Combine(folder, f.Name);
                    await outputs.DownloadAsync(set, f, local);
                    File.SetAttributes(local, File.GetAttributes(local) | FileAttributes.ReadOnly); // a copy of the output: not for editing
                    got++;
                }
                await Send(ctx, 200, new { folder, got, had });
                return;
            }
            case "/api/pdf-check":
            case "/api/pdf-start":
            case "/api/pdf-status":
                if (!Activity.IsAdmin(session!.Email)) { await Send(ctx, 403, new { error = "Make PDFs is for the admin only." }); return; }
                await PdfAsync(ctx, path, Strs("paths"), body.ValueKind == JsonValueKind.Object && body.TryGetProperty("useLocal", out var ul) && ul.ValueKind == JsonValueKind.True, engine);
                return;
            case "/api/get":
                var only = Strs("paths");
                await engine.GetAsync(null, only.Count > 0 ? only : null);
                break;
            case "/api/checkout":
                await engine.CheckOutAsync(Strs("paths").Select(Local));
                break;
            case "/api/checkin":
                // Parts before assemblies before drawings, so a new part is in the index before what uses it.
                await engine.CheckInManyAsync(Strs("paths").OrderBy(Rank).Select(Local), Str("note"));
                break;
            case "/api/undo":
                foreach (var p in Strs("paths")) await engine.UndoAsync(Local(p));
                break;
            case "/api/remove":
                await engine.RemoveAsync(Local(Str("path")), Str("reason"));
                break;
            case "/api/restore":
                await engine.RestoreAsync(Local(Str("path")));
                break;
            default:
                await Send(ctx, 404, new { error = "not found" });
                return;
        }
        if (run != "") (runId, runIndex) = (run, engine.Known);
        // What went wrong goes in pdm-page.log too (Admin 20 Nov: three errors on a sync left nothing in the log).
        foreach (var l in log.Where(l => l.StartsWith("Couldn't") || l.StartsWith("Skipped") || l.StartsWith("Not ")))
            Console.WriteLine($"{path[5..]}: {l}");
        await Send(ctx, 200, new { log, timing = engine.Time.Snapshot() });
    }

    // Make PDFs only (Grant, 20 Nov). check: the drawings named and which of their files aren't the vault's latest here.
    // start: takes SOLIDWORKS and copies the files out of P:\ within this request, then exports in the background.
    // status: the background run's progress, and its result when it's done.
    sealed class PdfJob { public int N, Total; public string Name = ""; public volatile bool Running = true; public List<string> Log = new(); public string? Folder; }
    PdfJob? pdfJob;

    async Task PdfAsync(HttpListenerContext ctx, string path, List<string> paths, bool useLocal, Engine engine)
    {
        if (path == "/api/pdf-status")
        {
            var j = pdfJob;
            await Send(ctx, 200, j == null ? new { running = false } : new { running = j.Running, n = j.N, total = j.Total, name = j.Name, log = j.Running ? null : j.Log, folder = j.Folder });
            return;
        }
        if (pdfJob is { Running: true }) { await Send(ctx, 409, new { error = "PDFs are already being made; wait for that run to finish." }); return; }
        var log = new List<string>();
        var run = new PdfRun(s, await new Index(db!, s).AllAsync(), log.Add);
        var items = run.Plan(paths);
        if (path == "/api/pdf-check")
        {
            await Send(ctx, 200, new { drawings = items.Count, notLatest = PdfRun.NotLatest(items, s, engine.Mine), log });
            return;
        }
        var job = await run.StartAsync(items, useLocal);
        if (job == null) { await Send(ctx, 200, new { started = false, log }); return; }
        var state = pdfJob = new PdfJob { Total = items.Count(i => i.Skip == null), Log = log, Folder = job.RunFolder };
        Interlocked.Increment(ref working); // the tray's Update and Exit ask first while it runs
        _ = Task.Run(async () =>
        {
            try { await run.FinishAsync(job, (n, total, name) => { state.N = n; state.Total = total; state.Name = name; }); }
            catch (Exception ex) { Console.WriteLine($"Make PDFs: {ex}"); log.Add("Error: " + ex.Message); }
            finally
            {
                foreach (var l in log.Where(l => l.StartsWith("Skipped") || l.StartsWith("Error"))) Console.WriteLine($"Make PDFs: {l}");
                try { Process.Start(new ProcessStartInfo("explorer.exe", $"\"{job.RunFolder}\"") { UseShellExecute = true }); } catch (Exception) { }
                state.Running = false; lastWork = DateTime.UtcNow;
                Interlocked.Decrement(ref working);
            }
        });
        await Send(ctx, 200, new { started = true, total = state.Total, folder = job.RunFolder });
    }

    async Task LoginAsync(HttpListenerContext ctx, string email, string password, string newPassword)
    {
        Close();
        try
        {
            session = await Login.RunAsync(s, email, password, () => newPassword != "" ? newPassword : throw new NewPasswordNeeded());
        }
        catch (NewPasswordNeeded)
        {
            await Send(ctx, 200, new { newPasswordNeeded = true });
            return;
        }
        catch (Exception ex)
        {
            await Send(ctx, 401, new { error = ex.Message });
            return;
        }
        var region = RegionEndpoint.GetBySystemName(s.Region);
        s3 = new AmazonS3Client(session.Credentials, region);
        db = new AmazonDynamoDBClient(session.Credentials, region);
        updater.Start(new AmazonS3Client(session.Credentials, region), s, Console.WriteLine); // its own client: logout disposes s3
        minVersion = await new Vault(s3, s).MinVersionAsync();
        lastCheck = DateTime.UtcNow;
        await Send(ctx, 200, Me());
        Updater.UpdatedTo = null; // the page has shown it
    }

    // Every file with its state for me on this PC: held here (changed or not), held elsewhere, newer in the vault,
    // and SOLIDWORKS files on this PC that aren't in the vault yet (isNew), which check-in adds.
    // usedBy lists the files that use it, so the page can count the ones outside what's being checked out.
    // Where downloaded outputs go on this PC: Documents\PDM Outputs\<screen>\<pin, no colons>\<kind>\.
    static string OutputsRoot() => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "PDM Outputs");
    static string OutputFolder(Outputs.OutputSet x) => Path.Combine(OutputsRoot(), x.Screen, Outputs.PinFolder(x.Pin), x.Kind);
    static bool HaveOutput(Outputs.OutputSet x, Outputs.OutputFile f)
    {
        var local = Path.Combine(OutputFolder(x), f.Name);
        try { return File.Exists(local) && new FileInfo(local).Length == f.Size && Vault.Md5(local).Equals(f.Md5, StringComparison.OrdinalIgnoreCase); }
        catch (IOException) { return false; }
    }

    async Task<object> FilesAsync(Engine engine, List<string> log)
    {
        var all = await new Index(db!, s).AllAsync();
        // Old copies of moved files are tidied first (as Download latest does); what can't be stays as an old copy
        // (movedTo), which the page lists but never ticks, and check-in refuses (27 Nov, James's re-add).
        var here = engine.LocalNew(all);
        foreach (var l in log) Console.WriteLine($"Check-in list: {l}");
        var state = LocalState.Load(s.LocalRoot);
        var usedBy = all.Where(f => !f.Removed).SelectMany(f => f.Refs.Distinct().Select(id => (id, by: f.FileId))).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.Select(x => x.by).ToList());
        var added = here.Select(x => (object)new
        {
            id = "new:" + x.Key, path = x.Key, refs = new List<string>(), missingRefs = 0,
            shared = Paths.IsShared(x.Key), usedBy = new List<string>(), isNew = true,
            movedTo = x.Gone == null ? null : MovedAway.Where(x.Gone), movedWho = x.Gone == null ? null : MovedAway.Who(x.Gone)
        });
        return all.OrderBy(f => f.Path, StringComparer.OrdinalIgnoreCase).Select(f =>
        {
            var local = Paths.LocalOf(s.LocalRoot, f.Path);
            bool mine = engine.Mine(f);
            state.Files.TryGetValue(f.Path, out var had);
            return new
            {
                id = f.FileId, path = f.Path, refs = f.Refs, missingRefs = f.MissingRefs.Count,
                shared = Paths.IsShared(f.Path), usedBy = usedBy.GetValueOrDefault(f.FileId) ?? new List<string>(),
                updatedBy = f.UpdatedBy, updatedAt = f.UpdatedAt, note = f.Note,
                lockedByName = f.LockedByName, lockedAt = f.LockedAt, lockMachine = f.LockMachine, mine,
                changed = mine && state.IsChanged(f.Path, local), missing = !File.Exists(local),
                newer = !f.Removed && !mine && (had == null || had.VersionId != f.VersionId || !File.Exists(local)),
                movedFrom = f.Moves.LastOrDefault()?.From,
                removed = f.Removed, removedByName = f.RemovedByName, removedAt = f.RemovedAt, removedReason = f.RemovedReason
            };
        }).Concat(added).ToList();
    }

    // Cognito's reasons, in the page's words.
    static string PasswordError(Exception ex) => ex switch
    {
        NotAuthorizedException => "The current password is wrong.",
        InvalidPasswordException => "The new password isn't allowed: it needs 8 or more characters, with a lowercase letter and a number.",
        LimitExceededException or TooManyRequestsException => "Too many tries. Wait a few minutes and try again.",
        _ => ex.Message
    };

    object Me() => new
    {
        email = session!.Email, name = session.Name, sharedEditor = session.SharedEditor, admin = Activity.IsAdmin(session.Email), tickNewFiles = s.TickNewFiles,
        version = Updater.Version, updateReady = updater.ReadyVersion, updatedTo = Updater.UpdatedTo,
        tooOld = Vault.TooOld(Updater.Version, minVersion) ? TooOldText() : null
    };

    // Starts an update check in the background (skipped while one runs or an update is already downloaded; nothing is
    // applied until Update is pressed) and re-reads the vault's minimum version, so "too old" shows without a restart.
    async Task CheckAsync()
    {
        lastCheck = DateTime.UtcNow;
        updater.Start(new AmazonS3Client(session!.Credentials, RegionEndpoint.GetBySystemName(s.Region)), s, Console.WriteLine);
        try { minVersion = await new Vault(s3!, s).MinVersionAsync(); }
        catch (Exception ex) when (ex is Amazon.Runtime.AmazonServiceException or HttpRequestException or IOException)
        { Console.WriteLine($"Minimum version not read: {ex.Message}"); }
    }

    string TooOldText() => $"This PDM is version {Updater.Version}; the vault needs {minVersion} or later. "
        + (updater.ReadyVersion != null ? $"Press Update to {updater.ReadyVersion} (top right) first." : "Press Reload list to fetch the update; when Update appears (top right), press it.");

    string Local(string key) => Paths.LocalOf(s.LocalRoot, key);

    static int Rank(string key) => Path.GetExtension(key).ToUpperInvariant() switch { ".SLDPRT" => 0, ".SLDASM" => 1, _ => 2 };

    void Close()
    {
        session = null;
        s3?.Dispose(); s3 = null;
        db?.Dispose(); db = null;
    }

    static async Task Send(HttpListenerContext ctx, int status, object value)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json));
        ctx.Response.StatusCode = status;
        ctx.Response.ContentType = "application/json; charset=utf-8";
        await ctx.Response.OutputStream.WriteAsync(bytes);
        ctx.Response.Close();
    }
}
