// SwMkPart: makes the two new joint 8.2 parts (APEX-A124, weekend run 28 Nov 2026) as plain feature parts, one
// configuration "Default", material Plain Carbon Steel (as GNRIC-BRA and GNRIC-MSW-01), properties at configuration level
// (PartNo, Description, Weight and Material links to its own file, Finish HDG). Writes a NEW file only, only under
// C:\PDM-work\weekend-2026-11-28\. Dimensions from calc-08a-11's brief (joint-8.2-redesign\drawing brief.md, v4).
//   seat: EA75x50x5 (root r1 8, toe r2 4), 45 long, one dia 14 hole 30 from the back of the vertical leg (45 to the toe),
//         on the seat's centre. Part axes: X along the 75 leg (toward the rail), Y up (heel corner at the origin, top of the
//         75 leg at Y 0, the 50 leg hanging down), Z along the length (centred, -22.5..22.5). Back of the 50 leg at X 0.
//   rail: SHS 40x40x4 (outer r 8, inner r 4), tube 438, end caps 34x34x4 corners R3 on both end faces, centred; one dia
//         14 vertical hole through both walls at each end, 19 from the tube end (hole centres 400 apart). Part axes: X
//         along the rail (centred, tube -219..219, caps to -223/223), Y up, Z across; origin on the centreline.
// Usage: SwMkPart.exe seat|rail <out.SLDPRT> <PartNo> <Description>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwMkPart.exe SwMkPart.cs
using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwMkPart
{
    const double mm = 0.001;
    static ModelDoc2 doc;
    static SketchManager skm;

    static void L(double x1, double y1, double x2, double y2) { skm.CreateLine(x1 * mm, y1 * mm, 0, x2 * mm, y2 * mm, 0); }
    static void A(double xc, double yc, double x1, double y1, double x2, double y2, short dir) { skm.CreateArc(xc * mm, yc * mm, 0, x1 * mm, y1 * mm, 0, x2 * mm, y2 * mm, 0, dir); }

    // rounded rectangle centred on the origin, half-sizes h, corner radius r (all in sketch coordinates)
    static void RoundSquare(double h, double r)
    {
        double c = h - r;
        L(-c, h, c, h); A(c, c, c, h, h, c, -1);
        L(h, c, h, -c); A(c, -c, h, -c, c, -h, -1);
        L(c, -h, -c, -h); A(-c, -c, -c, -h, -h, -c, -1);
        L(-h, -c, -h, c); A(-c, c, -h, c, -c, h, -1);
    }

    static bool Sketch(string plane)
    {
        doc.ClearSelection2(true);
        if (!doc.Extension.SelectByID2(plane, "PLANE", 0, 0, 0, false, 0, null, 0)) return false;
        skm.InsertSketch(true);
        skm.AddToDB = true;
        return true;
    }

    static void EndSketch() { skm.AddToDB = false; skm.InsertSketch(true); }

    static Feature Extrude(int end, double depth, bool merge, int t0 = 0, double startOffset = 0, bool flipStart = false, bool flip = false)
    {
        return (Feature)doc.FeatureManager.FeatureExtrusion3(true, false, flip, end, 0, depth * mm, 0, false, false, false, false, 0, 0, false, false,
            false, false, merge, true, true, t0, startOffset * mm, flipStart);
    }

    static Feature CutThroughBoth()
    {
        return (Feature)doc.FeatureManager.FeatureCut4(false, false, false, (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondThroughAll, 0.01, 0.01, false, false, false, false,
            0, 0, false, false, false, false, false, true, true, true, true, false, 0, 0, false, false);
    }

    static int Main(string[] a)
    {
        if (a.Length != 4 || (a[0] != "seat" && a[0] != "rail")) { Console.Error.WriteLine("Usage: SwMkPart seat|rail <out.SLDPRT> <PartNo> <Description>"); return 2; }
        string outp = Path.GetFullPath(a[1]);
        if (!outp.StartsWith(@"C:\PDM-work\weekend-2026-11-28\", StringComparison.OrdinalIgnoreCase) || File.Exists(outp) || !outp.EndsWith(".SLDPRT", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED: a new .SLDPRT under C:\\PDM-work\\weekend-2026-11-28 only: " + outp); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        string tpl = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        doc = (ModelDoc2)sw.NewDocument(tpl, 0, 0, 0);
        if (doc == null) { Console.Error.WriteLine("FAILED: no new part from " + tpl); return 3; }
        skm = doc.SketchManager;
        int rc = 0;
        try
        {
            if (a[0] == "seat")
            {
                if (!Sketch("Front Plane")) throw new Exception("no Front Plane");
                L(0, 0, 75, 0); L(75, 0, 75, -1); A(71, -1, 75, -1, 71, -5, -1); L(71, -5, 13, -5); A(13, -13, 13, -5, 5, -13, 1);
                L(5, -13, 5, -46); A(1, -46, 5, -46, 1, -50, -1); L(1, -50, 0, -50); L(0, -50, 0, 0);
                EndSketch();
                if (Extrude((int)swEndConditions_e.swEndCondMidPlane, 45, true) == null) throw new Exception("extrude failed");
                if (!Sketch("Top Plane")) throw new Exception("no Top Plane");
                skm.CreateCircleByRadius(30 * mm, 0, 0, 7 * mm);
                EndSketch();
                if (CutThroughBoth() == null) throw new Exception("hole cut failed");
            }
            else
            {
                if (!Sketch("Right Plane")) throw new Exception("no Right Plane");
                RoundSquare(20, 8); RoundSquare(16, 4);
                EndSketch();
                if (Extrude((int)swEndConditions_e.swEndCondMidPlane, 438, false) == null) throw new Exception("tube extrude failed");
                for (int s = 0; s < 2; s++)
                {
                    if (!Sketch("Right Plane")) throw new Exception("no Right Plane");
                    RoundSquare(17, 3);
                    EndSketch();
                    // start 219 from the Right Plane, 4 deep, away from the centre: +X for the first, -X for the second
                    if (Extrude((int)swEndConditions_e.swEndCondBlind, 4, false, (int)swStartConditions_e.swStartOffset, 219, s == 1, s == 1) == null) throw new Exception("cap extrude failed");
                }
                if (!Sketch("Top Plane")) throw new Exception("no Top Plane");
                skm.CreateCircleByRadius(200 * mm, 0, 0, 7 * mm);
                skm.CreateCircleByRadius(-200 * mm, 0, 0, 7 * mm);
                EndSketch();
                if (CutThroughBoth() == null) throw new Exception("hole cut failed");
            }
            ((PartDoc)doc).SetMaterialPropertyName2("", "SOLIDWORKS Materials", "Plain Carbon Steel");
            doc.ForceRebuild3(false);
            int e = 0, w = 0;
            bool ok = doc.Extension.SaveAs3(outp, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref e, ref w);
            if (!ok) throw new Exception("save failed " + e);
            string cfg = ((Configuration)doc.GetActiveConfiguration()).Name, fn = Path.GetFileName(outp);
            var pm = doc.Extension.CustomPropertyManager[cfg];
            pm.Add3("PartNo", (int)swCustomInfoType_e.swCustomInfoText, a[2], (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            pm.Add3("Description", (int)swCustomInfoType_e.swCustomInfoText, a[3], (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            pm.Add3("Weight", (int)swCustomInfoType_e.swCustomInfoText, "\"SW-Mass@@" + cfg + "@" + fn + "\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            pm.Add3("Material", (int)swCustomInfoType_e.swCustomInfoText, "\"SW-Material@@" + cfg + "@" + fn + "\"", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            pm.Add3("Finish", (int)swCustomInfoType_e.swCustomInfoText, "HDG", (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
            var mp = doc.Extension.CreateMassProperty();
            var bodies = (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true);
            var bx = (double[])((PartDoc)doc).GetPartBox(true);
            Console.WriteLine("MADE\t" + a[0] + "\t" + outp + "\tconfig " + cfg + "\tbodies " + (bodies == null ? 0 : bodies.Length) + "\tmass kg " + mp.Mass.ToString("0.0000") +
                "\tvolume mm3 " + (mp.Volume * 1e9).ToString("0.0") + "\tbox " + string.Join(" ", Array.ConvertAll(bx, v => (v * 1000).ToString("0.###"))) + "\terrors " + (doc.Extension.GetWhatsWrongCount()) + "\t" + (ok ? "SAVED" : "SAVE FAILED"));
        }
        catch (Exception ex) { Console.WriteLine("FAILED\t" + ex.Message); rc = 3; }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return rc;
    }
}
