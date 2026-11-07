// SwFixRefs: after the importer, make the paths SOLIDWORKS itself stores point into the vault, using SOLIDWORKS'
// ReplaceReferencedDocument on closed files. The Document Manager's ReplaceReference doesn't reach every stored
// path: components that never load (e.g. inside a suppressed sub-assembly) keep their old path in SOLIDWORKS' view.
// Usage: SwFixRefs.exe <vaultRoot> <files.csv> [--apply] [<file> ...]
//   For every file files.csv imports (or only the files given), lists the stored references
//   (GetDocumentDependencies2, no search) and repoints any outside the vault to the vault file of that name,
//   mapping old names to new ones from the importer's files.csv. Without --apply it only lists.
//    or: SwFixRefs.exe <vaultRoot> --round <round.csv> [--apply]   (files moved inside the vault; see Round below)
// Refuses to run while SOLIDWORKS is already open; ends the copy it started. Refuses Dropbox paths.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;

static class SwFixRefs
{
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: SwFixRefs <vaultRoot> <files.csv> [--apply] [<file> ...]"); return 2; }
        if (args.Length >= 2 && args[1] == "--round") return Round(args);
        string root = Path.GetFullPath(args[0]).TrimEnd('\\') + "\\";
        bool apply = args.Contains("--apply");
        var only = args.Skip(2).Where(a => a != "--apply").Select(Path.GetFullPath).ToList();
        if (root.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED (inside Dropbox): " + root); return 2; }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        // Where each name now lives in the vault: every vault file by its own name, plus old names from the plan.
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sldprt", ".sldasm", ".slddrw" };
        // P:\.pdm is the PDM app's own state: never read or touched.
        string pdmState = root + ".pdm\\";
        var vaultFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f)) && !f.StartsWith(pdmState, StringComparison.OrdinalIgnoreCase)).ToList();
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in vaultFiles) target[Path.GetFileName(f)] = f;
        // Only the files this import adds are checked and repointed: files already in the vault are tracked by
        // the PDM and change only through checkout / checkin.
        var imported = new List<string>();
        var lines = File.ReadAllLines(args[1]).Skip(1);
        foreach (var l in lines)
        {
            var c = l.Split(',');   // source,action,new_name,new_folder (no commas in these values)
            if (c.Length >= 4 && (c[1] == "import" || c[1] == "reuse"))   // reuse: already in the vault from an earlier screen
            {
                string p = root + c[3] + "\\" + c[2];
                if (!File.Exists(p)) continue;
                target[Path.GetFileName(c[0])] = p;
                if (c[1] == "import") imported.Add(p);
            }
        }

        ISldWorks sw;
        try { sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks()); }
        catch (Exception e)
        {
            Console.Error.WriteLine("SOLIDWORKS didn't start (" + e.Message.Split('\n')[0].Trim() + "). Start it by hand once, deal with any licence or activation message, close it and try again.");
            return 3;
        }
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started.");
        int outside = 0, fixedCount = 0, failed = 0;
        try
        {
            foreach (var f in (only.Count > 0 ? only : imported))
            {
                var deps = sw.GetDocumentDependencies2(f, false, false, false) as string[];
                if (deps == null) continue;
                for (int i = 0; i + 1 < deps.Length; i += 2)
                {
                    string p = deps[i + 1];
                    if (string.IsNullOrEmpty(p) || p.StartsWith(root, StringComparison.OrdinalIgnoreCase)) continue;
                    outside++;
                    string to;
                    if (!target.TryGetValue(Path.GetFileName(p), out to))
                    {
                        Console.WriteLine(Rel(f, root) + ": " + p + "  ** no vault file of that name **");
                        failed++;
                        continue;
                    }
                    if (!apply) { Console.WriteLine(Rel(f, root) + ": " + p + " -> " + Rel(to, root)); continue; }
                    bool ok = sw.ReplaceReferencedDocument(f, p, to);
                    Console.WriteLine(Rel(f, root) + ": " + p + " -> " + Rel(to, root) + (ok ? "" : "  ** FAILED **"));
                    if (ok) fixedCount++; else failed++;
                }
            }
        }
        finally
        {
            try { sw.ExitApp(); } catch { }
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw);
            sw = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (started != null && !started.WaitForExit(30000))
            {
                Console.Error.WriteLine("SOLIDWORKS did not exit after 30 s; ending the copy this run started.");
                try { started.Kill(); started.WaitForExit(10000); } catch { }
            }
        }
        Console.WriteLine();
        Console.WriteLine("Stored references outside the vault: " + outside + (apply ? ", repointed: " + fixedCount : "") + ", problems: " + failed);
        // Without --apply this is the gate: any stored link outside the vault fails it, even one that could be repointed.
        if (!apply && outside > 0) { Console.WriteLine("GATE FAILED: " + outside + " stored link(s) outside the vault."); return 1; }
        return failed == 0 ? 0 : 1;
    }

    // ---------- round mode: files moved inside the vault (0804 restructure, 7 Nov) ----------
    // round.csv columns action,old_path,new_path,who,why (paths relative to the vault root). For the files the round
    // changes ("changed", "moved+changed", at new_path), every stored reference to an old path of a "moved" or
    // "moved+changed" row is repointed to its new path. A stored path counts as in the vault when it starts with the
    // root, P:\ or C:\PDM\ (a rehearsal copy's untouched files still store P:\ paths; the part below the root is used).
    // Without --apply: the gate, over EVERY file in the vault: no stored reference to an old or retired path, none to a
    // file that isn't there, none outside the vault.
    static int Round(string[] args)
    {
        string root = Path.GetFullPath(args[0]).TrimEnd('\\') + "\\";
        if (args.Length < 3) { Console.Error.WriteLine("Usage: SwFixRefs <vaultRoot> --round <round.csv> [--apply]"); return 2; }
        bool apply = args.Contains("--apply");
        if (root.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || root.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED (Dropbox / M:): " + root); return 2; }
        if (!Directory.Exists(root)) { Console.Error.WriteLine("No such vault root: " + root); return 2; }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }
        var lines = File.ReadAllLines(args[2]);
        var head = lines[0].TrimStart('\uFEFF').Split(',').ToList();
        int ia = head.IndexOf("action"), io = head.IndexOf("old_path"), inew = head.IndexOf("new_path");
        if (ia < 0 || io < 0 || inew < 0) { Console.Error.WriteLine("round.csv needs columns action, old_path, new_path"); return 2; }
        var moved = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var retired = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        foreach (var l in lines.Skip(1))
        {
            if (l.Trim() == "") continue;
            var c = l.Split(',');   // no commas in the paths (checked when the list is made)
            string a = c[ia], o = c[io], n = c[inew];
            if (a == "moved" || a == "moved+changed") moved[o] = n;
            if (a == "retired") retired.Add(o);
            if (a == "changed" || a == "moved+changed") changed.Add(root + n);
        }
        Console.WriteLine("Round: " + moved.Count + " moved, " + retired.Count + " retired, " + changed.Count + " changed files to repoint" + (apply ? "" : " (gate: every vault file)"));
        string pdmState = root + ".pdm\\";
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sldprt", ".sldasm", ".slddrw" };
        var files = apply ? changed : Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => exts.Contains(Path.GetExtension(f)) && !f.StartsWith(pdmState, StringComparison.OrdinalIgnoreCase) && !Path.GetFileName(f).StartsWith("~$")).ToList();

        ISldWorks sw;
        try { sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks()); }
        catch (Exception e) { Console.Error.WriteLine("SOLIDWORKS didn't start (" + e.Message.Split('\n')[0].Trim() + ")."); return 3; }
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started.");
        int repointed = 0, toOld = 0, toRetired = 0, missing = 0, outside = 0, failed = 0, checkedFiles = 0;
        try
        {
            foreach (var f in files)
            {
                if (!File.Exists(f)) { Console.WriteLine(Rel(f, root) + ": ** not in the vault (moved yet?) **"); failed++; continue; }
                if (apply && (File.GetAttributes(f) & FileAttributes.ReadOnly) != 0) { Console.WriteLine(Rel(f, root) + ": ** read-only (not checked out) **"); failed++; continue; }
                checkedFiles++;
                var deps = sw.GetDocumentDependencies2(f, false, false, false) as string[];
                if (deps == null) continue;
                for (int i = 0; i + 1 < deps.Length; i += 2)
                {
                    string p = deps[i + 1];
                    if (string.IsNullOrEmpty(p)) continue;
                    string rel = InVault(p, root);
                    string to;
                    if (rel != null && moved.TryGetValue(rel, out to))
                    {
                        if (!apply) { Console.WriteLine(Rel(f, root) + ": still points at the old path " + p + "  ** GATE **"); toOld++; continue; }
                        bool ok = sw.ReplaceReferencedDocument(f, p, root + to);
                        Console.WriteLine(Rel(f, root) + ": " + p + " -> " + to + (ok ? "" : "  ** FAILED **"));
                        if (ok) repointed++; else failed++;
                        continue;
                    }
                    if (apply) continue;
                    if (rel == null) { Console.WriteLine(Rel(f, root) + ": outside the vault: " + p + "  ** GATE **"); outside++; }
                    else if (retired.Contains(rel)) { Console.WriteLine(Rel(f, root) + ": uses a retired file: " + p + "  ** GATE **"); toRetired++; }
                    else if (!File.Exists(root + rel)) { Console.WriteLine(Rel(f, root) + ": no such vault file: " + p + "  ** GATE **"); missing++; }
                }
            }
        }
        finally
        {
            try { sw.ExitApp(); } catch { }
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw);
            sw = null;
            GC.Collect();
            GC.WaitForPendingFinalizers();
            if (started != null && !started.WaitForExit(30000))
            {
                Console.Error.WriteLine("SOLIDWORKS did not exit after 30 s; ending the copy this run started.");
                try { started.Kill(); started.WaitForExit(10000); } catch { }
            }
        }
        Console.WriteLine();
        if (apply)
        {
            Console.WriteLine("Files checked: " + checkedFiles + ", references repointed: " + repointed + ", problems: " + failed);
            return failed == 0 ? 0 : 1;
        }
        int gate = toOld + toRetired + missing + outside + failed;
        Console.WriteLine("Gate over " + checkedFiles + " files: old paths " + toOld + ", retired files " + toRetired + ", missing files " + missing
            + ", outside the vault " + outside + ", other problems " + failed);
        Console.WriteLine(gate == 0 ? "GATE PASSED" : "GATE FAILED");
        return gate == 0 ? 0 : 1;
    }

    // The path below the vault root, if p is in the vault (the root itself, P:\ or C:\PDM\); otherwise null.
    static string InVault(string p, string root)
    {
        foreach (var r in new[] { root, @"P:\", @"C:\PDM\" })
            if (p.StartsWith(r, StringComparison.OrdinalIgnoreCase)) return p.Substring(r.Length);
        return null;
    }

    static string Rel(string p, string root)
    {
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? p.Substring(root.Length) : p;
    }

    // The newest SOLIDWORKS installed. The plain ProgID can start an older install (Grant's laptop has 2024
    // beside 2026, and the plain name starts 2024), which refuses newer files as "future version".
    static Type NewestSolidWorks()
    {
        int best = -1;
        foreach (var n in Microsoft.Win32.Registry.ClassesRoot.GetSubKeyNames())
        {
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^SldWorks\.Application\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int v;
            if (m.Success && int.TryParse(m.Groups[1].Value, out v) && v > best) best = v;
        }
        return Type.GetTypeFromProgID(best >= 0 ? "SldWorks.Application." + best : "SldWorks.Application");
    }
}
