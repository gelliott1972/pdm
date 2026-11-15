// Draw: templates, PDF and plate drafts. See Draw.cs for usage.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    // Opens a COPY of one of James's drawings and saves, into outDir (refusing to overwrite anything):
    //   Unilumin-A3.slddrt    the sheet format (title block, border, zones, tolerance block)
    //   Unilumin-revision.sldrevtbt  the revision table as a table template
    //   Unilumin-A3.drwdot    the drawing template: James's document settings, sheet and format, revision table emptied,
    //                         every drawing view (and so every dimension and the note block) removed
    //   note-block.txt        the isometric view's note block (linked text and font), for make-plate to re-create
    // The copy itself is never saved.
    static int ExtractTemplates(string file, string outDir)
    {
        Directory.CreateDirectory(outDir);
        string fmt = Path.Combine(outDir, "Unilumin-A3.slddrt"), tpl = Path.Combine(outDir, "Unilumin-A3.drwdot"),
               rev = Path.Combine(outDir, "Unilumin-revision.sldrevtbt"), note = Path.Combine(outDir, "note-block.txt");
        foreach (var f in new[] { fmt, tpl, rev, note })
            if (File.Exists(f)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + f); return 2; }

        var doc = OpenDoc(file, null, false);
        var drw = (DrawingDoc)doc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        drw.ActivateSheet(sheet.GetName());

        if (sheet.SaveFormat(fmt)) Console.WriteLine("Sheet format: " + fmt); else problems.Add("sheet format not saved");

        // Drawing-level custom properties travel with the template; list them so we know what's in it.
        var cpm = doc.Extension.CustomPropertyManager[""];
        var pnames = cpm.GetNames() as object[];
        Console.WriteLine("Drawing custom properties:");
        if (pnames != null) foreach (string name in pnames)
            {
                string val, res; bool wasRes, linked;
                cpm.Get6(name, false, out val, out res, out wasRes, out linked);
                Console.WriteLine("  " + name + " = \"" + val + "\" (resolved \"" + res + "\")");
            }

        // Leftovers from James's drawing that would show on a new drawing: the revision shown in the title block
        // (it comes back from the revision table when James adds a revision) and the old drawing date.
        foreach (var stale in new[] { "Revision-Sheet1", "DWG_Date" })
            if (pnames != null && pnames.Contains(stale)) Console.WriteLine("  cleared " + stale + ": " + (cpm.Set2(stale, "") == 0));

        // The note block on the isometric view.
        var noteLines = new List<string>();
        var v = (View)drw.GetFirstView();
        var viewNames = new List<string>();
        for (v = (View)v.GetNextView(); v != null; v = (View)v.GetNextView())
        {
            viewNames.Add(v.GetName2());
            var anns = v.GetAnnotations() as object[];
            if (anns == null) continue;
            foreach (Annotation a in anns)
                if (a.GetType() == (int)swAnnotationType_e.swNote)
                {
                    var n = (Note)a.GetSpecificAnnotation();
                    var p = (double[])a.GetPosition();
                    var vp = (double[])v.Position;
                    noteLines.Add("view " + v.GetName2() + " orientation " + v.GetOrientationName() + "\r\noffset from view centre (mm) " + Mm(p[0] - vp[0]) + "," + Mm(p[1] - vp[1])
                        + "\r\nfont " + FontOf(a) + "\r\nlinked text:\r\n" + n.PropertyLinkedText + "\r\n");
                }
        }
        File.WriteAllText(note, string.Join("\r\n", noteLines));
        Console.WriteLine("Note block(s): " + noteLines.Count + " -> " + note);

        // Revision table: save its template, then empty it.
        var ra = sheet.RevisionTable;
        if (ra != null)
        {
            var ta = (TableAnnotation)ra;
            if (ta.SaveAsTemplate(rev)) Console.WriteLine("Revision table template: " + rev); else problems.Add("revision table template not saved");
            int rows = ta.RowCount;
            for (int r = rows - 1; r >= 0; r--)
            {
                int id = ra.GetIdForRowNumber(r);
                if (id > 0) Console.WriteLine("  revision row " + r + " '" + ra.GetRevisionForId(id) + "' deleted: " + ra.DeleteRevision(id, true));
            }
        }
        else problems.Add("no revision table on the sheet");

        // Remove every drawing view (their dimensions, centre marks and notes go with them).
        foreach (var name in viewNames)
        {
            doc.ClearSelection2(true);
            bool sel = doc.Extension.SelectByID2(name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
            bool del = sel && doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
            Console.WriteLine("  view " + name + " deleted: " + del);
            if (!del) problems.Add("view not deleted: " + name);
        }
        doc.ClearSelection2(true);
        doc.ForceRebuild3(false);

        int err = 0, warn = 0;
        bool ok = doc.Extension.SaveAs3(tpl, (int)swSaveAsVersion_e.swSaveAsCurrentVersion,
            (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine(ok ? "Drawing template: " + tpl : "Drawing template NOT saved: error " + err + ", warning " + warn);
        if (!ok) problems.Add("template not saved");
        sw.CloseDoc(doc.GetTitle());   // the copy is not saved
        return 0;
    }

    // Every BOM / cut-list table on a COPY of one of James's drawings saved as a table template (his columns, fonts,
    // widths). The copy is not saved.
    static int SaveTableTemplates(string file, string outDir)
    {
        Directory.CreateDirectory(outDir);
        var doc = OpenDoc(file, null, true);
        var sheetView = (View)((DrawingDoc)doc).GetFirstView();
        int n = 0;
        foreach (Annotation a in (sheetView.GetAnnotations() as object[]) ?? new object[0])
        {
            if (a.GetType() != (int)swAnnotationType_e.swTableAnnotation) continue;
            var ta = (TableAnnotation)a.GetSpecificAnnotation();
            string name = ta.Type == (int)swTableAnnotationType_e.swTableAnnotation_BillOfMaterials ? "Unilumin-BOM.sldbomtbt"
                        : ta.Type == (int)swTableAnnotationType_e.swTableAnnotation_WeldmentCutList ? "Unilumin-cutlist.sldwldtbt" : null;
            if (name == null) continue;
            string path = Path.Combine(outDir, name);
            if (File.Exists(path)) { Console.WriteLine("  exists, not overwritten: " + path); continue; }
            var p = (double[])a.GetPosition();
            bool ok = ta.SaveAsTemplate(path);
            Console.WriteLine((ok ? "Saved " : "NOT saved ") + path + " (" + ta.RowCount + " x " + ta.ColumnCount + ", at " + Mm(p[0]) + "," + Mm(p[1]) + ", columns: "
                + string.Join(" | ", Enumerable.Range(0, ta.ColumnCount).Select(c => Flat(ta.Text[0, c]))) + ")");
            if (ok) n++;
        }
        sw.CloseDoc(doc.GetTitle());
        Console.WriteLine(n + " table template(s) saved");
        return 0;
    }

    // James's scales (13 Nov survey): the largest that fits the main view in the box below.
    static readonly int[] PlateScales = { 1, 2, 5, 10, 20, 25, 50 };
    const double FitW = 0.190, FitH = 0.110;          // sheet metres for the plate face view
    const double MinSide = 0.060;                       // below this at 1:5 the plate is drawn bigger
    const int Magenta = 0xFF00FF;                       // COLORREF (R | G<<8 | B<<16)
    const string DraftLayer = "DRAFT";
    const double CentreMarkSize = 0.0125, CentreMarkGap = 0.0025;   // James's (PLP-06): 12.5 mm, gap 2.5, centreline font

    class Hole { public double[] C; public double D; public bool Thru; }

    // First draft of a flat plate: face view, side view for thickness, isometric with the note block, ordinate
    // dimensions from the top-left corner, centre marks, "n x Ø d THRU" callout, thickness. Everything the machine
    // places goes on layer DRAFT (magenta). Nothing is copied from another drawing, so nothing can dangle; it is
    // still checked before saving.
    static List<string> plateSlotFlags = new List<string>();
    static int weldedOthers;   // bodies welded to the plate (shown, not dimensioned)
    static int MakePlate(string partFile, string config, string output, string template, HashSet<string> flags)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        if (!File.Exists(template)) { Console.Error.WriteLine("REFUSED: no template " + template); return 2; }
        var t0 = DateTime.Now;
        string pdfOut = Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }

        // 1. The model: properties, body, thickness direction, holes.
        var part = OpenDoc(partFile, config, true);
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) part.ShowConfiguration2(config);
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) { Console.Error.WriteLine("no configuration " + config); return 2; }
        var props = new Dictionary<string, string>();
        foreach (var name in new[] { "PartNo", "Description", "Material", "Weight", "Finish" })
        {
            string v = Prop(part, config, name);
            props[name] = v;
            Console.WriteLine("  " + name + " = " + (v == "" ? "(missing)" : v));
        }
        if (props["PartNo"] == "") { Console.Error.WriteLine("REFUSED: no PartNo on " + config + "; no draft"); return 2; }
        var bodies = (object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true);
        if (bodies == null || bodies.Length == 0) { Console.Error.WriteLine("REFUSED: make-plate needs a solid body"); return 2; }
        // A welded bracket (batch 3, 08X04-BKP: a plate with an RHS stub or angles welded on): draw it as its plate, the
        // flattest of the large bodies, the rest shown in the views but not dimensioned; a CHECK note says so (overnight, Admin)
        Func<Body2, double[]> extOf = b => { var bb = (double[])b.GetBodyBox(); var e = new[] { bb[3] - bb[0], bb[4] - bb[1], bb[5] - bb[2] }; Array.Sort(e); return e; };
        var body = bodies.Length == 1 ? (Body2)bodies[0] : bodies.Cast<Body2>().Where(b => { var e = extOf(b); return e[0] < 0.25 * e[1]; }).OrderByDescending(b => { var e = extOf(b); return e[1] * e[2]; }).FirstOrDefault();
        if (body == null) { Console.Error.WriteLine("REFUSED: make-plate found no plate among " + bodies.Length + " bodies"); return 2; }
        weldedOthers = bodies.Length - 1;
        if (weldedOthers > 0) Console.WriteLine("Welded bracket: drawn as its plate " + body.Name + "; " + weldedOthers + " other bod" + (weldedOthers == 1 ? "y" : "ies") + " shown, not dimensioned");
        var box = (double[])body.GetBodyBox();
        var ext = new[] { box[3] - box[0], box[4] - box[1], box[5] - box[2] };
        int thin = Array.IndexOf(ext, ext.Min());
        string faceView = thin == 0 ? "*Right" : thin == 1 ? "*Top" : "*Front";
        var face = Enumerable.Range(0, 3).Where(i => i != thin).Select(i => ext[i]).ToArray();
        Console.WriteLine("Plate " + Mm(face[0]) + " x " + Mm(face[1]) + " x " + Mm(ext[thin]) + " thick; face view " + faceView);

        var holes = new List<Hole>();
        foreach (Face2 f in (object[])body.GetFaces())
        {
            var s = (Surface)f.GetSurface();
            if (!s.IsCylinder()) continue;
            var cp = (double[])s.CylinderParams;   // origin xyz, axis xyz, radius
            var axis = new[] { cp[3], cp[4], cp[5] };
            if (Math.Abs(Math.Abs(axis[thin]) - 1) > 1e-6) continue;           // not square to the plate
            if (f.GetLoopCount() != 2) continue;                                  // a rounded corner, not a full hole
            var fb = (double[])f.GetBox();
            double depth = fb[3 + thin] - fb[thin];
            holes.Add(new Hole { C = new[] { cp[0], cp[1], cp[2] }, D = 2 * cp[6], Thru = Math.Abs(depth - ext[thin]) < 1e-5 });
        }
        // slots (Grant 15 Nov: located by their centres only, sized only by their callout): pairs of half-round ends
        var ends = new List<Tuple<double[], double>>();   // end-arc centre (model), radius
        foreach (Face2 f in (object[])body.GetFaces())
        {
            var sf2 = (Surface)f.GetSurface();
            if (!sf2.IsCylinder() || !SlotEnd(f)) continue;
            var cp = (double[])sf2.CylinderParams;
            if (Math.Abs(Math.Abs(cp[3 + thin]) - 1) > 1e-6) continue;
            ends.Add(Tuple.Create(new[] { cp[0], cp[1], cp[2] }, cp[6]));
        }
        var slots = new List<Tuple<double[], double, double>>();   // centre (model), width, length (m)
        var usedEnd = new HashSet<int>();
        for (int i = 0; i < ends.Count; i++)
        {
            if (usedEnd.Contains(i)) continue;
            int best = -1; double bd = double.MaxValue;
            for (int j = 0; j < ends.Count; j++)
            {
                if (j == i || usedEnd.Contains(j) || Math.Abs(ends[i].Item2 - ends[j].Item2) > 1e-7) continue;
                double d0 = Math.Sqrt(Enumerable.Range(0, 3).Where(a => a != thin).Sum(a => Math.Pow(ends[i].Item1[a] - ends[j].Item1[a], 2)));
                if (d0 < bd) { bd = d0; best = j; }
            }
            if (best < 0) { problems.Add("a slot end without its partner"); continue; }
            usedEnd.Add(i); usedEnd.Add(best);
            var c0 = ends[i].Item1; var c1 = ends[best].Item1;
            slots.Add(Tuple.Create(new[] { (c0[0] + c1[0]) / 2, (c0[1] + c1[1]) / 2, (c0[2] + c1[2]) / 2 }, 2 * ends[i].Item2, bd + 2 * ends[i].Item2));
        }
        foreach (var g in slots.GroupBy(x => Math.Round(x.Item2 * 1e5) + "x" + Math.Round(x.Item3 * 1e5)))
            Console.WriteLine("  slots: " + g.Count() + " x " + Mm(g.First().Item2) + " wide x " + Mm(g.First().Item3) + " long");
        var groups = holes.GroupBy(h => Math.Round(h.D * 1000, 2)).OrderBy(g => g.Key).ToList();
        foreach (var g in groups) Console.WriteLine("  holes: " + g.Count() + " x Ø" + g.Key + (g.All(h => h.Thru) ? " THRU" : " (not all through)"));

        // 2. Scale and new sheet.
        // Grant 14 Nov: 1:5 by default for consistency; bigger (1:2, 1:1) only when the plate would be too small to
        // read at 1:5 (longest side under MinSide on the sheet), smaller (1:10 ...) only when it doesn't fit.
        Func<int, bool> fits = d => face[0] / d <= FitW && face[1] / d <= FitH;
        int den = 5;
        if (!fits(den)) den = PlateScales.Where(d => d > 5).FirstOrDefault(fits);
        else while (face.Max() / den < MinSide && den > 1 && fits(den == 5 ? 2 : 1)) den = den == 5 ? 2 : 1;
        if (den == 0) den = PlateScales.Last();
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        if (drwDoc == null) { Console.Error.WriteLine("could not start a drawing from " + template); return 3; }
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();

        // Scale first, so the views are laid out at their final size.
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        sp = (double[])sheet.GetProperties2();
        Console.WriteLine("Scale 1:" + den + (sp[2] == 1 && sp[3] == den ? "" : " NOT SET (sheet says " + sp[2] + ":" + sp[3] + ")"));
        if (sp[3] != den) problems.Add("sheet scale not set");

        // 3. Views (James's layout): face view at the top, its top edge TopY below the border, centred on MainX;
        //    side view to its right past the vertical ordinates; isometric in the lower-left box, at the largest of
        //    James's scales that fits there (it may be smaller than the sheet scale); note block to its right.
        const double MainX = 0.190, TopY = 0.255, IsoBoxL = 0.020, IsoBoxR = 0.185, IsoBoxB = 0.030, Gap = 0.020;
        var main = drw.CreateDrawViewFromModelView3(partFile, faceView, MainX, 0.200, 0);
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        main.UseSheetScale = 1;
        drwDoc.ForceRebuild3(false);
        if (Math.Abs(main.ScaleDecimal - 1.0 / den) > 1e-9) { main.UseSheetScale = 0; main.ScaleRatio = new double[] { 1, den }; drwDoc.ForceRebuild3(false); }
        var mo = (double[])main.GetOutline();
        main.Position = new[] { MainX, TopY - (mo[3] - mo[1]) / 2 };
        drwDoc.ForceRebuild3(false);
        mo = (double[])main.GetOutline();
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var side = drw.CreateUnfoldedViewAt3(mo[2] + 0.040, (mo[1] + mo[3]) / 2, 0, false);
        if (side == null) problems.Add("side view not created");
        drwDoc.ClearSelection2(true);
        var iso = drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.100, 0.100, 0);
        if (iso == null) problems.Add("isometric not created");
        else
        {
            iso.ReferencedConfiguration = config;
            drwDoc.ForceRebuild3(false);
            var io = (double[])iso.GetOutline();
            double w1 = (io[2] - io[0]) / iso.ScaleDecimal, h1 = (io[3] - io[1]) / iso.ScaleDecimal;   // at 1:1
            double boxW = IsoBoxR - IsoBoxL, boxH = mo[1] - Gap - IsoBoxB;
            int isoDen = PlateScales.Where(d => d >= den).FirstOrDefault(d => w1 / d <= boxW && h1 / d <= boxH);
            if (isoDen == 0) isoDen = PlateScales.Last();
            if (isoDen == den) iso.UseSheetScale = 1;
            else { iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, isoDen }; }
            drwDoc.ForceRebuild3(false);
            io = (double[])iso.GetOutline();
            iso.Position = new[] { IsoBoxL + (io[2] - io[0]) / 2, mo[1] - Gap - (io[3] - io[1]) / 2 };
            Console.WriteLine("Isometric at 1:" + isoDen);
        }
        drwDoc.ClearSelection2(true);
        // SOLIDWORKS re-scales the sheet when the first view goes in ("automatically scale new drawing views"), so the
        // sheet scale (shown in the title block) is set again now; the face view then follows the sheet.
        sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        main.UseSheetScale = 1;
        drwDoc.ForceRebuild3(false);
        sp = (double[])sheet.GetProperties2();
        if (sp[2] != 1 || sp[3] != den || Math.Abs(main.ScaleDecimal - 1.0 / den) > 1e-9)
            problems.Add("scale: sheet " + sp[2] + ":" + sp[3] + ", face view 1:" + (1 / main.ScaleDecimal).ToString("0.#") + ", wanted 1:" + den);
        foreach (var vw in new[] { main, side, iso }) if (vw != null)
            {
                var o = (double[])vw.GetOutline();
                Console.WriteLine("  view " + vw.GetName2() + " [" + vw.ReferencedConfiguration + "] 1:" + (1 / vw.ScaleDecimal).ToString("0.#") + " outline "
                    + Mm(o[0]) + "," + Mm(o[1]) + " - " + Mm(o[2]) + "," + Mm(o[3]));
            }

        // 4. DRAFT layer, magenta; everything placed from here on lands on it.
        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        lm.SetCurrentLayer(DraftLayer);

        // 6. Ordinate dimensions from the top-left corner, as James: across the top from the left edge (left edge, each
        //    distinct hole column, right edge) and down the right side from the top edge (top edge, each hole row, bottom).
        var mu = (MathUtility)sw.GetMathUtility();
        var selMgr = (SelectionMgr)drwDoc.SelectionManager;
        mo = (double[])main.GetOutline();
        var expectedOrd = new List<double>();   // mm, from the model's geometry, for the self-check
        var hLines = new List<Tuple<Edge, double>>(); var vLines = new List<Tuple<Edge, double>>();
        var circles = new List<Tuple<Edge, double[], double>>();   // edge, sheet centre, diameter (m)
        foreach (Edge e in (main.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (c.IsCircle())
            {
                var cp = (double[])c.CircleParams;
                var cpc = e.GetCurveParams3();
                var sp0 = (double[])cpc.StartPoint; var ep0 = (double[])cpc.EndPoint;
                bool full = Math.Abs(sp0[0] - ep0[0]) + Math.Abs(sp0[1] - ep0[1]) + Math.Abs(sp0[2] - ep0[2]) < 1e-9;
                if (full) circles.Add(Tuple.Create(e, ToSheet(mu, main, new[] { cp[0], cp[1], cp[2] }), 2 * cp[6]));
            }
            else if (c.IsLine())
            {
                var cpd = e.GetCurveParams3();
                var a0 = ToSheet(mu, main, (double[])cpd.StartPoint); var b0 = ToSheet(mu, main, (double[])cpd.EndPoint);
                if (Math.Abs(a0[1] - b0[1]) < 1e-7) hLines.Add(Tuple.Create(e, a0[1]));
                else if (Math.Abs(a0[0] - b0[0]) < 1e-7) vLines.Add(Tuple.Create(e, a0[0]));
            }
        }
        Console.WriteLine("Face view: " + hLines.Count + " horizontal, " + vLines.Count + " vertical edges, " + circles.Count + " circles; outline "
            + Mm(mo[0]) + "," + Mm(mo[1]) + " - " + Mm(mo[2]) + "," + Mm(mo[3]));
        if (hLines.Count < 2 || vLines.Count < 2) problems.Add("plate outline edges not found; no ordinate dimensions");
        else
        {
            var leftE = vLines.OrderBy(l => l.Item2).First(); var rightE = vLines.OrderBy(l => l.Item2).Last();
            var topE = hLines.OrderBy(l => l.Item2).Last(); var botE = hLines.OrderBy(l => l.Item2).First();
            // Each ordinate attaches to the hole nearest its dimension line (as James), so extension lines don't run
            // across other holes: the top hole of each column, the right-most hole of each row (Grant 14 Nov).
            var cols = circles.GroupBy(c => Math.Round(c.Item2[0] * 1e5)).Select(g => g.OrderByDescending(c => c.Item2[1]).First()).OrderBy(c => c.Item2[0]).ToList();
            var rows = circles.GroupBy(c => Math.Round(c.Item2[1] * 1e5)).Select(g => g.OrderByDescending(c => c.Item2[0]).First()).OrderByDescending(c => c.Item2[1]).ToList();
            double vs = main.ScaleDecimal;
            if (slots.Count == 0)
            {
                var hSel = new List<Edge> { leftE.Item1 }; hSel.AddRange(cols.Select(c => c.Item1)); hSel.Add(rightE.Item1);
                var vSel = new List<Edge> { topE.Item1 }; vSel.AddRange(rows.Select(c => c.Item1)); vSel.Add(botE.Item1);
                expectedOrd.Add(0); expectedOrd.AddRange(cols.Select(c => (c.Item2[0] - leftE.Item2) / vs * 1000)); expectedOrd.Add((rightE.Item2 - leftE.Item2) / vs * 1000);
                expectedOrd.Add(0); expectedOrd.AddRange(rows.Select(c => (topE.Item2 - c.Item2[1]) / vs * 1000)); expectedOrd.Add((topE.Item2 - botE.Item2) / vs * 1000);
                Ordinate(drwDoc, selMgr, main, hSel, (int)swAddOrdinateDims_e.swHorizontalOrdinate, leftE.Item2, topE.Item2 + 0.012, "across the top");
                Ordinate(drwDoc, selMgr, main, vSel, (int)swAddOrdinateDims_e.swVerticalOrdinate, rightE.Item2 + 0.012, topE.Item2, "down the right");
            }
            else
            {
                // Grant 15 Nov: slots by their centres only, both ways. Slot centre marks first (the rows pick them); a mark
                // serves one row only, so the row down the right prefers slots the top row didn't use; a slot centre a row
                // still can't take is left off and flagged (never dimensioned to its end or side)
                drw.ActivateView(main.GetName2());
                if (!main.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Slots, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0))
                    problems.Add("slot centre marks not inserted");
                drwDoc.ClearSelection2(true);
                var sc = slots.Select(x => ToSheet(mu, main, x.Item1)).ToList();
                var topSlots = sc.GroupBy(q => Math.Round(q[0] * 1e5)).Select(g => g.OrderByDescending(q => q[1]).First()).ToList();
                var rightSlots = sc.GroupBy(q => Math.Round(q[1] * 1e5)).Select(g => g.OrderBy(q => topSlots.Contains(q) ? 1 : 0).ThenByDescending(q => q[0]).First()).ToList();
                var hItems = new List<object> { leftE.Item1 }; var hVals = new List<double> { 0 };
                foreach (var c in cols) { hItems.Add(c.Item1); hVals.Add((c.Item2[0] - leftE.Item2) / vs * 1000); }
                foreach (var q in topSlots) if (!hVals.Any(v => Math.Abs(v - (q[0] - leftE.Item2) / vs * 1000) < 0.01)) { hItems.Add(q); hVals.Add((q[0] - leftE.Item2) / vs * 1000); }
                hItems.Add(rightE.Item1); hVals.Add((rightE.Item2 - leftE.Item2) / vs * 1000);
                var vItems = new List<object> { topE.Item1 }; var vVals = new List<double> { 0 };
                foreach (var c in rows) { vItems.Add(c.Item1); vVals.Add((topE.Item2 - c.Item2[1]) / vs * 1000); }
                foreach (var q in rightSlots) if (!vVals.Any(v => Math.Abs(v - (topE.Item2 - q[1]) / vs * 1000) < 0.01)) { vItems.Add(q); vVals.Add((topE.Item2 - q[1]) / vs * 1000); }
                vItems.Add(botE.Item1); vVals.Add((topE.Item2 - botE.Item2) / vs * 1000);
                Ordinate2(drwDoc, selMgr, main, hItems, (int)swAddOrdinateDims_e.swHorizontalOrdinate, leftE.Item2, topE.Item2 + 0.012, "across the top");
                foreach (var q in lastMissed) { double mv = (q[0] - leftE.Item2) / vs * 1000; int at = hVals.FindIndex(v => Math.Abs(v - mv) < 0.01); if (at >= 0) hVals.RemoveAt(at); plateSlotFlags.Add("CHECK: SLOT CENTRE ACROSS NOT DIMENSIONED (" + Math.Round(mv, 1) + ")"); }
                Ordinate2(drwDoc, selMgr, main, vItems, (int)swAddOrdinateDims_e.swVerticalOrdinate, rightE.Item2 + 0.012, topE.Item2, "down the right");
                foreach (var q in lastMissed) { double mv = (topE.Item2 - q[1]) / vs * 1000; int at = vVals.FindIndex(v => Math.Abs(v - mv) < 0.01); if (at >= 0) vVals.RemoveAt(at); plateSlotFlags.Add("CHECK: SLOT CENTRE DOWN NOT DIMENSIONED (" + Math.Round(mv, 1) + ")"); }
                expectedOrd.AddRange(hVals); expectedOrd.AddRange(vVals);
            }
        }
        drwDoc.ClearSelection2(true);

        // 7. Hole callouts: a diameter dimension on one hole of each size (driven by the model, so it can't dangle),
        //    text "n x Ø d THRU" as James writes it.
        int k = 0;
        foreach (var g in groups)
        {
            var mine = circles.Where(c => Math.Abs(c.Item3 * 1000 - g.Key) < 0.01).OrderBy(c => c.Item2[0] - c.Item2[1]).ToList();
            if (mine.Count == 0) { problems.Add("no visible circle for Ø" + g.Key); continue; }
            var pick = mine[0];
            drwDoc.ClearSelection2(true);
            var sd = (SelectData)selMgr.CreateSelectData(); sd.View = main;
            ((Entity)pick.Item1).Select4(false, sd);
            double lx = mo[0] - 0.030 - 0.008 * k, ly = pick.Item2[1] + 0.022 + 0.008 * k;
            var dd = drwDoc.AddDiameterDimension2(lx, ly, 0) as DisplayDimension;
            if (dd == null) { problems.Add("hole callout not placed for Ø" + g.Key); continue; }
            dd.SetBrokenLeader2(false, (int)swDisplayDimensionLeaderText_e.swBrokenLeaderHorizontalText);
            int n = g.Count();
            bool thru = g.All(h => h.Thru);
            dd.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, (n > 1 ? n + " x " : "") + "<MOD-DIAM>");
            dd.SetText((int)swDimensionTextParts_e.swDimensionTextSuffix, thru ? " THRU" : "");
            if (!thru) problems.Add("Ø" + g.Key + ": not every hole goes through; callout says nothing about depth");
            k++;
        }
        drwDoc.ClearSelection2(true);
        foreach (var g in slots.GroupBy(x => Math.Round(x.Item2 * 1e5) + "x" + Math.Round(x.Item3 * 1e5)))
        {
            // the leader to the slot nearest the callout, and that slot's end nearest it (Grant 15 Nov, 08X04-PLP-32)
            double tx = mo[0] - 0.050 - 0.008 * k, ty = mo[3] + 0.010 + 0.008 * k;   // clear of the top row's "0" (08X04-PLP-32)
            var s0 = g.OrderBy(x => { var q = ToSheet(mu, main, x.Item1); return Math.Pow(q[0] - tx, 2) + Math.Pow(q[1] - ty, 2); }).First();
            var cs = ToSheet(mu, main, s0.Item1);
            int n = g.Count();
            string text = (n > 1 ? n + " x " : "") + (s0.Item2 * 1000).ToString("0.0") + " X " + (s0.Item3 * 1000).ToString("0") + " THRU";
            var arc = ((main.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0]).Cast<Edge>()
                .Where(e => ((Curve)e.GetCurve()).IsCircle() && Math.Abs(((double[])((Curve)e.GetCurve()).CircleParams)[6] * 2 - s0.Item2) < 1e-7)
                .Select(e => Tuple.Create(e, ToSheet(mu, main, ((double[])((Curve)e.GetCurve()).CircleParams).Take(3).ToArray())))
                .Where(t => Math.Sqrt(Math.Pow(t.Item2[0] - cs[0], 2) + Math.Pow(t.Item2[1] - cs[1], 2)) < s0.Item3 * main.ScaleDecimal)
                .OrderBy(t => Math.Pow(t.Item2[0] - tx, 2) + Math.Pow(t.Item2[1] - ty, 2)).FirstOrDefault();
            drwDoc.ClearSelection2(true);
            if (arc != null) { var sd = (SelectData)selMgr.CreateSelectData(); sd.View = main; ((Entity)arc.Item1).Select4(false, sd); }
            var note = drwDoc.InsertNote(text) as Note;
            if (note == null) { problems.Add("slot callout not placed: " + text); continue; }
            ((Annotation)note.GetAnnotation()).SetPosition2(tx, Math.Max(ty, cs[1] + 0.022), 0);
            Console.WriteLine("Slot callout: " + text + (arc == null ? " (no leader: end arc not found)" : ""));
            if (arc == null) problems.Add("slot callout without a leader: " + text);
            k++;
        }
        drwDoc.ClearSelection2(true);

        // 8. Thickness on the side view: the two outermost vertical edges.
        if (side != null)
        {
            var lines = new List<Tuple<Edge, double[], double[]>>();
            foreach (Edge e in (side.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                var c = (Curve)e.GetCurve();
                if (!c.IsLine()) continue;
                var ps = (double[])e.GetCurveParams3().StartPoint; var pe = (double[])e.GetCurveParams3().EndPoint;
                var a = ToSheet(mu, side, ps); var b = ToSheet(mu, side, pe);
                if (Math.Abs(a[0] - b[0]) < 1e-7 && Math.Abs(a[1] - b[1]) > 1e-4) lines.Add(Tuple.Create(e, a, b));
            }
            if (lines.Count < 2) problems.Add("thickness: side view edges not found");
            else
            {
                var left = lines.OrderBy(l => l.Item2[0]).First(); var right = lines.OrderBy(l => l.Item2[0]).Last();
                drwDoc.ClearSelection2(true);
                var sd = (SelectData)selMgr.CreateSelectData(); sd.View = side;
                ((Entity)left.Item1).Select4(false, sd); ((Entity)right.Item1).Select4(true, sd);
                double top = Math.Max(Math.Max(left.Item2[1], left.Item3[1]), Math.Max(right.Item2[1], right.Item3[1]));
                var td = drwDoc.AddDimension2((left.Item2[0] + right.Item2[0]) / 2, top + 0.012, 0) as DisplayDimension;
                if (td == null) problems.Add("thickness dimension not placed");
            }
            drwDoc.ClearSelection2(true);
        }

        // 5. Centre marks on the holes of the face view, James's style. Placed AFTER the dimensions: with the marks'
        //    extension lines already there, selecting a hole for an ordinate measured to its edge, not its centre
        //    (14 Nov: 41.3 instead of 48).
        drw.ActivateView(main.GetName2());
        if (!main.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Hole, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0))
            problems.Add("centre marks not inserted");
        drwDoc.ClearSelection2(true);

        // 9. Note block on the isometric, as James's (text from _Templates\note-block.txt, Weight/Finish version).
        if (iso != null)
        {
            drw.ActivateView(iso.GetName2());
            string text = "<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Material:          <FONT size=12PTS>$PRPVIEW:\"Material\"<FONT size=8PTS>\n"   // Grant 15 Nov (08X04-PLP-02): "where is the material?"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"";
            var note = drwDoc.InsertNote(text) as Note;
            if (note == null) problems.Add("note block not placed");
            else
            {
                var an = (Annotation)note.GetAnnotation();
                var io = (double[])iso.GetOutline();
                an.SetPosition2(io[2] + 0.012, (io[1] + io[3]) / 2 + 0.010, 0);   // top-left of the note
                var tf = (TextFormat)an.GetTextFormat(0);
                tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212;
                an.SetTextFormat(0, false, tf);
            }
        }
        // Notes for James (08B04-SFA-2T CHECK style), outside the border bottom left as Grant placed them on GNRIC-LMW-02
        // (brief draw-3): a model with no material, or a weight far from the same plate in steel
        {
            var pflags = new List<string>(plateSlotFlags);
            if (weldedOthers > 0) pflags.Add("CHECK: WELDED PART: ONLY THE PLATE IS DIMENSIONED; " + weldedOthers + " WELDED BOD" + (weldedOthers == 1 ? "Y" : "IES") + " TO LOCATE AND SIZE; WELDS NOT ADDED");
            foreach (var x in ExtraChecks) pflags.Add((x.StartsWith("CHECK:", StringComparison.OrdinalIgnoreCase) ? x : "CHECK: " + x).ToUpperInvariant());
            if (props["Material"] == "") pflags.Add("CHECK: MODEL HAS NO MATERIAL PROPERTY");
            double steelKg = ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Cast<Body2>().Sum(b => ((double[])b.GetMassProperties(7850))[5]);
            double wProp;
            if (double.TryParse(props["Weight"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out wProp)
                && Math.Abs(wProp - steelKg) > Math.Max(0.1, 0.1 * steelKg))
                pflags.Add("CHECK: WEIGHT IS " + props["Weight"] + " KG, IN STEEL " + steelKg.ToString("0.0") + " KG");
            if (pflags.Count > 0)
            {
                drw.ActivateSheet(((Sheet)drw.GetCurrentSheet()).GetName());
                drw.EditSheet();
                var fn = drwDoc.InsertNote(string.Join("\n", pflags)) as Note;
                if (fn == null) problems.Add("CHECK note not placed");
                else
                {
                    ((Annotation)fn.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (pflags.Count - 1), 0);
                    foreach (var f in pflags) Console.WriteLine("Flag: " + f);
                }
            }
        }
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);

        // 9b. Revision row "0A*" (Grant 14 Nov, to discuss): the * says work in progress; at release it becomes 0A
        //     unless a formal one is made. DRAWN/CHECKED/APPROVED left empty.
        var ra = sheet.RevisionTable;
        if (ra == null) problems.Add("no revision table on the sheet");
        else
        {
            int id = ra.AddRevision("0A*");
            int row = ra.GetRowNumberForId(id);
            var ta = (TableAnnotation)ra;
            if (id <= 0 || row < 0) problems.Add("revision row not added");
            else
            {
                ta.Text[row, 1] = DateTime.Now.ToString("d/MM/yyyy");
                ta.Text[row, 2] = "ORIGINAL DESIGN";
                // The title block's REV box reads Revision-Sheet1; the table doesn't refresh it here, so set it.
                drwDoc.Extension.CustomPropertyManager[""].Set2("Revision-Sheet1", "0A*");
                Console.WriteLine("Revision row: " + string.Join(" | ", Enumerable.Range(0, ta.ColumnCount).Select(c => ta.Text[row, c])));
            }
        }
        drwDoc.ForceRebuild3(false);

        // 9c. Layout (Grant 14 Nov): the face view with the side view beside it, the isometric and note block below,
        //     each row centred, and the whole group moved so its centre of area sits in the middle of the free sheet
        //     (inside the border, above the title block).
        Layout(drwDoc, main, side, iso);
        // the isometric inside the border at the sheet scale (Grant 15 Nov, 08X04-PLP-32: he put it back to 1:2 and moved it
        // right): slide it right first; only if it then reaches past the title block's left, one scale smaller
        if (iso != null && ((double[])iso.GetOutline())[0] < BorderX0 + 0.003)
        {
            var io5 = (double[])iso.GetOutline();
            MoveOutline(drwDoc, iso, BorderX0 + 0.008, double.NaN);
            Console.WriteLine("Isometric slid right inside the border (kept at 1:" + Math.Round(1 / iso.ScaleDecimal) + ")");
        }
        double isoRightLimit = TitleLeft - 0.005;
        Func<double> isoRight = () =>
        {
            double r = ((double[])iso.GetOutline())[2];
            foreach (Annotation a in (iso.GetAnnotations() as object[]) ?? new object[0])
                if (a.GetType() == (int)swAnnotationType_e.swNote) { var e5 = ((Note)a.GetSpecificAnnotation()).GetExtent() as double[]; if (e5 != null) r = Math.Max(r, Math.Max(e5[0], e5[3])); }
            return r;
        };
        for (int tries = 0; iso != null && tries < 3 && isoRight() > isoRightLimit && ((double[])iso.GetOutline())[1] < TitleTop; tries++)
        {
            int cur = (int)Math.Round(1 / iso.ScaleDecimal);
            int next = PlateScales.Where(d => d > cur).DefaultIfEmpty(0).First();
            if (next == 0) break;
            iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, next };
            drwDoc.ForceRebuild3(false);
            Layout(drwDoc, main, side, iso);
            Console.WriteLine("Isometric and note block ran into the title block: now 1:" + next);
        }
        for (int tries = 0; iso != null && tries < 3 && ((double[])iso.GetOutline())[0] < BorderX0 + 0.003; tries++)
        {
            int cur = (int)Math.Round(1 / iso.ScaleDecimal);
            int next = PlateScales.Where(d => d > cur).DefaultIfEmpty(0).First();
            if (next == 0) break;
            iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, next };
            drwDoc.ForceRebuild3(false);
            Layout(drwDoc, main, side, iso);
            Console.WriteLine("Isometric past the border: now 1:" + next);
        }

        // 10. Self-check: every ordinate value equals the model's geometry; callouts and thickness likewise.
        var gotOrd = new List<double>(); var gotDia = new List<double>(); var gotThk = new List<double>();
        foreach (var vw in new[] { main, side })
            if (vw != null) foreach (Annotation a in (vw.GetAnnotations() as object[]) ?? new object[0])
                {
                    if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                    var dd = (DisplayDimension)a.GetSpecificAnnotation();
                    double val = ((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000;
                    int dt = dd.GetType();
                    if (dt == 1 || dt == 7 || dt == 8) gotOrd.Add(val);
                    else if (dt == 5 || dt == 6) gotDia.Add(val);
                    else gotThk.Add(val);
                }
        Func<IEnumerable<double>, string> fmt = l => string.Join(",", l.Select(x => Math.Round(x, 2)).OrderBy(x => x));
        var checks = new[] {
            Tuple.Create("ordinates", fmt(expectedOrd), fmt(gotOrd)),
            Tuple.Create("hole sizes", fmt(groups.Select(g => g.Key)), fmt(gotDia)),
            Tuple.Create("thickness", fmt(new[] { ext[thin] * 1000 }), fmt(gotThk)) };
        bool valuesOk = true;
        foreach (var c in checks)
        {
            bool same = c.Item2 == c.Item3;
            Console.WriteLine("Check " + c.Item1 + ": model " + c.Item2 + " / drawing " + c.Item3 + (same ? " OK" : " MISMATCH"));
            if (!same) { valuesOk = false; problems.Add(c.Item1 + " on the drawing don't match the model: NOT saved"); }
        }
        if (!valuesOk) return 1;

        // 10b. Count what's placed, nothing dangling, everything placed is on DRAFT.
        int dims = 0, notes = 0, marks = 0, offDraft = 0, dangling = 0;
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                int ty = a.GetType();
                if (ty == (int)swAnnotationType_e.swDisplayDimension) dims++;
                else if (ty == (int)swAnnotationType_e.swNote) notes++;
                else if (ty == (int)swAnnotationType_e.swCenterMarkSym) marks++;
                if (a.Layer != DraftLayer) offDraft++;
                if (a.IsDangling()) dangling++;
            }
        Console.WriteLine("Placed: " + dims + " dimensions, " + marks + " centre marks, " + notes + " notes; not on DRAFT " + offDraft + "; dangling " + dangling);
        if (dangling > 0) { problems.Add(dangling + " dangling annotations: NOT saved"); return 1; }
        var slotBad = SlotDims(drw);   // Grant 15 Nov: never a dimension to a slot's end, side or edge
        Console.WriteLine("Slot check: " + (slotBad.Count == 0 ? "no dimension to a slot end, side or edge" : slotBad.Count + " dimension(s) to slot geometry: " + string.Join(", ", slotBad)));
        if (slotBad.Count > 0) { problems.Add(slotBad.Count + " dimension(s) attached to a slot's end, side or edge: NOT saved"); return 1; }
        if (offDraft > 0) problems.Add(offDraft + " annotations not on the DRAFT layer");

        int err = 0, warn = 0;
        bool ok = drwDoc.Extension.SaveAs3(output, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine(ok ? "Saved " + output : "NOT saved: error " + err + ", warning " + warn);
        if (!ok) return 3;
        ok = drwDoc.Extension.SaveAs3(pdfOut, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine(ok ? "PDF " + pdfOut : "PDF failed: error " + err);
        Console.WriteLine("Time: " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s. The drawing is left open in SOLIDWORKS.");
        return 0;
    }

    // Free sheet area for views: inside the border, above the title block (sheet metres; Unilumin A3 format).
    const double AreaX0 = 0.020, AreaX1 = 0.400, AreaY0 = 0.070, AreaY1 = 0.280, RowGap = 0.015, ColGap = 0.020;

    static double W(double[] b) { return b[2] - b[0]; }
    static double H(double[] b) { return b[3] - b[1]; }
    static string Box(double[] b) { return Mm(b[0]) + "," + Mm(b[1]) + " - " + Mm(b[2]) + "," + Mm(b[3]); }

    // A view's box including its annotations: the view outline, every line of every annotation, and each
    // annotation's text (estimated from its text length and height around its position).
    static double[] Extent(View v)
    {
        var o = OutlineOf(v);   // what the view draws (an assembly view's outline also counts suppressed parts)
        double x0 = o[0], y0 = o[1], x1 = o[2], y1 = o[3];
        foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
        {
            var dd = a.GetDisplayData() as DisplayData;
            if (dd == null) continue;
            for (int i = 0; i < dd.GetLineCount(); i++)
            {
                var l = dd.GetLineAtIndex3(i) as double[];
                if (l == null || l.Length < 10) continue;
                x0 = Math.Min(x0, Math.Min(l[4], l[7])); x1 = Math.Max(x1, Math.Max(l[4], l[7]));
                y0 = Math.Min(y0, Math.Min(l[5], l[8])); y1 = Math.Max(y1, Math.Max(l[5], l[8]));
            }
            var pos = a.GetPosition() as double[];
            if (pos == null) continue;
            for (int i = 0; i < dd.GetTextCount(); i++)
            {
                string t = dd.GetTextAtIndex(i) ?? "";
                double h = dd.GetTextHeightAtIndex(i), w = t.Length * h * 0.6, ang = dd.GetTextAngleAtIndex(i);
                bool vert = Math.Abs(Math.Sin(ang)) > 0.7;
                double ex = vert ? h : w, ey = vert ? w : h;
                x0 = Math.Min(x0, pos[0] - ex); x1 = Math.Max(x1, pos[0] + ex);
                y0 = Math.Min(y0, pos[1] - ey); y1 = Math.Max(y1, pos[1] + ey);
            }
        }
        return new[] { x0, y0, x1, y1 };
    }

    static void Ordinate(ModelDoc2 doc, SelectionMgr selMgr, View v, List<Edge> edges, int kind, double x, double y, string what)
    {
        doc.ClearSelection2(true);
        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
        int selected = 0;
        for (int i = 0; i < edges.Count; i++) if (((Entity)edges[i]).Select4(i > 0, sd)) selected++;
        int r = doc.Extension.AddOrdinateDimension(kind, x, y, 0);
        EndOrdinate(doc);   // 15 Nov: SOLIDWORKS stays in "add to this row" mode; the next row's picks went into this one (08X04-PLP-32: a second 155)
        Console.WriteLine("Ordinate " + what + ": " + selected + " of " + edges.Count + " edges selected, result " + r);
        if (selected != edges.Count || r != 0) problems.Add("ordinate dimensions " + what + " may be incomplete (result " + r + ")");
    }

    static double[] ToSheet(MathUtility mu, View v, double[] modelPt)
    {
        var p = (MathPoint)mu.CreatePoint(modelPt);
        p = (MathPoint)p.MultiplyTransform(v.ModelToViewTransform);
        return (double[])p.ArrayData;
    }

    static string Prop(ModelDoc2 m, string config, string name)
    {
        string val, res; bool wasRes, linked;
        foreach (var c in new[] { config, "" })
        {
            var cpm = m.Extension.CustomPropertyManager[c];
            cpm.Get6(name, false, out val, out res, out wasRes, out linked);
            if (!string.IsNullOrEmpty(res)) return res;
        }
        return "";
    }

    // Accept the machine's work: move annotations off layer DRAFT onto no layer (James's annotations have none), so
    // they show in the document's normal colours. Without --all: only what is selected in SOLIDWORKS right now.
    // Works on the drawing as it is open in SOLIDWORKS; does not save (Grant / James saves with Ctrl+S).
    static int Accept(string file, bool all)
    {
        var doc = (ModelDoc2)sw.GetOpenDocumentByName(file) ?? OpenDoc(file, null, false);
        if (doc.IsOpenedReadOnly()) { Console.Error.WriteLine("REFUSED: the drawing is open read-only: " + file); return 2; }
        var anns = new List<Annotation>();
        if (all)
        {
            var drw = (DrawingDoc)doc;
            for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
                foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0]) anns.Add(a);
        }
        else
        {
            var sm = (SelectionMgr)doc.SelectionManager;
            int n = sm.GetSelectedObjectCount2(-1);
            for (int i = 1; i <= n; i++)
            {
                object o = sm.GetSelectedObject6(i, -1);
                Annotation a = o as Annotation;
                if (a == null && o is DisplayDimension) a = (Annotation)((DisplayDimension)o).GetAnnotation();
                if (a == null && o is Note) a = (Annotation)((Note)o).GetAnnotation();
                if (a == null && o is CenterMark) a = (Annotation)((CenterMark)o).GetAnnotation();
                if (a == null) { Console.WriteLine("  selection " + i + " is not a dimension, note or centre mark (type " + sm.GetSelectedObjectType3(i, -1) + "); skipped"); continue; }
                anns.Add(a);
            }
            if (n == 0) { Console.Error.WriteLine("Nothing is selected in SOLIDWORKS. Select the items to accept first, or use --all."); return 2; }
        }
        int moved = 0;
        foreach (var a in anns)
        {
            if (a.Layer != DraftLayer) continue;
            a.Layer = "";
            if (a.Layer == DraftLayer) problems.Add("could not move " + a.GetName());
            else { moved++; Console.WriteLine("  accepted " + TypeName(a.GetType()) + " " + a.GetName()); }
        }
        doc.GraphicsRedraw2();
        int left = 0;
        for (var v = (View)((DrawingDoc)doc).GetFirstView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0]) if (a.Layer == DraftLayer) left++;
        Console.WriteLine("Accepted " + moved + "; still on DRAFT (magenta): " + left + ". Not saved: save in SOLIDWORKS (Ctrl+S) to keep it.");
        return 0;
    }

    // Select (in SOLIDWORKS) every annotation still on layer DRAFT, so the person checking can Ctrl+click to drop some
    // and then accept the rest (Layer list -None-, or Draw.exe accept).
    static int SelectDraft(string file)
    {
        var doc = (ModelDoc2)sw.GetOpenDocumentByName(file);
        if (doc == null) { Console.Error.WriteLine("Not open in SOLIDWORKS: " + file); return 2; }
        sw.ActivateDoc3(doc.GetTitle(), false, 0, 0);
        doc.ClearSelection2(true);
        int n = 0, total = 0;
        for (var v = (View)((DrawingDoc)doc).GetFirstView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.Layer != DraftLayer) continue;
                total++;
                if (a.Select3(true, null)) n++;
                else
                {
                    // Some kinds (ordinate dimensions after the first of a group, centre marks) refuse Select3; try
                    // selecting the specific object instead.
                    object o = a.GetSpecificAnnotation();
                    bool ok = false;
                    var dd = o as DisplayDimension; var cm = o as CenterMark;
                    if (cm != null) ok = cm.Select(true, null);
                    else ok = a.Select2(true, 0);
                    if (ok) n++; else Console.WriteLine("  could not select " + TypeName(a.GetType()) + " " + a.GetName());
                }
            }
        Console.WriteLine("Selected " + n + " of " + total + " items on DRAFT (SOLIDWORKS says " + ((SelectionMgr)doc.SelectionManager).GetSelectedObjectCount2(-1) + " selected).");
        return 0;
    }

    // A look at a model: front/top/right (third angle) and an isometric on James's sheet, exported to a PDF, then
    // closed without saving. Also lists the planar faces (normal, area) and cylinders, to plan a drawing.
    static int Views(string partFile, string config, string pdfOut)
    {
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists: " + pdfOut); return 2; }
        string template = Path.Combine(TestRoot, @"_Templates\Unilumin-A3.DRWDOT");
        var part = OpenDoc(partFile, config, true);
        if (config != null && part.ConfigurationManager.ActiveConfiguration.Name != config) part.ShowConfiguration2(config);
        Console.WriteLine("Configuration: " + part.ConfigurationManager.ActiveConfiguration.Name);
        foreach (Body2 b in (object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true))
        {
            var box = (double[])b.GetBodyBox();
            Console.WriteLine("Body " + b.Name + ": box " + Mm(box[0]) + "," + Mm(box[1]) + "," + Mm(box[2]) + " - " + Mm(box[3]) + "," + Mm(box[4]) + "," + Mm(box[5]));
            foreach (Face2 f in (object[])b.GetFaces())
            {
                var s = (Surface)f.GetSurface();
                var fb = (double[])f.GetBox();
                string where = Mm(fb[0]) + "," + Mm(fb[1]) + "," + Mm(fb[2]) + " - " + Mm(fb[3]) + "," + Mm(fb[4]) + "," + Mm(fb[5]);
                var feat = f.GetFeature() as Feature;
                string fn = feat != null ? feat.Name : "?";
                if (s.IsPlane())
                {
                    var pp = (double[])s.PlaneParams;
                    var n = (double[])f.Normal;
                    Console.WriteLine("  plane n(" + n[0].ToString("0.##") + "," + n[1].ToString("0.##") + "," + n[2].ToString("0.##") + ") area " + (f.GetArea() * 1e6).ToString("0") + " mm2, loops " + f.GetLoopCount() + ", box " + where + " [" + fn + "]");
                }
                else if (s.IsCylinder())
                {
                    var cp = (double[])s.CylinderParams;
                    Console.WriteLine("  cylinder r " + Mm(cp[6]) + " axis (" + cp[3].ToString("0.##") + "," + cp[4].ToString("0.##") + "," + cp[5].ToString("0.##") + ") at " + Mm(cp[0]) + "," + Mm(cp[1]) + "," + Mm(cp[2]) + ", box " + where + " [" + fn + "]");
                }
                else Console.WriteLine("  other surface, box " + where + " [" + fn + "]");
            }
        }
        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        var drw = (DrawingDoc)drwDoc;
        bool ok3 = drw.Create3rdAngleViews2(partFile);
        drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.300, 0.120, 0);
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
        {
            if (config != null) v.ReferencedConfiguration = config;
            Console.WriteLine("  view " + v.GetName2() + " '" + v.GetOrientationName() + "' 1:" + (1 / v.ScaleDecimal).ToString("0.#"));
        }
        drwDoc.ForceRebuild3(false);
        int err = 0, warn = 0;
        bool ok = drwDoc.Extension.SaveAs3(pdfOut, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine((ok3 ? "" : "3 views not created; ") + (ok ? "PDF " + pdfOut : "PDF failed " + err));
        sw.CloseDoc(drwDoc.GetTitle());
        return ok ? 0 : 3;
    }

    static int Pdf(string file, string output)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists: " + output); return 2; }
        var doc = OpenDoc(file, null, true);
        int err = 0, warn = 0;
        bool ok = doc.Extension.SaveAs3(output, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine(ok ? "PDF: " + output : "PDF failed: error " + err + ", warning " + warn);
        return ok ? 0 : 3;
    }
}
