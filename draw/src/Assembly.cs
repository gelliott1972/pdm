// Draw: first draft of an assembly drawing, the "nuts and bolts" (brief draw-1 step 4, workflow B), laid out as
// James's assembly sheets (08A04-SFA-2T) with Grant's changes (14 Nov):
//   right: the face view with the top view above it, the bottom view below it and the side view beside it (linked);
//   left:  James's BOM in the top-left corner, the note block under it, front and back isometrics (upright) below;
//   balloons on the front isometric for every BOM item, in the same order round the product as the parts they
//   point to, so leaders don't cross. Dimensions: left to James on complex assemblies for now (flagged).
// Everything placed is on DRAFT. Sizes come from the edges each view really draws (an assembly view's outline
// also counts suppressed parts).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    static double[] LayoutBlocked;
    static int isoDenFinal;
    static List<Note> balloonNotes = new List<Note>();
    static readonly int[] AsmScales = { 5, 10, 15, 20, 25, 30, 40, 50 };

    // ---------- true view sizes ----------
    static Dictionary<string, double[]> geomCache = new Dictionary<string, double[]>();   // view name|scale -> box relative to view position

    // The box of what a view really draws (assembly views): the corners of each visible component's box, seen
    // through the view (about 13 boxes instead of ~600 edges, 14 Nov: measuring every edge took 15 minutes).
    // Measured once per view, relative to the view's position, and scaled with the view. A part view's outline is
    // already right.
    static Dictionary<string, double> geomScale = new Dictionary<string, double>();
    static List<double[]> leafBoxes;
    static Dictionary<string, double[]> outlinePad = new Dictionary<string, double[]>();   // view -> how much its outline overstates the drawing (left, bottom, right, top)   // visible parts' boxes in assembly coordinates
    static double[] OutlineOf(View v)
    {
        var comps = v.GetVisibleComponents() as object[];
        if (v.IsBroken())
        {   // a broken view: SOLIDWORKS's outline less the padding measured before it was broken
            var ob = (double[])v.GetOutline(); double[] pad;
            if (outlinePad.TryGetValue(v.GetName2(), out pad)) return new[] { ob[0] + pad[0], ob[1] + pad[1], ob[2] - pad[2], ob[3] - pad[3] };
            return ob;
        }
        if (comps == null || comps.Length == 0 || leafBoxes == null) return (double[])v.GetOutline();
        var p = (double[])v.Position;
        string key = v.GetName2();
        double[] rel; double s0;
        if (!geomCache.TryGetValue(key, out rel) || !geomScale.TryGetValue(key, out s0))
        {
            var mu = (MathUtility)sw.GetMathUtility();
            double x0 = double.MaxValue, y0 = double.MaxValue, x1 = double.MinValue, y1 = double.MinValue;
            // the visible parts' boxes from the (fully loaded) assembly: parts seen through a drawing view give none
            foreach (var b in leafBoxes)
            {
                for (int i = 0; i < 8; i++)
                {
                    var q = ToSheet(mu, v, new[] { (i & 1) == 0 ? b[0] : b[3], (i & 2) == 0 ? b[1] : b[4], (i & 4) == 0 ? b[2] : b[5] });
                    x0 = Math.Min(x0, q[0]); x1 = Math.Max(x1, q[0]); y0 = Math.Min(y0, q[1]); y1 = Math.Max(y1, q[1]);
                }
            }
            if (x0 == double.MaxValue) return (double[])v.GetOutline();
            rel = new[] { x0 - p[0], y0 - p[1], x1 - p[0], y1 - p[1] };
            s0 = v.ScaleDecimal;
            geomCache[key] = rel; geomScale[key] = s0;
        }
        double k = v.ScaleDecimal / s0;
        return new[] { rel[0] * k + p[0], rel[1] * k + p[1], rel[2] * k + p[0], rel[3] * k + p[1] };
    }

    // The visible parts under some components (a subassembly's own box counts its suppressed parts, 14 Nov).
    static IEnumerable<Component2> Leaves(IEnumerable<Component2> comps)
    {
        foreach (var c in comps)
        {
            if (c == null || c.IsSuppressed() || c.IsHidden(true)) continue;
            var ch = (c.GetChildren() as object[]) ?? new object[0];
            if (ch.Length == 0) yield return c;
            else foreach (var l in Leaves(ch.Cast<Component2>())) yield return l;
        }
    }

    // Move a view so its drawn box's centre is at (x, y); NaN keeps that coordinate (aligned views).
    static void CentreAt(ModelDoc2 doc, View v, double x, double y)
    {
        var o = OutlineOf(v); var p = (double[])v.Position;
        v.Position = new[] { double.IsNaN(x) ? p[0] : p[0] + x - (o[0] + o[2]) / 2, double.IsNaN(y) ? p[1] : p[1] + y - (o[1] + o[3]) / 2 };
        // no rebuild here (14 Nov: a rebuild after every move cost about 15 s each); the caller rebuilds once
    }

    // True drawn boxes of every view and the balloons of a drawing (to learn from one laid out by hand).
    static int ViewBoxes(string file)
    {
        var doc = (ModelDoc2)sw.GetOpenDocumentByName(file) ?? OpenDoc(file, null, true);
        geomCache.Clear(); geomScale.Clear();
        var fv = (View)((View)((DrawingDoc)doc).GetFirstView()).GetNextView();
        var model = (ModelDoc2)sw.GetOpenDocumentByName(fv.GetReferencedModelName());
        if (model is AssemblyDoc)
        {
            ((AssemblyDoc)model).ResolveAllLightWeightComponents(false);
            leafBoxes = Leaves(((object[])((AssemblyDoc)model).GetComponents(true) ?? new object[0]).Cast<Component2>()).Select(c => c.GetBox(false, false) as double[]).Where(b => b != null).ToList();
        }
        for (var v = (View)((View)((DrawingDoc)doc).GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
        {
            var o = OutlineOf(v);
            Console.WriteLine(v.GetName2() + " '" + v.GetOrientationName() + "' 1:" + (1 / v.ScaleDecimal).ToString("0") + " part boxes " + Box(o) + " (" + Mm(W(o)) + " x " + Mm(H(o)) + ")");
            var ls = ViewLines((MathUtility)sw.GetMathUtility(), v);
            if (ls.Count > 0)
            {
                var e = new[] { ls.Min(l => Math.Min(l.Item2[0], l.Item3[0])), ls.Min(l => Math.Min(l.Item2[1], l.Item3[1])), ls.Max(l => Math.Max(l.Item2[0], l.Item3[0])), ls.Max(l => Math.Max(l.Item2[1], l.Item3[1])) };
                Console.WriteLine("    edges " + Box(e) + " (" + Mm(W(e)) + " x " + Mm(H(e)) + ")");
            }
            var bl = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => a.GetType() == (int)swAnnotationType_e.swNote && ((Note)a.GetSpecificAnnotation()).IsBomBalloon()).ToList();
            foreach (var a in bl)
            {
                var p = (double[])a.GetPosition();
                var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
                Console.WriteLine("    balloon " + ((Note)a.GetSpecificAnnotation()).GetText() + " at " + Mm(p[0]) + "," + Mm(p[1]) + (lp != null && lp.Length >= 6 ? " leader " + string.Join(" ", Enumerable.Range(0, lp.Length / 3).Select(i => Mm(lp[3 * i]) + "," + Mm(lp[3 * i + 1]))) : ""));
            }
        }
        return 0;
    }

    // What weld information the model carries: weld features (beads, weld folders, fillet beads) in the assembly
    // and in every part/subassembly, by feature type.
    static int WeldProbe(string asmFile)
    {
        var asm = OpenDoc(asmFile, null, true);
        ((AssemblyDoc)asm).ResolveAllLightWeightComponents(false);
        var counts = new Dictionary<string, int>();
        var where = new Dictionary<string, HashSet<string>>();
        Action<ModelDoc2, string> scan = (m, label) =>
        {
            for (var f = (Feature)m.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
            {
                string t = f.GetTypeName2();
                Action<Feature> add = ff =>
                {
                    string tt = ff.GetTypeName2();
                    if (tt.IndexOf("weld", StringComparison.OrdinalIgnoreCase) < 0 && tt.IndexOf("bead", StringComparison.OrdinalIgnoreCase) < 0 && tt.IndexOf("Fillet", StringComparison.OrdinalIgnoreCase) < 0) return;
                    counts[tt] = (counts.ContainsKey(tt) ? counts[tt] : 0) + 1;
                    if (!where.ContainsKey(tt)) where[tt] = new HashSet<string>();
                    where[tt].Add(label);
                };
                add(f);
                for (var sf = (Feature)f.GetFirstSubFeature(); sf != null; sf = (Feature)sf.GetNextSubFeature()) add(sf);
            }
        };
        scan(asm, Path.GetFileName(asmFile));
        var seen = new HashSet<string>();
        foreach (Component2 c in ((object[])((AssemblyDoc)asm).GetComponents(false)) ?? new object[0])
        {
            if (c.IsSuppressed()) continue;
            var m = c.GetModelDoc2() as ModelDoc2;
            if (m == null || !seen.Add(m.GetPathName())) continue;
            scan(m, Path.GetFileName(m.GetPathName()));
        }
        Console.WriteLine("Weld-related features (type: count, in):");
        foreach (var kv in counts.OrderByDescending(k => k.Value)) Console.WriteLine("  " + kv.Key + ": " + kv.Value + "  in " + string.Join(", ", where[kv.Key].Take(6)) + (where[kv.Key].Count > 6 ? " ..." : ""));
        if (counts.Count == 0) Console.WriteLine("  none");
        Console.WriteLine("Files scanned: " + (seen.Count + 1));
        return 0;
    }

    // ---------- the drawing ----------
    static int MakeAssembly(string asmFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }
        string bomTpl = Path.Combine(Path.GetDirectoryName(template), "Unilumin-BOM.sldbomtbt");
        if (!File.Exists(bomTpl)) { Console.Error.WriteLine("REFUSED: no BOM template " + bomTpl + " (save-table-templates first)"); return 2; }
        var t0 = DateTime.Now;
        geomCache.Clear(); geomScale.Clear();

        // 1. Model.
        var asm = OpenDoc(asmFile, config, true);
        if (asm.ConfigurationManager.ActiveConfiguration.Name != config) asm.ShowConfiguration2(config);
        var props = new Dictionary<string, string>();
        foreach (var name in new[] { "PartNo", "Description", "Weight", "Finish" })
        { props[name] = Prop(asm, config, name); Console.WriteLine("  " + name + " = " + (props[name] == "" ? "(missing)" : props[name])); }
        if (props["PartNo"] == "") { Console.Error.WriteLine("REFUSED: no PartNo; no draft"); return 2; }
        ((AssemblyDoc)asm).ResolveAllLightWeightComponents(false);
        double[] box = null;
        foreach (Component2 c in ((object[])((AssemblyDoc)asm).GetComponents(false)) ?? new object[0])
        {
            if (c.IsSuppressed() || c.IsHidden(true) || ((c.GetChildren() as object[]) ?? new object[0]).Length > 0) continue;
            var cb = c.GetBox(false, false) as double[];
            if (cb == null) continue;
            box = box == null ? (double[])cb.Clone() : new[] { Math.Min(box[0], cb[0]), Math.Min(box[1], cb[1]), Math.Min(box[2], cb[2]), Math.Max(box[3], cb[3]), Math.Max(box[4], cb[4]), Math.Max(box[5], cb[5]) };
        }
        if (box == null) { Console.Error.WriteLine("REFUSED: no visible parts"); return 2; }
        leafBoxes = Leaves(((object[])((AssemblyDoc)asm).GetComponents(true) ?? new object[0]).Cast<Component2>())
            .Select(c => c.GetBox(false, false) as double[]).Where(b => b != null).ToList();
        Console.WriteLine("Visible parts measured: " + leafBoxes.Count);
        Func<double[], double> boxLen = b => Math.Max(b[3] - b[0], Math.Max(b[4] - b[1], b[5] - b[2]));
        memberBox = leafBoxes.OrderByDescending(boxLen).FirstOrDefault();
        var leafList = Leaves(((object[])((AssemblyDoc)asm).GetComponents(true) ?? new object[0]).Cast<Component2>()).Where(c => c.GetBox(false, false) != null).ToList();
        var memberComp = leafList.OrderByDescending(c => boxLen((double[])c.GetBox(false, false))).FirstOrDefault();
        memberName = memberComp != null ? memberComp.Name2 : null;
        var ext = new[] { box[3] - box[0], box[4] - box[1], box[5] - box[2] };
        int thin = Array.IndexOf(ext, ext.Min());
        string faceView = thin == 0 ? "*Right" : thin == 1 ? "*Top" : "*Front";
        Console.WriteLine("Assembly " + Mm(ext[0]) + " x " + Mm(ext[1]) + " x " + Mm(ext[2]) + "; face view " + faceView);
        Func<string> stamp = () => ((DateTime.Now - t0).TotalSeconds.ToString("0") + " s");
        Console.WriteLine("[" + stamp() + "] model read");

        // 2. Face view and BOM first (the BOM's size decides the space under it).
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var main = drw.CreateDrawViewFromModelView3(asmFile, faceView, 0.300, 0.170, 0);
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        var bom = main.InsertBomTable4(false, BorderX0, BorderY1, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
            (int)swBomType_e.swBomType_TopLevelOnly, config, bomTpl, false, 0, false);
        if (bom == null) problems.Add("BOM not inserted");
        else ((Annotation)((TableAnnotation)bom).GetAnnotation()).Layer = DraftLayerEnsure(drwDoc);
        if (bom != null) SortBomByPartNo((TableAnnotation)bom);   // James/Grant 15 Nov: alphabetical by part number, all assemblies; balloons come after, so they follow
        drwDoc.ForceRebuild3(true);
        var bomBox = BomBox(bom);
        int items = bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0;

        // 3. The plan, worked out from the model's size (Grant 14 Nov): the isometric centred under the BOM, from the
        //    left border to about grid line 5.5, with room for balloon columns and a top row; the projections (front,
        //    top above, bottom below, left and right sides) in the rest, with room for dimensions between them.
        //    Each gets the largest of James's scales that fits; the projection block is centred in its space.
        double xA, yB, zC;                        // face width, face height, depth (model mm along the face view)
        if (thin == 2) { xA = ext[0]; yB = ext[1]; zC = ext[2]; }
        else if (thin == 1) { xA = ext[0]; yB = ext[2]; zC = ext[1]; }
        else { xA = ext[2]; yB = ext[1]; zC = ext[0]; }
        const double m = 0.004, dimRoom = 0.024, padL = 0.018, padR = 0.016, padT = 0.014, isoZoneRight = 0.185;
        double isoW = 0.7071 * (ext[0] + ext[2]), isoH = 0.8165 * ext[1] + 0.4082 * (ext[0] + ext[2]);   // standard isometric of the model's box
        double izX0 = BorderX0 + m, izX1 = isoZoneRight, izY0 = RevTop + 0.012, izY1 = (bomBox != null ? bomBox[1] : BorderY1) - 0.010;
        double pzX0 = isoZoneRight + 0.005, pzX1 = BorderX1 - m, pzY0 = TitleTop + m, pzY1 = BorderY1 - m;
        int isoDen = AsmScales.FirstOrDefault(d => isoW / d + padL + padR <= izX1 - izX0 && isoH / d + padT <= izY1 - izY0);
        // A long member (LED member: one size at least LongRatio times the next): front, right side and end (top)
        // views only, as James draws them (08X06-LMW-02); the plates and holes are dimensioned down its length.
        var sorted = ext.OrderByDescending(e => e).ToArray();
        bool longMember = sorted[0] >= LongRatio * sorted[1];
        if (longMember) Console.WriteLine("Long member (" + (sorted[0] / sorted[1]).ToString("0.0") + " x): front, side and end views; plates and holes dimensioned down its length");
        Func<int, bool> projFits = d => longMember
            ? (zC + xA) / d + 3 * dimRoom <= pzX1 - pzX0 && (zC + yB) / d + 2 * dimRoom <= pzY1 - pzY0
            : (2 * zC + xA) / d + 3 * dimRoom <= pzX1 - pzX0 && (2 * zC + yB) / d + 2 * dimRoom <= pzY1 - pzY0;
        int den = AsmScales.FirstOrDefault(projFits);
        if (isoDen == 0) { isoDen = AsmScales.Last(); problems.Add("isometric too big for the space under the BOM even at 1:" + isoDen); }
        if (den == 0) { den = AsmScales.Last(); problems.Add("projections too big even at 1:" + den); }
        double sA = xA / den, sB = yB / den, sC = zC / den;
        double blockW = (longMember ? 1 : 2) * sC + sA + 3 * dimRoom, blockH = (longMember ? 1 : 2) * sC + sB + 2 * dimRoom + (longMember ? 0.012 : 0);   // long: room above the end view for its dimensions
        double fx = pzX0 + (pzX1 - pzX0 - blockW) / 2 + (longMember ? 0 : sC + dimRoom) + sA / 2;     // face view centre
        double fy = pzY0 + (pzY1 - pzY0 - blockH) / 2 + (longMember ? 0 : sC + dimRoom) + sB / 2;
        double ix = (izX0 + izX1) / 2 + (padL - padR) / 2, iy = (izY0 + izY1) / 2 - padT / 2;
        Console.WriteLine("[" + stamp() + "] plan: projections 1:" + den + " (block " + Mm(blockW) + " x " + Mm(blockH) + " mm in " + Mm(pzX1 - pzX0) + " x " + Mm(pzY1 - pzY0)
            + "), isometric 1:" + isoDen + " (" + Mm(isoW / isoDen) + " x " + Mm(isoH / isoDen) + " mm in " + Mm(izX1 - izX0) + " x " + Mm(izY1 - izY0) + ")");

        // 4. Draw it: scale, face view in place, the four projections, the isometric.
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 0; main.ScaleRatio = new double[] { 1, den };
        drwDoc.ForceRebuild3(false);
        CentreAt(drwDoc, main, fx, fy);
        Func<double, double, View> project = (x, y) =>
        {
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            var pv = drw.CreateUnfoldedViewAt3(x, y, 0, false);
            drwDoc.ClearSelection2(true);
            return pv;
        };
        var left = longMember ? null : project(fx - sA / 2 - dimRoom - sC / 2, fy);
        var right = project(fx + sA / 2 + dimRoom + sC / 2, fy);
        var top = project(fx, fy + sB / 2 + dimRoom + sC / 2);
        var bottom = longMember ? null : project(fx, fy - sB / 2 - dimRoom - sC / 2);
        main.UseSheetScale = 1;
        drwDoc.ForceRebuild3(false);
        geomCache.Clear(); geomScale.Clear();
        // put each projection exactly in its planned place (a view's position isn't the middle of what it draws)
        CentreAt(drwDoc, main, fx, fy);
        if (left != null) CentreAt(drwDoc, left, fx - sA / 2 - dimRoom - sC / 2, double.NaN);
        if (right != null) CentreAt(drwDoc, right, fx + sA / 2 + dimRoom + sC / 2, double.NaN);
        if (top != null) CentreAt(drwDoc, top, double.NaN, fy + sB / 2 + dimRoom + sC / 2);
        if (bottom != null) CentreAt(drwDoc, bottom, double.NaN, fy - sB / 2 - dimRoom - sC / 2);
        drwDoc.ForceRebuild3(false);
        var isoF = drw.CreateDrawViewFromModelView3(asmFile, "*Isometric", ix, iy, 0);
        if (isoF != null)
        {
            isoF.ReferencedConfiguration = config;
            if (isoDen == den) isoF.UseSheetScale = 1; else { isoF.UseSheetScale = 0; isoF.ScaleRatio = new double[] { 1, isoDen }; }
            drwDoc.ForceRebuild3(false);
            CentreAt(drwDoc, isoF, ix, iy);
            drwDoc.ForceRebuild3(false);
        }
        if (longMember && right != null)
        {
            // Grant 14 Nov "make the views bigger": the member gets the full height of the space (the end view goes
            // beside it, not above), the largest scale at which it fits broken, and breaks only as long as needed
            // Grant 15 Nov (08X06-LMW-01-grant): the member's top about 15 mm below the space's top, the holes-across
            // rows about 15 mm under its bottom
            const double labelRoom = 0.0155;
            const double acrossRoom = 0.0225;
            double memberH = pzY1 - pzY0 - labelRoom - acrossRoom;
            Func<int, bool> widthFits = d => (xA + zC + (top != null ? xA : 0)) / 1000.0 / d * 1000 / 1000 + 3 * dimRoom + 0.010 <= pzX1 - pzX0;
            den = BreakLongMember(drwDoc, drw, sheet, main, right, den, memberH, widthFits);
            geomCache.Clear(); geomScale.Clear();
            // re-place, side by side and centred: face | side (plate dims on its right) | end view
            if (top != null) top.RemoveAlignment();
            double wM = W(OutlineOf(main)), wS = W(OutlineOf(right)), wT = top != null ? W(OutlineOf(top)) : 0;
            // Grant's placing (08X06-LMW-01-grant, 15 Nov): the face view about 49 mm into the space, the side view
            // 49.5 mm on (centre to centre), the end view 77.7 mm on from that, clear of the side view's labels; all
            // moved left together if the end view's labels would run off the space
            const double faceIn = 0.049, faceToSide = 0.0495, sideToEnd = 0.0777, endDims = 0.016;
            double fx0 = pzX0 + faceIn, yMid = (pzY0 + acrossRoom + pzY1 - labelRoom) / 2;
            double over = (top != null ? fx0 + faceToSide + sideToEnd + wT / 2 : fx0 + faceToSide + wS / 2) + endDims - pzX1;
            if (over > 0) fx0 -= over;
            CentreAt(drwDoc, main, fx0, yMid);
            drwDoc.ForceRebuild3(false);
            CentreAt(drwDoc, right, fx0 + faceToSide, double.NaN);
            if (top != null)
            {
                var so = OutlineOf(right);
                CentreAt(drwDoc, top, fx0 + faceToSide + sideToEnd, so[3] - 0.014 - H(OutlineOf(top)) / 2);   // a little below the side view's top
            }
            drwDoc.ForceRebuild3(false);
            geomCache.Clear(); geomScale.Clear();
        }
        Console.WriteLine("[" + stamp() + "] views drawn: face, left " + (left != null) + ", right " + (right != null) + ", top " + (top != null) + ", bottom " + (bottom != null) + ", isometric " + (isoF != null));

        // Note block (in the isometric, so its links read that view's model): beside the BOM, else at the bottom left.
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        Annotation noteAnn = null;
        if (isoF != null)
        {
            drw.ActivateView(isoF.GetName2());
            lm.SetCurrentLayer(DraftLayerEnsure(drwDoc));
            var note = drwDoc.InsertNote("<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"" + (isoDen != den ? "\n<FONT size=8PTS>Isometric scale: <FONT size=3.5>1:" + isoDen : "")) as Note;
            lm.SetCurrentLayer("");
            if (note == null) problems.Add("note block not placed");
            else
            {
                noteAnn = (Annotation)note.GetAnnotation();
                var tf = (TextFormat)noteAnn.GetTextFormat(0); tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212; noteAnn.SetTextFormat(0, false, tf);
                if (longMember) noteAnn.SetPosition2(BorderX0 + 0.006, (bomBox != null ? bomBox[1] : BorderY1) - 0.005, 0);   // long member: top left, under the BOM (Grant 15 Nov)
                else noteAnn.SetPosition2((bomBox != null ? bomBox[2] : BorderX0) + 0.008, BorderY1 - m, 0);
            }
        }

        // 5. Balloons on the isometric, in columns round it.
        balloonNotes = new List<Note>();
        if (isoF != null && items > 0)
        {
            drw.ActivateView(isoF.GetName2());
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(isoF.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            var bo = drw.CreateAutoBalloonOptions();
            bo.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Circle;
            bo.IgnoreMultiple = true;
            bo.Style = (int)swBalloonStyle_e.swBS_Circular;
            bo.Size = (int)swBalloonFit_e.swBF_2Chars;
            bo.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
            bo.Layername = DraftLayer;
            balloonNotes = ((drw.AutoBalloon5(bo) as object[]) ?? new object[0]).Cast<Note>().ToList();
            drwDoc.ClearSelection2(true);
            SpreadBalloons(drwDoc, isoF, balloonNotes, noteAnn);
        }
        Console.WriteLine("[" + stamp() + "] balloons: " + balloonNotes.Count + " for " + items + " BOM items");
        if (balloonNotes.Count < items) problems.Add((items - balloonNotes.Count) + " BOM items without a balloon");

        // 6. Overall sizes (face view: width and height; left view: depth), flags, revision row.
        lm.SetCurrentLayer(DraftLayer);
        var dimsOk = new List<string>();
        var want = new[] { ext[0] * 1000, ext[1] * 1000, ext[2] * 1000 };
        bool[] mem;
        if (longMember)
        {
            // James's front view of an LED member looks at the plates' faces (holes visible): use whichever of the
            // face view and the side view shows more holes
            var plateView = right != null && ViewCirclesAsm(right).Count > ViewCirclesAsm(main).Count ? right : main;
            Console.WriteLine("  plates dimensioned on " + plateView.GetName2() + (plateView == right ? " (the side view shows the plates' holes)" : ""));
            mem = new[] { PlateOrdinates(drwDoc, drw, plateView, dimsOk), true };
            // the RHS itself (Grant 14 Nov): its width in both views, its length on the face view. Grant 15 Nov "all
            // holes should be clearly defined": each view's holes are also placed across (a row from the RHS edge,
            // which gives the width too) and sized, and the end view's holes both ways
            calledOut.Clear();
            bool mainAcross = HolesAcross(drwDoc, drw, main, main == plateView ? null : memberName, dimsOk);
            bool rightAcross = right != null && HolesAcross(drwDoc, drw, right, right == plateView ? null : memberName, dimsOk);
            RhsDims(drwDoc, drw, main, true, !mainAcross, dimsOk);
            if (right != null) RhsDims(drwDoc, drw, right, false, !rightAcross, dimsOk);
            if (top != null) HdgCallout(drwDoc, drw, top, dimsOk);
            foreach (var hv in new[] { main, right }.Where(x => x != null && x != plateView)) MemberHoles(drwDoc, drw, hv, dimsOk);   // Grant 14 Nov: holes in the RHS
            HoleSizes(drwDoc, drw, plateView, null, plateView.GetName2() == main.GetName2() ? -1 : +1, dimsOk);   // the plates' holes
        }
        else mem = MemberOrdinates(drwDoc, drw, main, dimsOk);
        if (top != null && longMember && EndViewHoles(drwDoc, drw, top, dimsOk)) { }   // the top plate's holes both ways, its size in brackets
        else if (top != null)
        {
            var tm = MemberOrdinates(drwDoc, drw, top, dimsOk);   // Grant 14 Nov: the top view as well
            if (!tm[0] || !tm[1]) { OverallByClick(drwDoc, drw, top, !tm[1], !tm[0], want, dimsOk); missingGroups.RemoveAll(g => g.StartsWith(top.GetName2())); }   // plain overall size where members failed
        }
        Console.WriteLine("[" + stamp() + "] member positions");
        if (!mem[0] || !mem[1]) OverallByClick(drwDoc, drw, main, !mem[1], !mem[0], want, dimsOk);   // plain overall size where members failed
        if (left != null) OverallByClick(drwDoc, drw, left, true, false, want, dimsOk);
        else if (right != null && !longMember) OverallByClick(drwDoc, drw, right, true, false, want, dimsOk);
        Console.WriteLine("[" + stamp() + "] overall dimensions");
        var flags = new List<string> { missingGroups.Count > 0 ? "CHECK: NOT DIMENSIONED: " + string.Join(", ", missingGroups).ToUpper().Replace("DRAWING VIEW", "VIEW ") : null, longMember ? null : MemberCentres ? "CHECK: MEMBER DIMS TO CENTRELINES" + (centreFallbacks.Count > 0 ? " (EXCEPT " + string.Join(", ", centreFallbacks).ToUpper().Replace("DRAWING VIEW", "VIEW ") + ": EDGES)" : "") : "CHECK: MEMBER DIMS TO UNDERSIDE / LEFT EDGE", "CHECK: WELDS NOT ADDED" };
        flags = flags.Where(f => f != null).ToList();
        foreach (var g in missingGroups) problems.Add("not dimensioned: " + g);
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        var flagNote = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);
        var taken = new List<double[]> { bomBox };
        if (noteAnn != null) { var np = (double[])noteAnn.GetPosition(); taken.Add(new[] { np[0] - 0.002, np[1] - 0.030, np[0] + 0.110, np[1] + 0.002 }); }   // generous: the text estimate ran short (08X06-LMW-02)
        foreach (var vw in new[] { main, left, right, top, bottom, isoF }) if (vw != null) taken.Add(longMember && vw == isoF ? OutlineOf(vw) : ExtentTrue(vw));   // long member: the note block (part of the isometric) sits top left now; its own box is above
        if (flagNote != null) PlaceFlags((Annotation)flagNote.GetAnnotation(), string.Join("\n", flags), taken, longMember ? "BL" : null);   // long member: bottom left, above the revision table (Grant 15 Nov)
        // nothing may overlap: views with each other, the BOM, the title block
        var boxes = new[] { main, left, right, top, bottom, isoF }.Where(v => v != null).Select(v => Tuple.Create(v.GetName2(), OutlineOf(v))).ToList();
        for (int i = 0; i < boxes.Count; i++)
        {
            var bx = boxes[i].Item2;
            if (bomBox != null && bx[0] < bomBox[2] && bx[2] > bomBox[0] && bx[1] < bomBox[3] && bx[3] > bomBox[1]) problems.Add("layout: " + boxes[i].Item1 + " overlaps the BOM");
            if (bx[2] > TitleLeft && bx[1] < TitleTop) problems.Add("layout: " + boxes[i].Item1 + " runs into the title block");
            if (bx[0] < BorderX0 || bx[2] > BorderX1 || bx[1] < BorderY0 || bx[3] > BorderY1) problems.Add("layout: " + boxes[i].Item1 + " runs off the sheet");
            for (int j = i + 1; j < boxes.Count; j++)
            {
                var by = boxes[j].Item2;
                if (bx[0] < by[2] && bx[2] > by[0] && bx[1] < by[3] && bx[3] > by[1]) problems.Add("layout: " + boxes[i].Item1 + " overlaps " + boxes[j].Item1);
            }
        }

        // 7. Check and save. The BOM is sorted again last: a rebuild during the run put it back in assembly order (15 Nov)
        if (bom != null) { drwDoc.ForceRebuild3(true); SortBomByPartNo((TableAnnotation)bom); drwDoc.ForceRebuild3(true); }
        foreach (var d in dimsOk) Console.WriteLine("  " + d);
        bool ok = items > 0 && !dimsOk.Any(d => d.Contains("MISMATCH"));
        Console.WriteLine("Check BOM: " + items + " items" + (items > 0 ? " OK" : " EMPTY"));
        foreach (var d in dimsOk.Where(d => d.Contains("NOT PLACED"))) problems.Add(d);
        if (!ok)
        {
            problems.Add((items == 0 ? "empty BOM" : "a dimension doesn't match the model") + ": NOT saved");
            int e2 = 0, w2 = 0; string fail = Path.ChangeExtension(output, ".FAILED.pdf");
            if (!File.Exists(fail)) drwDoc.Extension.SaveAs3(fail, 0, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e2, ref w2);
            return 1;
        }
        return SaveDraft(drw, drwDoc, output, pdfOut, t0);
    }

    // A named view in the (read-only, never saved) model: the isometric turned half round, to see the back.
    static string MakeBackIsoView(ModelDoc2 asm)
    {
        try
        {
            const string name = "Draw back iso";
            asm.ShowNamedView2("*Isometric", -1);
            var mv = (ModelView)asm.ActiveView;
            var mu = (MathUtility)sw.GetMathUtility();
            var axis = (MathVector)mu.CreateVector(new double[] { 0, 1, 0 });
            var origin = (MathPoint)mu.CreatePoint(new double[] { 0, 0, 0 });
            var half = (MathTransform)mu.CreateTransformRotateAxis(origin, axis, Math.PI);
            var t = mv.Orientation3;
            mv.Orientation3 = (MathTransform)half.Multiply(t);
            asm.DeleteNamedView(name);
            asm.NameView(name);
            Console.WriteLine("Back isometric: named view '" + name + "' made in the open model (not saved)");
            return name;
        }
        catch (Exception ex) { Console.WriteLine("Back isometric not made: " + ex.Message); return null; }
    }

    // A view's box: what it draws plus its annotations.
    static double[] ExtentTrue(View v)
    {
        var o = OutlineOf(v); var e = Extent(v);
        var vo = (double[])v.GetOutline();
        // Extent() starts from the view outline; for assembly views replace that part with the drawn box
        double x0 = o[0], y0 = o[1], x1 = o[2], y1 = o[3];
        foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
        {
            var p = a.GetPosition() as double[];
            if (p == null) continue;
            var dd = a.GetDisplayData() as DisplayData;
            double w = 0.004, h = 0.004;
            for (int i = 0; dd != null && i < dd.GetTextCount(); i++) { w = Math.Max(w, (dd.GetTextAtIndex(i) ?? "").Length * dd.GetTextHeightAtIndex(i) * 0.6); h += dd.GetTextHeightAtIndex(i) * 1.4; }
            x0 = Math.Min(x0, p[0] - 0.003); x1 = Math.Max(x1, p[0] + w); y0 = Math.Min(y0, p[1] - h); y1 = Math.Max(y1, p[1] + 0.003);
        }
        return new[] { x0, y0, x1, y1 };
    }

    // James's assembly arrangement. Returns null when it all fits, else the reason.
    static string LayoutAsm(ModelDoc2 doc, View main, View side, View top, View bottom, View isoF, View isoB, double[] bomBox, Annotation noteAnn)
    {
        const double g = 0.015, m = 0.004, dimRoom = 0.018;   // dimRoom: space for dimensions between and beside the profiles (Grant 14 Nov)
        doc.ForceRebuild3(false);
        var mo = OutlineOf(main);
        double[] so = side != null ? OutlineOf(side) : null, to = top != null ? OutlineOf(top) : null, bo = bottom != null ? OutlineOf(bottom) : null;
        // right block: top / face / bottom stacked, side to the right of the face
        double blockW = W(mo) + (so != null ? g + W(so) : 0);
        double blockH = H(mo) + (to != null ? dimRoom + H(to) : 0) + (bo != null ? dimRoom + H(bo) : 0);
        double regionB = TitleTop + m, regionT = BorderY1 - m;
        if (blockH > regionT - regionB) return "views too tall (" + Mm(blockH) + " mm)";
        double faceL = BorderX1 - m - dimRoom - W(mo);                   // side view goes to the left of the face view (Grant); room for the height dimension on the right
        double baseY = regionB + (regionT - regionB - blockH) / 2;      // block centred up and down
        double faceCy = baseY + (bo != null ? H(bo) + dimRoom : 0) + H(mo) / 2;
        CentreAt(doc, main, faceL + W(mo) / 2, faceCy);
        mo = OutlineOf(main);
        if (side != null) CentreAt(doc, side, mo[0] - dimRoom - W(so) / 2, double.NaN);
        if (top != null) CentreAt(doc, top, double.NaN, mo[3] + dimRoom + H(to) / 2);
        if (bottom != null) CentreAt(doc, bottom, double.NaN, mo[1] - dimRoom - H(bo) / 2);
        double blockL = side != null ? OutlineOf(side)[0] : mo[0];
        if (bomBox != null && bomBox[2] + g > blockL && regionT > bomBox[1]) return "views run into the BOM";
        // left region: under the BOM, left of the views
        double lx0 = BorderX0 + m, lx1 = Math.Min(blockL - g, TitleLeft - m), ly1 = (bomBox != null ? bomBox[1] : BorderY1) - g, ly0 = RevTop + 0.012;   // left of the title block, clear of the revision table
        if (noteAnn != null)
        {
            // beside the BOM if there is room before the views, else under it
            double nx = (bomBox != null ? bomBox[2] : lx0) + 0.008, ny = BorderY1 - m;
            var sideTop = side != null ? OutlineOf(side)[3] : 0;
            if (nx + 0.065 > blockL - 0.005 && !(nx + 0.065 < TitleLeft && ny - 0.022 > Math.Max(sideTop, OutlineOf(main)[3]) + 0.005))
            { nx = lx0 + 0.005; ny = ly1; ly1 = ly1 - 0.022 - g; }
            noteAnn.SetPosition2(nx, ny, 0);
            doc.ForceRebuild3(false);
        }
        if (lx1 - lx0 < 0.05) return "no room left of the views";
        var isos = new[] { isoF, isoB }.Where(x => x != null).ToList();
        if (isos.Count == 0) return null;
        // the front isometric carries the balloons: keep room for a column each side and a row on top (Grant 14 Nov:
        // balloons not over the part)
        const double padL = 0.018, padR = 0.016, padT = 0.014;
        var real = isos.Select(OutlineOf).ToList();
        var io = real.Select((b, i) => isos[i] == isoF ? new[] { b[0] - padL, b[1], b[2] + padR, b[3] + padT } : b).ToList();
        Func<int, double, double, Tuple<double, double>> realCentre = (i, px, py) =>
            Tuple.Create(px + ((real[i][0] + real[i][2]) - (io[i][0] + io[i][2])) / 2, py + ((real[i][1] + real[i][3]) - (io[i][1] + io[i][3])) / 2);
        double rw = lx1 - lx0, rh = ly1 - ly0;
        double sideW = io.Sum(b => W(b)) + g * (io.Count - 1), sideH = io.Max(b => H(b));
        double stackW = io.Max(b => W(b)), stackH = io.Sum(b => H(b)) + g * (io.Count - 1);
        if (sideW <= rw && sideH <= rh)
        {   // side by side, centred in the region
            double x = lx0 + (rw - sideW) / 2;
            for (int i = 0; i < isos.Count; i++) { var c = realCentre(i, x + W(io[i]) / 2, ly0 + rh / 2); CentreAt(doc, isos[i], c.Item1, c.Item2); x += W(io[i]) + g; }
        }
        else if (stackW <= rw && stackH <= rh)
        {
            double y = ly1 - (rh - stackH) / 2;
            for (int i = 0; i < isos.Count; i++) { var c = realCentre(i, lx0 + rw / 2, y - H(io[i]) / 2); CentreAt(doc, isos[i], c.Item1, c.Item2); y -= H(io[i]) + g; }
        }
        else return "isometrics don't fit left of the views (" + Mm(rw) + " x " + Mm(rh) + " mm free)";
        // nothing into the title block
        foreach (var iv in isos) { var b = OutlineOf(iv); if (b[2] > TitleLeft && b[1] < TitleTop) return "an isometric runs into the title block"; }
        return null;
    }

    // Balloons outside the isometric's drawn box, in the same order round it as the parts they point to, evenly
    // spaced, so the leaders fan out without crossing; clear of the note block and on the paper.
    static void SpreadBalloons(ModelDoc2 doc, View iso, List<Note> notes, Annotation noteAnn)
    {
        if (notes.Count == 0) return;
        var io = OutlineOf(iso);
        double cx = (io[0] + io[2]) / 2, cy = (io[1] + io[3]) / 2, rx = W(io) / 2 + 0.010, ry = H(io) / 2 + 0.010;
        var tips = new List<Tuple<Annotation, double>>();
        foreach (var n in notes)
        {
            var a = (Annotation)n.GetAnnotation();
            var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
            var pos = (double[])a.GetPosition();
            double tx = cx, ty = cy;
            if (lp != null && lp.Length >= 3)
            {
                int best = 0; double bd = -1;
                for (int i = 0; i + 2 < lp.Length; i += 3) { double d = Math.Abs(lp[i] - pos[0]) + Math.Abs(lp[i + 1] - pos[1]); if (d > bd) { bd = d; best = i; } }
                tx = lp[best]; ty = lp[best + 1];
            }
            tips.Add(Tuple.Create(a, Math.Atan2((ty - cy) / ry, (tx - cx) / rx)));
        }
        // Grant's arrangement (08B04-SFA-2T, 14 Nov): balloons in tidy columns either side of the isometric, each
        // level with its part and kept in height order (leaders run nearly flat and never cross); parts near the top
        // middle get a row along the top, in left-to-right order. About 10 mm between balloons.
        const double pitch = 0.010;
        var tipsXY = new List<Tuple<Annotation, double, double>>();
        foreach (var n in notes)
        {
            var a = (Annotation)n.GetAnnotation();
            var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
            var pos = (double[])a.GetPosition();
            double tx = cx, ty = cy;
            if (lp != null && lp.Length >= 3)
            {
                int best = 0; double bd = -1;
                for (int i = 0; i + 2 < lp.Length; i += 3) { double d = Math.Abs(lp[i] - pos[0]) + Math.Abs(lp[i + 1] - pos[1]); if (d > bd) { bd = d; best = i; } }
                tx = lp[best]; ty = lp[best + 1];
            }
            tipsXY.Add(Tuple.Create(a, tx, ty));
        }
        double colL = io[0] - 0.010, colR = io[2] + 0.008, rowT = io[3] + 0.008;
        var left = tipsXY.Where(t => t.Item2 < cx - 0.15 * W(io)).ToList();
        var right = tipsXY.Where(t => t.Item2 > cx + 0.15 * W(io)).ToList();
        var topRow = tipsXY.Where(t => !left.Contains(t) && !right.Contains(t)).ToList();
        // a column holds what fits in the view's height; extras go to the top row
        int perCol = Math.Max(1, (int)(H(io) / pitch));
        foreach (var col in new[] { left, right })
            while (col.Count > perCol) { var lo = col.OrderBy(t => t.Item3).Last(); col.Remove(lo); topRow.Add(lo); }
        double yMin = Math.Max(RevTop + 0.010, io[1]), yMax = Math.Min(BorderY1 - 0.006, io[3]);
        Func<List<Tuple<Annotation, double, double>>, double, int> placeColumn = (col, x) =>
        {
            var ord = col.OrderBy(t => t.Item3).ToList();
            var ys = ord.Select(t => Math.Max(yMin, Math.Min(yMax, t.Item3))).ToList();
            for (int pass = 0; pass < 100; pass++)
            {
                bool ch = false;
                for (int i = 1; i < ys.Count; i++) if (ys[i] - ys[i - 1] < pitch - 1e-9) { double push = (pitch - (ys[i] - ys[i - 1])) / 2; ys[i - 1] -= push; ys[i] += push; ch = true; }
                if (ys.Count > 0 && ys[0] < yMin) { double d = yMin - ys[0]; for (int i = 0; i < ys.Count; i++) ys[i] += d; ch = true; }
                if (ys.Count > 0 && ys[ys.Count - 1] > yMax + pitch * 2) { double d = ys[ys.Count - 1] - (yMax + pitch * 2); for (int i = 0; i < ys.Count; i++) ys[i] -= d; ch = true; }
                if (!ch) break;
            }
            int k = 0;
            for (int i = 0; i < ord.Count; i++) if (ord[i].Item1.SetPosition2(Math.Max(BorderX0 + 0.006, x), ys[i], 0)) k++;
            return k;
        };
        int moved = placeColumn(left, colL) + placeColumn(right, colR);
        var tordered = topRow.OrderBy(t => t.Item2).ToList();
        var xs = tordered.Select(t => t.Item2).ToList();
        for (int pass = 0; pass < 100; pass++)
        {
            bool ch = false;
            for (int i = 1; i < xs.Count; i++) if (xs[i] - xs[i - 1] < pitch - 1e-9) { double push = (pitch - (xs[i] - xs[i - 1])) / 2; xs[i - 1] -= push; xs[i] += push; ch = true; }
            if (!ch) break;
        }
        double[] nb = noteAnn != null ? Extent1(noteAnn) : null;
        double ry0 = rowT;   // the layout keeps room for this row above the isometric
        for (int i = 0; i < tordered.Count; i++) if (tordered[i].Item1.SetPosition2(xs[i], Math.Min(BorderY1 - 0.006, ry0), 0)) moved++;
        int nB = tipsXY.Count;
        Console.WriteLine("Balloons: " + left.Count + " in the left column, " + right.Count + " in the right, " + tordered.Count + " along the top");
        doc.ForceRebuild3(false);
        Console.WriteLine("Balloons placed round the isometric in order: " + moved + " of " + nB);
    }

    static double[] Extent1(Annotation a)
    {
        var p = (double[])a.GetPosition();
        var dd = a.GetDisplayData() as DisplayData;
        double x1 = p[0], y0 = p[1];
        for (int i = 0; dd != null && i < dd.GetTextCount(); i++)
        {
            string t = dd.GetTextAtIndex(i) ?? ""; double h = dd.GetTextHeightAtIndex(i);
            x1 = Math.Max(x1, p[0] + t.Length * h * 0.6); y0 = Math.Min(y0, p[1] - h * (i + 1) * 1.5);
        }
        return new[] { p[0], y0, x1, p[1] };
    }

    // The BOM in alphabetical order of part number (Grant 15 Nov, all assemblies), description second, items renumbered 1, 2, 3...
    static void SortBomByPartNo(TableAnnotation ta)
    {
        int dc = Enumerable.Range(0, ta.ColumnCount).Where(c => Flat(ta.Text[0, c]).Trim().ToUpper() == "DESCRIPTION").DefaultIfEmpty(-1).First();
        int pc = Enumerable.Range(0, ta.ColumnCount).Where(c => Flat(ta.Text[0, c]).Trim().ToUpper() == "PARTNO").DefaultIfEmpty(-1).First();
        if (pc < 0) { problems.Add("BOM: no PartNo column to sort on"); return; }
        var bt = (BomTableAnnotation)ta;
        var sd = (BomTableSortData)bt.GetBomTableSortData();
        sd.set_ColumnIndex(0, pc); sd.set_Ascending(0, true);   // Grant 15 Nov: by part number, description second
        sd.set_ColumnIndex(1, dc); sd.set_Ascending(1, true);
        sd.set_ColumnIndex(2, -1);
        sd.SortMethod = (int)swBomTableSortMethod_e.swBomTableSortMethod_Literal;
        sd.DoNotChangeItemNumber = false;
        bool ok = bt.Sort(sd);
        var rows = Enumerable.Range(1, ta.RowCount - 1).Select(r => Flat(ta.Text[r, pc])).ToList();
        bool sorted = rows.Zip(rows.Skip(1), (a, b) => string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0).All(x => x);
        Console.WriteLine("BOM sorted by part number: " + (ok && sorted ? "OK" : "NOT SORTED (" + ok + ")") + ": " + string.Join(" | ", rows));
        if (!(ok && sorted)) problems.Add("BOM not in alphabetical order");
    }

    static double[] BomBox(object bom)
    {
        if (bom == null) return null;
        var ta = (TableAnnotation)bom;
        var ann = (Annotation)ta.GetAnnotation();
        double w = Enumerable.Range(0, ta.ColumnCount).Sum(c => ta.GetColumnWidth(c)), h = Enumerable.Range(0, ta.RowCount).Sum(r => ta.GetRowHeight(r));
        var p = (double[])ann.GetPosition();
        return new[] { p[0], p[1] - h, p[0] + w, p[1] };
    }

    // Overall width (and height) of a view: the outermost straight edges, picked by clicking the middle of each as a
    // person would (selecting a part's edge object picked the wrong instance, 14 Nov). The value must equal one of
    // the model's overall sizes, or the draft isn't saved.
    static void OverallByClick(ModelDoc2 doc, DrawingDoc drw, View v, bool width, bool height, double[] modelMm, List<string> report)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        var o = OutlineOf(v);
        Func<Tuple<Edge, double[], double[]>, double[]> mid = l => new[] { (l.Item2[0] + l.Item3[0]) / 2, (l.Item2[1] + l.Item3[1]) / 2 };
        // Click each edge at several points along it (a crossing member can be under any one point); keep the first
        // pair that gives a straight dimension equal to the size measured from the model's edges.
        Action<Tuple<Edge, double[], double[]>, Tuple<Edge, double[], double[]>, double, double, string, double> dim = (l0, l1, x, y, what, expect) =>
        {
            drw.ActivateView(v.GetName2());
            var fr = new[] { 0.12, 0.3, 0.45, 0.62, 0.8, 0.93 };
            Func<Tuple<Edge, double[], double[]>, double, double[]> at = (l, f) => new[] { l.Item2[0] + (l.Item3[0] - l.Item2[0]) * f, l.Item2[1] + (l.Item3[1] - l.Item2[1]) * f };
            var smg = (SelectionMgr)doc.SelectionManager;
            for (int i = -1; i < fr.Length; i++)
            {
                doc.ClearSelection2(true);
                if (i < 0)
                {   // first: the edge objects themselves, in this view
                    var sd = (SelectData)smg.CreateSelectData(); sd.View = v;
                    if (!((Entity)l0.Item1).Select4(false, sd) || !((Entity)l1.Item1).Select4(true, sd)) { Console.WriteLine("    " + what + ": direct select failed"); continue; }
                }
                else
                {
                    var p0 = at(l0, fr[i]); var p1 = at(l1, fr[(i + 2) % fr.Length]);
                    if (!doc.Extension.SelectByID2("", "EDGE", p0[0], p0[1], 0, false, 0, null, 0)) continue;
                    if (!doc.Extension.SelectByID2("", "EDGE", p1[0], p1[1], 0, true, 0, null, 0)) continue;
                }
                var dd = doc.AddDimension2(x, y, 0) as DisplayDimension;
                doc.ClearSelection2(true);
                if (dd == null) continue;
                double val = ((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000;
                int ty = dd.GetType();
                bool straight = ty == (int)swDimensionType_e.swLinearDimension || ty == (int)swDimensionType_e.swHorLinearDimension || ty == (int)swDimensionType_e.swVertLinearDimension;
                Console.WriteLine("    " + what + " try " + (i < 0 ? "direct" : fr[i].ToString("0.00")) + ": type " + ty + " value " + val.ToString("0.#") + " (want " + expect.ToString("0.#") + ")");
                if (straight && Math.Abs(val - expect) < 0.5)
                {
                    bool inModel = modelMm.Any(mm => Math.Abs(mm - val) < 0.5);
                    report.Add(v.GetName2() + " " + what + " " + val.ToString("0.#") + " OK" + (inModel ? " (= model overall size)" : " (outermost edges; model box " + string.Join("/", modelMm.Select(mm => mm.ToString("0"))) + ")"));
                    return;
                }
                ((Annotation)dd.GetAnnotation()).Select3(false, null);
                doc.Extension.DeleteSelection2(0);
            }
            report.Add(v.GetName2() + " " + what + ": NOT PLACED (no clean pick; " + expect.ToString("0.#") + " wanted)");
        };
        var vert = ls.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7 && Math.Abs(l.Item2[1] - l.Item3[1]) > 0.0002).ToList();   // short edges count too: a 40 mm stub top is 1.3 mm at 1:30 (14 Nov)
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (width && vert.Count >= 2)
        {
            var l0 = vert.OrderBy(l => l.Item2[0]).First(); var l1 = vert.OrderBy(l => l.Item2[0]).Last();
            dim(l0, l1, (o[0] + o[2]) / 2, o[3] + 0.010, "overall width", (l1.Item2[0] - l0.Item2[0]) / v.ScaleDecimal * 1000);
        }
        if (height && hor.Count >= 2)
        {
            var l0 = hor.OrderBy(l => l.Item2[1]).First(); var l1 = hor.OrderBy(l => l.Item2[1]).Last();
            dim(l0, l1, o[2] + 0.012, (o[1] + o[3]) / 2, "overall height", (l1.Item2[1] - l0.Item2[1]) / v.ScaleDecimal * 1000);
        }
    }

    // Member positions on an assembly face view (Grant 14 Nov, James's 08A04-SFA-2T style): ordinates up the right
    // side to the underside of each horizontal member, and across the top to the left side of each upright, from
    // the bottom-left; the last of each in brackets (the overall size, reference). Members are the view's long
    // straight edges (at least MinMemberFrac of the view); edges closer than MemberGapMm belong to one member.
    // Every value is checked against the geometry; a group that doesn't check is removed (the caller then puts the
    // plain overall size instead). Returns which directions were placed.
    const double MinMemberFrac = 0.30, MemberGapMm = 100;
    static bool MemberCentres;
    static List<string> centreFallbacks = new List<string>(), missingGroups = new List<string>();   // --centrelines: dimension to each member's centreline instead of its underside / left side (Grant 14 Nov)
    static bool[] MemberOrdinates(ModelDoc2 doc, DrawingDoc drw, View v, List<string> report)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        var o = OutlineOf(v);
        double sc = v.ScaleDecimal;
        var placed = new[] { false, false };
        bool wantCentres = MemberCentres;
        for (int dir = 0; dir < 2; dir++)   // 0: heights of horizontal members (vertical ordinates), 1: positions of uprights
          for (int attempt = 0; attempt < (wantCentres ? 2 : 1); attempt++)
          {
            MemberCentres = wantCentres && attempt == 0;   // a centreline group that fails falls back to edges
            if (placed[dir]) break;
            bool horiz = dir == 0;
            double span = horiz ? W(o) : H(o);
            Func<Tuple<Edge, double[], double[]>, double> pos = l => horiz ? l.Item2[1] : l.Item2[0];
            Func<Tuple<Edge, double[], double[]>, double> len = l => horiz ? Math.Abs(l.Item3[0] - l.Item2[0]) : Math.Abs(l.Item3[1] - l.Item2[1]);
            var all = ls.Where(l => horiz ? Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 : Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).ToList();
            // a member passing behind others is drawn in pieces: add up the pieces at the same position (14 Nov)
            var longL = all.GroupBy(l => Math.Round(pos(l) * 1e5))
                .Where(g => g.Sum(len) >= MinMemberFrac * span)
                .Select(g => g.OrderByDescending(len).First()).OrderBy(pos).ToList();
            if (all.Count < 2 || longL.Count == 0) { report.Add(v.GetName2() + " member " + (horiz ? "heights" : "positions") + ": no members found"); continue; }
            // datum: the outermost edge at the bottom / left; the far end: the outermost at the top / right
            var datum = all.OrderBy(pos).First(); var far = all.OrderBy(pos).Last();
            // members: clusters of long edges; each member's underside / left side
            // members: clusters of long edges (both faces of a member are within MemberGapMm)
            var clusters = new List<List<Tuple<Edge, double[], double[]>>>();
            double gap = MemberGapMm / 1000 * sc;
            foreach (var l in longL)
                if (clusters.Count == 0 || pos(l) - pos(clusters.Last().Last()) > gap) clusters.Add(new List<Tuple<Edge, double[], double[]>> { l }); else clusters.Last().Add(l);
            clusters = clusters.Where(c => pos(c[0]) - pos(datum) > 1e-5 && pos(far) - pos(c[0]) > 1e-5).ToList();
            // what each ordinate attaches to: the underside / left edge, or (--centrelines) a centreline drawn between
            // the member's two faces, which stays tied to them
            var items = new List<object> { datum.Item1 };
            var expect = new List<double> { 0 };
            if (MemberCentres) { drw.ActivateView(v.GetName2()); }
            int centred = 0;
            foreach (var c in clusters)
            {
                var lo = c.First(); var hi = c.Last();
                if (MemberCentres && c.Count >= 2 && pos(hi) - pos(lo) > 1e-5)
                {
                    doc.ClearSelection2(true);
                    var sdc = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sdc.View = v;
                    ((Entity)lo.Item1).Select4(false, sdc); ((Entity)hi.Item1).Select4(true, sdc);
                    var cl = drw.InsertCenterLine2();
                    doc.ClearSelection2(true);
                    if (cl != null)
                    {
                        var an = (Annotation)cl.GetAnnotation();
                        an.Layer = DraftLayer;
                        // a centreline is picked by clicking on it (like the slot centre marks): points along it
                        double mid = (pos(lo) + pos(hi)) / 2;
                        var pts = new[] { 0.3, 0.5, 0.7, 0.15, 0.85 }.Select(f => horiz
                            ? new[] { lo.Item2[0] + (lo.Item3[0] - lo.Item2[0]) * f, mid }
                            : new[] { mid, lo.Item2[1] + (lo.Item3[1] - lo.Item2[1]) * f }).ToList();
                        items.Add(pts); expect.Add(Math.Round((mid - pos(datum)) / sc * 1000, 1)); centred++;
                        continue;
                    }
                }
                items.Add(lo.Item1); expect.Add(Math.Round((pos(lo) - pos(datum)) / sc * 1000, 1));
            }
            items.Add(far.Item1); expect.Add(Math.Round((pos(far) - pos(datum)) / sc * 1000, 1));
            if (MemberCentres) Console.WriteLine("  " + v.GetName2() + " " + (horiz ? "heights" : "positions") + ": " + centred + " of " + clusters.Count + " members by centreline");
            // select and place
            drw.ActivateView(v.GetName2());
            doc.ClearSelection2(true);
            var smg = (SelectionMgr)doc.SelectionManager;
            var sd = (SelectData)smg.CreateSelectData(); sd.View = v;
            int sel = 0;
            for (int i = 0; i < items.Count; i++)
            {
                bool ok1 = false;
                if (items[i] is List<double[]>)
                {
                    foreach (var q in (List<double[]>)items[i])
                    {
                        int nBefore = smg.GetSelectedObjectCount2(-1);
                        if (doc.Extension.SelectByID2("", "CENTERLINES", q[0], q[1], 0, i > 0, 0, null, 0) && smg.GetSelectedObjectCount2(-1) > nBefore) { ok1 = true; break; }
                    }
                }
                else ok1 = ((Entity)items[i]).Select4(i > 0, sd);
                if (ok1) sel++;
            }
            int kind = horiz ? (int)swAddOrdinateDims_e.swVerticalOrdinate : (int)swAddOrdinateDims_e.swHorizontalOrdinate;
            double x = horiz ? o[2] + 0.010 : pos(datum), y = horiz ? pos(datum) : o[3] + 0.008;
            if (!horiz && y + 0.012 > BorderY1) y = o[1] - 0.008;   // no room above: below the view (08X06-LMW-02 end view)
            var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
            int r;
            try { r = doc.Extension.AddOrdinateDimension(kind, x, y, 0); }
            catch (System.Runtime.InteropServices.COMException ex) { r = -99; Console.WriteLine("    SOLIDWORKS refused the ordinates (" + ex.Message + ")"); }   // 14 Nov: RPC_E_SERVERFAULT on a busy top view
            doc.ClearSelection2(true);
            var made = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
                .Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !before.Contains(a.GetName())).ToList();
            var got = made.Select(a => Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 1)).OrderBy(z => z).ToList();
            bool ok = sel == items.Count && r == 0 && string.Join(",", got) == string.Join(",", expect.OrderBy(z => z));
            string what = v.GetName2() + " member " + (horiz ? "heights" : "positions") + (MemberCentres ? " (centrelines)" : "") + " " + string.Join(" / ", expect);
            if (ok)
            {
                // the far end is the overall size: in brackets (reference), as James
                foreach (var a in made)
                {
                    var dd = (DisplayDimension)a.GetSpecificAnnotation();
                    if (Math.Abs(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000 - expect.Last()) < 0.05) dd.ShowParenthesis = true;
                }
                report.Add(what + " OK");
                placed[dir] = true;
                if (wantCentres && !MemberCentres) centreFallbacks.Add(v.GetName2() + " " + (horiz ? "heights" : "positions"));   // placed, but to edges
            }
            else
            {
                report.Add(what + ": REMOVED (selected " + sel + "/" + items.Count + ", result " + r + ", got " + string.Join("/", got) + ")"
                    + (MemberCentres ? "; trying member edges instead" : ""));
                doc.ClearSelection2(true);
                foreach (var a in made) a.Select3(true, null);
                doc.Extension.DeleteSelection2(0);
                if (!MemberCentres || !wantCentres) missingGroups.Add(v.GetName2() + " " + (horiz ? "heights" : "positions"));   // nothing more to try
            }
          }
        MemberCentres = wantCentres;
        return placed;
    }

    const double LongRatio = 5, PlateClusterMm = 150;   // LED mount plates are about 100 long (08X06-LMW-02)

    // Down a long member, as James (08X06-LMW-02): 0 at the far end of the top, then for each plate along it the
    // plate's near edge in brackets (reference) and the holes in it, then the far end in brackets. The member runs
    // up and down the face view. Every value is checked against the geometry; if they don't match, nothing is kept.
    static bool PlateOrdinates(ModelDoc2 doc, DrawingDoc drw, View v, List<string> report)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        var circ = ViewCirclesAsm(v);
        var o = OutlineOf(v);
        double sc = v.ScaleDecimal;
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (hor.Count < 2) { report.Add(v.GetName2() + " plate positions: no edges found"); return false; }
        // Grant 15 Nov: where edges or holes line up, the one nearest the dimension line (here on the right), so no
        // extension line crosses the part
        Func<Tuple<Edge, double[], double[]>, double> rx = l => Math.Max(l.Item2[0], l.Item3[0]);
        var datum = hor.OrderByDescending(l => Math.Round(l.Item2[1] * 1e6)).ThenByDescending(rx).First();      // the top
        var far = hor.OrderBy(l => Math.Round(l.Item2[1] * 1e6)).ThenByDescending(rx).First();                   // the bottom end
        // clusters of horizontal edges (one per plate) between the ends, by height
        var mid = hor.Where(l => datum.Item2[1] - l.Item2[1] > 1e-5 && l.Item2[1] - far.Item2[1] > 1e-5).OrderByDescending(l => Math.Round(l.Item2[1] * 1e6)).ThenByDescending(rx).ToList();
        var clusters = new List<List<Tuple<Edge, double[], double[]>>>();
        foreach (var l in mid)
            if (clusters.Count == 0 || clusters.Last().Last().Item2[1] - l.Item2[1] > PlateClusterMm / 1000 * sc) clusters.Add(new List<Tuple<Edge, double[], double[]>> { l }); else clusters.Last().Add(l);
        var items = new List<object> { datum.Item1 };
        var expect = new List<double> { 0 };
        var refVals = new HashSet<double>();
        Func<double, double> val = y => Math.Round((datum.Item2[1] - y) / sc * 1000, 1);
        // a cluster touching the top datum is the top plate: its underside (plate thickness) counts, not in brackets
        foreach (var c in clusters)
        {
            var edge = c.First();                                   // the plate's top (near) edge
            double v0 = val(edge.Item2[1]);
            if (!expect.Contains(v0)) { items.Add(edge.Item1); expect.Add(v0); if (v0 > PlateClusterMm) refVals.Add(v0); }
            double yTop = c.First().Item2[1], yBot = c.Last().Item2[1];
            foreach (var h in circ.Where(ci => ci.Item2[1] <= yTop + 1e-6 && ci.Item2[1] >= yBot - 1e-6).GroupBy(ci => Math.Round(ci.Item2[1] * 1e5)).Select(g => g.OrderByDescending(ci => ci.Item2[0]).First()))
            {
                double hv = val(h.Item2[1]);
                if (!expect.Contains(hv)) { items.Add(h.Item1); expect.Add(hv); }
            }
        }
        // holes outside any plate (in the member itself)
        foreach (var h in circ.GroupBy(ci => Math.Round(ci.Item2[1] * 1e5)).Select(g => g.OrderByDescending(ci => ci.Item2[0]).First()))
        {
            double hv = val(h.Item2[1]);
            if (!expect.Contains(hv) && h.Item2[1] < datum.Item2[1] && h.Item2[1] > far.Item2[1]) { items.Add(h.Item1); expect.Add(hv); }
        }
        double fv = val(far.Item2[1]);
        items.Add(far.Item1); expect.Add(fv); refVals.Add(fv);
        drw.ActivateView(v.GetName2());
        doc.ClearSelection2(true);
        var smg = (SelectionMgr)doc.SelectionManager;
        var sd = (SelectData)smg.CreateSelectData(); sd.View = v;
        int sel = 0;
        for (int i = 0; i < items.Count; i++) if (((Entity)items[i]).Select4(i > 0, sd)) sel++;
        var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
        int r;
        try { r = doc.Extension.AddOrdinateDimension((int)swAddOrdinateDims_e.swVerticalOrdinate, o[2] + 0.010, datum.Item2[1], 0); }
        catch (System.Runtime.InteropServices.COMException) { r = -99; }
        doc.ClearSelection2(true);
        var made = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
            .Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !before.Contains(a.GetName())).ToList();
        var got = made.Select(a => Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 1)).OrderBy(z => z).ToList();
        string what = v.GetName2() + " plates and holes down the member " + string.Join(" / ", expect.OrderBy(z => z).Select(z => refVals.Contains(z) ? "(" + z + ")" : z.ToString()));
        if (sel == items.Count && r == 0 && string.Join(",", got) == string.Join(",", expect.OrderBy(z => z)))
        {
            foreach (var a in made)
            {
                var dd = (DisplayDimension)a.GetSpecificAnnotation();
                double gv = Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1);
                if (refVals.Contains(gv)) dd.ShowParenthesis = true;
            }
            report.Add(what + " OK");
            return true;
        }
        report.Add(what + ": REMOVED (selected " + sel + "/" + items.Count + ", result " + r + ", got " + string.Join("/", got) + ")");
        doc.ClearSelection2(true);
        foreach (var a in made) a.Select3(true, null);
        doc.Extension.DeleteSelection2(0);
        missingGroups.Add(v.GetName2() + " plates");
        return false;
    }

    // The member (RHS/SHS) of a long member: its width across (between its two long edges, below the view) and, if
    // asked, its length (from the top plate's underside, or its own top, to its bottom end, left of the view).
    // Positions are given in the view's unbroken edge coordinates (a broken view maps them; sheet coordinates put a
    // dimension inside the view, 14 Nov). Each value is checked against the edges; a mismatch is removed.
    static void RhsDims(ModelDoc2 doc, DrawingDoc drw, View v, bool length, bool width, List<string> report)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        double sc = v.ScaleDecimal;
        var vert = ls.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).ToList();
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (vert.Count < 2 || hor.Count < 2) { report.Add(v.GetName2() + " RHS: no edges"); return; }
        Func<Tuple<Edge, double[], double[]>, double> vlen = l => Math.Abs(l.Item2[1] - l.Item3[1]);
        double maxLen = vert.Max(vlen);
        var longV = vert.Where(l => vlen(l) >= 0.5 * maxLen).OrderBy(l => l.Item2[0]).ToList();   // the member's long faces
        double yBot = hor.Min(l => l.Item2[1]);
        Action<Tuple<Edge, double[], double[]>, Tuple<Edge, double[], double[]>, double, double, string, double> add = (a, b, x, y, what, expect) =>
        {
            drw.ActivateView(v.GetName2());
            doc.ClearSelection2(true);
            var sd = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sd.View = v;
            DisplayDimension dd = null;
            if (((Entity)a.Item1).Select4(false, sd) && ((Entity)b.Item1).Select4(true, sd))
                try { dd = doc.AddDimension2(x, y, 0) as DisplayDimension; } catch (System.Runtime.InteropServices.COMException) { }
            doc.ClearSelection2(true);
            double got = dd == null ? double.NaN : Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1);
            if (dd != null && Math.Abs(got - expect) < 0.05) { report.Add(v.GetName2() + " RHS " + what + " " + got + " OK"); return; }
            report.Add(v.GetName2() + " RHS " + what + ": REMOVED (got " + got + ", want " + expect + ")");
            if (dd != null) { ((Annotation)dd.GetAnnotation()).Select3(false, null); doc.Extension.DeleteSelection2(0); }
            missingGroups.Add(v.GetName2() + " RHS " + what);
        };
        var l0 = longV.First(); var l1 = longV.Last();
        if (width && l1.Item2[0] - l0.Item2[0] > 1e-5)
            add(l0, l1, (l0.Item2[0] + l1.Item2[0]) / 2, yBot - 0.010, "width", Math.Round((l1.Item2[0] - l0.Item2[0]) / sc * 1000, 1));
        if (length)
        {
            // the overall length, top plate to bottom (Grant 15 Nov, 08X06-LMW-02-grant: 2155 in place of the RHS's 2145,
            // the RHS length is in the BOM), 8 mm left of the member, inside the hole ordinates
            var topE = hor.OrderByDescending(l => l.Item2[1]).First();
            var botE = hor.OrderBy(l => l.Item2[1]).First();
            add(topE, botE, l0.Item2[0] - 0.0082, (topE.Item2[1] + botE.Item2[1]) / 2, "overall length", Math.Round((topE.Item2[1] - botE.Item2[1]) / sc * 1000, 1));
        }
    }

    // The HDG drainage hole in the top plate (Grant 14 Nov), as James writes it (08A06-HBW-01): "Ø10" over "for HDG
    // Drainage" on a leader. It's the hole in the end view that lies over the member's section (inside its box across
    // the length); the bolt holes and slots lie outside it. A diameter dimension, so the size comes from the model.
    static void HdgCallout(ModelDoc2 doc, DrawingDoc drw, View v, List<string> report)
    {
        if (memberBox == null) return;
        var ext = new[] { memberBox[3] - memberBox[0], memberBox[4] - memberBox[1], memberBox[5] - memberBox[2] };
        int along = Array.IndexOf(ext, ext.Max());
        var vt = (double[])((MathTransform)v.ModelToViewTransform).ArrayData;
        var compT = new Dictionary<object, double[]>();
        var holes = new List<Tuple<Edge, double[], double>>();
        foreach (var ce in ViewEdges(v))
        {
            var c = (Curve)ce.Item2.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])c.CircleParams;
            var own = ce.Item1 == null ? null : (((Entity)ce.Item2).GetComponent() as Component2 ?? ce.Item1);
            double[] ct = null;
            if (own != null && !compT.TryGetValue(own, out ct)) { var t2 = own.Transform2; ct = t2 == null ? null : (double[])t2.ArrayData; compT[own] = ct; }
            var a = Xf(ct, new[] { cp[0], cp[1], cp[2] });   // assembly space
            bool over = Enumerable.Range(0, 3).Where(i => i != along).All(i => a[i] > memberBox[i] + 0.001 && a[i] < memberBox[i + 3] - 0.001);
            if (over) holes.Add(Tuple.Create(ce.Item2, Xf(vt, a), cp[6]));
        }
        var dist = holes.GroupBy(h => Math.Round(h.Item2[0] * 1e5) + "," + Math.Round(h.Item2[1] * 1e5)).Select(g => g.First()).ToList();
        if (dist.Count == 0) { report.Add(v.GetName2() + " HDG hole: none over the member"); return; }
        var o = OutlineOf(v);
        int k = 0;   // each callout goes straight below the end view, under its ordinate row
        foreach (var h in dist)
        {
            drw.ActivateView(v.GetName2());
            doc.ClearSelection2(true);
            var sd = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sd.View = v;
            DisplayDimension dd = null;
            if (((Entity)h.Item1).Select4(false, sd))
                try { dd = doc.AddDiameterDimension2(o[0] - 0.0065 - 0.010 * k, o[3] + 0.0226 + 0.010 * k, 0) as DisplayDimension; } catch (System.Runtime.InteropServices.COMException) { }   // above the end view, to the left (Grant 15 Nov)
            doc.ClearSelection2(true);
            double want = Math.Round(2 * h.Item3 * 1000, 1);
            double got = dd == null ? double.NaN : Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1);
            if (dd != null && Math.Abs(got - want) < 0.05)
            {
                dd.SetBrokenLeader2(false, (int)swDisplayDimensionLeaderText_e.swBrokenLeaderHorizontalText);
                dd.SetText((int)swDimensionTextParts_e.swDimensionTextCalloutBelow, "for HDG Drainage");
                calledOut.Add(v.GetName2() + ":" + HoleKey(h.Item2));
                report.Add(v.GetName2() + " HDG hole Ø" + got + " OK");
            }
            else
            {
                report.Add(v.GetName2() + " HDG hole: REMOVED (got " + got + ", want Ø" + want + ")");
                if (dd != null) { ((Annotation)dd.GetAnnotation()).Select3(false, null); doc.Extension.DeleteSelection2(0); }
                missingGroups.Add(v.GetName2() + " HDG hole");
            }
            k++;
        }
    }

    // Holes in the member itself (Grant 14 Nov: "like on the RHS down the bottom") on a view that isn't the plate view:
    // ordinates down the left side from the top (0), and a callout per size "n x Ø d THRU ALL" (both walls) or
    // "THRU ONE WALL", worked out from the member's own edges (the far wall's holes are hidden in the view).
    static void MemberHoles(ModelDoc2 doc, DrawingDoc drw, View v, List<string> report)
    {
        if (memberName == null) return;
        var mu = (MathUtility)sw.GetMathUtility();
        var dist = FacingHoles(v, memberName).Where(h => !h.Slot).Select(h => Tuple.Create(h.E, h.C, h.Cp, h.Own)).ToList();   // slots: centres only (Grant 15 Nov)
        if (dist.Count == 0) return;
        var ls = ViewLines(mu, v);
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (hor.Count == 0) return;
        var datum = hor.OrderByDescending(l => Math.Round(l.Item2[1] * 1e6)).ThenBy(l => Math.Min(l.Item2[0], l.Item3[0])).First();
        double sc = v.ScaleDecimal;
        var o = OutlineOf(v);
        // ordinates: the top, then one hole per height, the one nearest the row on the left (Grant 15 Nov)
        var rows = dist.GroupBy(h => Math.Round(h.Item2[1] * 1e5)).Select(g => g.OrderBy(h => h.Item2[0]).First()).OrderByDescending(h => h.Item2[1]).ToList();
        var expect = new List<double> { 0 };
        expect.AddRange(rows.Select(h => Math.Round((datum.Item2[1] - h.Item2[1]) / sc * 1000, 1)));
        drw.ActivateView(v.GetName2());
        doc.ClearSelection2(true);
        var smg = (SelectionMgr)doc.SelectionManager;
        var sd = (SelectData)smg.CreateSelectData(); sd.View = v;
        int sel = ((Entity)datum.Item1).Select4(false, sd) ? 1 : 0;
        foreach (var h in rows) if (((Entity)h.Item1).Select4(true, sd)) sel++;
        var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
        int r;
        try { r = doc.Extension.AddOrdinateDimension((int)swAddOrdinateDims_e.swVerticalOrdinate, o[0] - 0.010, datum.Item2[1], 0); }
        catch (System.Runtime.InteropServices.COMException) { r = -99; }
        doc.ClearSelection2(true);
        var made = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
            .Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !before.Contains(a.GetName())).ToList();
        var got = made.Select(a => Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 1)).OrderBy(z => z).ToList();
        string what = v.GetName2() + " holes in the member " + string.Join(" / ", expect);
        if (sel == rows.Count + 1 && r == 0 && string.Join(",", got) == string.Join(",", expect.OrderBy(z => z))) report.Add(what + " OK");
        else
        {
            report.Add(what + ": REMOVED (selected " + sel + "/" + (rows.Count + 1) + ", result " + r + ", got " + string.Join("/", got) + ")");
            doc.ClearSelection2(true); foreach (var a in made) a.Select3(true, null); doc.Extension.DeleteSelection2(0);
            missingGroups.Add(v.GetName2() + " member holes");
        }
        HoleSizes(doc, drw, v, memberName, 2, report);   // right of the view (Grant 15 Nov, 08X06-LMW-02-grant)
    }

    // Holes seen face-on in a view (Grant 15 Nov: "all holes should be clearly defined"): round holes and slot ends
    // whose axis faces the viewer. Each circular edge is sampled on the sheet to see how much of a circle it draws:
    // a round hole's edges go all the way round, a slot end is half a circle, a plate's rounded corner only a quarter
    // (left out). ownerName: only holes in that component (the member). C is the centre in the view's edge coordinates.
    class FHole { public Edge E; public double[] C, Cp; public Component2 Own; public double R; public bool Slot; }
    static HashSet<string> calledOut = new HashSet<string>();   // holes already called out in a view (the HDG hole)
    static string HoleKey(double[] c) { return Math.Round(c[0] * 1e5) + "," + Math.Round(c[1] * 1e5); }
    static List<FHole> FacingHoles(View v, string ownerName)
    {
        var vt = (double[])((MathTransform)v.ModelToViewTransform).ArrayData;
        var compT = new Dictionary<object, double[]>();
        var arcs = new List<Tuple<FHole, bool[]>>();   // each circular edge facing the viewer, with the 30-degree sectors it covers
        foreach (var ce in ViewEdges(v))
        {
            var e = ce.Item2;
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])c.CircleParams;
            var own = ce.Item1 == null ? null : (((Entity)e).GetComponent() as Component2 ?? ce.Item1);
            if (ownerName != null && (own == null || own.Name2 != ownerName)) continue;
            double[] ct = null;
            if (own != null && !compT.TryGetValue(own, out ct)) { var t2 = own.Transform2; ct = t2 == null ? null : (double[])t2.ArrayData; compT[own] = ct; }
            var a1 = Xf(vt, Xf(ct, new[] { cp[0], cp[1], cp[2] }));
            var a2 = Xf(vt, Xf(ct, new[] { cp[0] + cp[3], cp[1] + cp[4], cp[2] + cp[5] }));
            double axZ = Math.Abs(a2[2] - a1[2]) / Math.Sqrt(Math.Pow(a2[0] - a1[0], 2) + Math.Pow(a2[1] - a1[1], 2) + Math.Pow(a2[2] - a1[2], 2) + 1e-30);
            if (axZ < 0.99) continue;   // edge-on: a hole seen from the side, the rounded corners at a member's end
            var pd = e.GetCurveParams3();
            var sec = new bool[12];
            for (int i = 0; i <= 16; i++)
            {
                double u = pd.UMinValue + (pd.UMaxValue - pd.UMinValue) * i / 16;
                var q = (double[])c.Evaluate2(u, 0);
                var qs = Xf(vt, Xf(ct, new[] { q[0], q[1], q[2] }));
                double ang = Math.Atan2(qs[1] - a1[1], qs[0] - a1[0]);
                sec[((int)Math.Floor((ang + Math.PI) / (Math.PI / 6)) % 12 + 12) % 12] = true;
            }
            arcs.Add(Tuple.Create(new FHole { E = e, C = a1, Cp = cp, Own = own, R = cp[6] }, sec));
        }
        var r = new List<FHole>();
        foreach (var g in arcs.GroupBy(a => HoleKey(a.Item1.C) + "," + Math.Round(a.Item1.R * 1e5)))
        {
            int n = Enumerable.Range(0, 12).Count(i => g.Any(a => a.Item2[i]));
            var h = g.First().Item1;
            if (n >= 11) r.Add(h);                                     // all the way round: a round hole
            else if (n >= 5 && n <= 8) { h.Slot = true; r.Add(h); }   // half a circle: a slot end
        }
        return r;
    }

    // An ordinate row: items[0] is the datum (0), the rest are checked against expect; values in refVals in brackets.
    // A row that doesn't check is removed and listed as not dimensioned.
    // An item that is a sheet point (double[]) is a slot's centre mark, picked by clicking it (as Section.cs).
    // moveToY: where a horizontal row's labels end up (a broken view places them in its unbroken coordinates).
    static bool OrdinateRow(ModelDoc2 doc, DrawingDoc drw, View v, bool vertical, List<object> items, List<double> expect, HashSet<double> refVals, double x, double y, string what, List<string> report, double moveToY = double.NaN)
    {
        drw.ActivateView(v.GetName2());
        doc.ClearSelection2(true);
        var smg0 = (SelectionMgr)doc.SelectionManager;
        var sd = (SelectData)smg0.CreateSelectData(); sd.View = v;
        int sel = 0;
        for (int i = 0; i < items.Count; i++)
        {
            bool ok1 = false;
            if (items[i] is double[])
            {
                var q = (double[])items[i];
                foreach (var off in new[] { new[] { 0.0, 0.0 }, new[] { 0.002, 0.0 }, new[] { -0.002, 0.0 }, new[] { 0.0, 0.002 }, new[] { 0.0, -0.002 } })
                {
                    int nb = smg0.GetSelectedObjectCount2(-1);
                    if (!doc.Extension.SelectByID2("", "CENTERMARKSYMS", q[0] + off[0], q[1] + off[1], 0, i > 0, 0, null, 0)) continue;
                    int na = smg0.GetSelectedObjectCount2(-1);
                    if (na > nb && smg0.GetSelectedObjectType3(na, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS) { ok1 = true; break; }
                }
            }
            else ok1 = ((Entity)items[i]).Select4(i > 0, sd);
            if (ok1) sel++;
        }
        var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
        int r;
        try { r = doc.Extension.AddOrdinateDimension(vertical ? (int)swAddOrdinateDims_e.swVerticalOrdinate : (int)swAddOrdinateDims_e.swHorizontalOrdinate, x, y, 0); }
        catch (System.Runtime.InteropServices.COMException) { r = -99; }
        doc.ClearSelection2(true);
        var made = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
            .Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !before.Contains(a.GetName())).ToList();
        Func<List<double>> values = () => made.Select(a => Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 1)).OrderBy(z => z).ToList();
        var got = values();
        if (r == 0 && string.Join(",", got) != string.Join(",", expect.OrderBy(z => z)))
        {
            // 15 Nov: SOLIDWORKS sometimes measures to the near side of a hole or slot end (22.5 for a slot at 30):
            // put every arc end on its centre and measure again
            foreach (var a in made)
            {
                var dm = (Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0);
                dm.SetArcEndCondition(1, (int)swArcEndCondition_e.swArcEndConditionCenter);
                dm.SetArcEndCondition(2, (int)swArcEndCondition_e.swArcEndConditionCenter);
            }
            doc.EditRebuild3();
            var got2 = values();
            Console.WriteLine("    " + v.GetName2() + " " + what + ": arc ends set to centres: " + string.Join("/", got) + " -> " + string.Join("/", got2));
            got = got2;
        }
        string w = v.GetName2() + " " + what + " " + string.Join(" / ", expect.OrderBy(z => z).Select(z => refVals.Contains(z) ? "(" + z + ")" : z.ToString()));
        if (sel == items.Count && r == 0 && string.Join(",", got) == string.Join(",", expect.OrderBy(z => z)))
        {
            foreach (var a in made)
            {
                var dd = (DisplayDimension)a.GetSpecificAnnotation();
                if (refVals.Contains(Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1))) dd.ShowParenthesis = true;
            }
            if (!double.IsNaN(moveToY))
            {
                foreach (var a in made) { var ap = (double[])a.GetPosition(); a.SetPosition2(ap[0], moveToY, 0); }
                doc.GraphicsRedraw2();
                Console.WriteLine("    " + v.GetName2() + " " + what + ": labels moved to " + (moveToY * 1000).ToString("0.0") + ", now at " + string.Join("/", made.Select(a => (((double[])a.GetPosition())[1] * 1000).ToString("0.0")).Distinct()));
            }
            report.Add(w + " OK");
            return true;
        }
        report.Add(w + ": REMOVED (selected " + sel + "/" + items.Count + ", result " + r + ", got " + string.Join("/", got) + ")");
        doc.ClearSelection2(true); foreach (var a in made) a.Select3(true, null); doc.Extension.DeleteSelection2(0);
        missingGroups.Add(v.GetName2() + " " + what);
        return false;
    }

    // Holes across a member view (Grant 15 Nov): an ordinate row under the view from the RHS's left face to each hole
    // centre, its right face last in brackets (so the row gives the RHS width too). If a hole lies outside the RHS (in a
    // plate standing proud of it), the row runs from the nearest edge left of the holes to the nearest right of them.
    // The off-centre check: each member hole's place across the RHS is reported. Returns true if the row was placed.
    static bool HolesAcross(ModelDoc2 doc, DrawingDoc drw, View v, string ownerName, List<string> report)
    {
        var holes = FacingHoles(v, ownerName).Where(h => !h.Slot).ToList();   // slots are placed by their centres only (Grant 15 Nov)
        if (holes.Count == 0) return false;
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        double sc = v.ScaleDecimal;
        var vert = ls.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7 && Math.Abs(l.Item2[1] - l.Item3[1]) > 0.0002).ToList();
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (vert.Count < 2 || hor.Count == 0) return false;
        Func<Tuple<Edge, double[], double[]>, double> vlen = l => Math.Abs(l.Item2[1] - l.Item3[1]);
        double maxLen = vert.Max(vlen);
        var longV = vert.Where(l => vlen(l) >= 0.5 * maxLen).OrderBy(l => l.Item2[0]).ToList();   // the RHS's long faces
        var l0 = longV.First(); var l1 = longV.Last();
        double hx0 = holes.Min(h => h.C[0]), hx1 = holes.Max(h => h.C[0]);
        // one row of holes at a time (14 Nov: picking edges and holes from different plates made SOLIDWORKS refuse the
        // row): the edges beside that row and, for each position across, the hole nearest it; next row if refused
        var rowsY = holes.Select(h => h.C[1]).GroupBy(y => Math.Round(y * 1e5)).Select(g => g.First()).OrderBy(y => y).Take(3).ToList();
        // Grant 15 Nov: from the bottom holes. SOLIDWORKS sometimes refuses a row once (RPC fault) and takes it the
        // second time, so the bottom row is tried twice before the rows above it
        var tries = new[] { rowsY[0] }.Concat(rowsY).ToList();
        for (int ti = 0; ti < tries.Count; ti++)
        {
        double ay = tries[ti];
        Func<Tuple<Edge, double[], double[]>, bool> beside = l => Math.Min(l.Item2[1], l.Item3[1]) <= ay + 1e-6 && Math.Max(l.Item2[1], l.Item3[1]) >= ay - 1e-6;
        var datum = hx0 >= l0.Item2[0] - 1e-6 ? l0 : vert.Where(l => l.Item2[0] < hx0 - 1e-6 && beside(l)).OrderByDescending(l => Math.Round(l.Item2[0] * 1e6)).ThenBy(l => Math.Min(l.Item2[1], l.Item3[1])).FirstOrDefault();
        var far = hx1 <= l1.Item2[0] + 1e-6 ? l1 : vert.Where(l => l.Item2[0] > hx1 + 1e-6 && beside(l)).OrderBy(l => Math.Round(l.Item2[0] * 1e6)).ThenBy(l => Math.Min(l.Item2[1], l.Item3[1])).FirstOrDefault();
        if (datum == null || far == null) continue;
        var pts = holes.GroupBy(h => Math.Round(h.C[0] * 1e5)).Select(g => g.OrderBy(h => Math.Abs(h.C[1] - ay)).First()).OrderBy(h => h.C[0]).ToList();
        if (ownerName != null && l1.Item2[0] - l0.Item2[0] > 1e-5 && ti == 0)
            foreach (var h in pts)
            {
                double off = Math.Round((h.C[0] - (l0.Item2[0] + l1.Item2[0]) / 2) / sc * 1000, 1);
                report.Add(v.GetName2() + " member hole " + (Math.Abs(off) < 0.05 ? "on the RHS centre line" : "OFF CENTRE by " + Math.Abs(off)) + " (checked in the model)");
            }
        Func<double, double> val = x => Math.Round((x - datum.Item2[0]) / sc * 1000, 1);
        var items = new List<object> { datum.Item1 };
        var expect = new List<double> { 0 };
        foreach (var h in pts) { double hv = val(h.C[0]); if (hv > 0.05 && !expect.Contains(hv)) { items.Add(h.E); expect.Add(hv); } }
        foreach (var face in new[] { l0, l1 })
        {
            double rv = val(face.Item2[0]);
            if (face != datum && face != far && rv > 0.05 && !expect.Contains(rv)) { items.Add(face.Item1); expect.Add(rv); }
        }
        double fv = val(far.Item2[0]);
        if (!expect.Contains(fv)) { items.Add(far.Item1); expect.Add(fv); }
        double yBot = hor.Min(l => l.Item2[1]);
        bool last = ti == tries.Count - 1;
        int nMissing = missingGroups.Count;
        if (OrdinateRow(doc, drw, v, false, items, expect, new HashSet<double> { fv }, datum.Item2[0], yBot - 0.010, "holes across", report, OutlineOf(v)[1] - 0.0146)) return true;   // labels about 15 mm under the member as drawn (Grant 15 Nov)
        if (!last) missingGroups.RemoveRange(nMissing, missingGroups.Count - nMissing);   // try the next row before giving up
        }
        if (missingGroups.All(g => g != v.GetName2() + " holes across")) { report.Add(v.GetName2() + " holes across: no edge to measure from"); missingGroups.Add(v.GetName2() + " holes across"); }
        return false;
    }

    // The end view of a long member (the top plate). Grant 15 Nov: "for the top plate we just need slot centers":
    // each slot's centre placed both ways from the plate's bottom-left corner, the far edges in brackets; round holes
    // by their centres too, except the HDG drainage hole (it has its own callout). Square to a slot, its end arc gives
    // the centre's position; along it, the slot's centre mark is picked (only one row per view can pick centre marks,
    // so that row goes first). Returns false (the caller dimensions the outline instead) if there's nothing to place.
    static bool EndViewHoles(ModelDoc2 doc, DrawingDoc drw, View v, List<string> report)
    {
        var holes = FacingHoles(v, null).Where(h => !calledOut.Contains(v.GetName2() + ":" + HoleKey(h.C))).ToList();
        if (holes.Count == 0) return false;
        // slot ends in pairs: same size, in line, nearest
        var ends = holes.Where(h => h.Slot).ToList();
        var used = new HashSet<FHole>();
        var slots = new List<Tuple<FHole, FHole>>();
        foreach (var a in ends)
        {
            if (used.Contains(a)) continue;
            var b = ends.Where(x => x != a && !used.Contains(x) && Math.Abs(x.R - a.R) < 1e-7 && (Math.Abs(x.C[0] - a.C[0]) < 1e-6 || Math.Abs(x.C[1] - a.C[1]) < 1e-6))
                        .OrderBy(x => Math.Pow(x.C[0] - a.C[0], 2) + Math.Pow(x.C[1] - a.C[1], 2)).FirstOrDefault();
            if (b == null) { problems.Add(v.GetName2() + ": a slot end without its partner"); continue; }
            used.Add(a); used.Add(b); slots.Add(Tuple.Create(a, b));
        }
        var round = holes.Where(h => !h.Slot).ToList();
        if (slots.Count == 0 && round.Count == 0) return false;
        var mu = (MathUtility)sw.GetMathUtility();
        var ls = ViewLines(mu, v);
        var o = OutlineOf(v);
        double sc = v.ScaleDecimal;
        var vert = ls.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7 && Math.Abs(l.Item2[1] - l.Item3[1]) > 0.0002).ToList();
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 && Math.Abs(l.Item2[0] - l.Item3[0]) > 0.0002).ToList();
        if (vert.Count < 2 || hor.Count < 2) return false;
        if (slots.Count > 0)
        {
            drw.ActivateView(v.GetName2());
            if (!v.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Slots, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0))
                problems.Add(v.GetName2() + ": slot centre marks not inserted");
            doc.ClearSelection2(true);
        }
        // per direction (0: across, measuring x; 1: up, measuring y): the targets and whether any is a centre-mark pick
        // nearness to each row (Grant 15 Nov: the hole nearest the dimension line): the across row is below the
        // view (lowest first), the up row on its right (right-most first)
        Func<int, double[], double> away = (d, q) => d == 0 ? q[1] : -q[0];
        var rows = new List<Tuple<bool, List<Tuple<object, double, double[]>>>>();
        for (int dir = 0; dir < 2; dir++)
        {
            int ci = dir;
            var t = new List<Tuple<object, double, double[]>>();
            foreach (var h in round) t.Add(Tuple.Create((object)h.E, h.C[ci], h.C));
            bool pick = false;
            foreach (var sl in slots)
            {
                double mid = (sl.Item1.C[ci] + sl.Item2.C[ci]) / 2;
                bool along = Math.Abs(sl.Item1.C[ci] - sl.Item2.C[ci]) > 1e-6;   // the slot runs this way: pick its centre mark
                var cpt = new[] { (sl.Item1.C[0] + sl.Item2.C[0]) / 2, (sl.Item1.C[1] + sl.Item2.C[1]) / 2 };
                t.Add(Tuple.Create((object)cpt, mid, cpt)); pick = true;   // Grant 15 Nov: a slot by its centre only, both ways, never its end arc
            }
            rows.Add(Tuple.Create(pick, t));
        }
        foreach (int dir in new[] { 0, 1 }.OrderBy(d => rows[d].Item1 ? 0 : 1))
        {
            bool vertical = dir == 1;   // a vertical row measures heights
            Func<Tuple<Edge, double[], double[]>, double> pos = l => vertical ? l.Item2[1] : l.Item2[0];
            var lines = vertical ? hor : vert;
            // of edge pieces in line, the one nearest the row
            Func<Tuple<Edge, double[], double[]>, double> near = l => vertical ? -Math.Max(l.Item2[0], l.Item3[0]) : Math.Min(l.Item2[1], l.Item3[1]);
            var datum = lines.OrderBy(l => Math.Round(pos(l) * 1e6)).ThenBy(near).First();
            var far = lines.OrderByDescending(l => Math.Round(pos(l) * 1e6)).ThenBy(near).First();
            Func<double, double> val = p => Math.Round((p - pos(datum)) / sc * 1000, 1);
            var items = new List<object> { datum.Item1 };
            var expect = new List<double> { 0 };
            int dd0 = dir;
            foreach (var tg in rows[dir].Item2.GroupBy(tg => Math.Round(tg.Item2 * 1e5)).Select(g => g.OrderBy(tg => tg.Item1 is double[] ? 1 : 0).ThenBy(tg => away(dd0, tg.Item3)).First()).OrderBy(tg => tg.Item2))
            {
                double hv = val(tg.Item2);
                if (hv > 0.05 && !expect.Contains(hv)) { items.Add(tg.Item1); expect.Add(hv); }
            }
            double fv = val(pos(far));
            if (!expect.Contains(fv)) { items.Add(far.Item1); expect.Add(fv); }
            OrdinateRow(doc, drw, v, vertical, items, expect, new HashSet<double> { fv },
                vertical ? o[2] + 0.010 : pos(datum), vertical ? pos(datum) : o[1] - 0.008, vertical ? "slot centres up the plate" : "slot centres across the plate", report);
        }
        HoleSizes(doc, drw, v, null, 0, report);
        return true;
    }

    // One callout per hole size in a view (Grant 15 Nov): "n x Ø d" and how far it goes, worked out from the part's
    // own circular edges on the hole's axis (each wall has two): in a member, both walls "THRU ALL", one "THRU ONE
    // WALL"; in a plate "THRU". A slot gets its width ("n x 12 WIDE SLOT THRU"); its end centres are placed by the
    // ordinates. Holes already called out (the HDG hole) are left out. side: -1 left of the view, +1 right below the
    // hole, 2 right above it (member holes, Grant 15 Nov), 0 below the view.
    static void HoleSizes(ModelDoc2 doc, DrawingDoc drw, View v, string ownerName, int side, List<string> report)
    {
        var all = FacingHoles(v, ownerName).Where(h => !calledOut.Contains(v.GetName2() + ":" + HoleKey(h.C))).ToList();
        if (all.Count == 0) return;
        var o = OutlineOf(v);
        drw.ActivateView(v.GetName2());
        var sd = (SelectData)((SelectionMgr)doc.SelectionManager).CreateSelectData(); sd.View = v;
        int k = 0;
        foreach (var g in all.GroupBy(h => (h.Slot ? "slot " : "") + Math.Round(h.R * 2000, 1)))
        {
            var h = side == 0 ? g.OrderByDescending(x => Math.Round(x.C[0] * 1e4)).ThenBy(x => x.C[1]).First()   // below the view: the right-hand one
                              : g.OrderBy(x => Math.Round(x.C[1] * 1e4)).ThenBy(x => side * -x.C[0]).First();   // beside it: the lowest, nearest the text's side (Grant 15 Nov)
            double dia = Math.Round(h.R * 2000, 1);
            int n = h.Slot ? (g.Count() + 1) / 2 : g.Count();   // a slot has two ends
            var along = new HashSet<double>();
            var body = h.Own == null ? null : h.Own.GetBody() as Body2;
            if (body != null)
                foreach (Edge e in (object[])body.GetEdges() ?? new object[0])
                {
                    var c2 = (Curve)e.GetCurve();
                    if (!c2.IsCircle()) continue;
                    var q = (double[])c2.CircleParams;
                    if (Math.Abs(q[6] - h.Cp[6]) > 1e-6) continue;
                    double dx = q[0] - h.Cp[0], dy = q[1] - h.Cp[1], dz = q[2] - h.Cp[2];
                    double ax = h.Cp[3], ay = h.Cp[4], az = h.Cp[5];
                    double cx = dy * az - dz * ay, cy = dz * ax - dx * az, cz = dx * ay - dy * ax;   // offset x axis: zero when on the axis
                    if (Math.Sqrt(cx * cx + cy * cy + cz * cz) < 1e-5) along.Add(Math.Round((dx * ax + dy * ay + dz * az) * 1e5));
                }
            int walls = along.Count / 2;
            bool member = h.Own != null && h.Own.Name2 == memberName;
            string thru = member ? (walls >= 2 ? " THRU ALL" : walls == 1 ? " THRU ONE WALL" : "") : (walls >= 1 ? " THRU" : "");
            if (thru == "") problems.Add(v.GetName2() + ": couldn't tell how far the Ø" + dia + " holes go");
            double px = side < 0 ? o[0] - 0.050 : side == 2 ? o[2] + 0.0143 : side > 0 ? o[2] + 0.030 : (o[0] + o[2]) / 2 + 0.0024;   // below: centred under the view (Grant 15 Nov)
            double py = side == 0 ? o[1] - 0.0276 - 0.010 * k : side == 2 ? h.C[1] + 0.0124 + 0.009 * k : side > 0 ? h.C[1] - 0.012 - 0.009 * k : h.C[1] + 0.016 + 0.009 * k;   // right: just below the hole, clear of the ordinate lines above it
            doc.ClearSelection2(true);
            DisplayDimension dd = null;
            if (((Entity)h.E).Select4(false, sd))
                try { dd = doc.AddDiameterDimension2(px, py, 0) as DisplayDimension; } catch (System.Runtime.InteropServices.COMException) { }
            doc.ClearSelection2(true);
            double got = dd == null ? double.NaN : Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1);
            string cnt = n > 1 ? n + " x " : "";
            string text = h.Slot ? cnt + dia + " WIDE SLOT" + thru : cnt + "Ø" + dia + thru;
            if (dd != null && Math.Abs(got - dia) < 0.05)
            {
                dd.SetBrokenLeader2(false, (int)swDisplayDimensionLeaderText_e.swBrokenLeaderHorizontalText);
                dd.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, h.Slot ? cnt : cnt + "<MOD-DIAM>");
                dd.SetText((int)swDimensionTextParts_e.swDimensionTextSuffix, (h.Slot ? " WIDE SLOT" : "") + thru);
                report.Add(v.GetName2() + " hole callout " + text + " OK (" + walls + " wall" + (walls == 1 ? "" : "s") + " in the model)");
            }
            else
            {
                report.Add(v.GetName2() + " hole callout " + text + ": REMOVED (got " + got + ")");
                if (dd != null) { ((Annotation)dd.GetAnnotation()).Select3(false, null); doc.Extension.DeleteSelection2(0); }
                missingGroups.Add(v.GetName2() + " " + (h.Slot ? "slot" : "Ø" + dia) + " size");
            }
            k++;
        }
    }

    // Circles in a view (holes), with their centres on the sheet, transformed as ViewLines does.
    static List<Tuple<Edge, double[]>> ViewCirclesAsm(View v)
    {
        var r = new List<Tuple<Edge, double[]>>();
        var vt = (double[])((MathTransform)v.ModelToViewTransform).ArrayData;
        var compT = new Dictionary<object, double[]>();
        foreach (var ce in ViewEdges(v))
        {
            var e = ce.Item2;
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])c.CircleParams;
            var own = ce.Item1 == null ? null : (((Entity)e).GetComponent() as Component2 ?? ce.Item1);
            double[] ct = null;
            if (own != null && !compT.TryGetValue(own, out ct)) { var t2 = own.Transform2; ct = t2 == null ? null : (double[])t2.ArrayData; compT[own] = ct; }
            r.Add(Tuple.Create(e, Xf(vt, Xf(ct, new[] { cp[0], cp[1], cp[2] }))));
        }
        return r;
    }

    // Break lines on a long member (Grant 14 Nov: "draw it and see"; James's 08X06-LMW-02 is broken): keep a zone round
    // every plate and hole and both ends, break out the empty lengths between them (at least BreakMinMm), then use the
    // largest of James's scales at which the shortened views fit the space. The face view and the side view get the
    // same breaks (they're aligned). Returns the scale used (unchanged if breaking didn't work).
    static int ForcedDen;
    static double[] memberBox;
    static string memberName;    // that part's component name   // assembly box of the longest visible part (the RHS/SHS of a long member)   // --scale=N: the scale for a long member's views (Grant 14 Nov: "what does it look like at 1:10")
    const double MaxBreakFrac = 1.0 / 3;   // the breaks may cut out at most this much of the member's length
    const double BreakMinMm = 150, BreakKeepMm = 40, BreakGapSheet = 0.003;   // 3 mm between break lines on paper
    static int BreakLongMember(ModelDoc2 doc, DrawingDoc drw, Sheet sheet, View main, View side, int den, double spaceH, Func<int, bool> widthFits)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var pv = ViewCirclesAsm(side).Count > ViewCirclesAsm(main).Count ? side : main;
        var ls = ViewLines(mu, pv);
        var circ = ViewCirclesAsm(pv);
        double sc0 = pv.ScaleDecimal, cy = ((double[])pv.Position)[1];
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).Select(l => l.Item2[1]).ToList();
        if (hor.Count < 2) return den;
        double yTop = hor.Max(), yBot = hor.Min();
        // features in model mm from the top: every horizontal edge except the long faces, and every hole
        var feats = hor.Concat(circ.Select(c => c.Item2[1])).Select(y => (yTop - y) / sc0 * 1000).Distinct().OrderBy(z => z).ToList();
        double L = (yTop - yBot) / sc0 * 1000;
        // keep zones, merged
        var keep = new List<double[]>();
        foreach (var f in feats)
        {
            double a = Math.Max(0, f - BreakKeepMm), b = Math.Min(L, f + BreakKeepMm);
            if (keep.Count > 0 && a <= keep.Last()[1]) keep.Last()[1] = Math.Max(keep.Last()[1], b); else keep.Add(new[] { a, b });
        }
        var gaps = new List<double[]>();
        for (int i = 1; i < keep.Count; i++) if (keep[i][0] - keep[i - 1][1] >= BreakMinMm) gaps.Add(new[] { keep[i - 1][1], keep[i][0] });
        if (gaps.Count == 0) { Console.WriteLine("Breaks: no empty length long enough"); return den; }
        double keptMin = L - gaps.Sum(g => g[1] - g[0]);
        Func<int, bool> fitsBroken = d => keptMin / 1000 / d + gaps.Count * BreakGapSheet <= spaceH && widthFits(d);
        Func<int, double> cutFrac = d => Math.Max(0, L - (spaceH - gaps.Count * BreakGapSheet) * d * 1000) / L;
        int den2 = ForcedDen > 0 ? ForcedDen
            : AsmScales.FirstOrDefault(d => fitsBroken(d) && cutFrac(d) <= MaxBreakFrac);   // Grant 14 Nov: 1:5 "a bit big", 1:10 "much better"
        if (den2 == 0) den2 = AsmScales.FirstOrDefault(fitsBroken);
        if (den2 != 0) Console.WriteLine("  scale rule: breaks cut out " + (cutFrac(den2) * 100).ToString("0") + "% at 1:" + den2 + " (at most " + (MaxBreakFrac * 100).ToString("0") + "%)");
        if (ForcedDen > 0 && keptMin / 1000 / ForcedDen + gaps.Count * BreakGapSheet > spaceH) problems.Add("1:" + ForcedDen + " (asked for) is too big even with every break");
        if (den2 == 0) den2 = AsmScales.Last();
        // at that scale, take out only what's needed to fill the height: each break shares the cut by its gap's length,
        // centred in its gap
        double need = L - (spaceH - gaps.Count * BreakGapSheet) * den2 * 1000;   // model mm to remove
        if (need <= 0) { Console.WriteLine("Breaks: none needed at 1:" + den2); gaps.Clear(); }
        else
        {
            double total = gaps.Sum(g => g[1] - g[0]);
            gaps = gaps.Select(g => { double c = (g[0] + g[1]) / 2, r = (g[1] - g[0]) * Math.Min(1, need / total); return new[] { c - r / 2, c + r / 2 }; }).ToList();
        }
        double keptMm = L - gaps.Sum(g => g[1] - g[0]);
        Console.WriteLine("Breaks: " + gaps.Count + " (" + string.Join(", ", gaps.Select(g => g[0].ToString("0") + "-" + g[1].ToString("0"))) + " mm from the top); kept length " + keptMm.ToString("0") + " of " + L.ToString("0") + " mm; scale 1:" + den + " -> 1:" + den2);
        // tight breaks (Grant 14 Nov: "too wide"): the gap between each pair of break lines, a drawing setting
        double gap0 = doc.Extension.GetUserPreferenceDouble((int)swUserPreferenceDoubleValue_e.swDetailingBreakLineGap, 0);
        doc.Extension.SetUserPreferenceDouble((int)swUserPreferenceDoubleValue_e.swDetailingBreakLineGap, 0, BreakGapSheet);
        Console.WriteLine("  break gap " + Mm(gap0) + " -> " + Mm(doc.Extension.GetUserPreferenceDouble((int)swUserPreferenceDoubleValue_e.swDetailingBreakLineGap, 0)) + " mm");
        // new scale first (the side and end views follow the face view), then the breaks at that scale
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den2, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 1;
        doc.ForceRebuild3(false);
        double k = pv.ScaleDecimal / sc0;
        foreach (var v in gaps.Count == 0 ? new View[0] : new[] { main, side })
        {
            geomCache.Clear(); geomScale.Clear();
            var trueB = OutlineOf(v); var outB = (double[])v.GetOutline();
            outlinePad[v.GetName2()] = new[] { trueB[0] - outB[0], trueB[1] - outB[1], outB[2] - trueB[2], outB[3] - trueB[3] };
            double vcy = ((double[])v.Position)[1];
            double topRel = (yTop - cy) * k + (((double[])pv.Position)[1] - cy) * 0;   // the member's top, relative to the view centre, at the new scale
            // the top of the member sits at the same height in both views (aligned); measure it in this view
            var lsv = ViewLines(mu, v).Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).Select(l => l.Item2[1]).ToList();
            double vTop = lsv.Count > 0 ? lsv.Max() : vcy + topRel;
            foreach (var g in gaps)
            {
                // break positions: paper units from the view centre (model units are refused, 14 Nov)
                double y1 = vTop - g[0] / 1000 * v.ScaleDecimal - vcy, y2 = vTop - g[1] / 1000 * v.ScaleDecimal - vcy;
                var bl = v.InsertBreak2((int)swBreakLineOrientation_e.swBreakLineHorizontal, y1, y2, (int)swBreakLineStyle_e.swBreakLine_ZigZag, 3);
                if (bl == null) Console.WriteLine("  break not inserted in " + v.GetName2() + " at " + g[0].ToString("0"));
                else Console.WriteLine("  break " + v.GetName2() + " asked " + Mm(y1) + "/" + Mm(y2) + ", got " + Mm((double)bl.GetPosition(0)) + "/" + Mm((double)bl.GetPosition(1)));
            }
            Console.WriteLine("  " + v.GetName2() + " break gap " + Mm(v.BreakLineGap) + " -> " + Mm(BreakGapSheet) + " mm");
            v.BreakLineGap = BreakGapSheet;   // each view keeps its own gap (the document setting didn't change it)
            doc.ClearSelection2(true);
            doc.Extension.SelectByID2(v.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            drw.BreakView();
            doc.ClearSelection2(true);
        }
        doc.ForceRebuild3(false);
        var mo = OutlineOf(main);
        double want = keptMm / 1000 * main.ScaleDecimal + gaps.Count * BreakGapSheet;
        if (gaps.Count > 0) Console.WriteLine("  broken: " + main.IsBroken() + "; face view now " + Mm(H(mo)) + " mm tall (expected about " + Mm(want) + ")");
        if (gaps.Count > 0 && !main.IsBroken()) problems.Add("break lines inserted but the view didn't break");
        return den2;
    }

    static string DraftLayerEnsure(ModelDoc2 drwDoc)
    {
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        return DraftLayer;
    }

    // Experiment kept for reference: which BOM settings fill the table (14 Nov: the configuration must be named).
    static int BomTest(string asmFile, string template)
    {
        var asm = OpenDoc(asmFile, "Default", true);
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var v = ((DrawingDoc)drwDoc).CreateDrawViewFromModelView3(asmFile, "*Front", 0.25, 0.15, 0);
        v.ReferencedConfiguration = "Default";
        drwDoc.ForceRebuild3(true);
        string james = Path.Combine(Path.GetDirectoryName(template), "Unilumin-BOM.sldbomtbt");
        foreach (var cfg in new[] { "", "Default" })
        {
            var ta = v.InsertBomTable4(false, 0.02, 0.28, 1, 2, cfg, james, false, 0, false) as TableAnnotation;
            drwDoc.ForceRebuild3(true);
            Console.WriteLine("config '" + cfg + "': " + (ta == null ? "not inserted" : ta.RowCount + " rows"));
        }
        return 0;
    }
}
