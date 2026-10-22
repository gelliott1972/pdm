using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Pdm;

// One row of pdm-files.
public sealed class FileRecord
{
    public string FileId { get; set; } = "";
    public string Path { get; set; } = "";          // vault key, e.g. APEX/SC08/LS-08X-06/LS-08X-06.SLDASM
    public string VersionId { get; set; } = "";     // S3 version of the current file
    public string ETag { get; set; } = "";          // MD5 of the current file
    public long Size { get; set; }
    public string UpdatedAt { get; set; } = "";
    public string UpdatedBy { get; set; } = "";
    public string Note { get; set; } = "";
    public List<string> Refs { get; set; } = new();         // fileIds this file references
    public List<string> MissingRefs { get; set; } = new();  // references not found in the vault
    public List<PartNoEntry> PartNos { get; set; } = new();
    public string? LockedBy { get; set; }           // email
    public string? LockedByName { get; set; }
    public string? LockedAt { get; set; }
    public string? LockMachine { get; set; }
}

public sealed class LockTakenException(FileRecord holder) : Exception($"{holder.Path} is checked out by {holder.LockedByName} since {holder.LockedAt}")
{
    public FileRecord Holder { get; } = holder;
}

public sealed class Index(IAmazonDynamoDB db, Settings s)
{
    public async Task<List<FileRecord>> AllAsync()
    {
        var result = new List<FileRecord>();
        Dictionary<string, AttributeValue>? start = null;
        do
        {
            var page = await db.ScanAsync(new ScanRequest { TableName = s.FilesTable, ExclusiveStartKey = start });
            result.AddRange(page.Items.Select(FromItem));
            start = page.LastEvaluatedKey is { Count: > 0 } k ? k : null;
        } while (start != null);
        return result;
    }

    public async Task<FileRecord?> GetAsync(string fileId)
    {
        var r = await db.GetItemAsync(new GetItemRequest
        {
            TableName = s.FilesTable, ConsistentRead = true,
            Key = new() { ["fileId"] = S(fileId) }
        });
        return r.Item is { Count: > 0 } ? FromItem(r.Item) : null;
    }

    // Whole-record write, used by the loader.
    public Task PutAsync(FileRecord f) => db.PutItemAsync(new PutItemRequest { TableName = s.FilesTable, Item = ToItem(f) });

    // Takes the lock unless someone else holds it. Holding it already is fine.
    public async Task LockAsync(string fileId, string email, string name, string machine)
    {
        try
        {
            await db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = s.FilesTable,
                Key = new() { ["fileId"] = S(fileId) },
                UpdateExpression = "SET lockedBy = :me, lockedByName = :name, lockedAt = :at, lockMachine = :machine",
                ConditionExpression = "attribute_exists(fileId) AND (attribute_not_exists(lockedBy) OR lockedBy = :me)",
                ExpressionAttributeValues = new()
                {
                    [":me"] = S(email), [":name"] = S(name), [":at"] = S(Now()), [":machine"] = S(machine)
                }
            });
        }
        catch (ConditionalCheckFailedException)
        {
            var holder = await GetAsync(fileId) ?? throw new InvalidOperationException("File is not in the index.");
            throw new LockTakenException(holder);
        }
    }

    public async Task UnlockAsync(string fileId, string email)
    {
        try
        {
            await db.UpdateItemAsync(new UpdateItemRequest
            {
                TableName = s.FilesTable,
                Key = new() { ["fileId"] = S(fileId) },
                UpdateExpression = "REMOVE lockedBy, lockedByName, lockedAt, lockMachine",
                ConditionExpression = "lockedBy = :me",
                ExpressionAttributeValues = new() { [":me"] = S(email) }
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException("You don't hold this file.");
        }
    }

    // Records a new version and releases the lock, only while I still hold it.
    public async Task CheckInAsync(FileRecord f, string email)
    {
        f.LockedBy = f.LockedByName = f.LockedAt = f.LockMachine = null;
        var item = ToItem(f);
        try
        {
            await db.PutItemAsync(new PutItemRequest
            {
                TableName = s.FilesTable, Item = item,
                ConditionExpression = "lockedBy = :me",
                ExpressionAttributeValues = new() { [":me"] = S(email) }
            });
        }
        catch (ConditionalCheckFailedException)
        {
            throw new InvalidOperationException("You no longer hold this file; the new version is in the vault but not recorded. Tell Grant.");
        }
    }

    // PartNo register: point each PartNo at its file and configuration, and drop ones the file no longer has.
    public async Task UpdatePartNosAsync(FileRecord f, IEnumerable<PartNoEntry> previous)
    {
        foreach (var p in f.PartNos)
            await db.PutItemAsync(new PutItemRequest
            {
                TableName = s.PartNosTable,
                Item = new()
                {
                    ["partNo"] = S(p.PartNo), ["fileId"] = S(f.FileId), ["path"] = S(f.Path),
                    ["config"] = S(p.Config), ["status"] = S(p.Status)
                }
            });
        foreach (var old in previous.Where(o => !f.PartNos.Any(p => p.PartNo.Equals(o.PartNo, StringComparison.OrdinalIgnoreCase))))
        {
            try
            {
                await db.DeleteItemAsync(new DeleteItemRequest
                {
                    TableName = s.PartNosTable,
                    Key = new() { ["partNo"] = S(old.PartNo) },
                    ConditionExpression = "fileId = :id",
                    ExpressionAttributeValues = new() { [":id"] = S(f.FileId) }
                });
            }
            catch (ConditionalCheckFailedException) { } // now belongs to another file
        }
    }

    // PartNos already registered to a different file.
    public async Task<List<string>> PartNoClashesAsync(FileRecord f)
    {
        var clashes = new List<string>();
        foreach (var p in f.PartNos)
        {
            var r = await db.GetItemAsync(new GetItemRequest
            {
                TableName = s.PartNosTable, Key = new() { ["partNo"] = S(p.PartNo) }
            });
            if (r.Item is { Count: > 0 } && r.Item["fileId"].S != f.FileId)
                clashes.Add($"{p.PartNo} is already on {r.Item["path"].S}");
        }
        return clashes;
    }

    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    static AttributeValue S(string v) => new() { S = v };

    static Dictionary<string, AttributeValue> ToItem(FileRecord f)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["fileId"] = S(f.FileId), ["path"] = S(f.Path), ["versionId"] = S(f.VersionId), ["etag"] = S(f.ETag),
            ["size"] = new() { N = f.Size.ToString() }, ["updatedAt"] = S(f.UpdatedAt), ["updatedBy"] = S(f.UpdatedBy),
            ["note"] = S(f.Note),
            ["refs"] = new() { L = f.Refs.Select(S).ToList() },
            ["missingRefs"] = new() { L = f.MissingRefs.Select(S).ToList() },
            ["partNos"] = new()
            {
                L = f.PartNos.Select(p => new AttributeValue
                {
                    M = new() { ["config"] = S(p.Config), ["partNo"] = S(p.PartNo), ["status"] = S(p.Status) }
                }).ToList()
            }
        };
        if (f.LockedBy != null)
        {
            item["lockedBy"] = S(f.LockedBy);
            item["lockedByName"] = S(f.LockedByName ?? "");
            item["lockedAt"] = S(f.LockedAt ?? "");
            item["lockMachine"] = S(f.LockMachine ?? "");
        }
        return item;
    }

    static FileRecord FromItem(Dictionary<string, AttributeValue> i)
    {
        string Str(string k) => i.TryGetValue(k, out var v) && v.S != null ? v.S : "";
        string? Opt(string k) => i.TryGetValue(k, out var v) ? v.S : null;
        List<string> Strs(string k) => i.TryGetValue(k, out var v) && v.L != null ? v.L.Select(x => x.S).ToList() : new();
        return new FileRecord
        {
            FileId = Str("fileId"), Path = Str("path"), VersionId = Str("versionId"), ETag = Str("etag"),
            Size = i.TryGetValue("size", out var sz) && sz.N != null ? long.Parse(sz.N) : 0,
            UpdatedAt = Str("updatedAt"), UpdatedBy = Str("updatedBy"), Note = Str("note"),
            Refs = Strs("refs"), MissingRefs = Strs("missingRefs"),
            PartNos = i.TryGetValue("partNos", out var pn) && pn.L != null
                ? pn.L.Select(m => new PartNoEntry(m.M["config"].S, m.M["partNo"].S, m.M["status"].S)).ToList()
                : new(),
            LockedBy = Opt("lockedBy"), LockedByName = Opt("lockedByName"), LockedAt = Opt("lockedAt"), LockMachine = Opt("lockMachine")
        };
    }
}
