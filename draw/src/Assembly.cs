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
    static List<double[]> leafBoxes;   // visible parts' boxes in assembly coordinates
    static double[] OutlineOf(View v)
    {
        var comps = v.GetVisibleComponents() as object[];
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
        int den = AsmScales.FirstOrDefault(d => (2 * zC + xA) / d + 3 * dimRoom <= pzX1 - pzX0 && (2 * zC + yB) / d + 2 * dimRoom <= pzY1 - pzY0);
        if (isoDen == 0) { isoDen = AsmScales.Last(); problems.Add("isometric too big for the space under the BOM even at 1:" + isoDen); }
        if (den == 0) { den = AsmScales.Last(); problems.Add("projections too big even at 1:" + den); }
        double sA = xA / den, sB = yB / den, sC = zC / den;
        double blockW = 2 * sC + sA + 3 * dimRoom, blockH = 2 * sC + sB + 2 * dimRoom;
        double fx = pzX0 + (pzX1 - pzX0 - blockW) / 2 + sC + dimRoom + sA / 2;     // face view centre
        double fy = pzY0 + (pzY1 - pzY0 - blockH) / 2 + sC + dimRoom + sB / 2;
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
        var left = project(fx - sA / 2 - dimRoom - sC / 2, fy);
        var right = project(fx + sA / 2 + dimRoom + sC / 2, fy);
        var top = project(fx, fy + sB / 2 + dimRoom + sC / 2);
        var bottom = project(fx, fy - sB / 2 - dimRoom - sC / 2);
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
                noteAnn.SetPosition2((bomBox != null ? bomBox[2] : BorderX0) + 0.008, BorderY1 - m, 0);
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
        var mem = MemberOrdinates(drwDoc, drw, main, dimsOk);
        if (top != null) MemberOrdinates(drwDoc, drw, top, dimsOk);   // Grant 14 Nov: the top view as well
        Console.WriteLine("[" + stamp() + "] member positions");
        if (!mem[0] || !mem[1]) OverallByClick(drwDoc, drw, main, !mem[1], !mem[0], want, dimsOk);   // plain overall size where members failed
        if (left != null) OverallByClick(drwDoc, drw, left, true, false, want, dimsOk);
        Console.WriteLine("[" + stamp() + "] overall dimensions");
        var flags = new List<string> { MemberCentres ? "CHECK: MEMBER POSITIONS ARE TO MEMBER CENTRELINES" + (centreFallbacks.Count > 0 ? " (EXCEPT " + string.Join(", ", centreFallbacks).ToUpper().Replace("DRAWING VIEW", "VIEW ") + ": UNDERSIDE / LEFT SIDE)" : "") : "CHECK: MEMBER POSITIONS ARE TO THE UNDERSIDE / LEFT SIDE OF EACH MEMBER", "CHECK: WELD SYMBOLS NOT ADDED (NO WELD BEADS IN THE MODEL)" };
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        var flagNote = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);
        var taken = new List<double[]> { bomBox };
        if (noteAnn != null) taken.Add(Extent1(noteAnn));
        foreach (var vw in new[] { main, left, right, top, bottom, isoF }) if (vw != null) taken.Add(ExtentTrue(vw));
        if (flagNote != null) PlaceFlags((Annotation)flagNote.GetAnnotation(), string.Join("\n", flags), taken);
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

        // 7. Check and save.
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
    static List<string> centreFallbacks = new List<string>();   // --centrelines: dimension to each member's centreline instead of its underside / left side (Grant 14 Nov)
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
            }
            else
            {
                report.Add(what + ": REMOVED (selected " + sel + "/" + items.Count + ", result " + r + ", got " + string.Join("/", got) + ")"
                    + (MemberCentres ? "; trying member edges instead" : ""));
                doc.ClearSelection2(true);
                foreach (var a in made) a.Select3(true, null);
                doc.Extension.DeleteSelection2(0);
                if (MemberCentres) centreFallbacks.Add(v.GetName2() + " " + (horiz ? "heights" : "positions"));
            }
          }
        MemberCentres = wantCentres;
        return placed;
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
