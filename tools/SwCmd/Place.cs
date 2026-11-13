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
        public double[] Geo;   // where the entity is in the part (face: box + area; edge: end points; vertex: point)
        public double[] Xf;
        public bool Done;
        public object New;
    }

    static List<string> patternNotes = new List<string>();

    static HashSet<string> WrongNames(ModelDoc2 doc)
    {
        var r = new HashSet<string>();
        if (doc.Extension.GetWhatsWrongCount() == 0) return r;
        object feats, codes, warns;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warns);
        foreach (var f in (feats as object[] ?? new object[0]).OfType<Feature>()) r.Add(f.Name + "	" + f.GetTypeName2());
        return r;
    }

    class PatternFix { public string Feature, OldInst; public int Axis; public object Pid; public double[] Xf, Geo; }

    // An edge's end points (sorted) moved by xf if given; null for a closed edge.
    static double[] EdgeGeo(Edge ed, MathUtility mu, MathTransform xf)
    {
        var sv = ed.GetStartVertex() as Vertex; var ev = ed.GetEndVertex() as Vertex;
        if (sv == null || ev == null) return null;
        var a = (double[])sv.GetPoint(); var b = (double[])ev.GetPoint();
        if (xf != null) { a = Apply(mu, xf, a); b = Apply(mu, xf, b); }
        bool swap = a[0] > b[0] + 1e-12 || (Math.Abs(a[0] - b[0]) <= 1e-12 && (a[1] > b[1] + 1e-12 || (Math.Abs(a[1] - b[1]) <= 1e-12 && a[2] > b[2])));
        return swap ? b.Concat(a).ToArray() : a.Concat(b).ToArray();
    }

    static object PartEntityOf(ISldWorks sw, Component2 rc, object ent, Dictionary<string, List<KeyValuePair<object, object>>> cache)
    {
        var oldDoc = rc.GetModelDoc2() as ModelDoc2;
        if (oldDoc == null) return null;
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
        return list.FirstOrDefault(kv => (want == null || want.IsInstanceOfType(kv.Key)) && sw.IsSame(kv.Value, ent) == (int)swObjectEquality.swObjectSame).Key;
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

    // A face: the box of its edges' end points (exact, unlike Face2.GetBox) moved by xf if given, and its area.
    static double[] FaceGeo(Face2 f, MathUtility mu, MathTransform xf)
    {
        var pts = new List<double[]>();
        foreach (var e in (f.GetEdges() as object[] ?? new object[0]).Cast<Edge>())
            foreach (var v in new[] { e.GetStartVertex() as Vertex, e.GetEndVertex() as Vertex })
                if (v != null) { var q = (double[])v.GetPoint(); pts.Add(xf == null ? q : Apply(mu, xf, q)); }
        if (pts.Count == 0) return null;
        return new[] { pts.Min(q => q[0]), pts.Min(q => q[1]), pts.Min(q => q[2]), pts.Max(q => q[0]), pts.Max(q => q[1]), pts.Max(q => q[2]), f.GetArea() };
    }

    static double[] Geo(object e)
    {
        var f = e as Face2;
        if (f != null) return FaceGeo(f, null, null);
        var ed = e as Edge;
        if (ed != null)
        {
            var sv = ed.GetStartVertex() as Vertex; var ev = ed.GetEndVertex() as Vertex;
            if (sv == null || ev == null) return null;   // a closed edge (full circle): not matched by position
            var a = (double[])sv.GetPoint(); var b = (double[])ev.GetPoint();
            bool swap = a[0] > b[0] || (a[0] == b[0] && (a[1] > b[1] || (a[1] == b[1] && a[2] > b[2])));
            return swap ? b.Concat(a).ToArray() : a.Concat(b).ToArray();
        }
        var v = e as Vertex;
        return v != null ? (double[])v.GetPoint() : null;
    }

    static int SwapAsm(ISldWorks sw, string asmPath, string oldPart, string newAsm, string innerPart, bool save, bool checkedOut)
    {
        // As form-sub: read-only referenced files SOLIDWORKS holds as modified are not prompted or saved (on P:\ every
        // file not checked out is read-only).
        DiscardReadOnlyRefs(sw);
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
        var wrongBefore = WrongNames(doc);
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
                fx.Geo = Geo(hit.Key);
                var face = hit.Key as Face2;
                var body = face != null ? face.GetBody() as Body2 : hit.Key is Edge ? ((Edge)hit.Key).GetBody() as Body2 : null;
                fx.BodyName = body != null ? body.Name : "";
                fx.What = want.Name + (fx.BodyName != "" ? " of body \"" + fx.BodyName + "\"" : "");
            }
        }
        Console.WriteLine("Mate entities on those instances: " + fixes.Count + " in " + fixes.Select(f => f.Mate).Distinct().Count() + " mates");
        foreach (var fx in fixes) Console.WriteLine("  " + fx.Mate + " [" + fx.Index + "] " + fx.OldInst + ": " + fx.What + (fx.Pid == null && fx.FeatName == null ? "  <- NO ID" : ""));
        if (fixes.Any(f => f.Pid == null && f.FeatName == null)) { problems.Add("some mate entities could not be identified in the part; nothing replaced"); return Finish(); }

        // The mates as they are now: type, alignment, flip, distance, and every entity (to add them again after).
        var mateInfo = new Dictionary<string, object[]>(StringComparer.OrdinalIgnoreCase);   // name -> {type, align, flip, dist, entities[]}
        foreach (var name in fixes.Select(f => f.Mate).Distinct())
        {
            var mf = ((AssemblyDoc)doc).FeatureByName(name) as Feature;
            var mate = (Mate2)mf.GetSpecificFeature2();
            double dist = 0;
            var dd = mf.GetFirstDisplayDimension() as DisplayDimension;
            if (dd != null) { var v = ((Dimension)dd.GetDimension2(0)).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null) as double[]; if (v != null && v.Length > 0) dist = v[0]; }
            var ents = Enumerable.Range(0, mate.GetMateEntityCount()).Select(i => mate.MateEntity(i).Reference).ToArray();
            mateInfo[name] = new object[] { mate.Type, mate.Alignment, mate.Flipped, dist, ents };
            Console.WriteLine("  " + name + ": " + FlagName(typeof(swMateType_e), mate.Type) + ", " + FlagName(typeof(swMateAlign_e), mate.Alignment)
                + (mate.Flipped ? ", flipped" : "") + (dd != null ? ", " + Mm(dist) + " mm" : ""));
        }
        var positions = (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>()
            .Where(c => !insts.Any(i => c.Name2 == i.Name2 || c.Name2.StartsWith(i.Name2 + "/")))
            .ToDictionary(c => c.Name2, c => (double[])c.Transform2.ArrayData);

        // Component patterns whose direction is on an old instance: SOLIDWORKS loses that reference in the replace
        // (13 Nov test: a warning on "LocalLPattern1 - STUBS"; its instances stayed where they were). Listed.
        var patternFixes = new List<PatternFix>();
        foreach (var pf in AllFeatures(doc).Where(f => f.GetTypeName2() == "LocalLPattern" || f.GetTypeName2() == "LocalCirPattern"))
        {
            try
            {
                var refs = new List<string>();
                var ld = pf.GetDefinition() as LocalLinearPatternFeatureData;
                var cd = ld == null ? pf.GetDefinition() as LocalCircularPatternFeatureData : null;
                if (ld != null && ld.AccessSelections(doc, null))
                {
                    int axn = 0;
                    foreach (var ax in new[] { ld.D1Axis, ld.D2Axis })
                    {
                        axn++;
                        var en = ax as Entity; var oc = en == null ? null : en.GetComponent() as Component2;
                        if (ax != null && oc == null)
                            // no owner given (B-3T, 13 Nov): it may still be an edge of the old part; ask each old instance
                            foreach (var oi in insts) if (PartEntityOf(sw, oi, ax, cache) != null) { oc = oi; break; }
                        if (ax != null) refs.Add("direction on " + (oc != null ? oc.Name2 : "the assembly") + " (" + (ax is Edge ? "edge" : ax is Face2 ? "face" : ax is Feature ? "feature " + ((Feature)ax).Name : Microsoft.VisualBasic.Information.TypeName(ax)) + ")");
                        if (oc != null && instByName.ContainsKey(oc.Name2))
                        {
                            var pe = PartEntityOf(sw, oc, ax, cache);
                            var od = oc.GetModelDoc2() as ModelDoc2;
                            var peb = pe is Face2 ? ((Face2)pe).GetBody() as Body2 : pe is Edge ? ((Edge)pe).GetBody() as Body2 : null;
                            Console.WriteLine("    direction " + axn + " is " + (pe == null ? "not found in the part" : pe.GetType().Name + (peb != null ? " of body \"" + peb.Name + "\"" : "")));
                            if (pe != null && od != null) patternFixes.Add(new PatternFix { Feature = pf.Name, OldInst = oc.Name2, Axis = axn, Pid = od.Extension.GetPersistReference3(pe), Xf = (double[])oc.Transform2.ArrayData, Geo = Geo(pe) });
                        }
                    }
                    var seeds = (ld.SeedComponentArray as object[] ?? new object[0]).OfType<Component2>().Select(c => c.Name2).ToList();
                    refs.Add("seeds " + string.Join(", ", seeds));
                    if (seeds.Any(n => instByName.ContainsKey(n))) refs.Add("A SEED IS THE OLD PART");
                    ld.ReleaseSelectionAccess();
                }
                else if (cd != null && cd.AccessSelections(doc, null))
                {
                    var en = cd.Axis as Entity; var oc = en == null ? null : en.GetComponent() as Component2;
                    if (cd.Axis != null) refs.Add("axis on " + (oc != null ? oc.Name2 : "the assembly"));
                    cd.ReleaseSelectionAccess();
                }
                bool onOld = refs.Any(r => insts.Any(i => r.EndsWith(" on " + i.Name2)));
                Console.WriteLine("  pattern " + pf.Name + ": " + (refs.Count == 0 ? "no direction read" : string.Join(", ", refs)) + (onOld ? "  <- ON THE OLD PART: Grant to check after" : ""));
                if (onOld) patternNotes.Add(pf.Name + " (direction was on " + Path.GetFileName(oldPart) + ")");
            }
            catch (Exception ex) { Console.WriteLine("  pattern " + pf.Name + ": could not read (" + ex.Message + ")"); }
        }

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
        var mu = (MathUtility)sw.GetMathUtility();

        // 3. The same entity in each new instance (matched to the old one by position). Inside the SFA the parts of the
        // new instance stay lightweight and SOLIDWORKS refuses to resolve them (SW's dry run, 13 Nov: swResolveError),
        // so each entity is found in the new assembly's own document and taken up one level: part -> new assembly
        // (that document's component) -> SFA (the new instance). Nothing here changes a configuration or saves a part.
        var naDoc = FindOpen(sw, newAsm) ?? (news.Count > 0 ? news[0].GetModelDoc2() as ModelDoc2 : null);
        if (naDoc == null) { problems.Add("the new assembly is not loaded: " + newAsm); return Finish(); }
        int lw = ((AssemblyDoc)naDoc).ResolveAllLightWeightComponents(false);
        var naComps = ((((AssemblyDoc)naDoc).GetComponents(true) as object[]) ?? new object[0]).Cast<Component2>().ToList();
        Console.WriteLine("In " + Path.GetFileName(newAsm) + " (" + FlagName(typeof(swComponentResolveStatus_e), lw) + "): "
            + string.Join(", ", naComps.Select(c => c.Name2 + " <" + c.ReferencedConfiguration + "> " + FlagName(typeof(swComponentSuppressionState_e), c.GetSuppression2()).Replace("swComponent", ""))));
        var naInner = naComps.Where(c => string.Equals(c.GetPathName(), innerPart, StringComparison.OrdinalIgnoreCase)).ToList();
        if (naInner.Count != 1) { problems.Add(Path.GetFileName(newAsm) + " has " + naInner.Count + " instances of " + Path.GetFileName(innerPart) + ", not 1"); return Finish(); }
        var innerTop = naInner[0];
        var innerDoc = FindOpen(sw, innerPart) ?? innerTop.GetModelDoc2() as ModelDoc2;
        if (innerDoc == null) { problems.Add(Path.GetFileName(innerPart) + " is not loaded"); return Finish(); }
        int aerr2 = 0;
        sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref aerr2);

        Func<double[], Component2> newAt = xf => news.FirstOrDefault(n =>
        {
            var d = (double[])n.Transform2.ArrayData;
            return Enumerable.Range(0, 12).All(i => Math.Abs(d[i] - xf[i]) < 1e-9);
        });
        // part entity (by ID) -> SFA entity in instance ni
        Func<Component2, object, object> upFromInner = (ni, pid) =>
        {
            int err = 0;
            var pe = innerDoc.Extension.GetObjectByPersistReference3(pid, out err);
            if (pe == null) return null;
            var e1 = innerTop.GetCorrespondingEntity(pe);
            return e1 == null ? null : ni.GetCorrespondingEntity(e1);
        };
        // Looked up twice: once here to report, and again just before its mate is added, because adding a mate rebuilds
        // the SFA and the face objects found before go stale (SW's copy, 13 Nov: "object disconnected" in B-3T, C-2T, C-3T).
        Func<MateFix, bool, object> locate = (fx, say) =>
        {
            var ni = newAt(fx.Xf);
            if (ni == null) { if (say) problems.Add(fx.Mate + ": no new instance where " + fx.OldInst + " was"); return null; }
            object ne = null; string how = "";
            if (fx.FeatName != null)
            {
                var f1 = innerTop.FeatureByName(fx.FeatName);
                ne = f1 == null ? null : ni.GetCorresponding(f1);
                how = "by name";
            }
            else
            {
                ne = upFromInner(ni, fx.Pid);
                how = "by ID in " + Path.GetFileName(innerPart);
                if (ne == null && fx.Geo != null && fx.Geo.Length == 7)
                {
                    // A plate face: the face of a plate component of the new assembly in the same place (its coordinates
                    // are the old part's), same corner box and area. Faces from the component's own bodies (its
                    // configuration); their points are read as they come and moved by the component's transform: a face
                    // counts only if exactly one fits.
                    var hits = new List<KeyValuePair<Component2, object>>();
                    double nearD = double.MaxValue; string near = "none";
                    // The faces come from the new instance's children in the SFA (selectable there, even lightweight);
                    // their points are part coordinates, moved by the same component's transform in the new assembly.
                    foreach (var kid in ((ni.GetChildren() as object[]) ?? new object[0]).Cast<Component2>())
                    {
                        var pc = naComps.FirstOrDefault(c => kid.Name2.EndsWith("/" + c.Name2, StringComparison.OrdinalIgnoreCase));
                        if (pc == null) { if (say) Console.WriteLine("    (" + kid.Name2 + ": not matched in " + Path.GetFileName(newAsm) + ")"); continue; }
                        object info;
                        var bodies = (kid.GetBodies3((int)swBodyType_e.swSolidBody, out info) as object[] ?? new object[0]).Cast<Body2>().ToList();
                        foreach (var b in bodies)
                            foreach (var f in (b.GetFaces() as object[] ?? new object[0]).Cast<Face2>())
                            {
                                double da = Math.Abs(f.GetArea() - fx.Geo[6]);
                                if (da > 1e-9) continue;   // area first: the positions take many SOLIDWORKS calls (13 Nov: ~1 min a mate)
                                bool fits = false;
                                foreach (var rel in new MathTransform[] { null, (MathTransform)pc.Transform2 })   // each face counted once
                                {
                                    var g = FaceGeo(f, mu, rel);
                                    if (g == null) continue;
                                    double dg = Enumerable.Range(0, 6).Max(a => Math.Abs(g[a] - fx.Geo[a]));
                                    if (da + dg < nearD) { nearD = da + dg; near = pc.Name2 + (rel == null ? " (as read)" : " (moved)") + " area " + (f.GetArea() * 1e6).ToString("0.###", Inv) + " box " + P3(g.Take(3).ToArray()) + "-" + P3(g.Skip(3).Take(3).ToArray()); }
                                    if (da <= 1e-10 && dg < 1e-8) fits = true;
                                }
                                if (fits) hits.Add(new KeyValuePair<Component2, object>(kid, f));
                            }
                    }
                    how = "by position (" + hits.Count + " match" + (hits.Count == 1 ? ": " + hits[0].Key.Name2 : "") + ")";
                    if (hits.Count == 1)
                    {
                        var f = hits[0].Value;
                        var en = f as Entity;
                        var owner = en == null ? null : en.GetComponent() as Component2;
                        ne = owner != null ? f : hits[0].Key.GetCorrespondingEntity(f);
                        how += owner != null ? "; face of " + owner.Name2 : "";
                        if (ne == null) how += "; no entity in the SFA";
                    }
                    else if (say) Console.WriteLine("    looked for area " + (fx.Geo[6] * 1e6).ToString("0.###", Inv) + " mm2 box " + P3(fx.Geo.Take(3).ToArray()) + "-" + P3(fx.Geo.Skip(3).Take(3).ToArray()) + "; nearest: " + near);
                }
            }
            if (!say) return ne;
            Console.WriteLine("  " + fx.Mate + " [" + fx.Index + "]: " + fx.What + " -> " + (ne != null ? "found " + how : "NOT FOUND " + how));
            if (ne == null)
            {
                if (acceptGone.Contains(fx.Mate, StringComparer.OrdinalIgnoreCase)) Console.WriteLine("  " + fx.Mate + ": not put back; --accept-gone, so it is dropped below");
                else problems.Add(fx.Mate + " [" + fx.Index + "]: " + fx.What + " not found in the new assembly: not put back (--accept-gone " + fx.Mate + " to drop it)");
            }
            return ne;
        };
        foreach (var fx in fixes) fx.New = locate(fx, true);

        // 4. Each mate again with the new entity: same type, alignment, flip and distance; the broken one deleted and the
        // new one given its name. (Changing the mate's entities in place crashed SOLIDWORKS, 13 Nov test.)
        var simple = new HashSet<int> { (int)swMateType_e.swMateCOINCIDENT, (int)swMateType_e.swMatePARALLEL, (int)swMateType_e.swMatePERPENDICULAR,
                                        (int)swMateType_e.swMateCONCENTRIC, (int)swMateType_e.swMateDISTANCE };
        var sel = (SelectionMgr)doc.SelectionManager;
        foreach (var grp in fixes.GroupBy(f => f.Mate))
        {
            if (grp.Any(f => f.New == null)) continue;
            var info = mateInfo[grp.Key];
            int type = (int)info[0], align = (int)info[1]; bool flip = (bool)info[2]; double dist = (double)info[3];
            var ents = ((object[])info[4]).ToArray();
            if (!simple.Contains(type) || ents.Length != 2) { problems.Add(grp.Key + ": " + FlagName(typeof(swMateType_e), type) + " with " + ents.Length + " entities: put back by hand"); continue; }
            // The other entity from the mate as it is now, and the new one found again (see locate).
            var cur = ((AssemblyDoc)doc).FeatureByName(grp.Key) as Feature;
            var curMate = cur == null ? null : cur.GetSpecificFeature2() as Mate2;
            if (curMate != null && curMate.GetMateEntityCount() == ents.Length)
                for (int j = 0; j < ents.Length; j++)
                {
                    if (grp.Any(f => f.Index == j)) continue;
                    var r = curMate.MateEntity(j).Reference;
                    if (r != null) ents[j] = r;
                }
            bool fresh = true;
            foreach (var fx in grp) { var nf = locate(fx, false); if (nf == null) fresh = false; else ents[fx.Index] = nf; }
            if (!fresh) { problems.Add(grp.Key + ": its new entity could not be found again just before adding the mate"); continue; }
            var namesBefore = new HashSet<string>(TopMates(doc).Select(m => m.Name));
            doc.ClearSelection2(true);
            bool selOk = true;
            foreach (var e in ents)
            {
                var sd = sel.CreateSelectData(); sd.Mark = 1;
                var en = e as Entity; var fe = e as Feature;
                bool one = en != null ? en.Select4(true, sd) : fe != null && fe.Select2(true, 1);
                if (!one) selOk = false;
            }
            if (!selOk) { problems.Add(grp.Key + ": could not select its entities"); doc.ClearSelection2(true); continue; }
            int merr;
            var added = asm.AddMate5(type, align, flip, dist, dist, dist, 0, 0, 0, 0, 0, false, false, 0, out merr);
            doc.ClearSelection2(true);
            var nm = TopMates(doc).FirstOrDefault(m => !namesBefore.Contains(m.Name));
            if (added == null || nm == null) { problems.Add(grp.Key + ": SOLIDWORKS did not add the mate (" + FlagName(typeof(swAddMateError_e), merr) + ")"); continue; }
            var old = ((AssemblyDoc)doc).FeatureByName(grp.Key) as Feature;
            if (old != null && old.Select2(false, 0)) doc.Extension.DeleteSelection2(0);
            doc.ClearSelection2(true);
            string addedName = nm.Name;
            nm.Name = grp.Key;
            Console.WriteLine("  " + grp.Key + ": added again on the new assembly (" + addedName + " renamed " + nm.Name + "), the broken one deleted");
            if (nm.Name != grp.Key) problems.Add(grp.Key + ": the new mate is called " + nm.Name);
            foreach (var fx in grp) fx.Done = true;
        }
        // Pattern directions that were on the old part: the same edge or face of the weldment-only part.
        foreach (var px in patternFixes)
        {
            var ni = newAt(px.Xf);
            int perr = 0;
            var pe = innerDoc.Extension.GetObjectByPersistReference3(px.Pid, out perr);
            var ne = ni == null ? null : upFromInner(ni, px.Pid);
            if (ne == null && ni != null && px.Geo != null && px.Geo.Length == 6)
            {   // an edge (of a plate): the one edge of the new instance's children in the same place
                var eh = new List<object>();
                foreach (var kid in ((ni.GetChildren() as object[]) ?? new object[0]).Cast<Component2>())
                {
                    var pc = naComps.FirstOrDefault(c => kid.Name2.EndsWith("/" + c.Name2, StringComparison.OrdinalIgnoreCase));
                    if (pc == null) continue;
                    object info;
                    foreach (var b in (kid.GetBodies3((int)swBodyType_e.swSolidBody, out info) as object[] ?? new object[0]).Cast<Body2>())
                        foreach (var ed in (b.GetEdges() as object[] ?? new object[0]).Cast<Edge>())
                            if (new MathTransform[] { null, (MathTransform)pc.Transform2 }.Any(rel =>
                                { var g = EdgeGeo(ed, mu, rel); return g != null && Enumerable.Range(0, 6).All(k => Math.Abs(g[k] - px.Geo[k]) < 1e-8); }))
                                eh.Add(ed);
                }
                Console.WriteLine("  pattern " + px.Feature + ": direction edge by position: " + eh.Count + " match");
                if (eh.Count == 1) ne = eh[0];
            }
            var pf = ((AssemblyDoc)doc).FeatureByName(px.Feature) as Feature;
            var ld = pf == null ? null : pf.GetDefinition() as LocalLinearPatternFeatureData;
            if (ne == null || ld == null || !ld.AccessSelections(doc, null)) { Console.WriteLine("  pattern " + px.Feature + ": direction not found again (" + (pe == null ? "not in " + Path.GetFileName(innerPart) + ", ID " + FlagName(typeof(swPersistReferencedObjectStates_e), perr) + ": on a plate?" : ne == null ? "no assembly entity" : "pattern not readable") + "), left for Grant's check"); continue; }
            if (px.Axis == 1) ld.D1Axis = ne; else ld.D2Axis = ne;
            bool pok = pf.ModifyDefinition(ld, doc, null);
            if (!pok) ld.ReleaseSelectionAccess();
            // The same edge can point the other way (C-2T, 13 Nov: the two stub copies jumped across, 30 components).
            // If anything moved, reverse that direction once; if still moved, the check below stops the save.
            Func<int> movedNow = () =>
            {
                doc.ForceRebuild3(false);
                return (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>().Count(c =>
                {
                    double[] was;
                    if (!positions.TryGetValue(c.Name2, out was)) return false;
                    var d = (double[])c.Transform2.ArrayData;
                    return Enumerable.Range(0, 12).Any(i => Math.Abs(d[i] - was[i]) > 1e-8);
                });
            };
            if (pok)
            {
                int mv = movedNow();
                if (mv > 0)
                {
                    var ld2 = pf.GetDefinition() as LocalLinearPatternFeatureData;
                    if (ld2 != null && ld2.AccessSelections(doc, null))
                    {
                        if (px.Axis == 1) ld2.D1ReverseDirection = !ld2.D1ReverseDirection; else ld2.D2ReverseDirection = !ld2.D2ReverseDirection;
                        bool rok = pf.ModifyDefinition(ld2, doc, null);
                        if (!rok) ld2.ReleaseSelectionAccess();
                        int mv2 = movedNow();
                        Console.WriteLine("  pattern " + px.Feature + ": " + mv + " components moved, direction reversed: " + (rok ? mv2 + " moved now" : "SOLIDWORKS refused"));
                    }
                }
            }
            Console.WriteLine("  pattern " + px.Feature + ": direction " + px.Axis + (pok ? " put back on the new assembly" : " NOT put back (SOLIDWORKS refused), left for Grant's check"));
            if (pok) patternNotes.RemoveAll(n => n.StartsWith(px.Feature + " ("));
        }

        // Mates named with --accept-gone that could not be put back are deleted and listed (brief: Grant looks at them).
        var dropped = new List<string>();
        foreach (var name in acceptGone)
        {
            var mine = fixes.Where(f => string.Equals(f.Mate, name, StringComparison.OrdinalIgnoreCase)).ToList();
            if (mine.Count == 0) { problems.Add("--accept-gone " + name + ": not a mate on " + Path.GetFileName(oldPart)); continue; }
            if (mine.All(f => f.Done)) { problems.Add("--accept-gone " + name + ": it was put back, nothing dropped"); continue; }
            var mf = ((AssemblyDoc)doc).FeatureByName(name) as Feature;
            doc.ClearSelection2(true);
            if (mf != null && mf.Select2(false, 0) && doc.Extension.DeleteSelection2(0)) { dropped.Add(name); Console.WriteLine("  DROPPED " + name + " (" + string.Join("; ", mine.Select(f => f.What)) + ")"); }
            else problems.Add("could not delete " + name);
            doc.ClearSelection2(true);
        }
        doc.ForceRebuild3(false);

        // 5. Nothing else moved, and the new instances sit where the old ones did.
        int moved = 0;
        foreach (var c in (asm.GetComponents(false) as object[] ?? new object[0]).Cast<Component2>())
        {
            double[] was;
            if (!positions.TryGetValue(c.Name2, out was)) continue;
            var d = (double[])c.Transform2.ArrayData;
            if (Enumerable.Range(0, 12).Any(i => Math.Abs(d[i] - was[i]) > 1e-8)) { moved++; problems.Add("moved: " + c.Name2); }
        }
        Console.WriteLine("Other components: " + positions.Count + ", moved: " + moved);
        foreach (var n in news) Console.WriteLine("  new instance " + n.Name2 + (n.IsFixed() ? ", fixed" : ", floating"));
        int wrong = doc.Extension.GetWhatsWrongCount();
        Console.WriteLine("Mates put back: " + fixes.Select(f => f.Mate).Distinct().Count(m => fixes.Where(f => f.Mate == m).All(f => f.Done)) + " of "
            + fixes.Select(f => f.Mate).Distinct().Count() + "; dropped: " + (dropped.Count == 0 ? "none" : string.Join(", ", dropped)) + "; items wrong: " + errorsBefore + " before, " + wrong + " now");
        ReportWhatsWrong(doc);
        // Any pattern with a new warning after the swap is listed too (B-3T, 13 Nov: a direction SOLIDWORKS gives no
        // owner or type for; the pattern's instances did not move).
        foreach (var w in WrongNames(doc).Where(w => !wrongBefore.Contains(w) && w.Contains("Pattern")))
        {
            string pn = w.Split('	')[0];
            if (!patternNotes.Any(n => n.StartsWith(pn + " ("))) patternNotes.Add(pn + " (new warning after the swap; its instances did not move)");
        }
        foreach (var pn in patternNotes) Console.WriteLine("FOR GRANT'S CHECK: pattern " + pn);
        foreach (var dn in dropped) Console.WriteLine("FOR GRANT'S CHECK: mate dropped " + dn);
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
        nc.Description = string.Equals(fromC.Description, from, StringComparison.OrdinalIgnoreCase) ? newCfg : fromC.Description;   // not the from name (SW, 13 Nov)
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
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
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

    // After a refused save: a file this command opened is closed again unsaved, so a rerun starts from the file on disk
    // (SW's dry run, 13 Nov: a refused delete-feature left SFP-05 open with the features already gone in memory).
    static void DiscardIfOpened(ISldWorks sw, ModelDoc2 doc, bool wasOpen)
    {
        if (wasOpen) { Console.Error.WriteLine("  It was open before this command, so it stays open WITH the unsaved change: close it before running again."); return; }
        sw.CloseDoc(doc.GetTitle());
        Console.Error.WriteLine("  Closed it again without saving: the file on disk is unchanged.");
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
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
        if (!SaveDoc(sw, doc, checkedOut)) return 2;
        return Finish();
    }
}
