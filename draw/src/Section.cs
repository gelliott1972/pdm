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

    static int MakeSection(string partFile, string config, string output, string template)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".pdf");
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
        if (bodies == null || bodies.Length != 1) { Console.Error.WriteLine("REFUSED: make-section needs one solid body"); return 2; }
        var body = (Body2)bodies[0];
        var box = (double[])body.GetBodyBox();
        var faces = ((object[])body.GetFaces()).Cast<Face2>().ToList();

        // Length axis: the axis with two flat end faces of equal area facing opposite ways.
        int lenAx = -1; double endArea = double.MaxValue;
        for (int ax = 0; ax < 3; ax++)
        {
            int a0 = ax;
            var ends = faces.Where(f => ((Surface)f.GetSurface()).IsPlane() && Math.Abs(Math.Abs(((double[])f.Normal)[a0]) - 1) < 1e-6)
                .Where(f => { var fb = (double[])f.GetBox(); return Math.Abs(fb[a0] - box[a0]) < 1e-7 || Math.Abs(fb[a0] - box[3 + a0]) < 1e-7; }).ToList();   // at the very ends
            if (ends.Count == 2 && Math.Abs(ends[0].GetArea() - ends[1].GetArea()) < 1e-9 && ((double[])ends[0].Normal)[ax] * ((double[])ends[1].Normal)[ax] < 0
                && ends[0].GetArea() < endArea) { lenAx = ax; endArea = ends[0].GetArea(); }
        }
        if (lenAx < 0) { Console.Error.WriteLine("REFUSED: can't find the member's two end faces (not a straight member)"); return 2; }
        double length = box[3 + lenAx] - box[lenAx];
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
        if (cuts.Count == 0) problems.Add("no holes or slots found");

        // The face to look at: the biggest flat face square to the holes' axis (the outside of that leg).
        var cutAxis = cuts.Count > 0 ? cuts[0].Axis : new double[] { 0, 0, 1 };
        int cAx = Enumerable.Range(0, 3).First(i => Math.Abs(Math.Abs(cutAxis[i]) - 1) < 1e-6);
        if (cuts.Any(c => Math.Abs(Math.Abs(c.Axis[cAx]) - 1) > 1e-6)) problems.Add("holes or slots in more than one direction: only the " + "XYZ"[cAx] + " ones are drawn");
        var lookFace = faces.Where(f => ((Surface)f.GetSurface()).IsPlane() && Math.Abs(Math.Abs(((double[])f.Normal)[cAx]) - 1) < 1e-6).OrderByDescending(f => f.GetArea()).First();
        var nrm = (double[])lookFace.Normal;
        string faceView = cAx == 0 ? (nrm[0] > 0 ? "*Right" : "*Left") : cAx == 1 ? (nrm[1] > 0 ? "*Top" : "*Bottom") : (nrm[2] > 0 ? "*Front" : "*Back");
        var mp = (MassProperty)part.Extension.CreateMassProperty();
        var com = (double[])mp.CenterOfMass;
        Console.WriteLine("Face view " + faceView + " (face normal " + "XYZ"[cAx] + (nrm[cAx] > 0 ? "+" : "-") + "); centre of mass " + Mm(com[0]) + "," + Mm(com[1]) + "," + Mm(com[2]));

        // 2. Scale: 1:5 by default (Grant 14 Nov); bigger when the member would be under MinSide on the sheet.
        double span = Math.Max(box[3] - box[0], Math.Max(box[4] - box[1], box[5] - box[2]));
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
        if (Math.Abs(s1[0] - s0[0]) < Math.Abs(s1[1] - s0[1]))
        {
            // the member runs up the sheet: turn the view a quarter turn so it runs across, as James draws members
            main.Angle = Math.PI / 2;
            drwDoc.ForceRebuild3(false);
            s0 = ToSheet(mu, main, p0); s1 = ToSheet(mu, main, p1);
            Console.WriteLine("Face view turned 90 degrees so the member runs across the sheet");
        }
        bool lenHoriz = Math.Abs(s1[0] - s0[0]) > Math.Abs(s1[1] - s0[1]);
        if (!lenHoriz) problems.Add("member still runs up the sheet after turning the view");
        var mo = (double[])main.GetOutline();
        // End view: projected to the right of the (turned) face view, so it is a true projection and stays level with
        // it (Grant 14 Nov: the two views linked, as James's).
        drwDoc.ClearSelection2(true);
        drwDoc.Extension.SelectByID2(main.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var side = drw.CreateUnfoldedViewAt3(mo[2] + 0.080, (mo[1] + mo[3]) / 2, 0, false);
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
            foreach (var vw in new[] { main, iso }) if (vw != null) { vw.UseSheetScale = 0; vw.ScaleRatio = new double[] { 1, d }; }   // the end view follows the face view
            drwDoc.ForceRebuild3(false);
            var o1 = (double[])main.GetOutline(); var o2 = side != null ? (double[])side.GetOutline() : o1; var o3 = iso != null ? (double[])iso.GetOutline() : new double[4];
            double topW = W(o1) + W(o2) + 0.100, topH = Math.Max(H(o1), H(o2)) + 0.030, isoW = W(o3) + 0.070, isoH = H(o3);
            bool fitsAll = topW <= AreaX1 - AreaX0 && topH + RowGap + isoH <= AreaY1 - AreaY0 && isoW <= AreaX1 - AreaX0;
            den = d;
            if (fitsAll) break;
            Console.WriteLine("  1:" + d + " doesn't fit (top row " + Mm(topW) + " x " + Mm(topH) + ", isometric " + Mm(W(o3)) + " x " + Mm(isoH) + ")");
        }
        mo = (double[])main.GetOutline();
        var sp = (double[])sheet.GetProperties2();
        sheet.SetProperties2((int)sp[0], (int)sp[1], 1, den, sp[4] != 0, sp[5], sp[6], sp[7] != 0);
        foreach (var vw in new[] { main, iso }) if (vw != null) vw.UseSheetScale = 1;
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
            var heel = acrossLines.OrderBy(l => Math.Abs(across(l.Item2) - across(comS))).First();
            var toe = acrossLines.OrderBy(l => Math.Abs(across(l.Item2) - across(heel.Item2))).Last();
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
                if (slotAcross) { tA.Add(Tuple.Create((object)arc.Item1, cs)); tX.Add(Tuple.Create((object)cs, cs)); acrossNeedsPick = true; }
                else { tX.Add(Tuple.Create((object)arc.Item1, cs)); tA.Add(Tuple.Create((object)cs, cs)); alongNeedsPick = true; }
            }
            if (alongNeedsPick && acrossNeedsPick) problems.Add("slots in both directions: one direction's slot positions can't be dimensioned yet");
            var cutSheet = cuts.Select(c => ToSheet(mu, main, c.C)).ToList();
            var sel = new List<object>();
            Action doAcross = () => {
            // across the face: from the heel, one per distinct position, then the toe
            var tAcross = tX.GroupBy(t => Math.Round(across(t.Item2) * 1e5)).Select(g => g.OrderBy(t => -Math.Abs(along(t.Item2) - along(end0.Item2))).First()).OrderBy(t => Math.Abs(across(t.Item2) - across(heel.Item2))).ToList();
            sel = new List<object> { heel.Item1 }; sel.AddRange(tAcross.Select(t => t.Item1)); sel.Add(toe.Item1);
            expected.Add(0);
            expected.AddRange(cutSheet.GroupBy(q => Math.Round(across(q) * 1e5)).Select(g => Math.Abs(across(g.First()) - across(heel.Item2)) / vs * 1000));
            expected.Add(Math.Abs(across(toe.Item2) - across(heel.Item2)) / vs * 1000);
            double endLine = along(end1.Item2) + 0.012;
            Ordinate2(drwDoc, selMgr, main, sel, lenHoriz ? (int)swAddOrdinateDims_e.swVerticalOrdinate : (int)swAddOrdinateDims_e.swHorizontalOrdinate,
                lenHoriz ? endLine : across(heel.Item2), lenHoriz ? across(heel.Item2) : endLine, "across the face");
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
            int n = g.Count();
            string text = (n > 1 ? n + " x " : "") + (c0.Slot ? (c0.Width * 1000).ToString("0.0") + " X " + (c0.Length * 1000).ToString("0") : "<MOD-DIAM>" + (c0.Width * 1000).ToString("0.#")) + " THRU";
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
            an.SetPosition2(mo[0] - 0.035, mo[3] + 0.015 + 0.008 * k, 0);
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
                Dim2(drwDoc, selMgr, side, left.Item1, right.Item1, (left.Item2[0] + right.Item2[0]) / 2, so[3] + 0.010, "section width");
                Dim2(drwDoc, selMgr, side, bot.Item1, top.Item1, so[2] + 0.012, (bot.Item2[1] + top.Item2[1]) / 2, "section height");
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
                    Dim2(drwDoc, selMgr, side, longest.Item1, inner.Item1, tx, ty, "wall thickness");
                    secExpected.Add(t);
                }
            }
            else problems.Add("end view outline not found; no section sizes");
            drwDoc.ClearSelection2(true);
        }

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
                + "<FONT size=8PTS>Material:          <FONT size=12PTS>$PRPVIEW:\"Material\"<FONT size=8PTS>\n"
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
        var flags = new List<string>(); Note flagNote = null;
        var lm2 = Regex.Match(props["Description"], @"L\s*=\s*(\d+(\.\d+)?)");
        if (lm2.Success && Math.Abs(double.Parse(lm2.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) - length * 1000) > 0.5)
            flags.Add("CHECK: DESCRIPTION SAYS L=" + lm2.Groups[1].Value + ", MODEL IS " + Mm(length) + " LONG");
        if (props["Material"] == "") flags.Add("CHECK: MODEL HAS NO MATERIAL PROPERTY");
        // Weight: compare with the same volume in steel (7850 kg/m3); a part with no material is weighed at
        // SOLIDWORKS's default density and comes out far too light (14 Nov: 0.1 kg for a 1.2 kg angle).
        double steelKg = ((double[])body.GetMassProperties(7850))[5];
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
        if (!ok1 || !ok2) { problems.Add("dimensions don't match the model: NOT saved"); return 1; }
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
        var r = new List<Tuple<Edge, double[], double[]>>();
        foreach (var ce in ViewEdges(v))
        {
            var e = ce.Item2;
            var c = (Curve)e.GetCurve();
            if (!c.IsLine()) continue;
            var cpd = e.GetCurveParams3();
            r.Add(Tuple.Create(e, ToSheet(mu, v, InAsm(mu, ce.Item1, (double[])cpd.StartPoint)), ToSheet(mu, v, InAsm(mu, ce.Item1, (double[])cpd.EndPoint))));
        }
        return r;
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
    static void Ordinate2(ModelDoc2 doc, SelectionMgr selMgr, View v, List<object> items, int kind, double x, double y, string what)
    {
        doc.ClearSelection2(true);
        ((DrawingDoc)doc).ActivateView(v.GetName2());   // picks by position only land in the active view
        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
        int selected = 0;
        for (int i = 0; i < items.Count; i++)
        {
            bool ok;
            if (items[i] is double[])
            {
                // a slot centre: pick the centre mark's middle on the sheet, as a person clicking it would
                var q = (double[])items[i];
                // centre first; if another dimension's line already covers it, a little way along the mark's arms
                ok = false;
                var sm = (SelectionMgr)doc.SelectionManager;
                foreach (var off in new[] { new[] { 0.0, 0.0 }, new[] { 0.002, 0.0 }, new[] { -0.002, 0.0 }, new[] { 0.0, 0.002 }, new[] { 0.0, -0.002 } })
                {
                    int before = sm.GetSelectedObjectCount2(-1);
                    if (!doc.Extension.SelectByID2("", "CENTERMARKSYMS", q[0] + off[0], q[1] + off[1], 0, i > 0, 0, null, 0)) continue;
                    int n = sm.GetSelectedObjectCount2(-1);
                    if (n > before && sm.GetSelectedObjectType3(n, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS) { ok = true; break; }
                }
                Console.WriteLine("    pick slot centre at " + Mm(q[0]) + "," + Mm(q[1]) + ": " + ok + " (" + ((SelectionMgr)doc.SelectionManager).GetSelectedObjectType3(((SelectionMgr)doc.SelectionManager).GetSelectedObjectCount2(-1), -1) + ")");
            }
            else ok = items[i] is CenterMark ? ((CenterMark)items[i]).Select(i > 0, sd) : ((Entity)items[i]).Select4(i > 0, sd);
            if (ok) selected++;
        }
        int r = doc.Extension.AddOrdinateDimension(kind, x, y, 0);
        Console.WriteLine("Ordinate " + what + ": " + selected + " of " + items.Count + " selected, result " + r);
        if (selected != items.Count || r != 0) problems.Add("ordinate dimensions " + what + " may be incomplete (result " + r + ")");
    }

    static void Dim2(ModelDoc2 doc, SelectionMgr selMgr, View v, Edge a, Edge b, double x, double y, string what)
    {
        doc.ClearSelection2(true);
        var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v;
        ((Entity)a).Select4(false, sd); ((Entity)b).Select4(true, sd);
        var d = doc.AddDimension2(x, y, 0) as DisplayDimension;
        Console.WriteLine("Dimension " + what + ": " + (d != null ? "placed" : "NOT placed"));
        if (d == null) problems.Add(what + " not placed");
        doc.ClearSelection2(true);
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
