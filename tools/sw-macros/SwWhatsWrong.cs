// SwWhatsWrong: read-only (apex-admin / Grant 22 Nov 2026, 08A06-STA-01 rebuild warning): open a model read-only in one
// configuration, rebuild in memory, and list every What's Wrong item with its type, error code and, for mates, the
// components and entity types it ties. Never saves.
// Usage: SwWhatsWrong.exe <model> [configuration]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwWhatsWrong.exe SwWhatsWrong.cs

using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwWhatsWrong
{
    static string S(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "(" + ex.Message + ")"; } }

    static void Report(ModelDoc2 doc, string label)
    {
        object feats, codes, warns;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warns);
        var fs = feats as object[]; var cs = codes as int[]; var wf = warns as bool[];
        Console.WriteLine("WHATSWRONG\t" + label + "\t" + (fs == null ? 0 : fs.Length) + " items");
        if (fs == null) return;
        for (int i = 0; i < fs.Length; i++)
        {
            var f = (Feature)fs[i];
            int code = cs != null && i < cs.Length ? cs[i] : -1;
            Console.WriteLine("ITEM\t" + ((wf != null && i < wf.Length && wf[i]) ? "warning" : "error") + "\t" + f.Name + "\ttype " + f.GetTypeName2()
                + "\tcode " + code + " " + Enum.GetName(typeof(swFeatureError_e), code) + "\tsuppressed " + S(() => ((bool[])f.IsSuppressed2((int)swInConfigurationOpts_e.swThisConfiguration, null))[0].ToString()));
            var mate = f.GetSpecificFeature2() as Mate2;
            if (mate != null)
            {
                Console.WriteLine("  MATE\ttype " + Enum.GetName(typeof(swMateType_e), mate.Type) + "\talign " + mate.Alignment + "\tflipped " + S(() => mate.Flipped.ToString()));
                for (int k = 0; k < mate.GetMateEntityCount(); k++)
                {
                    var me = mate.MateEntity(k);
                    Console.WriteLine("  ENTITY\t" + k + "\t" + S(() => me.ReferenceComponent.Name2) + "\t" + S(() => me.ReferenceComponent.GetPathName()) + "\t[" + S(() => me.ReferenceComponent.ReferencedConfiguration) + "]\t" + Enum.GetName(typeof(swSelectType_e), me.ReferenceType2));
                }
            }
            string err = S(() => { bool w2; return f.GetErrorCode2(out w2).ToString(); });
            Console.WriteLine("  FEATURE-ERROR\t" + err);
            foreach (Feature p in (object[])f.GetParents() ?? new object[0]) Console.WriteLine("  PARENT\t" + p.Name + "\t" + p.GetTypeName2());
            foreach (Feature c in (object[])f.GetChildren() ?? new object[0]) Console.WriteLine("  CHILD\t" + c.Name + "\t" + c.GetTypeName2());
            var sk = f.GetSpecificFeature2() as Sketch;
            if (sk != null)
            {
                var rm = sk.RelationManager;
                foreach (SketchRelation r in (object[])rm.GetRelations((int)swSketchRelationFilterType_e.swAll) ?? new object[0])
                {
                    var types = r.GetEntitiesType() as int[];
                    string line = "  RELATION\t" + Enum.GetName(typeof(swConstraintType_e), r.GetRelationType()) + "\tentities " + r.GetEntitiesCount() + (r.Suppressed ? " (suppressed)" : "");
                    var defs = r.GetDefinitionEntities2() as object[];
                    if (defs != null)
                        foreach (var d in defs)
                        {
                            var ent = d as Entity;
                            var comp = ent == null ? null : ent.GetComponent() as Component2;
                            line += "\t[" + (comp == null ? "(no component)" : comp.Name2 + " " + S(() => Path.GetFileName(comp.GetPathName())) + " <" + S(() => comp.ReferencedConfiguration) + ">") + "]";
                        }
                    bool bad = defs == null || Array.Exists(defs, d => d == null);
                    if (bad || line.Contains(".SLD")) Console.WriteLine(line + (bad ? "	MISSING ENTITY" : ""));
                }
            }
        }
    }

    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]); string config = a.Length > 1 ? a[1] : null;
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        var spec = (DocumentSpecification)sw.GetOpenDocSpec(file);
        spec.ReadOnly = true; spec.Silent = true;
        if (config != null) spec.ConfigurationName = config;
        var doc = sw.OpenDoc7(spec);
        if (doc == null) { Console.Error.WriteLine("could not open, error " + spec.Error); return 3; }
        try
        {
            Console.WriteLine("OPENED\t" + file + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name + "\terrors " + spec.Error + " warnings " + spec.Warning);
            Report(doc, "after open");
            doc.ForceRebuild3(false);
            Report(doc, "after full rebuild (in memory)");
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }
}
