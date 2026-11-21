// SwSketchRefs: read-only (Grant 22 Nov 2026, 08A06-STA-01 "Floor_Plate_Layout" code 51): open an assembly read-only,
// edit one sketch in memory and list its relations by filter (all / dangling / external / in context / broken),
// with the sketch entities and the model entities each relation ties to (component, entity type, other sketch),
// plus the sketch's dimensions and what they attach to. Leaves the sketch without saving; the file is read-only.
// Usage: SwSketchRefs.exe <assembly> <sketch name> [configuration]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwSketchRefs.exe SwSketchRefs.cs

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwSketchRefs
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string S(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "(" + ex.GetType().Name + ")"; } }
    static string Mm(double v) { return (v * 1000).ToString("0.##", Inv); }

    static string Describe(object o)
    {
        if (o == null) return "NULL (missing)";
        var seg = o as SketchSegment;
        if (seg != null) return "sketch segment " + S(() => seg.GetName()) + " of " + S(() => ((Feature)seg.GetSketch()).Name);
        var pt = o as SketchPoint;
        if (pt != null) return "sketch point (" + Mm(pt.X) + ", " + Mm(pt.Y) + ") of " + S(() => ((Feature)pt.GetSketch()).Name);
        var ent = o as Entity;
        if (ent != null)
        {
            var comp = ent.GetComponent() as Component2;
            string kind = o is Face2 ? "face" : o is Edge ? "edge" : o is Vertex ? "vertex" : "entity";
            string where = "";
            var v = o as Vertex;
            if (v != null) { var p = (double[])v.GetPoint(); where = " at (" + Mm(p[0]) + ", " + Mm(p[1]) + ", " + Mm(p[2]) + ")"; }
            var e = o as Edge;
            if (e != null) { var cp = e.GetCurveParams3(); var sp = (double[])cp.StartPoint; var ep = (double[])cp.EndPoint; where = " from (" + Mm(sp[0]) + ", " + Mm(sp[1]) + ", " + Mm(sp[2]) + ") to (" + Mm(ep[0]) + ", " + Mm(ep[1]) + ", " + Mm(ep[2]) + ")"; }
            return kind + " of " + (comp == null ? "(assembly itself)" : comp.Name2 + " [" + S(() => Path.GetFileName(comp.GetPathName())) + " <" + S(() => comp.ReferencedConfiguration) + ">]") + where;
        }
        var f = o as Feature;
        if (f != null) return "feature " + f.Name + " (" + f.GetTypeName2() + ")";
        return o.GetType().Name;
    }

    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]), name = a[1]; string config = a.Length > 2 ? a[2] : null;
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        var spec = (DocumentSpecification)sw.GetOpenDocSpec(file);
        spec.ReadOnly = true; spec.Silent = true;
        if (config != null) spec.ConfigurationName = config;
        var doc = sw.OpenDoc7(spec);
        if (doc == null) { Console.Error.WriteLine("could not open, error " + spec.Error); return 3; }
        try
        {
            Feature feat = null;
            for (var f = (Feature)doc.FirstFeature(); f != null && feat == null; f = (Feature)f.GetNextFeature())
                if (f.Name == name) feat = f;
            if (feat == null) { Console.Error.WriteLine("no feature " + name); return 3; }
            var sk = (Sketch)feat.GetSpecificFeature2();
            Console.WriteLine("SKETCH\t" + name + "\tconstrained status " + S(() => Enum.GetName(typeof(swConstrainedStatus_e), sk.GetConstrainedStatus())) + "\tsegments " + S(() => ((object[])sk.GetSketchSegments() ?? new object[0]).Length.ToString()) + "\tpoints " + S(() => ((object[])sk.GetSketchPoints2() ?? new object[0]).Length.ToString()));
            doc.ClearSelection2(true);
            feat.Select2(false, -1);
            doc.EditSketch();
            var rm = sk.RelationManager;
            foreach (swSketchRelationFilterType_e filt in new[] { swSketchRelationFilterType_e.swDangling, swSketchRelationFilterType_e.swBroken, swSketchRelationFilterType_e.swExternal, swSketchRelationFilterType_e.swDefinedInContext, swSketchRelationFilterType_e.swOverDefining, swSketchRelationFilterType_e.swAll })
            {
                var rels = (object[])rm.GetRelations((int)filt) ?? new object[0];
                Console.WriteLine("FILTER\t" + filt + "\t" + rels.Length + " relations");
                if (filt == swSketchRelationFilterType_e.swAll && rels.Length > 60) { Console.WriteLine("  (all: listing only the count)"); continue; }
                foreach (SketchRelation r in rels)
                {
                    var types = (int[])r.GetEntitiesType() ?? new int[0];
                    Console.WriteLine("  REL\t" + Enum.GetName(typeof(swConstraintType_e), r.GetRelationType()) + "\tentity types " + string.Join(",", types.Select(t => Enum.GetName(typeof(swSketchRelationEntityTypes_e), t) ?? t.ToString())));
                    foreach (var o in (object[])r.GetEntities() ?? new object[0]) Console.WriteLine("    SKETCH-SIDE\t" + Describe(o));
                    foreach (var o in (object[])r.GetDefinitionEntities2() ?? new object[0]) Console.WriteLine("    MODEL-SIDE\t" + Describe(o));
                }
            }
            for (var dd = (DisplayDimension)feat.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)feat.GetNextDisplayDimension(dd))
            {
                var dim = dd.GetDimension2(0);
                var ann = (Annotation)dd.GetAnnotation();
                Console.WriteLine("DIM\t" + S(() => dim.FullName) + "\t" + S(() => Mm(((double[])dim.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0])) + "\tdriven " + S(() => dim.DrivenState.ToString()));
                foreach (var o in (object[])ann.GetAttachedEntities3() ?? new object[0]) Console.WriteLine("    ATTACHED\t" + Describe(o));
            }
            doc.InsertSketch2(false);   // leave the sketch without rebuilding; nothing is saved
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }
}
