// SwProfiles: read-only survey of weldment members (brief sw-profiles, step 0, 16 Nov 2026).
// Opens each file read-only in the running SOLIDWORKS 2026, never saves, closes what it opened. Tab-separated output:
//   MEMBER  <file>  <feature>  <profile path>  <profile configuration (size)>  <bodies>
//   CUT     <file>  <part config>  <folder>  <qty>  <Description resolved>  <Description raw>  <member feature(s)>  <measured section>  <length prop>
//           (for a folder with no member, the measured section is "plate t x w x l (volume)": brief step 10)
//   MAT     <file>  <part config>  <SOLIDWORKS material>  <database>  <Material property level>  <raw>  <resolved>  <mass>  <volume>   (step 9)
//   NOMEMBERS <file>
//   PROFILE <file>  <config>  <Description>  <other properties name=value;...>  <sketch section>   (for .sldlfp profile files)
//   ERROR   <file>  <message>
// The measured section comes from the body's geometry: the long axis is the direction of the longest straight edge;
// the flat faces square to it give, per direction, the outside size (outermost faces) and the wall (outermost to next).
// Usage: SwProfiles.exe <file or @listfile> [...]   Run under SwCmd's lock (--as sw). Refuses Dropbox paths except copies.
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwProfiles.exe SwProfiles.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwProfiles
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.##", Inv); }
    static string Clean(string s) { return (s ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "); }

    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwProfiles <file or @listfile> [...]"); return 2; }
        var files = new List<string>();
        foreach (var a in args)
            if (a.StartsWith("@")) files.AddRange(File.ReadAllLines(a.Substring(1)).Select(l => l.Trim()).Where(l => l != "" && !l.StartsWith("#")));
            else files.Add(a);
        if (System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length == 0) { Console.Error.WriteLine("SOLIDWORKS is not running"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026: " + sw.RevisionNumber()); return 2; }
        int rc = 0;
        foreach (var a in files)
        {
            string file = Path.GetFullPath(a);
            if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith("M:", StringComparison.OrdinalIgnoreCase))
            { Console.WriteLine("ERROR\t" + file + "\trefused (Dropbox): survey a copy"); rc = 2; continue; }
            bool wasOpen = sw.GetOpenDocumentByName(file) != null;
            int errs = 0, warns = 0;
            var doc = wasOpen ? (ModelDoc2)sw.GetOpenDocumentByName(file)
                : sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
            if (doc == null) { Console.WriteLine("ERROR\t" + file + "\tcould not open (errors " + errs + ")"); rc = 3; continue; }
            try
            {
                if (file.EndsWith(".sldlfp", StringComparison.OrdinalIgnoreCase)) Profile(doc, file);
                else Part(doc, file);
            }
            catch (Exception e) { Console.WriteLine("ERROR\t" + file + "\t" + Clean(e.Message)); rc = 3; }
            finally { if (!wasOpen) sw.CloseDoc(doc.GetTitle()); }
            Console.Out.Flush();
        }
        return rc;
    }

    static void Part(ModelDoc2 doc, string file)
    {
        var part = (PartDoc)doc;
        string active = doc.ConfigurationManager.ActiveConfiguration.Name;
        // Members: feature definitions are the same in every configuration (the size can differ only by suppression)
        bool any = false;
        foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
        {
            if (f.GetTypeName2() != "WeldMemberFeat") continue;
            any = true;
            var d = f.GetDefinition() as StructuralMemberFeatureData;
            Console.WriteLine("MEMBER\t" + file + "\t" + Clean(f.Name) + "\t" + Clean(d == null ? "?" : d.WeldmentProfilePath) + "\t" + Clean(d == null ? "?" : d.ConfigurationName)
                + "\t" + (f.IsSuppressed() ? "suppressed in " + active : ""));
        }
        if (!any) Console.WriteLine("NOMEMBERS\t" + file);
        var measured = new Dictionary<string, string>();
        foreach (string cfg in (string[])doc.GetConfigurationNames())
        {
            if (doc.ConfigurationManager.ActiveConfiguration.Name != cfg) doc.ShowConfiguration2(cfg);
            // Material (step 9): the SOLIDWORKS material, the Material property (configuration, then file), mass and volume
            {
                string db, mat = part.GetMaterialPropertyName2(cfg, out db);
                string praw = "", pres = "", where = "";
                foreach (var lvl in new[] { cfg, "" })
                {
                    var m = doc.Extension.CustomPropertyManager[lvl];
                    if (m == null || !(((string[])m.GetNames() ?? new string[0]).Any(n => n.Equals("Material", StringComparison.OrdinalIgnoreCase)))) continue;
                    bool wr, lk; m.Get6("Material", false, out praw, out pres, out wr, out lk);
                    where = lvl == "" ? "file" : "cfg"; break;
                }
                var mp = (MassProperty)doc.Extension.CreateMassProperty();
                string mass = mp == null ? "" : string.Format(Inv, "{0:0.###} kg\t{1:0.###} cm3", mp.Mass, mp.Volume * 1e6);
                Console.WriteLine("MAT\t" + file + "\t" + Clean(cfg) + "\t" + Clean(mat) + "\t" + Clean(db) + "\t" + where + "\t" + Clean(praw) + "\t" + Clean(pres) + "\t" + mass);
            }
            foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
            {
                if (f.GetTypeName2() != "CutListFolder") continue;
                var bf = f.GetSpecificFeature2() as BodyFolder;
                if (bf == null) continue;
                var bodies = ((object[])bf.GetBodies() ?? new object[0]).Cast<Body2>().ToList();
                if (bodies.Count == 0) continue;
                var members = new SortedSet<string>();
                foreach (var b in bodies)
                    foreach (Feature bfeat in (object[])b.GetFeatures() ?? new object[0])
                        if (bfeat.GetTypeName2() == "WeldMemberFeat") { members.Add(bfeat.Name); break; }
                var cpm = f.CustomPropertyManager;
                string raw, res; bool wasRes, link;
                cpm.Get6("Description", false, out raw, out res, out wasRes, out link);
                string lraw, lres;
                cpm.Get6("LENGTH", false, out lraw, out lres, out wasRes, out link);
                string key = string.Join(",", members), sect;
                if (members.Count == 0) sect = "plate " + Plate(bodies[0]);   // step 10: James's "PLATE, t x w x l"
                else if (!measured.TryGetValue(key, out sect)) { sect = Section(bodies[0]); measured[key] = sect; }   // once per member per part
                Console.WriteLine("CUT\t" + file + "\t" + Clean(cfg) + "\t" + Clean(f.Name) + "\t" + bodies.Count + "\t" + Clean(res) + "\t" + Clean(raw)
                    + "\t" + string.Join(",", members) + "\t" + sect + "\t" + Clean(lres));
            }
        }
        if (doc.ConfigurationManager.ActiveConfiguration.Name != active) doc.ShowConfiguration2(active);
    }

    // A library profile: per configuration (size), its properties and the section of its profile sketch (box of the sketch).
    static void Profile(ModelDoc2 doc, string file)
    {
        string active = doc.ConfigurationManager.ActiveConfiguration.Name;
        foreach (string cfg in (string[])doc.GetConfigurationNames())
        {
            if (doc.ConfigurationManager.ActiveConfiguration.Name != cfg) doc.ShowConfiguration2(cfg);
            var props = new List<string>();
            string desc = "";
            foreach (var mgr in new[] { Tuple.Create("cfg", doc.Extension.CustomPropertyManager[cfg]), Tuple.Create("file", doc.Extension.CustomPropertyManager[""]) })
            {
                var names = (string[])mgr.Item2.GetNames() ?? new string[0];
                foreach (var n in names)
                {
                    string raw, res; bool wasRes, link;
                    mgr.Item2.Get6(n, false, out raw, out res, out wasRes, out link);
                    props.Add(mgr.Item1 + ":" + n + "=" + Clean(raw) + (raw != res ? " [" + Clean(res) + "]" : ""));
                    if (n.Equals("Description", StringComparison.OrdinalIgnoreCase) && desc == "") desc = Clean(res) + " (" + mgr.Item1 + ")";
                }
            }
            // sketch box: the profile sketch's segments
            string sk = "";
            foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
            {
                if (f.GetTypeName2() != "ProfileFeature") continue;
                var s = f.GetSpecificFeature2() as Sketch;
                if (s == null) continue;
                double x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9;
                foreach (SketchPoint p in (object[])s.GetSketchPoints2() ?? new object[0])
                { x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X); y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y); }
                var dims = new List<string>();
                for (var dd = (DisplayDimension)f.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)f.GetNextDisplayDimension(dd))
                { var dim = (Dimension)dd.GetDimension2(0); dims.Add(dim.Name + "=" + Mm(dim.SystemValue)); }
                sk += (sk == "" ? "" : " | ") + f.Name + ": points box " + (x0 > x1 ? "none" : Mm(x1 - x0) + " x " + Mm(y1 - y0)) + "; dims " + string.Join(" ", dims);
            }
            Console.WriteLine("PROFILE\t" + file + "\t" + Clean(cfg) + "\t" + desc + "\t" + string.Join("; ", props) + "\t" + sk);
        }
        if (doc.ConfigurationManager.ActiveConfiguration.Name != active) doc.ShowConfiguration2(active);
    }

    // A plate body's size, "t x w x l": the normal of its largest flat face, the longest straight edge square to that normal,
    // and the third direction; extents over the body's vertices (edge end points).
    static string Plate(Body2 b)
    {
        Face2 big = null; double area = 0;
        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
            if (((Surface)f.GetSurface()).IsPlane() && f.GetArea() > area) { area = f.GetArea(); big = f; }
        if (big == null) return "no flat face";
        var n = (double[])big.Normal;
        double[] u = null; double best = 0;
        var pts = new List<double[]>();
        foreach (Edge e in (object[])b.GetEdges() ?? new object[0])
        {
            var cp = (double[])e.GetCurveParams2();
            pts.Add(new[] { cp[0], cp[1], cp[2] }); pts.Add(new[] { cp[3], cp[4], cp[5] });
            if (!((Curve)e.GetCurve()).IsLine()) continue;
            double dx = cp[3] - cp[0], dy = cp[4] - cp[1], dz = cp[5] - cp[2], len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-9 || Math.Abs((dx * n[0] + dy * n[1] + dz * n[2]) / len) > 1e-4) continue;
            if (len > best) { best = len; u = new[] { dx / len, dy / len, dz / len }; }
        }
        if (u == null) return "no straight edge";
        var v = new[] { n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0] };
        Func<double[], double> ext = d => { var pr = pts.Select(p => p[0] * d[0] + p[1] * d[1] + p[2] * d[2]).ToList(); return pr.Max() - pr.Min(); };
        double t = ext(n), a = ext(u), c = ext(v);
        return Mm(t) + " x " + Mm(Math.Min(a, c)) + " x " + Mm(Math.Max(a, c)) + " (volume " + Vol(b) + " cm3)";
    }
    static string Vol(Body2 b)
    {
        var mp = b.GetMassProperties(1) as double[];   // centre x,y,z, volume, area, mass...
        return mp == null || mp.Length < 4 ? "?" : (mp[3] * 1e6).ToString("0.#", Inv);
    }

    // Section of a straight member body, from its geometry.
    static string Section(Body2 b)
    {
        var edges = ((object[])b.GetEdges() ?? new object[0]).Cast<Edge>().ToList();
        double[] axis = null; double best = 0;
        foreach (var e in edges)
        {
            var c = (Curve)e.GetCurve();
            if (!c.IsLine()) continue;
            var cp = (double[])e.GetCurveParams2();
            double dx = cp[3] - cp[0], dy = cp[4] - cp[1], dz = cp[5] - cp[2];
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len > best) { best = len; axis = new[] { dx / len, dy / len, dz / len }; }
        }
        if (axis == null) return "no straight edge";
        // flat faces whose normal is square to the axis, grouped by direction (sign ignored)
        var groups = new List<Tuple<double[], List<double>>>();
        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
        {
            var s = (Surface)f.GetSurface();
            if (!s.IsPlane()) continue;
            var n = (double[])f.Normal;
            if (Math.Abs(n[0] * axis[0] + n[1] * axis[1] + n[2] * axis[2]) > 1e-4) continue;
            var pp = (double[])s.PlaneParams;   // normal (0..2), root point (3..5)
            var g = groups.FirstOrDefault(t => Math.Abs(Math.Abs(t.Item1[0] * n[0] + t.Item1[1] * n[1] + t.Item1[2] * n[2]) - 1) < 1e-4);
            if (g == null) { g = Tuple.Create(n, new List<double>()); groups.Add(g); }
            double off = pp[3] * g.Item1[0] + pp[4] * g.Item1[1] + pp[5] * g.Item1[2];
            if (!g.Item2.Any(o => Math.Abs(o - off) < 1e-6)) g.Item2.Add(off);
        }
        var parts = new List<string>();
        foreach (var g in groups.OrderByDescending(t => t.Item2.Count == 0 ? 0 : t.Item2.Max() - t.Item2.Min()))
        {
            var o = g.Item2.OrderBy(x => x).ToList();
            double size = o.Last() - o.First();
            string wall = o.Count >= 3 ? Mm(Math.Min(o[1] - o[0], o[o.Count - 1] - o[o.Count - 2])) : "-";
            parts.Add(Mm(size) + "(wall " + wall + ", planes " + o.Count + ")");
        }
        var bb = (double[])b.GetBodyBox();
        return "axis len " + Mm(best) + "; " + (parts.Count == 0 ? "no flat sides" : string.Join(" x ", parts));
    }
}
