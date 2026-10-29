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
            return await Load(settings, args.Contains("--dry"));
        case "inspect" when args.Length == 2:
            var info = new DocReader().Read(Path.GetFullPath(args[1]));
            foreach (var r in info.References) Console.WriteLine($"ref     {r}");
            foreach (var p in info.PartNos) Console.WriteLine($"partno  {(p.Config == "" ? "(file)" : p.Config)} = {p.PartNo} {p.Status}");
            return 0;
        case "map" when args.Length == 2:
            return Drive.Map(settings.LocalRoot, args[1], Console.WriteLine);
        default:
            Console.WriteLine("Usage: pdm              open the PDM page");
            Console.WriteLine("       pdm shell        log in and type commands");
            Console.WriteLine("       pdm load [--dry] admin: load P:\\ into the vault (uses the pdm-admin SSO login)");
            Console.WriteLine("       pdm map <folder> admin: map P: to <folder> permanently (run as administrator)");
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

// Admin load with Grant's SSO login (profile pdm-admin). Run "aws sso login --profile pdm-admin" first.
static async Task<int> Load(Settings s, bool dryRun)
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
    var engine = new Engine(s, index, vault, "", "Admin", true, Console.WriteLine);
    await new Loader(s, index, vault, engine, "Grant Elliott", Console.WriteLine).RunAsync(dryRun);
    return 0;
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
