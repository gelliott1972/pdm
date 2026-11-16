// SwWeldment: add the Weldment feature to a multibody part so it gets a cut list (brief sw-profiles step 11, 16 Nov 2026:
// 08B04-BKP-01.SLDPRT, a plate plus two angles as bodies, no Weldment feature). The shape must not change: per
// configuration, every solid body's volume and the part's bounding box are read before and after; any difference refuses
// the save. Prints the cut-list folders it formed (bodies, properties). Descriptions and lengths are set afterwards with
// SwCmd set-cutlist-prop.
// Usage: SwWeldment.exe <part> [--save] [--checked-out]
//   Without --save: a look pass (the feature is added in memory, the file is closed without saving).
//   A P:\ file needs --checked-out and must be writable (checked out); other files only under C:\code\pdm\scratch or C:\PDM-work.
// Run under SwCmd's lock (--as sw). Refuses Dropbox.
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwWeldment.exe SwWeldment.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwWeldment
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }

    static int Main(string[] args)
    {
        var pos = args.Where(a => !a.StartsWith("--")).ToList();
        bool save = args.Contains("--save"), checkedOut = args.Contains("--checked-out");
        if (pos.Count != 1) { Console.Error.WriteLine("Usage: SwWeldment <part> [--save] [--checked-out]"); return 2; }
        string file = Path.GetFullPath(pos[0]);
        if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith("M:", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED (Dropbox): " + file); return 2; }
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED: .pdm"); return 2; }
        if (save && vault)
        {
            if (!checkedOut) { Console.Error.WriteLine("REFUSED: a vault file is written only with --checked-out"); return 2; }
            if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) { Console.Error.WriteLine("REFUSED: the vault file is read-only, so it is not checked out"); return 2; }
        }
        if (save && !vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase) && !file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED: saves only in P:\\ (checked out), C:\\code\\pdm\\scratch or C:\\PDM-work"); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026: " + sw.RevisionNumber()); return 2; }
        if (sw.GetOpenDocumentByName(file) != null) { Console.Error.WriteLine("REFUSED: already open in SOLIDWORKS; close it first: " + file); return 2; }
        int errs = 0, warns = 0;
        var opt = swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : swOpenDocOptions_e.swOpenDocOptions_ReadOnly);
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)opt, "", ref errs, ref warns);
        if (doc == null) { Console.Error.WriteLine("Could not open " + file + " (errors " + errs + ")"); return 3; }
        var problems = new List<string>();
        try
        {
            var cfgs = (string[])doc.GetConfigurationNames();
            string active = doc.ConfigurationManager.ActiveConfiguration.Name;
            var before = new Dictionary<string, Shape>();
            foreach (var c in cfgs) { doc.ShowConfiguration2(c); doc.ForceRebuild3(false); before[c] = Measure(doc); Console.WriteLine("BEFORE\t" + c + "\t" + before[c]); }
            doc.ShowConfiguration2(active);
            var feats = Features(doc);
            if (feats.Any(t => t == "WeldmentFeature" || t == "MBWeldmentFeature")) problems.Add("already has a Weldment feature");
            if (problems.Count == 0)
            {
                var wf = doc.FeatureManager.InsertWeldmentFeature();
                if (wf == null) problems.Add("SOLIDWORKS did not add the Weldment feature");
                else Console.WriteLine("ADDED\t" + wf.Name + " (" + wf.GetTypeName2() + ")");
            }
            if (problems.Count == 0)
                foreach (var c in cfgs)
                {
                    doc.ShowConfiguration2(c);
                    doc.ForceRebuild3(false);
                    for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
                        if (f.GetTypeName2() == "SolidBodyFolder") { var bf = f.GetSpecificFeature2() as BodyFolder; if (bf != null) { bf.SetAutomaticCutList(true); bf.SetAutomaticUpdate(true); bf.UpdateCutList(); } }
                    var after = Measure(doc);
                    Console.WriteLine("AFTER\t" + c + "\t" + after);
                    if (after.ToString() != before[c].ToString()) problems.Add(c + ": shape changed: " + before[c] + " -> " + after);
                    int n = 0;
                    for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
                    {
                        if (f.GetTypeName2() != "SolidBodyFolder") continue;
                        for (var sub = f.GetFirstSubFeature() as Feature; sub != null; sub = sub.GetNextSubFeature() as Feature)
                        {
                            if (sub.GetTypeName2() != "CutListFolder") continue;
                            n++;
                            var folder = sub.GetSpecificFeature2() as BodyFolder;
                            var bodies = ((folder == null ? null : folder.GetBodies() as object[]) ?? new object[0]).Cast<Body2>().Select(b => b.Name);
                            var pm = sub.CustomPropertyManager;
                            var props = ((pm.GetNames() as object[]) ?? new object[0]).Cast<string>().Select(p => { string raw, res; bool wr, lk; pm.Get6(p, false, out raw, out res, out wr, out lk); return p + "=" + res; });
                            Console.WriteLine("CUT\t" + c + "\t" + sub.Name + "\t[" + string.Join(" + ", bodies) + "]\t" + string.Join("; ", props));
                        }
                    }
                    if (n == 0) problems.Add(c + ": no cut-list folder formed");
                    int er = 0;
                    object errFeats, errCodes, warnFlags;
                    doc.Extension.GetWhatsWrong(out errFeats, out errCodes, out warnFlags);   // errors only: warnings skipped
                    var ef = errFeats as object[]; var wfl = warnFlags as bool[];
                    if (ef != null) for (int i = 0; i < ef.Length; i++) if (wfl == null || !wfl[i]) er++;
                    if (er > 0) problems.Add(c + ": " + er + " rebuild error(s)");
                }
            doc.ShowConfiguration2(active);
            if (problems.Count == 0 && save)
            {
                int e = 0, w = 0;
                if (!doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w)) problems.Add("save failed (errors " + e + ")");
                else Console.WriteLine("SAVED\t" + file);
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        foreach (var p in problems) Console.WriteLine("PROBLEM\t" + p);
        Console.WriteLine(problems.Count == 0 ? (save ? "OK (saved)" : "OK (look pass, not saved)") : "NOT SAVED");
        return problems.Count == 0 ? 0 : 1;
    }

    static List<string> Features(ModelDoc2 doc)
    {
        var r = new List<string>();
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature) r.Add(f.GetTypeName2());
        return r;
    }

    class Shape
    {
        public List<string> Bodies = new List<string>(); public string Box;
        public override string ToString() { return Bodies.Count + " bodies: " + string.Join(", ", Bodies) + "; box " + Box; }
    }
    static Shape Measure(ModelDoc2 doc)
    {
        var s = new Shape();
        double[] box = null;
        foreach (Body2 b in ((object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]))
        {
            var mp = b.GetMassProperties(1) as double[];
            s.Bodies.Add((mp == null ? "?" : (mp[3] * 1e9).ToString("0.###", Inv)) + " mm3");
            var bb = (double[])b.GetBodyBox();
            if (box == null) box = (double[])bb.Clone();
            else for (int i = 0; i < 3; i++) { box[i] = Math.Min(box[i], bb[i]); box[3 + i] = Math.Max(box[3 + i], bb[3 + i]); }
        }
        s.Bodies.Sort(StringComparer.Ordinal);
        s.Box = box == null ? "none" : Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]);
        return s;
    }
}
