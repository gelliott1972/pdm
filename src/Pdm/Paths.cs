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

    public static bool IsPdf(string key) => key.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);

    // A scene's release folders (R8): <Scene>/_Released, _Review, _Commented, _WIP.
    static readonly string[] ReleaseFolders = ["_Released", "_Review", "_Commented", "_WIP"];
    // In one of them, or below one (a release set's folder, <Scene>/_Review/<Set>_<code>).
    public static bool InReleaseFolder(string key)
    {
        var parts = key.Split('/');
        return parts.Length >= 2 && parts[..^1].Any(p => ReleaseFolders.Contains(p, StringComparer.OrdinalIgnoreCase));
    }

    // A scene's drawing register, rewritten by each release (Grant, 9 Nov): <Scene>/<scene>_Drawing_Register.xlsx,
    // e.g. APEX/SC08/SC08_Drawing_Register.xlsx.
    public static string RegisterKey(string scene) => $"{scene}/{scene.Split('/')[^1]}_Drawing_Register.xlsx";
    public static bool IsRegister(string key)
    {
        var parts = key.Split('/');
        return parts.Length == 3 && parts[2].Equals($"{parts[1]}_Drawing_Register.xlsx", StringComparison.OrdinalIgnoreCase);
    }

    // Written only by the release step: everything in release folders, and the drawing registers.
    public static bool IsReleaseFile(string key) => InReleaseFolder(key) || IsRegister(key);

    // A part, assembly or drawing, not one of SOLIDWORKS's ~ temp or lock files.
    public static bool IsSolidWorksFile(string key) =>
        Path.GetExtension(key).ToLowerInvariant() is ".sldprt" or ".sldasm" or ".slddrw" && !Path.GetFileName(key).StartsWith('~');

    // After a file leaves this PC: removes its folder, and the folders above it, while they are empty
    // (Explorer's desktop.ini and Thumbs.db don't count). Never the root itself. Quietly stops on any error.
    public static void RemoveEmptyFolders(string root, string removedFile)
    {
        var top = Path.GetFullPath(root).TrimEnd('\\');
        var dir = Path.GetDirectoryName(Path.GetFullPath(removedFile));
        try
        {
            while (dir != null && dir.Length > top.Length && dir.StartsWith(top + "\\", StringComparison.OrdinalIgnoreCase) && Directory.Exists(dir))
            {
                if (Directory.EnumerateDirectories(dir).Any()) return;
                var files = Directory.GetFiles(dir);
                if (files.Any(f => Path.GetFileName(f) is not ("desktop.ini" or "Thumbs.db"))) return;
                foreach (var f in files) { File.SetAttributes(f, FileAttributes.Normal); File.Delete(f); }
                Directory.Delete(dir);
                dir = Path.GetDirectoryName(dir);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }

    public static void SetReadOnly(string localPath, bool readOnly)
    {
        if (!File.Exists(localPath)) return;
        var a = File.GetAttributes(localPath);
        File.SetAttributes(localPath, readOnly ? a | FileAttributes.ReadOnly : a & ~FileAttributes.ReadOnly);
    }
}
