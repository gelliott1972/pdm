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

    static string FilePath(string root) => Path.Combine(root, ".pdm", "state.json");

    public static LocalState Load(string root)
    {
        var path = FilePath(root);
        if (!File.Exists(path)) return new LocalState();
        var loaded = JsonSerializer.Deserialize<LocalState>(File.ReadAllText(path)) ?? new LocalState();
        loaded.Files = new Dictionary<string, Entry>(loaded.Files, StringComparer.OrdinalIgnoreCase);
        return loaded;
    }

    public void Save(string root)
    {
        var path = FilePath(root);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, path, overwrite: true);
    }

    public void Record(string key, string localPath, string fileId, string versionId, string etag)
    {
        var info = new FileInfo(localPath);
        Files[key] = new Entry { FileId = fileId, VersionId = versionId, ETag = etag, Size = info.Length, WriteTimeUtc = info.LastWriteTimeUtc };
    }

    // True when the file on disk differs from what was last fetched or checked in.
    // A file that can't be read (held by another program) counts as changed, so nothing overwrites it.
    public bool IsChanged(string key, string localPath)
    {
        if (!Files.TryGetValue(key, out var e)) return File.Exists(localPath);
        if (!File.Exists(localPath)) return true;
        var info = new FileInfo(localPath);
        if (info.Length == e.Size && info.LastWriteTimeUtc == e.WriteTimeUtc) return false;
        try { return Vault.Md5(localPath) != e.ETag; }
        catch (IOException) { return true; }
    }
}
