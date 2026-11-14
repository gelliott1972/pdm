// Draw: what a drawing view actually draws (GetPolylines7): lines and arcs in sheet coordinates, with the edge each
// came from. Used for true view sizes (an assembly view's outline counts suppressed parts) and for dimensioning.
using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    class Seg { public Edge E; public double[] A, B; public bool Arc; public double[] C; public double R; }

    // raw polylines; points are in the view's own space, converted to the sheet with the view transform
    static List<Seg> ViewSegs(View v)
    {
        var r = new List<Seg>();
        object edgesObj = null;
        var data = v.GetPolylines7(1, out edgesObj) as double[];
        var edges = edgesObj as object[];
        if (data == null) return r;
        var t = (double[])((MathTransform)v.ModelToViewTransform).ArrayData;   // rotation 3x3, translation, scale
        int i = 0, k = 0;
        while (i < data.Length)
        {
            int type = (int)data[i]; int gsz = (int)data[i + 1];
            double[] g = new double[gsz]; Array.Copy(data, i + 2, g, 0, gsz);
            int j = i + 2 + gsz + 6;               // colour, style, font, weight, layer, override
            int np = (int)data[j]; j++;
            var pts = new List<double[]>();
            for (int q = 0; q < np; q++) pts.Add(new[] { data[j + 3 * q], data[j + 3 * q + 1], data[j + 3 * q + 2] });
            i = j + 3 * np;
            var s = new Seg { E = edges != null && k < edges.Length ? edges[k] as Edge : null, Arc = type == 1 };
            k++;
            if (pts.Count == 0) continue;
            s.A = pts[0]; s.B = pts[pts.Count - 1];
            if (s.Arc && gsz >= 12) { s.C = new[] { g[0], g[1], g[2] }; s.R = Math.Sqrt(Math.Pow(g[3] - g[0], 2) + Math.Pow(g[4] - g[1], 2)); }
            s.A = pts.Aggregate((a, b) => a); 
            r.Add(s);
            // keep every point for the box
            boxPts.AddRange(pts);
        }
        return r;
    }
    static List<double[]> boxPts = new List<double[]>();

    static int ViewGeom(string file)
    {
        var doc = (ModelDoc2)sw.GetOpenDocumentByName(file) ?? OpenDoc(file, null, true);
        var drw = (DrawingDoc)doc;
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
        {
            boxPts.Clear();
            var lines = ViewLines((MathUtility)sw.GetMathUtility(), v);
            if (lines.Count > 0)
            {
                double lx0 = lines.Min(l => Math.Min(l.Item2[0], l.Item3[0])), lx1 = lines.Max(l => Math.Max(l.Item2[0], l.Item3[0]));
                double ly0 = lines.Min(l => Math.Min(l.Item2[1], l.Item3[1])), ly1 = lines.Max(l => Math.Max(l.Item2[1], l.Item3[1]));
                Console.WriteLine(v.GetName2() + ": " + lines.Count + " straight edges, box " + Mm(lx0) + "," + Mm(ly0) + " - " + Mm(lx1) + "," + Mm(ly1) + " = " + Mm(lx1 - lx0) + " x " + Mm(ly1 - ly0)
                    + " (at scale: " + ((lx1 - lx0) / v.ScaleDecimal * 1000).ToString("0") + " x " + ((ly1 - ly0) / v.ScaleDecimal * 1000).ToString("0") + " mm)");
            }
            var segs = ViewSegs(v);
            var o = (double[])v.GetOutline(); var p = (double[])v.Position;
            if (boxPts.Count == 0) { Console.WriteLine(v.GetName2() + ": no polylines"); continue; }
            double x0 = boxPts.Min(q => q[0]), x1 = boxPts.Max(q => q[0]), y0 = boxPts.Min(q => q[1]), y1 = boxPts.Max(q => q[1]);
            Console.WriteLine(v.GetName2() + " '" + v.GetOrientationName() + "' scale " + v.ScaleDecimal + ": " + segs.Count + " segs (" + segs.Count(s => s.E != null) + " with edges); raw box "
                + Mm(x0) + "," + Mm(y0) + " - " + Mm(x1) + "," + Mm(y1) + "; outline " + Mm(o[0]) + "," + Mm(o[1]) + " - " + Mm(o[2]) + "," + Mm(o[3]) + "; position " + Mm(p[0]) + "," + Mm(p[1]));
        }
        return 0;
    }
}
