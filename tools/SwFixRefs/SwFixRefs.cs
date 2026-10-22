// SwFixRefs: after the importer, make the paths SOLIDWORKS itself stores point into the vault, using SOLIDWORKS'
// ReplaceReferencedDocument on closed files. The Document Manager's ReplaceReference doesn't reach every stored
// path: components that never load (e.g. inside a suppressed sub-assembly) keep their old path in SOLIDWORKS' view.
// Usage: SwFixRefs.exe <vaultRoot> <files.csv> [--apply] [<file> ...]
//   For every SOLIDWORKS file under <vaultRoot> (or only the files given), lists the stored references
//   (GetDocumentDependencies2, no search) and repoints any outside the vault to the vault file of that name,
//   mapping old names to new ones from the importer's files.csv. Without --apply it only lists.
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
        string root = Path.GetFullPath(args[0]).TrimEnd('\\') + "\\";
        bool apply = args.Contains("--apply");
        var only = args.Skip(2).Where(a => a != "--apply").Select(Path.GetFullPath).ToList();
        if (root.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED (inside Dropbox): " + root); return 2; }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        // Where each name now lives in the vault: every vault file by its own name, plus old names from the plan.
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sldprt", ".sldasm", ".slddrw" };
        var vaultFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories).Where(f => exts.Contains(Path.GetExtension(f))).ToList();
        var target = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in vaultFiles) target[Path.GetFileName(f)] = f;
        var lines = File.ReadAllLines(args[1]).Skip(1);
        foreach (var l in lines)
        {
            var c = l.Split(',');   // source,action,new_name,new_folder (no commas in these values)
            if (c.Length >= 4 && c[1] == "import")
            {
                string p = root + c[3] + "\\" + c[2];
                if (File.Exists(p)) target[Path.GetFileName(c[0])] = p;
            }
        }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application"));
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        int outside = 0, fixedCount = 0, failed = 0;
        try
        {
            foreach (var f in (only.Count > 0 ? only : vaultFiles))
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
        return failed == 0 ? 0 : 1;
    }

    static string Rel(string p, string root)
    {
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? p.Substring(root.Length) : p;
    }
}
