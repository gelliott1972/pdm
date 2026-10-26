// SwResave: open each SOLIDWORKS file, rebuild every configuration, and save it, so the component names stored
// inside update after Document Manager renames or replace-ref (0811 D14, D30; import: stale names lose suppression
// on a configuration switch). Each file gets its own SOLIDWORKS start (a batch died with RPC_E 0x800706BE), with one
// retry if that start crashes.
//
// Usage:
//   SwResave.exe <file> [<file> ...]
//       Work copies. Refuses the vault (P:\, C:\PDM\) and Dropbox.
//   SwResave.exe --import <plan files.csv> <vaultRoot> [--dry]
//       After Importer --apply and SwFixRefs --apply. Resaves only the assemblies the plan marks "import"
//       (vaultRoot\new_folder\new_name), children before parents (dependency order from Document Manager references,
//       which needs SW_DM_KEY). Refuses every other file, anything under .pdm, and Dropbox. --dry lists the order only.
// Exit code: 0 all saved, 1 any failure, 2 refused or bad arguments.
// Refuses to start while SOLIDWORKS is already open. Build (bin is gitignored; copy the interop DLLs in first):
//   csc /platform:x64 /out:bin\SwResave.exe /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll
//       /r:bin\SolidWorks.Interop.swdocumentmgr.dll SwResave.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;
using SolidWorks.Interop.swdocumentmgr;

static class SwResave
{
    static int Main(string[] args)
    {
        if (args.Length < 1) return Usage();
        // Internal: one file in this process (the parent starts one of these per file).
        // Exit 2 there means bad arguments or refused (no retry); 3 means SOLIDWORKS failed mid-run (retried once).
        if (args[0] == "--one" || args[0] == "--import-one")
        {
            string f;
            try
            {
                if (args[0] == "--one" && args.Length == 2) f = Path.GetFullPath(args[1]);
                else if (args[0] == "--import-one" && args.Length == 4)
                {
                    var allowed = AllowList(args[1], args[2]);
                    if (allowed == null) return 2;
                    f = Path.GetFullPath(args[3]);
                    if (!allowed.Contains(f, StringComparer.OrdinalIgnoreCase) || Forbidden(f)) { Console.Error.WriteLine("REFUSED (not in the import allow-list): " + f); return 2; }
                }
                else return Usage();
            }
            catch (Exception ex) { Console.Error.WriteLine("Bad arguments (" + ex.GetType().Name + "): " + ex.Message); return 2; }
            return One(f);
        }

        List<string> files;
        string[] childPrefix;
        if (args[0] == "--import")
        {
            if (args.Length < 3) return Usage();
            var allowed = AllowList(args[1], args[2]);
            if (allowed == null) return 2;
            var missing = allowed.Where(f => !File.Exists(f)).ToList();
            if (missing.Count > 0) { foreach (var m in missing) Console.Error.WriteLine("Missing in the vault: " + m); return 2; }
            files = DependencyOrder(allowed);
            if (files == null) return 2;
            Console.WriteLine("Import resave: " + files.Count + " assemblies, children first:");
            for (int i = 0; i < files.Count; i++) Console.WriteLine(string.Format("  {0,2}. {1}", i + 1, files[i]));
            if (args.Contains("--dry")) return 0;
            childPrefix = new[] { "--import-one", Quote(Path.GetFullPath(args[1])), Quote(Path.GetFullPath(args[2])) };
        }
        else
        {
            files = args.Select(Path.GetFullPath).ToList();
            foreach (var p in files)
                if (IsVault(p) || Forbidden(p)) { Console.Error.WriteLine("Refusing vault or Dropbox path: " + p); return 2; }
            childPrefix = new[] { "--one" };
        }

        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }
        string self = Process.GetCurrentProcess().MainModule.FileName;
        int problems = 0;
        foreach (var f in files)
        {
            int rc = -1;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                if (!WaitForNoSolidWorks()) { Console.WriteLine(Path.GetFileName(f) + ": SOLIDWORKS still running from the previous file  ** NOT SAVED **"); rc = 1; break; }
                rc = RunChild(self, childPrefix.Concat(new[] { Quote(f) }));
                if (rc == 0 || rc == 1) break;  // ran to the end (saved or failed): don't retry
                if (rc == 2) { Console.WriteLine(Path.GetFileName(f) + ": refused or bad arguments (see above), not retried"); break; }
                Console.WriteLine(Path.GetFileName(f) + ": SOLIDWORKS failed mid-run (exit " + rc + ")" + (attempt == 1 ? ", retrying once" : ""));
            }
            if (rc != 0) problems++;
        }
        Console.WriteLine(problems == 0 ? "RESULT: all saved" : "RESULT: " + problems + " problem(s)");
        return problems == 0 ? 0 : 1;
    }

    static int Usage()
    {
        Console.Error.WriteLine("Usage: SwResave <file> [<file> ...]   or   SwResave --import <plan files.csv> <vaultRoot> [--dry]");
        return 2;
    }

    // Open, rebuild every configuration, save. One SOLIDWORKS start, exited at the end.
    static int One(string path)
    {
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }
        if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) { Console.WriteLine(Path.GetFileName(path) + ": read-only on disk  ** NOT SAVED **"); return 1; }
        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        try
        {
            bool asm = path.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);
            int errs = 0, warns = 0;
            var doc = sw.OpenDoc6(path, (int)(asm ? swDocumentTypes_e.swDocASSEMBLY : swDocumentTypes_e.swDocPART),
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errs, ref warns);
            Console.Write(Path.GetFileName(path) + ": open errors=" + errs + " warnings=" + warns);
            if (doc == null || errs != 0) { Console.WriteLine("  ** NOT SAVED **"); return 1; }
            string first = ((IConfiguration)doc.GetActiveConfiguration()).Name;
            var names = (string[])doc.GetConfigurationNames();
            foreach (string cn in names)
            {
                doc.ShowConfiguration2(cn);
                if (asm) ((IAssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                doc.ForceRebuild3(false);
            }
            doc.ShowConfiguration2(first);
            int serr = 0, swarn = 0;
            bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref serr, ref swarn);
            Console.WriteLine("  rebuilt " + names.Length + " config(s), save " + (ok ? "ok" : "FAILED") + " (errors=" + serr + " warnings=" + swarn + ")");
            return ok ? 0 : 1;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Console.WriteLine();
            Console.WriteLine(Path.GetFileName(path) + ": SOLIDWORKS error 0x" + ex.ErrorCode.ToString("X8") + "  ** NOT SAVED **");
            return 3;
        }
        finally
        {
            try { sw.CloseAllDocuments(true); } catch { }
            try { sw.ExitApp(); } catch { }
            try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw); } catch { }
            if (started != null && !started.WaitForExit(30000))
                try { started.Kill(); started.WaitForExit(10000); } catch { }
        }
    }

    // The plan's "import" assemblies, as full vault paths. Null (after a message) if the plan or root is unusable.
    static List<string> AllowList(string csvPath, string vaultRoot)
    {
        if (!File.Exists(csvPath)) { Console.Error.WriteLine("No such plan: " + csvPath); return null; }
        string root = Path.GetFullPath(vaultRoot);
        if (!Directory.Exists(root) || Forbidden(root + "\\")) { Console.Error.WriteLine("Bad vault root: " + root); return null; }
        var lines = File.ReadAllLines(csvPath);
        if (lines.Length == 0) return new List<string>();
        var head = SplitCsv(lines[0].TrimStart('\uFEFF'));
        int ia = head.IndexOf("action"), iname = head.IndexOf("new_name"), ifold = head.IndexOf("new_folder");
        if (ia < 0 || iname < 0 || ifold < 0) { Console.Error.WriteLine("Plan needs columns action, new_name, new_folder: " + csvPath); return null; }
        var list = new List<string>();
        foreach (var line in lines.Skip(1))
        {
            if (line.Trim() == "") continue;
            var c = SplitCsv(line);
            if (c.Count <= Math.Max(ia, Math.Max(iname, ifold)) || c[ia] != "import") continue;
            if (!c[iname].EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase)) continue;
            string p = Path.GetFullPath(Path.Combine(root, c[ifold], c[iname]));
            if (!p.StartsWith(root.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase) || Forbidden(p)) { Console.Error.WriteLine("REFUSED plan row outside the vault root or under .pdm: " + p); return null; }
            if (!list.Contains(p, StringComparer.OrdinalIgnoreCase)) list.Add(p);
        }
        return list;
    }

    // Children before parents, from each assembly's Document Manager references to the other listed assemblies.
    static List<string> DependencyOrder(List<string> files)
    {
        string key = System.Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set (needed for the dependency order)."); return null; }
        var dm = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key);
        if (dm == null) { Console.Error.WriteLine("Document Manager returned no application (key rejected?)."); return null; }
        var byName = files.GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        var deps = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in files)
        {
            var d = new List<string>();
            SwDmDocumentOpenError err;
            var doc = dm.GetDocument(f, SwDmDocumentType.swDmDocumentAssembly, true, out err) as ISwDMDocument31;
            if (doc == null) { Console.Error.WriteLine("Document Manager could not open " + f + ": " + err); return null; }
            try
            {
                var search = dm.GetSearchOptionObject();
                search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchExternalReference);
                object broken, isVirtual, stamps, imported;
                var refs = doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[];
                foreach (var r in refs ?? new string[0])
                {
                    // Prefer the exact path; fall back to the file name, since references may still store old folders.
                    var hit = files.FirstOrDefault(x => x.Equals(Path.GetFullPath(r), StringComparison.OrdinalIgnoreCase));
                    List<string> same;
                    if (hit == null && byName.TryGetValue(Path.GetFileName(r), out same) && same.Count == 1) hit = same[0];
                    if (hit != null && !hit.Equals(f, StringComparison.OrdinalIgnoreCase) && !d.Contains(hit)) d.Add(hit);
                }
            }
            finally { doc.CloseDoc(); }
            deps[f] = d;
        }
        var order = new List<string>();
        var state = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);  // 1 visiting, 2 done
        foreach (var f in files)
            if (!Visit(f, deps, state, order)) return null;
        return order;
    }

    static bool Visit(string f, Dictionary<string, List<string>> deps, Dictionary<string, int> state, List<string> order)
    {
        int s;
        if (state.TryGetValue(f, out s)) { if (s == 1) { Console.Error.WriteLine("Reference loop at " + f); return false; } return true; }
        state[f] = 1;
        foreach (var c in deps[f])
            if (!Visit(c, deps, state, order)) return false;
        state[f] = 2;
        order.Add(f);
        return true;
    }

    static int RunChild(string self, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(self, string.Join(" ", args)) { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    static bool WaitForNoSolidWorks()
    {
        for (int i = 0; i < 60; i++)
        {
            if (Process.GetProcessesByName("SLDWORKS").Length == 0) return true;
            System.Threading.Thread.Sleep(1000);
        }
        return false;
    }

    static bool IsVault(string p)
    {
        return p.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || p.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
    }

    static bool Forbidden(string p)
    {
        return p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || p.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    // Quote one argument for the Windows command line: backslashes before a quote (or at the end, before the
    // closing quote) are doubled, so a root like "P:\" reaches the child intact.
    static string Quote(string s)
    {
        var b = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char ch in s)
        {
            if (ch == '\\') { slashes++; continue; }
            if (ch == '"') { b.Append('\\', slashes * 2 + 1); b.Append('"'); }
            else { b.Append('\\', slashes); b.Append(ch); }
            slashes = 0;
        }
        b.Append('\\', slashes * 2);
        return b.Append('"').ToString();
    }

    static List<string> SplitCsv(string line)
    {
        var r = new List<string>();
        var cur = new System.Text.StringBuilder();
        bool q = false;
        for (int i = 0; i < line.Length; i++)
        {
            char ch = line[i];
            if (q)
            {
                if (ch == '"' && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else if (ch == '"') q = false;
                else cur.Append(ch);
            }
            else if (ch == '"') q = true;
            else if (ch == ',') { r.Add(cur.ToString()); cur.Clear(); }
            else cur.Append(ch);
        }
        r.Add(cur.ToString());
        return r;
    }

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
