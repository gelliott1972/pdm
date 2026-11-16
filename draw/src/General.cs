// General dimensioning for parts the member and plate routes don't take (Grant 16 Nov, after he dimensioned 08C04-SFP-04,
// 08X04-SFP-04, 08B04-SFP-05 and 08X04-BRP-02 by hand on the Desk: "there has to be a way to get these simple things
// right"). His recipe doesn't depend on the kind of part: in the face view, ordinates from one end to every flat edge
// and round-hole centre, both ways, and the hole size. Positions come from the MODEL (flat faces square to the sheet
// direction, full cylinders along the view direction); a position is dimensioned only where the view draws an edge
// or a hole circle there, and every value made is compared with the model's before saving.
using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    class OrdCand { public object Ent; public double Lo, Hi; }                       // an edge or hole circle; its span the other way on the sheet
    class OrdPos { public double P; public List<OrdCand> C = new List<OrdCand>(); public bool Hole; }

    // What a view actually draws: the box round its visible edges on the sheet (not SOLIDWORKS's view outline, which
    // is often much bigger: Grant 16 Nov).
    static double[] DrawnBox(View v)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var cp = e.GetCurveParams3();
            foreach (var pt in new[] { (double[])cp.StartPoint, (double[])cp.EndPoint })
            {
                var q = ToSheet(mu, v, pt);
                x0 = Math.Min(x0, q[0]); y0 = Math.Min(y0, q[1]); x1 = Math.Max(x1, q[0]); y1 = Math.Max(y1, q[1]);
            }
        }
        return x0 > x1 ? (double[])v.GetOutline() : new[] { x0, y0, x1, y1 };
    }

    // Points along a view's visible edges on the sheet (ends, and points between them about `step` apart).
    static List<double[]> EdgeSamples(View v, double step)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var r = new List<double[]>();
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var cp = e.GetCurveParams3();
            var a = ToSheet(mu, v, (double[])cp.StartPoint); var b = ToSheet(mu, v, (double[])cp.EndPoint);
            int n = Math.Max(1, (int)(Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])) / step));
            for (int i = 0; i <= n; i++) r.Add(new[] { a[0] + (b[0] - a[0]) * i / n, a[1] + (b[1] - a[1]) * i / n });
        }
        return r;
    }

    // CHECK notes outside the border, bottom left: two lines at most (a third runs up into the revision table,
    // seen on 08X04-BRP-02 and 08C04-SFP-05, 16 Nov). The flags are spread over the two lines, about even in length.
    static List<string> PackFlags(List<string> flags)
    {
        if (flags.Count <= 2) return flags.ToList();
        int total = flags.Sum(f => f.Length), run = 0, cut = flags.Count;
        for (int i = 0; i < flags.Count; i++) { run += flags[i].Length; if (run >= total / 2.0) { cut = i + 1; break; } }
        if (cut >= flags.Count) cut = flags.Count - 1;
        return new List<string> { string.Join("      ", flags.Take(cut)), string.Join("      ", flags.Skip(cut)) };
    }

    static bool Near(double a, double b) { return Math.Abs(a - b) < 2e-6; }

    // Ordinates and hole sizes on the face view. Returns the number of dimension rows and callouts made; the values
    // expected from the model are added to `expected`.
    static int GeneralDims(ModelDoc2 doc, SelectionMgr sm, MathUtility mu, View v, List<Body2> bodies, List<double> expected)
    {
        int hAx = SheetAxis(mu, v, 0), vAx = SheetAxis(mu, v, 1);
        if (hAx == vAx) return 0;
        int nAx = 3 - hAx - vAx;
        double vs = v.ScaleDecimal;
        // the model: flat faces square to each sheet direction, and round holes along the view direction
        var px = new List<double>(); var py = new List<double>(); var cyls = new List<double[]>();
        foreach (var b in bodies)
            foreach (Face2 f in (object[])b.GetFaces())
            {
                var s = (Surface)f.GetSurface();
                if (s.IsPlane())
                {
                    var pp = (double[])s.PlaneParams;
                    var q = ToSheet(mu, v, new[] { pp[3], pp[4], pp[5] });
                    if (Math.Abs(Math.Abs(pp[hAx]) - 1) < 1e-6) px.Add(q[0]);
                    else if (Math.Abs(Math.Abs(pp[vAx]) - 1) < 1e-6) py.Add(q[1]);
                }
                else if (s.IsCylinder() && f.GetLoopCount() == 2)
                {
                    var cp = (double[])s.CylinderParams;
                    if (Math.Abs(Math.Abs(cp[3 + nAx]) - 1) > 1e-6) continue;
                    var q = ToSheet(mu, v, new[] { cp[0], cp[1], cp[2] });
                    cyls.Add(new[] { q[0], q[1], 2 * cp[6] });
                }
            }
        // the view: straight edges on those faces, and full circles on those holes
        var lines = ViewLines(mu, v);
        var xs = new List<OrdPos>(); var ys = new List<OrdPos>();
        Action<List<OrdPos>, double, OrdCand, bool> add = (list, p, c, hole) =>
        {
            var at = list.FirstOrDefault(o => Near(o.P, p));
            if (at == null) { at = new OrdPos { P = p, Hole = hole }; list.Add(at); }
            at.C.Add(c);
        };
        foreach (var l in lines)
        {
            double dx = Math.Abs(l.Item2[0] - l.Item3[0]), dy = Math.Abs(l.Item2[1] - l.Item3[1]);
            if (dx < 1e-7 && dy > 2e-4 && px.Any(p => Near(p, l.Item2[0])))
                add(xs, l.Item2[0], new OrdCand { Ent = l.Item1, Lo = Math.Min(l.Item2[1], l.Item3[1]), Hi = Math.Max(l.Item2[1], l.Item3[1]) }, false);
            else if (dy < 1e-7 && dx > 2e-4 && py.Any(p => Near(p, l.Item2[1])))
                add(ys, l.Item2[1], new OrdCand { Ent = l.Item1, Lo = Math.Min(l.Item2[0], l.Item3[0]), Hi = Math.Max(l.Item2[0], l.Item3[0]) }, false);
        }
        var holes = new List<Tuple<Edge, double[], double>>();   // circle edge, centre on the sheet, diameter (model)
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle() || e.GetStartVertex() != null) continue;   // full circles only (a slot's end is an arc)
            var cp = (double[])c.CircleParams;
            if (Math.Abs(Math.Abs(cp[3 + nAx]) - 1) > 1e-6) continue;
            var q = ToSheet(mu, v, new[] { cp[0], cp[1], cp[2] });
            double d = 2 * cp[6];
            if (!cyls.Any(cy => Near(cy[0], q[0]) && Near(cy[1], q[1]) && Math.Abs(cy[2] - d) < 1e-7)) continue;   // a hole in the model, not a chamfer or counterbore edge
            if (holes.Any(h => Near(h.Item2[0], q[0]) && Near(h.Item2[1], q[1]) && Math.Abs(h.Item3 - d) < 1e-7)) continue;
            holes.Add(Tuple.Create(e, q, d));
            add(xs, q[0], new OrdCand { Ent = e, Lo = q[1], Hi = q[1] }, true);
            add(ys, q[1], new OrdCand { Ent = e, Lo = q[0], Hi = q[0] }, true);
        }
        if (xs.Count < 2 && ys.Count < 2) { Console.WriteLine("General dimensions: no flat edges found in the face view"); return 0; }
        xs = xs.OrderBy(o => o.P).ToList(); ys = ys.OrderBy(o => o.P).ToList();
        var db = DrawnBox(v);   // what the view draws (08X06-SFP-03: with no edges one way the rows were placed from 0, off the view)
        double x0 = db[0], x1 = db[2], y0 = db[1], y1 = db[3];
        double wv = x1 - x0, hv = y1 - y0;
        int made = 0;
        bool rowBelow = false, rowAbove = false;   // where the rows across ended up (the hole-size text goes on the other side)
        // one row: positions `ps` (along x when posX), on the high side (top / right) or the low side of the view
        Action<List<OrdPos>, bool, bool, string> row = (ps, posX, high, what) =>
        {
            if (ps.Count > 14) ps = ps.Where((o, i) => i == 0 || i == ps.Count - 1 || o.Hole).ToList();   // a busy part: ends and holes only
            if (ps.Count < 2 || ps[0].Hole || ps.All(o => o.Hole)) { if (ps.Count >= 2) Console.WriteLine("General ordinates " + what + ": no flat edge to start from; row left out"); return; }
            var items = ps.Select(o => (high ? o.C.OrderByDescending(c => c.Hi).First() : o.C.OrderBy(c => c.Lo).First()).Ent).ToList();
            double zero = ps[0].P;
            var vals = ps.Select(o => (o.P - zero) / vs * 1000).ToList();
            expected.AddRange(vals);
            double rowAt = posX ? (high ? y1 + 0.012 : y0 - 0.012) : (high ? x1 + 0.012 : x0 - 0.012);
            if (posX) { if (high) rowAbove = true; else rowBelow = true; }
            if (posX) Ordinate2(doc, sm, v, items, (int)swAddOrdinateDims_e.swHorizontalOrdinate, zero, rowAt, what);
            else Ordinate2(doc, sm, v, items, (int)swAddOrdinateDims_e.swVerticalOrdinate, rowAt, zero, what);
            Console.WriteLine("General ordinates " + what + ": " + string.Join(", ", vals.Select(x => Math.Round(x, 2))));
            made++;
        };
        // the long way of the view: one row, on the side most of its features are nearer. The short way: when the view is
        // long (1.5 to 1 or more), a row at each end with its own zero, so no extension line runs the length of the part
        // (Grant's 08C04-SFP-04: 0/17.5/62.5/80 at the left end, 0/5/25/106 at the right)
        Action<List<OrdPos>, bool, double, double, double, string> rows = (ps, posX, c0, c1, span, what) =>
        {
            if (ps.Count < 2) return;
            double cross = c1 - c0;
            bool split = cross >= 1.5 * span && cross > 0.040;
            if (!split)
            {
                int hi = ps.Count(o => c1 - o.C.Max(c => c.Hi) < o.C.Min(c => c.Lo) - c0), lo = ps.Count(o => c1 - o.C.Max(c => c.Hi) > o.C.Min(c => c.Lo) - c0);
                row(ps, posX, hi >= lo, what);
                return;
            }
            var lowRow = new List<OrdPos>(); var highRow = new List<OrdPos>();
            foreach (var o in ps)
            {
                double dLo = o.C.Min(c => c.Lo) - c0, dHi = c1 - o.C.Max(c => c.Hi);
                bool toLo = dLo <= 0.15 * cross, toHi = dHi <= 0.15 * cross;
                if (!toLo && !toHi) { if (dLo <= dHi) toLo = true; else toHi = true; }
                if (toLo) lowRow.Add(o);
                if (toHi) highRow.Add(o);
            }
            Func<List<OrdPos>, List<OrdPos>, bool> within = (a, b) => a.Count == 0 || (b.Count > 0 && Near(a[0].P, b[0].P) && a.All(o => b.Contains(o)));
            if (within(lowRow, highRow)) lowRow.Clear(); else if (within(highRow, lowRow)) highRow.Clear();
            row(lowRow, posX, false, what + (posX ? ", bottom" : ", left"));
            row(highRow, posX, true, what + (posX ? ", top" : ", right"));
        };
        rows(xs, true, y0, y1, wv, "across");
        rows(ys, false, x0, x1, hv, "up");
        doc.ClearSelection2(true);
        // hole sizes: one diameter dimension per size, "n x" when there are several (no THRU: not checked here)
        int k = 0;
        foreach (var g in holes.GroupBy(h => Math.Round(h.Item3 * 1e6)))
        {
            // Grant's 08C04-SFP-04 / 08X04-SFP-04: the leader runs out past the part's edge on the side without a row of
            // ordinates, from the hole nearest that edge; the text never on the part
            double toTop = y1 - g.Max(h => h.Item2[1]), toBottom = g.Min(h => h.Item2[1]) - y0;
            bool up = rowBelow != rowAbove ? rowBelow : toTop <= toBottom;   // the side without a row; else the nearer side
            var pick = up ? g.OrderByDescending(h => h.Item2[1]).ThenBy(h => h.Item2[0]).First() : g.OrderBy(h => h.Item2[1]).ThenByDescending(h => h.Item2[0]).First();
            double tx = pick.Item2[0] + 0.012 + 0.012 * k;
            double ty = up ? y1 + 0.010 + 0.006 * k + (rowAbove ? 0.024 : 0) : y0 - 0.010 - 0.006 * k - (rowBelow ? 0.024 : 0);
            doc.ClearSelection2(true);
            ((DrawingDoc)doc).ActivateView(v.GetName2());
            var sd = (SelectData)sm.CreateSelectData(); sd.View = v;
            if (!((Entity)pick.Item1).Select4(false, sd)) continue;
            DisplayDimension dd = null;
            try { dd = doc.AddDiameterDimension2(tx, ty, 0) as DisplayDimension; } catch (System.Runtime.InteropServices.COMException) { }
            doc.ClearSelection2(true);
            if (dd == null) { Console.WriteLine("  hole size not placed for " + (pick.Item3 * 1000).ToString("0.##")); continue; }
            if (g.Count() > 1) dd.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, g.Count() + " x <MOD-DIAM>");
            expected.Add(pick.Item3 * 1000);
            Console.WriteLine("General hole size: " + (g.Count() > 1 ? g.Count() + " x " : "") + "Ø" + (pick.Item3 * 1000).ToString("0.##"));
            made++; k++;
        }
        return made;
    }
}
