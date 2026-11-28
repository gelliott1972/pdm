using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// fix-holes <drawing> [pdf] --view=NAME [--dim=20]   (Grant's three fixes, 21 Nov, through Admin)
// Only what was asked: a centre mark on each hole seen face-on in the named view (document settings, no sizes of
// ours), and, with --dim, the one dimension of that value in the view remade from the hole nearest to where it
// sits (same edge side, same place). Everything else is listed before and after and must be unchanged, with
// nothing dangling; only then is the drawing saved. It is left open.
static partial class Draw
{
    class AnnFact { public string View, Name, What; public double X, Y; public bool Dangling; }

    static List<AnnFact> AnnFacts(DrawingDoc drw)
    {
        var l = new List<AnnFact>();
        for (var x = (View)drw.GetFirstView(); x != null; x = (View)x.GetNextView())
            foreach (Annotation a in (x.GetAnnotations() as object[]) ?? new object[0])
            {
                var p = a.GetPosition() as double[];
                string what = TypeName(a.GetType());
                if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    var dd = (DisplayDimension)a.GetSpecificAnnotation(); var d = dd.GetDimension2(0) as Dimension;
                    what += " " + (d == null ? "?" : Math.Round(d.GetSystemValue2("") * 1000, 3).ToString());
                }
                else if (a.GetType() == (int)swAnnotationType_e.swNote) what += " '" + Flat(((Note)a.GetSpecificAnnotation()).GetText()) + "'";
                l.Add(new AnnFact { View = x.GetName2(), Name = a.GetName(), What = what, X = p == null ? 0 : p[0], Y = p == null ? 0 : p[1], Dangling = a.IsDangling() });
            }
        return l;
    }

    static int FixHoles(List<string> pos, string[] rawArgs)
    {
        string file = pos[0], pdf = pos.Count > 1 ? pos[1] : null;
        string vn = rawArgs.Where(a => a.StartsWith("--view=", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(7)).FirstOrDefault();
        string dv = rawArgs.Where(a => a.StartsWith("--dim=", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(6)).FirstOrDefault();
        if (vn == null) { Console.Error.WriteLine("--view=NAME is needed"); return 2; }
        if (pdf != null && File.Exists(pdf)) { Console.Error.WriteLine("REFUSED: already exists: " + pdf); return 2; }
        var doc = OpenDoc(file, null, false);
        var drw = (DrawingDoc)doc;
        var v = LiveView(doc, vn);
        if (v == null) { Console.Error.WriteLine("no view named '" + vn + "'"); return 1; }
        var mu = (MathUtility)sw.GetMathUtility();
        var selMgr = (SelectionMgr)doc.SelectionManager;
        var before = AnnFacts(drw);
        Console.WriteLine("Before: " + before.Count + " annotations, " + before.Count(a => a.What.StartsWith("swDisplayDimension")) + " dimensions, " + before.Count(a => a.What.StartsWith("swCenterMarkSym")) + " centre marks, " + before.Count(a => a.Dangling) + " dangling");
        drw.ActivateView(v.GetName2());
        var g = LiveRead(mu, v);
        double vs = v.ScaleDecimal;
        var holes = g.Holes.GroupBy(h => Math.Round(h.Item2[0] * 1e5) + "," + Math.Round(h.Item2[1] * 1e5)).Select(x => x.First()).ToList();
        Console.WriteLine("View '" + v.GetName2() + "': " + holes.Count + " hole(s) face-on: " + string.Join("; ", holes.Select(h => "dia " + Mm(h.Item3) + " at " + Mm(h.Item2[0]) + "," + Mm(h.Item2[1]))));
        if (holes.Count == 0) { Console.Error.WriteLine("no holes seen face-on; nothing done, not saved"); return 1; }
        var lm = (LayerMgr)doc.GetLayerManager();
        Console.WriteLine("Current layer: '" + (lm.GetCurrentLayer() as string ?? "") + "'");

        // 1. centre marks
        int made = 0;
        foreach (var h in holes)
        {
            if (before.Any(a => a.View == v.GetName2() && a.What.StartsWith("swCenterMarkSym") && Math.Abs(a.X - h.Item2[0]) < 1e-4 && Math.Abs(a.Y - h.Item2[1]) < 1e-4))
            { Console.WriteLine("  hole at " + Mm(h.Item2[0]) + "," + Mm(h.Item2[1]) + ": has a centre mark already"); continue; }
            doc.ClearSelection2(true);
            var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
            bool sel = ((Entity)h.Item1).Select4(false, sd);
            var cm = drw.InsertCenterMark3((int)swCenterMarkStyle_e.swCenterMark_Single, false, false) as CenterMark;
            Console.WriteLine("  centre mark at " + Mm(h.Item2[0]) + "," + Mm(h.Item2[1]) + ": " + (cm != null ? "added" : "NOT added (selected " + sel + ")"));
            if (cm != null) made++;
        }
        doc.ClearSelection2(true);

        // 2. the dimension, from the hole nearest to it
        string oldName = null, newName = null; double[] oldPos = null;
        if (dv != null)
        {
            double want = double.Parse(dv, System.Globalization.CultureInfo.InvariantCulture);
            var hits = new List<Annotation>();
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                var d = ((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0) as Dimension;
                if (d != null && Math.Abs(d.GetSystemValue2("") * 1000 - want) < 0.01) hits.Add(a);
            }
            if (hits.Count != 1) { Console.Error.WriteLine(hits.Count + " dimensions of " + want + " in the view (need exactly one); not saved"); return 1; }
            var old = hits[0]; oldName = old.GetName(); oldPos = (double[])old.GetPosition();
            var odd = (DisplayDimension)old.GetSpecificAnnotation();
            int arrowSide = odd.ArrowSide; string layer = old.Layer;
            var hole = holes.OrderBy(h => Math.Pow(h.Item2[0] - oldPos[0], 2) + Math.Pow(h.Item2[1] - oldPos[1], 2)).First();
            // the straight edge that is `want` from that hole's centre, on the side where the dimension sits
            var cands = new List<Tuple<Edge, double, bool>>();   // edge, distance of the hole-edge midpoint from the dimension text, measuresX
            foreach (var e in g.Ver) if (Math.Abs(Math.Abs(e.Item2 - hole.Item2[0]) / vs * 1000 - want) < 0.01) cands.Add(Tuple.Create(e.Item1, Math.Abs((e.Item2 + hole.Item2[0]) / 2 - oldPos[0]), true));
            foreach (var e in g.Hor) if (Math.Abs(Math.Abs(e.Item2 - hole.Item2[1]) / vs * 1000 - want) < 0.01) cands.Add(Tuple.Create(e.Item1, Math.Abs((e.Item2 + hole.Item2[1]) / 2 - oldPos[1]), false));
            cands = cands.Where(c => c.Item2 < 0.003).OrderBy(c => c.Item2).ToList();
            Console.WriteLine("Dimension " + want + " '" + oldName + "' at " + Mm(oldPos[0]) + "," + Mm(oldPos[1]) + "; nearest hole at " + Mm(hole.Item2[0]) + "," + Mm(hole.Item2[1]) + "; " + cands.Count + " edge(s) fit");
            if (cands.Count == 0) { Console.Error.WriteLine("no straight edge " + want + " from that hole where the dimension sits; not saved"); return 1; }
            doc.ClearSelection2(true);
            if (!old.Select3(false, null) || !doc.Extension.DeleteSelection2(0)) { Console.Error.WriteLine("the old dimension could not be removed; not saved"); return 1; }
            doc.ClearSelection2(true);
            var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
            ((Entity)cands[0].Item1).Select4(false, sd); ((Entity)hole.Item1).Select4(true, sd);
            var nd = doc.AddDimension2(oldPos[0], oldPos[1], 0) as DisplayDimension;
            doc.ClearSelection2(true);
            if (nd == null) { Console.Error.WriteLine("the new dimension was not placed; not saved"); return 1; }
            var na = (Annotation)nd.GetAnnotation();
            nd.ArrowSide = arrowSide; if (na.Layer != layer) na.Layer = layer;
            na.SetPosition2(oldPos[0], oldPos[1], 0);
            newName = na.GetName();
            double nv = ((Dimension)nd.GetDimension2(0)).GetSystemValue2("") * 1000;
            Console.WriteLine("New dimension '" + newName + "' value " + Math.Round(nv, 3));
            if (Math.Abs(nv - want) > 0.01) { Console.Error.WriteLine("the new dimension reads " + nv + ", not " + want + "; not saved"); return 1; }
        }
        doc.GraphicsRedraw2();
        doc.ForceRebuild3(false);

        // 3. before and after
        var after = AnnFacts(drw);
        bool bad = false;
        foreach (var b in before)
        {
            if (b.Name == oldName && b.View == v.GetName2()) continue;
            var a = after.FirstOrDefault(x => x.View == b.View && x.Name == b.Name);
            if (a == null) { Console.WriteLine("  GONE: " + b.View + " " + b.Name + " " + b.What); bad = true; }
            else if (a.What != b.What || Math.Abs(a.X - b.X) > 1e-6 || Math.Abs(a.Y - b.Y) > 1e-6)
            { Console.WriteLine("  CHANGED: " + b.View + " " + b.Name + ": " + b.What + " at " + Mm(b.X) + "," + Mm(b.Y) + " -> " + a.What + " at " + Mm(a.X) + "," + Mm(a.Y)); bad = true; }
        }
        var added = after.Where(a => !before.Any(b => b.View == a.View && b.Name == a.Name && b.Name != oldName)).ToList();
        foreach (var a in added) Console.WriteLine("  new: " + a.View + " " + a.Name + " " + a.What + " at " + Mm(a.X) + "," + Mm(a.Y));
        int expectNew = made + (dv != null ? 1 : 0);
        if (added.Count != expectNew) { Console.WriteLine("  " + added.Count + " new annotations, expected " + expectNew); bad = true; }
        if (dv != null && oldPos != null)
        {
            var n = after.FirstOrDefault(a => a.Name == newName && a.View == v.GetName2());
            if (n == null || Math.Abs(n.X - oldPos[0]) > 1e-5 || Math.Abs(n.Y - oldPos[1]) > 1e-5) { Console.WriteLine("  the new dimension is not where the old one was" + (n != null ? ": " + Mm(n.X) + "," + Mm(n.Y) : "")); bad = true; }
        }
        int dang = after.Count(a => a.Dangling);
        Console.WriteLine("After: " + after.Count + " annotations, " + after.Count(a => a.What.StartsWith("swDisplayDimension")) + " dimensions, " + after.Count(a => a.What.StartsWith("swCenterMarkSym")) + " centre marks, " + dang + " dangling");
        Console.WriteLine("Other annotations unchanged: " + (bad ? "NO" : "yes (" + (before.Count - (oldName != null ? 1 : 0)) + " checked: same name, value, place)"));
        if (made != holes.Count) { Console.WriteLine("centre marks added " + made + " of " + holes.Count); bad = true; }
        if (bad || dang != 0) { Console.WriteLine("NOT SAVED. The drawing is left open, unsaved."); return 1; }
        int err = 0, warn = 0;
        bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
        Console.WriteLine(ok ? "Saved: " + file : "SAVE FAILED: error " + err + ", warning " + warn);
        if (!ok) return 3;
        if (pdf != null)
        {
            ok = doc.Extension.SaveAs3(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, null, ref err, ref warn);
            Console.WriteLine(ok ? "PDF: " + pdf : "PDF failed: error " + err + ", warning " + warn);
        }
        Console.WriteLine("Left open.");
        return ok ? 0 : 3;
    }

    // loose-probe <drawing> --parts=A,B : look only. Per view that holds loose (dangling) ordinate dimensions: the loose
    // and the sound dimensions of the view, the view's outer edges, and the flat and upright edges of the components
    // whose name contains one of the given part names (the new parts), in sheet mm and in model mm from the top/left.
    static int LooseProbe(string file, string[] rawArgs)
    {
        var parts = (rawArgs.Where(a => a.StartsWith("--parts=", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(8)).FirstOrDefault() ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        var doc = OpenDoc(file, null, true); var drw = (DrawingDoc)doc;
        var mu = (MathUtility)sw.GetMathUtility();
        foreach (var v in LiveViews(drw).ToList())
        {
            var anns = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().ToList();
            if (!anns.Any(a => a.IsDangling() && a.GetType() == (int)swAnnotationType_e.swDisplayDimension)) continue;
            double vs = v.ScaleDecimal;
            Console.WriteLine("VIEW " + v.GetName2() + " scale 1:" + (1 / vs).ToString("0.##"));
            foreach (var a in anns.Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension))
            {
                var dd = (DisplayDimension)a.GetSpecificAnnotation(); var d = dd.GetDimension2(0) as Dimension; var p = (double[])a.GetPosition();
                Console.WriteLine("  dim " + a.GetName() + " " + DimKind(dd) + " value " + (d == null ? "?" : Math.Round(d.GetSystemValue2("") * 1000, 2).ToString()) + " at " + Mm(p[0]) + "," + Mm(p[1]) + (a.IsDangling() ? " LOOSE" : ""));
            }
            var es = AsmEdges(mu, v);
            if (!es.Any(e => e.Hor) || !es.Any(e => !e.Hor)) { Console.WriteLine("  no straight edges"); continue; }
            double top = es.Where(e => e.Hor).Max(e => e.At), bot = es.Where(e => e.Hor).Min(e => e.At), left = es.Where(e => !e.Hor).Min(e => e.At), right = es.Where(e => !e.Hor).Max(e => e.At);
            Console.WriteLine("  outer edges: top " + Mm(top) + " bottom " + Mm(bot) + " left " + Mm(left) + " right " + Mm(right) + "  (" + Math.Round((top - bot) / vs * 1000, 1) + " high, " + Math.Round((right - left) / vs * 1000, 1) + " wide)");
            foreach (var grp in es.Where(e => parts.Any(pn => e.Comp.IndexOf(pn, StringComparison.OrdinalIgnoreCase) >= 0)).GroupBy(e => e.Comp))
                Console.WriteLine("  " + grp.Key + ": flat edges down from top " + string.Join(" ", grp.Where(e => e.Hor).Select(e => Math.Round((top - e.At) / vs * 1000, 1)).Distinct().OrderBy(x => x))
                    + " | upright edges from left " + string.Join(" ", grp.Where(e => !e.Hor).Select(e => Math.Round((e.At - left) / vs * 1000, 1)).Distinct().OrderBy(x => x)));
        }
        sw.CloseDoc(doc.GetTitle());
        return 0;
    }

    // The straight edges a view shows that lie flat or upright on the sheet, with the component each belongs to
    // (an assembly view: each edge is in its component's own space, so the component's transform comes first).
    class AsmEdge { public Edge E; public string Comp; public bool Hor; public double At, From, To; }

    static List<AsmEdge> AsmEdges(MathUtility mu, View v)
    {
        var l = new List<AsmEdge>();
        var comps = ((v.GetVisibleComponents() as object[]) ?? new object[0]).Cast<Component2>().ToList();
        var asm = v.ReferencedDocument as AssemblyDoc; bool warned = false; var byName = new Dictionary<string, Component2>(StringComparer.OrdinalIgnoreCase);
        if (asm != null) foreach (Component2 mc0 in (asm.GetComponents(false) as object[]) ?? new object[0]) byName[mc0.Name2] = mc0;
        Action<Component2> add = c =>
        {
            // the view's own component (named <view root>/<path>) may carry no transform: take it from the model's component
            MathTransform mt = c == null ? null : c.Transform2;
            if (c != null && asm != null && c.Name2.IndexOf('/') > 0)
            {
                Component2 mc; byName.TryGetValue(c.Name2.Substring(c.Name2.IndexOf('/') + 1), out mc);
                if (mc != null && mc.Transform2 != null) mt = mc.Transform2; else if (!warned) { warned = true; Console.WriteLine("  (no model component for " + c.Name2 + ")"); }
            }
            var ct = mt == null ? null : (double[])mt.ArrayData;
            foreach (Edge e in (v.GetVisibleEntities2(c, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                if (!((Curve)e.GetCurve()).IsLine()) continue;
                var cp = e.GetCurveParams3(); var p0 = (double[])cp.StartPoint; var p1 = (double[])cp.EndPoint;
                var a = ToSheet(mu, v, ct == null ? p0 : Xf(ct, p0)); var b = ToSheet(mu, v, ct == null ? p1 : Xf(ct, p1));
                if (Math.Abs(a[1] - b[1]) < 1e-7 && Math.Abs(a[0] - b[0]) > 1e-7) l.Add(new AsmEdge { E = e, Comp = c == null ? "" : c.Name2, Hor = true, At = a[1], From = Math.Min(a[0], b[0]), To = Math.Max(a[0], b[0]) });
                else if (Math.Abs(a[0] - b[0]) < 1e-7 && Math.Abs(a[1] - b[1]) > 1e-7) l.Add(new AsmEdge { E = e, Comp = c == null ? "" : c.Name2, Hor = false, At = a[0], From = Math.Min(a[1], b[1]), To = Math.Max(a[1], b[1]) });
            }
        };
        if (comps.Count == 0) add(null); else foreach (var c in comps) add(c);
        return l;
    }
}
