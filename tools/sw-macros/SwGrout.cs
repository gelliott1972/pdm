// SwGrout: the no-grout 10 mm change on LS-08X-06 and LS-08X-11 (Grant via Admin, 22 Nov 2026): floor plates 10 lower,
// roof plates 10 higher, screen unchanged. Read-only commands open P:\ files or copies under C:\PDM-work\. Commands
// that save write only to copies under C:\PDM-work\grout\ (the rehearsal) or, with --checked-out, to a P:\ file that is
// checked out here (writable); a read-only vault file is refused. Never M:\, Dropbox or .pdm.
//   read-only: mates <asm> [config] | dims <file> [config] | xforms <asm> <config> <out.txt> | eq <file>
//   saving:    setdims <file> <config|*> <dim>=<mm> ... | gv <part> <config> <global>=<value> ...
//              plane <asm> <plane> <offset mm> | feet <part> <sketch> <dim>=<mm> ... | setcomp <asm> <config> <comp> <cfg>
//              delcomp <asm> <file name> | matedist <asm> <mate> <add mm> | addcfg <part> <base config> <new config>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwGrout.exe SwGrout.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwGrout
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static ISldWorks sw;
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }
    static string S(Func<string> f) { try { return f() ?? ""; } catch { return ""; } }

    static int Errors(ModelDoc2 doc, bool list)
    {
        object f, c, w; doc.Extension.GetWhatsWrong(out f, out c, out w);
        var ef = f as object[]; var wf = w as bool[]; int n = 0;
        if (ef != null)
            for (int i = 0; i < ef.Length; i++)
            {
                bool warn = wf != null && i < wf.Length && wf[i];
                if (!warn) n++;
                if (list && i < 60) Console.WriteLine("  " + (warn ? "warning" : "error") + "\t" + S(() => ((Feature)ef[i]).Name));
            }
        return n;
    }

    static Feature FeatureNamed(ModelDoc2 doc, string name)
    {
        for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) if (f.Name == name) return f;
        throw new Exception("no feature " + name);
    }

    static double PlaneYv(Feature f) { return ((double[])((MathTransform)((RefPlane)f.GetSpecificFeature2()).Transform).ArrayData)[10]; }
    static string PlaneY(Feature f) { return Mm(PlaneYv(f)); }

    static string What(object r)
    {
        if (r == null) return "?";
        var face = r as Face2; if (face != null) return "face of " + S(() => ((Feature)face.GetFeature()).Name);
        var edge = r as Edge;
        if (edge != null)
        {
            var fs = edge.GetTwoAdjacentFaces2() as object[];
            return "edge of " + (fs == null ? "?" : string.Join(" / ", fs.Where(x => x != null).Select(x => S(() => ((Feature)((Face2)x).GetFeature()).Name))));
        }
        var vx = r as Vertex; if (vx != null) return "vertex";
        var sp = r as SketchPoint; if (sp != null) return "point of " + S(() => ((Feature)sp.GetSketch()).Name);
        var ss = r as SketchSegment; if (ss != null) return "line of " + S(() => ((Feature)ss.GetSketch()).Name);
        var ft = r as Feature; if (ft != null) return ft.GetTypeName2() + " " + ft.Name;
        var rp = r as RefPlane; if (rp != null) return "plane " + S(() => ((Feature)rp).Name);
        return r.GetType().Name;
    }

    static int Main(string[] a)
    {
        if (a.Length < 2) { Console.Error.WriteLine("Usage: SwGrout mates <asm> [config] | xforms <asm> <config> <out> | setdims <part> <config> <dim>=<mm> ..."); return 2; }
        // --checked-out (the live change, only after Grant's go): a command that saves may also write a P:\ vault file, but
        // only one that is checked out here (the PDM makes it writable; a read-only vault file is refused, as SwCmd does).
        // Read-only commands may open P:\ files. Never M:\ / Dropbox, never .pdm.
        bool checkedOut = a.Contains("--checked-out");
        a = a.Where(x => x != "--checked-out").ToArray();
        string cmd = a[0], file = Path.GetFullPath(a[1]);
        bool write = cmd == "setdims" || cmd == "plane" || cmd == "addcfg" || cmd == "setcomp" || (cmd == "eq" && a.Length > 2) || cmd == "gv" || cmd == "feet" || cmd == "delcomp" || cmd == "matedist" || cmd == "rename" || cmd == "props" || cmd == "kitseed" || cmd == "repoint" || cmd == "resave" || cmd == "breaklink" || (cmd == "remate" && !(a.Length > 5 && a[5] == "dry")) || cmd == "kitcfg" || (cmd == "placekits" && !(a.Length > 8 && a[8] == "dry"));
        if (file.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0)
        { Console.Error.WriteLine("REFUSED: never the Dropbox files or .pdm: " + file); return 2; }
        bool inVault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (cmd == "import") { if (!file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: STEP only from C:\\PDM-work"); return 2; } }
        else if (write)
        {
            if (inVault)
            {
                if (!checkedOut) { Console.Error.WriteLine("REFUSED: a vault file is written only with --checked-out: " + file); return 2; }
                if (!File.Exists(file)) { Console.Error.WriteLine("REFUSED: no such vault file: " + file); return 2; }
                if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) { Console.Error.WriteLine("REFUSED: the vault file is read-only, so it is not checked out: " + file); return 2; }
            }
            else if (!file.StartsWith(@"C:\PDM-work\grout\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: writes only to C:\\PDM-work\\grout copies or checked-out P:\\ files"); return 2; }
        }
        else if (!inVault && !file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: only P:\\ or copies under C:\\PDM-work"); return 2; }
        if (cmd == "xforms" && !Path.GetFullPath(a[3]).StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under C:\\PDM-work"); return 2; }
        sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        if (cmd == "repoint")
        {
            // repoint <asm> <old path> <new path> [oldCfg=newCfg ...] [--checked-out] (anchors step, 22 Nov): every component
            // of <old path> in <asm> becomes <new path>, each keeping its instance and getting its configuration renamed by the
            // map. Read first (instance -> configuration), then SOLIDWORKS's ReplaceReferencedDocument on the closed file,
            // then the configurations set per instance number, rebuilt and saved. Same write rule as the other commands.
            string oldP = Path.GetFullPath(a[2]), newP = Path.GetFullPath(a[3]);
            var map = a.Skip(4).Select(m => m.Split(new[] { '=' }, 2)).ToDictionary(q => q[0], q => q[1]);
            int e1 = 0, w1 = 0;
            var rd = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocASSEMBLY, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e1, ref w1);
            if (rd == null) { Console.WriteLine("FAILED\topen " + e1); return 3; }
            var cfgOf = new Dictionary<string, string>();
            foreach (Component2 c in ((object[])((AssemblyDoc)rd).GetComponents(true) ?? new object[0]))
                if (string.Equals(c.GetPathName(), oldP, StringComparison.OrdinalIgnoreCase) || string.Equals(Path.GetFileName(c.GetPathName()), Path.GetFileName(oldP), StringComparison.OrdinalIgnoreCase))
                    cfgOf[c.Name2.Substring(c.Name2.LastIndexOf('-') + 1)] = c.ReferencedConfiguration;
            sw.CloseDoc(rd.GetTitle());
            Console.WriteLine("BEFORE\t" + cfgOf.Count + " instances: " + string.Join(", ", cfgOf.GroupBy(kv => kv.Value).Select(g => g.Count() + " x " + g.Key)));
            if (cfgOf.Count == 0) { Console.WriteLine("FAILED\tno components of " + oldP); return 3; }
            bool rr = sw.ReplaceReferencedDocument(file, oldP, newP);
            Console.WriteLine("REPLACE REFERENCE\t" + rr);
            if (!rr) return 3;
            int e2 = 0, w2 = 0;
            var wd = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocASSEMBLY, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight), "", ref e2, ref w2);
            if (wd == null) { Console.WriteLine("FAILED\topen after replace " + e2); return 3; }
            int prc = 0;
            try
            {
                var wa = (AssemblyDoc)wd; wa.ResolveAllLightWeightComponents(false);
                int set = 0, seen = 0;
                foreach (Component2 c in ((object[])wa.GetComponents(true) ?? new object[0]))
                {
                    if (!string.Equals(c.GetPathName(), newP, StringComparison.OrdinalIgnoreCase)) continue;
                    seen++;
                    string num = c.Name2.Substring(c.Name2.LastIndexOf('-') + 1), oc, nc2;
                    if (!cfgOf.TryGetValue(num, out oc)) { Console.WriteLine("PROBLEM\tno earlier configuration for " + c.Name2); prc = 1; continue; }
                    if (!map.TryGetValue(oc, out nc2)) nc2 = oc;
                    c.ReferencedConfiguration = nc2;
                    if (c.ReferencedConfiguration == nc2) set++; else { Console.WriteLine("PROBLEM\t" + c.Name2 + " did not take " + nc2); prc = 1; }
                }
                wd.ForceRebuild3(false);
                int er = Errors(wd, true);
                var now = ((object[])wa.GetComponents(true) ?? new object[0]).Cast<Component2>().Where(c => string.Equals(c.GetPathName(), newP, StringComparison.OrdinalIgnoreCase)).GroupBy(c => c.ReferencedConfiguration).Select(g => g.Count() + " x " + g.Key);
                int left = ((object[])wa.GetComponents(true) ?? new object[0]).Cast<Component2>().Count(c => string.Equals(Path.GetFileName(c.GetPathName()), Path.GetFileName(oldP), StringComparison.OrdinalIgnoreCase));
                Console.WriteLine("AFTER\t" + seen + " instances of " + Path.GetFileName(newP) + ": " + string.Join(", ", now) + "\tconfigurations set " + set + "\told file left " + left + "\terrors " + er);
                if (seen != cfgOf.Count || left > 0 || er > 0) prc = 1;
                if (prc == 0) { int se = 0, swn = 0; bool ok = wd.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref swn); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + se); if (!ok) prc = 1; }
                else Console.WriteLine("NOT SAVED");
            }
            finally { sw.CloseDoc(wd.GetTitle()); }
            return prc;
        }
        if (cmd == "import")
        {
            // import <file.step> <out.SLDPRT>: STEP into a part (one part, bodies kept), saved only under C:\PDM-work\grout\
            string outp = Path.GetFullPath(a[2]);
            if (!outp.StartsWith(@"C:\PDM-work\grout\", StringComparison.OrdinalIgnoreCase) || File.Exists(outp)) { Console.Error.WriteLine("REFUSED: output only new, under C:\\PDM-work\\grout"); return 2; }
            var imp = (ImportStepData)sw.GetImportFileData(file);
            if (imp != null) imp.MapConfigurationData = true;
            int ie = 0;
            var idoc = sw.LoadFile4(file, "r", imp, ref ie) as ModelDoc2;
            if (idoc == null) { Console.WriteLine("FAILED\timport error " + ie); return 3; }
            Console.WriteLine("IMPORTED\t" + idoc.GetTitle() + "\ttype " + idoc.GetType() + "\terror " + ie);
            int irc = 0;
            try
            {
                if (idoc.GetType() != (int)swDocumentTypes_e.swDocPART) { Console.WriteLine("PROBLEM\tthe STEP came in as an assembly, not one part"); irc = 1; }
                else
                {
                    var bodies = (object[])((PartDoc)idoc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0];
                    var box = (double[])((PartDoc)idoc).GetPartBox(true);
                    Console.WriteLine("BODIES\t" + bodies.Length + "\tBOX\t" + Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]));
                    int se = 0, swn = 0;
                    bool ok = idoc.Extension.SaveAs3(outp, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, null, null, ref se, ref swn);
                    Console.WriteLine(ok ? "SAVED\t" + outp : "SAVE FAILED\t" + se);
                    if (!ok) irc = 1;
                }
            }
            finally { sw.CloseDoc(idoc.GetTitle()); }
            return irc;
        }
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        int e = 0, w = 0;
        var t0 = DateTime.Now;
        string cfgArg = a.Length > 2 && cmd != "plane" && cmd != "feet" && cmd != "delcomp" && cmd != "matedist" && cmd != "props" && cmd != "kitseed" && cmd != "remate" && cmd != "placekits" ? a[2] : "";
        var doc = sw.OpenDoc6(file, type, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight)
            | (write ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), (cfgArg == "*" ? "" : cfgArg), ref e, ref w);
        if (doc == null) { Console.WriteLine("FAILED\tcould not open " + file + " (" + e + ")"); return 3; }
        if (!string.Equals(doc.GetPathName(), file, StringComparison.OrdinalIgnoreCase)) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("FAILED\tSOLIDWORKS gave " + doc.GetPathName()); return 3; }
        Console.WriteLine("OPENED\t" + file + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name + "\terrors " + e + " warnings " + w);
        int rc = 0;
        try
        {
            if (cmd == "mates")
            {
                if (cfgArg != "") doc.ShowConfiguration2(cfgArg);
                if (type == (int)swDocumentTypes_e.swDocASSEMBLY) ((AssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                doc.ForceRebuild3(false);
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                {
                    if (f.GetTypeName2() != "MateGroup") continue;
                    for (var m = (Feature)f.GetFirstSubFeature(); m != null; m = (Feature)m.GetNextSubFeature())
                    {
                        var mate = m.GetSpecificFeature2() as Mate2;
                        if (mate == null) continue;
                        var ents = new List<string>();
                        for (int k = 0; k < mate.GetMateEntityCount(); k++)
                        {
                            var me = mate.MateEntity(k);
                            ents.Add(S(() => me.ReferenceComponent.Name2) + " : " + S(() => What(me.Reference)));
                        }
                        bool warn; int code = m.GetErrorCode2(out warn);
                        string extra = "";
                        if (mate.Type == (int)swMateType_e.swMateSLOT)
                            extra = "\tslot constraint " + S(() => ((SlotMateFeatureData)m.GetDefinition()).Constraint.ToString() + " (0 free, 1 centre, 2 distance, 3 percent)");
                        Console.WriteLine("MATE\t" + m.Name + "\t" + (swMateType_e)mate.Type + extra + (m.IsSuppressed() ? "\tSUPPRESSED" : "") + (code != 0 ? "\tERROR " + code : "") + "\t" + string.Join("\t| ", ents));
                    }
                }
            }
            else if (cmd == "dims")
            {
                // dims <file> [config]: every feature (with sub-features) in order: type, suppression, parents, its dimensions
                if (cfgArg != "") doc.ShowConfiguration2(cfgArg);
                doc.ForceRebuild3(false);
                Action<Feature, string> show = null;
                show = (f, ind) =>
                {
                    var dims = new List<string>();
                    for (var dd = f.GetFirstDisplayDimension() as DisplayDimension; dd != null; dd = f.GetNextDisplayDimension(dd) as DisplayDimension)
                    {
                        var d = dd.GetDimension2(0);
                        dims.Add(d.FullName + "=" + Mm(((double[])d.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0]) + (d.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven ? "(driven)" : ""));
                    }
                    var par = ((object[])f.GetParents() ?? new object[0]).Cast<Feature>().Select(p => p.Name);
                    string t = f.GetTypeName2();
                    if (t == "MateGroup" || t == "Reference" || t.EndsWith("Folder") || t == "HistoryFolder") return;
                    Console.WriteLine("FEAT\t" + ind + f.Name + "\t" + t + (f.IsSuppressed() ? "\tSUPPRESSED" : "") + "\tparents: " + string.Join(", ", par) + (dims.Count > 0 ? "\t" + string.Join("  ", dims) : ""));
                    if (t != "ProfileFeature")
                        for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature()) show(s, ind + "  ");
                };
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) show(f, "");
                if (type == (int)swDocumentTypes_e.swDocPART)
                {
                    var mu = (MathUtility)sw.GetMathUtility();
                    for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                    {
                        var sk = f.GetSpecificFeature2() as Sketch;
                        if (sk == null || f.IsSuppressed()) continue;
                        var toModel = (MathTransform)sk.ModelToSketchTransform.Inverse();
                        foreach (SketchSegment sg in (object[])sk.GetSketchSegments() ?? new object[0])
                        {
                            var ln = sg as SketchLine; if (ln == null || sg.ConstructionGeometry) continue;
                            var p0 = (SketchPoint)ln.GetStartPoint2(); var p1 = (SketchPoint)ln.GetEndPoint2();
                            var a0 = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { p0.X, p0.Y, p0.Z })).MultiplyTransform(toModel)).ArrayData;
                            var a1 = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { p1.X, p1.Y, p1.Z })).MultiplyTransform(toModel)).ArrayData;
                            Console.WriteLine("LINE\t" + f.Name + "\t(" + string.Join(", ", a0.Select(Mm)) + ") -> (" + string.Join(", ", a1.Select(Mm)) + ")");
                        }
                    }
                    var box = (double[])((PartDoc)doc).GetPartBox(true);
                    Console.WriteLine("BOX\t" + Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]));
                }
            }
            else if (cmd == "faces")
            {
                // faces <part> [config]: every solid body's faces by surface type, and the features (thread check, 24 Nov)
                foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                {
                    var kinds = new Dictionary<string, int>();
                    foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                    {
                        var su = (Surface)f.GetSurface();
                        string k = su.IsPlane() ? "plane" : su.IsCylinder() ? "cylinder" : su.IsCone() ? "cone" : su.IsTorus() ? "torus" : su.IsSphere() ? "sphere" : su.IsSwept() ? "swept" : su.IsRevolved() ? "revolved" : su.IsBlending() ? "blend" : su.IsOffset() ? "offset" : su.IsParametric() ? "bspline" : "other(" + su.Identity() + ")";
                        kinds[k] = kinds.ContainsKey(k) ? kinds[k] + 1 : 1;
                    }
                    var bb = (double[])b.GetBodyBox();
                    Console.WriteLine("BODY\t" + b.Name + "\tfaces " + string.Join(", ", kinds.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)) + "\tbox " + Mm(bb[0]) + ".." + Mm(bb[3]) + ", " + Mm(bb[1]) + ".." + Mm(bb[4]) + ", " + Mm(bb[2]) + ".." + Mm(bb[5]));
                }
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                {
                    string tn = f.GetTypeName2();
                    if (tn.EndsWith("Folder") || tn == "MaterialFolder" || tn == "HistoryFolder" || tn == "SensorFolder" || tn == "DocsFolder" || tn == "DetailCabinet" || tn == "SelectionSetFolder" || tn == "EqnFolder" || tn == "CommentsFolder" || tn == "FavoriteFolder") continue;
                    Console.WriteLine("FEAT\t" + f.Name + "\t" + tn + (f.IsSuppressed() ? "\tSUPPRESSED" : ""));
                    for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature())
                        Console.WriteLine("FEAT\t  " + s.Name + "\t" + s.GetTypeName2() + (s.IsSuppressed() ? "\tSUPPRESSED" : ""));
                }
            }
            else if (cmd == "holes")
            {
                // holes <part> [config] <diameter mm>: the axis points of every cylindrical face of that diameter (part axes)
                double dia = double.Parse(a[a.Length - 1], Inv) / 1000.0;
                var seen = new List<string>();
                foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                    foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                    {
                        var su = (Surface)f.GetSurface(); if (!su.IsCylinder()) continue;
                        var cp = (double[])su.CylinderParams; if (Math.Abs(cp[6] * 2 - dia) > 1e-6) continue;
                        var bx = (double[])f.GetBox();
                        string s = "axis (" + string.Join(", ", cp.Skip(3).Take(3).Select(v => Math.Round(v, 3).ToString(Inv))) + ")\tface box centre (" + Mm((bx[0] + bx[3]) / 2) + ", " + Mm((bx[1] + bx[4]) / 2) + ", " + Mm((bx[2] + bx[5]) / 2) + ")";
                        if (!seen.Contains(s)) { seen.Add(s); Console.WriteLine("HOLE\t" + s); }
                    }
                Console.WriteLine("HOLES\t" + seen.Count);
            }
            else if (cmd == "xforms")
            {
                var asm = (AssemblyDoc)doc;
                asm.ResolveAllLightWeightComponents(false);
                doc.ForceRebuild3(false);
                var lines = new List<string>();
                foreach (Component2 k in (object[])asm.GetComponents(false) ?? new object[0])
                {
                    if (k.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed) continue;
                    var t = k.Transform2; string pos = "-";
                    if (t != null) { var d = (double[])t.ArrayData; pos = string.Join(" ", d.Take(9).Select(v => Math.Round(v, 5).ToString("0.#####", Inv))) + " | " + Mm(d[9]) + " " + Mm(d[10]) + " " + Mm(d[11]); }
                    lines.Add("COMP\t" + k.Name2 + "\t" + Path.GetFileName(k.GetPathName()) + "\t<" + k.ReferencedConfiguration + ">\t" + pos);
                }
                lines.Sort(StringComparer.Ordinal);
                File.WriteAllLines(Path.GetFullPath(a[3]), lines);
                Console.WriteLine("POSITIONS\t" + lines.Count + " active components");
                Console.WriteLine("WHATSWRONG\t" + Errors(doc, true) + " errors");
            }
            else if (cmd == "setdims")
            {
                // config "*" = every configuration (a part with one length), otherwise that configuration only
                bool all = cfgArg == "*";
                if (all) cfgArg = ((Configuration)doc.GetActiveConfiguration()).Name;
                doc.ShowConfiguration2(cfgArg);
                if (((Configuration)doc.GetActiveConfiguration()).Name != cfgArg) throw new Exception("no configuration " + cfgArg);
                int scope = all ? (int)swInConfigurationOpts_e.swAllConfiguration : (int)swInConfigurationOpts_e.swThisConfiguration;
                foreach (var kv in a.Skip(3))
                {
                    var p = kv.Split('=');
                    var dim = (Dimension)doc.Parameter(p[0]);
                    if (dim == null) throw new Exception("no dimension " + p[0]);
                    double old = ((double[])dim.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0];
                    int r = dim.SetSystemValue3(double.Parse(p[1], Inv) / 1000.0, scope, null);
                    doc.ForceRebuild3(false);
                    double now = ((double[])dim.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0];
                    Console.WriteLine("SET\t" + cfgArg + "\t" + p[0] + "\t" + Mm(old) + " -> " + Mm(now) + "\t(result " + r + ")");
                    if (Math.Abs(now * 1000 - double.Parse(p[1], Inv)) > 1e-6) { Console.WriteLine("PROBLEM\tvalue not taken"); rc = 1; }
                }
                // the other configurations must not change: check their values of the same dimensions and their errors
                foreach (string c in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
                    int er = Errors(doc, false);
                    var vals = a.Skip(3).Select(kv => kv.Split('=')[0]).Select(n => Mm(((double[])((Dimension)doc.Parameter(n)).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0]));
                    Console.WriteLine("CONFIG\t" + c + "\t" + string.Join(" ", vals) + "\terrors " + er);
                    if (er > 0) rc = 1;
                }
                doc.ShowConfiguration2(cfgArg);
                if (rc == 0)
                {
                    bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                    Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e);
                    if (!ok) rc = 1;
                }
                else Console.WriteLine("NOT SAVED\tproblems above");
            }
            else if (cmd == "eq")
            {
                // eq <file>: equations and design table, read-only
                var em = doc.GetEquationMgr();
                Console.WriteLine("EQUATIONS\t" + em.GetCount() + "\tlinked file " + S(() => em.LinkToFile ? em.FilePath : "-"));
                for (int i = 0; i < em.GetCount(); i++) Console.WriteLine("  EQ\t" + em.Equation[i] + "\t= " + em.Value[i].ToString(Inv) + (em.GlobalVariable[i] ? "\tglobal" : ""));
                var dt = doc.GetDesignTable() as DesignTable;
                Console.WriteLine("DESIGN TABLE\t" + (dt == null ? "none" : "yes"));
                if (dt != null)
                {
                    bool att = dt.Attach();
                    if (!att) att = dt.EditTable2(false) != null;
                    int rows = dt.GetRowCount(), cols = dt.GetColumnCount();
                    Console.WriteLine("  opened " + att + "\tsize " + rows + " rows x " + cols + " columns\tstart row " + dt.GetStartRowNumber() + " col " + dt.GetStartColumnNumber());
                    for (int r = 0; r <= Math.Min(rows, 70); r++)
                        Console.WriteLine("  ROW " + r + "\t" + string.Join("\t", Enumerable.Range(0, cols + 1).Select(c => r == 0 ? S(() => dt.GetHeaderText(c)) : S(() => dt.GetEntryText(r, c)))));
                    // eq <file> <config> <column>=<value> ...: set cells (rehearsal copies only), update, rebuild, show the lengths
                    if (a.Length > 3 && file.StartsWith(@"C:\PDM-work\grout\", StringComparison.OrdinalIgnoreCase))
                    {
                        int title = 0;
                        int row = Enumerable.Range(0, rows + 1).FirstOrDefault(r => S(() => dt.GetEntryText(r, 0)) == a[2]);
                        if (row <= title) throw new Exception("no row " + a[2]);
                        foreach (var kv in a.Skip(3))
                        {
                            var p = kv.Split('=');
                            int col = Enumerable.Range(0, cols + 1).FirstOrDefault(c => S(() => dt.GetHeaderText(c)) == p[0]);
                            if (col == 0) throw new Exception("no column " + p[0]);
                            string old = dt.GetEntryText(row, col);
                            dt.SetEntryText(row, col, p[1]);
                            Console.WriteLine("CELL\t" + a[2] + "\t" + p[0] + "\t" + old + " -> " + dt.GetEntryText(row, col));
                        }
                        bool up = dt.UpdateTable((int)swDesignTableUpdateOptions_e.swUpdateDesignTableAll, true);
                        Console.WriteLine("UPDATE\t" + up);
                        dt.Detach();
                        foreach (string c in (string[])doc.GetConfigurationNames())
                        {
                            doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
                            Console.WriteLine("CONFIG\t" + c + "\tlength " + Mm(((double[])((Dimension)doc.Parameter("D1@MEMBER_SKETCH")).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0])
                                + "\tplate start " + Mm(((double[])((Dimension)doc.Parameter("D1@PLATE_POSITION_SKETCH")).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0]) + "\terrors " + Errors(doc, false));
                        }
                        bool sv = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                        Console.WriteLine(sv ? "SAVED\t" + file : "SAVE FAILED\t" + e);
                    }
                    else dt.Detach();
                }
            }
            else if (cmd == "gv")
            {
                // gv <part> <config> <global>=<value> ...: set global variables in that configuration only (rehearsal copies only),
                // then list every configuration's member length and plate start, so other configurations can be seen unchanged
                doc.ShowConfiguration2(cfgArg);
                if (((Configuration)doc.GetActiveConfiguration()).Name != cfgArg) throw new Exception("no configuration " + cfgArg);
                var em = doc.GetEquationMgr();
                foreach (var kv in a.Skip(3))
                {
                    var p = kv.Split('=');
                    int idx = Enumerable.Range(0, em.GetCount()).FirstOrDefault(i => em.Equation[i].Replace(" ", "").StartsWith("\"" + p[0] + "\"="));
                    if (!em.Equation[idx].Replace(" ", "").StartsWith("\"" + p[0] + "\"=")) throw new Exception("no global " + p[0]);
                    string old = em.Equation[idx];
                    int r = em.SetEquationAndConfigurationOption(idx, "\"" + p[0] + "\"= " + p[1], (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    em.EvaluateAll();
                    doc.ForceRebuild3(false);
                    Console.WriteLine("GLOBAL\t" + cfgArg + "\t" + old + " -> " + em.Equation[idx] + "\t(result " + r + ")");
                }
                foreach (string c in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
                    Console.WriteLine("CONFIG\t" + c + "\tlength " + Mm(((double[])((Dimension)doc.Parameter("D1@MEMBER_SKETCH")).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0])
                        + "\tplate start " + Mm(((double[])((Dimension)doc.Parameter("D1@PLATE_POSITION_SKETCH")).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0]) + "\terrors " + Errors(doc, false));
                }
                doc.ShowConfiguration2(cfgArg);
                bool sv = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                Console.WriteLine(sv ? "SAVED\t" + file : "SAVE FAILED\t" + e);
            }
            else if (cmd == "feet")
            {
                // feet <part> <sketch> <top dim>=<mm> <other dim>=<mm>: the 08x06-SFW-01 layout (22 Nov): the feet sit on the
                // origin (one foot coincident to it, the others horizontal to that foot). Hold the TOP instead: drop the
                // foot-to-origin relation, add a vertical dimension origin -> the top of that foot's leg (its present value),
                // then set the leg dimensions, so only the feet move. Rehearsal copies only.
                var feat = FeatureNamed(doc, a[2]);
                var sk = (Sketch)feat.GetSpecificFeature2();
                doc.ClearSelection2(true);
                feat.Select2(false, -1);
                doc.EditSketch();
                var rm = sk.RelationManager;
                SketchPoint origin = null, foot = null; double fx = 0, fy = 0;
                foreach (SketchRelation r in (object[])rm.GetRelations((int)swSketchRelationFilterType_e.swExternal) ?? new object[0])
                {
                    if (r.GetRelationType() != (int)swConstraintType_e.swConstraintType_COINCIDENT) continue;
                    var defs = (object[])r.GetDefinitionEntities2() ?? new object[0];
                    var ents = (object[])r.GetEntities() ?? new object[0];
                    origin = defs.OfType<SketchPoint>().FirstOrDefault(p => S(() => ((Feature)p.GetSketch()).Name) != a[2]);
                    foot = ents.OfType<SketchPoint>().FirstOrDefault(p => S(() => ((Feature)p.GetSketch()).Name) == a[2]) ?? defs.OfType<SketchPoint>().FirstOrDefault(p => S(() => ((Feature)p.GetSketch()).Name) == a[2]);
                    if (origin != null && foot != null) { fx = foot.X; fy = foot.Y; Console.WriteLine("DELETE RELATION\tfoot (" + Mm(foot.X) + ", " + Mm(foot.Y) + ") coincident to the origin\t" + rm.DeleteRelation(r)); break; }
                }
                if (origin == null || foot == null) throw new Exception("no foot-to-origin relation found");
                // the relation's objects are gone after the delete: look the points up again by position
                SketchPoint top = null;
                foot = ((object[])sk.GetSketchPoints2()).Cast<SketchPoint>().First(q => Math.Abs(q.X - fx) < 1e-6 && Math.Abs(q.Y - fy) < 1e-6);
                foreach (SketchSegment sg in (object[])sk.GetSketchSegments() ?? new object[0])
                {
                    var ln = sg as SketchLine; if (ln == null) continue;
                    var p0 = (SketchPoint)ln.GetStartPoint2(); var p1 = (SketchPoint)ln.GetEndPoint2();
                    if (Math.Abs(p0.X - foot.X) < 1e-6 && Math.Abs(p0.Y - foot.Y) < 1e-6 && Math.Abs(p1.X - foot.X) < 1e-6) top = p1;
                    if (Math.Abs(p1.X - foot.X) < 1e-6 && Math.Abs(p1.Y - foot.Y) < 1e-6 && Math.Abs(p0.X - foot.X) < 1e-6) top = p0;
                }
                if (top == null) throw new Exception("no vertical leg at the foot");
                var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
                doc.ClearSelection2(true);
                bool s1 = doc.Extension.SelectByID2("Point1@Origin", "EXTSKETCHPOINT", 0, 0, 0, false, 0, null, 0), s2 = top.Select4(true, sd);
                // no "Modify" box when the dimension is added (it waits for a click: the 22 Nov hang); put back after
                bool inputOn = sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate);
                sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, false);
                DisplayDimension dd;
                try { dd = doc.AddVerticalDimension2(top.X + 0.1, top.Y / 2, 0) as DisplayDimension; }
                finally { sw.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swInputDimValOnCreate, inputOn); }
                Console.WriteLine("HOLD TOP\tvertical dimension origin -> (" + Mm(top.X) + ", " + Mm(top.Y) + ")\tselected " + s1 + "/" + s2 + "\t" + (dd == null ? "FAILED" : dd.GetDimension2(0).FullName + " = " + Mm(dd.GetDimension2(0).SystemValue) + " driven state " + dd.GetDimension2(0).DrivenState));
                if (dd == null) { doc.InsertSketch2(false); throw new Exception("could not add the holding dimension"); }
                // keep the leg on the origin's vertical too (the deleted relation also held X)
                doc.ClearSelection2(true);
                doc.Extension.SelectByID2("Point1@Origin", "EXTSKETCHPOINT", 0, 0, 0, false, 0, null, 0); top.Select4(true, sd);
                doc.SketchAddConstraints("sgVERTICALPOINTS2D");
                doc.ClearSelection2(true);
                foreach (var kv in a.Skip(3))
                {
                    var p = kv.Split('=');
                    var dim = (Dimension)doc.Parameter(p[0]);
                    double old = dim.SystemValue;
                    dim.SetSystemValue3(double.Parse(p[1], Inv) / 1000.0, (int)swInConfigurationOpts_e.swAllConfiguration, null);
                    Console.WriteLine("SET\t" + p[0] + "\t" + Mm(old) + " -> " + Mm(dim.SystemValue));
                }
                doc.ClearSelection2(true);
                doc.InsertSketch2(true);
                doc.ForceRebuild3(false);
                foreach (SketchSegment sg in (object[])sk.GetSketchSegments() ?? new object[0])
                {
                    var ln = sg as SketchLine; if (ln == null) continue;
                    var p0 = (SketchPoint)ln.GetStartPoint2(); var p1 = (SketchPoint)ln.GetEndPoint2();
                    Console.WriteLine("LINE\t(" + Mm(p0.X) + ", " + Mm(p0.Y) + ") -> (" + Mm(p1.X) + ", " + Mm(p1.Y) + ")");
                }
                Console.WriteLine("SKETCH\t" + Enum.GetName(typeof(swConstrainedStatus_e), sk.GetConstrainedStatus()));
                int er = 0;
                foreach (string c in (string[])doc.GetConfigurationNames()) { doc.ShowConfiguration2(c); doc.ForceRebuild3(false); int x = Errors(doc, true); er += x; Console.WriteLine("CONFIG\t" + c + "\terrors " + x); }
                if (er == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); }
                else { Console.WriteLine("NOT SAVED\terrors"); rc = 1; }
            }
            else if (cmd == "delcomp")
            {
                // delcomp <asm> <file name>: delete every top-level component of <asm> that uses that file (rehearsal copies only)
                var asm = (AssemblyDoc)doc;
                asm.ResolveAllLightWeightComponents(false);
                var top = (Component2)((Configuration)doc.GetActiveConfiguration()).GetRootComponent3(true);
                var kill = ((object[])top.GetChildren() ?? new object[0]).Cast<Component2>()
                    .Where(c => string.Equals(Path.GetFileName(c.GetPathName()), a[2], StringComparison.OrdinalIgnoreCase)).ToList();
                var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
                doc.ClearSelection2(true);
                int sel = kill.Count(c => c.Select4(true, sd, false));
                bool del = sel > 0 && doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                doc.ForceRebuild3(false);
                int left = ((object[])top.GetChildren() ?? new object[0]).Cast<Component2>().Count(c => string.Equals(Path.GetFileName(c.GetPathName()), a[2], StringComparison.OrdinalIgnoreCase));
                int er = Errors(doc, true);
                Console.WriteLine("DELETED\t" + a[2] + "\t" + sel + " selected of " + kill.Count + "\tdelete " + del + "\tleft " + left + "\terrors " + er);
                if (left == 0 && er == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); }
                else { Console.WriteLine("NOT SAVED"); rc = 1; }
            }
            else if (cmd == "matedist")
            {
                // matedist <asm> <mate> <add mm>: add to a distance mate's value (rehearsal copies only)
                Feature m = null; for (var g = (Feature)doc.FirstFeature(); g != null && m == null; g = (Feature)g.GetNextFeature()) if (g.GetTypeName2() == "MateGroup") m = g;
                Feature mf = null;
                for (var s = (Feature)m.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature()) if (s.Name == a[2]) mf = s;
                if (mf == null) throw new Exception("no mate " + a[2]);
                var md = (DistanceMateFeatureData)mf.GetDefinition();
                double old = md.Distance;
                md.Distance = old + double.Parse(a[3], Inv) / 1000.0;
                bool ok = mf.ModifyDefinition(md, doc, null);
                doc.ForceRebuild3(false);
                int er = Errors(doc, true);
                Console.WriteLine("MATEDIST\t" + a[2] + "\t" + Mm(old) + " -> " + Mm(((DistanceMateFeatureData)mf.GetDefinition()).Distance) + "\t" + ok + "\terrors " + er);
                if (ok && er == 0) { bool sv = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(sv ? "SAVED\t" + file : "SAVE FAILED\t" + e); }
                else { rc = 1; Console.WriteLine("NOT SAVED"); }
            }
            else if (cmd == "rename")
            {
                // rename <part> <old config> <new config> <file that uses it> ...: the users are opened first (so SOLIDWORKS
                // updates their references when the configuration is renamed), then the part; all are saved. Each user
                // must pass the same write rule as the part.
                var users = new List<ModelDoc2>();
                foreach (var u in a.Skip(4))
                {
                    string up = Path.GetFullPath(u);
                    bool uv = up.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase);
                    if (uv ? (!checkedOut || (File.GetAttributes(up) & FileAttributes.ReadOnly) != 0) : !up.StartsWith(@"C:\PDM-work\grout\", StringComparison.OrdinalIgnoreCase))
                        throw new Exception("not writable here: " + up);
                    int ut = up.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : up.EndsWith(".SLDDRW", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocDRAWING : (int)swDocumentTypes_e.swDocPART;
                    int ue = 0, uw = 0;
                    var ud = sw.OpenDoc6(up, ut, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref ue, ref uw);
                    if (ud == null || !string.Equals(ud.GetPathName(), up, StringComparison.OrdinalIgnoreCase)) throw new Exception("could not open " + up);
                    users.Add(ud);
                    Console.WriteLine("USER OPEN\t" + up);
                }
                try
                {
                    var cfg = (Configuration)doc.GetConfigurationByName(cfgArg);
                    if (cfg == null) throw new Exception("no configuration " + cfgArg);
                    cfg.Name = a[3];
                    Console.WriteLine("RENAMED\t" + cfgArg + " -> " + cfg.Name);
                    if (cfg.Name != a[3]) throw new Exception("rename not taken");
                    bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                    Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e);
                    foreach (var ud in users)
                    {
                        ud.ForceRebuild3(false);
                        int ue = 0, uw = 0;
                        bool uok = ud.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref ue, ref uw);
                        Console.WriteLine((uok ? "SAVED\t" : "SAVE FAILED\t") + ud.GetPathName() + "\terrors " + Errors(ud, true));
                        if (!uok) rc = 1;
                    }
                }
                finally { foreach (var ud in users) sw.CloseDoc(ud.GetTitle()); }
            }
            else if (cmd == "props")
            {
                // props <part> <reference part> <old>=<new>: material and custom properties (file and each configuration of
                // the same name) copied from the reference part, with <old> replaced by <new> in the values. A property whose
                // reference value does not contain <old> keeps this part's own value if it has one.
                var sub = a[3].Split('=');
                int re = 0, rw = 0;
                var refDoc = sw.OpenDoc6(Path.GetFullPath(a[2]), (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref re, ref rw);
                if (refDoc == null) throw new Exception("could not open " + a[2]);
                try
                {
                    string db;
                    string mat = ((PartDoc)refDoc).GetMaterialPropertyName2("", out db);
                    foreach (string c in (string[])doc.GetConfigurationNames())
                    {
                        ((PartDoc)doc).SetMaterialPropertyName2(c, db, mat);
                        Console.WriteLine("MATERIAL\t" + c + "\t" + ((PartDoc)doc).GetMaterialPropertyName2(c, out db) + " (" + db + ")");
                    }
                    var scopes = new List<string> { "" };
                    scopes.AddRange(((string[])refDoc.GetConfigurationNames()).Where(c => ((string[])doc.GetConfigurationNames()).Contains(c)));
                    foreach (var sc in scopes)
                    {
                        var src = refDoc.Extension.CustomPropertyManager[sc];
                        var dst = doc.Extension.CustomPropertyManager[sc];
                        foreach (string n in (string[])src.GetNames() ?? new string[0])
                        {
                            string val, res; bool wasRes, linked;
                            src.Get6(n, false, out val, out res, out wasRes, out linked);
                            int ty = src.GetType2(n);
                            string mine = null;
                            if (((string[])dst.GetNames() ?? new string[0]).Contains(n)) { string r2; bool w2, l2; dst.Get6(n, false, out mine, out r2, out w2, out l2); }
                            string v = val.Contains(sub[0]) ? val.Replace(sub[0], sub[1]) : (val.Contains("@" + Path.GetFileName(a[2])) ? val.Replace(Path.GetFileName(a[2]), Path.GetFileName(file)) : (mine ?? val));
                            int ar = dst.Add3(n, ty, v, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                            Console.WriteLine("PROP\t" + (sc == "" ? "(file)" : sc) + "\t" + n + "\t= " + v + "\t(" + ar + ")");
                        }
                    }
                }
                finally { sw.CloseDoc(refDoc.GetTitle()); }
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e);
            }
            else if (cmd == "kitseed")
            {
                // kitseed <kit> <seed> <new part> <config for new> <config for old> <new name for that> <new name for that>
                // (anchors step, Grant 22 Nov, option A): a second seed of <new part> with copies of the seed's mates, added to
                // the seed's sketch pattern; the old seed is suppressed in <config for new>, the new one in <config for old>;
                // then both configurations are renamed. Checks: the new seed sits exactly where the old one does, and in each
                // configuration the 4 anchor positions are the same as before, now of the right file.
                var asm = (AssemblyDoc)doc;
                string cNew = a[4], cOld = a[5], newPart = Path.GetFullPath(a[3]);
                Func<string, List<string>> anchors = cfgName =>
                {
                    doc.ShowConfiguration2(cfgName); doc.ForceRebuild3(false);
                    return ((object[])asm.GetComponents(true) ?? new object[0]).Cast<Component2>()
                        .Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && Path.GetFileName(c.GetPathName()).StartsWith("FAST-HST3", StringComparison.OrdinalIgnoreCase))
                        .Select(c => { var d = (double[])c.Transform2.ArrayData; return Path.GetFileName(c.GetPathName()) + " " + string.Join(" ", Enumerable.Range(0, 12).Select(i => Math.Round(d[i] * (i >= 9 ? 1000 : 1), 3).ToString("0.###", Inv))); })
                        .OrderBy(x => x.Substring(x.IndexOf(' '))).ToList();
                };
                var before = new Dictionary<string, List<string>> { { cNew, anchors(cNew) }, { cOld, anchors(cOld) } };
                foreach (var kv in before) foreach (var l in kv.Value) Console.WriteLine("BEFORE\t" + kv.Key + "\t" + l);
                doc.ShowConfiguration2(cNew); doc.ForceRebuild3(false);
                var seed = (Component2)asm.GetComponentByName(a[2]);
                if (seed == null) throw new Exception("no component " + a[2]);
                var seedXf = (MathTransform)seed.Transform2;
                var seedT = (double[])seedXf.ArrayData;
                string seedFile = Path.GetFileName(seed.GetPathName());
                int pe = 0, pw = 0;
                var partOpen = sw.OpenDoc6(newPart, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref pe, ref pw);   // AddComponent needs it in memory
                sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref pe);
                var mu = (MathUtility)sw.GetMathUtility();
                var inv = (MathTransform)seedXf.Inverse();
                string asmName = Path.GetFileNameWithoutExtension(file);
                var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
                Feature mg = null; for (var g = (Feature)doc.FirstFeature(); g != null && mg == null; g = (Feature)g.GetNextFeature()) if (g.GetTypeName2() == "MateGroup") mg = g;
                var toCopy = new List<Feature>();
                for (var s = (Feature)mg.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature())
                {
                    var mt = s.GetSpecificFeature2() as Mate2; if (mt == null || s.IsSuppressed()) continue;
                    bool has = false; for (int k = 0; k < mt.GetMateEntityCount(); k++) if (S(() => mt.MateEntity(k).ReferenceComponent.Name2) == seed.Name2) has = true;
                    if (has) toCopy.Add(s);
                }
                // the sketch the seed's origin is mated to, and its points at this configuration's anchor positions. A sketch
                // pattern for the new part could not be made through the API (22 Nov), so each new anchor gets the seed's 3 mates
                // on its own sketch point: the positions still follow the sketch in each configuration.
                SketchPoint seedPoint = null;
                foreach (var s in toCopy)
                {
                    var mt = (Mate2)s.GetSpecificFeature2();
                    for (int k = 0; k < 2; k++)
                    {
                        var r = mt.MateEntity(k).Reference as SketchPoint;
                        if (r != null && S(() => mt.MateEntity(k).ReferenceComponent.Name2) != seed.Name2) seedPoint = r;
                    }
                }
                if (seedPoint == null) throw new Exception("the seed origin is not mated to a sketch point");
                var posSk = (Sketch)seedPoint.GetSketch();
                var toModel = (MathTransform)posSk.ModelToSketchTransform.Inverse();
                var anchorPos = ((object[])asm.GetComponents(true) ?? new object[0]).Cast<Component2>()
                    .Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && Path.GetFileName(c.GetPathName()).StartsWith("FAST-HST3", StringComparison.OrdinalIgnoreCase))
                    .Select(c => ((double[])c.Transform2.ArrayData).Skip(9).Take(3).ToArray()).ToList();
                var targets = new List<SketchPoint>();
                foreach (SketchPoint sp in (object[])posSk.GetSketchPoints2() ?? new object[0])
                {
                    var mp = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { sp.X, sp.Y, sp.Z })).MultiplyTransform(toModel)).ArrayData;
                    if (anchorPos.Any(q => Math.Abs(q[0] - mp[0]) < 1e-6 && Math.Abs(q[1] - mp[1]) < 1e-6 && Math.Abs(q[2] - mp[2]) < 1e-6)
                        && !targets.Any(t => Math.Abs(t.X - sp.X) < 1e-9 && Math.Abs(t.Y - sp.Y) < 1e-9)) targets.Add(sp);
                }
                Console.WriteLine("TARGETS\t" + targets.Count + " sketch points at the " + anchorPos.Count + " anchor positions");
                if (targets.Count != anchorPos.Count) throw new Exception("sketch points and anchors do not match");
                var newComps = new List<Component2>();
                foreach (var tp in targets)
                {
                    var tm = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { tp.X, tp.Y, tp.Z })).MultiplyTransform(toModel)).ArrayData;
                    var nc = asm.AddComponent5(newPart, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig, "", false, "", tm[0], tm[1], tm[2]);
                    if (nc == null) throw new Exception("could not add " + newPart);
                    var newPartDoc = (ModelDoc2)nc.GetModelDoc2();
                    int made = 0;
                    foreach (var s in toCopy)
                    {
                        var mt = (Mate2)s.GetSpecificFeature2();
                        int k = S(() => mt.MateEntity(0).ReferenceComponent.Name2) == seed.Name2 ? 0 : 1;
                        var mine = mt.MateEntity(k); var other = mt.MateEntity(1 - k);
                        doc.ClearSelection2(true);
                        var oref = other.Reference;
                        bool o1 = oref is SketchPoint ? tp.Select4(true, sd) : oref is Feature ? ((Feature)oref).Select2(true, 0) : ((Entity)oref).Select4(true, sd);
                        bool o2;
                        if (mine.Reference is SketchPoint || mine.ReferenceType2 == (int)swSelectType_e.swSelEXTSKETCHPOINTS)
                            o2 = doc.Extension.SelectByID2("Point1@Origin@" + nc.Name2 + "@" + asmName, "EXTSKETCHPOINT", 0, 0, 0, true, 0, null, 0);
                        else
                        {
                            var ep = (double[])mine.EntityParams;   // plane: point xyz, normal xyz (assembly)
                            var p = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { ep[0], ep[1], ep[2] })).MultiplyTransform(inv)).ArrayData;
                            var nv = (double[])((MathVector)((MathVector)mu.CreateVector(new[] { ep[3], ep[4], ep[5] })).MultiplyTransform(inv)).ArrayData;
                            Face2 match = null;
                            foreach (Body2 b in (object[])((PartDoc)newPartDoc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                                foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                                {
                                    var su = (Surface)f.GetSurface(); if (!su.IsPlane()) continue;
                                    var pp = (double[])su.PlaneParams;
                                    double dot = pp[0] * nv[0] + pp[1] * nv[1] + pp[2] * nv[2];
                                    double off = (p[0] - pp[3]) * pp[0] + (p[1] - pp[4]) * pp[1] + (p[2] - pp[5]) * pp[2];
                                    if (Math.Abs(Math.Abs(dot) - 1) < 1e-6 && Math.Abs(off) < 1e-6) { match = f; break; }
                                }
                            if (match == null) throw new Exception("no matching face on the new part for mate " + s.Name);
                            var ent = (Entity)nc.GetCorrespondingEntity(match);
                            o2 = ent != null && ent.Select4(true, sd);
                        }
                        int er;
                        var nm = asm.AddMate5(mt.Type, mt.Alignment, false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out er);
                        if (nm != null) made++;
                        else Console.WriteLine("MATE FAILED\t" + nc.Name2 + "\t" + s.Name + "\t" + er + "\tselected " + o1 + "/" + o2);
                    }
                    doc.ClearSelection2(true);
                    doc.ForceRebuild3(false);
                    var nT = (double[])nc.Transform2.ArrayData;
                    double drot = Enumerable.Range(0, 9).Max(i => Math.Abs(nT[i] - seedT[i]));
                    double dpos = Enumerable.Range(0, 3).Max(i => Math.Abs(nT[9 + i] - tm[i])) * 1000;
                    Console.WriteLine("ADDED\t" + nc.Name2 + "\tmates " + made + "/" + toCopy.Count + "\trotation vs seed " + drot.ToString("0.######", Inv) + "\tposition vs its sketch point " + dpos.ToString("0.###", Inv) + " mm");
                    if (made != toCopy.Count || drot > 1e-6 || dpos > 1e-3) throw new Exception("a new anchor is not where it should be; nothing saved");
                    newComps.Add(nc);
                }
                // suppress per configuration: the old seed and its sketch pattern (the pattern's own instances do not follow
                // a suppressed seed, 22 Nov), in the configuration that now uses the new part
                doc.ShowConfiguration2(cNew); doc.ForceRebuild3(false);
                Console.WriteLine("SUPPRESS\t" + cNew + "\t" + seed.Name2 + "\t" + ((Component2)asm.GetComponentByName(seed.Name2)).SetSuppression2((int)swComponentSuppressionState_e.swComponentSuppressed));
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                    if (f.GetTypeName2() == "LocalSketchPattern" && ((object[])f.GetParents() ?? new object[0]).Cast<Feature>().Any(q => q.Name == seed.Name2))
                        Console.WriteLine("SUPPRESS\t" + cNew + "\t" + f.Name + "\t" + f.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swThisConfiguration, null));
                doc.ForceRebuild3(false);
                doc.ShowConfiguration2(cOld); doc.ForceRebuild3(false);
                foreach (var nc in newComps) Console.WriteLine("SUPPRESS\t" + cOld + "\t" + nc.Name2 + "\t" + ((Component2)asm.GetComponentByName(nc.Name2)).SetSuppression2((int)swComponentSuppressionState_e.swComponentSuppressed));
                // check, then rename
                var after = new Dictionary<string, List<string>> { { cNew, anchors(cNew) }, { cOld, anchors(cOld) } };
                bool good = true;
                foreach (var kv in after)
                {
                    foreach (var l in kv.Value) Console.WriteLine("AFTER\t" + kv.Key + "\t" + l);
                    var bpos = before[kv.Key].Select(x => x.Substring(x.IndexOf(' '))).ToList();
                    var apos = kv.Value.Select(x => x.Substring(x.IndexOf(' '))).ToList();
                    string want = kv.Key == cNew ? Path.GetFileName(newPart) : seedFile;
                    int right = kv.Value.Count(x => x.StartsWith(want, StringComparison.OrdinalIgnoreCase));
                    bool same = bpos.Count == apos.Count && bpos.Zip(apos, (x, y) => x == y).All(z => z);
                    Console.WriteLine("CHECK\t" + kv.Key + "\t" + kv.Value.Count + " anchors, " + right + " of " + want + "\tpositions same " + same + "\terrors " + Errors(doc, true));
                    if (!same || right != kv.Value.Count || kv.Value.Count != before[kv.Key].Count) good = false;
                }
                if (!good) throw new Exception("check failed; nothing saved");
                ((Configuration)doc.GetConfigurationByName(cNew)).Name = a[6];
                ((Configuration)doc.GetConfigurationByName(cOld)).Name = a[7];
                Console.WriteLine("CONFIGS\t" + string.Join(", ", (string[])doc.GetConfigurationNames()));
                int er2 = 0; foreach (string c in (string[])doc.GetConfigurationNames()) { doc.ShowConfiguration2(c); doc.ForceRebuild3(false); er2 += Errors(doc, true); }
                if (er2 > 0) throw new Exception("rebuild errors; nothing saved");
                bool sv = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
                Console.WriteLine(sv ? "SAVED\t" + file : "SAVE FAILED\t" + e);
                if (!sv) rc = 1;
            }
            else if (cmd == "resave")
            {
                // resave <asm>: open, resolve, rebuild and save with SOLIDWORKS (after a Document Manager reference change,
                // so assemblies above it see the change), only if there are no rebuild errors
                if (type == (int)swDocumentTypes_e.swDocASSEMBLY) ((AssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                doc.ForceRebuild3(false);
                int er = Errors(doc, true);
                Console.WriteLine("REBUILT\terrors " + er);
                if (er == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!ok) rc = 1; }
                else { Console.WriteLine("NOT SAVED"); rc = 1; }
            }
            else if (cmd == "breaklink")
            {
                // breaklink <part>: break the link of every 3D Interconnect (imported) feature to its source file, save
                int n = 0;
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                    if (f.Is3DInterconnectFeature) { int b = f.BreakLink(true, true); Console.WriteLine("BREAK LINK\t" + f.Name + "\tresult " + b); n++; }
                doc.ForceRebuild3(false);
                int er = Errors(doc, true);
                Console.WriteLine("LINKS BROKEN\t" + n + "\terrors " + er);
                if (er == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!ok) rc = 1; }
                else { rc = 1; Console.WriteLine("NOT SAVED"); }
            }
            else if (cmd == "remate")
            {
                // remate <asm> <kit component> <old anchor file> <new anchor file> [dry] (anchors step, 22 Nov): every mate of
                // <asm> that holds an <old anchor file> instance inside <kit component> is moved to the <new anchor file>
                // instance at the same place in that kit, on the face with the same geometry (part axes). "dry" only lists.
                bool dry = a.Length > 5 && a[5] == "dry";
                var asm = (AssemblyDoc)doc; asm.ResolveAllLightWeightComponents(false); doc.ForceRebuild3(false);
                var mu = (MathUtility)sw.GetMathUtility();
                var all = ((object[])asm.GetComponents(false) ?? new object[0]).Cast<Component2>().ToList();
                var newInKit = all.Where(c => c.Name2.StartsWith(a[2] + "/", StringComparison.OrdinalIgnoreCase) && Path.GetFileName(c.GetPathName()).Equals(a[4], StringComparison.OrdinalIgnoreCase)
                    && c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed).ToList();
                Console.WriteLine("NEW ANCHORS IN KIT\t" + newInKit.Count);
                Feature mg = null; for (var g = (Feature)doc.FirstFeature(); g != null && mg == null; g = (Feature)g.GetNextFeature()) if (g.GetTypeName2() == "MateGroup") mg = g;
                int fixedN = 0, failed = 0;
                var plan = new List<object[]>();
                for (var s = (Feature)mg.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature())
                {
                    var mt = s.GetSpecificFeature2() as Mate2; if (mt == null) continue;
                    int k = -1;
                    for (int i = 0; i < mt.GetMateEntityCount(); i++)
                    {
                        string cn = S(() => mt.MateEntity(i).ReferenceComponent.Name2);
                        if (cn.StartsWith(a[2] + "/", StringComparison.OrdinalIgnoreCase) && cn.Substring(a[2].Length + 1).StartsWith(Path.GetFileNameWithoutExtension(a[3]), StringComparison.OrdinalIgnoreCase)) k = i;
                    }
                    if (k < 0) continue;
                    var me = mt.MateEntity(k);
                    var oldComp = me.ReferenceComponent;
                    var ep = me.EntityParams as double[];
                    Console.WriteLine("MATE\t" + s.Name + "\t" + (swMateType_e)mt.Type + "\ton " + S(() => oldComp.Name2) + "\tentity type " + me.ReferenceType2 + "\tparams " + (ep == null ? "none" : string.Join(" ", ep.Select(v => Math.Round(v * 1000, 3).ToString(Inv)))));
                    if (ep == null) { failed++; continue; }
                    var oT = (double[])oldComp.Transform2.ArrayData;
                    var target = newInKit.FirstOrDefault(c => { var t = (double[])c.Transform2.ArrayData; return Enumerable.Range(0, 12).All(i => Math.Abs(t[i] - oT[i]) < 1e-6); });
                    if (target == null) { Console.WriteLine("  PROBLEM\tno new anchor at the same place"); failed++; continue; }
                    var inv = (MathTransform)((MathTransform)oldComp.Transform2).Inverse();   // the mate's stored geometry is in the old anchor's place; both parts share one frame
                    var p = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { ep[0], ep[1], ep[2] })).MultiplyTransform(inv)).ArrayData;
                    var nv = (double[])((MathVector)((MathVector)mu.CreateVector(new[] { ep[3], ep[4], ep[5] })).MultiplyTransform(inv)).ArrayData;
                    double rad = ep.Length > 6 ? ep[6] : 0;
                    Face2 match = null;
                    foreach (Body2 b in (object[])((PartDoc)target.GetModelDoc2()).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
                        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
                        {
                            var su = (Surface)f.GetSurface();
                            if (su.IsPlane() && me.ReferenceType2 == (int)swSelectType_e.swSelFACES && rad == 0)
                            {
                                var pp = (double[])su.PlaneParams;
                                double dot = pp[0] * nv[0] + pp[1] * nv[1] + pp[2] * nv[2];
                                double off = (p[0] - pp[3]) * pp[0] + (p[1] - pp[4]) * pp[1] + (p[2] - pp[5]) * pp[2];
                                if (Math.Abs(Math.Abs(dot) - 1) < 1e-6 && Math.Abs(off) < 1e-6) { match = f; break; }
                            }
                            if (su.IsCylinder() && rad > 0)
                            {
                                var cp = (double[])su.CylinderParams;
                                double dot = cp[3] * nv[0] + cp[4] * nv[1] + cp[5] * nv[2];
                                double[] d = { p[0] - cp[0], p[1] - cp[1], p[2] - cp[2] };
                                double along = d[0] * cp[3] + d[1] * cp[4] + d[2] * cp[5];
                                double dist = Math.Sqrt(Math.Max(0, d[0] * d[0] + d[1] * d[1] + d[2] * d[2] - along * along));
                                if (Math.Abs(Math.Abs(dot) - 1) < 1e-6 && dist < 1e-6 && Math.Abs(cp[6] - rad) < 1e-6) { match = f; break; }
                            }
                        }
                    if (match == null) { Console.WriteLine("  PROBLEM\tno face with the same geometry on " + target.Name2); failed++; continue; }
                    var ent = target.GetCorrespondingEntity(match);
                    Console.WriteLine("  TARGET\t" + target.Name2 + "\t" + (match.GetSurface() as Surface).IsCylinder());
                    if (dry) continue;
                    plan.Add(new object[] { s, mt.MateEntity(1 - k).Reference, mt.Type, mt.Alignment, ent });
                }
                // changing a mate's entities through its definition faulted (22 Nov): each mate is replaced by a new one of the
                // same type and alignment between the same plate entity and the new anchor's face
                var sdx = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
                foreach (var pl in plan)
                {
                    var s = (Feature)pl[0]; string oldName = s.Name;
                    doc.ClearSelection2(true);
                    bool del = s.Select2(false, 0) && doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                    doc.ClearSelection2(true);
                    var oref = pl[1];
                    bool o1 = oref is Feature ? ((Feature)oref).Select2(true, 0) : ((Entity)oref).Select4(true, sdx);
                    bool o2 = ((Entity)pl[4]).Select4(true, sdx);
                    int er0;
                    var nm = ((AssemblyDoc)doc).AddMate5((int)pl[2], (int)pl[3], false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out er0);
                    doc.ClearSelection2(true);
                    Console.WriteLine("  REPLACED\t" + oldName + "\tdeleted " + del + "\tselected " + o1 + "/" + o2 + "\t" + (nm == null ? "FAILED " + er0 : "new " + ((Feature)nm).Name));
                    if (del && nm != null) fixedN++; else failed++;
                }
                doc.ForceRebuild3(false);
                int er = Errors(doc, true);
                Console.WriteLine("REMATE\tre-attached " + fixedN + "\tfailed " + failed + "\terrors " + er);
                if (!dry && fixedN > 0 && failed == 0 && er == 0) { bool sv = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(sv ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!sv) rc = 1; }
                else if (!dry) { Console.WriteLine("NOT SAVED"); rc = 1; }
            }
            else if (cmd == "kitcfg")
            {
                // kitcfg <kit> <base config> <new config> <dim>=<mm> ... : a copy of <base config> (same components and
                // suppression), then the dimensions set in the new configuration only; lists the anchor positions of both
                var cm = doc.ConfigurationManager;
                doc.ShowConfiguration2(cfgArg);
                if (((Configuration)doc.GetActiveConfiguration()).Name != cfgArg) throw new Exception("no configuration " + cfgArg);
                var nc = cm.AddConfiguration2(a[3], "", "", 0, "", "", true);
                if (nc == null) throw new Exception("could not add " + a[3]);
                doc.ShowConfiguration2(a[3]);
                foreach (var kv in a.Skip(4))
                {
                    var p = kv.Split('=');
                    var dim = (Dimension)doc.Parameter(p[0]);
                    if (dim == null) throw new Exception("no dimension " + p[0]);
                    dim.SetSystemValue3(double.Parse(p[1], Inv) / 1000.0, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                }
                doc.ForceRebuild3(false);
                var asm = (AssemblyDoc)doc;
                int er = 0;
                foreach (string c in (string[])doc.GetConfigurationNames())
                {
                    doc.ShowConfiguration2(c); doc.ForceRebuild3(false); er += Errors(doc, true);
                    var pos = ((object[])asm.GetComponents(true) ?? new object[0]).Cast<Component2>()
                        .Where(k => k.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed)
                        .Select(k => { var d = (double[])k.Transform2.ArrayData; return Path.GetFileNameWithoutExtension(k.GetPathName()) + " (" + Mm(d[9]) + ", " + Mm(d[10]) + ", " + Mm(d[11]) + ")"; });
                    Console.WriteLine("CONFIG\t" + c + "\t" + string.Join("  ", pos));
                }
                if (er == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!ok) rc = 1; }
                else { Console.WriteLine("NOT SAVED\terrors"); rc = 1; }
            }
            else if (cmd == "placekits")
            {
                // placekits <asm> <kit> <kit config> <plate file> <plate config|*> <concrete up|down> <hole dia mm> [dry]
                // (LS-08A-11 anchors, Grant 22 Nov): one kit on every <plate file> component (in that configuration) that has
                // no kit yet: kit Top Plane coincident with the plate face on the nut side (away from the concrete), and two
                // diagonal points of the kit's Anchor_Position_Sketch concentric with the matching holes. Mated only to the
                // kit's own sketch and plane, never to anchor instances. Checks every kit lands exactly on its 4 holes.
                bool dry = a.Length > 8 && a[8] == "dry";
                var asm = (AssemblyDoc)doc; asm.ResolveAllLightWeightComponents(false); doc.ForceRebuild3(false);
                var mu = (MathUtility)sw.GetMathUtility();
                string kitPath = Path.GetFullPath(a[2]), kitCfg = a[3], plateFile = a[4], plateCfg = a[5];
                double[] away = a[6] == "down" ? new[] { 0.0, 1.0, 0.0 } : new[] { 0.0, -1.0, 0.0 };   // concrete below: nut side faces up
                double holeR = double.Parse(a[7], Inv) / 2000.0;
                int ke = 0, kw = 0;
                var kitDoc = sw.OpenDoc6(kitPath, (int)swDocumentTypes_e.swDocASSEMBLY, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, kitCfg, ref ke, ref kw);
                if (kitDoc == null) throw new Exception("could not open the kit " + kitPath);
                kitDoc.ShowConfiguration2(kitCfg); kitDoc.ForceRebuild3(false);
                // the kit's anchor points (kit axes) in that configuration, from its position sketch
                Feature skf = null; for (var f = (Feature)kitDoc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature()) if (f.Name == "Anchor_Position_Sketch") skf = f;
                var ksk = (Sketch)skf.GetSpecificFeature2();
                var kToModel = (MathTransform)ksk.ModelToSketchTransform.Inverse();
                var anchorsInKit = ((object[])((AssemblyDoc)kitDoc).GetComponents(true) ?? new object[0]).Cast<Component2>()
                    .Where(k => k.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed && Path.GetFileName(k.GetPathName()).StartsWith("FAST-HST3", StringComparison.OrdinalIgnoreCase))
                    .Select(k => ((double[])k.Transform2.ArrayData).Skip(9).Take(3).ToArray()).ToList();
                var kpts = new List<SketchPoint>(); var kxyz = new List<double[]>();
                foreach (SketchPoint sp in (object[])ksk.GetSketchPoints2() ?? new object[0])
                {
                    var mp = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new[] { sp.X, sp.Y, sp.Z })).MultiplyTransform(kToModel)).ArrayData;
                    if (anchorsInKit.Any(q => Math.Abs(q[0] - mp[0]) < 1e-7 && Math.Abs(q[1] - mp[1]) < 1e-7 && Math.Abs(q[2] - mp[2]) < 1e-7) && !kxyz.Any(q => Math.Abs(q[0] - mp[0]) < 1e-7 && Math.Abs(q[2] - mp[2]) < 1e-7)) { kpts.Add(sp); kxyz.Add(mp); }
                }
                // the sketch points' selection names in the kit ("PointN@Anchor_Position_Sketch"), found by trying the IDs
                var kNames = new List<string>();
                foreach (var sp in kpts)
                {
                    string found = null;
                    var ids = (int[])sp.GetID();
                    foreach (var cand in ids.Select(i => "Point" + i).Distinct())
                    {
                        kitDoc.ClearSelection2(true);
                        if (!kitDoc.Extension.SelectByID2(cand + "@Anchor_Position_Sketch", "SKETCHPOINT", 0, 0, 0, false, 0, null, 0)) continue;
                        var got = ((SelectionMgr)kitDoc.SelectionManager).GetSelectedObject6(1, -1) as SketchPoint;
                        if (got != null && Math.Abs(got.X - sp.X) < 1e-9 && Math.Abs(got.Y - sp.Y) < 1e-9) { found = cand; break; }
                    }
                    kitDoc.ClearSelection2(true);
                    if (found == null) throw new Exception("no selection name for a kit sketch point (IDs " + string.Join(",", ids) + ")");
                    kNames.Add(found);
                }
                Console.WriteLine("KIT POINTS\t" + string.Join(", ", kNames));
                Console.WriteLine("KIT\t" + kitCfg + "\tanchor points " + string.Join("  ", kxyz.Select(q => "(" + Mm(q[0]) + ", " + Mm(q[1]) + ", " + Mm(q[2]) + ")")));
                if (kxyz.Count != 4 || anchorsInKit.Count != 4) throw new Exception("the kit configuration does not show 4 anchors on 4 sketch points");
                sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref ke);
                string staName = Path.GetFileNameWithoutExtension(file);
                var all = ((object[])asm.GetComponents(false) ?? new object[0]).Cast<Component2>().ToList();
                var kitsHere = all.Where(c => Path.GetFileName(c.GetPathName()).Equals(Path.GetFileName(kitPath), StringComparison.OrdinalIgnoreCase)).ToList();
                var plates = all.Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed
                    && Path.GetFileName(c.GetPathName()).Equals(plateFile, StringComparison.OrdinalIgnoreCase)
                    && (plateCfg == "*" || c.ReferencedConfiguration == plateCfg)).ToList();
                Console.WriteLine("PLATES\t" + plates.Count + " x " + plateFile + " <" + plateCfg + ">\tkits already in the assembly " + kitsHere.Count);
                var sd = ((SelectionMgr)doc.SelectionManager).CreateSelectData();
                int placed = 0, failed = 0, skipped = 0;
                foreach (var pl in plates)
                {
                    var T = (MathTransform)pl.Transform2;
                    object info;
                    var bodies = (object[])pl.GetBodies3((int)swBodyType_e.swSolidBody, out info) ?? new object[0];
                    var holes = new List<Tuple<Face2, double[]>>();   // face, centre (asm)
                    var faces = new List<Face2>();
                    foreach (Body2 b in bodies) foreach (Face2 f in (object[])b.GetFaces() ?? new object[0]) faces.Add(f);
                    Func<double[], double[]> P = q => (double[])((MathPoint)((MathPoint)mu.CreatePoint(q)).MultiplyTransform(T)).ArrayData;
                    Func<double[], double[]> V = q => (double[])((MathVector)((MathVector)mu.CreateVector(q)).MultiplyTransform(T)).ArrayData;
                    foreach (var f in faces)
                    {
                        var su = (Surface)f.GetSurface(); if (!su.IsCylinder()) continue;
                        var cp = (double[])su.CylinderParams; if (Math.Abs(cp[6] - holeR) > 1e-6) continue;
                        var ax = V(new[] { cp[3], cp[4], cp[5] });
                        if (Math.Abs(Math.Abs(ax[0] * away[0] + ax[1] * away[1] + ax[2] * away[2]) - 1) > 1e-6) continue;
                        var bx = (double[])f.GetBox();
                        holes.Add(Tuple.Create(f, P(new[] { (bx[0] + bx[3]) / 2, (bx[1] + bx[4]) / 2, (bx[2] + bx[5]) / 2 })));
                    }
                    // the nut-side face: planar, outward normal = away, the holes pass through it
                    Face2 nutFace = null; double[] nutPt = null;
                    foreach (var f in faces)
                    {
                        var su = (Surface)f.GetSurface(); if (!su.IsPlane()) continue;
                        var pp = (double[])su.PlaneParams;
                        var n = V(new[] { pp[0], pp[1], pp[2] }); double sg = f.FaceInSurfaceSense() ? -1 : 1;
                        n = n.Select(v => v * sg).ToArray();
                        if (Math.Abs(n[0] * away[0] + n[1] * away[1] + n[2] * away[2] - 1) > 1e-6) continue;
                        var p0 = P(new[] { pp[3], pp[4], pp[5] });
                        if (holes.Count == 0) continue;
                        double dmax = holes.Max(h => Math.Abs((h.Item2[0] - p0[0]) * away[0] + (h.Item2[1] - p0[1]) * away[1] + (h.Item2[2] - p0[2]) * away[2]));
                        int touching = 0;
                        foreach (Edge ed in (object[])f.GetEdges() ?? new object[0]) foreach (var h in holes) if (((object[])h.Item1.GetEdges() ?? new object[0]).Contains(ed)) touching++;
                        if (dmax < 0.011 && touching >= 4) { nutFace = f; nutPt = p0; }
                    }
                    string pname = pl.Name2;
                    if (holes.Count != 4 || nutFace == null) { Console.WriteLine("PROBLEM\t" + pname + "\tholes " + holes.Count + "\tnut face " + (nutFace != null)); failed++; continue; }
                    // centre of the hole pattern on the nut face
                    var c0 = new double[3]; foreach (var h in holes) for (int i = 0; i < 3; i++) c0[i] += h.Item2[i] / 4;
                    double off = (c0[0] - nutPt[0]) * away[0] + (c0[1] - nutPt[1]) * away[1] + (c0[2] - nutPt[2]) * away[2];
                    for (int i = 0; i < 3; i++) c0[i] -= off * away[i];
                    // a kit already on this plate?
                    if (kitsHere.Any(k => { var d = (double[])k.Transform2.ArrayData; return Math.Abs(d[9] - c0[0]) < 1e-4 && Math.Abs(d[10] - c0[1]) < 1e-4 && Math.Abs(d[11] - c0[2]) < 1e-4; }))
                    { Console.WriteLine("SKIP\t" + pname + "\ta kit is already there"); skipped++; continue; }
                    // kit axes: Y = away; X one of the horizontal directions so that the 4 kit points land on the 4 holes
                    double[] Y = away; double[] X = null, Z = null;
                    foreach (var xc in new[] { new[] { 1.0, 0, 0 }, new[] { 0, 0, 1.0 }, new[] { -1.0, 0, 0 }, new[] { 0, 0, -1.0 } })
                    {
                        var zc = new[] { xc[1] * Y[2] - xc[2] * Y[1], xc[2] * Y[0] - xc[0] * Y[2], xc[0] * Y[1] - xc[1] * Y[0] };
                        bool all4 = kxyz.All(k => holes.Any(h => Enumerable.Range(0, 3).All(i => Math.Abs(c0[i] + k[0] * xc[i] + k[1] * Y[i] + k[2] * zc[i] - (h.Item2[i] - ((h.Item2[0] - c0[0]) * away[0] + (h.Item2[1] - c0[1]) * away[1] + (h.Item2[2] - c0[2]) * away[2]) * away[i])) < 2e-4)));
                        if (all4) { X = xc; Z = zc; break; }
                    }
                    if (X == null) { Console.WriteLine("PROBLEM\t" + pname + "\tthe kit's 4 anchor points do not fit these 4 holes in any orientation"); failed++; continue; }
                    if (dry) { Console.WriteLine("WOULD PLACE\t" + pname + "\tat (" + Mm(c0[0]) + ", " + Mm(c0[1]) + ", " + Mm(c0[2]) + ")"); placed++; continue; }
                    var kc = asm.AddComponent5(kitPath, (int)swAddComponentConfigOptions_e.swAddComponentConfigOptions_CurrentSelectedConfig, "", false, kitCfg, c0[0], c0[1], c0[2]);
                    if (kc == null) { Console.WriteLine("PROBLEM\t" + pname + "\tcould not add the kit"); failed++; continue; }
                    var arr = new double[] { X[0], X[1], X[2], Y[0], Y[1], Y[2], Z[0], Z[1], Z[2], c0[0], c0[1], c0[2], 1, 0, 0, 0 };
                    kc.Transform2 = (MathTransform)mu.CreateTransform(arr);
                    if (kc.ReferencedConfiguration != kitCfg) kc.ReferencedConfiguration = kitCfg;
                    // mates: plane to nut face, and two diagonal sketch points concentric with their holes
                    int made = 0;
                    doc.ClearSelection2(true);
                    bool s1 = doc.Extension.SelectByID2("Top Plane@" + kc.Name2 + "@" + staName, "PLANE", 0, 0, 0, false, 0, null, 0);
                    bool s2 = ((Entity)nutFace).Select4(true, sd);
                    int er;
                    var m1 = asm.AddMate5((int)swMateType_e.swMateCOINCIDENT, (int)swMateAlign_e.swMateAlignCLOSEST, false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out er);
                    if (m1 != null) made++; else Console.WriteLine("  MATE FAILED\tplane\t" + er + "\t" + s1 + "/" + s2);
                    var diag = new[] { 0, Enumerable.Range(1, 3).OrderByDescending(j => Math.Pow(kxyz[j][0] - kxyz[0][0], 2) + Math.Pow(kxyz[j][2] - kxyz[0][2], 2)).First() };
                    foreach (int j in diag)
                    {
                        var kw3 = Enumerable.Range(0, 3).Select(i => c0[i] + kxyz[j][0] * X[i] + kxyz[j][1] * Y[i] + kxyz[j][2] * Z[i]).ToArray();
                        var hole = holes.OrderBy(h => Enumerable.Range(0, 3).Sum(i => Math.Pow(h.Item2[i] - kw3[i] - ((h.Item2[0] - kw3[0]) * away[0] + (h.Item2[1] - kw3[1]) * away[1] + (h.Item2[2] - kw3[2]) * away[2]) * away[i], 2))).First();
                        object ent = null;
                        doc.ClearSelection2(true);
                        bool t1 = doc.Extension.SelectByID2(kNames[j] + "@Anchor_Position_Sketch@" + kc.Name2 + "@" + staName, "EXTSKETCHPOINT", 0, 0, 0, false, 0, null, 0);
                        bool t2 = ((Entity)hole.Item1).Select4(true, sd);
                        var m2 = asm.AddMate5((int)swMateType_e.swMateCONCENTRIC, (int)swMateAlign_e.swMateAlignCLOSEST, false, 0, 0, 0, 0, 0, 0, 0, 0, false, false, 0, out er);
                        if (m2 != null) made++; else Console.WriteLine("  MATE FAILED\tpoint " + j + "\t" + er + "\t" + t1 + "/" + t2 + "\t" + kNames[j]);
                    }
                    doc.ClearSelection2(true);
                    doc.ForceRebuild3(false);
                    var d2 = (double[])kc.Transform2.ArrayData;
                    double dev = Enumerable.Range(0, 12).Max(i => Math.Abs(d2[i] - arr[i]) * (i >= 9 ? 1000 : 1));
                    // the kit's anchors on the plate's holes
                    int onHole = 0;
                    foreach (var k in kxyz) { var kw2 = Enumerable.Range(0, 3).Select(i => d2[9 + i] + k[0] * d2[i] + k[1] * d2[3 + i] + k[2] * d2[6 + i]).ToArray(); if (holes.Any(h => Enumerable.Range(0, 3).All(i => Math.Abs(h.Item2[i] - kw2[i] - ((h.Item2[0] - kw2[0]) * away[0] + (h.Item2[1] - kw2[1]) * away[1] + (h.Item2[2] - kw2[2]) * away[2]) * away[i]) < 2e-4))) onHole++; }
                    Console.WriteLine("PLACED\t" + kc.Name2 + " <" + kc.ReferencedConfiguration + ">\ton " + pname + "\tmates " + made + "/3\tmoved by the mates " + dev.ToString("0.###", Inv) + "\tanchors on holes " + onHole + "/4\tat (" + Mm(d2[9]) + ", " + Mm(d2[10]) + ", " + Mm(d2[11]) + ")");
                    if (made == 3 && dev < 0.01 && onHole == 4) placed++; else failed++;
                }
                sw.CloseDoc(kitDoc.GetTitle());
                doc.ForceRebuild3(false);
                int er2 = Errors(doc, true);
                Console.WriteLine("PLACEKITS\tplaced " + placed + "\tskipped " + skipped + "\tfailed " + failed + "\terrors " + er2);
                if (!dry && placed > 0 && failed == 0 && er2 == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!ok) rc = 1; }
                else if (!dry) { Console.WriteLine("NOT SAVED"); rc = 1; }
            }
            else if (cmd == "plane")
            {
                // plane <asm> <plane> <offset mm>: make the plane a distance <offset> from its reference (upwards, +Y of the
                // file), check where it ends up, save (rehearsal copies only: setdims's root check covers "plane" too)
                var f = FeatureNamed(doc, a[2]);
                var def = (RefPlaneFeatureData)f.GetDefinition();
                def.AccessSelections(doc, null);
                Console.WriteLine("PLANE\t" + a[2] + "\tconstraint " + def.Constraint[0] + "\tdistance " + Mm(def.AngleOrDistance[0]) + "\treversed " + def.ReversedReferenceDirection[0] + "\tat Y " + PlaneY(f));
                double want = double.Parse(a[3], Inv) / 1000.0;
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    if (attempt == 1) { def = (RefPlaneFeatureData)f.GetDefinition(); def.AccessSelections(doc, null); def.ReversedReferenceDirection[0] = !def.ReversedReferenceDirection[0]; }
                    def.Constraint[0] = (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance;
                    def.AngleOrDistance[0] = want;
                    bool ok = f.ModifyDefinition(def, doc, null);
                    doc.ForceRebuild3(false);
                    double y = PlaneYv(f);
                    Console.WriteLine("PLANE\t" + a[2] + "\tModifyDefinition " + ok + "\tnow at Y " + Mm(y));
                    if (Math.Abs(y - want) < 1e-6) break;
                    if (attempt == 1) { Console.WriteLine("PROBLEM\tplane not at +" + a[3]); rc = 1; }
                }
                Console.WriteLine("WHATSWRONG\t" + Errors(doc, true) + " errors");
                if (rc == 0) { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); if (!ok) rc = 1; }
            }
            else if (cmd == "addcfg")
            {
                // addcfg <part> <base config> <new config>: a derived configuration of the base (it keeps everything of
                // the base until a value is set in it); shows the parent chain
                var cm = doc.ConfigurationManager;
                var baseCfg = (Configuration)doc.GetConfigurationByName(cfgArg);
                if (baseCfg == null) throw new Exception("no configuration " + cfgArg);
                var par = baseCfg.GetParent() as Configuration;
                Console.WriteLine("BASE\t" + cfgArg + "\tparent " + (par == null ? "-" : par.Name) + "\tderived " + baseCfg.IsDerived());
                doc.ShowConfiguration2(cfgArg);
                var nc = cm.AddConfiguration2(a[3], "grout +10 (rehearsal)", "", (int)swConfigurationOptions2_e.swConfigOption_DontActivate, cfgArg, "", false);
                Console.WriteLine("ADDED\t" + (nc == null ? "FAILED" : nc.Name + "\tparent " + ((Configuration)nc.GetParent()).Name));
                if (nc == null) rc = 1;
                else { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); }
            }
            else if (cmd == "setcomp")
            {
                // setcomp <asm> <asm config> <component name> <new referenced config>
                doc.ShowConfiguration2(cfgArg);
                if (((Configuration)doc.GetActiveConfiguration()).Name != cfgArg) throw new Exception("no configuration " + cfgArg);
                var comp = (Component2)((AssemblyDoc)doc).GetComponentByName(a[3]);
                if (comp == null) throw new Exception("no component " + a[3]);
                string old = comp.ReferencedConfiguration;
                comp.ReferencedConfiguration = a[4];
                doc.ForceRebuild3(false);
                Console.WriteLine("COMPONENT\t" + a[3] + "\t" + old + " -> " + comp.ReferencedConfiguration + "\terrors " + Errors(doc, true));
                if (comp.ReferencedConfiguration != a[4]) { rc = 1; Console.WriteLine("PROBLEM\tnot changed"); }
                else { bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w); Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + e); }
            }
            else throw new Exception("unknown command " + cmd);
        }
        catch (Exception ex) { Console.WriteLine("FAILED\t" + ex.Message); rc = 3; }
        finally { sw.CloseDoc(doc.GetTitle()); }
        Console.WriteLine("DONE\texit " + rc + "\t" + (int)(DateTime.Now - t0).TotalSeconds + " s");
        return rc;
    }
}
