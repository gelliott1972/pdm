// SwPlain (SW, 24 Nov 2026, APEX-A69 tests for Revit): small files of fastener bodies for Revit's STEP import test.
// Reads parts read-only (P:\ or copies under C:\PDM-work\) and never saves them (feature suppression is in memory only);
// writes only new files under C:\PDM-work\: <out>.SLDPRT, <out>.STEP (AP203) and <out>.SAT (ACIS 7.0).
//   SwPlain <out base under C:\PDM-work\> [dry] <item> ...
//   item = <part>|<config>|<12 numbers: rotation row by row, then x y z in mm>|<mode>|<label>
//   mode = asis | suppress:<feature>,<feature>... | anchor-simple
// anchor-simple: the HST3-R M12 anchor as plain shapes (cylinders, cones, a hexagonal prism) on the part's X axis, with
// the same overall length, washer bearing face (x 0) and tip (x -157.5) as the Hilti body (measured 24 Nov); the part's
// own body is not used.
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
    static ISldWorks sw;
    static MathUtility mu;
    static Modeler modeler;
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

    static string Edges(Body2 b)
    {
        // short edges (Revit's short-curve tolerance is about 0.8 mm; 1/32 inch) and the smallest face
        var lens = new List<double>();
        foreach (Edge ed in (object[])b.GetEdges() ?? new object[0])
        {
            var cp = (CurveParamData)ed.GetCurveParams3();
            lens.Add(((Curve)ed.GetCurve()).GetLength3(cp.UMinValue, cp.UMaxValue));
        }
        lens.Sort();
        return "edges " + lens.Count + ", shortest " + string.Join(" ", lens.Take(3).Select(Mm)) + " mm, under 0.8 mm: " + lens.Count(l => l < 0.0008)
            + ", smallest face " + Mm(((object[])b.GetFaces()).Cast<Face2>().Min(f => f.GetArea()) * 1000) + " mm2"
            + ", volume " + (((double[])b.GetMassProperties(1.0))[3] * 1e9).ToString("0.0", Inv) + " mm3";
    }

    static void Move(Body2 b, double[] d)
    {
        b.ApplyTransform((MathTransform)mu.CreateTransform(new double[] { 1, 0, 0, 0, 1, 0, 0, 0, 1, d[0], d[1], d[2], 1, 0, 0, 0 }));
    }

    // slide a body along dir so its lowest point along dir is at lo (metres)
    static void Align(Body2 b, double[] dir, double lo)
    {
        double x, y, z;
        b.GetExtremePoint(-dir[0], -dir[1], -dir[2], out x, out y, out z);
        double s = x * dir[0] + y * dir[1] + z * dir[2];
        Move(b, new[] { dir[0] * (lo - s), dir[1] * (lo - s), dir[2] * (lo - s) });
    }

    static double EndRadius(Body2 b, double x)
    {
        // radius of the flat end face at x (from its area), on the X axis
        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
        {
            var s = (Surface)f.GetSurface(); if (!s.IsPlane()) continue;
            var bx = (double[])f.GetBox();
            if (Math.Abs(bx[0] - x) < 1e-6 && Math.Abs(bx[3] - x) < 1e-6) return Math.Sqrt(f.GetArea() / Math.PI);
        }
        return -1;
    }

    static readonly double[] X = { 1, 0, 0 };
    static Body2 Cyl(double x0, double x1, double r)
    {
        var b = (Body2)modeler.CreateBodyFromCyl(new[] { x0, 0, 0, 1, 0, 0, r, x1 - x0 });
        Align(b, X, x0); return b;
    }
    static Body2 Cone(double x0, double x1, double r0, double r1)
    {
        var b = (Body2)modeler.CreateBodyFromCone(new[] { x0, 0, 0, 1, 0, 0, r0, r1, x1 - x0 });
        Align(b, X, x0);
        if (Math.Abs(EndRadius(b, x0) - r0) > 1e-6)
        {
            b = (Body2)modeler.CreateBodyFromCone(new[] { x0, 0, 0, 1, 0, 0, r1, r0, x1 - x0 });
            Align(b, X, x0);
        }
        if (Math.Abs(EndRadius(b, x0) - r0) > 1e-6 || Math.Abs(EndRadius(b, x1) - r1) > 1e-6) throw new Exception("cone radii not as asked");
        return b;
    }
    static Body2 Hex(double x0, double x1, double af)
    {
        // three slabs across the flats, 60 degrees apart around X, cut to length by a cylinder beyond the corners
        Body2 h = Cyl(x0, x1, af / Math.Sqrt(3) + 0.001);
        for (int i = 0; i < 3; i++)
        {
            double t = i * Math.PI / 3;
            double[] n = { 0, Math.Cos(t), Math.Sin(t) };
            var s = (Body2)modeler.CreateBodyFromBox3(new[] { (x0 + x1) / 2, 0, 0, n[0], n[1], n[2], 1.0, 1.0, af });
            Align(s, n, -af / 2);
            int err;
            var res = h.Operations2((int)swBodyOperationType_e.SWBODYINTERSECT, s, out err) as object[];
            if (res == null || res.Length != 1) throw new Exception("hexagon: intersect gave " + (res == null ? "nothing" : res.Length.ToString()) + " (" + err + ")");
            h = (Body2)res[0];
        }
        return h;
    }

    static Body2 AnchorSimple()
    {
        // HST3-R M12 (x along the anchor; tip at -157.5, washer bearing face at 0, top at 27.5), mm
        double m = 0.001;
        var parts = new List<Body2> {
            Cyl(-157.5 * m, -153.222 * m, 5.96 * m),               // tip
            Cone(-153.222 * m, -145.506 * m, 5.96 * m, 4.11 * m),  // expansion cone
            Cyl(-147.703 * m, -127.7 * m, 5.81 * m),               // sleeve, plain
            Cyl(-127.7 * m, -125.097 * m, 5.95 * m),               // collar
            Cyl(-125.097 * m, -104.5 * m, 5.325 * m),              // neck
            Cyl(-104.5 * m, 20.516 * m, 6 * m),                    // threaded shank, plain
            Cyl(0, 2.5 * m, 12 * m),                               // washer
            Hex(2.5 * m, 12.5 * m, 19 * m),                        // nut, AF 19
            Cone(20.516 * m, 24.853 * m, 6 * m, 4.55 * m),         // end chamfer
            Cyl(24.853 * m, 27.5 * m, 4.55 * m) };                 // end
        var b = parts[0];
        foreach (var q in parts.Skip(1))
        {
            int err;
            var res = b.Operations2((int)swBodyOperationType_e.SWBODYADD, q, out err) as object[];
            if (res == null || res.Length != 1) throw new Exception("anchor: union gave " + (res == null ? "nothing" : res.Length.ToString()) + " (" + err + ")");
            b = (Body2)res[0];
        }
        return b;
    }

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Usage: SwPlain <out base> [dry] <part>|<config>|<12 numbers>|<mode>|<label> ..."); return 2; }
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
        sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        mu = (MathUtility)sw.GetMathUtility();
        modeler = (Modeler)sw.GetModeler();
        var bodies = new List<Tuple<Body2, string>>();
        int rc = 0;
        foreach (var it in items)
        {
            var p = it.Split('|');
            string file = Path.GetFullPath(p[0]), cfg = p[1], mode = p[3], label = p[4];
            var xf = p[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Select(v => double.Parse(v, Inv)).ToArray();
            var t = (MathTransform)mu.CreateTransform(new double[] { xf[0], xf[1], xf[2], xf[3], xf[4], xf[5], xf[6], xf[7], xf[8], xf[9] / 1000, xf[10] / 1000, xf[11] / 1000, 1, 0, 0, 0 });
            var made = new List<Body2>();
            if (mode == "anchor-simple") made.Add(AnchorSimple());
            else
            {
                int e = 0, w = 0;
                var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), cfg, ref e, ref w);
                if (doc == null) { Console.WriteLine("FAILED\tcould not open " + file + " (" + e + ")"); return 3; }
                try
                {
                    doc.ShowConfiguration2(cfg); doc.ForceRebuild3(false);
                    if (mode.StartsWith("suppress:"))
                        foreach (var fn in mode.Substring(9).Split(','))
                        {
                            Feature ft = null;
                            for (var g = (Feature)doc.FirstFeature(); g != null; g = (Feature)g.GetNextFeature()) if (g.Name == fn) ft = g;
                            if (ft == null) throw new Exception("no feature " + fn + " in " + file);
                            ft.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                        }
                    doc.ForceRebuild3(false);
                    foreach (Body2 b0 in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]) made.Add((Body2)b0.Copy());
                }
                finally { sw.CloseDoc(doc.GetTitle()); }   // read-only, never saved: the suppression goes with it
            }
            foreach (var b in made)
            {
                b.ApplyTransform(t);
                var bb = (double[])b.GetBodyBox();
                Console.WriteLine("BODY\t" + label + "\t" + (mode == "anchor-simple" ? "plain shapes (not from the part)" : Path.GetFileNameWithoutExtension(file) + " <" + cfg + "> " + mode) + "\tfaces: " + Kinds(b) + "\t" + Edges(b)
                    + "\tbox " + Mm(bb[0]) + ".." + Mm(bb[3]) + ", " + Mm(bb[1]) + ".." + Mm(bb[4]) + ", " + Mm(bb[2]) + ".." + Mm(bb[5]));
                bodies.Add(Tuple.Create(b, label));
            }
        }
        if (dry || rc != 0) { Console.WriteLine(dry ? "DRY\tnothing written" : "NOT WRITTEN"); return rc; }
        string tpl = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart);
        var nd = (ModelDoc2)sw.NewDocument(tpl, 0, 0, 0);
        if (nd == null) { Console.WriteLine("FAILED\tno new part (template " + tpl + ")"); return 3; }
        int e2 = 0, w2 = 0;
        try
        {
            foreach (var bt in bodies)
            {
                var ft = (Feature)((PartDoc)nd).CreateFeatureFromBody3(bt.Item1, false, 0);
                if (ft == null) { Console.WriteLine("PROBLEM\tcould not add " + bt.Item2); rc = 1; continue; }
                ft.Name = bt.Item2;
            }
            nd.ForceRebuild3(false);
            // the solid bodies carry the labels, so the STEP names say which is which
            var nb = ((object[])((PartDoc)nd).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Cast<Body2>().ToList();
            foreach (var b in nb) { var fs = (object[])b.GetFeatures(); if (fs != null && fs.Length > 0) b.Name = ((Feature)fs[0]).Name; }
            Console.WriteLine("BODIES IN THE FILE\t" + string.Join(", ", nb.Select(b => b.Name)));
            int oldAp = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
            int oldAcis = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputVersion);
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, 203);
            sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputVersion, (int)swAcisOutputVersion_e.swAcisOutputVersion_70);
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
            finally
            {
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, oldAp);
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swAcisOutputVersion, oldAcis);
            }
        }
        finally { sw.CloseDoc(nd.GetTitle()); }
        return rc;
    }
}
