using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Amazon;
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
    AmazonS3Client? s3;
    AmazonDynamoDBClient? db;

    public async Task<int> RunAsync(bool openBrowser)
    {
        var url = $"http://localhost:{s.PagePort}/";
        using var listener = new HttpListener();
        listener.Prefixes.Add(url);
        try { listener.Start(); }
        catch (HttpListenerException ex)
        {
            Console.WriteLine($"Can't start the page on {url}: {ex.Message}. Is pdm already running?");
            return 1;
        }
        Console.WriteLine($"PDM page: {url}  (close this window to stop)");
        if (openBrowser)
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }

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
        switch (path)
        {
            case "/api/me":
                await Send(ctx, 200, new { email = session.Email, name = session.Name, sharedEditor = session.SharedEditor });
                return;
            case "/api/files":
                await Send(ctx, 200, await FilesAsync(engine));
                return;
            case "/api/get":
                await engine.GetAsync(null);
                break;
            case "/api/checkout":
                await engine.CheckOutAsync(Strs("paths").Select(Local));
                break;
            case "/api/checkin":
                // Parts before assemblies before drawings, so a new part is in the index before what uses it.
                foreach (var p in Strs("paths").OrderBy(Rank))
                    await engine.CheckInAsync(Local(p), Str("note"));
                break;
            case "/api/undo":
                foreach (var p in Strs("paths")) await engine.UndoAsync(Local(p));
                break;
            default:
                await Send(ctx, 404, new { error = "not found" });
                return;
        }
        await Send(ctx, 200, new { log });
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
        await Send(ctx, 200, new { email = session.Email, name = session.Name, sharedEditor = session.SharedEditor });
    }

    // Every file with its state for me on this PC: held here (changed or not), held elsewhere, newer in the vault.
    // usedBy lists the files that use it, so the page can count the ones outside what's being checked out.
    async Task<object> FilesAsync(Engine engine)
    {
        var all = await new Index(db!, s).AllAsync();
        var state = LocalState.Load(s.LocalRoot);
        var usedBy = all.SelectMany(f => f.Refs.Distinct().Select(id => (id, by: f.FileId))).GroupBy(x => x.id).ToDictionary(g => g.Key, g => g.Select(x => x.by).ToList());
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
                changed = mine && state.IsChanged(f.Path, local),
                newer = !mine && (had == null || had.VersionId != f.VersionId || !File.Exists(local))
            };
        }).ToList();
    }

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
