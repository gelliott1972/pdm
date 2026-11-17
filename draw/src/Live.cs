// live <what> [--view=NAME]: work on the drawing Grant has open in SOLIDWORKS, on the view he has selected (Grant
// 17 Nov: "a mechanism for me to ask you what to do with what I have open ... rebuilding from scratch is slowing us
// down a lot"). Never opens, closes or saves a document; only adds to the active drawing, and only inside the test
// folder. Runs while the SOLIDWORKS lock is Grant's (he asks in the Draw window) or a Draw round.
//   live info                    the active drawing, the selected view, its edges, slots, holes and annotations
//   live slots [--side=right|left] [--no-rows] [--no-callout]
//                                slot centre marks in the view, a row of vertical ordinates beside it (flat edges and
//                                slot centres, zero at the bottom edge), one "n x W X L THRU" note per slot size.
//                                Only what is added goes on the DRAFT layer.
using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    static View LiveView(ModelDoc2 doc, string name)
    {
        var drw = (DrawingDoc)doc;
        if (name != null)
        {
            for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView()) if (string.Equals(x.GetName2(), name, StringComparison.OrdinalIgnoreCase)) return x;
            return null;
        }
        var sm = (SelectionMgr)doc.SelectionManager;
        for (int i = 1; i <= sm.GetSelectedObjectCount2(-1); i++)
            if (sm.GetSelectedObjectType3(i, -1) == (int)swSelectType_e.swSelDRAWINGVIEWS) return sm.GetSelectedObject6(i, -1) as View;
        // something inside a view is selected, or the view is only active (its border dotted)
        for (int i = 1; i <= sm.GetSelectedObjectCount2(-1); i++) { var dv = sm.GetSelectedObjectsDrawingView2(i, -1) as View; if (dv != null) return dv; }
        var av = drw.ActiveDrawingView as View;
        return av != null && av != (View)drw.GetFirstView() ? av : null;
    }

    class LiveGeom
    {
        public List<Tuple<Edge, double, double, double>> Hor = new List<Tuple<Edge, double, double, double>>();   // edge, y, x0, x1 (sheet)
        public List<Tuple<Edge, double, double, double>> Ver = new List<Tuple<Edge, double, double, double>>();   // edge, x, y0, y1
        public List<Tuple<double[], double, double, Edge, bool>> Slots = new List<Tuple<double[], double, double, Edge, bool>>();   // sheet centre, width, length (model m), an end arc, upright
        public List<Tuple<Edge, double[]>[]> SlotEnds = new List<Tuple<Edge, double[]>[]>();   // per slot: its two end arcs with their sheet centres
        public List<Tuple<Edge, double[], double>> Holes = new List<Tuple<Edge, double[], double>>();   // full circles: edge, sheet centre, diameter (model m)
    }

    static LiveGeom LiveRead(MathUtility mu, View v)
    {
        var g = new LiveGeom();
        var arcs = new List<Tuple<Edge, double[], double>>();
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve(); var cp = e.GetCurveParams3();
            var a = ToSheet(mu, v, (double[])cp.StartPoint); var b = ToSheet(mu, v, (double[])cp.EndPoint);
            if (c.IsCircle())
            {
                var q = (double[])c.CircleParams;
                var mp = (double[])cp.StartPoint; var ep = (double[])cp.EndPoint;
                double chord = Math.Sqrt(Math.Pow(mp[0] - ep[0], 2) + Math.Pow(mp[1] - ep[1], 2) + Math.Pow(mp[2] - ep[2], 2));
                // seen face-on only: the circle's axis along the view direction
                var ax = (MathVector)mu.CreateVector(new[] { q[3], q[4], q[5] }); ax = (MathVector)ax.MultiplyTransform(v.ModelToViewTransform);
                var ad = (double[])ax.ArrayData; double al = Math.Sqrt(ad[0] * ad[0] + ad[1] * ad[1] + ad[2] * ad[2]);
                if (al < 1e-12 || Math.Abs(Math.Abs(ad[2]) / al - 1) > 1e-6) continue;
                var cs = ToSheet(mu, v, new[] { q[0], q[1], q[2] });
                if (chord < 1e-9) g.Holes.Add(Tuple.Create(e, cs, 2 * q[6]));
                else if (Math.Abs(chord - 2 * q[6]) < 1e-6) arcs.Add(Tuple.Create(e, cs, q[6]));
            }
            else if (c.IsLine())
            {
                if (Math.Abs(a[1] - b[1]) < 1e-7 && Math.Abs(a[0] - b[0]) > 1e-7) g.Hor.Add(Tuple.Create(e, a[1], Math.Min(a[0], b[0]), Math.Max(a[0], b[0])));
                else if (Math.Abs(a[0] - b[0]) < 1e-7 && Math.Abs(a[1] - b[1]) > 1e-7) g.Ver.Add(Tuple.Create(e, a[0], Math.Min(a[1], b[1]), Math.Max(a[1], b[1])));
            }
        }
        // two edges of one hole (near and far face) show as one circle
        g.Holes = g.Holes.GroupBy(h => Math.Round(h.Item2[0] * 1e6) + "," + Math.Round(h.Item2[1] * 1e6) + "," + Math.Round(h.Item3 * 1e6)).Select(x => x.First()).ToList();
        arcs = arcs.GroupBy(h => Math.Round(h.Item2[0] * 1e6) + "," + Math.Round(h.Item2[1] * 1e6) + "," + Math.Round(h.Item3 * 1e6)).Select(x => x.First()).ToList();
        var used = new HashSet<int>();
        for (int i = 0; i < arcs.Count; i++)
        {
            if (used.Contains(i)) continue;
            int best = -1; double bd = double.MaxValue;
            for (int j = 0; j < arcs.Count; j++)
            {
                if (j == i || used.Contains(j) || Math.Abs(arcs[i].Item3 - arcs[j].Item3) > 1e-7) continue;
                double d = Math.Sqrt(Math.Pow(arcs[i].Item2[0] - arcs[j].Item2[0], 2) + Math.Pow(arcs[i].Item2[1] - arcs[j].Item2[1], 2));
                if (d > 1e-6 && d < bd) { bd = d; best = j; }
            }
            if (best < 0) continue;
            used.Add(i); used.Add(best);
            var c0 = arcs[i].Item2; var c1 = arcs[best].Item2;
            g.SlotEnds.Add(new[] { Tuple.Create(arcs[i].Item1, c0), Tuple.Create(arcs[best].Item1, c1) });
            g.Slots.Add(Tuple.Create(new[] { (c0[0] + c1[0]) / 2, (c0[1] + c1[1]) / 2 }, 2 * arcs[i].Item3, bd / v.ScaleDecimal + 2 * arcs[i].Item3, arcs[i].Item1, Math.Abs(c0[1] - c1[1]) > Math.Abs(c0[0] - c1[0])));
        }
        return g;
    }

    static IEnumerable<View> LiveViews(DrawingDoc drw)
    {
        for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView()) yield return x;
    }

    // One row of ordinates over edges and midpoints (a double[] is a sheet point: the middle of an edge there is picked,
    // as a person does with right-click > Select Midpoint). First item is the zero. Returns how many were selected.
    static int LiveRow(ModelDoc2 doc, View v, List<object> items, int kind, double x, double y, string what)
    {
        doc.ClearSelection2(true);
        ((DrawingDoc)doc).ActivateView(v.GetName2());
        var sm = (SelectionMgr)doc.SelectionManager;
        var sd = (SelectData)sm.CreateSelectData(); sd.View = v;
        int selected = 0;
        foreach (var it in items)
        {
            bool ok;
            if (it is double[])
            {
                var q = (double[])it; int n0 = sm.GetSelectedObjectCount2(-1);
                ok = doc.Extension.SelectByRay(q[0], q[1], 0.05, 0, 0, -1, 0.0003, (int)swSelectType_e.swSelMIDPOINTS, selected > 0, 0, 0);
                int n1 = sm.GetSelectedObjectCount2(-1);
                ok = ok && n1 > n0 && sm.GetSelectedObjectType3(n1, -1) == (int)swSelectType_e.swSelMIDPOINTS;
                Console.WriteLine("    pick midpoint at " + Mm(q[0]) + "," + Mm(q[1]) + ": " + ok + (n1 > n0 ? " (type " + sm.GetSelectedObjectType3(n1, -1) + ")" : ""));
                if (!ok && n1 > n0) sm.DeSelect2(n1, -1);
            }
            else ok = ((Entity)it).Select4(selected > 0, sd);
            if (ok) selected++;
        }
        if (selected != items.Count) { Console.WriteLine("Ordinate " + what + ": only " + selected + " of " + items.Count + " could be selected; row not made"); doc.ClearSelection2(true); return selected; }
        int r = doc.Extension.AddOrdinateDimension(kind, x, y, 0);
        EndOrdinate(doc);
        Console.WriteLine("Ordinate " + what + ": " + selected + " of " + items.Count + " selected, result " + r);
        return selected;
    }

    // fix-balloons <drawing>: a balloon that has come loose from its part (dangling) is replaced by a fresh one picked
    // on the nearest visible edge that gives the same item number, at the same place and on the same layer. Saves only
    // when nothing in the drawing is dangling afterwards. (08C04-SFP-03, 17 Nov: one loose balloon in a drawing Grant
    // had accepted.)
    static int FixBalloons(string drawing)
    {
        var doc = OpenDoc(drawing, null, false); var drw = (DrawingDoc)doc;
        var mu = (MathUtility)sw.GetMathUtility();
        int done = 0, left = 0;
        foreach (var v in LiveViews(drw).ToList())
        {
            foreach (Annotation a in ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().ToList())
            {
                if (a.GetType() != (int)swAnnotationType_e.swNote || !a.IsDangling()) continue;
                var n = (Note)a.GetSpecificAnnotation();
                if (!n.IsBomBalloon()) continue;
                string txt = n.GetText(); var pos = (double[])a.GetPosition(); string layer = a.Layer;
                var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
                double ax = lp != null && lp.Length >= 2 ? lp[0] : pos[0], ay = lp != null && lp.Length >= 2 ? lp[1] : pos[1];
                Console.WriteLine("Loose balloon '" + txt + "' in " + v.GetName2() + " at " + Mm(pos[0]) + "," + Mm(pos[1]) + ", leader " + (lp == null ? "none" : string.Join(" ", lp.Select(z => Mm(z)))));
                drw.ActivateView(v.GetName2());
                var cands = new List<double[]>();
                var comps = ((v.GetVisibleComponents() as object[]) ?? new object[0]).Cast<Component2>().ToList();
                Action<Component2> add = c =>
                {
                    var ct = c == null || c.Transform2 == null ? null : (double[])c.Transform2.ArrayData;
                    foreach (Edge e in (v.GetVisibleEntities2(c, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
                    {
                        if (!((Curve)e.GetCurve()).IsLine()) continue;
                        var cp = e.GetCurveParams3(); var p0 = (double[])cp.StartPoint; var p1 = (double[])cp.EndPoint;
                        foreach (double t in Enumerable.Range(1, 39).Select(i => i / 40.0))   // along the edge, so the arrow can land near where it pointed
                        {
                            var m = new[] { p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, p0[2] + (p1[2] - p0[2]) * t };
                            cands.Add(ToSheet(mu, v, ct == null ? m : Xf(ct, m)));
                        }
                    }
                };
                if (comps.Count == 0) add(null); else foreach (var c in comps) add(c);
                bool ok = false; int tried = 0;
                var known = new Dictionary<string, string>();   // picked component -> the item number its balloon shows
                var smb = (SelectionMgr)doc.SelectionManager;
                foreach (var q in cands.OrderBy(q => Math.Pow(q[0] - ax, 2) + Math.Pow(q[1] - ay, 2)).Take(4000))
                {
                    doc.ClearSelection2(true);
                    if (!doc.Extension.SelectByID2("", "EDGE", q[0], q[1], 0, false, 0, null, 0)) continue;
                    var pc = smb.GetSelectedObjectsComponent4(1, -1) as Component2; string key = pc == null ? "" : pc.Name2;
                    if (known.ContainsKey(key) && known[key] != txt) continue;   // that part is another item
                    tried++;
                    var bop = (BalloonOptions)doc.Extension.CreateBalloonOptions();
                    bop.Style = (int)swBalloonStyle_e.swBS_Circular; bop.Size = (int)swBalloonFit_e.swBF_2Chars;
                    bop.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
                    var nb = doc.Extension.InsertBOMBalloon2(bop) as Note;
                    doc.ClearSelection2(true);
                    if (nb == null) continue;
                    var na = (Annotation)nb.GetAnnotation();
                    known[key] = nb.GetText();
                    if (nb.GetText() != txt || na.IsDangling()) { na.Select3(false, null); doc.Extension.DeleteSelection2(0); doc.ClearSelection2(true); continue; }
                    na.Layer = layer; na.SetPosition2(pos[0], pos[1], 0);
                    doc.ClearSelection2(true); a.Select3(false, null); doc.Extension.DeleteSelection2(0); doc.ClearSelection2(true);
                    Console.WriteLine("  re-attached: new balloon '" + txt + "' picked at " + Mm(q[0]) + "," + Mm(q[1]) + " (try " + tried + "), same place, layer '" + layer + "'");
                    ok = true; break;
                }
                if (ok) done++; else { left++; Console.WriteLine("  NOT re-attached after " + tried + " tries"); }
            }
        }
        doc.EditRebuild3();
        int dangling = 0;
        for (var x = (View)drw.GetFirstView(); x != null; x = (View)x.GetNextView())
            foreach (Annotation a in (x.GetAnnotations() as object[]) ?? new object[0]) if (a.IsDangling()) dangling++;
        Console.WriteLine("Balloons re-attached " + done + ", left " + left + "; dangling now " + dangling);
        if (done > 0 && dangling == 0)
        {
            int err = 0, warn = 0;
            bool saved = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
            Console.WriteLine(saved ? "Saved." : "NOT saved (error " + err + ", warning " + warn + ")");
            return saved ? 0 : 1;
        }
        Console.WriteLine("Not saved.");
        return 1;
    }

    static ModelDoc2 LiveDoc()
    {
        var doc = sw.ActiveDoc as ModelDoc2;
        if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocDRAWING) { Console.Error.WriteLine("REFUSED: the active document is not a drawing"); return null; }
        string p = doc.GetPathName();
        if (!p.StartsWith(TestRoot + "\\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: Draw works only inside " + TestRoot + ": " + p); return null; }
        return doc;
    }

    static string DimKind(DisplayDimension dd)
    {
        return Enum.GetName(typeof(swDimensionType_e), dd.Type2) ?? dd.Type2.ToString();
    }

    static int Live(List<string> pos, HashSet<string> flags)
    {
        var doc = LiveDoc(); if (doc == null) return 2;
        var drw = (DrawingDoc)doc;
        string vn = flags.Where(f => f.StartsWith("view=")).Select(f => f.Substring(5)).FirstOrDefault();
        var v = LiveView(doc, vn);
        var mu = (MathUtility)sw.GetMathUtility();
        Console.WriteLine("Active: " + doc.GetPathName());
        if (pos[0] == "info")
        {
            for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView())
            {
                var o = (double[])x.GetOutline();
                var bv = x.GetBaseView() as View;
                Console.WriteLine("  view '" + x.GetName2() + "' type " + x.Type + " scale 1:" + (1 / x.ScaleDecimal).ToString("0.##") + " orientation '" + x.GetOrientationName() + "'" + (bv != null ? " parent '" + bv.GetName2() + "'" : "")
                    + " outline " + Mm(o[0]) + "," + Mm(o[1]) + " to " + Mm(o[2]) + "," + Mm(o[3]) + (v != null && x.GetName2() == v.GetName2() ? "   <== selected" : ""));
            }
            if (v == null) { Console.WriteLine("No view selected."); return 0; }
            var g = LiveRead(mu, v);
            double vs = v.ScaleDecimal;
            if (g.Hor.Count > 0)
            {
                double y0 = g.Hor.Min(h => h.Item2);
                Console.WriteLine("  flat edges up from the lowest (mm, longest edge at that height): " + string.Join(", ", g.Hor.GroupBy(h => Math.Round((h.Item2 - y0) / vs * 1000, 2)).OrderBy(x => x.Key).Select(x => x.Key + " (" + Math.Round(x.Max(h => h.Item4 - h.Item3) / vs * 1000, 1) + ")")));
            }
            if (g.Ver.Count > 0)
            {
                double x0 = g.Ver.Min(h => h.Item2);
                Console.WriteLine("  upright edges across from the left (mm, longest): " + string.Join(", ", g.Ver.GroupBy(h => Math.Round((h.Item2 - x0) / vs * 1000, 2)).OrderBy(x => x.Key).Select(x => x.Key + " (" + Math.Round(x.Max(h => h.Item4 - h.Item3) / vs * 1000, 1) + ")")));
            }
            double bx = g.Ver.Count > 0 ? g.Ver.Min(h => h.Item2) : 0, by = g.Hor.Count > 0 ? g.Hor.Min(h => h.Item2) : 0;
            foreach (var s in g.Slots) Console.WriteLine("  slot " + Mm(s.Item2) + " x " + Mm(s.Item3) + (s.Item5 ? " upright" : " lying") + ", centre " + Math.Round((s.Item1[0] - bx) / vs * 1000, 2) + " across, " + Math.Round((s.Item1[1] - by) / vs * 1000, 2) + " up");
            foreach (var h in g.Holes) Console.WriteLine("  hole dia " + Mm(h.Item3) + ", centre " + Math.Round((h.Item2[0] - bx) / vs * 1000, 2) + " across, " + Math.Round((h.Item2[1] - by) / vs * 1000, 2) + " up");
            var shown = flags.Contains("all") ? Enumerable.Repeat((View)drw.GetFirstView(), 1).Concat(LiveViews(drw)).ToList() : new List<View> { v };
            foreach (var sv in shown) { if (shown.Count > 1) Console.WriteLine("  in '" + sv.GetName2() + "':");
            foreach (Annotation a in (sv.GetAnnotations() as object[]) ?? new object[0])
            {
                var p = a.GetPosition() as double[];
                string what = TypeName(a.GetType());
                if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    var dd = (DisplayDimension)a.GetSpecificAnnotation(); var d = dd.GetDimension2(0) as Dimension;
                    what = DimKind(dd) + " " + (d == null ? "?" : Math.Round(d.GetSystemValue2("") * 1000, 2).ToString());
                }
                else if (a.GetType() == (int)swAnnotationType_e.swNote) what = "note '" + Flat(((Note)a.GetSpecificAnnotation()).GetText()) + "'";
                Console.WriteLine("    " + what + " '" + a.GetName() + "' layer '" + a.Layer + "'" + (p != null ? " at " + Mm(p[0]) + "," + Mm(p[1]) : "") + (a.IsDangling() ? "   <== DANGLING" : ""));
            }
            }
            return 0;
        }
        if (pos[0] == "part-slots")
        {
            // read-only: how each slot seen in the view is made in the part (feature, sketch, true slot or lines and arcs)
            if (v == null) { Console.Error.WriteLine("No view selected."); return 1; }
            var g1 = LiveRead(mu, v);
            for (int i = 0; i < g1.Slots.Count; i++)
            {
                var sl = g1.Slots[i]; var ends = g1.SlotEnds[i];
                double sep = Math.Sqrt(Math.Pow(ends[0].Item2[0] - ends[1].Item2[0], 2) + Math.Pow(ends[0].Item2[1] - ends[1].Item2[1], 2)) / v.ScaleDecimal;
                var sides = g1.Ver.Concat(g1.Hor).Where(h => h.Item3 < (sl.Item5 ? sl.Item1[1] : sl.Item1[0]) && h.Item4 > (sl.Item5 ? sl.Item1[1] : sl.Item1[0]) && Math.Abs(Math.Abs(h.Item2 - (sl.Item5 ? sl.Item1[0] : sl.Item1[1])) - sl.Item2 / 2 * v.ScaleDecimal) < 1e-5).ToList();
                Console.WriteLine("Slot " + (i + 1) + ": width " + Mm(sl.Item2) + ", length " + Mm(sl.Item3) + ", end centres " + Mm(sep) + " apart, straight sides seen " + sides.Count + (sides.Count > 0 ? " (each " + string.Join("/", sides.Select(h => Mm((h.Item4 - h.Item3) / v.ScaleDecimal))) + " long)" : ""));
                foreach (var end in ends)
                {
                    var cpm = end.Item1.GetCurveParams3(); var cq = (double[])((Curve)end.Item1.GetCurve()).CircleParams;
                    foreach (object fo in (end.Item1.GetTwoAdjacentFaces2() as object[]) ?? new object[0])
                    {
                        var f = fo as Face2; if (f == null) continue;
                        var sf = (Surface)f.GetSurface();
                        string kind = sf.IsCylinder() ? "cylinder r " + Mm(((double[])sf.CylinderParams)[6]) : sf.IsPlane() ? "flat" : "other";
                        var feat = f.GetFeature() as Feature;
                        string fs = feat == null ? "no feature" : "'" + feat.Name + "' (" + feat.GetTypeName2() + ")";
                        if (feat != null)
                            for (var sub = feat.GetFirstSubFeature() as Feature; sub != null; sub = sub.GetNextSubFeature() as Feature)
                            {
                                var sk = sub.GetSpecificFeature2() as Sketch;
                                if (sk == null) { fs += "; sub '" + sub.Name + "' (" + sub.GetTypeName2() + ")"; continue; }
                                var segs = (sk.GetSketchSegments() as object[]) ?? new object[0];
                                fs += "; sketch '" + sub.Name + "': " + sk.GetSketchSlotCount() + " slot entit(ies), " + segs.Length + " segment(s)";
                            }
                        Console.WriteLine("    end arc r " + Mm(cq[6]) + ": face " + kind + ", made by " + fs);
                    }
                }
            }
            return 0;
        }
        if (pos[0] == "cutlist")
        {
            // live cutlist: the weldment cut-list table (top left, James's template) and its balloons on the isometric
            var all = LiveViews(drw).ToList();
            if (all.Count == 0) { Console.Error.WriteLine("no views"); return 1; }
            var main = all[0];
            var iso = all.FirstOrDefault(x => (x.GetOrientationName() ?? "").IndexOf("Isometric", StringComparison.OrdinalIgnoreCase) >= 0);
            var lmc = (LayerMgr)doc.GetLayerManager(); string wasc = lmc.GetCurrentLayer() as string ?? "";
            if (lmc.GetLayer(DraftLayer) == null) lmc.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
            lmc.SetCurrentLayer(DraftLayer);
            var pd = main.ReferencedDocument as PartDoc;
            if (pd != null)
            {
                var bodies = (pd.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[]) ?? new object[0];
                Console.WriteLine("Model " + System.IO.Path.GetFileName(((ModelDoc2)pd).GetPathName()) + ": weldment " + pd.IsWeldment() + ", solid bodies " + bodies.Length + ", configuration '" + main.ReferencedConfiguration + "'");
                if (!pd.IsWeldment()) { Console.WriteLine("The model is not a weldment, so it has no cut list to show. Nothing added."); return 1; }
            }
            int r;
            try { r = AddCutList(drw, main, iso, main.ReferencedConfiguration, System.IO.Path.Combine(TestRoot, "_Templates", "x.drwdot")); }
            finally { lmc.SetCurrentLayer(wasc); doc.ClearSelection2(true); doc.GraphicsRedraw2(); }
            Console.WriteLine(r < 0 ? "cut-list table not inserted" : "Cut list in; " + r + " item(s) without a balloon. Not saved.");
            return r < 0 ? 1 : 0;
        }
        if (pos[0] == "remove")
        {
            // live remove --names=D7,D8: delete these annotations of the view, only if they are on DRAFT (to redo a step)
            if (v == null) { Console.Error.WriteLine("No view selected."); return 1; }
            var names = new HashSet<string>(flags.Where(f => f.StartsWith("names=")).SelectMany(f => f.Substring(6).Split(',')), StringComparer.OrdinalIgnoreCase);
            doc.ClearSelection2(true); int sel = 0;
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
                if (names.Contains(a.GetName()) && a.Layer == DraftLayer && a.Select3(sel > 0, null)) sel++;
            bool ok = sel > 0 && doc.Extension.DeleteSelection2(0);
            doc.ClearSelection2(true); doc.GraphicsRedraw2();
            Console.WriteLine("Removed " + (ok ? sel : 0) + " of " + names.Count + " from " + v.GetName2() + ". Not saved.");
            return ok && sel == names.Count ? 0 : 1;
        }
        if (pos[0] == "slots")
        {
            if (v == null)
            {
                // nothing selected: the one view that shows slots face-on, if there is only one
                var withSlots = LiveViews(drw).Where(x => LiveRead(mu, x).Slots.Count > 0).ToList();
                if (withSlots.Count == 1) v = withSlots[0];
                else { Console.Error.WriteLine("No view selected and " + withSlots.Count + " views show slots (" + string.Join(", ", withSlots.Select(x => x.GetName2())) + "): click a view's border first (or --view=NAME)."); return 1; }
            }
            Console.WriteLine("View: " + v.GetName2());
            var g = LiveRead(mu, v);
            double vs = v.ScaleDecimal;
            Console.WriteLine("Slots: " + g.Slots.Count + (g.Slots.Count > 0 ? " (" + string.Join(", ", g.Slots.Select(s => Mm(s.Item2) + " x " + Mm(s.Item3))) + ")" : ""));
            if (g.Slots.Count == 0 || g.Hor.Count == 0 || g.Ver.Count == 0) { Console.Error.WriteLine("no slots (or no flat edges) seen face-on in this view; nothing added"); return 1; }
            bool left = flags.Contains("side=left");
            var lm = (LayerMgr)doc.GetLayerManager(); string layerWas = lm.GetCurrentLayer() as string ?? "";
            if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
            lm.SetCurrentLayer(DraftLayer);
            var selMgr = (SelectionMgr)doc.SelectionManager;
            var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
            int rc = 0;
            try
            {
                drw.ActivateView(v.GetName2());
                // 1. slot centre marks (not twice: a view that already has centre marks keeps them)
                var marks = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => a.GetType() == (int)swAnnotationType_e.swCenterMarkSym).ToList();
                if (marks.Count > 0) Console.WriteLine("Centre marks already in the view: " + marks.Count + " (none added)");
                else if (flags.Contains("no-marks")) Console.WriteLine("Centre marks: not asked for");
                else
                {
                    bool cm = v.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Slots, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0);
                    Func<int> count = () => ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Count(a => a.GetType() == (int)swAnnotationType_e.swCenterMarkSym);
                    int n = count();
                    // 08B04-BKP-01, slots in an angle's leg (17 Nov): the automatic insert answers yes and adds nothing, with
                    // every option; InsertCenterMark3 on a slot's end arc gives that arc's own mark (its Slot argument
                    // changes nothing) and on a straight side gives nothing. So: say so, and leave the slot out of the row.
                    Console.WriteLine(n > 0 ? "Slot centre marks: " + n + " inserted" : "CHECK: SOLIDWORKS made no slot centre marks in this view (the automatic insert adds nothing here)");
                    if (n == 0) rc = 1;
                }
                doc.ClearSelection2(true);
                // 2. vertical ordinates beside the view: zero at the lowest flat edge, the flat edges asked for (--edges=12,112:
                //    mm above the lowest; default the top edge only), and each height of slot centres
                var expect = new List<double>();
                if (!flags.Contains("no-rows"))
                {
                    double y0 = g.Hor.Min(h => h.Item2);
                    string ef = flags.Where(f => f.StartsWith("edges=")).Select(f => f.Substring(6)).FirstOrDefault();
                    var heights = ef != null ? ef.Split(',').Where(t => t.Trim() != "").Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture)).ToList()
                        : new List<double> { Math.Round((g.Hor.Max(h => h.Item2) - y0) / vs * 1000, 2) };
                    double sideX = left ? g.Ver.Min(h => h.Item2) - 0.0121 : g.Ver.Max(h => h.Item2) + 0.0121;
                    var rowItems = new List<Tuple<double, object>>();
                    Func<double, Edge> edgeAt = mm => g.Hor.Where(h => Math.Abs((h.Item2 - y0) / vs * 1000 - mm) < 0.02).OrderByDescending(h => h.Item4 - h.Item3).Select(h => h.Item1).FirstOrDefault();
                    rowItems.Add(Tuple.Create(0.0, (object)edgeAt(0)));
                    foreach (double mm in heights)
                    {
                        var e = edgeAt(mm);
                        if (e == null) { Console.WriteLine("CHECK: no flat edge " + mm + " mm up; left out"); rc = 1; continue; }
                        rowItems.Add(Tuple.Create(mm, (object)e));
                    }
                    // one slot per height, the one nearest the row. --centre=midpoint: the middle of the slot's straight
                    // side nearest the row (an upright slot), where SOLIDWORKS makes no slot-centre mark (08B04-BKP-01)
                    bool mid = flags.Contains("centre=midpoint");
                    foreach (var grp in g.Slots.GroupBy(s => Math.Round(s.Item1[1] * 1e5)))
                    {
                        var s0 = grp.OrderBy(s => Math.Abs(s.Item1[0] - sideX)).First();
                        object pick = s0.Item1;
                        if (mid)
                        {
                            var side = g.Ver.Where(h => Math.Abs(Math.Abs(h.Item2 - s0.Item1[0]) - s0.Item2 / 2 * vs) < 1e-5 && h.Item3 < s0.Item1[1] && h.Item4 > s0.Item1[1] && Math.Abs((h.Item3 + h.Item4) / 2 - s0.Item1[1]) < 1e-6)
                                .OrderBy(h => Math.Abs(h.Item2 - sideX)).FirstOrDefault();
                            if (side == null) { Console.WriteLine("CHECK: no straight side found for the slot at " + Math.Round((s0.Item1[1] - y0) / vs * 1000, 2) + " up (a lying slot?); left out"); rc = 1; continue; }
                            pick = new[] { side.Item2, (side.Item3 + side.Item4) / 2 };
                        }
                        rowItems.Add(Tuple.Create(Math.Round((s0.Item1[1] - y0) / vs * 1000, 2), pick));
                    }
                    rowItems = rowItems.OrderBy(t => t.Item1).ToList();
                    expect = rowItems.Select(t => t.Item1).ToList();
                    if (mid) { lastMissed = new List<double[]>(); LiveRow(doc, v, rowItems.Select(t => t.Item2).ToList(), (int)swAddOrdinateDims_e.swVerticalOrdinate, sideX, y0, "up the " + (left ? "left" : "right")); }
                    else Ordinate2(doc, selMgr, v, rowItems.Select(t => t.Item2).ToList(), (int)swAddOrdinateDims_e.swVerticalOrdinate, sideX, y0, "up the " + (left ? "left" : "right"));
                    foreach (var q in lastMissed) { double mv = Math.Round((q[1] - y0) / vs * 1000, 2); expect.Remove(mv); Console.WriteLine("CHECK: slot centre " + mv + " up could not be picked; not dimensioned"); rc = 1; }
                    doc.ClearSelection2(true);
                }
                // 3. one note per slot size, under the view, its leader to the slot end nearest the text
                if (!flags.Contains("no-callout"))
                {
                    double yb = g.Hor.Min(h => h.Item2); int k = 0;
                    foreach (var grp in g.Slots.Select((s, i) => new { s, i }).GroupBy(x => Math.Round(x.s.Item2 * 1e5) + "x" + Math.Round(x.s.Item3 * 1e5)))
                    {
                        var first = grp.OrderBy(x => x.s.Item1[0]).First();
                        double tx = first.s.Item1[0] + 0.006, ty = yb - 0.009 - 0.008 * k;
                        string at = flags.Where(f => f.StartsWith("at=")).Select(f => f.Substring(3)).FirstOrDefault();   // --at=x,y: where the note goes (sheet mm)
                        if (at != null) { var xy = at.Split(',').Select(t => double.Parse(t, System.Globalization.CultureInfo.InvariantCulture) / 1000).ToArray(); tx = xy[0]; ty = xy[1] - 0.008 * k; }
                        var end = g.SlotEnds[first.i].OrderBy(e => Math.Pow(e.Item2[0] - tx, 2) + Math.Pow(e.Item2[1] - ty, 2)).First();
                        doc.ClearSelection2(true);
                        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v; ((Entity)end.Item1).Select4(false, sd);
                        int n = grp.Count();
                        // slots in line behind one another show as one in the view (08BXX-MSW-01, two angles): count the
                        // half-round faces of that radius along the view direction in the model
                        try
                        {
                            var pdm = v.ReferencedDocument as PartDoc; int half = 0;
                            if (pdm != null)
                                foreach (Body2 b in (pdm.GetBodies2((int)swBodyType_e.swSolidBody, true) as object[]) ?? new object[0])
                                    foreach (Face2 f in (b.GetFaces() as object[]) ?? new object[0])
                                    {
                                        var sf = (Surface)f.GetSurface(); if (!sf.IsCylinder()) continue;
                                        var cy = (double[])sf.CylinderParams; if (Math.Abs(cy[6] - first.s.Item2 / 2) > 1e-7) continue;
                                        var ax = (MathVector)mu.CreateVector(new[] { cy[3], cy[4], cy[5] }); ax = (MathVector)ax.MultiplyTransform(v.ModelToViewTransform);
                                        var ad = (double[])ax.ArrayData; double al = Math.Sqrt(ad[0] * ad[0] + ad[1] * ad[1] + ad[2] * ad[2]);
                                        if (al > 1e-12 && Math.Abs(Math.Abs(ad[2]) / al - 1) < 1e-6) half++;
                                    }
                            Console.WriteLine("  slots of this size: " + n + " seen in the view, " + half + " half-round faces in the model (" + half / 2 + " slots)");
                            if (half / 2 > n) n = half / 2;
                        }
                        catch (Exception ex) { Console.WriteLine("  model count failed: " + ex.Message); }
                        string text = (n > 1 ? n + " x " : "") + (first.s.Item2 * 1000).ToString("0.0") + " X " + (first.s.Item3 * 1000).ToString("0") + " THRU";
                        var note = doc.InsertNote(text) as Note;
                        if (note == null) { Console.WriteLine("slot note not placed: " + text); rc = 1; }
                        else { ((Annotation)note.GetAnnotation()).SetPosition2(tx, ty, 0); Console.WriteLine("Slot note: " + text + " at " + Mm(tx) + "," + Mm(ty)); }
                        k++;
                    }
                    doc.ClearSelection2(true);
                }
                // only what was added goes on DRAFT
                int put = 0; var made = new List<double>();
                foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
                {
                    if (before.Contains(a.GetName())) continue;
                    if (a.Layer != DraftLayer) a.Layer = DraftLayer;
                    put++;
                    if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension) made.Add(Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 2));
                    Console.WriteLine("    added " + TypeName(a.GetType()) + " '" + a.GetName() + "' layer '" + a.Layer + "'" + (a.IsDangling() ? " DANGLING" : ""));
                }
                string fe = string.Join(",", expect.OrderBy(z => z)), fg = string.Join(",", made.OrderBy(z => z));
                Console.WriteLine("Added " + put + " item(s), all on " + DraftLayer + ". Check: model " + fe + " / drawing " + fg + (fe == fg ? " OK" : " MISMATCH"));
                if (fe != fg) rc = 1;
                var bad = SlotDims(drw);
                Console.WriteLine("Slot check: " + (bad.Count == 0 ? "no dimension to a slot end, side or edge" : string.Join(", ", bad)));
                int dangling = 0;
                for (var x = (View)drw.GetFirstView(); x != null; x = (View)x.GetNextView())
                    foreach (Annotation a in (x.GetAnnotations() as object[]) ?? new object[0]) if (a.IsDangling()) dangling++;
                Console.WriteLine("Dangling: " + dangling + ". Not saved.");
                if (dangling > 0) rc = 1;
            }
            finally { lm.SetCurrentLayer(layerWas); doc.ClearSelection2(true); doc.GraphicsRedraw2(); }
            return rc;
        }
        Console.Error.WriteLine("live: unknown " + pos[0]);
        return 2;
    }
}
