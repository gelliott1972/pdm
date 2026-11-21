// SwReimport: Revit re-import TEST COPY edits (apex-admin, Grant's overnight handover 22 Nov 2026). Works only on files
// under C:\PDM-work\reimport-test\ (refuses anything else, so never P:\ or Dropbox). Commands:
//   points  <part> <sketch>                       read-only: sketch points (mm, sketch coordinates)
//   measure <part>                                read-only: per configuration, solid bodies (volume mm3) and box
//   mates   <asm> <component name>                read-only: mates that involve that component (type, value, entities)
//   xforms  <asm> [config]                        read-only: every component (all levels) with its position in the top
//   setdim  <part> <dim full name> <mm>           set a dimension (this configuration), rebuild, save
//   extend  <part> <sketch> <x mm> <new x mm>     free the sketch point at x (delete its relations) and move it, save
//   hole    <part> <plane> <x mm> <y mm> <dia mm> sketch a circle on a plane and cut it through all, save
//   matedist <asm> <mate name> <mm>               set a distance mate's value, rebuild, save
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwReimport.exe SwReimport.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwReimport
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static ISldWorks sw;
    static string S(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "(" + ex.GetType().Name + ")"; } }
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }
    static double Arg(string s) { return double.Parse(s, Inv) / 1000.0; }

    static ModelDoc2 Open(string file, bool write)
    {
        int e = 0, w = 0;
        int type = file.EndsWith(".SLDASM", StringComparison.OrdinalIgnoreCase) ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
        var doc = sw.OpenDoc6(file, type, (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (int)swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight | (write ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) throw new Exception("could not open " + file + ", error " + e);
        Console.WriteLine("OPENED\t" + file + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name + "\terrors " + e + " warnings " + w);
        return doc;
    }

    static Feature Feat(ModelDoc2 doc, string name)
    {
        for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
        {
            if (f.Name == name) return f;
            for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature()) if (s.Name == name) return s;
        }
        throw new Exception("no feature " + name);
    }

    static string Measure(ModelDoc2 doc)
    {
        var vols = new List<string>(); double[] box = null;
        foreach (Body2 b in (object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0])
        {
            var mp = b.GetMassProperties(1) as double[];
            vols.Add(mp == null ? "?" : (mp[3] * 1e9).ToString("0.0", Inv));
            var bb = (double[])b.GetBodyBox();
            if (box == null) box = (double[])bb.Clone();
            else for (int i = 0; i < 3; i++) { box[i] = Math.Min(box[i], bb[i]); box[3 + i] = Math.Max(box[3 + i], bb[3 + i]); }
        }
        vols.Sort(StringComparer.Ordinal);
        return vols.Count + " bodies (mm3 " + string.Join(", ", vols) + "); box " + (box == null ? "-" : Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]));
    }

    static void MeasureAll(ModelDoc2 doc, string label)
    {
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        foreach (string c in (string[])doc.GetConfigurationNames())
        {
            doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
            Console.WriteLine("MEASURE\t" + label + "\t" + c + "\t" + Measure(doc) + "\terrors " + Errors(doc));
        }
        doc.ShowConfiguration2(active);
    }

    static int Errors(ModelDoc2 doc)
    {
        object f, c, w; doc.Extension.GetWhatsWrong(out f, out c, out w);
        var ef = f as object[]; var wf = w as bool[]; int n = 0;
        if (ef != null) for (int i = 0; i < ef.Length; i++) if (wf == null || i >= wf.Length || !wf[i]) n++;
        return n;
    }

    static bool Save(ModelDoc2 doc)
    {
        int e = 0, w = 0;
        bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w);
        Console.WriteLine(ok ? "SAVED\t" + doc.GetPathName() : "SAVE FAILED\t" + e);
        return ok;
    }

    static void Walk(Component2 c, string path, MathTransform top)
    {
        foreach (Component2 k in (object[])c.GetChildren() ?? new object[0])
        {
            string p = path + "/" + k.Name2.Split('/').Last();
            if (k.IsSuppressed()) continue;
            var t = k.Transform2;
            string pos = "-";
            if (t != null) { var a = (double[])t.ArrayData; pos = string.Join(" ", a.Take(9).Select(v => Math.Round(v, 5).ToString("0.#####", Inv))) + " | " + Mm(a[9]) + " " + Mm(a[10]) + " " + Mm(a[11]); }
            string box = "-";
            try { var b = (double[])k.GetBox(false, false); if (b != null) box = Mm(b[0]) + ".." + Mm(b[3]) + ", " + Mm(b[1]) + ".." + Mm(b[4]) + ", " + Mm(b[2]) + ".." + Mm(b[5]); } catch { }
            Console.WriteLine("COMP\t" + p + "\t" + Path.GetFileName(k.GetPathName()) + "\t<" + k.ReferencedConfiguration + ">\t" + pos + "\tbox " + box);
            Walk(k, p, top);
        }
    }

    static int Main(string[] a)
    {
        string cmd = a[0], file = Path.GetFullPath(a[1]);
        if (!file.StartsWith(@"C:\PDM-work\reimport-test\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: test copy only (C:\\PDM-work\\reimport-test)"); return 2; }
        sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        bool write = cmd == "setdim" || cmd == "extend" || cmd == "hole" || cmd == "matedist" || cmd == "movecomp" || cmd == "unmove";
        var doc = Open(file, write);
        int rc = 0;
        try
        {
            if (cmd == "points")
            {
                var sk = (Sketch)Feat(doc, a[2]).GetSpecificFeature2();
                var mu = (MathUtility)sw.GetMathUtility();
                var toModel = (MathTransform)sk.ModelToSketchTransform.Inverse();
                foreach (SketchPoint p in (object[])sk.GetSketchPoints2() ?? new object[0])
                {
                    var mp = (double[])((MathPoint)((MathPoint)mu.CreatePoint(new double[] { p.X, p.Y, p.Z })).MultiplyTransform(toModel)).ArrayData;
                    Console.WriteLine("POINT\tsketch (" + Mm(p.X) + ", " + Mm(p.Y) + ", " + Mm(p.Z) + ")\tpart (" + Mm(mp[0]) + ", " + Mm(mp[1]) + ", " + Mm(mp[2]) + ")");
                }
                foreach (SketchSegment s in (object[])sk.GetSketchSegments() ?? new object[0]) Console.WriteLine("SEG\t" + s.GetName() + "\t" + (swSketchSegments_e)s.GetType());
            }
            else if (cmd == "measure") MeasureAll(doc, "now");
            else if (cmd == "mates")
            {
                var asm = (AssemblyDoc)doc;
                var comp = (Component2)asm.GetComponentByName(a[2]);
                if (comp == null) throw new Exception("no component " + a[2]);
                foreach (var o in (object[])comp.GetMates() ?? new object[0])
                {
                    var m = o as Mate2; if (m == null) continue;
                    string val = "";
                    try { var dp = m.DisplayDimension2[0]; if (dp != null) val = Mm(((double[])dp.GetDimension2(0).GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0]); } catch { }
                    string name = S(() => ((Feature)m).Name);
                    Console.WriteLine("MATE\t" + name + "\t" + (swMateType_e)m.Type + "\tvalue " + val + "\talign " + m.Alignment + "\tflipped " + S(() => m.Flipped.ToString()));
                    for (int k = 0; k < m.GetMateEntityCount(); k++)
                    {
                        var me = m.MateEntity(k);
                        Console.WriteLine("  ENTITY\t" + S(() => me.ReferenceComponent.Name2) + "\t" + (swSelectType_e)me.ReferenceType2);
                    }
                }
            }
            else if (cmd == "xforms")
            {
                if (a.Length > 2) doc.ShowConfiguration2(a[2]);
                doc.ForceRebuild3(false);
                var asm = (AssemblyDoc)doc;
                asm.ResolveAllLightWeightComponents(false);
                int n = 0;
                foreach (Component2 k in (object[])asm.GetComponents(false) ?? new object[0])
                {
                    if (k.GetSuppression2() == (int)swComponentSuppressionState_e.swComponentSuppressed) continue;
                    var t = k.Transform2;
                    string pos = "-";
                    if (t != null) { var d = (double[])t.ArrayData; pos = string.Join(" ", d.Take(9).Select(v => Math.Round(v, 5).ToString("0.#####", Inv))) + " | " + Mm(d[9]) + " " + Mm(d[10]) + " " + Mm(d[11]); }
                    string box = "-";
                    Console.WriteLine("COMP\t" + k.Name2 + "\t" + k.GetPathName() + "\t<" + k.ReferencedConfiguration + ">\t" + pos + "\tbox " + box);
                    n++;
                }
                Console.WriteLine("COUNT\t" + n + " active components");
                Console.WriteLine("ERRORS\t" + Errors(doc));
            }
            else if (cmd == "setdim")
            {
                MeasureAll(doc, "before");
                var dim = (Dimension)doc.Parameter(a[2]);
                if (dim == null) throw new Exception("no dimension " + a[2]);
                double old = ((double[])dim.GetSystemValue3((int)swInConfigurationOpts_e.swAllConfiguration, null))[0];
                int r = dim.SetSystemValue3(Arg(a[3]), (int)swSetValueInConfiguration_e.swSetValue_InAllConfigurations, null);
                Console.WriteLine("SETDIM\t" + a[2] + "\t" + Mm(old) + " -> " + a[3] + "\tresult " + r);
                doc.ForceRebuild3(false);
                MeasureAll(doc, "after");
                if (r != 0 || !Save(doc)) rc = 1;
            }
            else if (cmd == "extend")
            {
                MeasureAll(doc, "before");
                var feat = Feat(doc, a[2]);
                var sk = (Sketch)feat.GetSpecificFeature2();
                double x = Arg(a[3]), nx = Arg(a[4]);
                doc.ClearSelection2(true); feat.Select2(false, -1); doc.EditSketch();
                SketchPoint pt = null;
                foreach (SketchPoint p in (object[])sk.GetSketchPoints2() ?? new object[0]) if (Math.Abs(p.X - x) < 1e-6) pt = p;
                if (pt == null) throw new Exception("no point at x " + a[3]);
                int del = 0;
                var rm = sk.RelationManager;
                foreach (SketchRelation r in (object[])rm.GetRelations((int)swSketchRelationFilterType_e.swAll) ?? new object[0])
                {
                    bool hit = ((object[])r.GetEntities() ?? new object[0]).Any(o => { var sp = o as SketchPoint; return sp != null && Math.Abs(sp.X - x) < 1e-6 && Math.Abs(sp.Y - pt.Y) < 1e-6; });
                    if (hit) { Console.WriteLine("DELETE RELATION\t" + (swConstraintType_e)r.GetRelationType()); if (rm.DeleteRelation(r)) del++; }
                }
                bool moved = pt.SetCoords(nx, pt.Y, pt.Z);
                Console.WriteLine("EXTEND\t" + a[2] + "\tpoint x " + a[3] + " -> " + a[4] + "\trelations deleted " + del + "\tmoved " + moved + "\tnow (" + Mm(pt.X) + ", " + Mm(pt.Y) + ")");
                doc.InsertSketch2(true);
                doc.ForceRebuild3(false);
                MeasureAll(doc, "after");
                if (!moved || !Save(doc)) rc = 1;
            }
            else if (cmd == "hole")
            {
                MeasureAll(doc, "before");
                var plane = Feat(doc, a[2]);
                doc.ClearSelection2(true); plane.Select2(false, -1);
                doc.SketchManager.InsertSketch(true);
                var arc = doc.SketchManager.CreateCircleByRadius(Arg(a[3]), Arg(a[4]), 0, Arg(a[5]) / 2);
                Console.WriteLine("CIRCLE\t" + (arc != null));
                var fm = doc.FeatureManager;
                var cut = fm.FeatureCut4(false, false, false, (int)swEndConditions_e.swEndCondThroughAll, (int)swEndConditions_e.swEndCondThroughAll, 0, 0,
                    false, false, false, false, 0, 0, false, false, false, false, false, true, true, true, true, false,
                    (int)swStartConditions_e.swStartSketchPlane, 0, false, false);
                if (cut == null)   // through all in the other direction
                    Console.WriteLine("CUT\tfirst direction failed");
                else { cut.Name = "REIMPORT-TEST extra hole"; Console.WriteLine("CUT\t" + cut.Name); }
                doc.ClearSelection2(true);
                doc.ForceRebuild3(false);
                MeasureAll(doc, "after");
                if (cut == null || !Save(doc)) rc = 1;
            }
            else if (cmd == "movecomp")
            {
                // movecomp <asm> <component> <dx mm> <dy mm> <dz mm>: suppress the component's mates, move it in the
                // assembly's coordinates, fix it there, rebuild, save (test copy only)
                var asm = (AssemblyDoc)doc;
                var comp = (Component2)asm.GetComponentByName(a[2]);
                if (comp == null) throw new Exception("no component " + a[2]);
                var before = (double[])comp.Transform2.ArrayData;
                foreach (var o in (object[])comp.GetMates() ?? new object[0])
                {
                    var mf = o as Feature;
                    if (mf == null) continue;
                    bool ok = mf.SetSuppression2((int)swFeatureSuppressionAction_e.swSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null);
                    Console.WriteLine("SUPPRESS MATE	" + mf.Name + "	" + ok);
                }
                var d = (double[])before.Clone();
                d[9] += Arg(a[3]); d[10] += Arg(a[4]); d[11] += Arg(a[5]);
                var mu = (MathUtility)sw.GetMathUtility();
                comp.Transform2 = (MathTransform)mu.CreateTransform(d);
                doc.ClearSelection2(true);
                comp.Select4(false, null, false);
                asm.FixComponent();
                doc.ClearSelection2(true);
                doc.ForceRebuild3(false);
                var after = (double[])comp.Transform2.ArrayData;
                Console.WriteLine("MOVECOMP	" + a[2] + "	from " + Mm(before[9]) + " " + Mm(before[10]) + " " + Mm(before[11]) + "	to " + Mm(after[9]) + " " + Mm(after[10]) + " " + Mm(after[11]) + "	fixed " + comp.IsFixed() + "	rotation same " + Enumerable.Range(0, 9).All(i => Math.Abs(before[i] - after[i]) < 1e-9));
                Console.WriteLine("ERRORS	" + Errors(doc));
                if (!Save(doc)) rc = 1;
            }
            else if (cmd == "unmove")
            {
                // unmove <asm> <component>: undo movecomp: float the component, unsuppress its mates, rebuild, save
                var asm = (AssemblyDoc)doc;
                var comp = (Component2)asm.GetComponentByName(a[2]);
                if (comp == null) throw new Exception("no component " + a[2]);
                var before = (double[])comp.Transform2.ArrayData;
                doc.ClearSelection2(true); comp.Select4(false, null, false); asm.UnfixComponent(); doc.ClearSelection2(true);
                foreach (var o in (object[])comp.GetMates() ?? new object[0])
                {
                    var mf = o as Feature; if (mf == null) continue;
                    Console.WriteLine("UNSUPPRESS MATE	" + mf.Name + "	" + mf.SetSuppression2((int)swFeatureSuppressionAction_e.swUnSuppressFeature, (int)swInConfigurationOpts_e.swAllConfiguration, null));
                }
                doc.ForceRebuild3(false);
                var after = (double[])comp.Transform2.ArrayData;
                Console.WriteLine("UNMOVE	" + a[2] + "	from " + Mm(before[9]) + " " + Mm(before[10]) + " " + Mm(before[11]) + "	to " + Mm(after[9]) + " " + Mm(after[10]) + " " + Mm(after[11]) + "	fixed " + comp.IsFixed());
                Console.WriteLine("ERRORS	" + Errors(doc));
                if (!Save(doc)) rc = 1;
            }
            else if (cmd == "allmates")
            {
                // allmates <asm>: every mate in the assembly (top level of this file) with the components it ties
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                {
                    if (f.GetTypeName2() != "MateGroup") continue;
                    for (var m = (Feature)f.GetFirstSubFeature(); m != null; m = (Feature)m.GetNextSubFeature())
                    {
                        var mate = m.GetSpecificFeature2() as Mate2;
                        if (mate == null) continue;
                        var names = new List<string>();
                        for (int k = 0; k < mate.GetMateEntityCount(); k++) names.Add(S(() => mate.MateEntity(k).ReferenceComponent.Name2));
                        Console.WriteLine("MATE	" + m.Name + "	" + (swMateType_e)mate.Type + (m.IsSuppressed() ? "	SUPPRESSED" : "") + "	" + string.Join("	", names));
                    }
                }
            }
            else if (cmd == "matedist")
            {
                var m = Feat(doc, a[2]);
                var md = (DistanceMateFeatureData)m.GetDefinition();
                double old = md.Distance;
                md.Distance = Arg(a[3]);
                bool ok = m.ModifyDefinition(md, doc, null);
                Console.WriteLine("MATEDIST\t" + a[2] + "\t" + Mm(old) + " -> " + a[3] + "\tModifyDefinition " + ok);
                doc.ForceRebuild3(false);
                Console.WriteLine("ERRORS\t" + Errors(doc));
                if (!ok || !Save(doc)) rc = 1;
            }
            else throw new Exception("unknown command " + cmd);
        }
        catch (Exception ex) { Console.WriteLine("FAILED\t" + ex.Message); rc = 3; }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return rc;
    }
}
