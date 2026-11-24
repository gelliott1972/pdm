// SwPlain (SW, 24 Nov 2026, APEX-A69 test for Revit): one small file of fastener bodies with plain faces, placed in a top
// assembly's axes. Reads parts read-only (P:\ or copies under C:\PDM-work\), never saves them; writes only new files under
// C:\PDM-work\. For each item: the part's solid bodies in that configuration, optionally with the thread zone made plain,
// moved by the given transform; all of them into one new part, saved as <out>.SLDPRT, <out>.STEP (AP203) and <out>.SAT.
//   SwPlain <out base under C:\PDM-work\> [dry] <part>|<config>|<12 numbers: rotation row by row, then x y z in mm>|<plain|asis> ...
// "plain" = every torus face is thread: a cylinder of the thread's outer radius over the thread's length is united with the
// body, so the thread rings end up inside it and only the plain cylinder is left (outer size unchanged).
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwPlain.exe SwPlain.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwPlain
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }

    static string Kinds(Body2 b)
    {
        var k = new Dictionary<string, int>();
        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
        {
            var s = (Surface)f.GetSurface();
            string n = s.IsPlane() ? "plane" : s.IsCylinder() ? "cylinder" : s.IsCone() ? "cone" : s.IsTorus() ? "torus" : s.IsSphere() ? "sphere" : "other";
            k[n] = k.ContainsKey(n) ? k[n] + 1 : 1;
        }
        return string.Join(", ", k.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value));
    }

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Usage: SwPlain <out base> [dry] <part>|<config>|<12 numbers>|<plain|asis> ..."); return 2; }
        string outBase = Path.GetFullPath(a[0]);
        if (!outBase.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under C:\\PDM-work"); return 2; }
        bool dry = a[1] == "dry";
        var items = a.Skip(dry ? 2 : 1).ToArray();
        foreach (var it in items)
        {
            string f = Path.GetFullPath(it.Split('|')[0]);
            if (f.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || f.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || f.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0
                || !(f.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || f.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)))
            { Console.Error.WriteLine("REFUSED: parts only from P:\\ or C:\\PDM-work: " + f); return 2; }
        }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        var mu = (MathUtility)sw.GetMathUtility();
        var modeler = (Modeler)sw.GetModeler();
        var bodies = new List<Body2>();
        int rc = 0;
        foreach (var it in items)
        {
            var p = it.Split('|');
            string file = Path.GetFullPath(p[0]), cfg = p[1];
            var xf = p[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, Inv)).ToArray();
            bool plain = p[3] == "plain";
            int e = 0, w = 0;
            var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), cfg, ref e, ref w);
            if (doc == null) { Console.WriteLine("FAILED\tcould not open " + file + " (" + e + ")"); return 3; }
            try
            {
                doc.ShowConfiguration2(cfg); doc.ForceRebuild3(false);
                foreach (Body2 b0 in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                {
                    var b = (Body2)b0.Copy();
                    Console.WriteLine("BODY\t" + Path.GetFileNameWithoutExtension(file) + " <" + cfg + ">\tfaces before: " + Kinds(b));
                    {
                        // short edges (Revit's short-curve tolerance is about 0.8 mm; 1/32 inch)
                        var lens = new List<double>();
                        foreach (Edge ed in (object[])b.GetEdges() ?? new object[0])
                        {
                            var cp = (CurveParamData)ed.GetCurveParams3();
                            lens.Add(((Curve)ed.GetCurve()).GetLength3(cp.UMinValue, cp.UMaxValue));
                        }
                        lens.Sort();
                        Console.WriteLine("  edges " + lens.Count + "\tshortest " + string.Join(", ", lens.Take(5).Select(Mm)) + " mm\tunder 0.8 mm: " + lens.Count(l => l < 0.0008) + "\tsmallest face " + Mm(Math.Sqrt(((object[])b.GetFaces()).Cast<Face2>().Min(f => f.GetArea()))) + " mm (square root of area)");
                    }
                    if (plain)
                    {
                        // the thread zone: the torus faces' extent; their axis is the part's long axis (box's longest side)
                        var tor = ((object[])b.GetFaces() ?? new object[0]).Cast<Face2>().Where(f => ((Surface)f.GetSurface()).IsTorus()).ToList();
                        if (tor.Count == 0) { Console.WriteLine("  no torus faces: nothing to make plain"); }
                        else
                        {
                            var tp = (double[])((Surface)tor[0].GetSurface()).TorusParams;   // centre xyz, axis xyz, major, minor
                            double[] ax = { tp[3], tp[4], tp[5] }, c0 = { tp[0], tp[1], tp[2] };
                            double sLo = double.MaxValue, sHi = double.MinValue, rMax = 0;
                            foreach (var f in tor)
                            {
                                var q = (double[])((Surface)f.GetSurface()).TorusParams;
                                double s = (q[0] - c0[0]) * ax[0] + (q[1] - c0[1]) * ax[1] + (q[2] - c0[2]) * ax[2];
                                sLo = Math.Min(sLo, s - q[7]); sHi = Math.Max(sHi, s + q[7]); rMax = Math.Max(rMax, q[6] + q[7]);
                            }
                            // the outer radius of the thread = the largest radius any face reaches inside the zone: torus outer
                            // edges, and the cylinders and cones between the rings
                            foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                            {
                                var su = (Surface)f.GetSurface();
                                if (!su.IsCylinder()) continue;
                                var cp = (double[])su.CylinderParams;
                                double s = (cp[0] - c0[0]) * ax[0] + (cp[1] - c0[1]) * ax[1] + (cp[2] - c0[2]) * ax[2];
                                var bx = (double[])f.GetBox();
                                double lo = Math.Min(Math.Min((bx[0] - c0[0]) * ax[0] + (bx[1] - c0[1]) * ax[1] + (bx[2] - c0[2]) * ax[2], (bx[3] - c0[0]) * ax[0] + (bx[4] - c0[1]) * ax[1] + (bx[5] - c0[2]) * ax[2]), s);
                                double hi = Math.Max((bx[0] - c0[0]) * ax[0] + (bx[1] - c0[1]) * ax[1] + (bx[2] - c0[2]) * ax[2], (bx[3] - c0[0]) * ax[0] + (bx[4] - c0[1]) * ax[1] + (bx[5] - c0[2]) * ax[2]);
                                if (lo >= sLo - 1e-6 && hi <= sHi + 1e-6) rMax = Math.Max(rMax, cp[6]);
                            }
                            if (dry)
                                foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                                {
                                    var su = (Surface)f.GetSurface(); var bx = (double[])f.GetBox();
                                    string k = su.IsTorus() ? "torus major " + Mm(((double[])su.TorusParams)[6]) + " minor " + Mm(((double[])su.TorusParams)[7])
                                        : su.IsCylinder() ? "cylinder r " + Mm(((double[])su.CylinderParams)[6]) : su.IsCone() ? "cone" : su.IsPlane() ? "plane" : "other";
                                    Console.WriteLine("    FACE\t" + k + "\tx " + Mm(bx[0]) + ".." + Mm(bx[3]) + "\tr<= " + Mm(Math.Max(Math.Max(Math.Abs(bx[1]), Math.Abs(bx[4])), Math.Max(Math.Abs(bx[2]), Math.Abs(bx[5])))));
                                }
                            Console.WriteLine("  thread zone: " + tor.Count + " torus faces along the axis (" + string.Join(" ", ax.Select(v => Math.Round(v, 3).ToString(Inv))) + ") from " + Mm(sLo) + " to " + Mm(sHi) + " mm (length " + Mm(sHi - sLo) + "), outer diameter " + Mm(2 * rMax));
                            double[] bc = { c0[0] + ax[0] * sLo, c0[1] + ax[1] * sLo, c0[2] + ax[2] * sLo };
                            var cyl = (Body2)modeler.CreateBodyFromCyl(new[] { bc[0] + ax[0] * (sHi - sLo) / 2, bc[1] + ax[1] * (sHi - sLo) / 2, bc[2] + ax[2] * (sHi - sLo) / 2, ax[0], ax[1], ax[2], rMax, sHi - sLo });
                            if (cyl == null) { Console.WriteLine("PROBLEM\tcould not make the cylinder"); rc = 1; }
                            else
                            {
                                int err;
                                var res = b.Operations2((int)swBodyOperationType_e.SWBODYADD, cyl, out err) as object[];
                                if (res == null || res.Length != 1) { Console.WriteLine("PROBLEM\tthe union gave " + (res == null ? "nothing" : res.Length + " bodies") + " (" + err + ")"); rc = 1; }
                                else b = (Body2)res[0];
                            }
                        }
                    }
                    var t = (MathTransform)mu.CreateTransform(new double[] { xf[0], xf[1], xf[2], xf[3], xf[4], xf[5], xf[6], xf[7], xf[8], xf[9] / 1000, xf[10] / 1000, xf[11] / 1000, 1, 0, 0, 0 });
                    b.ApplyTransform(t);
                    var bb = (double[])b.GetBodyBox();
                    Console.WriteLine("  faces after: " + Kinds(b) + "\tbox in the top's axes " + Mm(bb[0]) + ".." + Mm(bb[3]) + ", " + Mm(bb[1]) + ".." + Mm(bb[4]) + ", " + Mm(bb[2]) + ".." + Mm(bb[5]));
                    bodies.Add(b);
                }
            }
            finally { sw.CloseDoc(doc.GetTitle()); }
        }
        if (dry || rc != 0) { Console.WriteLine(dry ? "DRY\tnothing written" : "NOT WRITTEN"); return rc; }
        string tpl = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        var nd = (ModelDoc2)sw.NewDocument(tpl, 0, 0, 0);
        if (nd == null) { Console.WriteLine("FAILED\tno new part (template " + tpl + ")"); return 3; }
        int e2 = 0, w2 = 0;
        try
        {
            foreach (var b in bodies)
                if (((PartDoc)nd).CreateFeatureFromBody3(b, false, 0) == null) { Console.WriteLine("PROBLEM\tcould not add a body"); rc = 1; }
            nd.ForceRebuild3(false);
            int oldAp = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
            if (oldAp != 203) sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, 203);
            try
            {
                foreach (var ext in new[] { ".SLDPRT", ".STEP", ".SAT" })
                {
                    string f = outBase + ext;
                    if (File.Exists(f)) { Console.WriteLine("REFUSED\t" + f + " exists; nothing overwritten"); rc = 1; continue; }
                    bool ok = nd.Extension.SaveAs3(f, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | (ext == ".SLDPRT" ? 0 : swSaveAsOptions_e.swSaveAsOptions_Copy)), null, null, ref e2, ref w2);
                    Console.WriteLine((ok ? "SAVED\t" : "SAVE FAILED\t") + f + (ok ? "\t" + (new FileInfo(f).Length / 1024) + " KB" : "\t" + e2));
                    if (!ok) rc = 1;
                }
            }
            finally { if (oldAp != 203) sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, oldAp); }
        }
        finally { sw.CloseDoc(nd.GetTitle()); }
        return rc;
    }
}
