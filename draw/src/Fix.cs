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
}
