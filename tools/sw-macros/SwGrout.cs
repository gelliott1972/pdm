// SwGrout: the 10 mm member change for the grout (Grant via Admin, 22 Nov 2026: all shafts of LS-08X-11; GNRIC-LM
// SPL-250+8M+670-FP1 2920 -> 2930, PL-395+5M+250-SPL 1895 -> 1905 and PL-395+3M+0-EC 1145 -> 1155, LED plate position
// 395 -> 405 on the two top ones). Read-only commands work on copies under C:\PDM-work\; the one that saves works only
// under C:\PDM-work\grout\ (the rehearsal), so never on P:\ or Dropbox.
//   mates   <asm> [config]                      read-only: every mate of this assembly (its own level) with, for each entity,
//                                               the component and the feature it belongs to (plane, sketch, face of ...)
//   xforms  <asm> <config> <out.txt>            read-only: every active component and its position in the top, plus errors
//   setdims <part> <config> <dim>=<mm> ...      in that configuration only: set each dimension, rebuild, list every
//                                               configuration's errors, save (rehearsal copies only)
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
        string cmd = a[0], file = Path.GetFullPath(a[1]);
        bool write = cmd == "setdims" || cmd == "plane" || cmd == "addcfg" || cmd == "setcomp" || (cmd == "eq" && a.Length > 2) || cmd == "gv";
        string root = write ? @"C:\PDM-work\grout\" : @"C:\PDM-work\";
        if (!file.StartsWith(root, StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: only copies under " + root); return 2; }
        if (cmd == "xforms" && !Path.GetFullPath(a[3]).StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under C:\\PDM-work"); return 2; }
        sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        int e = 0, w = 0;
        var t0 = DateTime.Now;
        string cfgArg = a.Length > 2 && cmd != "plane" ? a[2] : "";
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
