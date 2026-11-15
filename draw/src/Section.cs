// Draw: first draft of a single straight member (angle, RHS, flat bar) with holes and slots in its flat faces.
// Grant 14 Nov: "try a more complex part" -> 08X04-BKP-01 (100x100x8 angle with two M12 slots).
// James's style (GNRIC-BRP-10): a view straight onto each face that has holes or slots, an end view with the section
// sizes, the length as a reference dimension in brackets, an isometric with the note block.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    class Cut { public double[] C; public double Width, Length; public double[] Axis; public bool Slot; public double[] Dir; }   // a hole or a slot (model metres)

    static string PdfName;
    static int endDen;
    static int MakeSection(string partFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        // --pdf=<name>: a later version's PDF under its own name beside the drawing (Desk tickets, 15 Nov), e.g. "GNRIC-LMW-01 v2.pdf"
        string pdfOut = PdfName != null ? Path.Combine(Path.GetDirectoryName(Path.GetFullPath(output)), PdfName) : Path.ChangeExtension(output, ".pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + pdfOut); return 2; }
        var t0 = DateTime.Now;

        // 1. Model: properties, body, length axis, faces with holes or slots.
        var part = OpenDoc(partFile, config, true);
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) part.ShowConfiguration2(config);
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) { Console.Error.WriteLine("no configuration " + config); return 2; }
        var props = new Dictionary<string, string>();
        foreach (var name in new[] { "PartNo", "Description", "Material", "Weight", "Finish" })
        { props[name] = Prop(part, config, name); Console.WriteLine("  " + name + " = " + (props[name] == "" ? "(missing)" : props[name])); }
        if (props["PartNo"] == "") { Console.Error.WriteLine("REFUSED: no PartNo; no draft"); return 2; }
        var bodies = (object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true);
        if (bodies == null || bodies.Length == 0) { Console.Error.WriteLine("REFUSED: make-section needs a solid body"); return 2; }
        // A welded member (GNRIC-LMW, batch 1): the member is the longest body, the rest are end pieces (end caps)
        // shown in the views and counted in the overall length and the weight; holes are read from the member
        Func<Body2, double> spanOf = b => { var bb = (double[])b.GetBodyBox(); return Math.Max(bb[3] - bb[0], Math.Max(bb[4] - bb[1], bb[5] - bb[2])); };
        var body = bodies.Cast<Body2>().OrderByDescending(spanOf).First();
        var others = bodies.Cast<Body2>().Where(b => b != body).ToList();
        if (others.Any(b => spanOf(b) > 0.5 * spanOf(body))) { Console.Error.WriteLine("REFUSED: make-section needs one member (plus small end pieces); this part has " + bodies.Length + " bodies"); return 2; }
        if (others.Count > 0) Console.WriteLine("Welded member: " + body.Name + " plus " + others.Count + " end piece(s): " + string.Join(", ", others.Select(b => b.Name)));
        var box = (double[])body.GetBodyBox();
        var partBox = (double[])box.Clone();
        foreach (var b in others) { var bb = (double[])b.GetBodyBox(); for (int i = 0; i < 3; i++) { partBox[i] = Math.Min(partBox[i], bb[i]); partBox[3 + i] = Math.Max(partBox[3 + i], bb[3 + i]); } }
        var faces = ((object[])body.GetFaces()).Cast<Face2>().ToList();

        // Length axis: the axis with two flat end faces of equal area facing opposite ways.
        int lenAx = -1; double endArea = double.MaxValue; bool hollow = false;
        for (int ax = 0; ax < 3; ax++)
        {
            int a0 = ax;
            var ends = faces.Where(f => ((Surface)f.GetSurface()).IsPlane() && Math.Abs(Math.Abs(((double[])f.Normal)[a0]) - 1) < 1e-6)
                .Where(f => { var fb = (double[])f.GetBox(); return Math.Abs(fb[a0] - box[a0]) < 1e-7 || Math.Abs(fb[a0] - box[3 + a0]) < 1e-7; }).ToList();   // at the very ends
            if (ends.Count == 2 && Math.Abs(ends[0].GetArea() - ends[1].GetArea()) < 1e-9 && ((double[])ends[0].Normal)[ax] * ((double[])ends[1].Normal)[ax] < 0
                && ends[0].GetArea() < endArea) { lenAx = ax; endArea = ends[0].GetArea(); hollow = ends[0].GetLoopCount() >= 2; }
        }
        if (lenAx < 0) { Console.Error.WriteLine("REFUSED: can't find the member's two end faces (not a straight member)"); return 2; }
        double length = box[3 + lenAx] - box[lenAx];
        // LED members (GNRIC-LMW, batch 1) stand upright as James draws them (GNRIC-LWP-20) and as Grant signed off for
        // 08X06-LMW (15 Nov): the end view of the capped end to the right, the length measured from the top
        bool upright = Regex.IsMatch(props["PartNo"], "-LMW-", RegexOptions.IgnoreCase);
        bool capAtMax = others.Count == 0 || others.Average(b => { var bb = (double[])b.GetBodyBox(); return (bb[lenAx] + bb[3 + lenAx]) / 2; }) > (box[lenAx] + box[3 + lenAx]) / 2;
        Console.WriteLine("Member length " + Mm(length) + " along " + "XYZ"[lenAx]);

        // Holes (full cylinders) and slots (two half cylinders), square to a flat face.
        var cyl = faces.Where(f => ((Surface)f.GetSurface()).IsCylinder()).Select(f => new { F = f, P = (double[])((Surface)f.GetSurface()).CylinderParams }).ToList();
        var cuts = new List<Cut>();
        var halves = new List<Tuple<double[], double[], double>>();   // centre, axis, radius
        foreach (var c in cyl)
        {
            var axis = new[] { c.P[3], c.P[4], c.P[5] };
            if (Math.Abs(axis[lenAx]) > 1e-6) continue;                                        // profile fillets run along the member
            int ax = Enumerable.Range(0, 3).First(i => Math.Abs(Math.Abs(axis[i]) - 1) < 1e-6);
            var fb = (double[])c.F.GetBox();
            var centre = new[] { c.P[0], c.P[1], c.P[2] };
            centre[ax] = (fb[ax] + fb[3 + ax]) / 2;                                            // mid-depth
            if (c.F.GetLoopCount() == 2) cuts.Add(new Cut { C = centre, Width = 2 * c.P[6], Length = 2 * c.P[6], Axis = axis, Slot = false });
            else halves.Add(Tuple.Create(centre, axis, c.P[6]));
        }
        var used = new HashSet<int>();
        for (int i = 0; i < halves.Count; i++)
        {
            if (used.Contains(i)) continue;
            int best = -1; double bd = double.MaxValue;
            for (int j = 0; j < halves.Count; j++)
            {
                if (j == i || used.Contains(j) || Math.Abs(halves[i].Item3 - halves[j].Item3) > 1e-7) continue;
                double d = Dist(halves[i].Item1, halves[j].Item1);
                if (d < bd) { bd = d; best = j; }
            }
            if (best < 0) { problems.Add("half-round face without a partner (not a slot?)"); continue; }
            used.Add(i); used.Add(best);
            var a = halves[i].Item1; var b = halves[best].Item1;
            cuts.Add(new Cut { C = new[] { (a[0] + b[0]) / 2, (a[1] + b[1]) / 2, (a[2] + b[2]) / 2 }, Width = 2 * halves[i].Item3, Length = bd + 2 * halves[i].Item3, Axis = halves[i].Item2, Slot = true,
                Dir = new[] { (b[0] - a[0]) / bd, (b[1] - a[1]) / bd, (b[2] - a[2]) / bd } });
        }
        foreach (var c in cuts) Console.WriteLine("  " + (c.Slot ? "slot " + Mm(c.Width) + " x " + Mm(c.Length) : "hole Ø" + Mm(c.Width)) + " at " + Mm(c.C[0]) + "," + Mm(c.C[1]) + "," + Mm(c.C[2]));
        if (cuts.Count == 0) { if (others.Count == 0) problems.Add("no holes or slots found"); else Console.WriteLine("No holes or slots in the member"); }

        // The face to look at: the biggest flat face square to the holes' axis (the outside of that leg).
        var cutAxis = cuts.Count > 0 ? cuts[0].Axis : new double[] { 0, 0, 1 };
        int cAx = Enumerable.Range(0, 3).First(i => Math.Abs(Math.Abs(cutAxis[i]) - 1) < 1e-6);
        if (cuts.Any(c => Math.Abs(Math.Abs(c.Axis[cAx]) - 1) > 1e-6)) problems.Add("holes or slots in more than one direction: only the " + "XYZ"[cAx] + " ones are drawn");
        // the outside face on the holes' side (GNRIC-LMW-06: a tube has one on each side; the far one hides the holes)
        double holeAt = cuts.Count > 0 ? cuts.Average(c => c.C[cAx]) : double.NaN;
        Func<Face2, double> faceAt = f => { var fb = (double[])f.GetBox(); return (fb[cAx] + fb[3 + cAx]) / 2; };
        var lookFace = faces.Where(f => ((Surface)f.GetSurface()).IsPlane() && Math.Abs(Math.Abs(((double[])f.Normal)[cAx]) - 1) < 1e-6)
            .OrderBy(f => double.IsNaN(holeAt) ? 0 : Math.Round(Math.Abs(faceAt(f) - holeAt) * 1e4)).ThenByDescending(f => f.GetArea()).First();
        var nrm = (double[])lookFace.Normal;
        string faceView = cAx == 0 ? (nrm[0] > 0 ? "*Right" : "*Left") : cAx == 1 ? (nrm[1] > 0 ? "*Top" : "*Bottom") : (nrm[2] > 0 ? "*Front" : "*Back");
        var mp = (MassProperty)part.Extension.CreateMassProperty();
        var com = (double[])mp.CenterOfMass;
        Console.WriteLine("Face view " + faceView + " (face normal " + "XYZ"[cAx] + (nrm[cAx] > 0 ? "+" : "-") + "); centre of mass " + Mm(com[0]) + "," + Mm(com[1]) + "," + Mm(com[2]));

        // 2. Scale: 1:5 by default (Grant 14 Nov); bigger when the member would be under MinSide on the sheet.
        double span = Math.Max(partBox[3] - partBox[0], Math.Max(partBox[4] - partBox[1], partBox[5] - partBox[2]));
        Func<int, bool> fits = d => span / d <= FitH;
        int den = 5;
        if (!fits(den)) den = PlateScales.Where(d => d > 5).FirstOrDefault(fits);
        else while (span / den < MinSide && den > 1 && fits(den == 5 ? 2 : 1)) den = den == 5 ? 2 : 1;
        if (den == 0) den = PlateScales.Last();

        var drwDoc = (ModelDoc2)sw.NewDocument(template, (int)swDwgPaperSizes_e.swDwgPaperA3size, 0.42, 0.297);
        if (drwDoc == null) { Console.Error.WriteLine("could not start a drawing from " + template); return 3; }
        var drw = (DrawingDoc)drwDoc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var mu = (MathUtility)sw.GetMathUtility();
        var selMgr = (SelectionMgr)drwDoc.SelectionManager;

        // 3. Views: face view, end view projected along the member, isometric.
        var main = drw.CreateDrawViewFromModelView3(partFile, faceView, 0.150, 0.200, 0);
        if (main == null) { Console.Error.WriteLine("face view not created"); return 3; }
        main.ReferencedConfiguration = config;
        main.UseSheetScale = 0; main.ScaleRatio = new double[] { 1, den };
        drwDoc.ForceRebuild3(false);
        var p0 = new double[3]; var p1 = new double[3]; p1[lenAx] = 1;
        var s0 = ToSheet(mu, main, p0); var s1 = ToSheet(mu, main, p1);
        if (upright && Math.Abs(s1[0] - s0[0]) > Math.Abs(s1[1] - s0[1]))
        {
            main.Angle = Math.PI / 2;   // lying down: stand it up
            drwDoc.ForceRebuild3(false);
            s0 = ToSheet(mu, main, p0); s1 = ToSheet(mu, main, p1);
            Console.WriteLine("Face view turned 90 degrees so the member stands up");
        }
        if (!upright && Math.Abs(s1[0] - s0[0]) < Math.Abs(s1[1] - s0[1]))
        {
            // the member runs up the sheet: turn the view a quarter turn so it runs across, as James draws members
            main.Angle = Math.PI / 2;
            drwDoc.ForceRebuild3(false);
            s0 = ToSheet(mu, main, p0); s1 = ToSheet(mu, main, p1);
            Console.WriteLine("Face view turned 90 degrees so the member runs across the sheet");
        }
        bool lenHoriz = Math.Abs(s1[0] - s0[0]) > Math.Abs(s1[1] - s0[1]);
        if (!lenHoriz && !upright) problems.Add("member still runs up the sheet after turning the view");
        if (lenHoriz && upright) problems.Add("member still lies across the sheet after turning the view");
        var mo = (double[])main.GetOutline();
        // End view: projected to the right of the (turned) face view, so it is a true projection and stays level with
        // it (Grant 14 Nov: the two views linked, as James's).
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        View side, capView = null;
        if (upright)
        {
            // looking along the member (not projected: a projected view beside an upright member is a side view). With an
            // end cap (James's GNRIC-LWP-20): the open end shows the section, the capped end the cap and its hole.
            Func<bool, string> endName = atMax => lenAx == 1 ? (atMax ? "*Top" : "*Bottom") : lenAx == 0 ? (atMax ? "*Right" : "*Left") : (atMax ? "*Front" : "*Back");
            if (main.Angle != 0) problems.Add("upright member: face view was turned; the end views may not match");
            side = drw.CreateDrawViewFromModelView3(partFile, endName(others.Count > 0 ? !capAtMax : true), mo[2] + 0.080, mo[1] + 0.020, 0);
            if (side != null) side.ReferencedConfiguration = config;
            // the section view shows the tube alone: with caps at both ends (GNRIC-LMW-04) the wall can't be seen otherwise
            if (side != null && others.Count > 0)
            {
                var before = side.GetBodiesCount();
                side.Bodies = new Body2[] { body };
                drwDoc.ForceRebuild3(false);
                var now = (object[])side.Bodies;
                Console.WriteLine("Section view: member body only (" + before + " -> " + side.GetBodiesCount() + " bodies; Bodies returns " + (now == null ? "null" : now.Length.ToString()) + ")");
            }
            if (others.Count > 0)
            {
                capView = drw.CreateDrawViewFromModelView3(partFile, endName(capAtMax), mo[2] + 0.080, mo[3] - 0.020, 0);
                if (capView != null) capView.ReferencedConfiguration = config; else problems.Add("end cap view not created");
            }
        }
        else side = drw.CreateUnfoldedViewAt3(mo[2] + 0.080, (mo[1] + mo[3]) / 2, 0, false);
        drwDoc.ClearSelection2(true);
        if (side == null) problems.Add("end view not created");
        else Console.WriteLine("End view projected from the face view: '" + side.GetOrientationName() + "', aligned " + side.GetAlignment());
        drwDoc.ClearSelection2(true);
        var iso = drw.CreateDrawViewFromModelView3(partFile, "*Isometric", 0.100, 0.100, 0);
        if (iso != null) iso.ReferencedConfiguration = config;
        // Scale (Grant 14 Nov): the isometric at the same scale as the other views. Start from the 1:5 rule and step
        // to a smaller scale until face view + end view (with room for dimensions) and the isometric below them fit.
        foreach (int d in PlateScales.Where(x => x >= den))
        {
            foreach (var vw in new[] { main, iso, upright ? side : null, capView }) if (vw != null) { vw.UseSheetScale = 0; vw.ScaleRatio = new double[] { 1, d }; }   // a projected end view follows the face view
            drwDoc.ForceRebuild3(false);
            var o1 = (double[])main.GetOutline(); var o2 = side != null ? (double[])side.GetOutline() : o1; var o3 = iso != null ? (double[])iso.GetOutline() : new double[4];
            double topW = W(o1) + W(o2) + 0.100, topH = Math.Max(H(o1), H(o2)) + 0.030, isoW = W(o3) + 0.070, isoH = H(o3);
            bool fitsAll = upright
                ? isoW + topW <= AreaX1 - AreaX0 && Math.Max(topH, isoH) <= AreaY1 - AreaY0   // upright: the isometric beside the views
                : topW <= AreaX1 - AreaX0 && topH + RowGap + isoH <= AreaY1 - AreaY0 && isoW <= AreaX1 - AreaX0;
            den = d;
            if (fitsAll) break;
            Console.WriteLine("  1:" + d + " doesn't fit (top row " + Mm(topW) + " x " + Mm(topH) + ", isometric " + Mm(W(o3)) + " x " + Mm(isoH) + ")");
        }
        mo = (double[])main.GetOutline();
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        foreach (var vw in new[] { main, iso, upright ? side : null, capView }) if (vw != null) vw.UseSheetScale = 1;
        // upright members: the end views no smaller than 1:5 (at 1:20 a 40 mm section is 2 mm on the sheet, GNRIC-LMW-11)
        endDen = upright ? Math.Max(2, Math.Min(den, 5)) : den;   // and no bigger than 1:2 (GNRIC-LMW-12 at 1:1: the two end views overlapped)
        if (endDen != den)
            foreach (var vw in new[] { side, capView }) if (vw != null) { vw.UseSheetScale = 0; vw.ScaleRatio = new double[] { 1, endDen }; }
        drwDoc.ForceRebuild3(false);
        sp = (double[])sheet.GetProperties2();
        Console.WriteLine("Scale 1:" + den + (sp[3] == den && Math.Abs(main.ScaleDecimal - 1.0 / den) < 1e-9 ? "" : " NOT SET"));
        if (sp[3] != den) problems.Add("sheet scale not set");

        var lm = (LayerMgr)drwDoc.GetLayerManager();
        if (lm.GetLayer(DraftLayer) == null) lm.AddLayer(DraftLayer, "Placed by the machine, not yet checked", Magenta, (int)swLineStyles_e.swLineCONTINUOUS, (int)swLineWeights_e.swLW_NORMAL);
        lm.SetCurrentLayer(DraftLayer);

        // 4. Face view: slot centre marks first (dimensions attach to them), then ordinates along the member from
        //    one end and across the face from the heel (the edge nearest the centre of mass), then hole centre marks.
        drw.ActivateView(main.GetName2());
        bool anySlot = cuts.Any(c => c.Slot);
        if (anySlot && !main.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Slots, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0))
            problems.Add("slot centre marks not inserted");
        drwDoc.ClearSelection2(true);
        var marks = ((main.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
            .Where(a => a.GetType() == (int)swAnnotationType_e.swCenterMarkSym).Select(a => (CenterMark)a.GetSpecificAnnotation()).ToList();

        var lines = ViewLines(mu, main);
        var circles = ViewCircles(mu, main);
        var comS = ToSheet(mu, main, com);
        // in sheet terms: "along" = the member's direction on the sheet, "across" = the other one
        Func<double[], double> along = q => lenHoriz ? q[0] : q[1];
        Func<double[], double> across = q => lenHoriz ? q[1] : q[0];
        var alongLines = lines.Where(l => lenHoriz ? Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7 : Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).ToList();   // square to the member
        var acrossLines = lines.Where(l => lenHoriz ? Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7 : Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).ToList();  // along the member
        var expected = new List<double>(); var refDims = new List<double>();
        if (alongLines.Count < 2 || acrossLines.Count < 2) problems.Add("face view outline not found; no position dimensions");
        else
        {
            var end0 = alongLines.OrderBy(l => along(l.Item2)).First(); var end1 = alongLines.OrderBy(l => along(l.Item2)).Last();
            if (upright) { var t = end0; end0 = end1; end1 = t; }   // from the top down (08X06-LMW, Grant 15 Nov)
            // heel and toe: the member's long edges only (an end cap's short edges sit inside them, GNRIC-LMW)
            double longest = acrossLines.Max(l => Dist2(l.Item2, l.Item3));
            var sideLines = acrossLines.Where(l => Dist2(l.Item2, l.Item3) >= 0.5 * longest).ToList();
            // the two outermost (a tube's corner-radius lines lie inside them); the heel is the one nearer the centre of mass
            var outer = new[] { sideLines.OrderBy(l => across(l.Item2)).First(), sideLines.OrderBy(l => across(l.Item2)).Last() };
            var heel = outer.OrderBy(l => Math.Abs(across(l.Item2) - across(comS))).First();
            var toe = outer.First() == heel ? outer.Last() : outer.First();
            double vs = main.ScaleDecimal;
            // One target per cut and direction. Holes: the circle edge. Slots: in the direction square to the slot, its end
            // arc (same position as the slot centre); along the slot, its centre mark, picked by position (the API won't
            // put an ordinate on a centre mark selected any other way, and only one group of ordinates per drawing view
            // can pick centre marks, 14 Nov).
            var tA = new List<Tuple<object, double[]>>(); var tX = new List<Tuple<object, double[]>>();
            bool alongNeedsPick = false, acrossNeedsPick = false;
            foreach (var c in cuts)
            {
                var cs = ToSheet(mu, main, c.C);
                if (!c.Slot)
                {
                    var e = circles.OrderBy(ck => Dist2(ck.Item2, cs)).FirstOrDefault();
                    if (e == null || Dist2(e.Item2, cs) > 1e-4) { problems.Add("no circle at a hole centre"); continue; }
                    tA.Add(Tuple.Create((object)e.Item1, cs)); tX.Add(Tuple.Create((object)e.Item1, cs));
                    continue;
                }
                var ds = ToSheet(mu, main, new[] { c.C[0] + c.Dir[0], c.C[1] + c.Dir[1], c.C[2] + c.Dir[2] });
                bool slotAcross = Math.Abs(across(ds) - across(cs)) > Math.Abs(along(ds) - along(cs));
                var arc = circles.Where(ck => Math.Abs(ck.Item3 - c.Width) < 1e-6 && Dist2(ck.Item2, cs) < c.Length * main.ScaleDecimal)
                    .OrderBy(ck => Dist2(ck.Item2, cs)).FirstOrDefault();
                if (arc == null) { problems.Add("slot end arc not found"); continue; }
                // Grant 15 Nov: a slot is located by its centre only, both ways, never its end arc or sides
                tA.Add(Tuple.Create((object)cs, cs)); tX.Add(Tuple.Create((object)cs, cs)); alongNeedsPick = true; acrossNeedsPick = true;
            }
            // (both rows now pick slot centre marks; the value check and the slot check below catch a row SOLIDWORKS refuses)
            var cutSheet = cuts.Select(c => ToSheet(mu, main, c.C)).ToList();
            var sel = new List<object>();
            Action doAcross = () => {
            // across the face: from the heel, one per distinct position, then the toe
            var tAcross = tX.GroupBy(t => Math.Round(across(t.Item2) * 1e5)).Select(g => g.OrderBy(t => -Math.Abs(along(t.Item2) - along(end0.Item2))).First()).OrderBy(t => Math.Abs(across(t.Item2) - across(heel.Item2))).ToList();
            sel = new List<object> { heel.Item1 }; sel.AddRange(tAcross.Select(t => t.Item1)); sel.Add(toe.Item1);
            expected.Add(0);
            expected.AddRange(cutSheet.GroupBy(q => Math.Round(across(q) * 1e5)).Select(g => Math.Abs(across(g.First()) - across(heel.Item2)) / vs * 1000));
            expected.Add(Math.Abs(across(toe.Item2) - across(heel.Item2)) / vs * 1000);
            double endLine = along(end1.Item2) + (along(end1.Item2) >= along(end0.Item2) ? 0.012 : -0.012);
            Ordinate2(drwDoc, selMgr, main, sel, lenHoriz ? (int)swAddOrdinateDims_e.swVerticalOrdinate : (int)swAddOrdinateDims_e.swHorizontalOrdinate,
                lenHoriz ? endLine : across(heel.Item2), lenHoriz ? across(heel.Item2) : endLine, "across the face");
            foreach (var q in lastMissed)
            {
                double mv = Math.Abs(across(q) - across(heel.Item2)) / vs * 1000;
                int at = expected.FindLastIndex(e => Math.Abs(e - mv) < 0.01); if (at >= 0) expected.RemoveAt(at);
                slotFlags.Add("CHECK: SLOT CENTRE ACROSS THE FACE NOT DIMENSIONED (" + Math.Round(mv, 1) + ")");
            }
            };
            Action doAlong = () => {
            // along the member: ordinate from end0, one per distinct position, then end1 in brackets (reference)
            var tAlong = tA.GroupBy(t => Math.Round(along(t.Item2) * 1e5)).Select(g => g.OrderBy(t => Math.Abs(across(t.Item2) - across(toe.Item2))).First()).OrderBy(t => along(t.Item2)).ToList();
            sel = new List<object> { end0.Item1 }; sel.AddRange(tAlong.Select(t => t.Item1)); sel.Add(end1.Item1);
            expected.Add(0);
            expected.AddRange(cutSheet.GroupBy(q => Math.Round(along(q) * 1e5)).Select(g => Math.Abs(along(g.First()) - along(end0.Item2)) / vs * 1000));
            expected.Add(Math.Abs(along(end1.Item2) - along(end0.Item2)) / vs * 1000);
            refDims.Add(Math.Abs(along(end1.Item2) - along(end0.Item2)) / vs * 1000);
            double toeLine = across(toe.Item2) + (across(toe.Item2) > across(heel.Item2) ? 0.012 : -0.012);
            Ordinate2(drwDoc, selMgr, main, sel, lenHoriz ? (int)swAddOrdinateDims_e.swHorizontalOrdinate : (int)swAddOrdinateDims_e.swVerticalOrdinate,
                lenHoriz ? along(end0.Item2) : toeLine, lenHoriz ? toeLine : along(end0.Item2), "along the member");
            foreach (var q in lastMissed)
            {
                double mv = Math.Abs(along(q) - along(end0.Item2)) / vs * 1000;
                int at = expected.FindLastIndex(e => Math.Abs(e - mv) < 0.01); if (at >= 0) expected.RemoveAt(at);
                slotFlags.Add("CHECK: SLOT CENTRE ALONG THE MEMBER NOT DIMENSIONED (" + Math.Round(mv, 1) + ")");
            }
            };
            if (alongNeedsPick) { doAlong(); doAcross(); } else { doAcross(); doAlong(); }
            // the member's length is a reference dimension: brackets on the last "along" ordinate
            foreach (Annotation a in (main.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                var dd = (DisplayDimension)a.GetSpecificAnnotation();
                double v = ((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000;
                bool isAlong = lenHoriz ? dd.GetType() == 7 || IsHorizOrd(dd, mu) : !IsHorizOrd(dd, mu);
                if (Math.Abs(v - refDims[0]) < 0.01 && isAlong) { dd.ShowParenthesis = true; Console.WriteLine("  length " + Mm(length) + " shown as reference (in brackets)"); }
            }
        }
        drwDoc.ClearSelection2(true);

        // 5. Callouts: one per size, "n x W X L THRU" for slots (James, 08XXX-PLP-30), "n x Ø d THRU" for holes.
        int k = 0;
        foreach (var g in cuts.GroupBy(c => (c.Slot ? "S" : "H") + Math.Round(c.Width * 1e5) + "x" + Math.Round(c.Length * 1e5)))
        {
            var c0 = g.OrderBy(c => along(ToSheet(mu, main, c.C))).First();
            var cs = ToSheet(mu, main, c0.C);
            // a tube: a hole through both walls is two cylinders at one spot on the sheet; count spots, and say how far it
            // goes as on the LED members (Grant 15 Nov): THRU ALL (both walls) or THRU ONE WALL
            var spots = g.GroupBy(c => { var q = ToSheet(mu, main, c.C); return Math.Round(q[0] * 1e5) + "," + Math.Round(q[1] * 1e5); }).ToList();
            int n = hollow ? spots.Count : g.Count();
            string thru = !hollow ? " THRU" : spots.Max(spt => spt.Count()) >= 2 ? " THRU ALL" : " THRU ONE WALL";
            string text = (n > 1 ? n + " x " : "") + (c0.Slot ? (c0.Width * 1000).ToString("0.0") + " X " + (c0.Length * 1000).ToString("0") : "<MOD-DIAM>" + (c0.Width * 1000).ToString("0.#")) + thru;
            // leader to the slot's end arc / the hole's circle, picked by position on the sheet
            // slot: the leader goes to the top end of the slot (Grant 14 Nov), i.e. its upper end arc
            var arc = c0.Slot
                ? circles.Where(e => Math.Abs(e.Item3 - c0.Width) < 1e-6 && Dist2(e.Item2, cs) < c0.Length * main.ScaleDecimal).OrderByDescending(e => e.Item2[1]).FirstOrDefault()
                : circles.Where(e => Math.Abs(e.Item3 - c0.Width) < 1e-6).OrderBy(e => Dist2(e.Item2, cs)).FirstOrDefault();
            drwDoc.ClearSelection2(true);
            if (arc != null)
            {
                var sd = (SelectData)selMgr.CreateSelectData(); sd.View = main; ((Entity)arc.Item1).Select4(false, sd);
            }
            var note = drwDoc.InsertNote(text) as Note;
            if (note == null) { problems.Add("callout not placed: " + text); continue; }
            var an = (Annotation)note.GetAnnotation();
            if (arc != null)
            {
                // arrow tip to the top of the slot / hole (Grant 14 Nov); still attached to the same arc
                double r = c0.Width / 2 * main.ScaleDecimal;
                bool moved = an.SetLeaderAttachmentPointAtIndex(0, arc.Item2[0], arc.Item2[1] + r, 0);
                Console.WriteLine("  callout arrow moved to the top: " + moved);
            }
            mo = (double[])main.GetOutline();
            if (upright) an.SetPosition2(mo[0] - 0.045, cs[1] + 0.0124 + 0.009 * k, 0);   // beside the hole, left (the ordinates are on the right)
            else an.SetPosition2(mo[0] - 0.035, mo[3] + 0.015 + 0.008 * k, 0);
            Console.WriteLine("Callout: " + text.Replace("<MOD-DIAM>", "Ø") + (arc == null ? " (no leader: arc not found)" : ""));
            if (arc == null) problems.Add("callout without a leader: " + text);
            calloutTexts.Add(text);
            k++;
        }
        drwDoc.ClearSelection2(true);

        // 6. End view: the section's overall sizes and the wall thickness.
        var secExpected = new List<double>();
        if (side != null)
        {
            var sl = ViewLines(mu, side);
            var vert = sl.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).ToList();
            var hor = sl.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).ToList();
            double svs = side.ScaleDecimal;
            if (vert.Count >= 2 && hor.Count >= 2)
            {
                var left = vert.OrderBy(l => l.Item2[0]).First(); var right = vert.OrderBy(l => l.Item2[0]).Last();
                var bot = hor.OrderBy(l => l.Item2[1]).First(); var top = hor.OrderBy(l => l.Item2[1]).Last();
                var so = (double[])side.GetOutline();
                Dim2(drwDoc, selMgr, side, left.Item1, right.Item1, (left.Item2[0] + right.Item2[0]) / 2, so[3] + 0.010, "section width", left.Item2[0], right.Item2[0], true);
                Dim2(drwDoc, selMgr, side, bot.Item1, top.Item1, so[2] + 0.012, (bot.Item2[1] + top.Item2[1]) / 2, "section height", top.Item2[1], bot.Item2[1], false);
                secExpected.Add((right.Item2[0] - left.Item2[0]) / svs * 1000); secExpected.Add((top.Item2[1] - bot.Item2[1]) / svs * 1000);
                // wall thickness: the line nearest to and parallel with the longest outside line
                var longest = sl.OrderByDescending(l => Dist(l.Item2, l.Item3)).First();
                bool lv = Math.Abs(longest.Item2[0] - longest.Item3[0]) < 1e-7;
                var inner = (lv ? vert : hor).Where(l => l.Item1 != longest.Item1 && Overlap(l, longest, lv) > 0.3 * Dist(longest.Item2, longest.Item3))
                    .OrderBy(l => lv ? Math.Abs(l.Item2[0] - longest.Item2[0]) : Math.Abs(l.Item2[1] - longest.Item2[1])).FirstOrDefault();
                if (inner == null) problems.Add("wall thickness: inner line not found");
                else
                {
                    double t = (lv ? Math.Abs(inner.Item2[0] - longest.Item2[0]) : Math.Abs(inner.Item2[1] - longest.Item2[1])) / svs * 1000;
                    double tx = lv ? (inner.Item2[0] + longest.Item2[0]) / 2 : so[0] - 0.012, ty = lv ? so[1] - 0.010 : (inner.Item2[1] + longest.Item2[1]) / 2;
                    Dim2(drwDoc, selMgr, side, longest.Item1, inner.Item1, tx, ty, "wall thickness",
                        lv ? longest.Item2[0] : longest.Item2[1], lv ? inner.Item2[0] : inner.Item2[1], lv);
                    secExpected.Add(t);
                }
                // a tube's inside size (Grant 15 Nov: so it can't be confused with the end cap's 36): between the inner walls
                var xs = vert.Select(l => Math.Round(l.Item2[0] * 1e6) / 1e6).Distinct().OrderBy(x => x).ToList();
                if (hollow && xs.Count == 4)
                {
                    var in0 = vert.First(l => Math.Abs(l.Item2[0] - xs[1]) < 1e-6); var in1 = vert.First(l => Math.Abs(l.Item2[0] - xs[2]) < 1e-6);
                    double inside = (xs[2] - xs[1]) / svs * 1000;
                    Dim2(drwDoc, selMgr, side, in0.Item1, in1.Item1, (xs[1] + xs[2]) / 2, (so[1] + so[3]) / 2, "inside width", xs[1], xs[2], true);
                    secExpected.Add(inside);
                    double outW = (xs[3] - xs[0]) / svs * 1000;
                    if (secExpected.Count >= 3 && Math.Abs(outW - 2 * secExpected[2] - inside) > 0.05) problems.Add("inside " + inside.ToString("0.#") + " is not outside less two walls");
                }
                else if (hollow) problems.Add("inside size: walls not found in the end view");
            }
            else problems.Add("end view outline not found; no section sizes");
            drwDoc.ClearSelection2(true);
        }

        // 6b. A hole through an end cap, square to the end (GNRIC-LMW): the HDG drainage hole, called out as James does
        //     (08A06-HBW-01): "Ø10" over "for HDG Drainage", a diameter dimension so the size comes from the model.
        var capExpected = new List<double>();
        if (capView != null)
        {
            // the cap's size: its outermost straight edges inside the tube's outline (checked against the cap's body)
            var cl = ViewLines(mu, capView); var co = (double[])capView.GetOutline(); double cvs = capView.ScaleDecimal;
            var cv = cl.Where(l => Math.Abs(l.Item2[0] - l.Item3[0]) < 1e-7).OrderBy(l => l.Item2[0]).ToList();
            var ch = cl.Where(l => Math.Abs(l.Item2[1] - l.Item3[1]) < 1e-7).OrderBy(l => l.Item2[1]).ToList();
            // leave out the tube's outline (the outermost lines each way): the cap sits inside it
            if (cv.Count > 0) { double a = cv.First().Item2[0], b = cv.Last().Item2[0]; cv = cv.Where(l => l.Item2[0] > a + 1e-6 && l.Item2[0] < b - 1e-6).ToList(); }
            if (ch.Count > 0) { double a = ch.First().Item2[1], b = ch.Last().Item2[1]; ch = ch.Where(l => l.Item2[1] > a + 1e-6 && l.Item2[1] < b - 1e-6).ToList(); }
            var capBox = (double[])others[0].GetBodyBox();
            var capSizes = Enumerable.Range(0, 3).Where(i => i != lenAx).Select(i => Math.Round((capBox[3 + i] - capBox[i]) * 1000, 2)).ToList();
            if (cv.Count >= 2 && ch.Count >= 2)
            {
                double wv = (cv.Last().Item2[0] - cv.First().Item2[0]) / cvs * 1000, hv = (ch.Last().Item2[1] - ch.First().Item2[1]) / cvs * 1000;
                if (capSizes.Contains(Math.Round(wv, 2)) && capSizes.Contains(Math.Round(hv, 2)))
                {
                    Dim2(drwDoc, selMgr, capView, cv.First().Item1, cv.Last().Item1, (cv.First().Item2[0] + cv.Last().Item2[0]) / 2, co[1] - 0.010, "end cap width", cv.First().Item2[0], cv.Last().Item2[0], true);
                    Dim2(drwDoc, selMgr, capView, ch.First().Item1, ch.Last().Item1, co[2] + 0.012, (ch.First().Item2[1] + ch.Last().Item2[1]) / 2, "end cap height", ch.Last().Item2[1], ch.First().Item2[1], false);
                    capExpected.Add(wv); capExpected.Add(hv);
                }
                else problems.Add("end cap edges do not match the cap (" + Mm(wv / 1000) + " x " + Mm(hv / 1000) + "): cap size not dimensioned");
            }
            else problems.Add("end cap edges not found: cap size not dimensioned");
            drwDoc.ClearSelection2(true);
        }
        if (capView != null)
        {
            var side0 = side; side = capView;   // the hole is seen in the cap view
            var capHoles = others.SelectMany(b => ((object[])b.GetFaces()).Cast<Face2>())
                .Where(f => ((Surface)f.GetSurface()).IsCylinder() && f.GetLoopCount() == 2)
                .Select(f => (double[])((Surface)f.GetSurface()).CylinderParams)
                .Where(cp => Math.Abs(Math.Abs(new[] { cp[3], cp[4], cp[5] }[lenAx]) - 1) < 1e-6).ToList();
            var sc = ViewCircles(mu, side);
            var so = (double[])side.GetOutline();
            int kk = 0;
            foreach (var hd in capHoles.Select(cp => 2 * cp[6]).Distinct())
            {
                var e = sc.Where(c => Math.Abs(c.Item3 - hd) < 1e-6).OrderBy(c => Dist2(c.Item2, new[] { (so[0] + so[2]) / 2, (so[1] + so[3]) / 2 })).FirstOrDefault();
                if (e == null) { problems.Add("end cap hole Ø" + Mm(hd) + " not seen in the end view"); continue; }
                drw.ActivateView(side.GetName2());
                drwDoc.ClearSelection2(true);
                var sd = (SelectData)selMgr.CreateSelectData(); sd.View = side;
                DisplayDimension dd = null;
                if (((Entity)e.Item1).Select4(false, sd))
                    try { dd = drwDoc.AddDiameterDimension2(capAtMax ? so[0] - 0.0065 - 0.010 * kk : so[2] + 0.058, capAtMax ? so[3] + 0.0226 + 0.010 * kk : (so[1] + so[3]) / 2 + 0.006 + 0.010 * kk, 0) as DisplayDimension; } catch (System.Runtime.InteropServices.COMException) { }   // above left (Grant 15 Nov, 08X06-LMW); right, level with it, when the cap view is the lower one (GNRIC-LMW-04/-12: clear of the member, the dimensions and the label below)
                drwDoc.ClearSelection2(true);
                double gotD = dd == null ? double.NaN : ((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000;
                if (dd != null && Math.Abs(gotD - hd * 1000) < 0.05)
                {
                    dd.SetBrokenLeader2(false, (int)swDisplayDimensionLeaderText_e.swBrokenLeaderHorizontalText);
                    // caps at both ends, a hole in each (GNRIC-LMW-04/-05): one callout for both, as only one cap is drawn
                    int nCap = others.Count(b => ((object[])b.GetFaces()).Cast<Face2>().Any(f => ((Surface)f.GetSurface()).IsCylinder() && f.GetLoopCount() == 2
                        && Math.Abs(2 * ((double[])((Surface)f.GetSurface()).CylinderParams)[6] - hd) < 1e-7));
                    if (nCap > 1) dd.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, nCap + " x <MOD-DIAM>");
                    dd.SetText((int)swDimensionTextParts_e.swDimensionTextCalloutBelow, nCap > 1 ? "for HDG Drainage, one in each end cap" : "for HDG Drainage");
                    capExpected.Add(hd * 1000);
                    Console.WriteLine("End cap hole: Ø" + Mm(hd) + " for HDG Drainage");
                }
                else
                {
                    problems.Add("end cap hole callout not placed (got " + gotD + ")");
                    if (dd != null) { ((Annotation)dd.GetAnnotation()).Select3(false, null); drwDoc.Extension.DeleteSelection2(0); }
                }
                kk++;
            }
            side = side0;
        }

        // D7 (Grant 15 Nov): the stock goes with the material on the note block, e.g. "Plain Carbon Steel, SHS 40x40x4",
        // built from the section measured in the end view (the dimensions above, checked against the model before saving)
        string stock = "";
        if (hollow && secExpected.Count >= 3)
        {
            double sw0 = Math.Round(secExpected[0], 1), sh0 = Math.Round(secExpected[1], 1), st0 = Math.Round(secExpected[2], 1);
            stock = (Math.Abs(sw0 - sh0) < 0.05 ? "SHS " : "RHS ") + Mm(Math.Max(sw0, sh0) / 1000) + "x" + Mm(Math.Min(sw0, sh0) / 1000) + "x" + Mm(st0 / 1000);
            Console.WriteLine("Stock: " + stock + " (measured)");
        }
        // the cut list's label for the member (the profile's description), to compare
        string cutLabel = CutListLabel(part, body);
        if (cutLabel != null) Console.WriteLine("Cut list label: " + cutLabel);

        // 7. Hole centre marks (after the dimensions, see Plate.cs), note block, flags, revision row.
        drw.ActivateView(main.GetName2());
        if (cuts.Any(c => !c.Slot) && !main.AutoInsertCenterMarks2((int)swAutoInsertCenterMarkTypes_e.swAutoInsertCenterMarkType_Hole, 0, true, true, false, CentreMarkSize, CentreMarkGap, true, true, 0))
            problems.Add("hole centre marks not inserted");
        drwDoc.ClearSelection2(true);
        if (iso != null)
        {
            drw.ActivateView(iso.GetName2());
            string text = "<FONT size=8PTS>Filename:         <FONT size=3.5>$PRPVIEW:\"SW-File Name(File Name)\"\n"
                + "<FONT size=8PTS>Config name:  <FONT size=3.5>$PRPVIEW:\"SW-Configuration Name(Configuration Name)\"\n"
                + "<FONT size=8PTS>Material:          <FONT size=12PTS>$PRPVIEW:\"Material\"" + (stock != "" ? ", " + stock : "") + "<FONT size=8PTS>\n"
                + "<FONT size=8PTS>Weight (kg):     <FONT size=12PTS>$PRPVIEW:\"Weight\"<FONT size=8PTS>\n"
                + "Finish:                 <FONT size=12PTS>$PRPVIEW:\"Finish\"";
            var note = drwDoc.InsertNote(text) as Note;
            if (note == null) problems.Add("note block not placed");
            else
            {
                var an = (Annotation)note.GetAnnotation();
                var io = (double[])iso.GetOutline();
                an.SetPosition2(io[2] + 0.012, (io[1] + io[3]) / 2 + 0.012, 0);
                var tf = (TextFormat)an.GetTextFormat(0); tf.TypeFaceName = "Century Gothic"; tf.CharHeight = 0.00212; an.SetTextFormat(0, false, tf);
            }
        }
        // Flag: a length written in the description (L=nnn) that disagrees with the model.
        var flags = new List<string>(slotFlags); Note flagNote = null;
        var lm2 = Regex.Match(props["Description"], @"(?:L\s*=\s*|MEMBER\s*x\s*)(\d+(\.\d+)?)", RegexOptions.IgnoreCase);   // "L=150", or "MEMBER x541mm" (GNRIC-LMW)
        if (lm2.Success && Math.Abs(double.Parse(lm2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - length * 1000) > 0.5)
            flags.Add("CHECK: DESCRIPTION SAYS L=" + lm2.Groups[1].Value + ", MODEL IS " + Mm(length) + " LONG");
        if (props["Material"] == "") flags.Add("CHECK: MODEL HAS NO MATERIAL PROPERTY");
        // Section named in the description (RHS/SHS and size) against the section measured in the end view (Admin 15 Nov,
        // after Grant: GNRIC-LMW-01 says RHS 40x40x4, the model is SHS 40x40x4). A check, not a correction.
        if (cutLabel != null && stock != "")
        {
            var cm = Regex.Match(cutLabel, @"\b(RHS|SHS)\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
            if (cm.Success)
            {
                string lab = cm.Groups[1].Value.ToUpper() + " " + string.Join("x", new[] { cm.Groups[2].Value, cm.Groups[3].Value }.Select(v => double.Parse(v, System.Globalization.CultureInfo.InvariantCulture)).OrderByDescending(v => v).Select(v => v.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture))) + "x" + cm.Groups[4].Value;
                if (lab != stock) flags.Add("CHECK: CUT LIST SAYS " + cutLabel.ToUpper() + ", MODEL IS " + stock);
            }
            else flags.Add("CHECK: CUT LIST SAYS " + cutLabel.ToUpper() + ", MODEL IS " + stock);
        }
        var secM = Regex.Match(props["Description"], @"\b(RHS|SHS)\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)\s*x\s*(\d+(?:\.\d+)?)", RegexOptions.IgnoreCase);
        if (secM.Success && secExpected.Count >= 3)
        {
            Func<int, double> g = i => double.Parse(secM.Groups[i].Value, System.Globalization.CultureInfo.InvariantCulture);
            double mw = Math.Round(secExpected[0], 1), mh = Math.Round(secExpected[1], 1), mt = Math.Round(secExpected[2], 1);
            string kind = Math.Abs(mw - mh) < 0.05 ? "SHS" : "RHS";
            var dSides = new[] { g(2), g(3) }.OrderBy(x => x).ToArray(); var mSides = new[] { mw, mh }.OrderBy(x => x).ToArray();
            bool same = secM.Groups[1].Value.ToUpper() == kind && Math.Abs(dSides[0] - mSides[0]) < 0.05 && Math.Abs(dSides[1] - mSides[1]) < 0.05 && Math.Abs(g(4) - mt) < 0.05;
            string measured = kind + " " + Mm(Math.Max(mw, mh) / 1000) + "x" + Mm(Math.Min(mw, mh) / 1000) + "x" + Mm(mt / 1000);
            if (!same) flags.Add("CHECK: DESCRIPTION SAYS " + secM.Groups[1].Value.ToUpper() + " " + secM.Groups[2].Value + "x" + secM.Groups[3].Value + "x" + secM.Groups[4].Value + ", MODEL IS " + measured);
            Console.WriteLine("Section: description " + secM.Value + ", model " + measured + (same ? " (same)" : " (DIFFERENT)"));
        }
        // Weight: compare with the same volume in steel (7850 kg/m3); a part with no material is weighed at
        // SOLIDWORKS's default density and comes out far too light (14 Nov: 0.1 kg for a 1.2 kg angle).
        double steelKg = ((double[])body.GetMassProperties(7850))[5] + others.Sum(b => ((double[])b.GetMassProperties(7850))[5]);
        double wProp;
        if (double.TryParse(props["Weight"], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out wProp)
            && Math.Abs(wProp - steelKg) > Math.Max(0.1, 0.1 * steelKg))
            flags.Add("CHECK: WEIGHT IS " + props["Weight"] + " KG, IN STEEL " + steelKg.ToString("0.0") + " KG");
        if (flags.Count > 0)
        {
            drw.ActivateSheet(sheet.GetName());
            ((DrawingDoc)drwDoc).EditSheet();
            flagNote = drwDoc.InsertNote(string.Join("\n", flags)) as Note;
            if (flagNote != null) foreach (var f in flags) Console.WriteLine("Flag: " + f);
            else problems.Add("flag note not placed");
        }
        lm.SetCurrentLayer("");
        drwDoc.ClearSelection2(true);
        AddDraftRevision(drwDoc, sheet);
        drwDoc.ForceRebuild3(false);

        // 8. Layout, then the value check against the model, then save.
        Layout(drwDoc, main, side, iso);
        if (upright && side != null)
        {
            // each end view level with its end of the member (the cap end view at the cap's end: Grant's 08X06-LMW layout)
            var so2 = (double[])side.GetOutline(); var mo2 = (double[])main.GetOutline(); double hs = so2[3] - so2[1];
            bool sideAtTop = capView == null || !capAtMax;
            MoveOutline(drwDoc, side, so2[0], sideAtTop ? mo2[3] - hs / 2 : mo2[1] + hs / 2);
            if (capView != null)
            {
                var co2 = (double[])capView.GetOutline(); double hc = co2[3] - co2[1];
                MoveOutline(drwDoc, capView, so2[0] + (so2[2] - so2[0]) / 2 - (co2[2] - co2[0]) / 2, capAtMax ? mo2[3] - hc / 2 : mo2[1] + hc / 2);
            }
            // the views (with their dimensions) centred in the paper right of the isometric and its note
            if (iso != null)
            {
                var vs = new[] { main, side, capView }.Where(x => x != null).ToList();
                double gl = vs.Min(x => Extent(x)[0]), gr = vs.Max(x => Extent(x)[2]);
                double freeL = Extent(iso)[2] + 0.010, freeR = BorderX1 - 0.003;
                double dx = (freeL + freeR) / 2 - (gl + gr) / 2;
                if (gl + dx < freeL) dx = freeL - gl;
                if (Math.Abs(dx) > 0.001) foreach (var x in vs) { var xo = (double[])x.GetOutline(); MoveOutline(drwDoc, x, xo[0] + dx, double.NaN); }
                Console.WriteLine("Upright views moved " + Mm(dx) + " across to sit centred right of the isometric");
            }
        }
        // Grant 15 Nov (D9): every view that shows a different body says what it shows (the cap's 36 read as the tube's
        // inside); with the scale when it differs from the sheet
        if (upright && (endDen != den || others.Count > 0))
        {
            lm.SetCurrentLayer(DraftLayer);
            string capName = "";
            if (others.Count > 0)
            {
                var cb = (double[])others[0].GetBodyBox();
                var cross = Enumerable.Range(0, 3).Where(i => i != lenAx).Select(i => cb[3 + i] - cb[i]).OrderByDescending(x => x).ToList();
                capName = "END CAP " + Mm(cross[0]) + "x" + Mm(cross[1]) + "x" + Mm(cb[3 + lenAx] - cb[lenAx]);
            }
            foreach (var vw in new[] { side, capView }.Where(x => x != null))
            {
                drw.ActivateView(vw.GetName2());
                var lbl = new List<string>();
                if (vw == capView) lbl.Add(capName);
                else if (others.Count > 0) lbl.Add((stock != "" ? stock : "MEMBER") + " (END CAP NOT SHOWN)");
                if (endDen != den) lbl.Add("SCALE 1:" + endDen);
                var ln = drwDoc.InsertNote(string.Join("\n", lbl)) as Note;
                if (ln == null) { problems.Add("end view scale label not placed"); continue; }
                var vo = (double[])vw.GetOutline();
                ((Annotation)ln.GetAnnotation()).SetPosition2((vo[0] + vo[2]) / 2 - 0.016, vo[1] - 0.016, 0);   // under the dimensions below the view
                Console.WriteLine("View label: " + string.Join(" / ", lbl));
            }
            lm.SetCurrentLayer("");
            drwDoc.ClearSelection2(true);
            Console.WriteLine("End views at 1:" + endDen + " (sheet 1:" + den + "), labelled");
        }
        if (flagNote != null)
            PlaceFlags((Annotation)flagNote.GetAnnotation(), string.Join("\n", flags), new[] { Extent(main), side != null ? Extent(side) : null, iso != null ? Extent(iso) : null });
        var got = new List<double>(); var gotSec = new List<double>();
        foreach (Annotation a in (main.GetAnnotations() as object[]) ?? new object[0])
            if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension) got.Add(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000);
        if (side != null) foreach (Annotation a in (side.GetAnnotations() as object[]) ?? new object[0])
                if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension) gotSec.Add(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000);
        Func<IEnumerable<double>, string> fmt = l => string.Join(",", l.Select(x => Math.Round(x, 2)).OrderBy(x => x));
        var modelSec = new List<double> { (box[3] - box[0]) * 1000, (box[4] - box[1]) * 1000, (box[5] - box[2]) * 1000 };
        bool ok1 = fmt(expected) == fmt(got), ok2 = fmt(secExpected) == fmt(gotSec);
        Console.WriteLine("Check positions: model " + fmt(expected) + " / drawing " + fmt(got) + (ok1 ? " OK" : " MISMATCH"));
        Console.WriteLine("Check section: from the end view " + fmt(secExpected) + " / drawing " + fmt(gotSec) + (ok2 ? " OK" : " MISMATCH") + " (model box " + fmt(modelSec) + ")");
        bool ok3 = true;
        if (capView != null)
        {
            var gotCap = new List<double>();
            foreach (Annotation a in (capView.GetAnnotations() as object[]) ?? new object[0])
                if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension) gotCap.Add(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000);
            ok3 = fmt(capExpected) == fmt(gotCap);
            Console.WriteLine("Check end cap: " + fmt(capExpected) + " / drawing " + fmt(gotCap) + (ok3 ? " OK" : " MISMATCH"));
        }
        if (!ok1 || !ok2 || !ok3) { problems.Add("dimensions don't match the model: NOT saved"); return 1; }
        return SaveDraft(drw, drwDoc, output, pdfOut, t0);
    }

    static List<string> calloutTexts = new List<string>();

    // Where a centre mark sits on the sheet (its annotation position).
    static double[] MarkAt(CenterMark m)
    {
        var p = ((Annotation)m.GetAnnotation()).GetPosition() as double[];
        return p ?? new double[] { double.NaN, double.NaN, 0 };
    }

    static bool IsHorizOrd(DisplayDimension dd, MathUtility mu)
    {
        // a horizontal ordinate's text sits above/below the view, its leader runs vertically
        var dd2 = (DisplayData)((Annotation)dd.GetAnnotation()).GetDisplayData();
        for (int i = 0; dd2 != null && i < dd2.GetLineCount(); i++)
        {
            var l = dd2.GetLineAtIndex3(i) as double[];
            if (l != null && l.Length >= 10) return Math.Abs(l[4] - l[7]) < Math.Abs(l[5] - l[8]);
        }
        return false;
    }

    static double Dist(double[] a, double[] b) { return Math.Sqrt(Enumerable.Range(0, Math.Min(a.Length, b.Length)).Sum(i => (a[i] - b[i]) * (a[i] - b[i]))); }
    static double Dist2(double[] a, double[] b) { return Math.Sqrt((a[0] - b[0]) * (a[0] - b[0]) + (a[1] - b[1]) * (a[1] - b[1])); }
    static double Overlap(Tuple<Edge, double[], double[]> a, Tuple<Edge, double[], double[]> b, bool vertical)
    {
        int i = vertical ? 1 : 0;
        double a0 = Math.Min(a.Item2[i], a.Item3[i]), a1 = Math.Max(a.Item2[i], a.Item3[i]), b0 = Math.Min(b.Item2[i], b.Item3[i]), b1 = Math.Max(b.Item2[i], b.Item3[i]);
        return Math.Max(0, Math.Min(a1, b1) - Math.Max(a0, b0));
    }

    static List<Tuple<Edge, double[], double[]>> ViewLines(MathUtility mu, View v)
    {
        // The transforms are applied here in code, not by SOLIDWORKS (14 Nov: four requests per edge made a large
        // assembly view take minutes). Checked against SOLIDWORKS on the first edges; if they ever disagree, it
        // falls back to asking SOLIDWORKS for every point.
        var r = new List<Tuple<Edge, double[], double[]>>();
        var vt = (double[])((MathTransform)v.ModelToViewTransform).ArrayData;
        var compT = new Dictionary<object, double[]>();
        bool local = true; int checkedN = 0;
        foreach (var ce in ViewEdges(v))
        {
            var e = ce.Item2;
            var cpd = e.GetCurveParams3();
            if (cpd.CurveType != (int)swCurveTypes_e.LINE_TYPE) continue;
            var own = ce.Item1 == null ? null : (((Entity)e).GetComponent() as Component2 ?? ce.Item1);   // the part the edge belongs to
            var sp = (double[])cpd.StartPoint; var ep = (double[])cpd.EndPoint;
            double[] a, b;
            if (local)
            {
                double[] ct = null;
                if (own != null && !compT.TryGetValue(own, out ct)) { var t2 = own.Transform2; ct = t2 == null ? null : (double[])t2.ArrayData; compT[own] = ct; }
                a = Xf(vt, Xf(ct, sp)); b = Xf(vt, Xf(ct, ep));
                if (checkedN < 5)
                {
                    var a2 = ToSheet(mu, v, InAsm(mu, own, sp));
                    if (Math.Abs(a2[0] - a[0]) > 1e-7 || Math.Abs(a2[1] - a[1]) > 1e-7) { local = false; Console.WriteLine("  (local transform disagrees; asking SOLIDWORKS per point)"); }
                    checkedN++;
                }
                if (!local) { a = ToSheet(mu, v, InAsm(mu, own, sp)); b = ToSheet(mu, v, InAsm(mu, own, ep)); }
            }
            else { a = ToSheet(mu, v, InAsm(mu, own, sp)); b = ToSheet(mu, v, InAsm(mu, own, ep)); }
            r.Add(Tuple.Create(e, a, b));
        }
        return r;
    }

    // SOLIDWORKS MathTransform array [rotation 3x3, translation, scale]: p' = scale * (p x R) + t
    static double[] Xf(double[] t, double[] p)
    {
        if (t == null) return p;
        double s = t[12] == 0 ? 1 : t[12];
        return new[] { s * (p[0] * t[0] + p[1] * t[3] + p[2] * t[6]) + t[9], s * (p[0] * t[1] + p[1] * t[4] + p[2] * t[7]) + t[10], s * (p[0] * t[2] + p[1] * t[5] + p[2] * t[8]) + t[11] };
    }

    // Visible edges of a view with the component each belongs to (null for a part view).
    static List<Tuple<Component2, Edge>> ViewEdges(View v)
    {
        var r = new List<Tuple<Component2, Edge>>();
        var comps = v.GetVisibleComponents() as object[];
        if (comps == null || comps.Length == 0)
        {
            foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0]) r.Add(Tuple.Create((Component2)null, e));
            return r;
        }
        foreach (Component2 c in comps)
            foreach (Edge e in (v.GetVisibleEntities2(c, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0]) r.Add(Tuple.Create(c, e));
        return r;
    }

    // A point in a component's own coordinates -> the assembly's.
    static double[] InAsm(MathUtility mu, Component2 c, double[] p)
    {
        if (c == null || c.Transform2 == null) return p;
        var mp = (MathPoint)mu.CreatePoint(p);
        return (double[])((MathPoint)mp.MultiplyTransform(c.Transform2)).ArrayData;
    }

    static List<Tuple<Edge, double[], double>> ViewCircles(MathUtility mu, View v)
    {
        var r = new List<Tuple<Edge, double[], double>>();
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])c.CircleParams;
            r.Add(Tuple.Create(e, ToSheet(mu, v, new[] { cp[0], cp[1], cp[2] }), 2 * cp[6]));
        }
        return r;
    }

    // Ordinate dimensions over a mix of edges and centre marks (first item is the zero).
    static List<double[]> lastMissed = new List<double[]>();
    static List<string> slotFlags = new List<string>();
    static void Ordinate2(ModelDoc2 doc, SelectionMgr selMgr, View v, List<object> items, int kind, double x, double y, string what)
    {
        doc.ClearSelection2(true);
        ((DrawingDoc)doc).ActivateView(v.GetName2());   // picks by position only land in the active view
        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
        int selected = 0;
        var missed = new List<double[]>();   // slot centres the row couldn't pick (their mark already serves the other row)
        // a failed pick clears the whole selection (15 Nov), so first find which slot centres can be picked, one at a time
        {
            var smp = (SelectionMgr)doc.SelectionManager;
            foreach (var q in items.OfType<double[]>())
            {
                doc.ClearSelection2(true);
                bool can = false;
                foreach (var off in PickOffsets)
                    if (doc.Extension.SelectByID2("", "CENTERMARKSYMS", q[0] + off[0], q[1] + off[1], 0, false, 0, null, 0)
                        && smp.GetSelectedObjectCount2(-1) == 1 && smp.GetSelectedObjectType3(1, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS) { can = true; break; }
                if (!can) missed.Add(q);
            }
            doc.ClearSelection2(true);
            items = items.Where(it => !(it is double[] && missed.Contains((double[])it))).ToList();
        }
        for (int i = 0; i < items.Count; i++)
        {
            bool ok;
            if (items[i] is double[])
            {
                // a slot centre: pick the centre mark's middle on the sheet, as a person clicking it would
                var q = (double[])items[i];
                ok = false;
                var sm = (SelectionMgr)doc.SelectionManager;
                foreach (var off in PickOffsets)
                {
                    int before = sm.GetSelectedObjectCount2(-1);
                    if (!doc.Extension.SelectByID2("", "CENTERMARKSYMS", q[0] + off[0], q[1] + off[1], 0, selected > 0, 0, null, 0)) continue;
                    int n = sm.GetSelectedObjectCount2(-1);
                    if (n > before && sm.GetSelectedObjectType3(n, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS) { ok = true; break; }
                    if (n > before) sm.DeSelect2(n, -1);   // something else under the click: put it back
                }
                Console.WriteLine("    pick slot centre at " + Mm(q[0]) + "," + Mm(q[1]) + ": " + ok);
                if (!ok) { missed.Add(q); problems.Add("slot centre pick failed after the pre-check: row " + what + " may be incomplete"); continue; }
            }
            else ok = items[i] is CenterMark ? ((CenterMark)items[i]).Select(selected > 0, sd) : ((Entity)items[i]).Select4(selected > 0, sd);
            if (ok) selected++;
        }
        var smx = (SelectionMgr)doc.SelectionManager;
        var selTypes = Enumerable.Range(1, smx.GetSelectedObjectCount2(-1)).Select(ix => smx.GetSelectedObjectType3(ix, -1)).ToList();
        var beforeDims = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(an => an.GetName()));
        int r = doc.Extension.AddOrdinateDimension(kind, x, y, 0);
        var madeVals = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(an => an.GetType() == (int)swAnnotationType_e.swDisplayDimension && !beforeDims.Contains(an.GetName()))
            .Select(an => Math.Round(((Dimension)((DisplayDimension)an.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 1)).ToList();
        Console.WriteLine("Ordinate " + what + ": " + selected + " of " + items.Count + " selected (types " + string.Join(",", selTypes) + "), result " + r + ", made " + string.Join("/", madeVals));
        if (selected != items.Count || r != 0) problems.Add("ordinate dimensions " + what + " may be incomplete (result " + r + ")");
        // Grant 15 Nov: slots by their centres only. SOLIDWORKS lets a slot's centre mark serve one ordinate row only (a
        // second row can't pick it, by position, name or object, nor a fresh mark laid over it), so a slot the row missed
        // is left out of it and flagged on the sheet; never dimensioned to its end or side
        lastMissed = missed;
        foreach (var q in missed) Console.WriteLine("    slot centre " + Mm(q[0]) + "," + Mm(q[1]) + " " + what + ": not dimensioned (its centre mark already serves the other row); flagged");
    }

    // aPos / bPos: where the two edges sit on the sheet along the measured direction (x when measuresX), when known.
    static void Dim2(ModelDoc2 doc, SelectionMgr selMgr, View v, Edge a, Edge b, double x, double y, string what, double aPos = double.NaN, double bPos = double.NaN, bool measuresX = true)
    {
        doc.ClearSelection2(true);
        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
        ((Entity)a).Select4(false, sd); ((Entity)b).Select4(true, sd);
        var d = doc.AddDimension2(x, y, 0) as DisplayDimension;
        Console.WriteLine("Dimension " + what + ": " + (d != null ? "placed" : "NOT placed"));
        if (d == null) problems.Add(what + " not placed");
        doc.ClearSelection2(true);
        if (d != null && !double.IsNaN(aPos)) SmallDimOutside(d, aPos, bPos, measuresX, what);
    }

    // The cut-list Description of the folder that holds this body (a weldment profile's label), or null.
    static string CutListLabel(ModelDoc2 part, Body2 body)
    {
        try
        {
            // cut-list folders sit under the Solid Bodies folder: every feature, sub-features included
            foreach (Feature f in (object[])part.FeatureManager.GetFeatures(true) ?? new object[0])
            {
                if (f.GetTypeName2() != "CutListFolder") continue;
                var bf = (BodyFolder)f.GetSpecificFeature2();
                var bodies = (object[])bf.GetBodies() ?? new object[0];
                if (!bodies.Cast<Body2>().Any(b => b.Name == body.Name)) continue;
                string v, rv; bool wr;
                f.CustomPropertyManager.Get5("Description", false, out v, out rv, out wr);
                return string.IsNullOrWhiteSpace(rv) ? null : rv.Trim();
            }
        }
        catch (Exception ex) { Console.WriteLine("  cut list not read: " + ex.Message); }
        return null;
    }

    // Grant 15 Nov: a dimension too narrow for its text gets the text outside the extension lines, beside the first
    // edge (a: the outside of the part), with the arrows outside pointing in.
    const double NarrowDim = 0.006;
    static void SmallDimOutside(DisplayDimension d, double aPos, double bPos, bool measuresX, string what)
    {
        if (Math.Abs(aPos - bPos) >= NarrowDim) return;
        d.ArrowSide = (int)swDimensionArrowsSide_e.swDimArrowsOutside;
        var an = (Annotation)d.GetAnnotation();
        var p = (double[])an.GetPosition();
        double side = aPos >= bPos ? 1 : -1;
        double t = aPos + side * 0.0045;   // just beyond the outer extension line
        an.SetPosition2(measuresX ? t : p[0], measuresX ? p[1] : t, 0);
        Console.WriteLine("  " + what + ": narrow, text outside the extension lines, arrows pointing in");
    }

    static void AddDraftRevision(ModelDoc2 drwDoc, Sheet sheet)
    {
        var ra = sheet.RevisionTable;
        if (ra == null) { problems.Add("no revision table on the sheet"); return; }
        int id = ra.AddRevision("0A*");
        int row = ra.GetRowNumberForId(id);
        var ta = (TableAnnotation)ra;
        if (id <= 0 || row < 0) { problems.Add("revision row not added"); return; }
        ta.Text[row, 1] = DateTime.Now.ToString("d/MM/yyyy");
        ta.Text[row, 2] = "ORIGINAL DESIGN";
        drwDoc.Extension.CustomPropertyManager[""].Set2("Revision-Sheet1", "0A*");
    }

    static int SaveDraft(DrawingDoc drw, ModelDoc2 drwDoc, string output, string pdfOut, DateTime t0)
    {
        int dims = 0, notes = 0, marks = 0, offDraft = 0, dangling = 0;
        for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                int ty = a.GetType();
                if (v == (View)drw.GetFirstView() && a.Layer != DraftLayer) continue;   // the sheet format's own notes
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
}
