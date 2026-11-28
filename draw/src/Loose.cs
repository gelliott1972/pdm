using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

// fix-loose <drawing> [pdf] --parts=A,B,...   (joint 8.2 change E, APEX-A124; Desk apex-admin-79, 28 Nov)
// A model change swapped parts, and what was attached to the old parts came loose (dangling). Only the loose things
// are touched:
//   balloons   a loose parts-list balloon is replaced by a fresh one picked on the nearest edge of one of the named
//              new parts, same place, layer, style
//   ordinates  a row with loose members is remade whole on the same line: each value keeps its edge (the edge at that
//              distance from the zero); a loose value with no edge there goes to the named new part that sits there,
//              on its edge nearest the zero. Text places and brackets are kept
//   leftovers  loose zero-value linear dimensions and loose weld-bead symbols (their geometry is gone) are deleted
// Every other annotation is compared before and after (same thing, same place). Saved only when nothing is loose
// afterwards and nothing else changed. The drawing is left open.
static partial class Draw
{
    static int FixLoose(List<string> pos, string[] rawArgs)
    {
        string file = pos[0], pdf = pos.Count > 1 ? pos[1] : null;
        var parts = (rawArgs.Where(a => a.StartsWith("--parts=", StringComparison.OrdinalIgnoreCase)).Select(a => a.Substring(8)).FirstOrDefault() ?? "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        bool dry = rawArgs.Any(a => a.Equals("--dry", StringComparison.OrdinalIgnoreCase));
        if (parts.Length == 0) { Console.Error.WriteLine("--parts=A,B is needed (the new parts)"); return 2; }
        if (pdf != null && File.Exists(pdf)) { Console.Error.WriteLine("REFUSED: already exists: " + pdf); return 2; }
        Func<string, bool> isNew = name => parts.Any(pn => name.IndexOf(pn, StringComparison.OrdinalIgnoreCase) >= 0);
        var doc = OpenDoc(file, null, false);
        var drw = (DrawingDoc)doc;
        var mu = (MathUtility)sw.GetMathUtility();
        var selMgr = (SelectionMgr)doc.SelectionManager;
        var before = AnnFacts(drw);
        Console.WriteLine("Before: " + before.Count + " annotations, " + before.Count(a => a.What.StartsWith("swDisplayDimension")) + " dimensions, " + before.Count(a => a.Dangling) + " loose");
        int balloonsDone = 0, rowsDone = 0, rowDims = 0, deleted = 0, left = 0;
        var notes = new List<string>();

        foreach (var v in LiveViews(drw).ToList())
        {
            var anns = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().ToList();
            if (!anns.Any(a => a.IsDangling())) continue;
            drw.ActivateView(v.GetName2());
            double vs = v.ScaleDecimal;
            Console.WriteLine("View " + v.GetName2() + " (1:" + (1 / vs).ToString("0.##") + ")");

            // 1. balloons
            List<Tuple<double[], Edge, string>> cands = null;
            foreach (var a in anns.Where(a => a.GetType() == (int)swAnnotationType_e.swNote && a.IsDangling()).ToList())
            {
                var n = (Note)a.GetSpecificAnnotation();
                if (!n.IsBomBalloon()) continue;
                var p = (double[])a.GetPosition(); string layer = a.Layer;
                var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
                double ax = p[0], ay = p[1];
                if (lp != null && lp.Length >= 3)
                {
                    // the leader's points: the arrow end is the one farthest from the balloon
                    double far = -1;
                    for (int i = 0; i + 2 < lp.Length + 0 && i + 1 < lp.Length; i += 3) { double dd0 = Math.Pow(lp[i] - p[0], 2) + Math.Pow(lp[i + 1] - p[1], 2); if (dd0 > far) { far = dd0; ax = lp[i]; ay = lp[i + 1]; } }
                }
                int style = n.GetBalloonStyle(), size = n.GetBalloonSize();
                if (cands == null) { cands = NewPartPoints(mu, v, isNew); Console.WriteLine("  " + cands.Count + " points on new parts"); }

                bool ok = false; int tried = 0; string onto = "", shows = "";
                if (cands.Count == 0)
                {
                    // the view shows none of the new parts: the balloon's part has left this assembly, nothing to point at
                    if (dry) { Console.WriteLine("  balloon at " + Mm(p[0]) + "," + Mm(p[1]) + ": would be deleted (no new part in this view)"); continue; }
                    doc.ClearSelection2(true);
                    bool goneB = a.Select3(false, null) && doc.Extension.DeleteSelection2(0);
                    doc.ClearSelection2(true);
                    Console.WriteLine("  balloon at " + Mm(p[0]) + "," + Mm(p[1]) + ": none of the new parts is in this view; " + (goneB ? "deleted" : "NOT deleted"));
                    if (goneB) { deleted++; notes.Add(v.GetName2() + ": loose balloon deleted (its part is no longer in this assembly; none of the new parts is in the view)"); } else left++;
                    continue;
                }
                var seenEdge = new HashSet<Edge>();
                foreach (var q in cands.OrderBy(q => Math.Pow(q.Item1[0] - ax, 2) + Math.Pow(q.Item1[1] - ay, 2)).Take(400))
                {
                    if (!seenEdge.Add(q.Item2) || seenEdge.Count > 12) continue;   // the dozen nearest edges, each once
                    doc.ClearSelection2(true);
                    var sdb = (SelectData)selMgr.CreateSelectData(); sdb.View = v;
                    sdb.X = q.Item1[0]; sdb.Y = q.Item1[1]; sdb.Z = 0;
                    if (!((Entity)q.Item2).Select4(false, sdb)) continue;
                    string pcName = q.Item3;
                    tried++;
                    var bop = (BalloonOptions)doc.Extension.CreateBalloonOptions();
                    bop.Style = style; bop.Size = size;
                    bop.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
                    var nb = doc.Extension.InsertBOMBalloon2(bop) as Note;
                    doc.ClearSelection2(true);
                    if (nb == null) continue;
                    var na = (Annotation)nb.GetAnnotation();
                    string t = nb.GetText() ?? "";
                    int num;
                    if (na.IsDangling() || !int.TryParse(t.Trim(), out num)) { na.Select3(false, null); doc.Extension.DeleteSelection2(0); doc.ClearSelection2(true); continue; }
                    if (na.Layer != layer) na.Layer = layer;
                    na.SetPosition2(p[0], p[1], 0);
                    doc.ClearSelection2(true); a.Select3(false, null); doc.Extension.DeleteSelection2(0); doc.ClearSelection2(true);
                    ok = true; onto = pcName; shows = t.Trim(); break;
                }
                if (ok) { balloonsDone++; Console.WriteLine("  balloon at " + Mm(p[0]) + "," + Mm(p[1]) + ": re-attached to " + Tail(onto) + ", shows item " + shows); notes.Add(v.GetName2() + ": balloon -> item " + shows + " (" + PartOf(onto) + ")"); }
                else { left++; Console.WriteLine("  balloon at " + Mm(p[0]) + "," + Mm(p[1]) + ": NOT re-attached (" + tried + " tries)"); }
            }

            // 2. ordinate rows with loose members
            anns = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().ToList();
            Func<Annotation, bool> isOrd = a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && DimKind((DisplayDimension)a.GetSpecificAnnotation()).Contains("Ordinate");
            Func<Annotation, double> val = a => { var d = ((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0) as Dimension; return d == null ? double.NaN : d.GetSystemValue2(""); };
            var ords = anns.Where(isOrd).ToList();
            var handled = new HashSet<Annotation>();
            List<AsmEdge> es = null;
            foreach (var first in ords.Where(a => a.IsDangling()).ToList())
            {
                if (handled.Contains(first)) continue;
                var fp = (double[])first.GetPosition();
                var sameX = ords.Where(a => Math.Abs(((double[])a.GetPosition())[0] - fp[0]) < 2e-5).ToList();
                var sameY = ords.Where(a => Math.Abs(((double[])a.GetPosition())[1] - fp[1]) < 2e-5).ToList();
                if (sameX.Count == sameY.Count) { left++; Console.WriteLine("  ordinate " + Math.Round(val(first) * 1000, 2) + ": its row is not clear; left"); handled.Add(first); continue; }
                bool upright = sameX.Count > sameY.Count;   // an upright row: texts share x, the values run in y
                var row = upright ? sameX : sameY;
                foreach (var a in row) handled.Add(a);
                Func<Annotation, double> m = a => ((double[])a.GetPosition())[upright ? 1 : 0];
                var zeros = row.Where(a => Math.Abs(val(a)) < 1e-9).ToList();
                // a second zero in brackets is a reference value on the zero edge, not the row's zero
                if (zeros.Count > 1 && zeros.Count(a => !((DisplayDimension)a.GetSpecificAnnotation()).ShowParenthesis) == 1) zeros = zeros.Where(a => !((DisplayDimension)a.GetSpecificAnnotation()).ShowParenthesis).ToList();
                string rowName = (upright ? "upright" : "flat") + " row " + string.Join("/", row.OrderBy(a => val(a)).Select(a => Math.Round(val(a) * 1000, 2).ToString()));
                if (zeros.Count != 1) { left += row.Count(a => a.IsDangling()); Console.WriteLine("  " + rowName + ": " + zeros.Count + " zeros; left"); continue; }
                if (es == null) es = AsmEdges(mu, v);
                var rel = es.Where(e => e.Hor == upright).ToList();
                double Z = m(zeros[0]);
                Func<double, List<AsmEdge>> at = t => rel.Where(e => Math.Abs(e.At - t) < 4e-6).OrderByDescending(e => e.To - e.From).ToList();
                if (at(Z).Count == 0) { left += row.Count(a => a.IsDangling()); Console.WriteLine("  " + rowName + ": no edge at the zero (" + Mm(Z) + "); left"); continue; }
                var plan = new List<Tuple<Annotation, AsmEdge, double>>();   // old dimension, its edge, the new sheet coordinate
                bool bad = false;
                foreach (var a in row.OrderBy(a => Math.Abs(val(a))))
                {
                    if (a == zeros[0]) { plan.Add(Tuple.Create(a, at(Z)[0], Z)); continue; }
                    double sgn = m(a) >= Z ? 1 : -1, dv = val(a) * vs;
                    AsmEdge pick = at(Z + sgn * dv).FirstOrDefault() ?? at(Z - sgn * dv).FirstOrDefault();
                    string how = "its edge";
                    if (pick == null && a.IsDangling())
                    {
                        // no edge there any more: the new part that sits there, on its edge nearest the zero
                        double t = Z + sgn * dv, margin = 0.015 * vs;
                        var comp = rel.Where(e => isNew(e.Comp)).GroupBy(e => e.Comp).Where(g => g.Min(e => e.At) - margin <= t && t <= g.Max(e => e.At) + margin)
                            .OrderBy(g => Math.Min(Math.Abs(g.Min(e => e.At) - t), Math.Abs(g.Max(e => e.At) - t))).FirstOrDefault();
                        if (comp != null) { pick = comp.OrderBy(e => Math.Abs(e.At - Z)).ThenByDescending(e => e.To - e.From).First(); how = "new part " + PartOf(comp.Key); }
                    }
                    if (pick == null) { bad = true; Console.WriteLine("  " + rowName + ": nothing found for " + Math.Round(val(a) * 1000, 2) + (a.IsDangling() ? " (loose)" : " (sound!)")); break; }
                    plan.Add(Tuple.Create(a, pick, pick.At));
                    double nv = Math.Abs(pick.At - Z) / vs * 1000;
                    if (a.IsDangling()) Console.WriteLine("    " + Math.Round(val(a) * 1000, 2) + " loose -> " + how + ", reads " + Math.Round(nv, 2));
                }
                if (bad) { left += row.Count(a => a.IsDangling()); continue; }
                if (dry) { Console.WriteLine("  " + rowName + ": would be remade (dry)"); continue; }
                // what to put back
                var old = plan.Select(t => new { P = (double[])t.Item1.GetPosition(), Paren = ((DisplayDimension)t.Item1.GetSpecificAnnotation()).ShowParenthesis, Layer = t.Item1.Layer,
                    WasLoose = t.Item1.IsDangling(), OldVal = val(t.Item1) * 1000, NewVal = Math.Abs(t.Item3 - Z) / vs * 1000, Shift = t.Item3 - (Z + (m(t.Item1) >= Z ? 1 : -1) * val(t.Item1) * vs), Edge = t.Item2 }).ToList();
                var others = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => !row.Contains(a)).Select(a => PosKey(a)));
                var zp = (double[])zeros[0].GetPosition();
                doc.ClearSelection2(true);
                bool selAll = true; int k = 0;
                foreach (var a in row) { selAll &= a.Select3(k > 0, null); k++; }
                if (!selAll || !doc.Extension.DeleteSelection2(0)) { left += row.Count(a => a.IsDangling()); Console.WriteLine("  " + rowName + ": could not be removed; left"); doc.ClearSelection2(true); continue; }
                doc.ClearSelection2(true);
                int got = LiveRow(doc, v, old.Select(o => (object)o.Edge.E).ToList(), upright ? (int)swAddOrdinateDims_e.swVerticalOrdinate : (int)swAddOrdinateDims_e.swHorizontalOrdinate, zp[0], zp[1], rowName);
                var fresh = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => isOrd(a) && !others.Contains(PosKey(a))).ToList();
                int matched = 0;
                foreach (var o in old)
                {
                    var na = fresh.Where(a => Math.Abs(val(a) * 1000 - o.NewVal) < 0.01).OrderBy(a => Math.Abs(m(a) - o.P[upright ? 1 : 0])).FirstOrDefault();
                    if (na == null) { Console.WriteLine("    " + Math.Round(o.NewVal, 2) + ": NOT made"); continue; }
                    fresh.Remove(na); matched++;
                    var nd = (DisplayDimension)na.GetSpecificAnnotation();
                    if (nd.ShowParenthesis != o.Paren) nd.ShowParenthesis = o.Paren;
                    if (na.Layer != o.Layer) na.Layer = o.Layer;
                    na.SetPosition2(upright ? o.P[0] : o.P[0] + o.Shift, upright ? o.P[1] + o.Shift : o.P[1], 0);
                    if (o.WasLoose) notes.Add(v.GetName2() + ": ordinate " + Math.Round(o.OldVal, 2) + (Math.Abs(o.OldVal - o.NewVal) > 0.01 ? " -> " + Math.Round(o.NewVal, 2) : "") + " re-attached");
                }
                if (matched != old.Count || got != old.Count) { left++; Console.WriteLine("  " + rowName + ": remade " + matched + " of " + old.Count + " (PROBLEM)"); }
                else { rowsDone++; rowDims += old.Count; Console.WriteLine("  " + rowName + ": remade, " + old.Count + " dimensions, " + old.Count(o => o.WasLoose) + " were loose"); }
            }

            // 3. leftovers whose geometry is gone
            if (!dry)
                foreach (var a in ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => a.IsDangling()).ToList())
                {
                    bool bead = a.GetType() == (int)swAnnotationType_e.swWeldBeadSymbol;
                    bool zeroDim = a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !DimKind((DisplayDimension)a.GetSpecificAnnotation()).Contains("Ordinate") && Math.Abs(val(a)) < 1e-9;
                    if (!bead && !zeroDim) continue;
                    doc.ClearSelection2(true);
                    bool gone = a.Select3(false, null) && doc.Extension.DeleteSelection2(0);
                    doc.ClearSelection2(true);
                    if (gone) { deleted++; notes.Add(v.GetName2() + ": loose " + (bead ? "weld-bead symbol" : "zero-value dimension") + " deleted"); }
                    Console.WriteLine("  loose " + (bead ? "weld-bead symbol" : "zero-value dimension") + ": " + (gone ? "deleted" : "NOT deleted"));
                }
        }
        doc.ClearSelection2(true);
        doc.EditRebuild3();

        // before and after
        var after = AnnFacts(drw);
        var pool = after.ToList(); int missing = 0;
        var gonePurpose = new List<AnnFact>();
        foreach (var b in before.OrderBy(x => x.Dangling ? 1 : 0))
        {
            var hit = pool.FirstOrDefault(x => x.View == b.View && x.What == b.What && Math.Abs(x.X - b.X) < 2e-6 && Math.Abs(x.Y - b.Y) < 2e-6);
            if (hit != null) { pool.Remove(hit); continue; }
            if (b.Dangling) { gonePurpose.Add(b); continue; }
            missing++; Console.WriteLine("  CHANGED OR GONE (was sound): " + b.View + " " + b.Name + " " + b.What + " at " + Mm(b.X) + "," + Mm(b.Y));
        }
        foreach (var x in pool) Console.WriteLine("  new: " + x.View + " " + x.Name + " " + x.What + " at " + Mm(x.X) + "," + Mm(x.Y) + (x.Dangling ? " LOOSE" : ""));
        int dang = after.Count(a => a.Dangling);
        Console.WriteLine("After: " + after.Count + " annotations, " + after.Count(a => a.What.StartsWith("swDisplayDimension")) + " dimensions, " + dang + " loose");
        Console.WriteLine("Done: balloons re-attached " + balloonsDone + "; ordinate rows remade " + rowsDone + " (" + rowDims + " dimensions); leftovers deleted " + deleted + "; left " + left + "; sound annotations changed " + missing);
        foreach (var s in notes) Console.WriteLine("CHANGE " + s);
        if (dry) { Console.WriteLine("Dry run: not saved."); return 0; }
        if (dang != 0 || left != 0 || missing != 0) { Console.WriteLine("NOT SAVED. Left open, unsaved."); return 1; }
        int err = 0, warn = 0;
        bool saved = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref err, ref warn);
        Console.WriteLine(saved ? "Saved: " + file : "SAVE FAILED: error " + err + ", warning " + warn);
        if (!saved) return 3;
        if (pdf != null)
        {
            saved = doc.Extension.SaveAs3(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, null, ref err, ref warn);
            Console.WriteLine(saved ? "PDF: " + pdf : "PDF failed: error " + err + ", warning " + warn);
        }
        return saved ? 0 : 3;
    }

    static string PosKey(Annotation a) { var p = a.GetPosition() as double[]; return a.GetType() + "|" + (p == null ? "?" : Math.Round(p[0] * 1e6) + "," + Math.Round(p[1] * 1e6)); }
    static string Tail(string compName) { int i = compName.LastIndexOf('/'); return i < 0 ? compName : compName.Substring(i + 1); }
    static string PartOf(string compName) { string t = Tail(compName); int i = t.LastIndexOf('-'); return i > 0 ? t.Substring(0, i) : t; }

    // Points along the visible straight edges of the new parts, on the sheet (for picking a balloon's part).
    static List<Tuple<double[], Edge, string>> NewPartPoints(MathUtility mu, View v, Func<string, bool> isNew)
    {
        var l = new List<Tuple<double[], Edge, string>>();
        var asm = v.ReferencedDocument as AssemblyDoc;
        var byName = new Dictionary<string, Component2>(StringComparer.OrdinalIgnoreCase);
        if (asm != null) foreach (Component2 mc0 in (asm.GetComponents(false) as object[]) ?? new object[0]) byName[mc0.Name2] = mc0;
        foreach (Component2 c in ((v.GetVisibleComponents() as object[]) ?? new object[0]))
        {
            if (!isNew(c.Name2)) continue;
            MathTransform mt = c.Transform2; Component2 mc;
            if (c.Name2.IndexOf('/') > 0 && byName.TryGetValue(c.Name2.Substring(c.Name2.IndexOf('/') + 1), out mc) && mc.Transform2 != null) mt = mc.Transform2;
            var ct = mt == null ? null : (double[])mt.ArrayData;
            foreach (Edge e in (v.GetVisibleEntities2(c, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                if (!((Curve)e.GetCurve()).IsLine()) continue;
                var cp = e.GetCurveParams3(); var p0 = (double[])cp.StartPoint; var p1 = (double[])cp.EndPoint;
                foreach (double t in new[] { 0.5, 0.3, 0.7, 0.15, 0.85 })
                {
                    var mpt = new[] { p0[0] + (p1[0] - p0[0]) * t, p0[1] + (p1[1] - p0[1]) * t, p0[2] + (p1[2] - p0[2]) * t };
                    l.Add(Tuple.Create(ToSheet(mu, v, ct == null ? mpt : Xf(ct, mpt)), e, c.Name2));
                }
            }
        }
        return l;
    }
}
