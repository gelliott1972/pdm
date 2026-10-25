// SwMeasure: open SOLIDWORKS files read-only and report, per configuration, what they physically are:
// mass, bounding box, and for parts whether it is a weldment and how many solid bodies it has.
// Used to compare files that look alike (0811 D20) and to check P/W letters (0811 D9). Never saves.
// Usage: SwMeasure.exe <file> [<file> ...]
// Closes everything between files, so same-named components in another work copy can't stand in.
// Refuses to run while SOLIDWORKS is already open. Build:
//   csc /platform:x64 /r:..\SwCheck\bin\SolidWorks.Interop.sldworks.dll /r:..\SwCheck\bin\SolidWorks.Interop.swconst.dll SwMeasure.cs

using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwMeasure
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: SwMeasure <file> [<file> ...]"); return 2; }
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
                foreach (string cn in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(cn);
                    if (asm) ((IAssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                    doc.ForceRebuild3(false);
                    var mp = (IMassProperty)doc.Extension.CreateMassProperty();
                    double mass = mp == null ? double.NaN : mp.Mass;
                    double[] box = asm ? (double[])((IAssemblyDoc)doc).GetBox(0) : (double[])((IPartDoc)doc).GetPartBox(true);
                    string size = box == null ? "?" : string.Format("{0:0.0} x {1:0.0} x {2:0.0} mm",
                        (box[3] - box[0]) * 1000, (box[4] - box[1]) * 1000, (box[5] - box[2]) * 1000);
                    string extra = "";
                    if (asm)
                    {
                        var comps = ((IAssemblyDoc)doc).GetComponents(false) as object[];
                        int n = comps == null ? 0 : comps.Cast<Component2>().Count(c => !c.IsSuppressed());
                        extra = "components (unsuppressed, all levels) " + n;
                    }
                    else
                    {
                        var part = (IPartDoc)doc;
                        var bodies = part.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[];
                        extra = "weldment " + (part.IsWeldment() ? "yes" : "no") + ", solid bodies " + (bodies == null ? 0 : bodies.Length);
                    }
                    Console.WriteLine(string.Format("  [{0}] mass {1:0.000} kg, box {2}, {3}", cn, mass, size, extra));
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
