// SwResave: open each SOLIDWORKS file, rebuild every configuration, and save it, so the component names stored
// inside update after DmEdit replace-ref (0811 D14: 17 subframes). Work copies only: refuses P:\ and Dropbox paths.
// Usage: SwResave.exe <file> [<file> ...]
// Closes everything between files, so a same-named file from another work copy can't stand in.
// Refuses to run while SOLIDWORKS is already open. Build:
//   csc /platform:x64 /r:..\SwCheck\bin\SolidWorks.Interop.sldworks.dll /r:..\SwCheck\bin\SolidWorks.Interop.swconst.dll SwResave.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwResave
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: SwResave <file> [<file> ...]"); return 2; }
        foreach (var a in args)
        {
            string p = Path.GetFullPath(a);
            if (p.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0
                || p.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("Refusing vault or Dropbox path: " + p); return 2; }
        }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started.");
        int problems = 0;
        try
        {
            foreach (var a in args)
            {
                string path = Path.GetFullPath(a);
                bool asm = path.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);
                int errs = 0, warns = 0;
                var doc = sw.OpenDoc6(path, (int)(asm ? swDocumentTypes_e.swDocASSEMBLY : swDocumentTypes_e.swDocPART),
                    (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errs, ref warns);
                Console.Write(Path.GetFileName(path) + ": open errors=" + errs + " warnings=" + warns);
                if (doc == null || errs != 0) { Console.WriteLine("  ** NOT SAVED **"); problems++; sw.CloseAllDocuments(true); continue; }
                string first = ((IConfiguration)doc.GetActiveConfiguration()).Name;
                foreach (string cn in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(cn);
                    if (asm) ((IAssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                    doc.ForceRebuild3(false);
                }
                doc.ShowConfiguration2(first);
                int serr = 0, swarn = 0;
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref serr, ref swarn);
                Console.WriteLine("  rebuilt " + ((string[])doc.GetConfigurationNames()).Length + " config(s), save " + (ok ? "ok" : "FAILED") + " (errors=" + serr + " warnings=" + swarn + ")");
                if (!ok) problems++;
                sw.CloseAllDocuments(true);
            }
        }
        finally
        {
            try { sw.CloseAllDocuments(true); sw.ExitApp(); } catch { }
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw);
            if (started != null && !started.WaitForExit(30000))
                try { started.Kill(); started.WaitForExit(10000); } catch { }
        }
        Console.WriteLine(problems == 0 ? "RESULT: all saved" : "RESULT: " + problems + " problem(s)");
        return problems == 0 ? 0 : 1;
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
