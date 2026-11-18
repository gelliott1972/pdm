// make-basic: Grant's "nuts and bolts" draft (16 Nov) for parts the full routes refuse: the sheet, the face view (the
// standard view showing the part's largest side) and its side view at one scale that fits, the isometric tucked in the
// lower-left corner with its note block (PlaceIso, as make-plate), James's cut-list table and balloons when the part is
// a weldment, revision 0A*, and CHECK notes for James. No dimensions: nothing can be wrong or dangling.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    static string BasicView;
    static int MakeBasic(string partFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }
        var t0 = DateTime.Now;
        LayoutBlocked = null; WeldedLayout = false;
        var part = OpenDoc(partFile, config, true);
        if (part == null) { Console.Error.WriteLine("could not open " + partFile); return 3; }
        bool isAsm = part.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) part.ShowConfiguration2(config);
        var props = new Dictionary<string, string>();
        foreach (var name in new[] { "PartNo", "Description", "Material", "Weight", "Finish" })
        {
            props[name] = Prop(part, config, name);
            Console.WriteLine("  " + name + " = " + (props[name] == "" ? "(missing)" : props[name]));
        }
        if (props["PartNo"] == "") { Console.Error.WriteLine("REFUSED: no PartNo on " + config + "; no draft"); return 2; }
        // overall size: the part's bodies, or the assembly's box
        double[] box = null;
        if (!isAsm)
        {
            foreach (Body2 b in ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true)) ?? new object[0])
            {
                var bb = (double[])b.GetBodyBox();
                box = box == null ? bb : new[] { Math.Min(box[0], bb[0]), Math.Min(box[1], bb[1]), Math.Min(box[2], bb[2]), Math.Max(box[3], bb[3]), Math.Max(box[4], bb[4]), Math.Max(box[5], bb[5]) };
            }
        }
        else box = (double[])((AssemblyDoc)part).GetBox((int)swBoundingBoxOptions_e.swBoundingBoxIncludeRefPlanes * 0);
        if (box == null) { Console.Error.WriteLine("REFUSED: no solid bodies"); return 2; }
        double ex = box[3] - box[0], ey = box[4] - box[1], ez = box[5] - box[2];
        // face view: the standard view showing the largest side; its side view to the right
        var choices = new[] { Tuple.Create("*Front", ex, ey, ez), Tuple.Create("*Top", ex, ez, ey), Tuple.Create("*Right", ez, ey, ex) };
        var pick = choices.OrderByDescending(c => c.Item2 * c.Item3).First();
        double fw = pick.Item2, fh = pick.Item3, sw2 = pick.Item4;   // face width, height; side view width (depth)
        // one scale for both views: they fit the space right of the isometric corner and above the title block
        int den = PlateScales.FirstOrDefault(d => (fw + sw2) / d + 0.090 <= 0.300 && fh / d + 0.040 <= 0.195);   // 17 Nov: was 0.170; the sheet has 219 mm above the title block (08X06-SFP-03, 769 long, came out at 1:10 with the page half empty)
        if (den == 0) den = PlateScales.Last();
        Console.WriteLine("Size " + Mm(ex) + " x " + Mm(ey) + " x " + Mm(ez) + "; face view " + pick.Item1 + "; scale 1:" + den);

        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        if (drwDoc == null) { Console.Error.WriteLine("could not start a drawing from " + template); return 3; }
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        View main = null;
        {
            var muV = (MathUtility)sw.GetMathUtility();
            var exts = new[] { ex, ey, ez };
            int thin = Enumerable.Range(0, 3).OrderBy(i => exts[i]).First();
            bool unique = exts.Count(e => Math.Abs(e - exts[thin]) < 1e-6) == 1;
            // --view=NAME (08X04-BKP-07, 18 Nov): a named view saved in the model, square to a face that sits skewed
            // to the standard views, is the main view
            if (BasicView != null)
            {
                main = drw.CreateDrawViewFromModelView3(partFile, BasicView, 0.250, 0.190, 0);
                if (main == null) { Console.Error.WriteLine("REFUSED: the model has no view named '" + BasicView + "'"); return 2; }
                Console.WriteLine("Face view: named view '" + BasicView + "'");
            }
            else
            foreach (var name in new[] { pick.Item1 }.Concat(choices.Select(c => c.Item1)).Distinct())
            {
                var v = drw.CreateDrawViewFromModelView3(partFile, name, 0.250, 0.190, 0);
                if (v == null) continue;
                v.ReferencedConfiguration = config;
                drwDoc.ForceRebuild3(false);
                int ha = SheetAxis(muV, v, 0), va = SheetAxis(muV, v, 1);
                if (!unique || isAsm || (ha != va && 3 - ha - va == thin)) { main = v; if (name != pick.Item1) Console.WriteLine("Face view " + name + " (" + pick.Item1 + " didn't show the part's largest side)"); break; }
                Console.WriteLine("  " + name + " shows " + "XYZ"[ha] + " across and " + "XYZ"[va] + " up, not the largest side: trying the next view");
                drwDoc.ClearSelection2(true);
                if (drwDoc.Extension.SelectByID2(v.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0)) drwDoc.Extension.DeleteSelection2(0);
                drwDoc.ClearSelection2(true);
            }
            if (main == null) main = drw.CreateDrawViewFromModelView3(partFile, pick.Item1, 0.250, 0.190, 0);
        }
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        drwDoc.ForceRebuild3(false);
        var mo = (double[])main.GetOutline();
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var side = drw.CreateUnfoldedViewAt3(mo[2] + 0.040, (mo[1] + mo[3]) / 2, 0, false);
        drwDoc.ClearSelection2(true);
        var iso = drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.080, 0.080, 0);
        if (iso != null) iso.ReferencedConfiguration = config;
        // the sheet scale after the views (SOLIDWORKS re-scales on the first view); views follow the sheet
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 1; if (side != null) side.UseSheetScale = 1; if (iso != null) iso.UseSheetScale = 1;
        drwDoc.ForceRebuild3(false);

        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        lm.SetCurrentLayer(DraftLayer);

        // note block on the isometric, as make-plate
        if (iso != null)
        {
            drw.ActivateView(iso.GetName2());
            string text = "<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Material:          <FONT size=12PTS>$PRPVIEW:\"Material\"<FONT size=8PTS>\n"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"";
            var note = drwDoc.InsertNote(text) as Note;
            if (note != null)
            {
                var an = (Annotation)note.GetAnnotation();
                var io = (double[])iso.GetOutline();
                an.SetPosition2(io[2] + 0.012, (io[1] + io[3]) / 2 + 0.010, 0);
                var tf = (TextFormat)an.GetTextFormat(0);
                tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212;
                an.SetTextFormat(0, false, tf);
            }
        }
        // Grant 16 Nov ("very simple - you should have been able to dimension this"): the simple sizes, each checked
        // against the model's exact extents; anything that doesn't match is left off
        var mu = (MathUtility)sw.GetMathUtility(); var selMgr = (SelectionMgr)drwDoc.SelectionManager;
        var expectBasic = new List<double>();
        int sized = 0; bool detailed = false;
        if (!isAsm)
        {
            var bodies = ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Cast<Body2>().ToList();
            var cutLabels = bodies.Select(b => CutListLabel(part, b)).Where(x => x != null);
            string secText = props["Description"] + " " + string.Join(" ", cutLabels);
            int general = GeneralDims(drwDoc, selMgr, mu, main, bodies, expectBasic);
            sized = general + BasicSizes(drwDoc, selMgr, mu, GeneralRows > 0 ? null : main, side, Extents(bodies), secText, expectBasic);   // no rows (08X04-BKP-07, a skewed view): the overall sizes instead
            detailed = GeneralRows > 0;   // 17 Nov: a hole size alone is not "edges and round holes dimensioned"
        }
        var flags = new List<string> { detailed ? "CHECK: BASIC DRAFT: EDGES AND ROUND HOLES DIMENSIONED; SLOTS, WELDS AND THE REST FOR JAMES" : sized > 0 ? "CHECK: BASIC DRAFT: OVERALL SIZES ONLY, DETAIL DIMENSIONS FOR JAMES" : "CHECK: BASIC DRAFT: DIMENSIONS FOR JAMES" };
        // a weldment: James's cut-list table and balloons; an assembly: his BOM is not added here (CHECK)
        if (!isAsm && ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Length > 1)
        {
            int missing = AddCutList(drw, main, iso, config, template, null, null, flags);
            if (missing < 0) flags.Add("CHECK: NO CUT LIST IN THE MODEL: NO PARTS TABLE OR BALLOONS");
            else if (missing > 0) flags.Add("CHECK: " + missing + " CUT-LIST ITEM(S) WITHOUT A BALLOON");
            WeldedLayout = true;
        }
        if (isAsm) flags.Add("CHECK: ASSEMBLY: BOM AND BALLOONS NOT ADDED");
        if (props["Material"] == "" && !isAsm) flags.Add("CHECK: MODEL HAS NO MATERIAL PROPERTY");
        lm.SetCurrentLayer(DraftLayer);
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        var flines = PackFlags(flags);
        var fn = drwDoc.InsertNote(string.Join("\n", flines)) as Note;
        if (fn != null) ((Annotation)fn.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (flines.Count - 1), 0);
        foreach (var f in flags) Console.WriteLine("Flag: " + f);
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);

        Layout(drwDoc, main, side, null);
        IsoClearOfDims = true;
        PlaceIso(drwDoc, main, side, iso, den);
        IsoClearOfDims = false;
        drwDoc.ForceRebuild3(false);

        if (sized > 0)
        {
            var pr = Prune(drwDoc, new[] { main, side }, expectBasic);
            if (pr.Item1.Count > 0) problems.Add("basic sizes: removed " + pr.Item1.Count + " that didn't match the model");
            Console.WriteLine("Check sizes: model " + string.Join(",", expectBasic.Select(x => Math.Round(x, 2))) + "; removed " + pr.Item1.Count + ", missing " + pr.Item2.Count);
        }
        int dangling = 0;
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0]) if (a.IsDangling()) dangling++;
        Console.WriteLine("Dangling: " + dangling);
        if (dangling > 0) { problems.Add(dangling + " dangling annotations: NOT saved"); return 1; }
        int err = 0, warn = 0;
        bool ok = drwDoc.Extension.SaveAs3(output, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine(ok ? "Saved " + output : "NOT saved: error " + err + ", warning " + warn);
        if (!ok) return 3;
        ok = drwDoc.Extension.SaveAs3(pdfOut, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine(ok ? "PDF " + pdfOut : "PDF failed: error " + err);
        Console.WriteLine("Time: " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s. The drawing is left open in SOLIDWORKS.");
        return 0;
    }

    // make-overview: a whole structure as James drew 08A04-STA-01 (Grant 16 Nov, item 7: "we can just copy that"):
    // A3, ONE dimetric view at the largest of his scales that fits right of the BOM, his BOM top left (alphabetical,
    // as all assemblies), balloons, revision 0A*, CHECK notes. No dimensions.
    static int MakeOverview(string asmFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }
        var t0 = DateTime.Now;
        var asm = OpenDoc(asmFile, config, true);
        if (asm == null) { Console.Error.WriteLine("could not open " + asmFile); return 3; }
        string pn = Prop(asm, config, "PartNo");
        if (pn == "") { Console.Error.WriteLine("REFUSED: no PartNo on " + config); return 2; }
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var v = drw.CreateDrawViewFromModelView3(asmFile, "*Dimetric", 0.300, 0.180, 0);
        if (v == null) { Console.Error.WriteLine("dimetric view not created"); return 3; }
        v.ReferencedConfiguration = config;
        drwDoc.ForceRebuild3(false);
        var o = (double[])v.GetOutline();
        double w1 = (o[2] - o[0]) / v.ScaleDecimal, h1 = (o[3] - o[1]) / v.ScaleDecimal;
        // James's: the view in the right-hand two thirds, over the title block's column (outline 193-425 x 66-292)
        int den = new[] { 5, 10, 20, 25, 50, 100 }.FirstOrDefault(d => w1 / d <= 0.240 && h1 / d <= 0.235);   // James's 08A04-STA-01 view: about 232 x 226 at 1:50
        if (den == 0) den = 100;
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        v.UseSheetScale = 1;
        drwDoc.ForceRebuild3(false);
        o = (double[])v.GetOutline();
        v.Position = new[] { 0.300, 0.072 + (o[3] - o[1]) / 2 };
        drwDoc.ForceRebuild3(false);
        Console.WriteLine("Dimetric view at 1:" + den);
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        lm.SetCurrentLayer(DraftLayer);
        var flags = new List<string> { "CHECK: OVERVIEW DRAFT (AS 08A04-STA-01): DIMENSIONS FOR JAMES" };
        string bomTpl = Path.Combine(Path.GetDirectoryName(template), "Unilumin-BOM.sldbomtbt");
        var bom = v.InsertBomTable4(false, BorderX0, BorderY1, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft,
            (int)swBomType_e.swBomType_TopLevelOnly, config, bomTpl, false, 0, false);
        int items = 0;
        if (bom == null) flags.Add("CHECK: BOM NOT INSERTED");
        else
        {
            var ta = (TableAnnotation)bom;
            ((Annotation)ta.GetAnnotation()).Layer = DraftLayer;
            SortBomByPartNo(ta);
            items = ta.RowCount - 1;
            Console.WriteLine("BOM: " + items + " rows");
            drw.ActivateView(v.GetName2());
            drwDoc.ClearSelection2(true);
            drwDoc.Extension.SelectByID2(v.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            var bo = drw.CreateAutoBalloonOptions();
            bo.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Circle;
            bo.IgnoreMultiple = true;
            bo.Style = (int)swBalloonStyle_e.swBS_Circular;
            bo.Size = (int)swBalloonFit_e.swBF_2Chars;
            bo.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
            bo.Layername = DraftLayer;
            var bl = ((drw.AutoBalloon5(bo) as object[]) ?? new object[0]).Cast<Note>().ToList();
            int nb = bl.Count;
            drwDoc.ClearSelection2(true);
            SpreadBalloons(drwDoc, v, bl, null);   // in columns round the view, as the assembly drafts
            Console.WriteLine("Balloons: " + nb + " for " + items + " BOM rows");
            if (nb < items) flags.Add("CHECK: " + (items - nb) + " BOM ITEM(S) WITHOUT A BALLOON");
        }
        drw.ActivateSheet(sheet.GetName());
        drw.EditSheet();
        var fn = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
        if (fn != null) ((Annotation)fn.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (flags.Count - 1), 0);
        foreach (var f in flags) Console.WriteLine("Flag: " + f);
        lm.SetCurrentLayer("");
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);
        if (bom != null) SortBomByPartNo((TableAnnotation)bom);   // again: a rebuild puts the order back (skill)
        int blank = 0;
        if (bom != null) { var tb = (TableAnnotation)bom; for (int r = 1; r < tb.RowCount; r++) if (Flat(tb.Text[r, tb.ColumnCount - 1]).Trim() == "") blank++; }
        if (blank > 0) Console.WriteLine("BOM rows without a PartNo: " + blank);
        int dangling = 0;
        foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0]) if (a.IsDangling()) dangling++;
        Console.WriteLine("Dangling: " + dangling);
        int err = 0, warn = 0;
        bool ok = drwDoc.Extension.SaveAs3(output, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine(ok ? "Saved " + output : "NOT saved: error " + err + ", warning " + warn);
        if (!ok) return 3;
        ok = drwDoc.Extension.SaveAs3(pdfOut, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine(ok ? "PDF " + pdfOut : "PDF failed: error " + err);
        Console.WriteLine("Time: " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s.");
        return 0;
    }

    // The part's exact size along model X, Y, Z (extreme points, not the loose body box).
    static double[] Extents(List<Body2> bodies)
    {
        var r = new double[3];
        for (int i = 0; i < 3; i++)
        {
            double hi = double.MinValue, lo = double.MaxValue;
            foreach (var b in bodies)
            {
                var d = new double[3]; d[i] = 1;
                double x, y, z;
                if (b.GetExtremePoint(d[0], d[1], d[2], out x, out y, out z)) hi = Math.Max(hi, new[] { x, y, z }[i]);
                if (b.GetExtremePoint(-d[0], -d[1], -d[2], out x, out y, out z)) lo = Math.Min(lo, new[] { x, y, z }[i]);
            }
            r[i] = hi - lo;
        }
        return r;
    }

    // Which model axis runs across (sheetDir 0) or up (1) the sheet in this view.
    static int SheetAxis(MathUtility mu, View v, int sheetDir)
    {
        var o = ToSheet(mu, v, new double[3]); int best = 0; double bm = -1;
        for (int i = 0; i < 3; i++) { var p = new double[3]; p[i] = 1; var s = ToSheet(mu, v, p); double d = Math.Abs(s[sheetDir] - o[sheetDir]); if (d > bm) { bm = d; best = i; } }
        return best;
    }

    // Basic draft sizes (Grant 16 Nov, DW7-DW10): the face view's overall width and height, the side view's width, and
    // the wall of a tube when the description or cut list names it (RHS/SHS a x b x t) and the side view shows it.
    // Each drawn value must equal the model's exact extent (or the named wall) or it isn't placed.
    static int BasicSizes(ModelDoc2 doc, SelectionMgr sm, MathUtility mu, View main, View side, double[] ext, string text, List<double> expected)
    {
        int n = 0;
        var tm = System.Text.RegularExpressions.Regex.Match(text ?? "", @"\b(?:RHS|SHS)\s*\d+(?:\.\d+)?\s*x\s*\d+(?:\.\d+)?\s*x\s*(\d+(?:\.\d+)?)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        double wall = tm.Success ? double.Parse(tm.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : double.NaN;
        foreach (var v in new[] { main, side })
        {
            if (v == null) continue;
            var lines = ViewLines(mu, v);
            var vert = lines.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).OrderBy(l => l.Item2[0]).ToList();
            var hor = lines.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).OrderBy(l => l.Item2[1]).ToList();
            var o = (double[])v.GetOutline(); double vs = v.ScaleDecimal;
            if (vert.Count >= 2)
            {
                var l = vert.First(); var r = vert.Last();
                double w = (r.Item2[0] - l.Item2[0]) / vs * 1000, m = ext[SheetAxis(mu, v, 0)] * 1000;
                if (Math.Abs(w - m) < 0.01) { Dim2(doc, sm, v, l.Item1, r.Item1, (l.Item2[0] + r.Item2[0]) / 2, o[3] + 0.010, "overall width", l.Item2[0], r.Item2[0], true); expected.Add(w); n++; }
                else Console.WriteLine("  width on the view " + w.ToString("0.##") + ", model " + m.ToString("0.##") + ": not dimensioned");
                if (v == side && !double.IsNaN(wall))
                {
                    // a line one wall in from either outside line, along most of it
                    foreach (var outer in new[] { l, r })
                    {
                        double len = Math.Abs(outer.Item2[1] - outer.Item3[1]);
                        var inner = vert.FirstOrDefault(q => q.Item1 != outer.Item1 && Math.Abs(Math.Abs(q.Item2[0] - outer.Item2[0]) / vs * 1000 - wall) < 0.01 && Overlap(q, outer, true) > 0.3 * len);
                        if (inner == null) continue;
                        Dim2(doc, sm, v, outer.Item1, inner.Item1, (outer.Item2[0] + inner.Item2[0]) / 2, o[1] - 0.010, "wall thickness", outer.Item2[0], inner.Item2[0], true);
                        expected.Add(wall); n++;
                        break;
                    }
                }
            }
            if (v == main && hor.Count >= 2)
            {
                var b = hor.First(); var t = hor.Last();
                double h = (t.Item2[1] - b.Item2[1]) / vs * 1000, m = ext[SheetAxis(mu, v, 1)] * 1000;
                if (Math.Abs(h - m) < 0.01) { Dim2(doc, sm, v, b.Item1, t.Item1, o[0] - 0.012, (b.Item2[1] + t.Item2[1]) / 2, "overall height", t.Item2[1], b.Item2[1], false); expected.Add(h); n++; }
                else Console.WriteLine("  height on the view " + h.ToString("0.##") + ", model " + m.ToString("0.##") + ": not dimensioned");
            }
            doc.ClearSelection2(true);
        }
        return n;
    }

    // Grant 16 Nov: not all-or-nothing. Deletes the dimensions in these views whose value isn't among the expected ones
    // (each expected value used once). Returns the values removed and the expected values not on the drawing.
    static Tuple<List<double>, List<double>> Prune(ModelDoc2 doc, IEnumerable<View> views, List<double> expected)
    {
        var left = expected.ToList(); var removed = new List<double>(); var del = new List<Annotation>();
        foreach (var v in views.Where(x => x != null))
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                double val = ((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000;
                int i = left.FindIndex(e => Math.Abs(e - val) < 0.011);
                if (i >= 0) left.RemoveAt(i); else { del.Add(a); removed.Add(val); }
            }
        for (int k = 0; k < del.Count; k++)
        {
            doc.ClearSelection2(true);
            if (del[k].Select3(false, null)) doc.Extension.DeleteSelection2(0);
            Console.WriteLine("  removed a dimension that doesn't match the model: " + removed[k].ToString("0.##"));
        }
        doc.ClearSelection2(true);
        foreach (var m in left) Console.WriteLine("  not on the drawing: " + m.ToString("0.##"));
        return Tuple.Create(removed, left);
    }

    // After Prune: every dimension left in the views is one of the expected values.
    static bool PrunedOk(IEnumerable<View> views, List<double> expected, bool wasOk)
    {
        if (wasOk) return true;
        var left = expected.ToList();
        foreach (var v in views.Where(x => x != null))
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                double val = ((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000;
                int i = left.FindIndex(e => Math.Abs(e - val) < 0.011);
                if (i < 0) return false;
                left.RemoveAt(i);
            }
        return true;
    }

    // The CHECK notes, bottom left outside the border (as Section.cs places them); made if there were none.
    static void SetFlags(DrawingDoc drw, ModelDoc2 doc, Sheet sheet, ref Note flagNote, List<string> flags)
    {
        if (flags.Count == 0) return;
        if (flagNote == null)
        {
            drw.ActivateSheet(sheet.GetName()); drw.EditSheet();
            var lm = (LayerMgr)doc.GetLayerManager(); lm.SetCurrentLayer(DraftLayer);
            flagNote = doc.InsertNote(string.Join("\n", PackFlags(flags))) as Note;
            lm.SetCurrentLayer("");
            if (flagNote == null) { problems.Add("flag note not placed"); return; }
        }
        else flagNote.SetText(string.Join("\n", PackFlags(flags)));
        ((Annotation)flagNote.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (PackFlags(flags).Count - 1), 0);
        foreach (var f in flags) Console.WriteLine("Flag: " + f);
    }
}
