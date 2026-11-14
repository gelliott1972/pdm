// Draw: first draft of an assembly drawing, the "nuts and bolts" (brief draw-1 step 4, workflow B): James's sheet,
// face view + linked side view + isometric with the note block, his BOM (top left), auto balloons on the isometric,
// a first go at dimensions (overall sizes), revision row, CHECK flags for what is left to James. Everything placed
// is on DRAFT. Values checked against the model before saving.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    static double[] LayoutBlocked;
    static List<Note> balloonNotes = new List<Note>();

    // Balloons just outside the isometric, each in line with the part it points to (from the view's middle through
    // the leader's tip), spread to at least BalloonGap apart, kept on the paper and clear of the note block.
    static void SpreadBalloons(ModelDoc2 doc, View iso, List<Note> notes)
    {
        if (notes.Count == 0) return;
        var io = (double[])iso.GetOutline();
        double cx = (io[0] + io[2]) / 2, cy = (io[1] + io[3]) / 2, rx = W(io) / 2 + 0.010, ry = H(io) / 2 + 0.010;
        double[] noteBox = null;
        foreach (Annotation a in (iso.GetAnnotations() as object[]) ?? new object[0])
            if (a.GetType() == (int)swAnnotationType_e.swNote && ((Note)a.GetSpecificAnnotation()).GetText().StartsWith("Filename")) { var e = Extent1(a); noteBox = e; }
        // each balloon's target angle from the view's middle to where its leader touches the part
        var items = new List<Tuple<Annotation, double>>();
        foreach (var n in notes)
        {
            var a = (Annotation)n.GetAnnotation();
            var lp = a.GetLeaderCount() > 0 ? a.GetLeaderPointsAtIndex(0) as double[] : null;
            var pos = (double[])a.GetPosition();
            double tx = cx, ty = cy;
            if (lp != null && lp.Length >= 3)
            {   // the leader point farthest from the balloon is the tip on the part
                int best = 0; double bd = -1;
                for (int i = 0; i + 2 < lp.Length; i += 3) { double d = Math.Abs(lp[i] - pos[0]) + Math.Abs(lp[i + 1] - pos[1]); if (d > bd) { bd = d; best = i; } }
                tx = lp[best]; ty = lp[best + 1];
            }
            items.Add(Tuple.Create(a, Math.Atan2(ty - cy, tx - cx)));
        }
        // even spacing in angle, keeping each one's order around the view
        var ordered = items.OrderBy(t => t.Item2).ToList();
        double minStep = 2 * Math.PI / Math.Max(ordered.Count, 1) * 0.6;
        var angles = ordered.Select(t => t.Item2).ToList();
        for (int pass = 0; pass < 20; pass++)
            for (int i = 1; i < angles.Count; i++) if (angles[i] - angles[i - 1] < minStep) angles[i] = angles[i - 1] + minStep;
        int moved = 0;
        for (int i = 0; i < ordered.Count; i++)
        {
            double ang = angles[i];
            double x = cx + rx * Math.Cos(ang), y = cy + ry * Math.Sin(ang);
            x = Math.Max(BorderX0 + 0.005, Math.Min(BorderX1 - 0.005, x)); y = Math.Max(RevTop + 0.005, Math.Min(BorderY1 - 0.005, y));
            if (noteBox != null && x > noteBox[0] - 0.005 && x < noteBox[2] + 0.005 && y > noteBox[1] - 0.005 && y < noteBox[3] + 0.005) x = noteBox[0] - 0.008;
            if (ordered[i].Item1.SetPosition2(x, y, 0)) moved++;
        }
        doc.ForceRebuild3(false);
        Console.WriteLine("Balloons spread round the isometric: " + moved + " of " + ordered.Count);
    }

    static double[] Extent1(Annotation a)
    {
        var p = (double[])a.GetPosition();
        var dd = a.GetDisplayData() as DisplayData;
        double x0 = p[0], y0 = p[1], x1 = p[0], y1 = p[1];
        for (int i = 0; dd != null && i < dd.GetTextCount(); i++)
        {
            string t = dd.GetTextAtIndex(i) ?? ""; double h = dd.GetTextHeightAtIndex(i);
            var tp = dd.GetTextPositionAtIndex(i) as double[];
            x1 = Math.Max(x1, p[0] + t.Length * h * 0.6); y0 = Math.Min(y0, p[1] - h * (i + 1) * 1.5);
        }
        return new[] { x0, y0, x1, y1 };
    }
    static int isoDenFinal;   // a box the views must keep clear of (the BOM), sheet metres

    static int MakeAssembly(string asmFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }
        string bomTpl = Path.Combine(Path.GetDirectoryName(template), "Unilumin-BOM.sldbomtbt");
        if (!File.Exists(bomTpl)) { Console.Error.WriteLine("REFUSED: no BOM template " + bomTpl + " (save-table-templates first)"); return 2; }
        var t0 = DateTime.Now;

        // 1. Model.
        var asm = OpenDoc(asmFile, config, true);
        if (asm.ConfigurationManager.ActiveConfiguration.Name != config) asm.ShowConfiguration2(config);
        var props = new Dictionary<string, string>();
        foreach (var name in new[] { "PartNo", "Description", "Weight", "Finish" })
        { props[name] = Prop(asm, config, name); Console.WriteLine("  " + name + " = " + (props[name] == "" ? "(missing)" : props[name])); }
        if (props["PartNo"] == "") { Console.Error.WriteLine("REFUSED: no PartNo; no draft"); return 2; }
        // size from the visible, unsuppressed parts only (the assembly's own box includes reference geometry)
        double[] box = null;
        ((AssemblyDoc)asm).ResolveAllLightWeightComponents(false);   // lightweight parts don't report their size
        int nAll = 0, nSup = 0, nHid = 0, nSub = 0, nNoBox = 0;
        foreach (Component2 c in ((object[])((AssemblyDoc)asm).GetComponents(false)) ?? new object[0])
        {
            nAll++;
            if (c.IsSuppressed()) { nSup++; continue; }
            if (c.IsHidden(true)) { nHid++; continue; }
            if (((c.GetChildren() as object[]) ?? new object[0]).Length > 0) { nSub++; continue; }
            var cb = c.GetBox(false, false) as double[];
            if (cb == null) { nNoBox++; continue; }
            box = box == null ? (double[])cb.Clone() : new[] { Math.Min(box[0], cb[0]), Math.Min(box[1], cb[1]), Math.Min(box[2], cb[2]), Math.Max(box[3], cb[3]), Math.Max(box[4], cb[4]), Math.Max(box[5], cb[5]) };
        }
        Console.WriteLine("Components: " + nAll + " (" + nSup + " suppressed, " + nHid + " hidden, " + nSub + " subassemblies, " + nNoBox + " without a size)");
        if (box == null) { Console.Error.WriteLine("REFUSED: no visible parts"); return 2; }
        var ext = new[] { box[3] - box[0], box[4] - box[1], box[5] - box[2] };
        int thin = Array.IndexOf(ext, ext.Min());
        string faceView = thin == 0 ? "*Right" : thin == 1 ? "*Top" : "*Front";
        Console.WriteLine("Assembly box " + Mm(ext[0]) + " x " + Mm(ext[1]) + " x " + Mm(ext[2]) + "; face view " + faceView + " (looking along its thinnest side)");

        // 2. Sheet, views.
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var mu = (MathUtility)sw.GetMathUtility();
        var selMgr = (SelectionMgr)drwDoc.SelectionManager;
        var main = drw.CreateDrawViewFromModelView3(asmFile, faceView, 0.250, 0.150, 0);
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        drwDoc.ForceRebuild3(false);
        var mo = (double[])main.GetOutline();
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var side = drw.CreateUnfoldedViewAt3(mo[2] + 0.060, (mo[1] + mo[3]) / 2, 0, false);
        drwDoc.ClearSelection2(true);
        var iso = drw.CreateDrawViewFromModelView3(asmFile, "*Isometric", 0.080, 0.080, 0);
        if (iso != null) iso.ReferencedConfiguration = config;

        // 3. BOM (James's template) at the top-left corner of the border, as on his assembly drawings; its size decides
        //    what room is left. Scale: the largest of James's scales at which views + isometric fit around it.
        var bom = main.InsertBomTable4(false, BorderX0, BorderY1, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
            (int)swBomType_e.swBomType_TopLevelOnly, config, bomTpl, false, 0, false);   // the configuration must be named, or the BOM stays empty (14 Nov)
        if (bom == null) problems.Add("BOM not inserted");
        if (bom != null) ((Annotation)((TableAnnotation)bom).GetAnnotation()).Layer = DraftLayerEnsure(drwDoc);
        drwDoc.ForceRebuild3(false);

        // Note block on the isometric now, so the layout trial below allows for it.
        if (iso != null)
        {
            drw.ActivateView(iso.GetName2());
            string text = "<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"";
            var lmN = (LayerMgr)drwDoc.GetLayerManager(); lmN.SetCurrentLayer(DraftLayerEnsure(drwDoc));
            var note = drwDoc.InsertNote(text) as Note;
            lmN.SetCurrentLayer("");
            if (note == null) problems.Add("note block not placed");
            else
            {
                var an = (Annotation)note.GetAnnotation();
                var io = (double[])iso.GetOutline();
                an.SetPosition2(io[2] + 0.012, (io[1] + io[3]) / 2 + 0.012, 0);
                var tf = (TextFormat)an.GetTextFormat(0); tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212; an.SetTextFormat(0, false, tf);
            }
        }

        // Scale: try James's scales from the largest; lay the sheet out for real each time and take the first where
        // nothing overlaps or runs off (dimensions come later, so the BOM is given a margin).
        int den = 0; double[] bomBox = null; isoDenFinal = 0;
        foreach (int d in new[] { 5, 10, 15, 20, 25, 30, 40, 50 })
        {
            foreach (var vw in new[] { main, iso }) if (vw != null) { vw.UseSheetScale = 0; vw.ScaleRatio = new double[] { 1, d }; }
            drwDoc.ForceRebuild3(true);
            bomBox = BomBox(bom);
            LayoutBlocked = bomBox == null ? null : new[] { bomBox[0], bomBox[1] - 0.005, bomBox[2] + 0.005, bomBox[3] };
            int before = problems.Count;
            Layout(drwDoc, main, side, iso);
            var lp = problems.Skip(before).ToList();
            problems.RemoveRange(before, problems.Count - before);
            den = d;
            if (lp.Count == 0) { isoDenFinal = d; break; }
            // If only the isometric doesn't fit, let it go one or two of James's scales smaller (main views stay)
            var ie = iso != null ? Extent(iso) : null;
            bool onlyIso = iso != null && lp.All(x => x.Contains("isometric") || (x.Contains("runs off") && x.Contains(Box(ie))));
            if (onlyIso)
            {
                var smaller = new[] { 5, 10, 15, 20, 25, 30, 40, 50 }.Where(x => x > d).Take(2).ToList();
                foreach (int di in smaller)
                {
                    iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, di };
                    drwDoc.ForceRebuild3(false);
                    int b2 = problems.Count;
                    Layout(drwDoc, main, side, iso);
                    var lp2 = problems.Skip(b2).ToList();
                    problems.RemoveRange(b2, problems.Count - b2);
                    if (lp2.Count == 0) { isoDenFinal = di; break; }
                }
                if (isoDenFinal > 0) { Console.WriteLine("  1:" + d + " fits with the isometric at 1:" + isoDenFinal); break; }
                iso.ScaleRatio = new double[] { 1, d };
            }
            Console.WriteLine("  1:" + d + " doesn't fit: " + string.Join("; ", lp));
        }
        Console.WriteLine("BOM: " + (bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0) + " items" + (bomBox != null ? ", " + Box(bomBox) : ""));
        LayoutBlocked = bomBox;
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 1;
        if (iso != null) { if (isoDenFinal == den || isoDenFinal == 0) iso.UseSheetScale = 1; else { iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, isoDenFinal }; } }
        drwDoc.ForceRebuild3(false);
        Console.WriteLine("Scale 1:" + den + (isoDenFinal > 0 && isoDenFinal != den ? ", isometric 1:" + isoDenFinal : ""));

        var lm = (LayerMgr)drwDoc.GetLayerManager();
        DraftLayerEnsure(drwDoc);
        lm.SetCurrentLayer(DraftLayer);

        // 4. Balloons on the isometric: every BOM item once.
        if (iso != null)
        {
            drw.ActivateView(iso.GetName2());
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(iso.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            // SOLIDWORKS's own arrangements stacked every balloon on one spot (14 Nov), so the balloons are spread
            // by SpreadBalloons after the final layout.
            int items = bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0;
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(iso.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            var bo = drw.CreateAutoBalloonOptions();
            bo.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Circle;
            bo.IgnoreMultiple = true;
            bo.Style = (int)swBalloonStyle_e.swBS_Circular;
            bo.Size = (int)swBalloonFit_e.swBF_2Chars;
            bo.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
            bo.Layername = DraftLayer;
            balloonNotes = ((drw.AutoBalloon5(bo) as object[]) ?? new object[0]).Cast<Note>().ToList();
            int nb = balloonNotes.Count; string used = "";
            Console.WriteLine("Balloons: " + nb + " for " + items + " BOM items" + (used != "" ? " (" + used + ")" : ""));
            if (nb < items) problems.Add((items - nb) + " BOM items without a balloon on the isometric");
            drwDoc.ClearSelection2(true);
        }

        // 5. First go at dimensions: the overall sizes (the outermost edges of the face view and the side view).
        // Dimensions: not yet on assemblies. The first try (outermost edges) picked the wrong edges inside the
        // subframe (90 degree angles, 270 instead of 600; 14 Nov), so they're left to James for now and flagged.
        var expect = new List<double>();

        // 6. Flags, revision row.
        var flags = new List<string> { "CHECK: NO DIMENSIONS YET", "CHECK: WELD SYMBOLS NOT ADDED" };
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        var flagNote = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);

        // 7. Layout (learned), flags into a free corner, check, save.
        Layout(drwDoc, main, side, iso);
        if (iso != null) SpreadBalloons(drwDoc, iso, balloonNotes);
        if (flagNote != null)
            PlaceFlags((Annotation)flagNote.GetAnnotation(), string.Join("\n", flags), new[] { Extent(main), side != null ? Extent(side) : null, iso != null ? Extent(iso) : null, bomBox });
        var got = new List<double>();
        foreach (var vw in new[] { main, side }) if (vw != null)
                foreach (Annotation a in (vw.GetAnnotations() as object[]) ?? new object[0])
                    if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension) got.Add(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000);
        Func<IEnumerable<double>, string> fmt = l => string.Join(",", l.Select(x => Math.Round(x, 1)).OrderBy(x => x));
        var boxMm = new[] { ext[0] * 1000, ext[1] * 1000, ext[2] * 1000 };
        int bomItems = bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0;
        bool ok = fmt(expect) == fmt(got) && got.All(g => boxMm.Any(b => Math.Abs(b - g) < 0.5)) && bomItems > 0;
        Console.WriteLine("Check BOM: " + bomItems + " items" + (bomItems > 0 ? " OK" : " EMPTY"));
        Console.WriteLine("Check overall sizes: from the views " + fmt(expect) + " / drawing " + fmt(got) + " / model box " + fmt(boxMm) + (ok ? " OK" : " MISMATCH"));
        if (!ok)
        {
            problems.Add("overall sizes don't match the model: NOT saved");
            int e2 = 0, w2 = 0; string fail = Path.ChangeExtension(output, ".FAILED.pdf");
            if (!File.Exists(fail) && drwDoc.Extension.SaveAs3(fail, 0, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e2, ref w2))
                Console.WriteLine("PDF of the failed draft, to look at: " + fail);
            return 1;
        }
        return SaveDraft(drw, drwDoc, output, pdfOut, t0);
    }

    // Experiment: which BOM settings fill the table for this assembly? New drawing, front view, three tries.
    static int BomTest(string asmFile, string template)
    {
        var asm = OpenDoc(asmFile, "Default", true);
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        var v = drw.CreateDrawViewFromModelView3(asmFile, "*Front", 0.25, 0.15, 0);
        v.ReferencedConfiguration = "Default";
        drwDoc.ForceRebuild3(true);
        string james = Path.Combine(Path.GetDirectoryName(template), "Unilumin-BOM.sldbomtbt");
        string std = @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS\lang\englishom-standard.sldbomtbt";
        var tries = new[] { Tuple.Create(james, "", 2), Tuple.Create(james, "Default", 2), Tuple.Create(std, "", 2), Tuple.Create(james, "Default", 1), Tuple.Create(james, "", 3) };
        double y = 0.28;
        foreach (var t in tries)
        {
            var b = v.InsertBomTable4(false, 0.02, y, 1, t.Item3, t.Item2, t.Item1, false, 0, false);
            drwDoc.ForceRebuild3(true);
            var ta = b as TableAnnotation;
            Console.WriteLine(Path.GetFileName(t.Item1) + " config '" + t.Item2 + "' type " + t.Item3 + ": " + (ta == null ? "not inserted" : (ta.RowCount + " rows x " + ta.ColumnCount + " cols; row1: "
                + (ta.RowCount > 1 ? string.Join(" | ", Enumerable.Range(0, ta.ColumnCount).Select(c => ta.Text[1, c])) : "-"))));
            y -= 0.06;
        }
        return 0;
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

    static string DraftLayerEnsure(ModelDoc2 drwDoc)
    {
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        return DraftLayer;
    }

    // Overall width (and height) of a view: between its outermost straight edges.
    static void OverallDims(ModelDoc2 doc, SelectionMgr selMgr, MathUtility mu, View v, List<double> expect, string what, bool widthOnly = false)
    {
        var ls = ViewLines(mu, v);
        var vert = ls.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).ToList();
        var hor = ls.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).ToList();
        var o = (double[])v.GetOutline();
        ((DrawingDoc)doc).ActivateView(v.GetName2());
        if (vert.Count >= 2)
        {
            var l0 = vert.OrderBy(l => l.Item2[0]).First(); var l1 = vert.OrderBy(l => l.Item2[0]).Last();
            Dim2(doc, selMgr, v, l0.Item1, l1.Item1, (l0.Item2[0] + l1.Item2[0]) / 2, o[3] + 0.012, what + " overall width");
            expect.Add((l1.Item2[0] - l0.Item2[0]) / v.ScaleDecimal * 1000);
        }
        else problems.Add(what + ": no vertical edges for the overall width");
        if (widthOnly) return;
        if (hor.Count >= 2)
        {
            var l0 = hor.OrderBy(l => l.Item2[1]).First(); var l1 = hor.OrderBy(l => l.Item2[1]).Last();
            Dim2(doc, selMgr, v, l0.Item1, l1.Item1, o[0] - 0.015, (l0.Item2[1] + l1.Item2[1]) / 2, what + " overall height");
            expect.Add((l1.Item2[1] - l0.Item2[1]) / v.ScaleDecimal * 1000);
        }
        else problems.Add(what + ": no horizontal edges for the overall height");
    }
}
