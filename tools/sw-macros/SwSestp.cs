// SwSestp: square-edge STEP (SESTP) sandbox tools (Grant, SW window, 22 Nov 2026). Works only on files under
// C:\PDM-work\sestp\ (refuses anything else, so never P:\ or Dropbox). Opens and closes only its own documents.
// Commands:
//   count  <part>                          read-only: per configuration, corner faces found by the 21 Nov rule and by the
//                                          new rule, and the other cylindrical faces (holes)
//   square <part>                          one Delete Face (delete and patch) feature per tube body on its corner faces (new
//                                          rule), on in every configuration; checks every configuration: 0 corner faces
//                                          left, holes unchanged, 0 rebuild errors; saves only if all is well
//   xforms <asm> <config> <out.txt>        read-only: every active component and its position in the top
//   export <asm> <config> <outDir> <name>  read-only: <name>_STR.STEP (everything except the DL_APEX tiles) and
//                                          <name>_LED.STEP (the DL_APEX tiles only), both from the same top so they overlay,
//                                          plus <name>_LED.txt (the tiles' envelope and the LED face) and the positions file
// Corner faces (new rule): in a body made by a Structural Member feature, a cylindrical face of radius 20 mm or less with
// two straight edges, each 20 mm or longer and along the axis, a quarter turn apart (a tube corner or an angle's root
// or toe). The 21 Nov rule had no body test and needed only one straight edge, so a hole through a plate 20 mm or
// thicker could pass it.
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwSestp.exe SwSestp.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwSestp
{
    const string Root = @"C:\PDM-work\sestp\";
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static ISldWorks sw;
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }

    static ModelDoc2 Open(string file, string config, bool write)
    {
        int e = 0, w = 0;
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        var t0 = DateTime.Now;
        var doc = sw.OpenDoc6(file, type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight | (int)swOpenDocOptions_e.swOpenDocOptions_LoadExternalReferencesInMemory
            | (write ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), config ?? "", ref e, ref w);
        if (doc == null) throw new Exception("could not open " + file + ", error " + e);
        if (!string.Equals(doc.GetPathName(), file, StringComparison.OrdinalIgnoreCase))
        { sw.CloseDoc(doc.GetTitle()); throw new Exception("SOLIDWORKS gave " + doc.GetPathName() + " for " + file + " (a document of the same name is already open)"); }
        Console.WriteLine("OPENED\t" + file + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name + "\terrors " + e + " warnings " + w + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
        return doc;
    }

    static int Errors(ModelDoc2 doc, bool list)
    {
        object f, c, w; doc.Extension.GetWhatsWrong(out f, out c, out w);
        var ef = f as object[]; var wf = w as bool[]; int n = 0;
        if (ef != null)
            for (int i = 0; i < ef.Length; i++)
            {
                bool warn = wf != null && i < wf.Length && wf[i];
                if (!warn) n++;
                if (list && i < 40) Console.WriteLine("  " + (warn ? "warning" : "error") + "\t" + ((Feature)ef[i]).Name);
            }
        return n;
    }

    // ---------- corner faces ----------
    static bool IsMemberBody(Body2 b)
    {
        foreach (Feature f in (object[])b.GetFeatures() ?? new object[0])
            if (f.GetTypeName2() == "WeldMemberFeat") return true;
        return false;
    }

    static readonly Dictionary<string, bool> tubeFile = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

    // a body brought in by Insert Part from a part that has structural members (22 Nov: 08X06-SFW-04 inserts 08X06-SFP-03,
    // and the inserted body does not follow the squared 08X06-SFP-03, so it is squared where it is)
    static bool IsInsertedTube(Body2 b)
    {
        foreach (Feature f in (object[])b.GetFeatures() ?? new object[0])
        {
            if (f.GetTypeName2() != "Stock") continue;
            object models, feats, data, status, ents, comps, extra; int cfgOpt; string cfgName;
            f.ListExternalFileReferences2(out models, out feats, out data, out status, out ents, out comps, out extra, out cfgOpt, out cfgName);
            foreach (var m in models as string[] ?? new string[0])
            {
                bool t;
                if (!tubeFile.TryGetValue(m, out t))
                {
                    // only when SOLIDWORKS already has the source loaded; never opened here (22 Nov test: opening it
                    // from its stored path brought a second copy of the inserted body into the part)
                    var src = sw.GetOpenDocumentByName(m) as ModelDoc2;
                    t = false;
                    if (src != null)
                        for (var g = (Feature)src.FirstFeature(); g != null && !t; g = (Feature)g.GetNextFeature())
                            if (g.GetTypeName2() == "WeldMemberFeat") t = true;
                    tubeFile[m] = t;
                    Console.WriteLine("INSERTED PART\t" + f.Name + "\t" + m + "\t" + (src == null ? "not loaded: left as it is" : t ? "has tube members" : "no tube members"));
                }
                if (t) return true;
            }
        }
        return false;
    }

    static bool Squarable(Body2 b) { return IsMemberBody(b) || IsInsertedTube(b); }

    static int BodyCount(ModelDoc2 doc) { return ((object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]).Length; }

    // In the configuration shown, each SQUARE-CORNERS feature made so far is tried on, one at a time, and kept on only if it
    // rebuilds without error and the body count stays what the configuration had before squaring (22 Nov, 08X06-SFW-04: a
    // feature made for a body of another configuration rebuilt without error here but added a second copy of a body).
    static void Settle(ModelDoc2 doc, List<string> names, int bodies)
    {
        string cfg = ((Configuration)doc.GetActiveConfiguration()).Name;
        foreach (var n in names)
        {
            var f = (Feature)((PartDoc)doc).FeatureByName(n);
            // never switch it on where a parent is off: SOLIDWORKS would switch the parent on too (22 Nov, 08X06-SFW-04:
            // the tube member used only in other configurations came back as an extra body)
            var offParent = ((object[])f.GetParents() ?? new object[0]).Cast<Feature>()
                .FirstOrDefault(p => { var s = p.IsSuppressed2((int)swInConfigurationOpts_e.swThisConfiguration, null) as bool[]; return s != null && s.Length > 0 && s[0]; });
            if (offParent != null)
            {
                f.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                Console.WriteLine("OFF IN\t" + cfg + "\t" + n + "\t(" + offParent.Name + " is off here)");
                continue;
            }
            f.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
            doc.ForceRebuild3(false);
            bool warn; int code = f.GetErrorCode2(out warn);
            int nb = BodyCount(doc);
            if ((code != 0 && !warn) || nb != bodies)
            {
                f.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                doc.ForceRebuild3(false);
                Console.WriteLine("OFF IN\t" + cfg + "\t" + n + "\t(" + (code != 0 && !warn ? "error " + code : "bodies " + bodies + " -> " + nb) + ": its body is not in this configuration)");
            }
        }
    }

    // 0 = not a corner, 1 = 21 Nov rule only, 2 = both rules
    static int Corner(Face2 f)
    {
        var s = (Surface)f.GetSurface();
        if (!s.IsCylinder()) return 0;
        var cp = (double[])s.CylinderParams;   // origin xyz, axis xyz, radius
        if (cp[6] > 0.020) return 0;
        double al = Math.Sqrt(cp[3] * cp[3] + cp[4] * cp[4] + cp[5] * cp[5]);
        double[] ax = { cp[3] / al, cp[4] / al, cp[5] / al };
        var radial = new List<double[]>();
        foreach (Edge e in (object[])f.GetEdges() ?? new object[0])
        {
            if (!((Curve)e.GetCurve()).IsLine()) continue;
            var cps = e.GetCurveParams3();
            var a = (double[])cps.StartPoint; var b = (double[])cps.EndPoint;
            double dx = b[0] - a[0], dy = b[1] - a[1], dz = b[2] - a[2], len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 0.020) continue;
            if (Math.Abs((dx * ax[0] + dy * ax[1] + dz * ax[2]) / len) < 0.999) continue;
            double[] m = { (a[0] + b[0]) / 2 - cp[0], (a[1] + b[1]) / 2 - cp[1], (a[2] + b[2]) / 2 - cp[2] };
            double along = m[0] * ax[0] + m[1] * ax[1] + m[2] * ax[2];
            double[] r = { m[0] - along * ax[0], m[1] - along * ax[1], m[2] - along * ax[2] };
            double rl = Math.Sqrt(r[0] * r[0] + r[1] * r[1] + r[2] * r[2]);
            if (rl > 1e-9) radial.Add(new[] { r[0] / rl, r[1] / rl, r[2] / rl });
        }
        if (radial.Count == 0) return 0;
        for (int i = 0; i < radial.Count; i++)
            for (int j = i + 1; j < radial.Count; j++)
            {
                double deg = Math.Acos(Math.Max(-1, Math.Min(1, radial[i][0] * radial[j][0] + radial[i][1] * radial[j][1] + radial[i][2] * radial[j][2]))) * 180 / Math.PI;
                if (deg > 80 && deg < 100) return 2;
            }
        return 1;
    }

    // old (21 Nov rule, any body), corners (new rule), other cylindrical faces, member bodies, bodies
    static int[] Count(ModelDoc2 doc)
    {
        int old = 0, neu = 0, cyl = 0, mem = 0, nb = 0;
        foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
        {
            nb++;
            bool member = Squarable(b);
            if (member) mem++;
            foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
            {
                int k = Corner(f);
                if (k >= 1) old++;
                if (k == 2 && member) neu++;
                else if (((Surface)f.GetSurface()).IsCylinder()) cyl++;
            }
        }
        return new[] { old, neu, cyl, mem, nb };
    }

    static string Volumes(ModelDoc2 doc)
    {
        var vols = new List<string>();
        foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
        {
            var mp = b.GetMassProperties(1) as double[];
            vols.Add(mp == null ? "?" : (mp[3] * 1e9).ToString("0.0", Inv));
        }
        vols.Sort(StringComparer.Ordinal);
        return vols.Count + " bodies (mm3 " + string.Join(", ", vols) + ")";
    }

    static int CountCmd(ModelDoc2 doc)
    {
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        int rc = 0;
        foreach (string c in (string[])doc.GetConfigurationNames())
        {
            doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
            var n = Count(doc);
            Console.WriteLine("COUNT\t" + c + "\t" + n[4] + " bodies, " + n[3] + " tube bodies\tcorner faces: 21 Nov rule " + n[0] + ", new rule " + n[1] + "\tother cylindrical faces " + n[2]);
            if (n[0] != n[1])
            {
                Console.WriteLine("DIFFERENT\t" + c + ": the two rules disagree"); rc = 1;
                foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                {
                    int o = 0, q = 0;
                    foreach (Face2 f in (object[])b.GetFaces() ?? new object[0]) { int k = Corner(f); if (k >= 1) o++; if (k == 2) q++; }
                    Console.WriteLine("  BODY\t" + b.Name + "\tsquarable " + Squarable(b) + "\t21 Nov rule " + o + ", quarter-round " + q + "\tfeatures: "
                        + string.Join(", ", ((object[])b.GetFeatures() ?? new object[0]).Cast<Feature>().Select(f => f.Name + " (" + f.GetTypeName2() + ")")));
                }
            }
        }
        doc.ShowConfiguration2(active);
        return rc;
    }

    static int SquareCmd(ModelDoc2 doc)
    {
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        var cfgs = (string[])doc.GetConfigurationNames();
        var holesBefore = new Dictionary<string, int>();
        var bodiesBefore = new Dictionary<string, int>();
        foreach (var c in cfgs)
        {
            doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
            var n = Count(doc); holesBefore[c] = n[2]; bodiesBefore[c] = n[4];
            Console.WriteLine("BEFORE\t" + c + "\t" + Volumes(doc) + "\tcorner faces " + n[1] + " (21 Nov rule " + n[0] + ")\tother cylindrical faces " + n[2] + "\terrors " + Errors(doc, false));
        }
        // Configuration by configuration (22 Nov: in 08X06-SFW-04 one tube body exists only in some configurations): in each,
        // the features made so far are settled first (on where they rebuild, off where their body is not there), then the
        // corner faces still left get one Delete Face feature per body.
        var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
        var madeNames = new List<string>();
        int made = 0, failed = 0;
        for (int ci = 0; ci < cfgs.Length; ci++)
        {
            doc.ShowConfiguration2(cfgs[ci]); doc.ForceRebuild3(false);
            Settle(doc, madeNames, bodiesBefore[cfgs[ci]]);
            int bodyNo = 0;
            foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
            {
                bodyNo++;
                if (!Squarable(b)) continue;
                var picks = ((object[])b.GetFaces() ?? new object[0]).Cast<Face2>().Where(f => Corner(f) == 2).ToList();
                if (picks.Count == 0) continue;
                string bname = b.Name;
                doc.ClearSelection2(true);
                // the Delete Face features there before, so the new one is found wherever it lands in the tree (25 Nov:
                // 08X11-DBW-01 had a feature after it, so "the last feature" was not the new one)
                var before = new HashSet<string>();
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) if (f.GetTypeName2() == "DelFace") before.Add(f.Name);
                int sel = picks.Count(f => ((Entity)f).Select4(true, sd));
                bool one = sel == picks.Count && doc.Extension.InsertDeleteFace((int)swFaceDeleteOption_e.swFaceDelete_Patch);
                Feature last = null;
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) if (f.GetTypeName2() == "DelFace" && !before.Contains(f.Name)) last = f;
                if (one && last != null && last.GetTypeName2() == "DelFace")
                {
                    last.Name = "SQUARE-CORNERS " + (ci + 1) + "-" + bodyNo + " " + bname;
                    madeNames.Add(last.Name); made++;
                }
                else failed++;
                Console.WriteLine("DELETEFACE\t" + cfgs[ci] + "\tbody " + bname + "\t" + sel + " of " + picks.Count + " faces\t" + (one ? "OK" : "FAILED"));
                doc.ClearSelection2(true);
            }
        }
        Console.WriteLine("DELETEFACE\t" + made + " features made, " + failed + " failed");
        bool good = failed == 0;
        foreach (var c in cfgs)
        {
            doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
            Settle(doc, madeNames, bodiesBefore[c]);
            var n = Count(doc); int er = Errors(doc, false);
            Console.WriteLine("AFTER\t" + c + "\t" + Volumes(doc) + "\tcorner faces " + n[1] + "\tother cylindrical faces " + n[2] + "\terrors " + er);
            if (er > 0) { good = false; Console.WriteLine("PROBLEM\t" + c + ": rebuild errors"); }
            if (n[1] > 0) { good = false; Console.WriteLine("PROBLEM\t" + c + ": " + n[1] + " corner faces left"); }
            if (n[4] != bodiesBefore[c]) { good = false; Console.WriteLine("PROBLEM	" + c + ": bodies " + bodiesBefore[c] + " -> " + n[4]); }
            if (n[2] != holesBefore[c]) { good = false; Console.WriteLine("PROBLEM\t" + c + ": other cylindrical faces " + holesBefore[c] + " -> " + n[2]); }
        }
        doc.ShowConfiguration2(active);
        if (!good) { Console.WriteLine("NOT SAVED\tproblems above"); return 1; }
        if (made == 0) { Console.WriteLine("NOTHING TO DO\tno corner faces; file left as it was"); return 0; }
        int e = 0, w = 0;
        bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
        Console.WriteLine(ok ? "SAVED\t" + doc.GetPathName() : "SAVE FAILED\t" + e);
        return ok ? 0 : 1;
    }

    // ---------- assembly ----------
    static List<Component2> Active(AssemblyDoc asm)
    {
        return ((object[])asm.GetComponents(false) ?? new object[0]).Cast<Component2>()
            .Where(k => k.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed).ToList();
    }

    static string Pos(Component2 k)
    {
        var t = k.Transform2;
        if (t == null) return "-";
        var d = (double[])t.ArrayData;
        return string.Join(" ", d.Take(9).Select(v => Math.Round(v, 5).ToString("0.#####", Inv))) + " | " + Mm(d[9]) + " " + Mm(d[10]) + " " + Mm(d[11]);
    }

    static Dictionary<string, string> Positions(AssemblyDoc asm, string outFile)
    {
        var map = new Dictionary<string, string>();
        var lines = new List<string>();
        foreach (var k in Active(asm))
        {
            string p = Pos(k);
            map[k.Name2] = p;
            lines.Add("COMP\t" + k.Name2 + "\t" + Path.GetFileName(k.GetPathName()) + "\t<" + k.ReferencedConfiguration + ">\t" + p);
        }
        lines.Sort(StringComparer.Ordinal);
        if (outFile != null) File.WriteAllLines(outFile, lines);
        Console.WriteLine("POSITIONS\t" + lines.Count + " active components" + (outFile != null ? "\t" + outFile : ""));
        return map;
    }

    static bool IsTile(Component2 k) { return Path.GetFileName(k.GetPathName() ?? "").StartsWith("DL_APEX", StringComparison.OrdinalIgnoreCase); }

    static bool HasTile(Component2 k)
    {
        if (IsTile(k)) return true;
        foreach (Component2 c in (object[])k.GetChildren() ?? new object[0])
            if (c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && HasTile(c)) return true;
        return false;
    }

    // components to hide for the LED file: every component with no tile in it, as high up the tree as possible
    static void NonTileRoots(Component2 parent, List<Component2> outList)
    {
        foreach (Component2 c in (object[])parent.GetChildren() ?? new object[0])
        {
            if (c.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed) continue;
            if (IsTile(c)) continue;
            if (HasTile(c)) NonTileRoots(c, outList); else outList.Add(c);
        }
    }

    static int SetHidden(List<Component2> comps, bool hidden)
    {
        int n = 0;
        foreach (var c in comps)
        {
            c.Visible = hidden ? (int)swComponentVisibilityState_e.swComponentHidden : (int)swComponentVisibilityState_e.swComponentVisible;
            if (c.Visible == (hidden ? (int)swComponentVisibilityState_e.swComponentHidden : (int)swComponentVisibilityState_e.swComponentVisible)) n++;
        }
        return n;
    }

    // product names in a STEP file, with counts
    static Dictionary<string, int> Products(string step)
    {
        var d = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (Match m in Regex.Matches(File.ReadAllText(step), @"PRODUCT\s*\(\s*'((?:[^']|'')*)'"))
        {
            string n = m.Groups[1].Value;
            d[n] = d.ContainsKey(n) ? d[n] + 1 : 1;
        }
        return d;
    }

    static bool Step(ModelDoc2 doc, string outFile)
    {
        int e = 0, w = 0;
        var t0 = DateTime.Now;
        bool ok = doc.Extension.SaveAs3(outFile, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref e, ref w);
        ok = ok && File.Exists(outFile);
        Console.WriteLine((ok ? "SAVED\t" + outFile + "\t" + new FileInfo(outFile).Length / 1024 + " KB" : "FAILED\t" + outFile + "\terrors " + e) + "\twarnings " + w + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
        return ok;
    }

    static double[] Xf(MathUtility mu, MathTransform t, double x, double y, double z)
    {
        var p = (MathPoint)mu.CreatePoint(new[] { x, y, z });
        return (double[])((MathPoint)p.MultiplyTransform(t)).ArrayData;
    }

    // APEX-A49 corner of one face: bottom-left seen from outside it (Y up); null if the face is not vertical or not square to the axes
    static double[] LedCorner(string facing, double[] lo, double[] hi)
    {
        var n = facing.Split(' ').Select(s => double.Parse(s, Inv)).ToArray();
        if (Math.Abs(n[1]) > 0.001) return null;
        double[] up = { 0, 1, 0 }, fwd = { -n[0], -n[1], -n[2] };
        double[] right = { fwd[1] * up[2] - fwd[2] * up[1], fwd[2] * up[0] - fwd[0] * up[2], fwd[0] * up[1] - fwd[1] * up[0] };
        var c = new double[3];
        for (int j = 0; j < 3; j++)
        {
            if (Math.Abs(n[j]) > 0.999) c[j] = n[j] > 0 ? hi[j] : lo[j];
            else if (Math.Abs(up[j]) > 0.999) c[j] = lo[j];
            else if (Math.Abs(right[j]) > 0.999) c[j] = right[j] > 0 ? lo[j] : hi[j];
            else return null;
        }
        return c;
    }

    static string FacingName(string facing)
    {
        var n = facing.Split(' ').Select(s => double.Parse(s, Inv)).ToArray();
        for (int j = 0; j < 3; j++) if (Math.Abs(n[j]) > 0.999) return (n[j] > 0 ? "+" : "-") + "XYZ"[j] + (j == 1 ? (n[j] > 0 ? " (up)" : " (down)") : "") + " (" + facing + ")";
        return "(" + facing + ")";
    }

    static void Leaves(Component2 k, List<Component2> outList)
    {
        if (k.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed || IsTile(k)) return;
        var ch = (object[])k.GetChildren() ?? new object[0];
        if (ch.Length == 0) outList.Add(k); else foreach (Component2 c in ch) Leaves(c, outList);
    }

    // the tiles' envelope in the top's axes, and the tile part's two big faces (the LED face is one of them)
    static void LedReport(AssemblyDoc asm, List<Component2> tiles, List<Component2> steelRoots, string outTxt)
    {
        var mu = (MathUtility)sw.GetMathUtility();
        var lines = new List<string>();
        double[] lo = { double.MaxValue, double.MaxValue, double.MaxValue }, hi = { double.MinValue, double.MinValue, double.MinValue };
        var normals = new Dictionary<string, int>();
        var glo = new Dictionary<string, double[]>(); var ghi = new Dictionary<string, double[]>();   // envelope per facing
        bool firstDone = false;
        foreach (var k in tiles)
        {
            var t = k.Transform2;
            var td = (double[])t.ArrayData;
            string facing = string.Join(" ", new[] { td[6], td[7], td[8] }.Select(v => Math.Round(v, 3).ToString("0.###", Inv)));
            if (!glo.ContainsKey(facing)) { glo[facing] = new[] { double.MaxValue, double.MaxValue, double.MaxValue }; ghi[facing] = new[] { double.MinValue, double.MinValue, double.MinValue }; }
            object info;
            foreach (Body2 b in (object[])k.GetBodies3((int)swBodyType_e.swSolidBody, out info) ?? new object[0])
            {
                var bb = (double[])b.GetBodyBox();   // part axes
                for (int i = 0; i < 8; i++)
                {
                    var p = Xf(mu, t, (i & 1) == 0 ? bb[0] : bb[3], (i & 2) == 0 ? bb[1] : bb[4], (i & 4) == 0 ? bb[2] : bb[5]);
                    for (int j = 0; j < 3; j++)
                    {
                        lo[j] = Math.Min(lo[j], p[j]); hi[j] = Math.Max(hi[j], p[j]);
                        glo[facing][j] = Math.Min(glo[facing][j], p[j]); ghi[facing][j] = Math.Max(ghi[facing][j], p[j]);
                    }
                }
                if (!firstDone)
                {
                    firstDone = true;
                    lines.Add("TILE PART\t" + Path.GetFileName(k.GetPathName()) + " <" + k.ReferencedConfiguration + ">\tbox in part axes " + Mm(bb[0]) + ".." + Mm(bb[3]) + ", " + Mm(bb[1]) + ".." + Mm(bb[4]) + ", " + Mm(bb[2]) + ".." + Mm(bb[5]));
                    // the biggest planar faces, with their outward normal (part axes) and colour, to tell which is the LED face
                    var faces = ((object[])b.GetFaces() ?? new object[0]).Cast<Face2>()
                        .Where(f => ((Surface)f.GetSurface()).IsPlane()).OrderByDescending(f => f.GetArea()).Take(4);
                    foreach (var f in faces)
                    {
                        var pp = (double[])((Surface)f.GetSurface()).PlaneParams;   // normal xyz, point xyz
                        double sgn = f.FaceInSurfaceSense() ? -1 : 1;
                        var col = f.MaterialPropertyValues as double[];
                        lines.Add("TILE FACE\tarea " + (f.GetArea() * 1e6).ToString("0", Inv) + " mm2\toutward normal (" + string.Join(", ", pp.Take(3).Select(v => (sgn * v).ToString("0.###", Inv))) + ")\tat " + string.Join(", ", pp.Skip(3).Take(3).Select(Mm))
                            + "\tcolour " + (col == null ? "(part/body colour)" : string.Join(" ", col.Take(3).Select(v => v.ToString("0.##", Inv)))));
                    }
                }
            }
            // the tile's part Z axis in the top's axes (which way each tile faces), counted
            var d = (double[])t.ArrayData;
            string n = string.Join(" ", new[] { d[6], d[7], d[8] }.Select(v => Math.Round(v, 3).ToString("0.###", Inv)));
            normals[n] = normals.ContainsKey(n) ? normals[n] + 1 : 1;
        }
        lines.Insert(0, "TILES\t" + tiles.Count + " DL_APEX components");
        lines.Insert(1, "ENVELOPE\tin the top assembly's axes (mm): X " + Mm(lo[0]) + ".." + Mm(hi[0]) + "\tY " + Mm(lo[1]) + ".." + Mm(hi[1]) + "\tZ " + Mm(lo[2]) + ".." + Mm(hi[2]));
        foreach (var kv in normals) lines.Add("TILE PART Z AXIS IN TOP\t(" + kv.Key + ")\t" + kv.Value + " tiles");
        // APEX-A49: the bottom-left corner of the LED face seen from the front. Grant 22 Nov: the LED face is the tile's full flat
        // face (part +Z), Y is up, and on a screen that wraps round, use the FRONT-FACING tiles (the way most tiles face) and
        // their face closest to the front. Viewer looks along -n; right = (-n) x up.
        // APEX-A67 (24 Nov): the corner line keeps LS-08A-11's wording; on a wrap, front = the VERTICAL face with the most tiles
        // (Grant 24 Nov, APEX-A49), the corner line says so, and every face gets its own measured line; plus the axis line (words
        // and unit vectors for left, up and outward) and the end-steel distances.
        if (normals.Count > 0)
        {
            bool wrap = normals.Count > 1;
            Func<string, bool> vertical = k => Math.Abs(double.Parse(k.Split(' ')[1], Inv)) < 0.001;
            string front = normals.OrderByDescending(kv => vertical(kv.Key) ? 1 : 0).ThenByDescending(kv => kv.Value).First().Key;
            var c = LedCorner(front, glo[front], ghi[front]);
            lines.Add(c != null
                ? "LED CORNER\tbottom-left of the LED face seen from the front (Y up), top assembly axes (mm): X " + Mm(c[0]) + "\tY " + Mm(c[1]) + "\tZ " + Mm(c[2])
                  + (wrap ? "\t(front: the vertical face with the most tiles, facing " + FacingName(front) + ", " + normals[front] + " of " + tiles.Count + " tiles)" : "")
                : "LED CORNER\tnot worked out: the screen is not square to the axes, or it has no vertical face");
            if (wrap)
                foreach (var kv in normals.OrderByDescending(kv => kv.Value))
                {
                    double[] l = glo[kv.Key], h = ghi[kv.Key];
                    var fc = LedCorner(kv.Key, l, h);
                    lines.Add("LED FACE\tfacing " + FacingName(kv.Key) + "\t" + kv.Value + " tiles\tX " + Mm(l[0]) + ".." + Mm(h[0]) + "\tY " + Mm(l[1]) + ".." + Mm(h[1]) + "\tZ " + Mm(l[2]) + ".." + Mm(h[2])
                        + "\t" + (fc != null ? "bottom-left seen from that side (Y up): X " + Mm(fc[0]) + "\tY " + Mm(fc[1]) + "\tZ " + Mm(fc[2]) : "no bottom-left (this face is not vertical)"));
                }
            if (c != null)
            {
                var n = front.Split(' ').Select(s => double.Parse(s, Inv)).ToArray();
                double[] up = { 0, 1, 0 }, fwd = { -n[0], -n[1], -n[2] };
                double[] right = { fwd[1] * up[2] - fwd[2] * up[1], fwd[2] * up[0] - fwd[0] * up[2], fwd[0] * up[1] - fwd[1] * up[0] };
                int jr = Enumerable.Range(0, 3).First(j => Math.Abs(right[j]) > 0.999), jn = Enumerable.Range(0, 3).First(j => Math.Abs(n[j]) > 0.999);
                string[] ax = { "x", "y", "z" };
                // steel: every body of the components with no tile in them, under top-level components with no tile in them
                var steel = new List<Component2>();
                foreach (var r in steelRoots) Leaves(r, steel);
                double plane = n[jn] > 0 ? ghi[front][jn] : glo[front][jn];
                double slo = double.MaxValue, shi = double.MinValue;   // steel along the face normal axis
                var boxes = new List<Tuple<Component2, Body2, double[], double[]>>();
                foreach (var k in steel)
                {
                    object info;
                    foreach (Body2 b in (object[])k.GetBodies3((int)swBodyType_e.swSolidBody, out info) ?? new object[0])
                    {
                        var bb = (double[])b.GetBodyBox();
                        double[] bl = { double.MaxValue, double.MaxValue, double.MaxValue }, bh = { double.MinValue, double.MinValue, double.MinValue };
                        for (int i = 0; i < 8; i++)
                        {
                            var p = Xf(mu, k.Transform2, (i & 1) == 0 ? bb[0] : bb[3], (i & 2) == 0 ? bb[1] : bb[4], (i & 4) == 0 ? bb[2] : bb[5]);
                            for (int j = 0; j < 3; j++) { bl[j] = Math.Min(bl[j], p[j]); bh[j] = Math.Max(bh[j], p[j]); }
                        }
                        slo = Math.Min(slo, bl[jn]); shi = Math.Max(shi, bh[jn]);
                        boxes.Add(Tuple.Create(k, b, bl, bh));
                    }
                }
                string side = boxes.Count == 0 ? "no steel found" : (slo + shi) / 2 > plane ? "steel at larger " + ax[jn] + " than the LED face" : "steel at smaller " + ax[jn] + " than the LED face";
                Func<double[], string> vec = v => "(" + string.Join(" ", v.Select(q => (Math.Abs(q) < 1e-9 ? 0 : q).ToString("0.###", Inv))) + ")";
                lines.Add("AXES\tfront face, unit vectors in the top assembly's axes: left " + vec(new[] { -right[0], -right[1], -right[2] }) + "\tup " + vec(up) + "\toutward " + vec(n)
                    + "\tseen from the front" + (wrap ? " (facing " + FacingName(front) + ")" : "") + ": " + ax[jr] + " increasing to the " + (right[jr] > 0 ? "right" : "left") + ", y up, " + side
                    + ", the LED face looking toward " + (n[jn] > 0 ? "+" : "-") + ax[jn]);
                // END STEEL: along the screen (the right direction), from each LED end to the outermost steel face, in a 1 mm slice
                // just above the LED corner's height (within 2 mm); positive = the steel reaches beyond the LED end
                double y0 = c[1], y1 = c[1] + 0.002, ymid = c[1] + 0.0005;
                // two figures per end (apex-admin 24 Nov): the outermost steel of any kind (on LS-08A-11 the GNRIC-LPP mounting
                // plates) and the outermost structural member, tube or angle (WeldMemberFeat bodies; on LS-08A-11 the posts)
                double uLo = double.MaxValue, uHi = double.MinValue, mLo = double.MaxValue, mHi = double.MinValue; int cut = 0, mcut = 0;
                var modeler = (Modeler)sw.GetModeler();
                double big = 0; for (int j = 0; j < 3; j++) big = Math.Max(big, Math.Max(Math.Abs(lo[j]), Math.Abs(hi[j])));
                big = 4 * big + 20;
                foreach (var t in boxes)
                {
                    if (t.Item4[1] < y0 || t.Item3[1] > y1) continue;   // body not at that height
                    var inv = (MathTransform)t.Item1.Transform2.Inverse();
                    var cp = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { 0.0, ymid, 0.0 })).MultiplyTransform(inv)).ArrayData;
                    var av = (double[])((MathVector)((MathVector)mu.CreateVector(new[] { 0.0, 1.0, 0.0 })).MultiplyTransform(inv)).ArrayData;
                    var slab = (Body2)modeler.CreateBodyFromBox3(new[] { cp[0], cp[1], cp[2], av[0], av[1], av[2], big, big, 0.001 });
                    if (slab == null) continue;
                    // a 1 mm slab (centred on, or starting at, ymid: either way within 2 mm above the corner)
                    int err;
                    var res = ((Body2)t.Item2.Copy()).Operations2((int)swBodyOperationType_e.SWBODYINTERSECT, slab, out err) as object[];
                    if (res == null) continue;
                    var rv = (double[])((MathVector)((MathVector)mu.CreateVector(new[] { right[0], right[1], right[2] })).MultiplyTransform(inv)).ArrayData;
                    bool member = IsMemberBody(t.Item2);
                    foreach (Body2 rb in res)
                    {
                        if (rb == null) continue;
                        cut++; if (member) mcut++;
                        foreach (int sg in new[] { 1, -1 })
                        {
                            double ex, ey, ez;
                            if (!rb.GetExtremePoint(sg * rv[0], sg * rv[1], sg * rv[2], out ex, out ey, out ez)) continue;
                            var w = Xf(mu, t.Item1.Transform2, ex, ey, ez);
                            double u = w[0] * right[0] + w[1] * right[1] + w[2] * right[2];
                            uLo = Math.Min(uLo, u); uHi = Math.Max(uHi, u);
                            if (member) { mLo = Math.Min(mLo, u); mHi = Math.Max(mHi, u); }
                        }
                    }
                }
                double ledLo = right[jr] > 0 ? glo[front][jr] : -ghi[front][jr], ledHi = right[jr] > 0 ? ghi[front][jr] : -glo[front][jr];
                lines.Add(cut == 0
                    ? "END STEEL\tnot measured: no steel in a 1 mm slice just above the LED corner's height (Y " + Mm(c[1]) + ")"
                    : "END STEEL\talong the screen, from each LED end (left, right seen from the front) to the outermost steel face, in a 1 mm slice just above the LED corner's height (Y " + Mm(c[1]) + "), mm, positive = steel beyond the LED end, negative = inside it:"
                      + "\tany steel: left " + Mm(ledLo - uLo) + "\tright " + Mm(uHi - ledHi)
                      + "\tmembers (tube or angle): " + (mcut == 0 ? "none in the slice" : "left " + Mm(ledLo - mLo) + "\tright " + Mm(mHi - ledHi))
                      + (wrap ? "\t(front face " + FacingName(front) + ")" : ""));
            }
        }
        else lines.Add("LED CORNER\tnot worked out: no tiles");
        File.WriteAllLines(outTxt, lines);
        foreach (var l in lines) Console.WriteLine(l);
    }

    static int ExportCmd(ModelDoc2 doc, string outDir, string name)
    {
        var asm = (AssemblyDoc)doc;
        asm.ResolveAllLightWeightComponents(false);
        doc.ForceRebuild3(false);
        Directory.CreateDirectory(outDir);
        string str = Path.Combine(outDir, name + "_STR.STEP"), led = Path.Combine(outDir, name + "_LED.STEP");
        foreach (var f in new[] { str, led }) if (File.Exists(f)) throw new Exception(f + " exists; nothing overwritten");
        Console.WriteLine("WHATSWRONG\t" + Errors(doc, true) + " errors");
        var before = Positions(asm, Path.Combine(outDir, "positions.txt"));
        var all = Active(asm);
        var tiles = all.Where(IsTile).ToList();
        if (tiles.Count == 0) throw new Exception("no DL_APEX tiles in this assembly");
        int oldAp = sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP);
        int rc = 0;
        try
        {
            if (oldAp != 203) sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, 203);
            Console.WriteLine("STEP\tAP203" + (oldAp != 203 ? " (was AP" + oldAp + ", put back after)" : ""));

            // structure: hide the tiles
            Console.WriteLine("HIDDEN FOR STR\t" + SetHidden(tiles, true) + " of " + tiles.Count + " tiles");
            if (!Step(doc, str)) rc = 1;
            SetHidden(tiles, false);

            // LED: hide everything that has no tile in it
            var hide = new List<Component2>();
            NonTileRoots((Component2)((Configuration)doc.GetActiveConfiguration()).GetRootComponent3(true), hide);
            Console.WriteLine("HIDDEN FOR LED\t" + SetHidden(hide, true) + " of " + hide.Count + " components (each with everything below it)");
            if (!Step(doc, led)) rc = 1;
            SetHidden(hide, false);
        }
        finally
        {
            if (oldAp != sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP))
                sw.SetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swStepAP, oldAp);
        }

        // nothing moved while hiding
        var after = Positions(asm, null);
        int moved = before.Count(kv => !after.ContainsKey(kv.Key) || after[kv.Key] != kv.Value);
        Console.WriteLine("MOVED WHILE EXPORTING\t" + moved);
        if (moved > 0) rc = 1;

        // what went into each file
        if (File.Exists(str) && File.Exists(led))
        {
            var ps = Products(str); var pl = Products(led);
            Console.WriteLine("STR PRODUCTS\t" + ps.Count + "\tDL_APEX among them: " + ps.Keys.Count(k => k.StartsWith("DL_APEX", StringComparison.OrdinalIgnoreCase)));
            Console.WriteLine("LED PRODUCTS\t" + pl.Count + "\t" + string.Join(", ", pl.Keys.OrderBy(k => k)));
            if (ps.Keys.Any(k => k.StartsWith("DL_APEX", StringComparison.OrdinalIgnoreCase))) { Console.WriteLine("PROBLEM\tthe STR file still has DL_APEX tiles: hidden components were exported"); rc = 1; }
        }
        // steel for the axis and end-steel lines: top-level components with no tile in them
        var steelRoots = ((object[])((Component2)((Configuration)doc.GetActiveConfiguration()).GetRootComponent3(true)).GetChildren() ?? new object[0]).Cast<Component2>()
            .Where(k => k.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && !HasTile(k)).ToList();
        LedReport(asm, tiles, steelRoots, Path.Combine(outDir, name + "_LED.txt"));
        return rc;
    }

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Usage: SwSestp count|square <part> | xforms <asm> <config> <out.txt> | export <asm> <config> <outDir> <name>"); return 2; }
        string cmd = a[0], file = Path.GetFullPath(a[1]);
        if (!file.StartsWith(Root, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: sandbox files only (" + Root + ")"); return 2; }
        if (cmd == "export" && !Path.GetFullPath(a[3]).StartsWith(Root, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under " + Root); return 2; }
        if (cmd == "xforms" && !Path.GetFullPath(a[3]).StartsWith(Root, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under " + Root); return 2; }
        sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        var t0 = DateTime.Now;
        ModelDoc2 doc = null;
        int rc = 0;
        try
        {
            doc = Open(file, cmd == "xforms" || cmd == "export" ? a[2] : null, cmd == "square");
            if (cmd == "count") rc = CountCmd(doc);
            else if (cmd == "square") rc = SquareCmd(doc);
            else if (cmd == "xforms")
            {
                var asm = (AssemblyDoc)doc;
                asm.ResolveAllLightWeightComponents(false);
                doc.ForceRebuild3(false);
                Positions(asm, Path.GetFullPath(a[3]));
                Console.WriteLine("WHATSWRONG\t" + Errors(doc, true) + " errors");
            }
            else if (cmd == "export") rc = ExportCmd(doc, Path.GetFullPath(a[3]), a[4]);
            else throw new Exception("unknown command " + cmd);
        }
        catch (Exception ex) { Console.WriteLine("FAILED\t" + ex.Message); rc = 3; }
        finally { if (doc != null) sw.CloseDoc(doc.GetTitle()); }
        Console.WriteLine("DONE\texit " + rc + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
        return rc;
    }
}
