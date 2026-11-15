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
        if (!sf.IsCylinder()) return false;
        double r = ((double[])sf.CylinderParams)[6];
        foreach (Edge e in (object[])f.GetEdges() ?? new object[0])
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsCircle()) continue;
            var cp = (double[])e.GetCurveParams2();
            double chord = Math.Sqrt(Math.Pow(cp[3] - cp[0], 2) + Math.Pow(cp[4] - cp[1], 2) + Math.Pow(cp[5] - cp[2], 2));
            if (Math.Abs(chord - 2 * r) < 0.01 * r) return true;   // half a circle
        }
        return false;
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
}
