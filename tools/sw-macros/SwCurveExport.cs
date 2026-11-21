// SwCurveExport: read-only test (apex-admin / Grant overnight 22 Nov 2026): export one part to STEP AP214, STEP AP242 and
// SAT with 3D curves / sketch entities switched on, once with its sketches as saved and once with every sketch shown
// (in memory only), to see whether weldment centreline sketches come through and with what names. The part is opened
// read-only and never saved; the export options are put back afterwards.
// Usage: SwCurveExport.exe <part> <out folder>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwCurveExport.exe SwCurveExport.cs

using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwCurveExport
{
    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]), outDir = Path.GetFullPath(a[1]);
        Directory.CreateDirectory(outDir);
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open " + e); return 3; }
        int oldAp = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
        bool old3d = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures);
        bool oldIgesSk = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSketchEntities);
        int oldAcis = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputGeometryPreference);
        string name = Path.GetFileNameWithoutExtension(file);
        try
        {
            sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures, true);
            sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSketchEntities, true);
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputGeometryPreference, (int)swAcisOutputGeometryPreference_e.swAcisOutputAs3DCurves_IncludeSketchEnts);
            foreach (var shown in new[] { false, true })
            {
                if (shown)
                    for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                        if (f.GetTypeName2() == "ProfileFeature" || f.GetTypeName2() == "3DProfileFeature")
                        { doc.ClearSelection2(true); f.Select2(false, -1); doc.UnblankSketch(); }
                doc.ClearSelection2(true);
                string tag = shown ? "sketches-shown" : "as-saved";
                foreach (var ap in new[] { 214, 242 })
                {
                    sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, ap);
                    string o = Path.Combine(outDir, name + " AP" + ap + " " + tag + ".STEP");
                    bool ok = doc.Extension.SaveAs3(o, 0, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e, ref w);
                    Console.WriteLine((ok ? "SAVED\t" : "FAILED\t") + o + "\terrors " + e + " warnings " + w);
                }
                string sat = Path.Combine(outDir, name + " " + tag + ".SAT");
                bool ok2 = doc.Extension.SaveAs3(sat, 0, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e, ref w);
                Console.WriteLine((ok2 ? "SAVED\t" : "FAILED\t") + sat + "\terrors " + e + " warnings " + w);
            }
        }
        finally
        {
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, oldAp);
            sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures, old3d);
            sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swIGESExportSketchEntities, oldIgesSk);
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputGeometryPreference, oldAcis);
            Console.WriteLine("OPTIONS PUT BACK\tAP" + sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP) + "\t3D curves " + sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swStepExport3DCurveFeatures) + "\tACIS " + sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputGeometryPreference));
            sw.CloseDoc(doc.GetTitle());
        }
        return 0;
    }
}
