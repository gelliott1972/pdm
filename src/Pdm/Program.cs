using Amazon;
using Amazon.DynamoDBv2;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using Amazon.S3;
using Pdm;

// No arguments: the page. "shell" is the test command line for the engine.
Velopack.VelopackApp.Build() // first: lets the installer and updater run their install/update steps
    .OnRestarted(v => Updater.UpdatedTo = v.ToString())
    .Run();
var settings = Settings.Load();
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "page";

try
{
    switch (command)
    {
        case "page":
            return await new Page(settings).RunAsync(openBrowser: !args.Contains("--no-open"));
        case "shell":
            return await Shell(settings);
        case "load":
            return await Load(settings, args.Contains("--dry"), Arg(args, "--note"));
        case "move" when args.Length >= 2:
            return await Admin(settings, async (index, vault, engine) =>
                await new Mover(settings, index, vault, AdminEmail, AdminName, Console.WriteLine).RunAsync(Path.GetFullPath(args[1]), Arg(args, "--note") ?? "", args.Contains("--dry")));
        case "remove" when args.Length >= 2:
            return await Admin(settings, async (index, vault, engine) =>
            {
                var note = Arg(args, "--note") ?? "";
                foreach (var f in Files(settings, args, r => r.Action == "retired")) await engine.RemoveAsync(f, note, args.Contains("--dry"));
                return 0;
            });
        case "checkout" when args.Length >= 2:
            return await Admin(settings, async (index, vault, engine) =>
            {
                await engine.CheckOutAsync(Files(settings, args, null), args.Contains("--dry"));
                return 0;
            });
        case "get":
            // Download latest with the admin login: for the admin PC, and for test copies standing in for an engineer's PC.
            return await Admin(settings, async (index, vault, engine) => { await engine.GetAsync(null); return 0; });
        case "unlock" when args.Length >= 2:
            return await Admin(settings, async (index, vault, engine) =>
            {
                await engine.ReleaseAsync(Files(settings, args, null), args.Contains("--dry"));
                return 0;
            });
        case "inspect" when args.Length == 2:
            var info = new DocReader().Read(Path.GetFullPath(args[1]));
            foreach (var r in info.References) Console.WriteLine($"ref     {r}");
            foreach (var p in info.PartNos) Console.WriteLine($"partno  {(p.Config == "" ? "(file)" : p.Config)} = {p.PartNo} {p.Status}");
            if (args[1].EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase))
            {
                var dm = new DocReader();
                var tb = dm.TitleBlockModel(Path.GetFullPath(args[1]));
                Console.WriteLine($"title block model  {tb?.ToString() ?? "(none)"}");
                var model = tb == null ? null : info.References.FirstOrDefault(r => Path.GetFileName(r).Equals(tb.Value.Model, StringComparison.OrdinalIgnoreCase));
                if (model != null)
                    foreach (var p in new[] { "Description", "PartNo" }) Console.WriteLine($"title block {p}  {dm.ModelProp(model, tb!.Value.Config, p) ?? "(none)"}");
            }
            return 0;
        case "map" when args.Length == 2:
            return Drive.Map(settings.LocalRoot, args[1], Console.WriteLine);
        case "release-file" when args.Length >= 2:
            // The release step's real run: files the PDFs a dry run passed (its work folder) into the vault.
            // Several work folders (a first dry run and its top-ups) file as one release: PDFs, set packages, register.
            return await Admin(settings, (index, vault, engine) =>
                new ReleaseFiler(settings, index, vault, AdminName, Console.WriteLine, AdminEmail).RunAsync(
                    args.Skip(1).TakeWhile(a => !a.StartsWith("--")).ToList(), args.Contains("--dry"), args.Contains("--incomplete")));
        case "activity-backfill":
            // One-off fill of the activity log from the vault's history (S3 versions, moves, removals); --since yyyy-mm-dd.
            return await Admin(settings, (index, vault, engine) => new ActivityBackfill(settings, index, vault, Console.WriteLine)
                .RunAsync(args.Contains("--dry"), Arg(args, "--since") is { } since ? DateTime.SpecifyKind(DateTime.Parse(since), DateTimeKind.Utc) : null));
        case "release" when args.Length >= 3:
            return await ReleaseDry(settings, args);
        default:
            Console.WriteLine("Usage: pdm              open the PDM page");
            Console.WriteLine("       pdm shell        log in and type commands");
            Console.WriteLine("       pdm load [--dry] [--note \"...\"]");
            Console.WriteLine("                        admin: load P:\\ into the vault (uses the pdm-admin SSO login); the note goes on new and changed files");
            Console.WriteLine("       pdm move <list.txt> --note \"...\" [--dry]");
            Console.WriteLine("                        admin: move vault files, one \"old path -> new path\" per line; all lines checked first");
            Console.WriteLine("       pdm remove <file> [<file> ...] --note \"why\" [--dry]   admin: remove from the vault (Restore brings back)");
            Console.WriteLine("       pdm get          admin: Download latest for this PC's root, with the admin login");
            Console.WriteLine("       pdm checkout <file> [<file> ...] [--dry]   admin: check out here, as the admin login");
            Console.WriteLine("       pdm unlock <file> [<file> ...] [--dry]     admin: release those check-outs, files left as they are");
            Console.WriteLine("                        move, remove, checkout and unlock also take a round list (round.csv: action,old_path,new_path,...)");
            Console.WriteLine("       pdm map <folder> admin: map P: to <folder> permanently (run as administrator)");
            Console.WriteLine("       pdm release <folder> [<folder> ...] <code> --dry [--with-shared] [--shared-to scene|own] [--no-export] [--as role] [--only a,b|list.txt]");
            Console.WriteLine("                        [--accept-checkout \"NAME=reason\"] [--accept-rev \"NAME=reason\"]   Grant's exceptions, recorded in the set");
            Console.WriteLine("                        admin: dry run of a release set on scratch copies (pdm-admin SSO, reads only); several folders make one set");
            Console.WriteLine("       pdm activity-backfill [--dry] [--since yyyy-mm-dd]   admin: fill the activity log from the vault's history");
            Console.WriteLine("       pdm release-file <dry run work folder> [<work folder> ...] [--dry] [--incomplete]");
            Console.WriteLine("                        admin: files the checked PDFs into the scene's _Review folder, each set's ZIP and drawing list into");
            Console.WriteLine("                        _Review\\_Sets, and rewrites the scene's drawing register; nothing unless every set is complete");
            return 2;
    }
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    return 1;
}

static async Task<int> Shell(Settings s)
{
    Console.Write("Email: ");
    var email = Console.ReadLine()?.Trim() ?? "";
    var password = ReadHidden("Password: ");

    Session session;
    try
    {
        session = await Login.RunAsync(s, email, password, () =>
        {
            Console.WriteLine("First login: choose a new password (12+ characters, with a number).");
            while (true)
            {
                var first = ReadHidden("New password: ");
                if (first == ReadHidden("Again: ")) return first;
                Console.WriteLine("They don't match, try again.");
            }
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Login failed: {ex.Message}");
        return 1;
    }
    Console.WriteLine($"Logged in as {session.Name}.{(session.SharedEditor ? " You may edit shared folders." : "")}");

    var region = RegionEndpoint.GetBySystemName(s.Region);
    using var s3 = new AmazonS3Client(session.Credentials, region);
    using var db = new AmazonDynamoDBClient(session.Credentials, region);
    var engine = new Engine(s, new Pdm.Index(db, s), new Vault(s3, s), session.Email, session.Name, session.SharedEditor, Console.WriteLine);

    Console.WriteLine("Commands: get [folder], status [folder], checkout <file> [<file> ...], checkin <file> [-m \"note\"], undo <file>, quit");
    Console.WriteLine($"Files can be given as full paths or relative to {s.LocalRoot}");
    while (true)
    {
        Console.Write("pdm> ");
        var line = Console.ReadLine();
        if (line == null) return 0;
        var words = Split(line);
        if (words.Count == 0) continue;
        var cmd = words[0].ToLowerInvariant();
        var rest = words.Skip(1).ToList();
        string Full(string p) => Path.IsPathRooted(p) ? p : Path.Combine(s.LocalRoot, p);
        try
        {
            await session.RefreshAsync();
            engine.Known = null; // each command reads the index afresh
            switch (cmd)
            {
                case "quit" or "exit":
                    return 0;
                case "get":
                    await engine.GetAsync(rest.Count > 0 ? Full(rest[0]) : null);
                    break;
                case "status":
                    await engine.StatusAsync(rest.Count > 0 ? Full(rest[0]) : null);
                    break;
                case "checkout" when rest.Count > 0:
                    await engine.CheckOutAsync(rest.Select(Full));
                    break;
                case "checkin" when rest.Count > 0:
                    var m = rest.IndexOf("-m");
                    var note = m >= 0 && m + 1 < rest.Count ? rest[m + 1] : "";
                    await engine.CheckInAsync(Full(rest[0]), note);
                    break;
                case "undo" when rest.Count > 0:
                    await engine.UndoAsync(Full(rest[0]));
                    break;
                default:
                    Console.WriteLine("Commands: get [folder], status [folder], checkout <file> [<file> ...], checkin <file> [-m \"note\"], undo <file>, quit");
                    break;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
        }
    }
}

// Files named on the command line (vault keys or P:\ paths), or a round list's rows: those matching `pick`,
// or every file the round holds (changed, moved, moved+changed, retired) when pick is null.
static List<string> Files(Settings s, string[] args, Func<Round.Row, bool>? pick)
{
    var named = args.Skip(1).TakeWhile(a => !a.StartsWith("--")).ToList();
    if (named.Count == 1 && Round.IsRound(Path.GetFullPath(named[0])))
    {
        var file = Path.GetFullPath(named[0]);
        named = pick == null ? Round.Held(file) : Round.Read(file).Where(pick).Select(r => r.Old).ToList();
    }
    return named.Select(f => Path.IsPathRooted(f) ? f : Path.Combine(s.LocalRoot, f.Replace('/', '\\'))).ToList();
}

static string? Arg(string[] args, string name)
{
    var i = Array.IndexOf(args, name);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : null;
}

// Admin jobs with Grant's SSO login (profile pdm-admin). Run "aws sso login --profile pdm-admin" first.
static async Task<int> Admin(Settings s, Func<Pdm.Index, Vault, Engine, Task<int>> job)
{
    if (!new CredentialProfileStoreChain().TryGetAWSCredentials("pdm-admin", out AWSCredentials credentials))
    {
        Console.WriteLine("No pdm-admin profile on this PC.");
        return 1;
    }
    var region = RegionEndpoint.GetBySystemName(s.Region);
    using var s3 = new AmazonS3Client(credentials, region);
    using var db = new AmazonDynamoDBClient(credentials, region);
    var index = new Pdm.Index(db, s);
    var vault = new Vault(s3, s);
    Console.WriteLine($"Vault: {s.Bucket}, {s.FilesTable}; local root {s.LocalRoot}");
    return await job(index, vault, new Engine(s, index, vault, AdminEmail, AdminName, true, Console.WriteLine));
}

// Admin load with Grant's SSO login (profile pdm-admin). Run "aws sso login --profile pdm-admin" first.
// Files this admin login has checked out on this PC (a round's) are checked in by the load.
static Task<int> Load(Settings s, bool dryRun, string? note) =>
    Admin(s, async (index, vault, engine) =>
    {
        await new Loader(s, index, vault, engine, AdminName, Console.WriteLine).RunAsync(dryRun, note);
        return 0;
    });

// Dry run of the release step: reads the index with the pdm-admin SSO login, copies the set to a scratch folder,
// exports and checks there, and writes a pass/fail list. Nothing is written to the vault.
static async Task<int> ReleaseDry(Settings s, string[] args)
{
    if (!args.Contains("--dry")) { Console.WriteLine("The release is a dry run first (--dry); then pdm release-file <its work folder> files the PDFs it passed."); return 2; }
    // pdm release <folder> [<folder> ...] <code>: several folders make one set (a shared drawing used by both is in once).
    var positional = args.Skip(1).TakeWhile(a => !a.StartsWith("--")).ToList();
    if (positional.Count < 2) { Console.WriteLine("Give the folder(s), then the code: pdm release <folder> [<folder> ...] <code> --dry"); return 2; }
    var code = positional[^1].Trim();
    if (!Release.CodeOk(code)) { Console.WriteLine($"\"{code}\" isn't a revision code (0A, 0B, 1, 1A, 2 ...)."); return 2; }
    var folderKeys = positional[..^1].Select(f => Paths.KeyOf(s.LocalRoot, Path.GetFullPath(f)).TrimEnd('/')).ToList();
    var folderKey = string.Join(" + ", folderKeys);
    if (!new CredentialProfileStoreChain().TryGetAWSCredentials("pdm-admin", out AWSCredentials credentials))
    { Console.WriteLine("No pdm-admin profile on this PC."); return 1; }
    using var db = new AmazonDynamoDBClient(credentials, RegionEndpoint.GetBySystemName(s.Region));
    var all = (await new Pdm.Index(db, s).AllAsync()).Where(r => !r.Removed).ToList(); // removed files are never released
    var release = new Release(s, all, Console.WriteLine);
    var asAt = Array.IndexOf(args, "--as");
    if (asAt >= 0 && asAt + 1 < args.Length) release.SwAs = args[asAt + 1];
    var i = Array.IndexOf(args, "--shared-to");
    if (i > 0 && i + 1 < args.Length) release.SharedTo = args[i + 1] == "own" ? "own" : "scene";
    if (Environment.GetEnvironmentVariable("PDM_SWCMD") is { Length: > 0 } sw) release.SwCmd = sw;
    // Grant's exceptions: --accept-checkout "NAME=reason" and --accept-rev "NAME=reason", each as often as needed.
    for (var k = 1; k + 1 < args.Length; k++)
    {
        if (args[k] is not ("--accept-checkout" or "--accept-rev")) continue;
        var eq = args[k + 1].IndexOf('=');
        if (eq <= 0 || eq == args[k + 1].Length - 1) { Console.WriteLine($"{args[k]} takes \"NAME=reason\""); return 2; }
        (args[k] == "--accept-rev" ? release.AcceptRev : release.AcceptCheckout)[Path.GetFileNameWithoutExtension(args[k + 1][..eq].Trim())] = args[k + 1][(eq + 1)..].Trim();
    }

    var items = folderKeys.SelectMany(f => release.Plan(f, code, args.Contains("--with-shared")))
        .GroupBy(x => x.Drawing.FileId).Select(g => g.First()).OrderBy(x => x.Drawing.Path, StringComparer.OrdinalIgnoreCase).ToList();
    if (items.Count == 0) { Console.WriteLine($"No drawings in the vault under {folderKey}."); return 1; }
    // --only a,b,c (or a list file, one per line): a top-up run of just those drawings of the set, by file name.
    var only = Arg(args, "--only");
    if (only != null)
    {
        static string Name(string p) => Path.GetFileNameWithoutExtension(p.Trim().Replace('/', '\\'));
        var names = (File.Exists(only) ? File.ReadAllLines(only) : only.Split(',')).Select(Name).Where(n => n != "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missing = names.Where(n => !items.Any(x => Name(x.Drawing.Path).Equals(n, StringComparison.OrdinalIgnoreCase))).ToList();
        if (missing.Count > 0) { Console.WriteLine($"Not drawings of this set: {string.Join(", ", missing)}"); return 2; }
        Console.WriteLine($"Top-up: {names.Count} of the set's {items.Count} drawings.");
        items = items.Where(x => names.Contains(Name(x.Drawing.Path))).ToList();
    }
    var work = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PDM-release",
        $"{DateTime.Now:yyyyMMdd-HHmmss}_{code}_{string.Join("+", folderKeys.Select(f => f.Split('/')[^1]))}{(only != null ? "_top-up" : "")}");
    Console.WriteLine($"{items.Count} drawings; {items.Count(x => x.Pass)} pass the checks before export. Work folder: {work}");
    if (!args.Contains("--no-export")) await release.ExportAndCheckAsync(items, code, work, Path.Combine(work, "copy"));
    Console.WriteLine();
    Console.WriteLine(release.Report(items, folderKey, code, dry: true, work));
    return items.All(x => x.Pass) ? 0 : 1;
}

static List<string> Split(string line)
{
    var words = new List<string>();
    var current = new System.Text.StringBuilder();
    bool quoted = false, any = false;
    foreach (var c in line)
    {
        if (c == '"') { quoted = !quoted; any = true; }
        else if (char.IsWhiteSpace(c) && !quoted) { if (any) words.Add(current.ToString()); current.Clear(); any = false; }
        else { current.Append(c); any = true; }
    }
    if (any) words.Add(current.ToString());
    return words;
}

static string ReadHidden(string prompt)
{
    Console.Write(prompt);
    var chars = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) break;
        if (key.Key == ConsoleKey.Backspace) { if (chars.Count > 0) chars.RemoveAt(chars.Count - 1); }
        else if (!char.IsControl(key.KeyChar)) chars.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string(chars.ToArray());
}

// Who admin jobs run as: Grant's SSO login. Locks they take for a moment (move, remove) show this name.
static partial class Program
{
    const string AdminEmail = "pdm-admin";
    const string AdminName = "Grant Elliott";
}
