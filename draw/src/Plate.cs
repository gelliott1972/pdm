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
        LayoutBlocked = null; WeldedLayout = false;

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
        weldedOthers = bodies.Length - 1; WeldedLayout = weldedOthers > 0;
        // Welded bracket route (item 5, Grant 16 Nov): the plate dimensioned as before, plus where each welded body sits on
        // the plate (its outer sides in the plate's ordinate rows), how far it stands off the plate (side view), James's
        // cut-list table top left and balloons on the isometric. Welds stay with James.
        var others = bodies.Cast<Body2>().Where(b => b != body).ToList();
        var plateName = body.Name;
        var otherNames = new HashSet<string>(others.Select(b => b.Name));
        Func<Edge, string> bodyOf = e => { try { var b = e.GetBody() as Body2; return b == null ? "" : b.Name; } catch { return ""; } };
        Body2 angledArm = null; double[] armAxis = null; double armAngle = 0; View angleView = null;
        var mu0 = (MathUtility)sw.GetMathUtility();
        if (weldedOthers > 0) Console.WriteLine("Welded bracket: plate " + body.Name + "; welded: " + string.Join(", ", others.Select(b => b.Name)));
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
        // An arm welded at an angle (08X04-BKP-06, 57°; Grant 16 Nov "we should have a side projection for this"): the
        // side projection looks across the arm so its angle shows true. When the arm runs across the face view, the face
        // view is turned a quarter turn first, so the arm runs up the sheet and the side view (to its right) is that one.
        var nrm = new double[3]; nrm[thin] = 1;
        foreach (var ob in others)
        {
            var ax = TubeAxis(ob);
            if (ax == null) continue;
            double cosA = Math.Abs(ax[0] * nrm[0] + ax[1] * nrm[1] + ax[2] * nrm[2]);
            if (cosA > 1e-3 && cosA < 1 - 1e-3) { angledArm = ob; armAxis = ax; break; }
        }
        if (angledArm != null)
        {
            var c0 = ((double[])angledArm.GetBodyBox()); var pc = new[] { (c0[0] + c0[3]) / 2, (c0[1] + c0[4]) / 2, (c0[2] + c0[5]) / 2 };
            var s0 = ToSheet(mu0, main, pc); var s1 = ToSheet(mu0, main, new[] { pc[0] + armAxis[0] * 0.1, pc[1] + armAxis[1] * 0.1, pc[2] + armAxis[2] * 0.1 });
            double adx = Math.Abs(s1[0] - s0[0]), ady = Math.Abs(s1[1] - s0[1]);
            armAngle = 90 - Math.Acos(Math.Abs(armAxis[0] * nrm[0] + armAxis[1] * nrm[1] + armAxis[2] * nrm[2])) * 180 / Math.PI;   // from the plate face
            Console.WriteLine("Angled arm " + angledArm.Name + ": " + Math.Round(armAngle, 2) + "° to the plate face; runs " + (adx >= ady ? "across" : "up") + " the face view");
            if (Math.Min(adx, ady) > 0.05 * Math.Max(adx, ady)) plateSlotFlags.Add("CHECK: ARM AT A COMPOUND ANGLE: NO VIEW SHOWS ITS TRUE ANGLE");
            if (adx >= ady)
            {
                main.Angle = Math.PI / 2;
                drwDoc.ForceRebuild3(false);
                mo = (double[])main.GetOutline();
                var t0s = ToSheet(mu0, main, pc); var t1s = ToSheet(mu0, main, new[] { pc[0] + armAxis[0] * 0.1, pc[1] + armAxis[1] * 0.1, pc[2] + armAxis[2] * 0.1 });
                Console.WriteLine("Face view turned a quarter turn: the arm now runs " + (Math.Abs(t1s[0] - t0s[0]) < Math.Abs(t1s[1] - t0s[1]) ? "up" : "ACROSS (not turned?)") + " the sheet");
            }
        }
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var side = drw.CreateUnfoldedViewAt3(mo[2] + 0.040, (mo[1] + mo[3]) / 2, 0, false);
        if (side == null) problems.Add("side view not created");
        drwDoc.ClearSelection2(true);
        if (angledArm != null) angleView = side;
        // Welded bracket isometric (Grant 16 Nov, 08X04-BKP-06: "the isometric angle could be improved"): the standard
        // isometric and the views from each corner of the face and side views; the one that shows the largest share of
        // each body's edges is kept, the others deleted.
        View pickIso = null;
        if (weldedOthers > 0)
        {
            var cands = new List<View>();
            var star = drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.100, 0.100, 0);
            if (star != null) { star.ReferencedConfiguration = config; cands.Add(star); }
            foreach (var par in new[] { main, side })
            {
                if (par == null) continue;
                var po = (double[])par.GetOutline();
                double pcx = (po[0] + po[2]) / 2, pcy = (po[1] + po[3]) / 2, hw = (po[2] - po[0]) / 2 + 0.030, hh = (po[3] - po[1]) / 2 + 0.030;
                foreach (var dxy in new[] { new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 }, new[] { 1, 1 } })
                {
                    drwDoc.ClearSelection2(true);
                    drwDoc.Extension.SelectByID2(par.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                    var v = drw.CreateUnfoldedViewAt3(pcx + dxy[0] * hw, pcy + dxy[1] * hh, 0, false);
                    if (v != null) cands.Add(v);
                }
            }
            drwDoc.ClearSelection2(true);
            drwDoc.ForceRebuild3(false);
            var totals = bodies.Cast<Body2>().ToDictionary(b => b.Name, b => Math.Max(1, ((object[])b.GetEdges() ?? new object[0]).Length));
            double bestScore = -1;
            foreach (var v in cands)
            {
                var seen = ((v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0]).Cast<Edge>().GroupBy(e => bodyOf(e)).ToDictionary(g => g.Key, g => g.Count());
                double score = totals.Sum(t => Math.Min(1.0, (seen.ContainsKey(t.Key) ? seen[t.Key] : 0) / (double)t.Value)) / totals.Count;
                Console.WriteLine("  isometric candidate " + v.GetName2() + " '" + v.GetOrientationName() + "': shows " + Math.Round(100 * score) + "% of the bodies' edges");
                if (score > bestScore + 1e-9) { bestScore = score; pickIso = v; }
            }
            foreach (var v in cands.Where(v => v != pickIso).ToList())
            {
                drwDoc.ClearSelection2(true);
                if (!(drwDoc.Extension.SelectByID2(v.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0) && drwDoc.Extension.DeleteSelection2(0))) problems.Add("isometric candidate " + v.GetName2() + " not deleted");
            }
            drwDoc.ClearSelection2(true);
            if (pickIso != null) { pickIso.RemoveAlignment(); Console.WriteLine("Isometric: " + pickIso.GetName2() + " kept"); }
        }
        var iso = pickIso ?? drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.100, 0.100, 0);
        if (iso == null) problems.Add("isometric not created");
        else
        {
            iso.ReferencedConfiguration = config;
            drwDoc.ForceRebuild3(false);
            var io = (double[])iso.GetOutline();
            double w1 = (io[2] - io[0]) / iso.ScaleDecimal, h1 = (io[3] - io[1]) / iso.ScaleDecimal;   // at 1:1
            double boxW = IsoBoxR - IsoBoxL, boxH = mo[1] - Gap - IsoBoxB;
            // welded brackets: the isometric goes in the bottom-left corner under the cut-list table, at the sheet scale
            // where it fits there (Grant 16 Nov, 08X04-BKP-06: enlarged it from 1:10 to the sheet's 1:5)
            if (weldedOthers > 0) { boxW = 0.140; boxH = 0.215; }
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
        var armH = new List<Tuple<Edge, double, string>>(); var armV = new List<Tuple<Edge, double, string>>();   // welded bodies' straight edges
        foreach (Edge e in (main.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            string bn = weldedOthers > 0 ? bodyOf(e) : plateName;
            if (bn != plateName)
            {
                if (otherNames.Contains(bn) && c.IsLine() && angledArm == null)   // an angled arm is located on its own projection
                {
                    var cpd = e.GetCurveParams3();
                    var a0 = ToSheet(mu, main, (double[])cpd.StartPoint); var b0 = ToSheet(mu, main, (double[])cpd.EndPoint);
                    if (Math.Abs(a0[1] - b0[1]) < 1e-7) armH.Add(Tuple.Create(e, a0[1], bn));
                    else if (Math.Abs(a0[0] - b0[0]) < 1e-7) armV.Add(Tuple.Create(e, a0[0], bn));
                }
                continue;
            }
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
            // each welded body's outer sides as seen on the plate face (its left and right, top and bottom), unless a
            // plate edge or hole is already at that place
            var armCols = armV.GroupBy(l => l.Item3).SelectMany(g => new[] { g.OrderBy(l => l.Item2).First(), g.OrderBy(l => l.Item2).Last() })
                .Where(l => Math.Abs(l.Item2 - leftE.Item2) > 1e-6 && Math.Abs(l.Item2 - rightE.Item2) > 1e-6)
                .GroupBy(l => Math.Round(l.Item2 * 1e5)).Select(g => g.First()).ToList();
            var armRows = armH.GroupBy(l => l.Item3).SelectMany(g => new[] { g.OrderBy(l => l.Item2).First(), g.OrderBy(l => l.Item2).Last() })
                .Where(l => Math.Abs(l.Item2 - topE.Item2) > 1e-6 && Math.Abs(l.Item2 - botE.Item2) > 1e-6)
                .GroupBy(l => Math.Round(l.Item2 * 1e5)).Select(g => g.First()).ToList();
            if (weldedOthers > 0) Console.WriteLine("Welded bodies on the face view: sides across " + string.Join("/", armCols.Select(l => Mm((l.Item2 - leftE.Item2) / vs)))
                + ", down " + string.Join("/", armRows.Select(l => Mm((topE.Item2 - l.Item2) / vs))));
            if (weldedOthers > 0 && angledArm == null && armCols.Count + armRows.Count == 0) plateSlotFlags.Add("CHECK: WELDED " + (weldedOthers == 1 ? "BODY" : "BODIES") + " NOT LOCATED ON THE PLATE");
            if (slots.Count == 0)
            {
                var hSel = new List<Edge> { leftE.Item1 }; hSel.AddRange(cols.Select(c => c.Item1)); hSel.AddRange(armCols.Select(l => l.Item1)); hSel.Add(rightE.Item1);
                var vSel = new List<Edge> { topE.Item1 }; vSel.AddRange(rows.Select(c => c.Item1)); vSel.AddRange(armRows.Select(l => l.Item1)); vSel.Add(botE.Item1);
                expectedOrd.Add(0); expectedOrd.AddRange(cols.Select(c => (c.Item2[0] - leftE.Item2) / vs * 1000)); expectedOrd.AddRange(armCols.Select(l => (l.Item2 - leftE.Item2) / vs * 1000)); expectedOrd.Add((rightE.Item2 - leftE.Item2) / vs * 1000);
                expectedOrd.Add(0); expectedOrd.AddRange(rows.Select(c => (topE.Item2 - c.Item2[1]) / vs * 1000)); expectedOrd.AddRange(armRows.Select(l => (topE.Item2 - l.Item2) / vs * 1000)); expectedOrd.Add((topE.Item2 - botE.Item2) / vs * 1000);
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
                foreach (var l in armCols) if (!hVals.Any(v => Math.Abs(v - (l.Item2 - leftE.Item2) / vs * 1000) < 0.01)) { hItems.Add(l.Item1); hVals.Add((l.Item2 - leftE.Item2) / vs * 1000); }
                hItems.Add(rightE.Item1); hVals.Add((rightE.Item2 - leftE.Item2) / vs * 1000);
                var vItems = new List<object> { topE.Item1 }; var vVals = new List<double> { 0 };
                foreach (var c in rows) { vItems.Add(c.Item1); vVals.Add((topE.Item2 - c.Item2[1]) / vs * 1000); }
                foreach (var q in rightSlots) if (!vVals.Any(v => Math.Abs(v - (topE.Item2 - q[1]) / vs * 1000) < 0.01)) { vItems.Add(q); vVals.Add((topE.Item2 - q[1]) / vs * 1000); }
                foreach (var l in armRows) if (!vVals.Any(v => Math.Abs(v - (topE.Item2 - l.Item2) / vs * 1000) < 0.01)) { vItems.Add(l.Item1); vVals.Add((topE.Item2 - l.Item2) / vs * 1000); }
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

        // 8. Thickness on the side view: the plate's two outermost vertical edges. A welded bracket: also how far each
        //    welded body stands off the plate, from the plate face it sits on to its far end (expected from the model).
        var expectedLin = new List<double> { ext[thin] * 1000 };
        if (side != null)
        {
            var lines = new List<Tuple<Edge, double[], double[]>>();
            var armLines = new List<Tuple<Edge, double[], double[], string>>();
            foreach (Edge e in (side.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                var c = (Curve)e.GetCurve();
                if (!c.IsLine()) continue;
                var ps = (double[])e.GetCurveParams3().StartPoint; var pe = (double[])e.GetCurveParams3().EndPoint;
                var a = ToSheet(mu, side, ps); var b = ToSheet(mu, side, pe);
                if (!(Math.Abs(a[0] - b[0]) < 1e-7 && Math.Abs(a[1] - b[1]) > 1e-4)) continue;
                string bn = weldedOthers > 0 ? bodyOf(e) : plateName;
                if (bn == plateName) lines.Add(Tuple.Create(e, a, b));
                else if (otherNames.Contains(bn)) armLines.Add(Tuple.Create(e, a, b, bn));
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
                else if (Math.Abs(right.Item2[0] - left.Item2[0]) < 0.006) SmallDimOutside(td, left.Item2[0], right.Item2[0], true, "plate thickness");
                // stand-off of each welded body, one dimension per body, stacked above the thickness
                int sk = 0;
                var standDone = new List<double>();   // one dimension per stand-off value (08B04-BKP-01: two angles, both 100)
                foreach (var g in armLines.Where(l => angledArm == null).GroupBy(l => l.Item4))
                {
                    double armL = g.Min(l => l.Item2[0]), armR = g.Max(l => l.Item2[0]);
                    bool onRight = (armL + armR) / 2 > (left.Item2[0] + right.Item2[0]) / 2;
                    var pface = onRight ? right : left;
                    var far = onRight ? g.OrderBy(l => l.Item2[0]).Last() : g.OrderBy(l => l.Item2[0]).First();
                    if (Math.Abs(far.Item2[0] - pface.Item2[0]) < 1e-6) continue;
                    var ob = others.First(b => b.Name == g.Key);
                    var abx = (double[])ob.GetBodyBox(); var pbx = (double[])body.GetBodyBox();
                    double stand = Math.Max(abx[3 + thin] - pbx[3 + thin], pbx[thin] - abx[thin]) * 1000;
                    if (standDone.Any(x => Math.Abs(x - stand) < 0.01)) continue;
                    standDone.Add(stand);
                    expectedLin.Add(stand);
                    drwDoc.ClearSelection2(true);
                    ((Entity)pface.Item1).Select4(false, sd); ((Entity)far.Item1).Select4(true, sd);
                    double ftop = Math.Max(top, g.Max(l => Math.Max(l.Item2[1], l.Item3[1])));
                    var ad = drwDoc.AddDimension2((pface.Item2[0] + far.Item2[0]) / 2, ftop + 0.022 + 0.010 * sk, 0) as DisplayDimension;
                    if (ad == null) problems.Add("stand-off of " + g.Key + " not placed");
                    else Console.WriteLine("Stand-off of " + g.Key + ": " + Math.Round(stand, 1));
                    sk++;
                }
            }
            drwDoc.ClearSelection2(true);
        }

        // 8b. Angled arm, on its projection: the angle to the plate face; how far the arm (with its cap) stands off the
        //     plate face; where the arm's sides meet the plate, from the plate's nearer end.
        var expectedAng = new List<double>();
        if (angledArm != null && angleView != null)
        {
            var av = angleView;
            var sdA = (SelectData)selMgr.CreateSelectData(); sdA.View = av;
            var org = ToSheet(mu, av, new[] { 0.0, 0, 0 });
            Func<double[], double[]> dirIn = v => { var b = ToSheet(mu, av, new[] { v[0] * 0.1, v[1] * 0.1, v[2] * 0.1 }); double ddx = b[0] - org[0], ddy = b[1] - org[1], l = Math.Sqrt(ddx * ddx + ddy * ddy); return l < 1e-9 ? null : new[] { ddx / l, ddy / l }; };
            var dn = dirIn(nrm); var da = dirIn(armAxis);
            Func<double[], double[], double> dot2 = (p, q) => p[0] * q[0] + p[1] * q[1];
            var segs = new List<Tuple<Edge, double[], double[], string>>();
            foreach (Edge e in (av.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                if (!((Curve)e.GetCurve()).IsLine()) continue;
                var cp = e.GetCurveParams3();
                segs.Add(Tuple.Create(e, ToSheet(mu, av, (double[])cp.StartPoint), ToSheet(mu, av, (double[])cp.EndPoint), bodyOf(e)));
            }
            Func<Tuple<Edge, double[], double[], string>, double> segLen = s => Math.Sqrt(Math.Pow(s.Item3[0] - s.Item2[0], 2) + Math.Pow(s.Item3[1] - s.Item2[1], 2));
            Func<Tuple<Edge, double[], double[], string>, double[]> segDir = s => { double l = segLen(s); return new[] { (s.Item3[0] - s.Item2[0]) / l, (s.Item3[1] - s.Item2[1]) / l }; };
            if (dn == null || da == null) problems.Add("angled arm: directions not found on its projection");
            else
            {
                var plateFaces = segs.Where(s => s.Item4 == plateName && segLen(s) > 0.01 && Math.Abs(dot2(segDir(s), dn)) < 1e-3).ToList();
                var plateEnds = segs.Where(s => s.Item4 == plateName && segLen(s) > 1e-4 && Math.Abs(Math.Abs(dot2(segDir(s), dn)) - 1) < 1e-3).ToList();
                var armSegs = segs.Where(s => s.Item4 == angledArm.Name).ToList();
                var weldSegs = segs.Where(s => s.Item4 != plateName && otherNames.Contains(s.Item4)).ToList();
                Console.WriteLine("Arm projection: " + plateFaces.Count + " plate face lines, " + plateEnds.Count + " plate ends, " + armSegs.Count + " arm lines");
                if (plateFaces.Count == 0 || armSegs.Count == 0) problems.Add("angled arm: plate face or arm not found on its projection");
                else
                {
                    double armMid = armSegs.Average(s => dot2(new[] { (s.Item2[0] + s.Item3[0]) / 2, (s.Item2[1] + s.Item3[1]) / 2 }, dn));
                    var pf = plateFaces.OrderBy(s => Math.Abs(dot2(s.Item2, dn) - armMid)).First();
                    double pfAt = dot2(pf.Item2, dn);
                    double sgn = armMid > pfAt ? 1 : -1;
                    var dOut = new[] { dn[0] * sgn, dn[1] * sgn };   // away from the plate, towards the arm
                    var daOut = dot2(da, dOut) > 0 ? da : new[] { -da[0], -da[1] };
                    var uf = segDir(pf); if (dot2(uf, daOut) < 0) uf = new[] { -uf[0], -uf[1] };   // along the face, on the arm's acute side
                    // 1. angle: the arm's longest side line and the plate face
                    var armSide = armSegs.Where(s => Math.Abs(Math.Abs(dot2(segDir(s), da)) - 1) < 1e-3).OrderByDescending(segLen).FirstOrDefault();
                    if (armSide == null) problems.Add("angled arm: no side line along the arm");
                    else
                    {
                        // where the side line meets the face line
                        var p = armSide.Item2; var r = segDir(armSide); var q = pf.Item2; var sv = uf;
                        double den2 = r[0] * sv[1] - r[1] * sv[0];
                        double t = Math.Abs(den2) < 1e-12 ? 0 : ((q[0] - p[0]) * sv[1] - (q[1] - p[1]) * sv[0]) / den2;
                        var ip = new[] { p[0] + r[0] * t, p[1] + r[1] * t };
                        var bis = new[] { uf[0] + daOut[0], uf[1] + daOut[1] }; double bl = Math.Sqrt(bis[0] * bis[0] + bis[1] * bis[1]);
                        drwDoc.ClearSelection2(true);
                        ((Entity)pf.Item1).Select4(false, sdA); ((Entity)armSide.Item1).Select4(true, sdA);
                        var angD = drwDoc.AddDimension2(ip[0] + 0.025 * bis[0] / bl, ip[1] + 0.025 * bis[1] / bl, 0) as DisplayDimension;
                        if (angD == null) problems.Add("arm angle not placed");
                        else { expectedAng.Add(armAngle); Console.WriteLine("Arm angle: " + Math.Round(armAngle, 2) + "°"); }
                    }
                    // 2. stand-off: the plate face to the farthest point of the welded bodies
                    var verts = new List<Tuple<object, double[], string>>();
                    foreach (var s in weldSegs)
                    {
                        var v0 = s.Item1.GetStartVertex(); var v1 = s.Item1.GetEndVertex();
                        if (v0 != null) verts.Add(Tuple.Create(v0, s.Item2, s.Item4));
                        if (v1 != null) verts.Add(Tuple.Create(v1, s.Item3, s.Item4));
                    }
                    var farV = verts.OrderByDescending(v => dot2(v.Item2, dOut)).FirstOrDefault();
                    if (farV != null)
                    {
                        var pbx = (double[])body.GetBodyBox();
                        double stand = others.Max(b => { var abx = (double[])b.GetBodyBox(); return Math.Max(abx[3 + thin] - pbx[3 + thin], pbx[thin] - abx[thin]); }) * 1000;
                        drwDoc.ClearSelection2(true);
                        ((Entity)pf.Item1).Select4(false, sdA); ((Entity)farV.Item1).Select4(true, sdA);
                        double ext0 = segs.Max(s => Math.Max(dot2(s.Item2, uf), dot2(s.Item3, uf)));
                        double mid0 = (pfAt + dot2(farV.Item2, dn)) / 2;
                        var sd2 = drwDoc.AddDimension2(dn[0] * mid0 + uf[0] * (ext0 + 0.012), dn[1] * mid0 + uf[1] * (ext0 + 0.012), 0) as DisplayDimension;
                        if (sd2 == null) problems.Add("arm stand-off not placed");
                        else { expectedLin.Add(stand); Console.WriteLine("Arm stand-off: " + Math.Round(stand, 1)); }
                    }
                    // 3. where the arm meets the plate: its corners on the plate face, from the plate's nearer end
                    var meet = verts.Where(v => v.Item3 == angledArm.Name && Math.Abs(dot2(v.Item2, dn) - pfAt) < 2e-4)
                        .GroupBy(v => Math.Round(dot2(v.Item2, uf) * 1e5)).Select(g => g.First()).OrderBy(v => dot2(v.Item2, uf)).ToList();
                    if (meet.Count >= 2) meet = new List<Tuple<object, double[], string>> { meet.First(), meet.Last() };
                    if (meet.Count == 0 || plateEnds.Count == 0) plateSlotFlags.Add("CHECK: WHERE THE ARM MEETS THE PLATE IS NOT DIMENSIONED");
                    else
                    {
                        double mAvg = meet.Average(v => dot2(v.Item2, uf));
                        var endL = plateEnds.OrderBy(s => Math.Abs(dot2(s.Item2, uf) - mAvg)).First();
                        double endAt = dot2(endL.Item2, uf);
                        int mk = 0;
                        foreach (var v in meet)
                        {
                            drwDoc.ClearSelection2(true);
                            ((Entity)endL.Item1).Select4(false, sdA); ((Entity)v.Item1).Select4(true, sdA);
                            double along2 = (endAt + dot2(v.Item2, uf)) / 2, off = pfAt - sgn * (0.015 + 0.010 * mk);
                            var md = drwDoc.AddDimension2(dn[0] * off + uf[0] * along2, dn[1] * off + uf[1] * along2, 0) as DisplayDimension;
                            double val = Math.Abs(dot2(v.Item2, uf) - endAt) / av.ScaleDecimal * 1000;
                            if (md == null) problems.Add("arm meeting point not placed");
                            else { expectedLin.Add(val); Console.WriteLine("Arm meets the plate at " + Math.Round(val, 1) + " from its end"); }
                            mk++;
                        }
                    }
                }
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
        // 9a. Welded bracket: James's cut-list table top left (from his 08X04-HBW-07: ITEM NO. | QTY. | DESCRIPTION | LENGTH)
        //     and a balloon per cut-list item on the isometric.
        if (weldedOthers > 0)
        {
            var fl = new List<string>();
            var fsz = face.OrderBy(x => x).ToArray();
            var tubes = others.Select(TubeSection).Where(x => x != null).Distinct().ToList();
            int missing = AddCutList(drw, main, iso, config, template, "PLATE, " + Mm(ext[thin]) + " x " + Mm(fsz[0]) + " x " + Mm(fsz[1]), tubes, fl);
            plateSlotFlags.AddRange(fl);
            if (missing < 0) plateSlotFlags.Add("CHECK: NO CUT LIST IN THE MODEL (NOT A WELDMENT): NO PARTS TABLE OR BALLOONS");
            else if (missing > 0) plateSlotFlags.Add("CHECK: " + missing + " CUT-LIST ITEM(S) WITHOUT A BALLOON");
            lm.SetCurrentLayer(DraftLayer);
        }
        // Notes for James (08B04-SFA-2T CHECK style), outside the border bottom left as Grant placed them on GNRIC-LMW-02
        // (brief draw-3): a model with no material, or a weight far from the same plate in steel
        {
            var pflags = new List<string>(plateSlotFlags);
            if (weldedOthers > 0) pflags.Add("CHECK: WELDS NOT ADDED");
            if (weldedOthers > 0)
            {
                // holes and slots in the welded bodies (an arm's bolt holes, an angle's slots) are not dimensioned yet
                int wh = others.Sum(b => ((object[])b.GetFaces()).Cast<Face2>().Count(f => ((Surface)f.GetSurface()).IsCylinder() && (f.GetLoopCount() == 2 || SlotEnd(f))));
                if (wh > 0) pflags.Add("CHECK: HOLES/SLOTS IN THE WELDED PARTS NOT DIMENSIONED");
            }
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
                // more than two: two to a line, so the note stays in the strip under the border (08X04-BKP-02, 16 Nov)
                var flines = PackFlags(pflags);
                var fn = drwDoc.InsertNote(string.Join("\n", flines)) as Note;
                if (fn == null) problems.Add("CHECK note not placed");
                else
                {
                    ((Annotation)fn.GetAnnotation()).SetPosition2(0.0034, 0.0054 + 0.0045 * (flines.Count - 1), 0);
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

        // 9c. Layout. Face and side views as before (learned from Grant's drawings, clear of the cut-list table). Then
        //     ONE step for the isometric (Grant 16 Nov: "I tend to tuck the isometric into the lower corner (not hard)
        //     and adjust the details accordingly; I try to leave as much room for the main drawings as possible";
        //     checked against his 11 approved plates and brackets): at the sheet scale, its drawn part about 28 mm in
        //     and 32 mm up; the note block 18 mm to its right (past any balloons), level with its middle. Only if the
        //     isometric or its note then hits a view or the title block: one scale smaller (08X04-PLP-03 was approved
        //     like that), at most twice, else a CHECK note. Replaces the earlier slide / shrink / note-under steps.
        Layout(drwDoc, main, side, null);
        PlaceIso(drwDoc, main, side, iso, den);

        // 10. Self-check: every ordinate value equals the model's geometry; callouts and thickness likewise.
        var gotOrd = new List<double>(); var gotDia = new List<double>(); var gotThk = new List<double>(); var gotAng = new List<double>();
        foreach (var vw in new[] { main, side, angleView != side ? angleView : null })
            if (vw != null) foreach (Annotation a in (vw.GetAnnotations() as object[]) ?? new object[0])
                {
                    if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                    var dd = (DisplayDimension)a.GetSpecificAnnotation();
                    double val = ((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000;
                    int dt = dd.GetType();
                    if (dt == (int)swDimensionType_e.swAngularDimension) { double deg = val / 1000 * 180 / Math.PI; gotAng.Add(deg > 90 && expectedAng.Count > 0 && expectedAng[0] < 90 ? 180 - deg : deg); }
                    else if (dt == 1 || dt == 7 || dt == 8) gotOrd.Add(val);
                    else if (dt == 5 || dt == 6) gotDia.Add(val);
                    else gotThk.Add(val);
                }
        Func<IEnumerable<double>, string> fmt = l => string.Join(",", l.Select(x => Math.Round(x, 2)).OrderBy(x => x));
        var checks = new[] {
            Tuple.Create("ordinates", fmt(expectedOrd), fmt(gotOrd)),
            Tuple.Create("hole sizes", fmt(groups.Select(g => g.Key)), fmt(gotDia)),
            Tuple.Create("thickness" + (weldedOthers > 0 ? " and stand-off" : ""), fmt(expectedLin), fmt(gotThk)),
            Tuple.Create("angles", fmt(expectedAng), fmt(gotAng)) };
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

    // James's cut-list table top left and a balloon per item on the isometric. Returns the number of items without a
    // balloon, or -1 when the table wouldn't go in. Tries the configuration as named, then its weldment parent
    // ("...<As Welded>"), anchored and then at a point.
    static int AddCutList(DrawingDoc drw, View main, View iso, string config, string template, string plateText = null, List<string> tubes = null, List<string> flagsOut = null)
    {
        var drwDoc = (ModelDoc2)drw;
        string tpl = Path.Combine(Path.GetDirectoryName(template), "Unilumin-cutlist.sldwldtbt");
        if (!File.Exists(tpl)) { Console.WriteLine("no cut-list table template " + tpl); return -1; }
        var names = new List<string> { "", config };   // "" (the view's own) is the one that works (08X04-BKP-02, 16 Nov)
        int lt = config.IndexOf('<'); if (lt > 0) { names.Add(config.Substring(0, lt) + "<As Welded>"); names.Add(config.Substring(0, lt)); }
        WeldmentCutListAnnotation wt = null;
        foreach (var cn in names.Distinct())
        {
            foreach (bool anchored in new[] { true, false })
            {
                drwDoc.ClearSelection2(true);
                wt = main.InsertWeldmentTable(anchored, BorderX0, BorderY1, (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft, cn, tpl);
                Console.WriteLine("  cut-list table, configuration '" + cn + "'" + (anchored ? " anchored" : " at a point") + ": " + (wt == null ? "no" : "yes"));
                if (wt != null) break;
            }
            if (wt != null) break;
        }
        if (wt == null) return -1;
        var ta = (TableAnnotation)wt;
        var an = (Annotation)ta.GetAnnotation();
        an.Layer = DraftLayer;
        AnchorCutLists(drw);   // attached to the border corner, not just placed there (Grant 16 Nov, 08X04-BKP-02)
        LayoutBlocked = BomBox(ta);   // the views keep clear of it, as of an assembly's BOM
        // the plate's row has no description in these models: "PLATE, t x w x l" from the model, as James writes it
        // (08X06-SFW-04 "PLATE, 5 x 32 x 72"); cut-list labels checked against the tubes measured in the model
        var blank = Enumerable.Range(1, ta.RowCount - 1).Where(r => Flat(ta.Text[r, 2]).Trim() == "" && Flat(ta.Text[r, 3]).Trim() == "").ToList();
        if (plateText != null && blank.Count == 1)
        {
            bool editable = ta.IsCellTextEditable(blank[0], 2);
            ta.Text[blank[0], 2] = plateText;
            if (Flat(ta.Text[blank[0], 2]).Trim() == "") ta.set_Text2(blank[0], 2, false, plateText);
            string now = Flat(ta.Text[blank[0], 2]).Trim();
            Console.WriteLine("  plate row " + blank[0] + ": " + (now == plateText ? plateText + " (typed from the model)" : "NOT written (editable " + editable + ", reads '" + now + "')"));
            if (now != plateText && flagsOut != null) flagsOut.Add("CHECK: CUT LIST: PLATE ROW HAS NO DESCRIPTION (" + plateText.ToUpper() + ")");
        }
        else if (plateText != null && flagsOut != null) flagsOut.Add("CHECK: CUT LIST: " + blank.Count + " ROW(S) WITHOUT A DESCRIPTION");
        // Grant 16 Nov: end caps come up empty (the model's cut-list item has no Description; fixed in the model, not here)
        else if (blank.Count > 0 && flagsOut != null) flagsOut.Add("CHECK: CUT LIST ITEM " + string.Join(", ", blank.Select(r => Flat(ta.Text[r, 0]).Trim())) + " HAS NO DESCRIPTION IN THE MODEL");
        if (tubes != null && flagsOut != null)
            for (int r = 1; r < ta.RowCount; r++)
            {
                var m = System.Text.RegularExpressions.Regex.Match(Flat(ta.Text[r, 2]), @"\b(RHS|SHS)\s*(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?)\s*[xX]\s*(\d+(?:\.\d+)?)");
                if (!m.Success) continue;
                string lab = m.Groups[1].Value.ToUpper() + " " + m.Groups[2].Value + "x" + m.Groups[3].Value + "x" + m.Groups[4].Value;
                if (!tubes.Contains(lab)) flagsOut.Add("CHECK: CUT LIST SAYS " + m.Value.ToUpper().Replace(" ", "") + ", MODEL IS " + (tubes.Count == 1 ? tubes[0] : string.Join(" / ", tubes)));
            }
        for (int r = 1; r < ta.RowCount; r++) Console.WriteLine("  cut list: " + string.Join(" | ", Enumerable.Range(0, ta.ColumnCount).Select(c => Flat(ta.Text[r, c]))));
        if (iso == null) return ta.RowCount - 1;
        drw.ActivateView(iso.GetName2());
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(iso.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var bo = drw.CreateAutoBalloonOptions();
        bo.Layout = (int)swBalloonLayoutType_e.swDetailingBalloonLayout_Square;
        bo.IgnoreMultiple = true;
        bo.Style = (int)swBalloonStyle_e.swBS_Circular;
        bo.Size = (int)swBalloonFit_e.swBF_2Chars;
        bo.UpperTextContent = (int)swBalloonTextContent_e.swBalloonTextItemNumber;
        bo.Layername = DraftLayer;
        var balloons = ((drw.AutoBalloon5(bo) as object[]) ?? new object[0]).Cast<Note>().ToList();
        int nb = balloons.Count;
        drwDoc.ClearSelection2(true);
        Console.WriteLine("Balloons: " + nb + " for " + (ta.RowCount - 1) + " cut-list items");
        PlaceBalloons(drwDoc, iso, balloons);
        return Math.Max(0, ta.RowCount - 1 - nb);
    }

    // Each balloon outward from the isometric's centre, through the point its leader touches, 10 mm outside the view's
    // outline; never below the view (the note block goes there): those go to the left or right side instead. Balloons
    // closer than 10 mm slide apart along their side.
    static void PlaceBalloons(ModelDoc2 doc, View iso, List<Note> balloons)
    {
        if (balloons.Count == 0) return;
        doc.ForceRebuild3(false);
        var o = DrawnBox(iso);   // Grant 16 Nov (08C04-SFP-04: he pulled the balloons in to the part): the drawn part, not the outline box
        double cx = (o[0] + o[2]) / 2, cy = (o[1] + o[3]) / 2, m = 0.012;
        var placed = new List<double[]>();
        // points along what the isometric draws (ends, and points between them on straight edges)
        var samples = new List<double[]>();
        {
            var muB = (MathUtility)sw.GetMathUtility();
            foreach (Edge e in (iso.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
            {
                var cp = e.GetCurveParams3();
                var a = ToSheet(muB, iso, (double[])cp.StartPoint); var b = ToSheet(muB, iso, (double[])cp.EndPoint);
                int steps = Math.Max(1, (int)(Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])) / 0.004));
                for (int i = 0; i <= steps; i++) samples.Add(new[] { a[0] + (b[0] - a[0]) * i / steps, a[1] + (b[1] - a[1]) * i / steps });
            }
        }
        foreach (var n in balloons)
        {
            var an = (Annotation)n.GetAnnotation();
            var bp = (double[])an.GetPosition();
            var lp = an.GetLeaderPointsAtIndex(0) as double[];
            if (lp == null || lp.Length < 3) continue;
            // the leader point farthest from the balloon is where it touches the part
            double[] at = null; double far = -1;
            for (int i = 0; i + 2 < lp.Length; i += 3) { double d = Math.Pow(lp[i] - bp[0], 2) + Math.Pow(lp[i + 1] - bp[1], 2); if (d > far) { far = d; at = new[] { lp[i], lp[i + 1] }; } }
            double dx = at[0] - cx, dy = at[1] - cy;
            if (Math.Abs(dx) < 1e-6 && Math.Abs(dy) < 1e-6) dx = -1;
            // where the ray leaves the outline grown by m
            double hx = (o[2] - o[0]) / 2 + m, hy = (o[3] - o[1]) / 2 + m;
            double t = Math.Min(Math.Abs(dx) > 1e-9 ? hx / Math.Abs(dx) : double.MaxValue, Math.Abs(dy) > 1e-9 ? hy / Math.Abs(dy) : double.MaxValue);
            double x = cx + dx * t, y = cy + dy * t;
            if (y < cy - hy + 1e-6) { x = dx < 0 ? cx - hx : cx + hx; y = Math.Max(at[1], o[1]); }   // not under the view
            // Grant 16 Nov (08C04-SFP-04: he pulled the balloons in close to the part): step out from the touch point and
            // stop at the first spot clear of the drawn edges, when that is nearer than the edge of the box
            {
                double len = Math.Sqrt(dx * dx + dy * dy), ux = dx / len, uy = dy / len;
                double full = Math.Sqrt((x - at[0]) * (x - at[0]) + (y - at[1]) * (y - at[1]));
                for (double s = 0.014; s < full; s += 0.003)
                {
                    double bx = at[0] + ux * s, by = at[1] + uy * s;
                    if (by < o[1] - 0.002) break;   // never under the part
                    if (samples.All(q => (q[0] - bx) * (q[0] - bx) + (q[1] - by) * (q[1] - by) > 0.009 * 0.009)) { x = bx; y = by; break; }
                }
            }
            foreach (var q in placed.Where(q => Math.Abs(q[0] - x) < m && Math.Abs(q[1] - y) < m).ToList())
            {
                if (Math.Abs(Math.Abs(x - cx) - hx) < 1e-6) y = q[1] + m * (y >= q[1] ? 1 : -1); else x = q[0] + m * (x >= q[0] ? 1 : -1);
            }
            an.SetPosition2(x, y, 0);
            placed.Add(new[] { x, y });
        }
        doc.ForceRebuild3(false);
    }

    // The isometric step (Grant 16 Nov, checked against his approved plates and brackets): see the comment at 9c in
    // MakePlate. Shared by make-plate and make-basic.
    static bool IsoClearOfDims;   // make-basic: the isometric keeps clear of the views' dimensions, and the views may move up for it
    static void PlaceIso(ModelDoc2 drwDoc, View main, View side, View iso, int den)
    {
        if (iso != null)
        {
            var muI = (MathUtility)sw.GetMathUtility();
            Func<double[]> drawn = () =>
            {
                double x0 = 1, y0 = 1, x1 = -1, y1 = -1;
                foreach (Edge e in (iso.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
                {
                    var cp = e.GetCurveParams3();
                    foreach (var pt in new[] { (double[])cp.StartPoint, (double[])cp.EndPoint })
                    {
                        var q = ToSheet(muI, iso, pt);
                        x0 = Math.Min(x0, q[0]); y0 = Math.Min(y0, q[1]); x1 = Math.Max(x1, q[0]); y1 = Math.Max(y1, q[1]);
                    }
                }
                return new[] { x0, y0, x1, y1 };
            };
            Func<Annotation, double[]> boxOf = a => { var e5 = ((Note)a.GetSpecificAnnotation()).GetExtent() as double[]; return e5 == null ? null : new[] { Math.Min(e5[0], e5[3]), Math.Min(e5[1], e5[4]), Math.Max(e5[0], e5[3]), Math.Max(e5[1], e5[4]) }; };
            Func<double[], double[], bool> hits = (p, q) => p != null && q != null && p[0] < q[2] && p[2] > q[0] && p[1] < q[3] && p[3] > q[1];
            var titleBox = new[] { TitleLeft, 0.0, 1.0, TitleTop };
            // a view's box for the clash test: the view itself; with IsoClearOfDims (make-basic: rows of ordinates under the
            // view) its dimensions too
            Func<View, double[]> vbox = vw =>
            {
                var b = Grow(OutlineOf(vw), 0.005);
                if (!IsoClearOfDims) return b;
                var e = ExtentTrue(vw);
                return new[] { Math.Min(b[0], e[0]), Math.Min(b[1], e[1]), Math.Max(b[2], e[2]), Math.Max(b[3], e[3]) };
            };
            string clash = null;
            int isoScale = den;
            for (int step = 0; step < 3; step++)
            {
                if (isoScale == den) iso.UseSheetScale = 1; else { iso.UseSheetScale = 0; iso.ScaleRatio = new double[] { 1, isoScale }; }
                drwDoc.ForceRebuild3(false);
                var anns = ((iso.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => a.GetType() == (int)swAnnotationType_e.swNote).ToList();
                Func<List<double[]>> balloonBoxes = () => anns.Where(a => ((Note)a.GetSpecificAnnotation()).IsBomBalloon()).Select(boxOf).Where(x => x != null).ToList();
                // after a change of scale the balloons are still where the bigger isometric had them: place them again
                if (step > 0) PlaceBalloons(drwDoc, iso, anns.Where(a => ((Note)a.GetSpecificAnnotation()).IsBomBalloon()).Select(a => (Note)a.GetSpecificAnnotation()).ToList());
                // the corner: drawn part 28 mm in and 32 mm up; balloons to its left stay 20 mm in (inside the border)
                var dw = drawn();
                var bb = balloonBoxes();
                double leftNow = Math.Min(dw[0] - 0.008, bb.Count > 0 ? bb.Min(x => x[0]) : dw[0]);
                var ip = (double[])iso.Position;
                double bottomNow = Math.Min(dw[1], bb.Count > 0 ? bb.Min(x => x[1]) - 0.004 : dw[1]);   // a balloon under the part: clear of the revision table (08C04-SFP-04)
                iso.Position = new[] { ip[0] + (0.020 - leftNow), ip[1] + (0.032 - bottomNow) };
                drwDoc.ForceRebuild3(false);
                dw = drawn(); bb = balloonBoxes();
                double rightOf = Math.Max(dw[2], bb.Count > 0 ? bb.Max(x => x[2]) : dw[2]);
                double gap = bb.Any(x => x[2] > dw[2]) ? 0.012 : 0.018;
                foreach (var a in anns.Where(a => !((Note)a.GetSpecificAnnotation()).IsBomBalloon()))
                {
                    var nb = boxOf(a);
                    double h = nb == null ? 0.025 : nb[3] - nb[1], w = nb == null ? 0.09 : nb[2] - nb[0];
                    double top = (dw[1] + dw[3]) / 2 + h / 2;                     // level with the isometric's middle
                    if (rightOf + gap + w > TitleLeft && top - h < TitleTop + 0.012) top = TitleTop + 0.012 + h;   // above the title block (Grant, 08X04-PLP-32)
                    a.SetPosition2(rightOf + gap, top, 0);                         // position = the note's top-left
                }
                drwDoc.ForceRebuild3(false);
                // what the isometric puts on the sheet: drawn part, balloons, note block
                Func<List<double[]>> partsNow = () => { var l = new List<double[]> { drawn() }; l.AddRange(anns.Select(boxOf).Where(x => x != null)); return l; };
                Func<List<double[]>, string> clashOf = parts =>
                {
                    string c = null;
                    // make-basic: the isometric's own lines against the views (it may tuck under them where its box is empty,
                    // as Grant laid out 08C04-SFP-04), not its box
                    var pts = IsoClearOfDims ? EdgeSamples(iso, 0.004) : null;
                    for (int pi = 0; pi < parts.Count; pi++)
                    {
                        var pb = parts[pi];
                        if (hits(pb, titleBox)) c = "the title block";
                        foreach (var vw in new[] { main, side })
                        {
                            if (vw == null) continue;
                            var vb = vbox(vw);
                            bool h = pts != null && pi == 0 ? pts.Any(p => p[0] > vb[0] - 0.003 && p[0] < vb[2] + 0.003 && p[1] > vb[1] - 0.003 && p[1] < vb[3] + 0.003) : hits(pb, vb);
                            if (h) c = vw.GetName2();
                        }
                        if (pb[0] < BorderX0 || pb[1] < BorderY0) c = "the border";
                    }
                    return c;
                };
                var parts0 = partsNow();
                clash = clashOf(parts0);
                var u = parts0.Aggregate((x, y) => new[] { Math.Min(x[0], y[0]), Math.Min(x[1], y[1]), Math.Max(x[2], y[2]), Math.Max(x[3], y[3]) });
                // hits a view: move the views right to make room, if the sheet has it, before any shrinking
                if (clash == main.GetName2() || (side != null && clash == side.GetName2()))
                {
                    var me = Grow(OutlineOf(main), 0.005); var se = side != null ? Grow(OutlineOf(side), 0.005) : me;
                    double viewsLeft = Math.Min(me[0], se[0]), viewsRight = Math.Max(me[2], se[2]);
                    // only what actually hits the views needs to clear them (the note block sits under them)
                    double dx = partsNow().Where(pb => hits(pb, me) || hits(pb, se)).Select(pb => pb[2] + 0.012 - viewsLeft).DefaultIfEmpty(0).Max();
                    if (dx > 0 && viewsRight + dx <= BorderX1 - 0.003)
                    {
                        var mo1 = (double[])main.GetOutline(); var so1 = side != null ? (double[])side.GetOutline() : null;
                        MoveOutline(drwDoc, main, mo1[0] + dx, double.NaN);
                        if (side != null) MoveOutline(drwDoc, side, so1[0] + dx, double.NaN);
                        drwDoc.ForceRebuild3(false);
                        Console.WriteLine("Views moved right " + Mm(dx) + " mm to make room for the isometric");
                        clash = clashOf(partsNow());
                    }
                }
                // still hits a view: move the views UP into the free paper above them before any shrinking (Grant 16 Nov:
                // "a layout tweak is all that is needed as there is plenty of room on the page"; his 08C04-SFP-04)
                if (IsoClearOfDims && (clash == main.GetName2() || (side != null && clash == side.GetName2())))
                {
                    var mb = vbox(main); var sb = side != null ? vbox(side) : mb;
                    var ipts = EdgeSamples(iso, 0.004); var abox = partsNow().Skip(1).ToList();
                    Func<double[], double> needFor = vb =>
                    {
                        double nd = 0;
                        foreach (var p in ipts) if (p[0] > vb[0] - 0.003 && p[0] < vb[2] + 0.003) nd = Math.Max(nd, p[1] + 0.004 - vb[1]);
                        foreach (var b in abox) if (b[0] < vb[2] && b[2] > vb[0]) nd = Math.Max(nd, b[3] + 0.004 - vb[1]);
                        return nd;
                    };
                    double need = Math.Max(needFor(mb), needFor(sb));
                    double limit = BorderY1 - 0.004;
                    if (LayoutBlocked != null && Math.Min(mb[0], sb[0]) < LayoutBlocked[2] + 0.004) limit = LayoutBlocked[1] - 0.006;   // under the cut-list table
                    double room = limit - Math.Max(mb[3], sb[3]);
                    if (need > 0 && room > 0.002)
                    {
                        double dy = Math.Min(need, room);
                        var mo2 = (double[])main.GetOutline();
                        MoveOutline(drwDoc, main, mo2[0], (mo2[1] + mo2[3]) / 2 + dy);   // the projected side view follows
                        drwDoc.ForceRebuild3(false);
                        Console.WriteLine("Views moved up " + Mm(dy) + " mm to make room for the isometric");
                        clash = clashOf(partsNow());
                    }
                }
                Console.WriteLine("Isometric at 1:" + isoScale + ", block " + Box(u) + (clash == null ? ", clear" : ", hits " + clash));
                if (clash == null) break;
                int next = PlateScales.Where(d => d > isoScale).DefaultIfEmpty(0).First();
                if (next == 0 || step == 2) break;
                isoScale = next;
            }
            if (clash != null) problems.Add("layout: the isometric or its note block hits " + clash);
        }
    }

    // A tube's long axis: square to both of its wall directions (the two directions with four parallel flat faces).
    static double[] TubeAxis(Body2 b)
    {
        var dirs = new List<Tuple<double[], HashSet<long>>>();
        foreach (Face2 f in (object[])b.GetFaces())
        {
            var s = (Surface)f.GetSurface();
            if (!s.IsPlane()) continue;
            var pp = (double[])s.PlaneParams;
            var n = new[] { pp[0], pp[1], pp[2] };
            double off = n[0] * pp[3] + n[1] * pp[4] + n[2] * pp[5];
            var d = dirs.FirstOrDefault(x => Math.Abs(x.Item1[0] * n[0] + x.Item1[1] * n[1] + x.Item1[2] * n[2]) > 1 - 1e-6);
            if (d == null) dirs.Add(d = Tuple.Create(n, new HashSet<long>()));
            double sgn = d.Item1[0] * n[0] + d.Item1[1] * n[1] + d.Item1[2] * n[2] > 0 ? 1 : -1;
            d.Item2.Add((long)Math.Round(sgn * off * 1e6));
        }
        var four = dirs.Where(d => d.Item2.Count == 4).ToList();
        if (four.Count != 2) return null;
        var a = four[0].Item1; var c = four[1].Item1;
        var x3 = new[] { a[1] * c[2] - a[2] * c[1], a[2] * c[0] - a[0] * c[2], a[0] * c[1] - a[1] * c[0] };
        double len = Math.Sqrt(x3.Sum(v => v * v));
        return len < 1e-6 ? null : x3.Select(v => v / len).ToArray();
    }

    // A welded tube's section from its own flat faces: the two directions with four parallel faces each (outside and
    // inside walls) give the outside sizes and the wall. "RHS 80x40x8" / "SHS 120x120x6.3", or null when not a tube.
    static string TubeSection(Body2 b)
    {
        var planes = new List<double[]>();   // unit normal (sign made positive), offset
        foreach (Face2 f in (object[])b.GetFaces())
        {
            var s = (Surface)f.GetSurface();
            if (!s.IsPlane()) continue;
            var pp = (double[])s.PlaneParams;
            var n = new[] { pp[0], pp[1], pp[2] };
            int big = Enumerable.Range(0, 3).OrderByDescending(i => Math.Abs(n[i])).First();
            if (n[big] < 0) n = n.Select(x => -x).ToArray();
            planes.Add(new[] { n[0], n[1], n[2], n[0] * pp[3] + n[1] * pp[4] + n[2] * pp[5] });
        }
        var dirs = new List<Tuple<double[], List<double>>>();
        foreach (var p in planes)
        {
            var d = dirs.FirstOrDefault(x => Math.Abs(x.Item1[0] * p[0] + x.Item1[1] * p[1] + x.Item1[2] * p[2]) > 1 - 1e-6);
            if (d == null) dirs.Add(d = Tuple.Create(new[] { p[0], p[1], p[2] }, new List<double>()));
            if (!d.Item2.Any(o => Math.Abs(o - p[3]) < 1e-6)) d.Item2.Add(p[3]);
        }
        var four = dirs.Where(d => d.Item2.Count == 4).Select(d => d.Item2.OrderBy(x => x).ToList()).ToList();
        if (four.Count != 2) return null;
        double a = (four[0][3] - four[0][0]) * 1000, c = (four[1][3] - four[1][0]) * 1000;
        double t0 = (four[0][1] - four[0][0]) * 1000, t1 = (four[1][1] - four[1][0]) * 1000;
        if (Math.Abs(t0 - t1) > 0.05) return null;
        Func<double, string> f1 = x => Math.Round(x, 1).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture);
        return (Math.Abs(a - c) < 0.05 ? "SHS " : "RHS ") + f1(Math.Max(a, c)) + "x" + f1(Math.Min(a, c)) + "x" + f1(t0);
    }

    // The sheet's cut-list anchor at the top-left border corner (where James's BOM anchor is), and every cut-list table
    // attached to it by its top-left corner (Grant 16 Nov, 08X04-BKP-02: "can't lock the BOM into the top LH corner").
    static int AnchorCutLists(DrawingDoc drw)
    {
        var doc = (ModelDoc2)drw;
        var sheet = (Sheet)drw.GetCurrentSheet();
        int wt = (int)swTableAnnotationType_e.swTableAnnotation_WeldmentCutList, bt = (int)swTableAnnotationType_e.swTableAnnotation_BillOfMaterials;
        var bomA = sheet.TableAnchor[bt] as TableAnchor;
        double[] want = bomA != null ? (double[])bomA.Position : null;
        Console.WriteLine("  BOM anchor: " + (want == null ? "none" : Mm(want[0]) + "," + Mm(want[1])));
        want = new[] { BorderX0, BorderY1 };   // the border corner, where James's tables sit (the template's anchors are at 20,287: off the corner)
        var a = sheet.TableAnchor[wt] as TableAnchor;
        Console.WriteLine("  cut-list anchor before: " + (a == null ? "none" : Mm(((double[])a.Position)[0]) + "," + Mm(((double[])a.Position)[1])));
        if (a == null) { sheet.SetAsTableAnchor(wt); a = sheet.TableAnchor[wt] as TableAnchor; }
        if (a == null) { Console.WriteLine("  cut-list anchor could not be made"); return 0; }
        a.Position = new[] { want[0], want[1] };
        var ap = (double[])a.Position;
        Console.WriteLine("  cut-list anchor now: " + Mm(ap[0]) + "," + Mm(ap[1]));
        int n = 0;
        foreach (Annotation an in (((View)drw.GetFirstView()).GetAnnotations() as object[]) ?? new object[0])
        {
            if (an.GetType() != (int)swAnnotationType_e.swTableAnnotation) continue;
            var ta = (TableAnnotation)an.GetSpecificAnnotation();
            if (ta.Type != wt) continue;
            ta.AnchorType = (int)swBOMConfigurationAnchorType_e.swBOMConfigurationAnchor_TopLeft;
            ta.Anchored = true;
            var p = (double[])an.GetPosition();
            Console.WriteLine("  cut-list table attached: " + ta.Anchored + ", top-left at " + Mm(p[0]) + "," + Mm(p[1]));
            if (ta.Anchored) n++;
        }
        doc.ForceRebuild3(false);
        return n;
    }

    static int AnchorCutListCmd(string file)
    {
        var doc = sw.ActiveDoc as ModelDoc2;
        if (doc == null || !string.Equals(doc.GetPathName(), file, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: not the active document: " + file); return 2; }
        int n = AnchorCutLists((DrawingDoc)doc);
        Console.WriteLine(n + " cut-list table(s) attached to the top-left anchor. Not saved.");
        return n > 0 ? 0 : 1;
    }

    // add-cutlist <drw>: James's cut-list table and balloons on the open drawing (first view = the face view, the
    // isometric found by its orientation); no save.
    static int AddCutListCmd(string file, string template)
    {
        var doc = sw.ActiveDoc as ModelDoc2;
        if (doc == null || !string.Equals(doc.GetPathName(), file, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: not the active document: " + file); return 2; }
        var drw = (DrawingDoc)doc;
        var views = new List<View>();
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView()) views.Add(v);
        var main = views.First();
        var iso = views.FirstOrDefault(v => (v.GetOrientationName() ?? "").IndexOf("Isometric", StringComparison.OrdinalIgnoreCase) >= 0);
        var lm = (LayerMgr)doc.GetLayerManager(); lm.SetCurrentLayer(DraftLayer);
        int r = AddCutList(drw, main, iso, main.ReferencedConfiguration, template);
        lm.SetCurrentLayer("");
        Console.WriteLine(r < 0 ? "cut-list table not inserted" : "done; " + r + " item(s) without a balloon. Not saved.");
        return r < 0 ? 1 : 0;
    }

    // Free sheet area for views: inside the border, above the title block (sheet metres; Unilumin A3 format).
    const double AreaX0 = 0.020, AreaX1 = 0.400, AreaY0 = 0.070, AreaY1 = 0.280, RowGap = 0.015, ColGap = 0.020;

    static double W(double[] b) { return b[2] - b[0]; }
    static double[] Grow(double[] b, double m) { return new[] { b[0] - m, b[1] - m, b[2] + m, b[3] + m }; }
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
