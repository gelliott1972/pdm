// SwRepoint: repoint a part's structural members to the vault profile library (brief sw-profiles, step 6, 16 Nov 2026).
// For every structural member whose profile is not already under <library root>: the same file and size under the root
// (the path after "DLAPEX_Members\"), or what a --map line says. SOLIDWORKS re-reads the profile, so per configuration
// the bodies' volumes and the bounding box are compared before and after, and rebuild errors counted; any difference
// refuses the save. Prints the cut-list Descriptions after (the labels are checked by the survey's analysis).
// Usage: SwRepoint.exe <part> --root <library root> [--map <tsv>] [--save] [--checked-out]
//   --root: P:\_Shared\_Profiles tonight; scratch\profiles\_Profiles for the rehearsal.
//   --map: tab-separated lines "<old library-relative file>\t<old size>\t<new library-relative file>\t<new size>"
//          (e.g. a size whose name is wrong in the library: Grant's call). Lines starting # are skipped.
//   Without --save: a look pass (changes made in memory, closed without saving).
//   A P:\ file needs --checked-out and must be writable; other saves only under C:\code\pdm\scratch or C:\PDM-work.
// Run under SwCmd's lock (--as sw), after Close All (the Dropbox and vault profiles have the same file names).
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwRepoint.exe SwRepoint.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwRepoint
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.###", Inv); }

    static string LibRel(string path)
    {
        int i = path.IndexOf(@"DLAPEX_Members\", StringComparison.OrdinalIgnoreCase);
        return i < 0 ? null : path.Substring(i + @"DLAPEX_Members\".Length);
    }

    static int Main(string[] args)
    {
        var pos = new List<string>(); var opts = new Dictionary<string, string>();
        for (int i = 0; i < args.Length; i++)
            if (args[i] == "--root" || args[i] == "--map") opts[args[i].Substring(2)] = args[++i];
            else if (args[i].StartsWith("--")) opts[args[i].Substring(2)] = "";
            else pos.Add(args[i]);
        bool save = opts.ContainsKey("save"), checkedOut = opts.ContainsKey("checked-out");
        if (pos.Count != 1 || !opts.ContainsKey("root")) { Console.Error.WriteLine("Usage: SwRepoint <part> --root <library root> [--map <tsv>] [--save] [--checked-out]"); return 2; }
        string file = Path.GetFullPath(pos[0]), root = Path.GetFullPath(opts["root"]).TrimEnd('\\');
        if (!Directory.Exists(root)) { Console.Error.WriteLine("No library root " + root); return 2; }
        foreach (var p in new[] { file, root })
            if (p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || p.StartsWith("M:", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED (Dropbox): " + p); return 2; }
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED: .pdm"); return 2; }
        if (save && vault)
        {
            if (!checkedOut) { Console.Error.WriteLine("REFUSED: a vault file is written only with --checked-out"); return 2; }
            if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) { Console.Error.WriteLine("REFUSED: the vault file is read-only, so it is not checked out"); return 2; }
        }
        if (save && !vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase) && !file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED: saves only in P:\\ (checked out), C:\\code\\pdm\\scratch or C:\\PDM-work"); return 2; }
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        if (opts.ContainsKey("map"))
            foreach (var l in File.ReadAllLines(opts["map"]).Where(l => l.Trim() != "" && !l.StartsWith("#")))
            { var c = l.Split('\t'); if (c.Length != 4) { Console.Error.WriteLine("Bad map line: " + l); return 2; } map[c[0] + "|" + c[1]] = new[] { c[2], c[3] }; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026: " + sw.RevisionNumber()); return 2; }
        if (sw.GetOpenDocumentByName(file) != null) { Console.Error.WriteLine("REFUSED: already open in SOLIDWORKS; close it first: " + file); return 2; }
        int errs = 0, warns = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : swOpenDocOptions_e.swOpenDocOptions_ReadOnly)), "", ref errs, ref warns);
        if (doc == null) { Console.Error.WriteLine("Could not open " + file + " (errors " + errs + ")"); return 3; }
        var problems = new List<string>();
        int changed = 0;
        try
        {
            var cfgs = (string[])doc.GetConfigurationNames();
            string active = doc.ConfigurationManager.ActiveConfiguration.Name;
            var before = new Dictionary<string, string>();
            foreach (var c in cfgs) { doc.ShowConfiguration2(c); doc.ForceRebuild3(false); before[c] = Measure(doc); Console.WriteLine("BEFORE\t" + c + "\t" + before[c] + "\terrors " + Errors(doc)); }
            doc.ShowConfiguration2(active);
            foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
            {
                if (f.GetTypeName2() != "WeldMemberFeat") continue;
                var d = f.GetDefinition() as StructuralMemberFeatureData;
                if (d == null) { problems.Add(f.Name + ": no definition"); continue; }
                string oldPath = d.WeldmentProfilePath, oldSize = d.ConfigurationName;
                if (oldPath.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("ALREADY\t" + f.Name + "\t" + oldPath + "\t" + oldSize); continue; }
                string rel = LibRel(oldPath);
                string[] m;
                string newRel, newSize;
                if (rel != null && map.TryGetValue(rel + "|" + oldSize, out m)) { newRel = m[0]; newSize = m[1]; }
                else if (map.TryGetValue(oldPath + "|" + oldSize, out m)) { newRel = m[0]; newSize = m[1]; }   // a straggler, by full path
                else if (rel != null) { newRel = rel; newSize = oldSize; }
                else { problems.Add(f.Name + ": straggler " + oldPath + " (" + oldSize + ") has no --map line"); continue; }
                string newPath = Path.Combine(root, newRel);
                if (!File.Exists(newPath)) { problems.Add(f.Name + ": no profile file " + newPath); continue; }
                if (!d.AccessSelections(doc, null)) { problems.Add(f.Name + ": could not edit its definition"); continue; }
                d.WeldmentProfilePath = newPath;
                d.ConfigurationName = newSize;
                bool ok = f.ModifyDefinition(d, doc, null);
                if (!ok) { d.ReleaseSelectionAccess(); problems.Add(f.Name + ": SOLIDWORKS refused the new profile " + newPath + " (" + newSize + ")"); continue; }
                var d2 = f.GetDefinition() as StructuralMemberFeatureData;
                Console.WriteLine("REPOINT\t" + f.Name + "\t" + oldPath + " (" + oldSize + ")\t-> " + d2.WeldmentProfilePath + " (" + d2.ConfigurationName + ")");
                if (!string.Equals(d2.WeldmentProfilePath, newPath, StringComparison.OrdinalIgnoreCase) || d2.ConfigurationName != newSize) problems.Add(f.Name + ": reads back " + d2.WeldmentProfilePath + " (" + d2.ConfigurationName + ")");
                changed++;
            }
            foreach (var c in cfgs)
            {
                doc.ShowConfiguration2(c);
                doc.ForceRebuild3(false);
                for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
                    if (f.GetTypeName2() == "SolidBodyFolder") { var bf = f.GetSpecificFeature2() as BodyFolder; if (bf != null) bf.UpdateCutList(); }
                string after = Measure(doc); int er = Errors(doc);
                Console.WriteLine("AFTER\t" + c + "\t" + after + "\terrors " + er);
                if (after != before[c]) problems.Add(c + ": shape changed");
                if (er > 0) problems.Add(c + ": " + er + " rebuild error(s)");
                for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
                {
                    if (f.GetTypeName2() != "SolidBodyFolder") continue;
                    for (var sub = f.GetFirstSubFeature() as Feature; sub != null; sub = sub.GetNextSubFeature() as Feature)
                    {
                        if (sub.GetTypeName2() != "CutListFolder") continue;
                        string raw, res; bool wr, lk;
                        sub.CustomPropertyManager.Get6("Description", false, out raw, out res, out wr, out lk);
                        Console.WriteLine("CUT\t" + c + "\t" + sub.Name + "\t" + res);
                    }
                }
            }
            doc.ShowConfiguration2(active);
            if (changed == 0 && problems.Count == 0) Console.WriteLine("Nothing to repoint");
            if (problems.Count == 0 && save && changed > 0)
            {
                int e = 0, w = 0;
                if (!doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w)) problems.Add("save failed (errors " + e + ")");
                else Console.WriteLine("SAVED\t" + file);
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        foreach (var p in problems) Console.WriteLine("PROBLEM\t" + p);
        Console.WriteLine(problems.Count == 0 ? (save && changed > 0 ? "OK (saved, " + changed + " members)" : "OK (not saved, " + changed + " members)") : "NOT SAVED");
        return problems.Count == 0 ? 0 : 1;
    }

    static int Errors(ModelDoc2 doc)
    {
        object feats, codes, warnFlags;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warnFlags);
        var ef = feats as object[]; var wf = warnFlags as bool[];
        int n = 0;
        if (ef != null) for (int i = 0; i < ef.Length; i++) if (wf == null || i >= wf.Length || !wf[i]) n++;
        return n;
    }

    // Per configuration: each solid body's volume (sorted) and the bounding box of all of them.
    static string Measure(ModelDoc2 doc)
    {
        var vols = new List<string>(); double[] box = null;
        foreach (Body2 b in ((object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]))
        {
            var mp = b.GetMassProperties(1) as double[];
            vols.Add(mp == null ? "?" : (mp[3] * 1e9).ToString("0.#", Inv));
            var bb = (double[])b.GetBodyBox();
            if (box == null) box = (double[])bb.Clone();
            else for (int i = 0; i < 3; i++) { box[i] = Math.Min(box[i], bb[i]); box[3 + i] = Math.Max(box[3 + i], bb[3 + i]); }
        }
        vols.Sort(StringComparer.Ordinal);
        return vols.Count + " bodies (mm3 " + string.Join(", ", vols) + "); box " + (box == null ? "none" : Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]));
    }
}
