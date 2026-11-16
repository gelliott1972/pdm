// SwLibFix: correct size names and Descriptions in the staging copy of the weldment profile library (brief sw-profiles,
// step 2, 16 Nov 2026; Grant via Admin: "name each size by what it actually draws, with Descriptions to match").
// Only configuration names and the configuration-level Description change. Every configuration's profile sketch
// (its dimensions and its points' box) is read before and after and must be identical, matched through the renames.
// Usage: SwLibFix.exe <profile .sldlfp> <changes.tsv> [--save]
//   changes.tsv: "<configuration>\t<new configuration name or ->\t<new Description or ->" per line; # comments skipped.
//   Only files under C:\code\pdm\scratch\profiles\ (the staging copy); never M:\ or Dropbox.
// Run under SwCmd's lock (--as sw), after Close All (the Dropbox originals have the same file names).
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwLibFix.exe SwLibFix.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwLibFix
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.####", Inv); }

    static int Main(string[] args)
    {
        var pos = args.Where(a => !a.StartsWith("--")).ToList();
        bool save = args.Contains("--save");
        if (pos.Count != 2) { Console.Error.WriteLine("Usage: SwLibFix <profile .sldlfp> <changes.tsv> [--save]"); return 2; }
        string file = Path.GetFullPath(pos[0]);
        if (!file.StartsWith(@"C:\code\pdm\scratch\profiles\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: only the staging copy under C:\\code\\pdm\\scratch\\profiles"); return 2; }
        var ops = new List<string[]>();
        foreach (var l in File.ReadAllLines(pos[1]).Where(l => l.Trim() != "" && !l.StartsWith("#")))
        { var c = l.Split('\t'); if (c.Length != 3) { Console.Error.WriteLine("Bad line: " + l); return 2; } ops.Add(c); }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        if (sw.GetOpenDocumentByName(file) != null) { Console.Error.WriteLine("REFUSED: already open; close it first"); return 2; }
        for (var d = (ModelDoc2)sw.GetFirstDocument(); d != null; d = (ModelDoc2)d.GetNext())
            if (string.Equals(Path.GetFileName(d.GetPathName() ?? ""), Path.GetFileName(file), StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("REFUSED: a file with the same name is open: " + d.GetPathName()); return 2; }
        int errs = 0, warns = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : swOpenDocOptions_e.swOpenDocOptions_ReadOnly)), "", ref errs, ref warns);
        if (doc == null) { Console.Error.WriteLine("Could not open (errors " + errs + ")"); return 3; }
        var problems = new List<string>();
        try
        {
            string active = doc.ConfigurationManager.ActiveConfiguration.Name;
            var cfgs = ((string[])doc.GetConfigurationNames()).ToList();
            var before = new Dictionary<string, string>();
            foreach (var c in cfgs) { doc.ShowConfiguration2(c); before[c] = SketchOf(doc); Console.WriteLine("BEFORE\t" + c + "\t" + Desc(doc, c) + "\t" + before[c]); }
            var rename = new Dictionary<string, string>();   // new name -> old name
            foreach (var c in cfgs) rename[c] = c;
            foreach (var op in ops)
            {
                if (!cfgs.Contains(op[0])) { problems.Add("no configuration " + op[0]); continue; }
                if (op[2] != "-")
                {
                    var m = doc.Extension.CustomPropertyManager[op[0]];
                    string was = Desc(doc, op[0]);
                    int r = m.Add3("Description", (int)swCustomInfoType_e.swCustomInfoText, op[2], (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged) problems.Add(op[0] + ": Description not set (" + r + ")");
                    Console.WriteLine("DESC\t" + op[0] + "\t" + was + " -> " + op[2]);
                }
            }
            foreach (var op in ops.Where(o => o[1] != "-"))
            {
                if (!cfgs.Contains(op[0])) continue;
                if (rename.ContainsKey(op[1])) { problems.Add("rename " + op[0] + " -> " + op[1] + ": that name is taken"); continue; }
                var conf = (Configuration)doc.GetConfigurationByName(op[0]);
                conf.Name = op[1];
                rename.Remove(op[0]); rename[op[1]] = op[0];
                Console.WriteLine("RENAME\t" + op[0] + " -> " + op[1]);
            }
            var now = ((string[])doc.GetConfigurationNames()).ToList();
            foreach (var c in now)
            {
                doc.ShowConfiguration2(c);
                string after = SketchOf(doc), old;
                if (!rename.TryGetValue(c, out old)) { problems.Add("unexpected configuration " + c); continue; }
                Console.WriteLine("AFTER\t" + c + (old != c ? " (was " + old + ")" : "") + "\t" + Desc(doc, c) + "\t" + after);
                if (after != before[old]) problems.Add(c + ": sketch differs");
            }
            if (now.Count != cfgs.Count) problems.Add("configuration count " + cfgs.Count + " -> " + now.Count);
            string act = rename.ContainsValue(active) ? rename.First(kv => kv.Value == active).Key : now[0];
            doc.ShowConfiguration2(act);
            if (problems.Count == 0 && save)
            {
                int e = 0, w = 0;
                if (!doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w)) problems.Add("save failed (" + e + ")");
                else Console.WriteLine("SAVED\t" + file);
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        foreach (var p in problems) Console.WriteLine("PROBLEM\t" + p);
        Console.WriteLine(problems.Count == 0 ? (save ? "OK (saved)" : "OK (look pass, not saved)") : "NOT SAVED");
        return problems.Count == 0 ? 0 : 1;
    }

    static string Desc(ModelDoc2 doc, string cfg)
    {
        string raw, res; bool wr, lk;
        doc.Extension.CustomPropertyManager[cfg].Get6("Description", false, out raw, out res, out wr, out lk);
        return res ?? "";
    }

    // The profile sketch: each dimension's value and the sketch points' box.
    static string SketchOf(ModelDoc2 doc)
    {
        var parts = new List<string>();
        foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
        {
            if (f.GetTypeName2() != "ProfileFeature") continue;
            var s = f.GetSpecificFeature2() as Sketch;
            double x0 = 1e9, y0 = 1e9, x1 = -1e9, y1 = -1e9; int n = 0;
            foreach (SketchPoint p in (object[])(s == null ? null : s.GetSketchPoints2()) ?? new object[0])
            { n++; x0 = Math.Min(x0, p.X); x1 = Math.Max(x1, p.X); y0 = Math.Min(y0, p.Y); y1 = Math.Max(y1, p.Y); }
            var dims = new List<string>();
            for (var dd = (DisplayDimension)f.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)f.GetNextDisplayDimension(dd))
            { var dim = (Dimension)dd.GetDimension2(0); dims.Add(dim.Name + "=" + Mm(dim.SystemValue)); }
            dims.Sort(StringComparer.Ordinal);
            parts.Add(f.Name + ": " + n + " points, box " + (n == 0 ? "none" : Mm(x0) + ".." + Mm(x1) + " x " + Mm(y0) + ".." + Mm(y1)) + "; " + string.Join(" ", dims));
        }
        return string.Join(" | ", parts);
    }
}
