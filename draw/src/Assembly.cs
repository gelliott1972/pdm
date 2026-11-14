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
        doc.ForceRebuild3(false);
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
        string backIso = MakeBackIsoView(asm);

        // 2. Views: face, then top above / bottom below / side to the right (projected, linked); front and back isos.
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var main = drw.CreateDrawViewFromModelView3(asmFile, faceView, 0.250, 0.150, 0);
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        main.UseSheetScale = 0; main.ScaleRatio = new double[] { 1, 25 };
        drwDoc.ForceRebuild3(false);
        var mo = (double[])main.GetOutline();
        Func<double, double, View> project = (x, y) =>
        {
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            var pv = drw.CreateUnfoldedViewAt3(x, y, 0, false);
            drwDoc.ClearSelection2(true);
            return pv;
        };
        double mcx = (mo[0] + mo[2]) / 2, mcy = (mo[1] + mo[3]) / 2;
        var side = project(mo[2] + 0.05, mcy);
        var top = project(mcx, mo[3] + 0.05);
        var bottom = project(mcx, mo[1] - 0.05);
        var isoF = drw.CreateDrawViewFromModelView3(asmFile, "*Isometric", 0.080, 0.120, 0);
        if (isoF != null) isoF.ReferencedConfiguration = config;
        View isoB = null;
        if (backIso != null)
        {
            isoB = drw.CreateDrawViewFromModelView3(asmFile, backIso, 0.080, 0.060, 0);
            if (isoB != null) isoB.ReferencedConfiguration = config;
        }
        if (isoB == null) problems.Add("back isometric not made");
        Console.WriteLine("Views: face, side " + (side != null) + ", top " + (top != null) + ", bottom " + (bottom != null) + ", isometric front " + (isoF != null) + ", back " + (isoB != null));

        // 3. BOM (James's template; the configuration must be named, or it stays empty) at the top-left corner.
        var bom = main.InsertBomTable4(false, BorderX0, BorderY1, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
            (int)swBomType_e.swBomType_TopLevelOnly, config, bomTpl, false, 0, false);
        if (bom == null) problems.Add("BOM not inserted");
        else ((Annotation)((TableAnnotation)bom).GetAnnotation()).Layer = DraftLayerEnsure(drwDoc);

        // Note block (in the front isometric, so its links read that view's model); placed by the layout.
        Annotation noteAnn = null;
        if (isoF != null)
        {
            drw.ActivateView(isoF.GetName2());
            var lmN = (LayerMgr)drwDoc.GetLayerManager(); lmN.SetCurrentLayer(DraftLayerEnsure(drwDoc));
            var note = drwDoc.InsertNote("<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"") as Note;
            lmN.SetCurrentLayer("");
            if (note == null) problems.Add("note block not placed");
            else
            {
                noteAnn = (Annotation)note.GetAnnotation();
                var tf = (TextFormat)noteAnn.GetTextFormat(0); tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212; noteAnn.SetTextFormat(0, false, tf);
            }
        }
        drwDoc.ForceRebuild3(true);
        var bomBox = BomBox(bom);
        Console.WriteLine("BOM: " + (bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0) + " items" + (bomBox != null ? ", " + Box(bomBox) : ""));

        // 4. Scale: James's scales from the largest; the isometrics may go up to two steps smaller. First that lays out.
        int den = 0, isoDen = 0;
        foreach (int d in AsmScales)
        {
            main.UseSheetScale = 0; main.ScaleRatio = new double[] { 1, d };
            drwDoc.ForceRebuild3(false);
            string why = null;
            foreach (int di in AsmScales.Where(x => x >= d).Take(3))
            {
                foreach (var iv in new[] { isoF, isoB }) if (iv != null) { iv.UseSheetScale = 0; iv.ScaleRatio = new double[] { 1, di }; }
                drwDoc.ForceRebuild3(false);
                why = LayoutAsm(drwDoc, main, side, top, bottom, isoF, isoB, bomBox, noteAnn);
                if (why == null) { den = d; isoDen = di; break; }
            }
            if (den > 0) break;
            Console.WriteLine("  1:" + d + " doesn't fit: " + why);
        }
        if (den == 0) { den = AsmScales.Last(); isoDen = den; problems.Add("nothing fits even at 1:" + den); }
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 1;
        foreach (var iv in new[] { isoF, isoB }) if (iv != null) { if (isoDen == den) iv.UseSheetScale = 1; else { iv.UseSheetScale = 0; iv.ScaleRatio = new double[] { 1, isoDen }; } }
        drwDoc.ForceRebuild3(false);
        geomCache.Clear(); geomScale.Clear();   // measure again at the final scale
        Console.WriteLine("Scale 1:" + den + (isoDen != den ? ", isometrics 1:" + isoDen : ""));

        // 5. Balloons on the front isometric, then spread so they don't cross.
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        int items = bom != null ? ((TableAnnotation)bom).RowCount - 1 : 0;
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
        }
        Console.WriteLine("Balloons: " + balloonNotes.Count + " for " + items + " BOM items");
        if (balloonNotes.Count < items) problems.Add((items - balloonNotes.Count) + " BOM items without a balloon");

        // 6. Flags, revision row, final layout, balloons spread, flags placed.
        var flags = new List<string> { "CHECK: NO DIMENSIONS YET", "CHECK: WELD SYMBOLS NOT ADDED" };
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        lm.SetCurrentLayer(DraftLayer);
        var flagNote = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);
        string final = LayoutAsm(drwDoc, main, side, top, bottom, isoF, isoB, bomBox, noteAnn);
        if (final != null) problems.Add("layout: " + final);
        if (isoF != null) SpreadBalloons(drwDoc, isoF, balloonNotes, noteAnn);
        var taken = new List<double[]> { bomBox };
        foreach (var vw in new[] { main, side, top, bottom, isoF, isoB }) if (vw != null) taken.Add(ExtentTrue(vw));
        if (flagNote != null) PlaceFlags((Annotation)flagNote.GetAnnotation(), string.Join("\n", flags), taken);

        // 7. Check and save.
        bool ok = items > 0;
        Console.WriteLine("Check BOM: " + items + " items" + (ok ? " OK" : " EMPTY"));
        if (!ok)
        {
            problems.Add("empty BOM: NOT saved");
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
        const double g = 0.015, m = 0.004;
        doc.ForceRebuild3(false);
        var mo = OutlineOf(main);
        double[] so = side != null ? OutlineOf(side) : null, to = top != null ? OutlineOf(top) : null, bo = bottom != null ? OutlineOf(bottom) : null;
        // right block: top / face / bottom stacked, side to the right of the face
        double blockW = W(mo) + (so != null ? g + W(so) : 0);
        double blockH = H(mo) + (to != null ? g + H(to) : 0) + (bo != null ? g + H(bo) : 0);
        double regionB = TitleTop + m, regionT = BorderY1 - m;
        if (blockH > regionT - regionB) return "views too tall (" + Mm(blockH) + " mm)";
        double faceL = BorderX1 - m - W(mo);                             // side view goes to the left of the face view (Grant)
        double baseY = regionB + (regionT - regionB - blockH) / 2;      // block centred up and down
        double faceCy = baseY + (bo != null ? H(bo) + g : 0) + H(mo) / 2;
        CentreAt(doc, main, faceL + W(mo) / 2, faceCy);
        mo = OutlineOf(main);
        if (side != null) CentreAt(doc, side, mo[0] - g - W(so) / 2, double.NaN);
        if (top != null) CentreAt(doc, top, double.NaN, mo[3] + g + H(to) / 2);
        if (bottom != null) CentreAt(doc, bottom, double.NaN, mo[1] - g - H(bo) / 2);
        double blockL = side != null ? OutlineOf(side)[0] : mo[0];
        if (bomBox != null && bomBox[2] + g > blockL && regionT > bomBox[1]) return "views run into the BOM";
        // left region: under the BOM, left of the views
        double lx0 = BorderX0 + m, lx1 = Math.Min(blockL - g, TitleLeft - m), ly1 = (bomBox != null ? bomBox[1] : BorderY1) - g, ly0 = RevTop + 0.012;   // left of the title block, clear of the revision table
        if (noteAnn != null)
        {
            noteAnn.SetPosition2(lx0 + 0.005, ly1, 0);
            doc.ForceRebuild3(false);
            var nb = Extent1(noteAnn);
            ly1 = ly1 - 0.022 - g;                                      // the note block is about 4 lines
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
