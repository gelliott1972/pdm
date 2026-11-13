// Plates out of the 0804 weldments (brief docs\briefs\plates-0804.md, 13 Nov): bodies, place.
//   bodies <part> [--config <name>]     every solid body of every configuration (or the one named): size along its own
//                                       frame (long x short x thick), volume, centre, faces, and the features that make it
//   place <asm> <part> <srcPart> <body> [--config <cfg>] [--src-config <cfg>] [--save] [--checked-out]
//                                       insert <part> (configuration <cfg>) into <asm>, FIXED, at the position and
//                                       orientation of solid body <body> of <srcPart> (in configuration <src-config>);
//                                       <srcPart>'s origin is taken as <asm>'s origin. Checked by overlap volume.
// Frame of a body: w = normal of its largest planar face, u = direction of its longest straight edge square to w,
// v = w x u, origin = centre of the box of its vertices in (u, v, w). The part's single body is matched to the target
// body frame to frame; of the four turns that keep the frame (u, v, w -> +-u, +-v, w sign to keep it right-handed),
// the one with the largest overlap volume (Boolean intersect of temporary copies) wins.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class SwCmd
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double m) { return (m * 1000).ToString("0.###", Inv); }

    class Frame
    {
        public double[] O, U, V, W;   // origin and axes, metres, part coordinates
        public double Lu, Lv, Lw;     // box sizes along U, V, W
        public string Why;            // null when found
    }

    static double[] Sub(double[] a, double[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
    static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    static double[] Cross(double[] a, double[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }
    static double[] Scale(double[] a, double s) { return new[] { a[0] * s, a[1] * s, a[2] * s }; }
    static double Len(double[] a) { return Math.Sqrt(Dot(a, a)); }
    static double[] Unit(double[] a) { return Scale(a, 1 / Len(a)); }
    static string P3(double[] p) { return "(" + Mm(p[0]) + ", " + Mm(p[1]) + ", " + Mm(p[2]) + ")"; }
    static string D3(double[] p) { return "(" + p[0].ToString("0.####", Inv) + ", " + p[1].ToString("0.####", Inv) + ", " + p[2].ToString("0.####", Inv) + ")"; }

    static Frame BodyFrame(Body2 b)
    {
        var f = new Frame();
        Face2 big = null; double bigArea = 0; double[] n = null;
        foreach (var face in (b.GetFaces() as object[] ?? new object[0]).Cast<Face2>())
        {
            var s = face.GetSurface() as Surface;
            if (s == null || !s.IsPlane()) continue;
            double a = face.GetArea();
            if (a > bigArea + 1e-12) { bigArea = a; big = face; var pp = (double[])s.PlaneParams; n = new[] { pp[0], pp[1], pp[2] }; }
        }
        if (big == null) { f.Why = "no planar face"; return f; }
        f.W = Unit(n);
        double best = 0;
        foreach (var e in (b.GetEdges() as object[] ?? new object[0]).Cast<Edge>())
        {
            var c = e.GetCurve() as Curve;
            if (c == null || !c.IsLine()) continue;
            var sv = e.GetStartVertex() as Vertex; var ev = e.GetEndVertex() as Vertex;
            if (sv == null || ev == null) continue;
            var d = Sub((double[])ev.GetPoint(), (double[])sv.GetPoint());
            double l = Len(d);
            if (l < 1e-9 || Math.Abs(Dot(d, f.W)) / l > 1e-6) continue;
            if (l > best + 1e-9) { best = l; f.U = Unit(d); }
        }
        if (f.U == null) { f.Why = "no straight edge square to the largest flat face"; return f; }
        f.V = Cross(f.W, f.U);
        var pts = (b.GetVertices() as object[] ?? new object[0]).Cast<Vertex>().Select(v => (double[])v.GetPoint()).ToList();
        if (pts.Count == 0) { f.Why = "no vertices"; return f; }
        Func<double[], double[]> lo = ax => new[] { pts.Min(p => Dot(p, ax)), pts.Max(p => Dot(p, ax)) };
        var ru = lo(f.U); var rv = lo(f.V); var rw = lo(f.W);
        f.Lu = ru[1] - ru[0]; f.Lv = rv[1] - rv[0]; f.Lw = rw[1] - rw[0];
        double cu = (ru[0] + ru[1]) / 2, cv = (rv[0] + rv[1]) / 2, cw = (rw[0] + rw[1]) / 2;
        f.O = new[] { cu * f.U[0] + cv * f.V[0] + cw * f.W[0], cu * f.U[1] + cv * f.V[1] + cw * f.W[1], cu * f.U[2] + cv * f.V[2] + cw * f.W[2] };
        return f;
    }

    static double Volume(Body2 b)
    {
        var mp = b.GetMassProperties(1.0) as double[];
        return mp != null && mp.Length > 3 ? mp[3] : 0;
    }

    static List<Body2> SolidBodies(PartDoc part)
    {
        return ((part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[]) ?? new object[0]).Cast<Body2>().ToList();
    }

    static string BodyLine(Body2 b)
    {
        var fr = BodyFrame(b);
        var mp = b.GetMassProperties(1.0) as double[];
        int faces = b.GetFaceCount();
        var feats = ((b.GetFeatures() as object[]) ?? new object[0]).Cast<Feature>().Select(x => x.Name).ToList();
        var sizes = fr.Why != null ? new double[0] : new[] { fr.Lu, fr.Lv, fr.Lw }.OrderByDescending(x => x).ToArray();
        return "BODY\t" + b.Name
            + "\t" + (fr.Why != null ? "size ? (" + fr.Why + ")" : string.Join(" x ", sizes.Select(Mm)) + " mm")
            + "\tvol " + (mp != null ? (mp[3] * 1e9).ToString("0.##", Inv) + " mm3" : "?")
            + "\tcentre " + (mp != null ? P3(new[] { mp[0], mp[1], mp[2] }) : "?")
            + (fr.Why == null ? "\tframe O " + P3(fr.O) + " u " + D3(fr.U) + " w " + D3(fr.W) : "")
            + "\tfaces " + faces
            + "\tfeatures " + string.Join(", ", feats)
            + (b.Visible ? "" : "\t[hidden]");
    }

    static int Bodies(ISldWorks sw, string path, string config)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocPART) { Console.Error.WriteLine("bodies needs a part: " + path); return 2; }
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (wasOpen && doc.GetSaveFlag()) Console.WriteLine("  (already open with unsaved changes: this lists them, not the saved file)");
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        var names = config != null ? new List<string> { config } : ConfigNames(doc);
        foreach (var c in names)
        {
            if (!string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, c, StringComparison.OrdinalIgnoreCase) && !doc.ShowConfiguration2(c))
            { problems.Add("could not show configuration " + c + " (has: " + string.Join(", ", ConfigNames(doc)) + ")"); continue; }
            doc.ForceRebuild3(false);
            var bodies = SolidBodies((PartDoc)doc);
            Console.WriteLine("CONFIG\t" + c + "\t" + bodies.Count + " solid bodies");
            foreach (var b in bodies) Console.WriteLine(BodyLine(b));
        }
        if (!string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, active, StringComparison.OrdinalIgnoreCase)) doc.ShowConfiguration2(active);
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving."); }
        return Finish();
    }

    // ---------- place ----------

    // SOLIDWORKS transform data: [0..8] rotation (rows = where the part's x, y, z axes go), [9..11] translation, [12] scale.
    static double[] TransformData(double[] ux, double[] uy, double[] uz, double[] t)
    {
        return new[] { ux[0], ux[1], ux[2], uy[0], uy[1], uy[2], uz[0], uz[1], uz[2], t[0], t[1], t[2], 1, 0, 0, 0 };
    }

    static readonly double[] Identity = { 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0 };

    // Maps frame n (the new part's body) onto frame t turned by (a, b): u -> a*Ut, v -> b*Vt, w -> a*b*Wt.
    static double[] FrameToFrame(Frame n, Frame t, int a, int b)
    {
        var U = Scale(t.U, a); var V = Scale(t.V, b); var W = Scale(t.W, a * b);
        Func<double[], double[]> R = x => new[] {
            U[0] * Dot(n.U, x) + V[0] * Dot(n.V, x) + W[0] * Dot(n.W, x),
            U[1] * Dot(n.U, x) + V[1] * Dot(n.V, x) + W[1] * Dot(n.W, x),
            U[2] * Dot(n.U, x) + V[2] * Dot(n.V, x) + W[2] * Dot(n.W, x) };
        var tr = Sub(t.O, R(n.O));
        return TransformData(R(new[] { 1.0, 0, 0 }), R(new[] { 0, 1.0, 0 }), R(new[] { 0, 0, 1.0 }), tr);
    }

    static double[] Apply(MathUtility mu, MathTransform xf, double[] p)
    {
        var mp = (MathPoint)mu.CreatePoint(p);
        return (double[])((MathPoint)mp.MultiplyTransform(xf)).ArrayData;
    }

    // Volume shared by the target body and the new body moved by xf (temporary copies; neither model changes).
    static double Overlap(Body2 target, Body2 nb, MathTransform xf, out string why)
    {
        why = null;
        var a = target.Copy() as Body2; var b = nb.Copy() as Body2;
        if (a == null || b == null) { why = "could not copy the bodies"; return 0; }
        if (!b.ApplyTransform(xf)) { why = "could not move the copy"; return 0; }
        int err;
        var res = a.Operations2((int)swBodyOperationType_e.SWBODYINTERSECT, b, out err) as object[];
        if (err != 0) why = "intersect error " + err;
        return res == null ? 0 : res.Cast<Body2>().Sum(x => Volume(x));
    }

    static Body2 FindBody(PartDoc part, string name)
    {
        return SolidBodies(part).FirstOrDefault(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    // Opens a part (read-only if asked) in a configuration; null with a problem if it can't.
    static ModelDoc2 OpenPartIn(ISldWorks sw, string path, string config, bool readOnly, out bool wasOpen)
    {
        bool keep = openReadOnly; openReadOnly = readOnly;
        var doc = OpenOrActivate(sw, path, config, out wasOpen);
        openReadOnly = keep;
        if (doc == null) return null;
        if (config != null && !string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, config, StringComparison.OrdinalIgnoreCase)
            && !doc.ShowConfiguration2(config))
        { problems.Add("could not show configuration " + config + " of " + path + " (has: " + string.Join(", ", ConfigNames(doc)) + ")"); return null; }
        return doc;
    }

    static bool AtOrigin(Component2 c)
    {
        var d = (double[])c.Transform2.ArrayData;
        return Enumerable.Range(0, 12).All(i => Math.Abs(d[i] - Identity[i]) < 1e-9);
    }

    static double placeTolerance = 0.001;   // the overlap may miss 0.1 % of the larger volume; --tolerance <percent> to allow more (listed slot differences)

    static int Place(ISldWorks sw, string asmPath, string partPath, string srcPath, string bodyName, string config, string srcConfig, bool save, bool checkedOut)
    {
        if (Kind(partPath) != (int)swDocumentTypes_e.swDocPART || Kind(srcPath) != (int)swDocumentTypes_e.swDocPART)
        { problems.Add("place inserts a part at a body of a part"); return Finish(); }
        var mu = (MathUtility)sw.GetMathUtility();

        // Target: the named body in the source part.
        bool srcWasOpen;
        var src = OpenPartIn(sw, srcPath, srcConfig, true, out srcWasOpen);
        if (src == null) return Finish();
        if (srcWasOpen && src.GetSaveFlag()) problems.Add(srcPath + " is open with unsaved changes: the position comes from them, not the saved file");
        string srcCfgNow = ((Configuration)src.GetActiveConfiguration()).Name;
        var target = FindBody((PartDoc)src, bodyName);
        if (target == null)
        {
            problems.Add("no solid body named " + bodyName + " in " + srcPath + " <" + srcCfgNow + ">; it has: "
                + string.Join(", ", SolidBodies((PartDoc)src).Select(b => b.Name)));
            if (!srcWasOpen) sw.CloseDoc(src.GetTitle());
            return Finish();
        }
        var ft = BodyFrame(target);
        double vt = Volume(target);
        var targetCopy = target.Copy() as Body2;   // kept after the source part closes
        Console.WriteLine("Target: " + BodyLine(target).Substring(5) + "   [" + Path.GetFileName(srcPath) + " <" + srcCfgNow + ">]");
        if (!srcWasOpen) { sw.CloseDoc(src.GetTitle()); Console.WriteLine("  (closed " + Path.GetFileName(srcPath) + " again, not saved)"); }
        if (ft.Why != null) { problems.Add("target body: " + ft.Why); return Finish(); }
        if (targetCopy == null) { problems.Add("could not copy the target body"); return Finish(); }

        // The part to insert: one solid body in the configuration used.
        bool partWasOpen;
        var part = OpenPartIn(sw, partPath, config, false, out partWasOpen);
        if (part == null) return Finish();
        string cfg = ((Configuration)part.GetActiveConfiguration()).Name;
        var nbs = SolidBodies((PartDoc)part);
        if (nbs.Count != 1) { problems.Add(Path.GetFileName(partPath) + " <" + cfg + "> has " + nbs.Count + " solid bodies; place needs exactly one"); return Finish(); }
        var nb = nbs[0];
        var fn = BodyFrame(nb);
        double vn = Volume(nb);
        Console.WriteLine("Insert: " + BodyLine(nb).Substring(5) + "   [" + Path.GetFileName(partPath) + " <" + cfg + ">]");
        if (fn.Why != null) { problems.Add("part body: " + fn.Why); return Finish(); }
        var st = new[] { ft.Lu, ft.Lv, ft.Lw }.OrderByDescending(x => x).ToArray();
        var sn = new[] { fn.Lu, fn.Lv, fn.Lw }.OrderByDescending(x => x).ToArray();
        if (Enumerable.Range(0, 3).Any(i => Math.Abs(st[i] - sn[i]) > 5e-5))
            problems.Add("sizes differ: target " + string.Join(" x ", st.Select(Mm)) + ", part " + string.Join(" x ", sn.Select(Mm)) + " mm");
        else if (Math.Abs(ft.Lu - fn.Lu) > 5e-5)
            problems.Add("the long sides don't line up (target u " + Mm(ft.Lu) + ", part u " + Mm(fn.Lu) + ")");

        // The four turns; the largest overlap wins.
        var results = new List<KeyValuePair<double[], double>>();
        foreach (var ab in new[] { new[] { 1, 1 }, new[] { -1, -1 }, new[] { 1, -1 }, new[] { -1, 1 } })
        {
            var data = FrameToFrame(fn, ft, ab[0], ab[1]);
            var xf = (MathTransform)mu.CreateTransform(data);
            var moved = Apply(mu, xf, fn.O);
            if (Len(Sub(moved, ft.O)) > 1e-7) { problems.Add("transform check failed (SOLIDWORKS reads the matrix differently): " + P3(moved) + " vs " + P3(ft.O)); return Finish(); }
            string why;
            double ov = Overlap(targetCopy, nb, xf, out why);
            Console.WriteLine("  turn u" + (ab[0] > 0 ? "+" : "-") + " v" + (ab[1] > 0 ? "+" : "-") + ": overlap " + (ov * 1e9).ToString("0.##", Inv) + " mm3" + (why != null ? " (" + why + ")" : ""));
            results.Add(new KeyValuePair<double[], double>(data, ov));
        }
        double big = Math.Max(vt, vn);
        var bestKv = results.OrderByDescending(r => r.Value).First();
        double best = bestKv.Value; var bestData = bestKv.Key;
        int fits = results.Count(r => Math.Abs(r.Value - best) <= big * 1e-6);
        double miss = big > 0 ? (big - best) / big : 1;
        Console.WriteLine("Overlap: " + (best * 1e9).ToString("0.##", Inv) + " mm3; target " + (vt * 1e9).ToString("0.##", Inv) + ", part "
            + (vn * 1e9).ToString("0.##", Inv) + " mm3; missing " + (miss * 100).ToString("0.###", Inv) + " % of the larger"
            + (fits > 1 ? "; " + fits + " turns fit equally (the part is symmetric that way)" : ""));
        if (miss > placeTolerance) problems.Add("the part doesn't fit the body: " + (miss * 100).ToString("0.##", Inv) + " % of the volume doesn't overlap (limit " + (placeTolerance * 100) + " %)");
        if (problems.Count > 0) { Console.Error.WriteLine("Not inserted: problems above."); return Finish(); }

        // Insert, move, fix.
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null || ReadOnlyOpen((ModelDoc2)asm)) return Finish();
        var doc = (ModelDoc2)asm;
        var before = Snapshot(asm);
        foreach (var o in (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>())
            Console.WriteLine("  in the assembly: " + o.Name2 + " <" + o.ReferencedConfiguration + ">" + (AtOrigin(o) ? " at the origin" : " NOT at the origin") + (o.IsFixed() ? ", fixed" : ", floating"));
        Console.WriteLine("  (the position is in " + Path.GetFileName(srcPath) + "'s coordinates, taken as the assembly's)");
        var comp = asm.AddComponent5(partPath, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig, "", false, cfg, 0, 0, 0) as Component2;
        if (comp == null) { problems.Add("AddComponent5 did not insert " + partPath); return Finish(); }
        comp.Transform2 = (MathTransform)mu.CreateTransform(bestData);
        doc.EditRebuild3();
        if (Select(doc, comp)) asm.FixComponent();
        doc.ClearSelection2(true);
        var got = (double[])comp.Transform2.ArrayData;
        double dev = Enumerable.Range(0, 12).Max(i => Math.Abs(got[i] - bestData[i]));
        string why2;
        double check = Overlap(targetCopy, nb, comp.Transform2, out why2);
        Console.WriteLine("Inserted " + comp.Name2 + " <" + comp.ReferencedConfiguration + ">" + (comp.IsFixed() ? ", fixed" : ", NOT FIXED")
            + "; placed as computed: " + (dev < 1e-9 ? "yes" : "NO (off by " + dev + ")") + "; overlap read back " + (check * 1e9).ToString("0.##", Inv) + " mm3");
        if (!comp.IsFixed()) problems.Add(comp.Name2 + " is not fixed");
        if (dev >= 1e-9) problems.Add(comp.Name2 + " is not where it was put (off by " + dev + ")");
        if (Math.Abs(check - best) > big * 1e-6) problems.Add("overlap read back differs: " + check + " vs " + best);
        if (!string.Equals(comp.ReferencedConfiguration, cfg, StringComparison.OrdinalIgnoreCase)) problems.Add(comp.Name2 + " uses " + comp.ReferencedConfiguration + ", not " + cfg);
        Diff(before, Snapshot(asm));
        if (save && problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        return AfterChange(sw, doc, save, checkedOut);
    }

    // ---------- new-asm ----------

    // A new assembly from SOLIDWORKS' default assembly template with <part> (configuration <cfg>) fixed at the origin;
    // always saved (that is the point). The template's configurations and custom properties are listed so the
    // properties can be set afterwards (DmEdit), as for the 0804 STA-01s.
    static int NewAsm(ISldWorks sw, string newAsm, string partPath, string config)
    {
        string template = sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly);
        if (string.IsNullOrEmpty(template) || !File.Exists(template)) { problems.Add("no default assembly template (" + template + ")"); return Finish(); }
        bool partWasOpen;
        var part = OpenPartIn(sw, partPath, config, false, out partWasOpen);
        if (part == null) return Finish();
        string cfg = ((Configuration)part.GetActiveConfiguration()).Name;
        var doc = sw.NewDocument(template, 0, 0, 0) as ModelDoc2;
        if (doc == null) { problems.Add("could not start a new assembly from " + template); return Finish(); }
        Console.WriteLine("New assembly from " + template);
        var asm = (AssemblyDoc)doc;
        var comp = asm.AddComponent5(partPath, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig, "", false, cfg, 0, 0, 0) as Component2;
        if (comp == null) { problems.Add("AddComponent5 did not insert " + partPath); return Finish(); }
        var mu = (MathUtility)sw.GetMathUtility();
        comp.Transform2 = (MathTransform)mu.CreateTransform(Identity);
        doc.EditRebuild3();
        if (Select(doc, comp)) asm.FixComponent();
        doc.ClearSelection2(true);
        Console.WriteLine("  " + comp.Name2 + " <" + comp.ReferencedConfiguration + ">" + (AtOrigin(comp) ? " at the origin" : " NOT AT THE ORIGIN") + (comp.IsFixed() ? ", fixed" : ", NOT FIXED"));
        if (!AtOrigin(comp)) problems.Add(comp.Name2 + " is not at the origin");
        if (!comp.IsFixed()) problems.Add(comp.Name2 + " is not fixed");
        if (!string.Equals(comp.ReferencedConfiguration, cfg, StringComparison.OrdinalIgnoreCase)) problems.Add(comp.Name2 + " uses " + comp.ReferencedConfiguration + ", not " + cfg);
        Console.WriteLine("  configurations: " + string.Join(", ", ConfigNames(doc)));
        PrintProps("  file properties", doc.Extension.CustomPropertyManager[""]);
        foreach (var c in ConfigNames(doc)) PrintProps("  " + c + " properties", doc.Extension.CustomPropertyManager[c]);
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above (the new assembly is left open, unsaved)."); return Finish(); }
        int err = 0, warn = 0;
        bool ok = doc.Extension.SaveAs3(newAsm, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref err, ref warn);
        Console.WriteLine((ok && File.Exists(newAsm) ? "Saved " : "SAVE FAILED ") + newAsm
            + (err != 0 ? " errors: " + FlagNames(typeof(swFileSaveError_e), err) : "") + (warn != 0 ? " warnings: " + FlagNames(typeof(swFileSaveWarning_e), warn) : ""));
        if (!ok || !File.Exists(newAsm) || err != 0) problems.Add("new-asm did not write " + newAsm);
        return Finish();
    }

    static void PrintProps(string label, CustomPropertyManager pm)
    {
        var names = ((pm.GetNames() as object[]) ?? new object[0]).Cast<string>().ToList();
        Console.WriteLine(label + ": " + (names.Count == 0 ? "none" : names.Count.ToString()));
        foreach (var n in names)
        {
            string val, res; bool wasRes, link;
            pm.Get6(n, false, out val, out res, out wasRes, out link);
            Console.WriteLine("    " + n + " = " + val + (res != val ? "   (" + res + ")" : ""));
        }
    }

    // ---------- swap-asm ----------

    // swap-asm <asm> <oldPart> <newAsm> <innerPart> [--save] [--checked-out]
    // Every top-level instance of <oldPart> in <asm> is replaced by <newAsm>, which holds <innerPart> (a Save As copy of
    // <oldPart>, so its faces keep their IDs) at its origin. Replacing a part by an assembly breaks every mate on the
    // part (13 Nov test: 10 in 08A04-SFA-2T), so before the replace each mate entity on <oldPart> is found in the part
    // (face, edge, vertex: by matching the component's entities; plane, axis, point: by feature name) and its persistent
    // ID kept; after it, the same entity is taken from <innerPart> inside the new instance and put back into the mate.
    // An entity on a body that <innerPart> no longer has (a plate) can't be put back: listed as a problem.
    class MateFix
    {
        public string Mate, OldInst, What, FeatName, BodyName;
        public int Index;
        public object Pid;
        public double[] Xf;
        public bool Done;
    }

    static List<Feature> TopMates(ModelDoc2 doc)
    {
        var r = new List<Feature>();
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        {
            if (f.GetTypeName2() != "MateGroup") continue;
            var stack = new Stack<Feature>();
            for (var m = f.GetFirstSubFeature() as Feature; m != null; m = m.GetNextSubFeature() as Feature) stack.Push(m);
            while (stack.Count > 0)
            {
                var m = stack.Pop();
                if (m.GetTypeName2() == "FtrFolder")
                {   // mates in a folder
                    for (var s = m.GetFirstSubFeature() as Feature; s != null; s = s.GetNextSubFeature() as Feature) stack.Push(s);
                    continue;
                }
                if (m.Name.StartsWith("___EndTag___")) continue;
                if (m.GetSpecificFeature2() is Mate2) r.Add(m);
            }
        }
        return r;
    }

    static object[] MateEntities(Feature f, out object data)
    {
        data = f.GetDefinition();
        if (data == null) return null;
        try { return ((dynamic)data).EntitiesToMate as object[]; } catch { return null; }
    }

    static int SwapAsm(ISldWorks sw, string asmPath, string oldPart, string newAsm, string innerPart, bool save, bool checkedOut)
    {
        var asm = OpenAssembly(sw, asmPath);
        if (asm == null || ReadOnlyOpen((ModelDoc2)asm)) return Finish();
        var doc = (ModelDoc2)asm;
        var insts = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>()
            .Where(c => string.Equals(c.GetPathName(), oldPart, StringComparison.OrdinalIgnoreCase)).ToList();
        if (insts.Count == 0) { problems.Add("no top-level instance of " + oldPart + " in " + asmPath); return Finish(); }
        foreach (var c in insts)
        {
            int st = c.GetSuppression2();
            if (st == (int)swComponentSuppressionState_e.swComponentSuppressed) { problems.Add(c.Name2 + " is suppressed"); return Finish(); }
            if (st != (int)swComponentSuppressionState_e.swComponentFullyResolved && st != (int)swComponentSuppressionState_e.swComponentResolved)
                c.SetSuppression2((int)swComponentSuppressionState_e.swComponentFullyResolved);
        }
        Console.WriteLine("Instances of " + Path.GetFileName(oldPart) + ": " + string.Join(", ", insts.Select(c => c.Name2 + (c.IsFixed() ? " (fixed)" : " (floating)"))));
        doc.ForceRebuild3(false);
        int errorsBefore = doc.Extension.GetWhatsWrongCount();
        if (errorsBefore > 0) { Console.WriteLine("Before the swap:"); ReportWhatsWrong(doc); problems.Clear(); Console.WriteLine("  (" + errorsBefore + " items wrong already; compared after)"); }

        // 1. Every mate entity on an instance: its persistent ID in the part.
        var fixes = new List<MateFix>();
        var instByName = insts.ToDictionary(c => c.Name2, StringComparer.OrdinalIgnoreCase);
        var cache = new Dictionary<string, List<KeyValuePair<object, object>>>();   // instance -> (part entity, assembly entity)
        foreach (var mf in TopMates(doc))
        {
            var mate = (Mate2)mf.GetSpecificFeature2();
            object data;
            var ents = MateEntities(mf, out data);
            for (int i = 0; i < mate.GetMateEntityCount(); i++)
            {
                var me = mate.MateEntity(i);
                var rc = me == null ? null : me.ReferenceComponent as Component2;
                if (rc == null || !instByName.ContainsKey(rc.Name2)) continue;
                var fx = new MateFix { Mate = mf.Name, OldInst = rc.Name2, Index = i, Xf = (double[])rc.Transform2.ArrayData };
                fixes.Add(fx);
                object ent = ents != null && i < ents.Length ? ents[i] : me.Reference;
                var feat = ent as Feature;
                if (feat != null) { fx.FeatName = feat.Name; fx.What = "feature \"" + feat.Name + "\""; continue; }
                var oldDoc = rc.GetModelDoc2() as ModelDoc2;
                if (oldDoc == null) { fx.What = "? (part not loaded)"; continue; }
                List<KeyValuePair<object, object>> list;
                if (!cache.TryGetValue(rc.Name2, out list))
                {
                    list = new List<KeyValuePair<object, object>>();
                    foreach (var b in SolidBodies((PartDoc)oldDoc))
                        foreach (var arr in new[] { b.GetFaces(), b.GetEdges(), b.GetVertices() })
                            foreach (var pe in (arr as object[] ?? new object[0]))
                            {
                                object ce = null;
                                try { ce = rc.GetCorrespondingEntity(pe); } catch { }
                                if (ce != null) list.Add(new KeyValuePair<object, object>(pe, ce));
                            }
                    cache[rc.Name2] = list;
                }
                Type want = ent is Face2 ? typeof(Face2) : ent is Edge ? typeof(Edge) : ent is Vertex ? typeof(Vertex) : null;
                var hit = list.FirstOrDefault(kv => (want == null || want.IsInstanceOfType(kv.Key))
                    && sw.IsSame(kv.Value, ent) == (int)swObjectEquality.swObjectSame);
                if (hit.Key == null) { fx.What = (want != null ? want.Name : "entity") + " (not found in the part)"; continue; }
                fx.Pid = oldDoc.Extension.GetPersistReference3(hit.Key);
                var face = hit.Key as Face2;
                var body = face != null ? face.GetBody() as Body2 : hit.Key is Edge ? ((Edge)hit.Key).GetBody() as Body2 : null;
                fx.BodyName = body != null ? body.Name : "";
                fx.What = want.Name + (fx.BodyName != "" ? " of body \"" + fx.BodyName + "\"" : "");
            }
        }
        Console.WriteLine("Mate entities on those instances: " + fixes.Count + " in " + fixes.Select(f => f.Mate).Distinct().Count() + " mates");
        foreach (var fx in fixes) Console.WriteLine("  " + fx.Mate + " [" + fx.Index + "] " + fx.OldInst + ": " + fx.What + (fx.Pid == null && fx.FeatName == null ? "  <- NO ID" : ""));
        if (fixes.Any(f => f.Pid == null && f.FeatName == null)) { problems.Add("some mate entities could not be identified in the part; nothing replaced"); return Finish(); }

        // 2. Replace every instance.
        var before = Snapshot(asm);
        if (!Select(doc, insts[0])) return Finish();
        bool ok = asm.ReplaceComponents2(newAsm, "Default", true, (int)swReplaceComponentsConfiguration_e.swReplaceComponentsConfiguration_ManuallySelect, true);
        Console.WriteLine((ok ? "Replaced " : "REPLACE FAILED ") + insts.Count + " instance(s) by " + newAsm + " <Default>");
        if (!ok) { problems.Add("ReplaceComponents2 returned false"); return Finish(); }
        doc.ForceRebuild3(false);
        var news = (asm.GetComponents(true) as object[] ?? new object[0]).Cast<Component2>()
            .Where(c => string.Equals(c.GetPathName(), newAsm, StringComparison.OrdinalIgnoreCase)).ToList();
        if (news.Count != insts.Count) problems.Add(news.Count + " instances of the new assembly, " + insts.Count + " before");
        Diff(before, Snapshot(asm));

        // 3. The same entities from the inner part of each new instance (matched to the old one by position).
        Func<double[], Component2> newAt = xf => news.FirstOrDefault(n =>
        {
            var d = (double[])n.Transform2.ArrayData;
            return Enumerable.Range(0, 12).All(i => Math.Abs(d[i] - xf[i]) < 1e-9);
        });
        var fixedNow = news.Where(n => n.IsFixed()).Count();
        foreach (var grp in fixes.GroupBy(f => f.Mate))
        {
            var mf = ((AssemblyDoc)doc).FeatureByName(grp.Key) as Feature;
            if (mf == null) { problems.Add("mate " + grp.Key + " is gone after the replace"); continue; }
            object data;
            var ents = MateEntities(mf, out data);
            if (ents == null) { problems.Add(grp.Key + ": can't read its entities"); continue; }
            bool changed = false;
            foreach (var fx in grp)
            {
                var ni = newAt(fx.Xf);
                if (ni == null) { problems.Add(grp.Key + ": no new instance where " + fx.OldInst + " was"); continue; }
                var inner = ((ni.GetChildren() as object[]) ?? new object[0]).Cast<Component2>()
                    .FirstOrDefault(c => string.Equals(c.GetPathName(), innerPart, StringComparison.OrdinalIgnoreCase));
                if (inner == null) { problems.Add(ni.Name2 + " has no " + Path.GetFileName(innerPart)); continue; }
                object ne = null;
                if (fx.FeatName != null) ne = inner.FeatureByName(fx.FeatName);
                else
                {
                    var innerDoc = inner.GetModelDoc2() as ModelDoc2;
                    int err;
                    var pe = innerDoc == null ? null : innerDoc.Extension.GetObjectByPersistReference3(fx.Pid, out err);
                    if (pe != null) ne = inner.GetCorrespondingEntity(pe);
                }
                if (ne == null) { problems.Add(grp.Key + " [" + fx.Index + "]: " + fx.What + " is not in " + Path.GetFileName(innerPart) + " (a plate?): not put back"); continue; }
                if (fx.Index >= ents.Length) { problems.Add(grp.Key + ": has " + ents.Length + " entities now"); continue; }
                ents[fx.Index] = ne; changed = true; fx.Done = true;
            }
            if (!changed) continue;
            try { ((dynamic)data).EntitiesToMate = ents; } catch (Exception ex) { problems.Add(grp.Key + ": " + ex.Message); continue; }
            bool mok = mf.ModifyDefinition(data, doc, null);
            Console.WriteLine("  " + grp.Key + ": " + (mok ? "put back on " + Path.GetFileName(innerPart) : "SOLIDWORKS REFUSED the change"));
            if (!mok) problems.Add(grp.Key + ": ModifyDefinition failed");
        }
        doc.ForceRebuild3(false);
        foreach (var n in news) Console.WriteLine("  new instance " + n.Name2 + (n.IsFixed() ? ", fixed" : ", floating"));
        int wrong = doc.Extension.GetWhatsWrongCount();
        Console.WriteLine("Mates put back: " + fixes.Count(f => f.Done) + " of " + fixes.Count + " entities; items wrong: " + errorsBefore + " before, " + wrong + " now");
        ReportWhatsWrong(doc);
        if (save && problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        if (save) { if (!SaveDoc(sw, doc, checkedOut)) return 2; }
        else Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save).");
        return Finish();
    }

    // ---------- dims, add-config ----------

    // Every feature (and sketch under it) with its dimensions: full name, value in mm (or degrees), in one configuration.
    static List<Feature> AllFeatures(ModelDoc2 doc)
    {
        var r = new List<Feature>();
        var f = doc.FirstFeature() as Feature;
        while (f != null)
        {
            r.Add(f);
            var sub = f.GetFirstSubFeature() as Feature;
            while (sub != null) { r.Add(sub); sub = sub.GetNextSubFeature() as Feature; }
            f = f.GetNextFeature() as Feature;
        }
        return r;
    }

    static string DimValue(Dimension d, string cfg)
    {
        var v = d.GetSystemValue3((int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { cfg }) as double[];
        if (v == null || v.Length == 0) return "?";
        bool angle = d.GetType() == (int)swDimensionParamType_e.swDimensionParamTypeDoubleAngular;
        return angle ? (v[0] * 180 / Math.PI).ToString("0.###", Inv) + " deg" : Mm(v[0]);
    }

    static int Dims(ISldWorks sw, string path, string config)
    {
        bool wasOpen;
        var doc = OpenPartIn(sw, path, config, true, out wasOpen);
        if (doc == null) return Finish();
        string cfg = ((Configuration)doc.GetActiveConfiguration()).Name;
        Console.WriteLine("Configuration: " + cfg + " (configurations: " + string.Join(", ", ConfigNames(doc)) + ")");
        var seen = new HashSet<string>();
        foreach (var f in AllFeatures(doc))
        {
            var dd = f.GetFirstDisplayDimension() as DisplayDimension;
            while (dd != null)
            {
                var d = dd.GetDimension2(0) as Dimension;
                if (d != null && seen.Add(d.FullName))
                    Console.WriteLine("DIM\t" + d.FullName + "\t" + DimValue(d, cfg) + "\t" + f.Name + " (" + f.GetTypeName2() + ")"
                        + (f.IsSuppressed() ? " [suppressed]" : ""));
                dd = f.GetNextDisplayDimension(dd) as DisplayDimension;
            }
        }
        if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving."); }
        return Finish();
    }

    // A new configuration copied from <from>: its configured dimensions changed in the new configuration only, named
    // features suppressed in it only; the from configuration is checked unchanged. Description, BOM part number source
    // and the from configuration's custom properties are carried over and listed (PartNo etc. are set afterwards).
    static int AddConfig(ISldWorks sw, string path, string newCfg, string from, List<string> dims, List<string> suppress, bool save, bool checkedOut)
    {
        if (from == null) { problems.Add("add-config needs --from <configuration>"); return Finish(); }
        bool wasOpen;
        var doc = OpenPartIn(sw, path, from, false, out wasOpen);
        if (doc == null || ReadOnlyOpen(doc)) return Finish();
        if (ConfigNames(doc).Any(c => string.Equals(c, newCfg, StringComparison.OrdinalIgnoreCase))) { problems.Add("configuration " + newCfg + " already exists"); return Finish(); }
        var parsed = new List<KeyValuePair<Dimension, double>>();
        foreach (var s in dims)
        {
            int eq = s.LastIndexOf('=');
            double mm;
            if (eq <= 0 || !double.TryParse(s.Substring(eq + 1), NumberStyles.Float, Inv, out mm)) { problems.Add("--dim needs <name>=<mm>: " + s); continue; }
            string dn = System.Text.RegularExpressions.Regex.Replace(s.Substring(0, eq), @"@[^@]+\.(Part|Assembly)$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            var d = doc.Parameter(dn) as Dimension;
            if (d == null) { problems.Add("no dimension " + s.Substring(0, eq) + " (see SwCmd dims)"); continue; }
            parsed.Add(new KeyValuePair<Dimension, double>(d, mm / 1000));
        }
        var supp = new List<Feature>();
        foreach (var n in suppress)
        {
            var f = ((PartDoc)doc).FeatureByName(n) as Feature;
            if (f == null) problems.Add("no feature named " + n); else supp.Add(f);
        }
        if (problems.Count > 0) { Console.Error.WriteLine("Nothing changed: problems above."); return Finish(); }

        var fromC = (Configuration)doc.GetConfigurationByName(from);
        var fromBodies = SolidBodies((PartDoc)doc).Select(b => BodyLine(b)).ToList();
        var fromDims = parsed.Select(kv => DimValue(kv.Key, from)).ToList();
        var cm = doc.ConfigurationManager;
        var nc = cm.AddConfiguration2(newCfg, "", "", 0, "", fromC.Description, true) as Configuration;
        if (nc == null) { problems.Add("SOLIDWORKS did not add configuration " + newCfg); return Finish(); }
        Console.WriteLine("Added configuration " + nc.Name + " from " + from + "; active now: " + ((Configuration)doc.GetActiveConfiguration()).Name);
        // Show the new configuration for real, then take the dimensions again: handles taken before it existed set
        // the value in the from configuration (13 Nov test on 08X04-PLP's D2@Sketch1).
        doc.ShowConfiguration2(from);
        doc.ShowConfiguration2(newCfg);
        Console.WriteLine("  showing " + ((Configuration)doc.GetActiveConfiguration()).Name);
        nc.BOMPartNoSource = fromC.BOMPartNoSource;
        nc.Description = fromC.Description;
        nc.UseDescriptionInBOM = fromC.UseDescriptionInBOM;
        nc.AlternateName = fromC.AlternateName;
        nc.UseAlternateNameInBOM = fromC.UseAlternateNameInBOM;
        for (int i = 0; i < parsed.Count; i++)
        {
            var d = doc.Parameter(parsed[i].Key.FullName.Substring(0, parsed[i].Key.FullName.LastIndexOf('@'))) as Dimension ?? parsed[i].Key;
            parsed[i] = new KeyValuePair<Dimension, double>(d, parsed[i].Value);
            int r = d.SetSystemValue3(parsed[i].Value, (int)swSetValueInConfiguration_e.swSetValue_InSpecificConfigurations, new[] { newCfg });
            if (r != (int)swSetValueReturnStatus_e.swSetValue_Successful) problems.Add("could not set " + d.FullName + " (status " + r + ")");
        }
        foreach (var f in supp)
            if (!f.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swSpecifyConfiguration, new[] { newCfg }))
                problems.Add("could not suppress " + f.Name + " in " + newCfg);
        doc.ForceRebuild3(false);
        ReportWhatsWrong(doc);
        for (int i = 0; i < parsed.Count; i++)
        {
            string now = DimValue(parsed[i].Key, newCfg), was = DimValue(parsed[i].Key, from);
            Console.WriteLine("  " + parsed[i].Key.FullName + ": " + newCfg + " " + now + ", " + from + " " + was + " (before " + fromDims[i] + ")");
            if (now != Mm(parsed[i].Value)) problems.Add(parsed[i].Key.FullName + " in " + newCfg + " is " + now + ", not " + Mm(parsed[i].Value));
            if (was != fromDims[i]) problems.Add(parsed[i].Key.FullName + " changed in " + from + " too: " + fromDims[i] + " -> " + was);
        }
        foreach (var f in supp) Console.WriteLine("  suppressed in " + newCfg + ": " + f.Name);
        Console.WriteLine("  " + newCfg + ": BOM part number source " + nc.BOMPartNoSource + ", description '" + nc.Description + "'");
        PrintProps("  " + from + " properties", doc.Extension.CustomPropertyManager[from]);
        PrintProps("  " + newCfg + " properties (new; set PartNo etc. after)", doc.Extension.CustomPropertyManager[newCfg]);
        // Re-show it: right after SetSuppression2 the bodies still showed the suppressed hole (13 Nov test).
        doc.ShowConfiguration2(from); doc.ShowConfiguration2(newCfg); doc.ForceRebuild3(false);
        Console.WriteLine("CONFIG\t" + newCfg);
        foreach (var b in SolidBodies((PartDoc)doc)) Console.WriteLine(BodyLine(b));
        doc.ShowConfiguration2(from);
        doc.ForceRebuild3(false);
        var fromAfter = SolidBodies((PartDoc)doc).Select(b => BodyLine(b)).ToList();
        if (!fromBodies.SequenceEqual(fromAfter)) { problems.Add("the bodies of " + from + " changed"); foreach (var l in fromAfter) Console.WriteLine("  now " + l); }
        else Console.WriteLine("  " + from + ": bodies unchanged");
        Console.WriteLine("  configurations now: " + string.Join(", ", ConfigNames(doc)));
        if (!save) { Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save)."); return Finish(); }
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        // Every configuration rebuilt before saving, so the Weight values stored for each are current (the from
        // configuration's Weight read 0.0 after the change, 13 Nov test).
        bool all = doc.Extension.ForceRebuildAll();
        Console.WriteLine("  rebuilt every configuration: " + (all ? "ok" : "SOLIDWORKS reported a failure"));
        if (!all) { problems.Add("ForceRebuildAll failed"); Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        if (!SaveDoc(sw, doc, checkedOut)) return 2;
        return Finish();
    }

    // ---------- delete-feature ----------

    // Deletes the named features with their absorbed sketches and children. Any other feature that goes with them is
    // a problem unless it is a sketch: name it too. Prints the features removed and every configuration's bodies
    // left. --save only when clean.
    // A document this command is to change must not be open read-only (it would be changed and then not saved).
    static bool ReadOnlyOpen(ModelDoc2 doc)
    {
        if (!doc.IsOpenedReadOnly()) return false;
        problems.Add(doc.GetPathName() + " is open read-only in SOLIDWORKS: close it first (SwCmd close), nothing changed");
        return true;
    }

    static List<string> FeatureNames(ModelDoc2 doc)
    {
        var r = new List<string>();
        var f = doc.FirstFeature() as Feature;
        while (f != null) { r.Add(f.Name + "\t" + f.GetTypeName2()); f = f.GetNextFeature() as Feature; }
        return r;
    }

    static int DeleteFeature(ISldWorks sw, string path, List<string> names, bool save, bool checkedOut)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocPART) { problems.Add("delete-feature is for parts"); return Finish(); }
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (ReadOnlyOpen(doc)) return Finish();
        var before = FeatureNames(doc);
        doc.ClearSelection2(true);
        foreach (var n in names)
        {
            var f = ((PartDoc)doc).FeatureByName(n) as Feature;
            if (f == null) { problems.Add("no feature named " + n); continue; }
            if (!f.Select2(true, 0)) problems.Add("could not select " + n);
        }
        if (problems.Count > 0) { doc.ClearSelection2(true); Console.Error.WriteLine("Nothing deleted: problems above."); return Finish(); }
        bool ok = doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed | (int)swDeleteSelectionOptions_e.swDelete_Children);
        var after = FeatureNames(doc);
        var gone = before.Where(x => !after.Contains(x)).ToList();
        Console.WriteLine((ok ? "Deleted" : "DELETE FAILED") + "; features removed (" + gone.Count + "):");
        foreach (var g in gone) Console.WriteLine("  - " + g.Replace("\t", "  (") + ")");
        if (!ok) problems.Add("DeleteSelection2 returned false");
        foreach (var n in names) if (after.Any(x => string.Equals(x.Split('\t')[0], n, StringComparison.OrdinalIgnoreCase))) problems.Add("still there: " + n);
        foreach (var g in gone)
        {
            var parts = g.Split('\t');
            bool named = names.Any(n => string.Equals(n, parts[0], StringComparison.OrdinalIgnoreCase));
            // sketches absorbed by the deleted features, and cut-list folders left empty (13 Nov test)
            bool sketch = parts[1] == "ProfileFeature" || parts[1] == "3DProfileFeature" || parts[1] == "CutListFolder";
            if (!named && !sketch) problems.Add("also removed, not named: " + parts[0] + " (" + parts[1] + "); name it too if that's meant");
        }
        doc.ForceRebuild3(false);
        ReportWhatsWrong(doc);
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        foreach (var c in ConfigNames(doc))
        {
            if (!string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, c, StringComparison.OrdinalIgnoreCase) && !doc.ShowConfiguration2(c))
            { problems.Add("could not show configuration " + c); continue; }
            doc.ForceRebuild3(false);
            var bodies = SolidBodies((PartDoc)doc);
            Console.WriteLine("CONFIG\t" + c + "\t" + bodies.Count + " solid bodies left");
            foreach (var b in bodies) Console.WriteLine(BodyLine(b));
        }
        if (!string.Equals(((Configuration)doc.GetActiveConfiguration()).Name, active, StringComparison.OrdinalIgnoreCase)) doc.ShowConfiguration2(active);
        if (!save) { Console.WriteLine("Not saved (the change is in SOLIDWORKS only; run save, or pass --save)."); return Finish(); }
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); return Finish(); }
        if (!SaveDoc(sw, doc, checkedOut)) return 2;
        return Finish();
    }
}
