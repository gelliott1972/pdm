// SwCheck: open a SOLIDWORKS file in SOLIDWORKS itself and report what it actually loaded, to check
// work done with the Document Manager. Never saves; closes everything it opened.
// Usage: SwCheck.exe <file> <rootFolder> [<config> ...]
//   For each configuration (default: all), lists every component: file, referenced configuration,
//   suppressed, PartNo and PartNoStatus. Then lists every open document and flags any outside <rootFolder>.
// Refuses to run while SOLIDWORKS is already open, so it never touches someone's session and same-named
// files already in memory can't stand in for the files being checked.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwCheck
{
    static int Main(string[] args)
    {
        if (args.Length < 2) { Console.Error.WriteLine("Usage: SwCheck <file> <rootFolder> [<config> ...]"); return 2; }
        string path = Path.GetFullPath(args[0]);
        string root = Path.GetFullPath(args[1]).TrimEnd('\\') + "\\";
        if (Process.GetProcessesByName("SLDWORKS").Length > 0)
        {
            Console.Error.WriteLine("SOLIDWORKS is already running. Close it first.");
            return 2;
        }

        ISldWorks sw;
        try { sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application")); }
        catch (Exception e)
        {
            Console.Error.WriteLine("SOLIDWORKS didn't start (" + e.Message.Split('\n')[0].Trim() + "). Start it by hand once, deal with any licence or activation message, close it and try again.");
            return 3;
        }
        // The SOLIDWORKS this run started, so it can be made to exit at the end (ExitApp alone can leave it
        // running with no window, holding files in memory).
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        sw.Visible = true;
        int problems = 0;
        try
        {
            // SOLIDWORKS searches File Locations > Referenced Documents before the assembly's own folders, so a
            // live folder listed there silently replaces the files being checked.
            string refDocs = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileLocationsDocuments) ?? "";
            foreach (var f in refDocs.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                bool dropbox = f.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0;
                if (dropbox) problems++;
                Console.WriteLine("Referenced Documents folder: " + f + (dropbox ? "  ** DROPBOX: remove it **" : !Path.GetFullPath(f).StartsWith(root, StringComparison.OrdinalIgnoreCase) ? "  (outside root)" : ""));
            }

            int errs = 0, warns = 0;
            var type = path.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase) ? swDocumentTypes_e.swDocASSEMBLY : swDocumentTypes_e.swDocPART;
            var doc = sw.OpenDoc6(path, (int)type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errs, ref warns);
            Console.WriteLine("Open " + Path.GetFileName(path) + ": errors=" + Flags(typeof(swFileLoadError_e), errs) + " warnings=" + Flags(typeof(swFileLoadWarning_e), warns));
            if (doc == null) return 1;
            // Needs rebuild is expected for files last saved elsewhere; anything else counts as a problem.
            if (errs != 0 || (warns & ~(int)swFileLoadWarning_e.swFileLoadWarning_NeedsRegen) != 0) problems++;

            var cfgs = args.Length > 2 ? args.Skip(2).ToArray() : (string[])doc.GetConfigurationNames();
            foreach (var cn in cfgs)
            {
                Console.WriteLine();
                bool shown = ((IConfiguration)doc.GetActiveConfiguration()).Name == cn || doc.ShowConfiguration2(cn);
                if (!shown) problems++;
                Console.WriteLine("Configuration " + cn + ": " + (shown ? "shown" : "COULD NOT SHOW"));
                Props(doc, cn, "");
                if (type == swDocumentTypes_e.swDocASSEMBLY)
                {
                    var asm = (IAssemblyDoc)doc;
                    asm.ResolveAllLightWeightComponents(false);
                    var comps = asm.GetComponents(false) as object[];
                    if (comps != null)
                        foreach (Component2 c in comps)
                        {
                            bool suppressed = c.IsSuppressed();
                            string p = c.GetPathName();
                            var m = c.GetModelDoc2() as IModelDoc2;
                            string depth = new string(' ', 2 * (c.Name2.Count(ch => ch == '/') + 1));
                            string flag = suppressed ? "  (suppressed)" : m == null ? "  ** NOT LOADED **" : !p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? "  ** OUTSIDE ROOT **" : "";
                            if (!suppressed && flag != "") problems++;
                            Console.WriteLine(depth + c.Name2 + " -> " + Rel(p, root) + " [" + c.ReferencedConfiguration + "]" + flag);
                            if (m != null && !suppressed) Props(m, c.ReferencedConfiguration, depth + "    ");
                        }
                }
            }

            Console.WriteLine();
            Console.WriteLine("Documents in memory:");
            var docs = sw.GetDocuments() as object[];
            if (docs != null)
                foreach (IModelDoc2 d in docs)
                {
                    string p = d.GetPathName();
                    bool outside = !p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
                    if (outside) problems++;
                    Console.WriteLine("  " + Rel(p, root) + (outside ? "  ** OUTSIDE ROOT **" : ""));
                    if (d.GetType() == (int)swDocumentTypes_e.swDocPART) problems += PartLinks(d, root);
                    problems += Equations(d);
                }
            Console.WriteLine();
            Console.WriteLine(problems == 0 ? "RESULT: all loaded from inside the root, nothing missing" : "RESULT: " + problems + " problem(s)");
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
                Console.Error.WriteLine("SOLIDWORKS did not exit after 30 s; ending the copy this check started.");
                try { started.Kill(); started.WaitForExit(10000); } catch { }
            }
        }
        return problems == 0 ? 0 : 1;
    }

    static void Props(IModelDoc2 m, string cfg, string indent)
    {
        foreach (var level in new[] { "", cfg })
        {
            var pm = m.Extension.CustomPropertyManager[level];
            foreach (var name in new[] { "PartNo", "PartNoStatus" })
            {
                string val, resolved;
                bool wasResolved;
                int r = pm.Get5(name, false, out val, out resolved, out wasResolved);
                if (r != (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent)
                    Console.WriteLine(indent + "  " + name + " (" + (level == "" ? "file" : "config") + ") = " + resolved);
            }
        }
    }

    // Files a part is built from (inserted or derived parts, skeletons), as SOLIDWORKS stores them.
    // These files are not always loaded, so the stored path is what has to be right.
    static int PartLinks(IModelDoc2 d, string root)
    {
        int problems = 0;
        if (d.ListExternalFileReferencesCount2() == 0) return 0;
        object models, comps, feats, types, status, ents, featComps;
        d.ListExternalFileReferences2(out models, out comps, out feats, out types, out status, out ents, out featComps);
        var m = models as string[];
        var f = feats as string[];
        if (m == null) return 0;
        foreach (var p in m.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            bool outside = !p.StartsWith(root, StringComparison.OrdinalIgnoreCase);
            bool exists = File.Exists(p);
            if (outside || !exists) problems++;
            Console.WriteLine("      part link -> " + Rel(p, root) + (outside ? "  ** OUTSIDE ROOT **" : "") + (exists ? "" : "  ** FILE NOT FOUND **"));
        }
        return problems;
    }

    // Equations: a linked equations file is a problem; the equations themselves are listed for reading.
    // EvaluateAll's return isn't a usable error signal (it gives -1 on healthy Toolbox parts too), so it isn't counted.
    static int Equations(IModelDoc2 d)
    {
        var em = d.GetEquationMgr() as IEquationMgr;
        if (em == null) return 0;
        int problems = 0;
        if (em.LinkToFile || !string.IsNullOrEmpty(em.FilePath))
        {
            problems++;
            Console.WriteLine("      ** EQUATIONS LINKED TO A FILE: " + em.FilePath + " (link " + (em.LinkToFile ? "on" : "off") + ") **");
        }
        int n = em.GetCount();
        if (n == 0) return problems;
        Console.WriteLine("      equations: " + n);
        for (int i = 0; i < n; i++)
        {
            string eq = em.Equation[i];
            bool global = em.GlobalVariable[i];
            Console.WriteLine("        " + (global ? "[global] " : "") + eq);
        }
        return problems;
    }

    static string Flags(Type e, int v)
    {
        if (v == 0) return "none";
        var names = Enum.GetValues(e).Cast<int>().Where(x => x != 0 && (v & x) == x).Select(x => Enum.GetName(e, x).Split('_').Last());
        return v + " (" + string.Join(", ", names) + ")";
    }

    static string Rel(string p, string root)
    {
        return p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? p.Substring(root.Length) : p;
    }
}
