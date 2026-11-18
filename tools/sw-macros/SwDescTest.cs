// SwDescTest: test a cut-list Description text expression on a SCRATCH copy (Admin's 08X04-BKP question, 17 Nov 2026).
// Sets <property> on cut-list folder <item> to <value> (typed as given, e.g. PLATE, 10 x 140 x "D2@Sketch3@part.SLDPRT"),
// optionally sets the display precision of one model dimension (--precision <dim full name> <decimals>),
// then shows every configuration and prints each cut-list folder's raw and evaluated value. Saves only with --save.
// Scratch copies, or a checked-out (writable) vault file with --checked-out. Refuses anything else.
// Usage: SwDescTest.exe <part> <item> <property> <value> [--precision D2@Sketch3 0] [--save] [--checked-out]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwDescTest.exe SwDescTest.cs

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwDescTest
{
    static int Main(string[] a)
    {
        var args = a.ToList();
        bool save = args.Remove("--save"), checkedOut = args.Remove("--checked-out");
        string precDim = null; int precN = -1;
        int pi = args.IndexOf("--precision");
        if (pi >= 0) { precDim = args[pi + 1]; precN = int.Parse(args[pi + 2]); args.RemoveRange(pi, 3); }
        if (args.Count != 4) { Console.Error.WriteLine("Usage: SwDescTest <part> <item> <property> <value> [--precision dim n] [--save]"); return 2; }
        string file = Path.GetFullPath(args[0]), item = args[1], prop = args[2], value = args[3];
        // Scratch copies; or (18 Nov, Admin's go for the real 08X04-BKP change) a vault file that is checked out and writable.
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("refused: .pdm"); return 2; }
        if (vault && (!checkedOut || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0)) { Console.Error.WriteLine("refused: a vault file only when checked out (--checked-out, writable)"); return 2; }
        if (!vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("refused: scratch copies or checked-out vault files only"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open, errors " + e); return 3; }
        try
        {
            if (precDim != null)
            {
                bool done = false;
                for (var f = (Feature)doc.FirstFeature(); f != null && !done; f = (Feature)f.GetNextFeature())
                    foreach (var sub in new[] { f }.Concat(Subs(f)))
                        for (var dd = (DisplayDimension)sub.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)sub.GetNextDisplayDimension(dd))
                        {
                            var dm = dd.GetDimension2(0);
                            if (dm != null && dm.FullName.StartsWith(precDim + "@", StringComparison.OrdinalIgnoreCase))
                            {
                                int r = dd.SetPrecision3(precN, -1, -1, -1);
                                Console.WriteLine("PRECISION\t" + dm.FullName + "\t" + precN + " decimals\tresult " + r + "\tin feature " + f.Name + " (" + f.GetTypeName2() + ")");
                                done = true; break;
                            }
                        }
                if (!done) Console.WriteLine("PRECISION\t" + precDim + "\tnot found");
            }
            var folder = Folders(doc).FirstOrDefault(f => f.Name == item);
            if (folder == null) { Console.WriteLine("no cut-list folder " + item); return 3; }
            var cpm = folder.CustomPropertyManager;
            int add = cpm.Add3(prop, (int)swCustomInfoType_e.swCustomInfoText, value, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
            Console.WriteLine("SET\t" + item + "\t" + prop + " = " + value + "\tAdd3 " + add + (add != 0 ? "\tSet2 " + cpm.Set2(prop, value) : ""));
            foreach (string c in (string[])doc.GetConfigurationNames())
            {
                doc.ShowConfiguration2(c);
                doc.ForceRebuild3(false);
                foreach (var f in Folders(doc))
                {
                    string raw, val; bool wasRes, linked;
                    f.CustomPropertyManager.Get6(prop, false, out raw, out val, out wasRes, out linked);
                    if (string.IsNullOrEmpty(raw) && f.Name.IndexOf("PLATE", StringComparison.OrdinalIgnoreCase) < 0 && f.Name != item) continue;
                    Console.WriteLine("READ\t" + c + "\t" + f.Name + "\traw " + raw + "\tvalue " + val);
                }
            }
            if (save)
            {
                int se = 0, sw2 = 0;
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED " + se);
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }

    static System.Collections.Generic.IEnumerable<Feature> Subs(Feature f)
    {
        for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature()) yield return s;
    }

    static System.Collections.Generic.List<Feature> Folders(ModelDoc2 doc)
    {
        var list = new System.Collections.Generic.List<Feature>();
        for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
            if (f.GetTypeName2() == "SolidBodyFolder")
                foreach (var s in Subs(f)) if (s.GetTypeName2() == "CutListFolder") list.Add(s);
        return list;
    }
}
