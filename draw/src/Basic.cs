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
        int den = PlateScales.FirstOrDefault(d => (fw + sw2) / d + 0.090 <= 0.300 && fh / d + 0.040 <= 0.170);
        if (den == 0) den = PlateScales.Last();
        Console.WriteLine("Size " + Mm(ex) + " x " + Mm(ey) + " x " + Mm(ez) + "; face view " + pick.Item1 + "; scale 1:" + den);

        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        if (drwDoc == null) { Console.Error.WriteLine("could not start a drawing from " + template); return 3; }
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var main = drw.CreateDrawViewFromModelView3(partFile, pick.Item1, 0.250, 0.190, 0);
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
        var flags = new List<string> { "CHECK: BASIC DRAFT: DIMENSIONS FOR JAMES" };
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
        var flines = flags.Count <= 2 ? flags : Enumerable.Range(0, (flags.Count + 1) / 2).Select(i => string.Join("      ", flags.Skip(2 * i).Take(2))).ToList();
        var fn = drwDoc.InsertNote(string.Join("\n", flines)) as Note;
        if (fn != null) ((Annotation)fn.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (flines.Count - 1), 0);
        foreach (var f in flags) Console.WriteLine("Flag: " + f);
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);

        Layout(drwDoc, main, side, null);
        PlaceIso(drwDoc, main, side, iso, den);
        drwDoc.ForceRebuild3(false);

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
}
