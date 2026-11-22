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
        bool write = cmd == "setdims" || cmd == "plane" || cmd == "addcfg" || cmd == "setcomp" || (cmd == "eq" && a.Length > 2) || cmd == "gv" || cmd == "feet" || cmd == "delcomp" || cmd == "matedist" || cmd == "rename";
        if (file.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0)
        { Console.Error.WriteLine("REFUSED: never the Dropbox files or .pdm: " + file); return 2; }
        bool inVault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (write)
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
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        int e = 0, w = 0;
        var t0 = DateTime.Now;
        string cfgArg = a.Length > 2 && cmd != "plane" && cmd != "feet" && cmd != "delcomp" && cmd != "matedist" ? a[2] : "";
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
