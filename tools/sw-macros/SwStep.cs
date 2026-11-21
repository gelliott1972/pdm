// SwStep: export one assembly (or part) configuration to STEP, read-only (Grant via Admin, 21 Nov 2026: LS-08A-11 for the
// wireframe). Opens the file read-only and resolved, shows the configuration, reports rebuild errors and components that
// are missing or suppressed, then saves as STEP. The STEP protocol (AP203 or AP214) is SOLIDWORKS' system export setting:
// if it is not the one asked for, it is set for this export only and put back afterwards. Never saves the model.
// Usage: SwStep.exe <model> <config> <out.step> [--ap 203|214]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwStep.exe SwStep.cs

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwStep
{
    static int Main(string[] a)
    {
        var args = a.ToList();
        int ap = 203;
        int ai = args.IndexOf("--ap");
        if (ai >= 0) { ap = int.Parse(args[ai + 1]); args.RemoveRange(ai, 2); }
        if (args.Count != 3) { Console.Error.WriteLine("Usage: SwStep <model> <config> <out.step> [--ap 203|214]"); return 2; }
        string file = Path.GetFullPath(args[0]), config = args[1], outFile = Path.GetFullPath(args[2]);
        if (File.Exists(outFile)) { Console.Error.WriteLine("REFUSED: " + outFile + " exists"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        int e = 0, w = 0;
        var t0 = DateTime.Now;
        var doc = sw.OpenDoc6(file, type, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly |
            swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight), config, ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open, errors " + e); return 3; }
        Console.WriteLine("OPENED\t" + file + "\tconfiguration " + doc.ConfigurationManager.ActiveConfiguration.Name + "\topen errors " + e + ", warnings " + w + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
        int oldAp = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
        try
        {
            if (type == (int)swDocumentTypes_e.swDocASSEMBLY)
            {
                var asm = (AssemblyDoc)doc;
                asm.ResolveAllLightWeightComponents(false);
                var comps = ((object[])asm.GetComponents(false) ?? new object[0]).Cast<Component2>().ToList();
                int supp = comps.Count(c => c.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed);
                var missing = comps.Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && c.GetModelDoc2() == null).ToList();
                Console.WriteLine("COMPONENTS\t" + comps.Count + " in all\t" + supp + " suppressed\t" + missing.Count + " missing (not loaded)");
                foreach (var m in missing) Console.WriteLine("MISSING\t" + m.Name2 + "\t" + m.GetPathName());
                foreach (var g in comps.Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed)
                                       .GroupBy(c => Path.GetFileName(c.GetPathName() ?? "")).OrderBy(g => g.Key))
                    Console.WriteLine("USES\t" + g.Key + "\t" + g.Count());
            }
            object feats, codes, warns;
            doc.Extension.GetWhatsWrong(out feats, out codes, out warns);
            var wf = warns as bool[]; var fs = feats as object[];
            int errs = 0, wrns = 0;
            if (fs != null) for (int i = 0; i < fs.Length; i++) { if (wf != null && i < wf.Length && wf[i]) wrns++; else errs++; }
            Console.WriteLine("WHATSWRONG\t" + errs + " errors\t" + wrns + " warnings");
            if (fs != null) for (int i = 0; i < fs.Length && i < 30; i++) Console.WriteLine("  " + ((wf != null && i < wf.Length && wf[i]) ? "warning" : "error") + "\t" + ((Feature)fs[i]).Name);
            if (oldAp != ap) sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, ap);
            Console.WriteLine("STEP\tprotocol AP" + sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP) + (oldAp != ap ? " (was AP" + oldAp + ", put back after)" : " (already set)"));
            t0 = DateTime.Now;
            bool ok = doc.Extension.SaveAs3(outFile, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e, ref w);
            Console.WriteLine((ok && File.Exists(outFile) ? "SAVED\t" + outFile + "\t" + new FileInfo(outFile).Length / 1048576 + " MB" : "FAILED\terrors " + e) + "\twarnings " + w + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
            return ok ? 0 : 1;
        }
        finally
        {
            if (oldAp != sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP))
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, oldAp);
            sw.CloseDoc(doc.GetTitle());
        }
    }
}
