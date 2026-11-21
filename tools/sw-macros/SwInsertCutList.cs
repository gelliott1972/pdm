// SwInsertCutList: show (and with --set, switch on) "transfer cut list properties" on a part's Insert Part features
// (Grant 21 Nov 2026, De angles: 08B04-BKP-01 / 08BXX-MSW-01 insert angle parts that already carry MATERIAL, LENGTH etc.,
// but the weldment's cut list does not receive them). Prints each Insert Part feature, its source part and the setting,
// then every configuration's measure (bodies, volumes, box) before and after, and the rebuild errors.
// Saves only with --save: scratch or C:\PDM-work copies, or a checked-out (writable) vault part with --checked-out.
// Usage: SwInsertCutList.exe <part> [--set] [--save] [--checked-out]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwInsertCutList.exe SwInsertCutList.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwInsertCutList
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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
        Func<double, string> mm = v => (v * 1000).ToString("0.##", Inv);
        return vols.Count + " bodies (mm3 " + string.Join(", ", vols) + "); box " + (box == null ? "-" : mm(box[0]) + ".." + mm(box[3]) + ", " + mm(box[1]) + ".." + mm(box[4]) + ", " + mm(box[2]) + ".." + mm(box[5]));
    }

    static int Errors(ModelDoc2 doc)
    {
        object f, c, w; doc.Extension.GetWhatsWrong(out f, out c, out w);
        var ef = f as object[]; var wf = w as bool[]; int n = 0;
        if (ef != null) for (int i = 0; i < ef.Length; i++) if (wf == null || i >= wf.Length || !wf[i]) n++;
        return n;
    }

    static int Main(string[] a)
    {
        var args = a.ToList();
        bool set = args.Remove("--set"), save = args.Remove("--save"), checkedOut = args.Remove("--checked-out");
        string file = Path.GetFullPath(args[0]);
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase);
        if (save && (vault ? (!checkedOut || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0)
                           : !(file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase))))
        { Console.Error.WriteLine("REFUSED: saves only to C:\\PDM-work or scratch copies, or a checked-out vault part (--checked-out)"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open, errors " + e); return 3; }
        int bad = 0;
        try
        {
            var configs = (string[])doc.GetConfigurationNames();
            var before = new Dictionary<string, string>(); var errB = new Dictionary<string, int>();
            foreach (var c in configs) { doc.ShowConfiguration2(c); doc.ForceRebuild3(false); before[c] = Measure(doc); errB[c] = Errors(doc); Console.WriteLine("BEFORE\t" + c + "\t" + before[c] + "\terrors " + errB[c]); }
            int changed = 0;
            for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
            {
                if (f.GetTypeName2() != "Stock") continue;   // Insert Part (derived part) features
                var d = f.GetDefinition() as DerivedPartFeatureData;
                if (d == null) continue;
                Console.WriteLine("INSERTPART\t" + f.Name + "\tfrom " + d.PathName + " [" + d.PartConfiguration + "]\tcut list properties " + d.ImportCutListProperties + "\tcustom properties " + d.ImportCustomProperties + "\tmaterial " + d.ImportMaterial);
                if (set && !d.ImportCutListProperties)
                {
                    d.AccessSelections(doc, null);
                    d.ImportCutListProperties = true;
                    bool ok = f.ModifyDefinition(d, doc, null);
                    Console.WriteLine("SET\t" + f.Name + "\tcut list properties -> true\tModifyDefinition " + ok);
                    if (ok) changed++; else bad++;
                }
            }
            if (changed > 0)
                foreach (var c in configs)
                {
                    doc.ShowConfiguration2(c); doc.ForceRebuild3(false);
                    string m = Measure(doc); int er = Errors(doc);
                    Console.WriteLine("AFTER\t" + c + "\t" + m + "\terrors " + er);
                    if (m != before[c]) { Console.WriteLine("PROBLEM\t" + c + ": shape changed"); bad++; }
                    if (er > errB[c]) { Console.WriteLine("PROBLEM\t" + c + ": rebuild errors " + errB[c] + " -> " + er); bad++; }
                }
            if (save && changed > 0 && bad == 0)
            {
                int se = 0, sw2 = 0;
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED " + se);
                if (!ok) bad++;
            }
            else if (save) Console.WriteLine("NOT SAVED" + (changed == 0 ? " (nothing to change)" : " (a check failed)"));
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return bad == 0 ? 0 : 1;
    }
}
