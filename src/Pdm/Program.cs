using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Pdm;

// Test command line for the engine. The page comes later.
var settings = Settings.Load();
var command = args.Length > 0 ? args[0].ToLowerInvariant() : "";

switch (command)
{
    case "login":
        return await LoginTest(settings);
    default:
        Console.WriteLine("Usage: pdm login");
        return 2;
}

static async Task<int> LoginTest(Settings s)
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

    Console.WriteLine($"Logged in as {session.Name} ({session.Email}). Shared editor: {(session.SharedEditor ? "yes" : "no")}");

    var region = RegionEndpoint.GetBySystemName(s.Region);
    using var s3 = new AmazonS3Client(session.Credentials, region);
    var list = await s3.ListObjectsV2Async(new ListObjectsV2Request { BucketName = s.Bucket, MaxKeys = 5 });
    Console.WriteLine($"Vault read: OK ({list.KeyCount ?? 0} files in the first page)");

    using var db = new AmazonDynamoDBClient(session.Credentials, region);
    await db.GetItemAsync(new GetItemRequest
    {
        TableName = s.FilesTable,
        Key = new() { ["fileId"] = new AttributeValue { S = "login-test" } }
    });
    Console.WriteLine("Index read: OK");
    return 0;
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
