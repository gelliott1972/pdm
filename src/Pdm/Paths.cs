namespace Pdm;

public static class Paths
{
    // Vault key for a file under the local root: APEX/SC08/x.SLDPRT.
    public static string KeyOf(string root, string localPath)
    {
        var full = Path.GetFullPath(localPath);
        var rel = Path.GetRelativePath(root, full);
        if (rel.StartsWith("..") || Path.IsPathRooted(rel)) throw new ArgumentException($"{localPath} is not under {root}");
        return rel.Replace('\\', '/');
    }

    public static string LocalOf(string root, string key) => Path.Combine(root, key.Replace('/', '\\'));

    // Shared folders: any _Shared folder, and APEX/GNRIC. Matches the deny rule on the pdm-engineer role.
    public static bool IsShared(string key)
    {
        var parts = key.Split('/');
        return parts[..^1].Any(p => p.Equals("_Shared", StringComparison.OrdinalIgnoreCase))
            || key.StartsWith("APEX/GNRIC/", StringComparison.OrdinalIgnoreCase);
    }

    // Files the vault never holds: the app's own folder and SOLIDWORKS lock files.
    public static bool IsIgnored(string key) =>
        key.StartsWith(".pdm/", StringComparison.OrdinalIgnoreCase)
        || Path.GetFileName(key).StartsWith("~$")
        || key.EndsWith(".pdm-download", StringComparison.OrdinalIgnoreCase);

    // SOLIDWORKS files on this PC that the page offers to add: parts, assemblies, drawings.
    // Skips ignored files and SOLIDWORKS's own ~ temp and lock files. Lists names only; never opens a file.
    public static IEnumerable<string> LocalSolidWorksKeys(string root)
    {
        if (!Directory.Exists(root)) return [];
        return Directory.EnumerateFiles(root, "*", new EnumerationOptions { RecurseSubdirectories = true, IgnoreInaccessible = true })
            .Select(p => KeyOf(root, p))
            .Where(k => IsSolidWorksFile(k) && !IsIgnored(k));
    }

    // A part, assembly or drawing, not one of SOLIDWORKS's ~ temp or lock files.
    public static bool IsSolidWorksFile(string key) =>
        Path.GetExtension(key).ToLowerInvariant() is ".sldprt" or ".sldasm" or ".slddrw" && !Path.GetFileName(key).StartsWith('~');

    public static void SetReadOnly(string localPath, bool readOnly)
    {
        if (!File.Exists(localPath)) return;
        var a = File.GetAttributes(localPath);
        File.SetAttributes(localPath, readOnly ? a | FileAttributes.ReadOnly : a & ~FileAttributes.ReadOnly);
    }
}
