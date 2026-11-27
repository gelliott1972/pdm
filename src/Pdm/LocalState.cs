using System.Text.Json;

namespace Pdm;

// What this PC last fetched or checked in, per file: P:\.pdm\state.json. No database.
public sealed class LocalState
{
    public sealed class Entry
    {
        public string FileId { get; set; } = "";
        public string VersionId { get; set; } = "";
        public string ETag { get; set; } = "";
        public long Size { get; set; }
        public DateTime WriteTimeUtc { get; set; }
    }

    public Dictionary<string, Entry> Files { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // The entries as read (or last saved), so Save writes only what this run changed (a private field: not in the file).
    Dictionary<string, Entry> asRead = new(StringComparer.OrdinalIgnoreCase);

    static string FilePath(string root) => Path.Combine(root, ".pdm", "state.json");

    public static LocalState Load(string root)
    {
        var path = FilePath(root);
        if (!File.Exists(path)) return new LocalState();
        var loaded = JsonSerializer.Deserialize<LocalState>(File.ReadAllText(path)) ?? new LocalState();
        loaded.Files = new Dictionary<string, Entry>(loaded.Files, StringComparer.OrdinalIgnoreCase);
        loaded.asRead = Copy(loaded.Files);
        return loaded;
    }

    // Saves this run's changes only: state.json is read again and just the entries this run added, changed or removed
    // are written into it (27 Nov: two gets at once, each saving the whole file, lost entries). The PC lock (PcLock.cs)
    // keeps commands apart; this keeps another command's entries even if something ever ran outside it.
    public void Save(string root)
    {
        var path = FilePath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var merged = File.Exists(path) ? Load(root).Files : new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, e) in Files)
            if (!asRead.TryGetValue(key, out var was) || !Same(was, e)) merged[key] = e;
        foreach (var key in asRead.Keys)
            if (!Files.ContainsKey(key)) merged.Remove(key);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(new LocalState { Files = merged }, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
        Files = merged;
        asRead = Copy(merged);
    }

    static bool Same(Entry a, Entry b) => a.FileId == b.FileId && a.VersionId == b.VersionId && a.ETag == b.ETag && a.Size == b.Size && a.WriteTimeUtc == b.WriteTimeUtc;

    static Dictionary<string, Entry> Copy(Dictionary<string, Entry> files) => files.ToDictionary(x => x.Key,
        x => new Entry { FileId = x.Value.FileId, VersionId = x.Value.VersionId, ETag = x.Value.ETag, Size = x.Value.Size, WriteTimeUtc = x.Value.WriteTimeUtc },
        StringComparer.OrdinalIgnoreCase);

    public void Record(string key, string localPath, string fileId, string versionId, string etag)
    {
        var info = new FileInfo(localPath);
        Files[key] = new Entry { FileId = fileId, VersionId = versionId, ETag = etag, Size = info.Length, WriteTimeUtc = info.LastWriteTimeUtc };
    }

    // True when the file on disk differs from what was last fetched or checked in.
    // A file that can't be read (held by another program) counts as changed, so nothing overwrites it.
    public bool IsChanged(string key, string localPath) => Differs(Files.GetValueOrDefault(key), localPath);

    // The same check against an entry already looked up, for work done in parallel.
    public static bool Differs(Entry? e, string localPath)
    {
        if (e == null) return File.Exists(localPath);
        if (!File.Exists(localPath)) return true;
        var info = new FileInfo(localPath);
        if (info.Length == e.Size && info.LastWriteTimeUtc == e.WriteTimeUtc) return false;
        try { return Vault.Md5(localPath) != e.ETag; }
        catch (IOException) { return true; }
    }
}
