// measure-member <part> <config>: what a welded member actually is, read from SOLIDWORKS geometry (read-only, never
// saves). Admin 15 Nov, after Grant: "How does the internal measurement of SHS40x40x4 end up 36mm not 32mm?"
// Per body: its box; the flat faces square to each cross axis (outside and inside walls); corner radii. Then the
// structural member features (profile library path, the profile sketch's own dimensions) and the end caps (thickness,
// offset, inward or not).
using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    static int MeasureMember(string partFile, string config)
    {
        var part = OpenDoc(partFile, config, true);
        if (part.ConfigurationManager.ActiveConfiguration.Name != config) part.ShowConfiguration2(config);
        Console.WriteLine("Configuration: " + part.ConfigurationManager.ActiveConfiguration.Name);
        var bodies = ((object[])((PartDoc)part).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Cast<Body2>().ToList();
        Func<double, string> mm = v => (v * 1000).ToString("0.###");
        foreach (var b in bodies)
        {
            var bb = (double[])b.GetBodyBox();
            Console.WriteLine("Body '" + b.Name + "': box X " + mm(bb[0]) + ".." + mm(bb[3]) + ", Y " + mm(bb[1]) + ".." + mm(bb[4]) + ", Z " + mm(bb[2]) + ".." + mm(bb[5])
                + "  (size " + mm(bb[3] - bb[0]) + " x " + mm(bb[4] - bb[1]) + " x " + mm(bb[5] - bb[2]) + ")");
            var faces = ((object[])b.GetFaces()).Cast<Face2>().ToList();
            for (int ax = 0; ax < 3; ax++)
            {
                int a = ax;
                var at = faces.Where(f => ((Surface)f.GetSurface()).IsPlane() && Math.Abs(Math.Abs(((double[])f.Normal)[a]) - 1) < 1e-6)
                    .Select(f => { var fb = (double[])f.GetBox(); return Math.Round(fb[a] * 1e6) / 1e6; }).Distinct().OrderBy(x => x).ToList();
                if (at.Count > 0) Console.WriteLine("  flat faces square to " + "XYZ"[ax] + " at " + string.Join(", ", at.Select(mm)));
            }
            var radii = faces.Where(f => ((Surface)f.GetSurface()).IsCylinder()).Select(f => Math.Round(((double[])((Surface)f.GetSurface()).CylinderParams)[6] * 1e6) / 1e6).Distinct().OrderBy(x => x);
            Console.WriteLine("  cylinder radii " + string.Join(", ", radii.Select(mm)));
        }
        foreach (Feature f in (object[])part.FeatureManager.GetFeatures(true) ?? new object[0])
        {
            string t = f.GetTypeName2();
            if (t == "WeldMemberFeat")
            {
                var d = f.GetDefinition() as StructuralMemberFeatureData;
                Console.WriteLine("Structural member '" + f.Name + "': profile " + (d == null ? "?" : d.WeldmentProfilePath) + (d == null ? "" : ", configuration " + d.ConfigurationName));
                for (var sf = (Feature)f.GetFirstSubFeature(); sf != null; sf = (Feature)sf.GetNextSubFeature())
                {
                    Console.WriteLine("  sub-feature '" + sf.Name + "' (" + sf.GetTypeName2() + ")");
                    for (var dd = (DisplayDimension)sf.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)sf.GetNextDisplayDimension(dd))
                    {
                        var dim = (Dimension)dd.GetDimension2(0);
                        Console.WriteLine("    " + dim.FullName + " = " + mm(dim.SystemValue));
                    }
                }
            }
            else if (t == "EndCap")
            {
                var d = f.GetDefinition() as EndCapFeatureData;
                if (d == null) { Console.WriteLine("End cap '" + f.Name + "': no data"); continue; }
                Console.WriteLine("End cap '" + f.Name + "': thickness " + mm(d.Thickness) + ", offset " + (Convert.ToBoolean(d.UseThicknessRatioForOffset) ? "ratio " + d.ThicknessRatioForOffset : mm(d.OffsetDistance))
                    + ", inward " + d.IsEndCapInward + (Convert.ToBoolean(d.IsEndCapInward) ? " (depth " + mm(d.DepthDistance) + ")" : "") + ", chamfer corners " + d.UseChamferCorners + (Convert.ToBoolean(d.UseChamferCorners) ? " " + mm(d.ChamferDistance) : ""));
            }
        }
        return 0;
    }

    // close-mine: close (without saving) Draw's own documents: files under the test folder (never a "-grant" file), and
    // unsaved new drawings whose views show a model under the test folder (drafts left by a run that refused to save).
    // Other sessions' and Grant's documents are left alone (no close-all while Grant has drawings open).
    static int CloseMine()
    {
        int closed = 0;
        var docs = new List<ModelDoc2>();
        for (var d = (ModelDoc2)sw.GetFirstDocument(); d != null; d = (ModelDoc2)d.GetNext()) docs.Add(d);
        // drawings first, so the models they hold can close too
        var order = new List<ModelDoc2>();
        foreach (var d in docs) { try { if (d.GetType() == (int)swDocumentTypes_e.swDocDRAWING) order.Insert(0, d); else order.Add(d); } catch { } }
        foreach (var d in order)
        {
            string path, title;
            try { path = d.GetPathName() ?? ""; title = d.GetTitle(); } catch (System.Runtime.InteropServices.COMException) { continue; }   // already closed with a drawing that held it
            bool mine;
            if (path == "")
            {
                mine = false;
                if (d.GetType() == (int)swDocumentTypes_e.swDocDRAWING)
                    for (var v = (View)((DrawingDoc)d).GetFirstView(); v != null; v = (View)v.GetNextView())
                    {
                        string r = v.GetReferencedModelName() ?? "";
                        if (r.StartsWith(TestRoot + "\\", StringComparison.OrdinalIgnoreCase)) { mine = true; break; }
                    }
            }
            else mine = path.StartsWith(TestRoot + "\\", StringComparison.OrdinalIgnoreCase) && title.IndexOf("-grant", StringComparison.OrdinalIgnoreCase) < 0 && path.IndexOf("-grant", StringComparison.OrdinalIgnoreCase) < 0;
            if (!mine) continue;
            sw.CloseDoc(path == "" ? title : path);
            Console.WriteLine("Closed " + (path == "" ? title + " (unsaved)" : path));
            closed++;
        }
        Console.WriteLine("Closed " + closed + " of Draw's documents; " + (docs.Count - closed) + " left open");
        return 0;
    }

    // Grant 15 Nov: slots are located by their centres only, in both directions, on every drawing; their size comes only
    // from the slot callout. A dimension attached to a slot's end arc, side or edge is refused like a dangling one.
    // A slot end: a cylinder face (a hole) with a half-circle edge (chord = diameter). A slot side: a flat face next to a
    // slot end, square to the plate (parallel to the slot's axis). An edge belongs to a slot if either of its faces does.
    static bool SlotEnd(Face2 f)
    {
        var sf = (Surface)f.GetSurface();
        if (!sf.IsCylinder() || f.GetLoopCount() != 1) return false;   // a round hole has two loops
        // a half cylinder: its circular edges (top and bottom rim, possibly split) sweep 2 x 180 degrees in all;
        // a plate's or tube's rounded corner sweeps 2 x 90
        double sweep = 0;
        foreach (Edge e in (object[])f.GetEdges() ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])e.GetCurveParams2();
            double a = Math.Abs(cp[7] - cp[6]);
            if (a > 2 * Math.PI + 1e-6) a = a % (2 * Math.PI);
            sweep += a;
        }
        return Math.Abs(sweep - 2 * Math.PI) < 0.1;
    }
    static bool SlotFace(Face2 f)
    {
        if (SlotEnd(f)) return true;
        var sf = (Surface)f.GetSurface();
        if (!sf.IsPlane()) return false;
        var n = (double[])f.Normal;
        foreach (Edge e in (object[])f.GetEdges() ?? new object[0])
            foreach (Face2 o in (object[])e.GetTwoAdjacentFaces2() ?? new object[0])
            {
                if (o == null || o.IsSame(f) || !SlotEnd(o)) continue;
                var cp = (double[])((Surface)o.GetSurface()).CylinderParams;
                if (Math.Abs(n[0] * cp[3] + n[1] * cp[4] + n[2] * cp[5]) < 1e-6) return true;   // square to the plate: the slot's side
            }
        return false;
    }
    // The dimensions on a drawing attached to slot geometry (the slot's size callout excepted).
    static List<string> SlotDims(DrawingDoc drw)
    {
        var bad = new List<string>();
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                var dd = (DisplayDimension)a.GetSpecificAnnotation();
                string txt = (dd.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix) ?? "") + (dd.GetText((int)swDimensionTextParts_e.swDimensionTextSuffix) ?? "");
                if (txt.IndexOf("SLOT", StringComparison.OrdinalIgnoreCase) >= 0) continue;   // the slot's size callout
                var ents = a.GetAttachedEntities3() as object[];
                bool slot = false;
                foreach (var o in ents ?? new object[0])
                {
                    var e = o as Edge;
                    if (e == null) continue;
                    foreach (Face2 f in (object[])e.GetTwoAdjacentFaces2() ?? new object[0])
                        if (f != null && SlotFace(f)) { slot = true; break; }
                    if (slot) break;
                }
                if (System.Environment.GetEnvironmentVariable("DRAW_DEBUG") == "1")
                    Console.WriteLine("  " + v.GetName2() + " " + Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1) + ": "
                        + string.Join(", ", (ents ?? new object[0]).Select(o2 => o2 == null ? "null" : o2 is Edge ? "edge(" + string.Join("/", ((object[])((Edge)o2).GetTwoAdjacentFaces2() ?? new object[0]).Select(ff => ff == null ? "-" : (((Surface)((Face2)ff).GetSurface()).IsCylinder() ? "cyl" : "plane") + (SlotFace((Face2)ff) ? "*SLOT*" : ""))) + ")" : o2.GetType().Name)));
                if (slot) bad.Add(v.GetName2() + " " + Math.Round(((Dimension)dd.GetDimension2(0)).GetSystemValue2("") * 1000, 1));
            }
        return bad;
    }
    static int SlotCheck(string drawing)
    {
        var doc = OpenDoc(drawing, null, true);
        var bad = SlotDims((DrawingDoc)doc);
        Console.WriteLine((bad.Count == 0 ? "OK no slot-end dimensions" : "SLOT-END DIMENSIONS " + bad.Count + ": " + string.Join(", ", bad)) + "  " + drawing);
        sw.CloseDoc(doc.GetPathName());
        return bad.Count == 0 ? 0 : 1;
    }

    // Where to click a slot centre mark (sheet metres from its centre): the centre, then the small centre cross (1 mm),
    // then out along the arms past the 2.5 mm gap (4, 6 mm). After the first row, its extension lines run through the
    // centre and along one arm, so the second row needs the other arm (15 Nov: 2 mm fell in the gap and picked nothing).
    static readonly double[][] PickOffsets = new[] { 0.0, 0.001, -0.001, 0.004, -0.004, 0.006, -0.006, 0.0085, -0.0085, 0.010, -0.010, 0.0115, -0.0115 }
        .SelectMany(d => d == 0 ? new[] { new[] { 0.0, 0.0 } } : new[] { new[] { d, 0.0 }, new[] { 0.0, d } }).ToArray();

    // A centre mark already used by one ordinate row can't be picked by position again (its extension lines cover it):
    // select the nearest centre mark to q by its name, then as an object, and keep it only if it lands as a centre mark.
    static bool PickMarkByName(ModelDoc2 doc, View v, double[] q, bool append)
    {
        var sm = (SelectionMgr)doc.SelectionManager;
        Annotation best = null; double bd = double.MaxValue;
        foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
        {
            if (a.GetType() != (int)swAnnotationType_e.swCenterMarkSym) continue;
            var p = (double[])a.GetPosition();
            double d = Math.Sqrt(Math.Pow(p[0] - q[0], 2) + Math.Pow(p[1] - q[1], 2));
            // the nearest; at the same spot, the newest (a fresh mark laid over one already used by a row)
            if (d < bd - 1e-5 || (Math.Abs(d - bd) <= 1e-5 && best != null && MarkNo(a) > MarkNo(best))) { bd = d; best = a; }
        }
        if (best == null || bd > 0.008) { foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0]) if (a.GetType() == (int)swAnnotationType_e.swCenterMarkSym) { var pp = (double[])a.GetPosition(); var cm = (CenterMark)a.GetSpecificAnnotation(); Console.WriteLine("      mark " + a.GetName() + " at " + (pp[0] * 1000).ToString("0.#") + "," + (pp[1] * 1000).ToString("0.#") + " style " + cm.Style); } Console.WriteLine("    no centre mark within 8 mm of " + (q[0] * 1000).ToString("0.#") + "," + (q[1] * 1000).ToString("0.#")); return false; }
        int nb = sm.GetSelectedObjectCount2(-1);
        string name = best.GetName() + "@" + v.GetName2();
        bool ok = doc.Extension.SelectByID2(name, "CENTERMARKSYMS", 0, 0, 0, append, 0, null, 0);
        int na = sm.GetSelectedObjectCount2(-1);
        if (ok && na > nb && sm.GetSelectedObjectType3(na, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS) { Console.WriteLine("    centre mark picked by name " + name); return true; }
        var sd = (SelectData)sm.CreateSelectData(); sd.View = v;
        ok = best.Select3(append, sd);
        na = sm.GetSelectedObjectCount2(-1);
        bool good = ok && na > nb && sm.GetSelectedObjectType3(na, -1) == (int)swSelectType_e.swSelCENTERMARKSYMS;
        Console.WriteLine("    centre mark " + name + (good ? " picked as an object" : " not picked (by name or object)"));
        return good;
    }

    static int MarkNo(Annotation a) { var m = System.Text.RegularExpressions.Regex.Match(a.GetName() ?? "", @"(\d+)$"); return m.Success ? int.Parse(m.Groups[1].Value) : 0; }

    // check <drawing>: read-only checks on a drawing as it is (open or from disk): every dimension's value per view,
    // dangling, anything placed by Draw not on DRAFT, and the slot check. Changes nothing, saves nothing.
    static int CheckDrawing(string drawing)
    {
        var doc = OpenDoc(drawing, null, true);
        var drw = (DrawingDoc)doc;
        int dims = 0, dangling = 0;
        for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
        {
            var vals = new List<double>();
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.IsDangling()) dangling++;
                if (a.GetType() != (int)swAnnotationType_e.swDisplayDimension) continue;
                dims++;
                vals.Add(Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 2));
            }
            if (vals.Count > 0) Console.WriteLine("  " + v.GetName2() + ": " + string.Join(",", vals.OrderBy(x => x)));
        }
        var slot = SlotDims(drw);
        Console.WriteLine("Dimensions " + dims + "; dangling " + dangling + "; slot check " + (slot.Count == 0 ? "OK" : "FAILED " + string.Join(", ", slot)));
        return dangling == 0 && slot.Count == 0 ? 0 : 1;
    }

    // align-iso <drawing>: in the open drawing, move the isometric up or down so its vertical centre is level with the
    // main view's (Grant 15 Nov, GNRIC-LMW-01). Keeps its left-right place. Edits the open drawing only; doesn't save.
    static int AlignIso(string drawing)
    {
        var doc = OpenDoc(drawing, null, false);   // for editing
        var drw = (DrawingDoc)doc;
        View main = null, iso = null;
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
        {
            string o = v.GetOrientationName() ?? "";
            if (o.IndexOf("Isometric", StringComparison.OrdinalIgnoreCase) >= 0) { if (iso == null) iso = v; }
            else if (main == null) main = v;
        }
        if (main == null || iso == null) { Console.Error.WriteLine("main view or isometric not found"); return 1; }
        var mo = (double[])main.GetOutline(); var io = (double[])iso.GetOutline();
        double want = (mo[1] + mo[3]) / 2, was = (io[1] + io[3]) / 2;
        Console.WriteLine("Main view " + main.GetName2() + " centre " + (want * 1000).ToString("0.0") + " mm; isometric " + iso.GetName2() + " centre " + (was * 1000).ToString("0.0") + " mm");
        MoveOutline(doc, iso, io[0], want);
        io = (double[])iso.GetOutline();
        Console.WriteLine("Isometric centre now " + ((io[1] + io[3]) / 2 * 1000).ToString("0.0") + " mm (left edge kept at " + (io[0] * 1000).ToString("0.0") + "); not saved");
        return 0;
    }

    // remove-note <drawing> <text>: in the open drawing, delete the sheet notes on DRAFT whose text contains <text> (e.g. a
    // CHECK line that no longer applies after the model was fixed). Edits the open drawing only; doesn't save.
    static int RemoveNote(string drawing, string contains)
    {
        var doc = OpenDoc(drawing, null, false);   // for editing
        var drw = (DrawingDoc)doc;
        int n = 0;
        for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swNote || a.Layer != DraftLayer) continue;
                var nt = (Note)a.GetSpecificAnnotation();
                string t = nt.GetText() ?? "";
                if (t.IndexOf(contains, StringComparison.OrdinalIgnoreCase) < 0) continue;
                doc.ClearSelection2(true);
                if (a.Select3(false, null) && doc.Extension.DeleteSelection2(0)) { n++; Console.WriteLine("Removed note: " + t.Replace("\n", " / ")); }
                else Console.WriteLine("Could not remove: " + t);
            }
        doc.ClearSelection2(true);
        Console.WriteLine(n + " note(s) removed; not saved");
        return n > 0 ? 0 : 1;
    }

    // save-active <out.SLDDRW>: save the active (unsaved) drawing as it is, e.g. a draft whose check refused it, so Grant
    // can look at it and say what to add (08X04-PLP-06). Refuses to overwrite.
    static int SaveActive(string output)
    {
        if (System.IO.File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists: " + output); return 2; }
        var doc = sw.ActiveDoc as ModelDoc2;
        if (doc == null || doc.GetType() != (int)swDocumentTypes_e.swDocDRAWING) { Console.Error.WriteLine("no active drawing"); return 1; }
        int err = 0, warn = 0;
        bool ok = doc.Extension.SaveAs3(output, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine((ok ? "Saved " : "NOT saved ") + doc.GetTitle() + " as " + output + " (error " + err + ")");
        return ok ? 0 : 1;
    }

    // add-projection <drawing> <view> <left|right|up|down>: in the open drawing, a projected view from <view> on that side
    // (Grant 15 Nov, 08X04-PLP-06: "all we need to do is add another projection"). Reports the full circles (holes) it
    // shows. Edits the open drawing only; doesn't save.
    static int AddProjection(string drawing, string viewName, string dir)
    {
        var doc = OpenDoc(drawing, null, false);
        var drw = (DrawingDoc)doc;
        View v = null;
        for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView()) if (x.GetName2() == viewName) v = x;
        if (v == null) { Console.Error.WriteLine("no view " + viewName); return 1; }
        var o = (double[])v.GetOutline();
        double w = o[2] - o[0], h = o[3] - o[1], g = 0.030;
        double px = dir == "left" ? o[0] - g - w / 2 : dir == "right" ? o[2] + g + w / 2 : (o[0] + o[2]) / 2;
        double py = dir == "up" ? o[3] + g + h / 2 : dir == "down" ? o[1] - g - h / 2 : (o[1] + o[3]) / 2;
        doc.ClearSelection2(true);
        doc.Extension.SelectByID2(v.GetName2(), "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
        var nv = drw.CreateUnfoldedViewAt3(px, py, 0, false);
        doc.ClearSelection2(true);
        if (nv == null) { Console.Error.WriteLine("projection not created"); return 1; }
        doc.ForceRebuild3(false);
        int full = 0;
        foreach (Edge e in (nv.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = e.GetCurveParams3(); var a = (double[])cp.StartPoint; var b = (double[])cp.EndPoint;
            if (Math.Abs(a[0] - b[0]) + Math.Abs(a[1] - b[1]) + Math.Abs(a[2] - b[2]) < 1e-9) full++;
        }
        var no = (double[])nv.GetOutline();
        Console.WriteLine("Projected " + nv.GetName2() + " (" + dir + " of " + viewName + ") at " + (no[0] * 1000).ToString("0") + "," + (no[1] * 1000).ToString("0") + " - " + (no[2] * 1000).ToString("0") + "," + (no[3] * 1000).ToString("0") + " mm; full circles seen: " + full + "; not saved");
        return 0;
    }

    // move-view <drawing> <view> <dx mm> <dy mm> / delete-view <drawing> <view>: small edits to the open drawing (no save)
    static int MoveView(string drawing, string viewName, double dxMm, double dyMm)
    {
        var doc = OpenDoc(drawing, null, false); var drw = (DrawingDoc)doc;
        for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView())
            if (x.GetName2() == viewName)
            {
                var p = (double[])x.Position; x.Position = new[] { p[0] + dxMm / 1000, p[1] + dyMm / 1000 }; doc.ForceRebuild3(false);
                var o = (double[])x.GetOutline();
                Console.WriteLine("Moved " + viewName + " to " + (o[0] * 1000).ToString("0") + "," + (o[1] * 1000).ToString("0") + " - " + (o[2] * 1000).ToString("0") + "," + (o[3] * 1000).ToString("0") + " mm");
                return 0;
            }
        Console.Error.WriteLine("no view " + viewName); return 1;
    }
    static int DeleteView(string drawing, string viewName)
    {
        var doc = OpenDoc(drawing, null, false);
        doc.ClearSelection2(true);
        bool ok = doc.Extension.SelectByID2(viewName, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0) && doc.Extension.DeleteSelection2(0);
        Console.WriteLine((ok ? "Deleted " : "Could not delete ") + viewName);
        return ok ? 0 : 1;
    }

    // hole-dims <drawing> <view>: in the open drawing, the holes seen face-on in <view> placed by ordinates from the view's
    // outer edges (across the top from the left, down the right from the top, as on the plates) and one "n x Ø d THRU"
    // callout per size attached to a hole; a floating "THRU" note with no leader is removed. No save. (08X04-PLP-06)
    static int HoleDims(string drawing, string viewName)
    {
        var doc = OpenDoc(drawing, null, false); var drw = (DrawingDoc)doc;
        View v = null;
        for (var x = (View)((View)drw.GetFirstView()).GetNextView(); x != null; x = (View)x.GetNextView()) if (x.GetName2() == viewName) v = x;
        if (v == null) { Console.Error.WriteLine("no view " + viewName); return 1; }
        var mu = (MathUtility)sw.GetMathUtility();
        var hor = new List<Tuple<Edge, double>>(); var ver = new List<Tuple<Edge, double>>(); var circ = new List<Tuple<Edge, double[], double>>();
        foreach (Edge e in (v.GetVisibleEntities2(null, (int)swViewEntityType_e.swViewEntityType_Edge) as object[]) ?? new object[0])
        {
            var c = (Curve)e.GetCurve(); var cp = e.GetCurveParams3();
            var a = ToSheet(mu, v, (double[])cp.StartPoint); var b = ToSheet(mu, v, (double[])cp.EndPoint);
            if (c.IsCircle())
            {
                var mp = (double[])cp.StartPoint; var ep = (double[])cp.EndPoint;
                if (Math.Abs(mp[0] - ep[0]) + Math.Abs(mp[1] - ep[1]) + Math.Abs(mp[2] - ep[2]) > 1e-9) continue;   // full circles only
                var q = (double[])c.CircleParams;
                circ.Add(Tuple.Create(e, ToSheet(mu, v, new[] { q[0], q[1], q[2] }), 2 * q[6]));
            }
            else if (c.IsLine())
            {
                if (Math.Abs(a[1] - b[1]) < 1e-7) hor.Add(Tuple.Create(e, a[1]));
                else if (Math.Abs(a[0] - b[0]) < 1e-7) ver.Add(Tuple.Create(e, a[0]));
            }
        }
        if (circ.Count == 0 || hor.Count < 2 || ver.Count < 2) { Console.Error.WriteLine("holes or edges not found in " + viewName); return 1; }
        var left = ver.OrderBy(l => l.Item2).First(); var right = ver.OrderBy(l => l.Item2).Last();
        var top = hor.OrderBy(l => l.Item2).Last(); var bot = hor.OrderBy(l => l.Item2).First();
        var lm = (LayerMgr)doc.GetLayerManager(); lm.SetCurrentLayer(DraftLayer);
        drw.ActivateView(v.GetName2());
        var selMgr = (SelectionMgr)doc.SelectionManager;
        double vs = v.ScaleDecimal;
        var cols = circ.GroupBy(c => Math.Round(c.Item2[0] * 1e5)).Select(g => g.OrderByDescending(c => c.Item2[1]).First()).OrderBy(c => c.Item2[0]).ToList();
        var rows = circ.GroupBy(c => Math.Round(c.Item2[1] * 1e5)).Select(g => g.OrderByDescending(c => c.Item2[0]).First()).OrderByDescending(c => c.Item2[1]).ToList();
        var before = new HashSet<string>(((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Select(a => a.GetName()));
        var hSel = new List<Edge> { left.Item1 }; hSel.AddRange(cols.Select(c => c.Item1)); hSel.Add(right.Item1);
        var vSel = new List<Edge> { top.Item1 }; vSel.AddRange(rows.Select(c => c.Item1)); vSel.Add(bot.Item1);
        var expect = new List<double> { 0, (right.Item2 - left.Item2) / vs * 1000, 0, (top.Item2 - bot.Item2) / vs * 1000 };
        expect.AddRange(cols.Select(c => (c.Item2[0] - left.Item2) / vs * 1000)); expect.AddRange(rows.Select(c => (top.Item2 - c.Item2[1]) / vs * 1000));
        Ordinate(doc, selMgr, v, hSel, (int)swAddOrdinateDims_e.swHorizontalOrdinate, left.Item2, top.Item2 + 0.012, "across the top");
        Ordinate(doc, selMgr, v, vSel, (int)swAddOrdinateDims_e.swVerticalOrdinate, right.Item2 + 0.012, top.Item2, "down the right");
        var o = (double[])v.GetOutline(); int k = 0;
        foreach (var g in circ.GroupBy(c => Math.Round(c.Item3 * 1e5)))
        {
            var pick = g.First(); doc.ClearSelection2(true);
            var sd = (SelectData)selMgr.CreateSelectData(); sd.View = v; ((Entity)pick.Item1).Select4(false, sd);
            var dd = doc.AddDiameterDimension2(o[0] - 0.030 - 0.008 * k, pick.Item2[1] + 0.022 + 0.008 * k, 0) as DisplayDimension;
            if (dd == null) { Console.WriteLine("callout not placed"); continue; }
            dd.SetBrokenLeader2(false, (int)swDisplayDimensionLeaderText_e.swBrokenLeaderHorizontalText);
            int n = g.Count();
            dd.SetText((int)swDimensionTextParts_e.swDimensionTextPrefix, (n > 1 ? n + " x " : "") + "<MOD-DIAM>");
            dd.SetText((int)swDimensionTextParts_e.swDimensionTextSuffix, " THRU");
            expect.Add(pick.Item3 * 1000); k++;
        }
        doc.ClearSelection2(true);
        var got = ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().Where(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension && !before.Contains(a.GetName()))
            .Select(a => Math.Round(((Dimension)((DisplayDimension)a.GetSpecificAnnotation()).GetDimension2(0)).GetSystemValue2("") * 1000, 2)).OrderBy(z => z).ToList();
        string fe = string.Join(",", expect.Select(z => Math.Round(z, 2)).OrderBy(z => z)), fg = string.Join(",", got);
        Console.WriteLine("Check: model " + fe + " / drawing " + fg + (fe == fg ? " OK" : " MISMATCH"));
        // the old floating callout (a note with no leader saying THRU)
        for (var x = (View)drw.GetFirstView(); x != null; x = (View)x.GetNextView())
            foreach (Annotation a in (x.GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swNote || a.Layer != DraftLayer) continue;
                var nt = (Note)a.GetSpecificAnnotation();
                if ((nt.GetText() ?? "").Contains("THRU") && a.GetLeaderCount() == 0)
                { doc.ClearSelection2(true); if (a.Select3(false, null) && doc.Extension.DeleteSelection2(0)) Console.WriteLine("Removed the floating callout: " + nt.GetText()); }
            }
        lm.SetCurrentLayer(""); doc.ClearSelection2(true);
        Console.WriteLine("not saved");
        return fe == fg ? 0 : 1;
    }

    static void EndOrdinate(ModelDoc2 doc)
    {
        doc.ClearSelection2(true);
        doc.EditRebuild3();
        doc.ClearSelection2(true);
    }
}
