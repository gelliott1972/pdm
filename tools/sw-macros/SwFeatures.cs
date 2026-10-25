// SwFeatures: open SOLIDWORKS parts read-only and list, for each configuration, every feature with its type,
// whether it is suppressed, and the values of its dimensions. Never saves. Used to find what differs between two
// copies of a part that should be the same (0811 D28: the 4-size interface plate vs the vault's 08XXX-PLP).
// Usage: SwFeatures.exe <file> [<file> ...]
// Refuses to run while SOLIDWORKS is already open. Build:
//   csc /platform:x64 /r:..\SwCheck\bin\SolidWorks.Interop.sldworks.dll /r:..\SwCheck\bin\SolidWorks.Interop.swconst.dll SwFeatures.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwFeatures
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: SwFeatures <file> [<file> ...]"); return 2; }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started.");
        try
        {
            foreach (var a in args)
            {
                string path = Path.GetFullPath(a);
                int errs = 0, warns = 0;
                var doc = sw.OpenDoc6(path, (int)swDocumentTypes_e.swDocPART,
                    (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
                Console.WriteLine();
                Console.WriteLine(Path.GetFileName(path) + "  (open errors=" + Flags(typeof(swFileLoadError_e), errs) + " warnings=" + Flags(typeof(swFileLoadWarning_e), warns) + ")");
                if (doc == null) continue;
                foreach (string cn in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(cn);
                    doc.ForceRebuild3(false);
                    Console.WriteLine("  [" + cn + "]");
                    for (var f = (IFeature)doc.FirstFeature(); f != null; f = (IFeature)f.GetNextFeature())
                    {
                        string type = f.GetTypeName2();
                        bool supp = ((bool[])f.IsSuppressed2((int)swInConfigurationOpts_e.swThisConfiguration, null))[0];
                        string dims = "";
                        for (var dd = (IDisplayDimension)f.GetFirstDisplayDimension(); dd != null; dd = (IDisplayDimension)f.GetNextDisplayDimension(dd))
                        {
                            var dim = dd.GetDimension2(0);
                            dims += " " + dim.Name + "=" + Math.Round(dim.SystemValue * (dim.GetType() == (int)swDimensionParamType_e.swDimensionParamTypeDoubleAngular ? 180 / Math.PI : 1000), 3);
                        }
                        Console.WriteLine("    " + f.Name + " (" + type + ")" + (supp ? " SUPPRESSED" : "") + dims);
                    }
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

    static string Flags(Type e, int v)
    {
        if (v == 0) return "none";
        return string.Join("|", Enum.GetValues(e).Cast<int>().Where(x => x != 0 && (v & x) == x).Select(x => Enum.GetName(e, x)).Distinct()) + " (" + v + ")";
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
