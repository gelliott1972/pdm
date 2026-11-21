// SwCutListTry: experiment on a COPY only (Grant 21 Nov 2026, De angles): delete named cut-list properties from the item
// that holds a given body, update the cut list, rebuild, and print that item's properties in every configuration.
// Refuses anything outside C:\PDM-work or C:\code\pdm\scratch. Saves only with --save.
// Usage: SwCutListTry.exe <part copy> <body name part> <prop,prop> [--save]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwCutListTry.exe SwCutListTry.cs

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwCutListTry
{
    static int Main(string[] a)
    {
        var args = a.ToList(); bool save = args.Remove("--save");
        string file = Path.GetFullPath(args[0]), bodyPart = args[1]; var props = args[2].Split(',');
        if (!(file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase)))
        { Console.Error.WriteLine("REFUSED: copies only"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open " + e); return 3; }
        try
        {
            Func<Feature> item = () =>
            {
                for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                    if (f.GetTypeName2() == "SolidBodyFolder")
                        for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature())
                            if (s.GetTypeName2() == "CutListFolder")
                            {
                                var bf = (BodyFolder)s.GetSpecificFeature2();
                                if (((object[])bf.GetBodies() ?? new object[0]).Cast<Body2>().Any(b => b.Name.Contains(bodyPart))) return s;
                            }
                return null;
            };
            var it = item();
            if (it == null) { Console.Error.WriteLine("no item holds a body named like " + bodyPart); return 3; }
            foreach (var p in props) Console.WriteLine("DELETE\t" + it.Name + "\t" + p + "\t" + it.CustomPropertyManager.Delete2(p));
            for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
                if (f.GetTypeName2() == "SolidBodyFolder") Console.WriteLine("UPDATECUTLIST\t" + ((BodyFolder)f.GetSpecificFeature2()).UpdateCutList());
            doc.ForceRebuild3(false);
            foreach (string c in (string[])doc.GetConfigurationNames())
            {
                doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
                it = item();
                var cpm = it.CustomPropertyManager;
                foreach (string n in (string[])cpm.GetNames() ?? new string[0])
                {
                    string raw, val; bool wr, lk; cpm.Get6(n, false, out raw, out val, out wr, out lk);
                    Console.WriteLine("PROP\t" + c + "\t" + it.Name + "\t" + n + " = " + val + "\t(" + raw + ")");
                }
            }
            if (save) { int se = 0, s2 = 0; Console.WriteLine(doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref s2) ? "SAVED" : "SAVE FAILED " + se); }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }
}
