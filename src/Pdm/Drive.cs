using System.Diagnostics;
using System.Security.Principal;
using Microsoft.Win32;

namespace Pdm;

// The permanent P: mapping: a DOS Devices entry in the registry, which Windows creates at every start-up
// for all programs (including ones run as administrator). It takes effect at the next restart.
public static class Drive
{
    const string Key = @"SYSTEM\CurrentControlSet\Control\Session Manager\DOS Devices";

    public static int Map(string letter, string folder, Action<string> say)
    {
        folder = Path.GetFullPath(folder).TrimEnd('\\');
        letter = letter.TrimEnd('\\', ':').ToUpperInvariant() + ":";

        if (!Directory.Exists(folder)) { say($"No such folder: {folder}"); return 1; }
        if (!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator))
        {
            say("This needs administrator rights: right-click PowerShell, choose \"Run as administrator\", and run it again.");
            return 1;
        }

        var current = Subst().GetValueOrDefault(letter);
        using var reg = Registry.LocalMachine.OpenSubKey(Key, writable: true)!;
        var stored = reg.GetValue(letter) as string;

        if (current == null && stored == null && Directory.Exists(letter + "\\"))
        {
            say($"{letter} is already used by a disk or network drive. Free it first, or tell Grant.");
            return 1;
        }
        if (current != null && !current.Equals(folder, StringComparison.OrdinalIgnoreCase))
        {
            say($"{letter} currently points to {current}. Remove that first: subst {letter} /D");
            return 1;
        }

        reg.SetValue(letter, @"\??\" + folder, RegistryValueKind.String);
        say($"{letter} now points to {folder}, and will after every restart.");
        // A subst made here only shows in administrator windows (Windows keeps them apart), so a new mapping needs a restart.
        if (current == null) say($"Restart Windows now: until then {letter} only shows in administrator windows.");
        return 0;
    }

    // Current SUBST mappings, e.g. P: -> E:\PDM-test\P.
    static Dictionary<string, string> Subst()
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in Run("subst", "").Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = line.Split("=>", 2);
            if (parts.Length == 2) result[parts[0].Trim().TrimEnd('\\')] = parts[1].Trim().TrimEnd('\\');
        }
        return result;
    }

    static string Run(string exe, string args)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, args) { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true })!;
        var output = p.StandardOutput.ReadToEnd();
        p.WaitForExit();
        return output;
    }
}
