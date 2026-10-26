// SwSuppDump: open an assembly in SOLIDWORKS and dump, for every component at every level, what SOLIDWORKS
// knows about it, to find why components show as not loaded. Never saves; closes everything it opened.
// Usage: SwSuppDump.exe <assembly> [<config>]
//   One tab-separated line per component in the given (default: active) configuration: name, suppressed,
//   suppression state, loaded, path (and whether it exists), referenced configuration, then its parent's
//   name, referenced configuration, solving (rigid / flexible) and whether the parent's file has that
//   configuration. Then each distinct sub-assembly file opened on its own, every configuration shown in turn:
//   the components that are active there but not loaded or whose file is missing.
// Refuses to run while SOLIDWORKS is already open.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwSuppDump
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: SwSuppDump <assembly> [<config>]"); return 2; }
        string path = Path.GetFullPath(args[0]);
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        ISldWorks sw;
        try { sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks()); }
        catch (Exception e) { Console.Error.WriteLine("SOLIDWORKS didn't start (" + e.Message.Split('\n')[0].Trim() + ")."); return 3; }
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        sw.Visible = true;
        try
        {
            int errs = 0, warns = 0;
            var doc = sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, args.Length > 1 ? args[1] : "", ref errs, ref warns);
            Console.WriteLine("Open " + Path.GetFileName(path) + ": errors=" + errs + " warnings=" + warns);
            if (doc == null) return 1;
            var asm = (IAssemblyDoc)doc;
            asm.ResolveAllLightWeightComponents(false);
            Console.WriteLine("Configuration " + ((IConfiguration)doc.GetActiveConfiguration()).Name);
            Console.WriteLine("name\tsuppressed\tstate\tloaded\tpath\texists\tref_config\tparent\tparent_ref_config\tparent_solving\tparent_has_config");
            var subs = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Component2 c in (object[])asm.GetComponents(false) ?? new object[0])
            {
                string p = c.GetPathName() ?? "";
                var par = c.GetParent() as Component2;
                string parHas = "";
                if (par != null)
                {
                    var pm = par.GetModelDoc2() as IModelDoc2;
                    parHas = pm == null ? "?" : ((string[])pm.GetConfigurationNames()).Contains(par.ReferencedConfiguration) ? "yes" : "NO";
                }
                if (p.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase) && File.Exists(p)) subs.Add(p);
                Console.WriteLine(string.Join("\t", new[] {
                    c.Name2, c.IsSuppressed() ? "yes" : "", c.GetSuppression2().ToString(), c.GetModelDoc2() == null ? "NO" : "yes",
                    p, p == "" ? "" : File.Exists(p) ? "yes" : "NO", c.ReferencedConfiguration,
                    par == null ? "" : par.Name2, par == null ? "" : par.ReferencedConfiguration,
                    par == null ? "" : par.Solving.ToString(), parHas }));
            }
            sw.CloseAllDocuments(true);

            Console.WriteLine();
            Console.WriteLine("Sub-assemblies on their own, every configuration:");
            foreach (var s in subs)
            {
                var d = sw.OpenDoc6(s, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errs, ref warns);
                if (d == null) { Console.WriteLine("  " + s + ": COULD NOT OPEN (" + errs + ")"); continue; }
                foreach (var cn in (string[])d.GetConfigurationNames())
                {
                    bool shown = d.ShowConfiguration2(cn);
                    var a = (IAssemblyDoc)d;
                    a.ResolveAllLightWeightComponents(false);
                    int active = 0, bad = 0;
                    foreach (Component2 c in (object[])a.GetComponents(false) ?? new object[0])
                    {
                        if (c.IsSuppressed()) continue;
                        active++;
                        string p = c.GetPathName() ?? "";
                        if (c.GetModelDoc2() == null || p == "" || !File.Exists(p))
                        {
                            bad++;
                            Console.WriteLine("    " + Path.GetFileName(s) + " [" + cn + "] " + c.Name2 + " -> " + p + " ** not loaded or missing **");
                        }
                    }
                    Console.WriteLine("  " + Path.GetFileName(s) + " [" + cn + "]" + (shown ? "" : " COULD NOT SHOW") + ": " + active + " active, " + bad + " not loaded or missing");
                }
                sw.CloseDoc(d.GetTitle());
            }
            return 0;
        }
        finally
        {
            try { sw.CloseAllDocuments(true); sw.ExitApp(); } catch { }
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
    }

    // The newest SOLIDWORKS installed (the plain ProgID can start an older install).
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
