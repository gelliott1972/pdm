// SwEqLink: open a SOLIDWORKS file read-only and report, for each configuration, whether its equations are
// linked to an outside file (Tools > Equations > Link to external file). Never saves. Used for 0811 D21, where
// SwCheck showed the link on only when the As Welded configuration was active.
// Usage: SwEqLink.exe <file> [<file> ...]
// Refuses to run while SOLIDWORKS is already open. Build:
//   csc /platform:x64 /r:..\SwCheck\bin\SolidWorks.Interop.sldworks.dll /r:..\SwCheck\bin\SolidWorks.Interop.swconst.dll SwEqLink.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwEqLink
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: SwEqLink <file> [<file> ...]"); return 2; }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started.");
        try
        {
            foreach (var a in args)
            {
                string path = Path.GetFullPath(a);
                bool asm = path.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase);
                int errs = 0, warns = 0;
                var doc = sw.OpenDoc6(path, (int)(asm ? swDocumentTypes_e.swDocASSEMBLY : swDocumentTypes_e.swDocPART),
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
                Console.WriteLine();
                Console.WriteLine(Path.GetFileName(path) + "  (open errors=" + errs + " warnings=" + warns + ")");
                if (doc == null) continue;
                Report(doc, "as opened");
                foreach (string cn in (string[])doc.GetConfigurationNames())
                {
                    bool shown = doc.ShowConfiguration2(cn);
                    Report(doc, cn + (shown ? "" : " (could not show)"));
                }
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
        return 0;
    }

    static void Report(IModelDoc2 doc, string label)
    {
        var em = doc.GetEquationMgr() as IEquationMgr;
        string active = ((IConfiguration)doc.GetActiveConfiguration()).Name;
        Console.WriteLine(string.Format("  [{0}] active={1}  link={2}  file={3}  equations={4}", label, active,
            em == null ? "?" : em.LinkToFile ? "on" : "off", em == null ? "" : em.FilePath, em == null ? 0 : em.GetCount()));
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
