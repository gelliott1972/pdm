// SwNamedView: add a named view to a part, looking square at the largest flat face of one body (Admin, 17 Nov 2026:
// 08X04-BKP-07 sits at an odd angle to the standard views; DW19 needs a view square to the plate). Changes no geometry:
// only the view orientation is stored. The view's right-hand direction runs along the face's longest straight edge.
// Usage: SwNamedView.exe <part> [--body <body name>] [--name <view name>] [--save] [--checked-out]
//   Without --name: a look pass that lists the 8 largest flat faces (body, area, normal, longest edge) and the
//   orientation convention read from *Front/*Top; nothing is changed.
//   With --name: sets the view square to the largest flat face (of --body if given), names it, reads it back.
//   Saves only with --save; a P:\ file needs --checked-out and must be writable. Other files only under C:\code\pdm\scratch.
// Run under SwCmd's lock (--as sw). Refuses Dropbox.
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwNamedView.exe SwNamedView.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwNamedView
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) { return v.ToString("0.####", Inv); }
    static string V(double[] v) { return "(" + F(v[0]) + ", " + F(v[1]) + ", " + F(v[2]) + ")"; }
    static double[] Cross(double[] a, double[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
    static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    static double[] Unit(double[] a) { double l = Math.Sqrt(Dot(a, a)); return new[] { a[0] / l, a[1] / l, a[2] / l }; }

    class Flat { public string Body; public double Area; public double[] Normal; public double[] Edge; public double EdgeLen; }

    static int Main(string[] a)
    {
        var args = a.ToList();
        bool save = args.Remove("--save"), checkedOut = args.Remove("--checked-out");
        string body = Opt(args, "--body"), name = Opt(args, "--name");
        if (args.Count != 1) { Console.Error.WriteLine("Usage: SwNamedView <part> [--body <b>] [--name <view>] [--save] [--checked-out]"); return 2; }
        string file = Path.GetFullPath(args[0]);
        if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith("M:", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED (Dropbox)"); return 2; }
        if (file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED: .pdm"); return 2; }
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (save && name == null) { Console.Error.WriteLine("--save needs --name"); return 2; }
        if (save && vault && (!checkedOut || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0)) { Console.Error.WriteLine("REFUSED: a vault file is written only when checked out (--checked-out, writable)"); return 2; }
        if (save && !vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: saves only to checked-out vault files or scratch"); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        int opts = (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly);
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, opts, "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open, errors " + e); return 3; }
        try
        {
            sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref e);
            var view = (ModelView)doc.ActiveView;
            if (view == null) { Console.Error.WriteLine("no model view"); return 3; }
            var mu = (MathUtility)sw.GetMathUtility();

            // Convention: read *Top and see where the model's +Y (the direction toward the viewer in Top) appears.
            doc.ShowNamedView2("*Top", (int)swStandardViews_e.swTopView);
            var top = (double[])view.Orientation3.ArrayData;
            bool rows = Math.Abs(top[7] - 1) < 1e-6;   // rotation rows are the view axes in model coordinates
            bool cols = Math.Abs(top[5] - 1) < 1e-6;   // columns are
            Console.WriteLine("CONVENTION\t*Top rotation " + string.Join(" ", top.Take(9).Select(F)) + "\t" + (rows ? "rows = view axes" : cols ? "columns = view axes" : "unknown"));
            if (!rows && !cols) { Console.Error.WriteLine("could not read the orientation convention"); return 3; }

            var flats = new List<Flat>();
            foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
            {
                if (body != null && b.Name != body) continue;
                foreach (Face2 f in (object[])b.GetFaces())
                {
                    var s = (Surface)f.GetSurface();
                    if (!s.IsPlane()) continue;
                    var pp = (double[])s.PlaneParams;
                    var n = Unit(new[] { pp[0], pp[1], pp[2] });
                    if (!f.FaceInSurfaceSense()) n = new[] { -n[0], -n[1], -n[2] };
                    double[] best = null; double bestLen = 0;
                    foreach (Edge ed in (object[])f.GetEdges())
                    {
                        var c = (Curve)ed.GetCurve();
                        if (!c.IsLine()) continue;
                        var cp = (CurveParamData)ed.GetCurveParams3();
                        double len = c.GetLength3(cp.UMinValue, cp.UMaxValue);
                        if (len > bestLen) { bestLen = len; var lp = (double[])c.LineParams; best = Unit(new[] { lp[3], lp[4], lp[5] }); }
                    }
                    flats.Add(new Flat { Body = b.Name, Area = f.GetArea(), Normal = n, Edge = best, EdgeLen = bestLen });
                }
            }
            flats = flats.OrderByDescending(x => x.Area).ToList();
            foreach (var x in flats.Take(8))
                Console.WriteLine("FACE\t" + x.Body + "\tarea " + F(x.Area * 1e6) + " mm2\tnormal " + V(x.Normal) + "\tlongest edge " + F(x.EdgeLen * 1000) + " mm along " + (x.Edge == null ? "-" : V(x.Edge)));
            if (name == null) { Console.WriteLine("Look pass: nothing changed."); return 0; }
            if (flats.Count == 0 || flats[0].Edge == null) { Console.Error.WriteLine("no flat face with a straight edge"); return 3; }

            var z = flats[0].Normal;
            var xa = flats[0].Edge;
            // Point the right-hand direction toward the larger positive model component, so the result is repeatable.
            int k = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(xa[i])).First();
            if (xa[k] < 0) xa = new[] { -xa[0], -xa[1], -xa[2] };
            var ya = Unit(Cross(z, xa));
            xa = Unit(Cross(ya, z));
            var cur = (double[])view.Orientation3.ArrayData;
            var d = (double[])cur.Clone();
            for (int i = 0; i < 3; i++)
            {
                double[] axis = i == 0 ? xa : i == 1 ? ya : z;
                for (int j = 0; j < 3; j++) { if (rows) d[i * 3 + j] = axis[j]; else d[j * 3 + i] = axis[j]; }
            }
            view.Orientation3 = (MathTransform)mu.CreateTransform(d);
            doc.GraphicsRedraw2();
            if (doc.Extension.GetNamedViewRotation(name) != null) doc.DeleteNamedView(name);
            doc.NameView(name);
            var back = (double[])doc.Extension.GetNamedViewRotation(name);
            Console.WriteLine("VIEW\t" + name + "\tright " + V(xa) + "\tup " + V(ya) + "\ttoward viewer " + V(z) + "\tface of " + flats[0].Body + ", " + F(flats[0].Area * 1e6) + " mm2");
            Console.WriteLine("READBACK\t" + (back == null ? "missing" : string.Join(" ", back.Take(9).Select(F))));
            if (back == null) { Console.Error.WriteLine("named view not stored"); return 4; }
            double err = 0;
            for (int i = 0; i < 9; i++) err = Math.Max(err, Math.Abs(back[i] - d[i]));
            if (err > 1e-6) { Console.Error.WriteLine("read-back differs by " + F(err)); return 4; }
            if (save)
            {
                int se = 0, sw2 = 0;
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED " + se);
                if (!ok) return 5;
            }
            else Console.WriteLine("Not saved (no --save).");
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }

    static string Opt(List<string> args, string key)
    {
        int i = args.IndexOf(key);
        if (i < 0) return null;
        string v = args[i + 1]; args.RemoveRange(i, 2); return v;
    }
}
