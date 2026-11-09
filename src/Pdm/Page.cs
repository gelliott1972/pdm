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
            catch (Exception ex) { await Send(ctx, 500, new { error = ex.Message }); }
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
        var engine = new Engine(s, new Index(db!, s), new Vault(s3!, s), session.Email, session.Name, session.SharedEditor, log.Add);
        var run = Str("run");
        engine.Known = run != "" && run == runId ? runIndex : null;
        if ((path is "/api/get" or "/api/checkout" or "/api/checkin" or "/api/undo" or "/api/remove" or "/api/restore") && Vault.TooOld(Updater.Version, minVersion))
        {
            await Send(ctx, 409, new { error = TooOldText() });
            return;
        }
        var mutating = path is "/api/get" or "/api/checkout" or "/api/checkin" or "/api/undo" or "/api/remove" or "/api/restore";
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
                await Send(ctx, 200, await FilesAsync(engine));
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
        await Send(ctx, 200, new { log, timing = engine.Time.Snapshot() });
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
    async Task<object> FilesAsync(Engine engine)
    {
        var all = await new Index(db!, s).AllAsync();
        var state = LocalState.Load(s.LocalRoot);
        var usedBy = all.Where(f => !f.Removed).SelectMany(f => f.Refs.Distinct().Select(id => (id, by: f.FileId))).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.Select(x => x.by).ToList());
        var inVault = all.Select(f => f.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var added = Paths.LocalSolidWorksKeys(s.LocalRoot).Where(k => !inVault.Contains(k)).Select(k => (object)new
        {
            id = "new:" + k, path = k, refs = new List<string>(), missingRefs = 0,
            shared = Paths.IsShared(k), usedBy = new List<string>(), isNew = true
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
        email = session!.Email, name = session.Name, sharedEditor = session.SharedEditor, admin = Activity.IsAdmin(session.Email),
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
