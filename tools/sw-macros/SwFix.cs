// SwFix: one open and one save per part for the sw-profiles overnight run (brief docs\briefs\sw-profiles.md, 16 Nov 2026):
// repoint structural members to the vault profile library (step 6), cut-list labels (step 6), SOLIDWORKS material and the
// Material property (step 9), plate Descriptions (step 10), end-cap Descriptions (step 12).
// Usage: SwFix.exe <part> <jobs.tsv> [--save] [--checked-out]
// jobs.tsv lines (tab-separated; # comments):
//   ROOT       <library root>                         P:\_Shared\_Profiles, or the staging copy for the rehearsal
//   MAP        <old library-relative file or full path> <old size> <new library-relative file> <new size>
//   REPOINT                                           every member not under ROOT: same file/size under ROOT, or its MAP line
//   MEMBERDESC <member feature> <Description>          cut-list folders whose bodies all come from that member
//   CAPDESC                                           folders whose bodies all come from End cap features:
//                                                     "END CAP a x b x t" (larger side x smaller side x thickness, measured)
//   PLATEDESC  <t x w x l measured> <Description>      folders with no member or end-cap body, whose first body measures so
//   MATERIAL   <name> <database>                      set in every configuration that has no SOLIDWORKS material
//   MATPROP                                           add the file-level Material = "SW-Material@<file>" if no Material property
// The cut-list Description is one value per cut-list folder for all configurations: wanted values are collected over
// every configuration first, and a folder wanted with two different values is not changed (listed as SKIP, which does not
// stop the save; nor do end caps sharing an item with a member, or caps of two sizes in one item).
// Checks, per configuration, before the save: each solid body's volume and the bounding box unchanged; no new rebuild
// error; every cut-list property other than the targeted Descriptions unchanged (resolved values); a configuration
// given a material has mass/volume within 1% of the material's density. After the save the file is re-opened
// read-only and measured again. Any PROBLEM: nothing is saved (exit 1).
// A P:\ file needs --checked-out and must be writable (checked out); other saves only under C:\code\pdm\scratch or C:\PDM-work.
// Run under SwCmd's lock (--as sw), after Close All (the Dropbox and vault profiles have the same file names).
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwFix.exe SwFix.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwFix
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string Mm(double v) { return (v * 1000).ToString("0.##", Inv); }
    static string VolFmt = "0.#";
    static List<string> problems = new List<string>();
    static List<string> skips = new List<string>();   // a Description left alone (listed for Grant); does not stop the save

    static int Main(string[] args)
    {
        // --vol-digits=N (N >= 1): print and compare body volumes to N decimals instead of 1. Only ever stricter; used
        // 18 Nov to see how big 08X04-SFP-03's "0.1 mm3" difference really is.
        var vd = args.FirstOrDefault(a => a.StartsWith("--vol-digits="));
        if (vd != null) { int n = Math.Max(1, int.Parse(vd.Substring(13))); VolFmt = "0." + new string('0', n); }
        var pos = args.Where(a => !a.StartsWith("--")).ToList();
        bool save = args.Contains("--save"), checkedOut = args.Contains("--checked-out");
        if (pos.Count != 2) { Console.Error.WriteLine("Usage: SwFix <part> <jobs.tsv> [--save] [--checked-out]"); return 2; }
        string file = Path.GetFullPath(pos[0]);
        if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith("M:", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED (Dropbox)"); return 2; }
        if (file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED: .pdm"); return 2; }
        bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || file.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (save && vault)
        {
            if (!checkedOut) { Console.Error.WriteLine("REFUSED: a vault file is written only with --checked-out"); return 2; }
            if ((File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) { Console.Error.WriteLine("REFUSED: the vault file is read-only, so it is not checked out"); return 2; }
        }
        if (save && !vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase) && !file.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED: saves only in P:\\ (checked out), C:\\code\\pdm\\scratch or C:\\PDM-work"); return 2; }

        string root = null; bool repoint = false, capDesc = false, matProp = false; string matName = null, matDb = null;
        var map = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
        var memberDesc = new Dictionary<string, string>(StringComparer.Ordinal);
        var plateDesc = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var l in File.ReadAllLines(pos[1]).Where(l => l.Trim() != "" && !l.StartsWith("#")))
        {
            var c = l.Split('\t');
            switch (c[0])
            {
                case "ROOT": root = Path.GetFullPath(c[1]).TrimEnd('\\'); break;
                case "MAP": map[c[1] + "|" + c[2]] = new[] { c[3], c[4] }; break;
                case "REPOINT": repoint = true; break;
                case "MEMBERDESC": memberDesc[c[1]] = c[2]; break;
                case "CAPDESC": capDesc = true; break;
                case "PLATEDESC": plateDesc[c[1]] = c[2]; break;
                case "MATERIAL": matName = c[1]; matDb = c[2]; break;
                case "MATPROP": matProp = true; break;
                default: Console.Error.WriteLine("Bad job line: " + l); return 2;
            }
        }
        if (repoint && (root == null || !Directory.Exists(root))) { Console.Error.WriteLine("REPOINT needs an existing ROOT"); return 2; }
        if (root != null && (root.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || root.StartsWith("M:", StringComparison.OrdinalIgnoreCase))) { Console.Error.WriteLine("REFUSED: ROOT in Dropbox"); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        if (sw.GetOpenDocumentByName(file) != null) { Console.Error.WriteLine("REFUSED: already open in SOLIDWORKS; close it first"); return 2; }
        int errs = 0, warns = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : swOpenDocOptions_e.swOpenDocOptions_ReadOnly)), "", ref errs, ref warns);
        if (doc == null) { Console.Error.WriteLine("Could not open " + file + " (errors " + errs + ")"); return 3; }
        var after = new Dictionary<string, string>();
        bool saved = false;
        int changes = 0;
        try
        {
            var part = (PartDoc)doc;
            var cfgs = (string[])doc.GetConfigurationNames();
            string active = doc.ConfigurationManager.ActiveConfiguration.Name;
            var before = new Dictionary<string, string>(); var beforeErr = new Dictionary<string, int>(); var beforeCut = new Dictionary<string, List<string>>();
            var beforeMat = new Dictionary<string, string>();
            foreach (var c in cfgs)
            {
                Show(doc, c);
                // rebuilt and cut list updated first: a stale stored cut list regroups on the next update (Dev, 15 Nov), which
                // would otherwise look like a change made here
                doc.ForceRebuild3(false); UpdateCut(doc);
                before[c] = Measure(doc); beforeErr[c] = Errors(doc); beforeCut[c] = CutSig(doc, false);
                string db; beforeMat[c] = part.GetMaterialPropertyName2(c, out db) ?? "";
                Console.WriteLine("BEFORE\t" + c + "\t" + before[c] + "\terrors " + beforeErr[c] + "\tmaterial " + (beforeMat[c] == "" ? "(none)" : beforeMat[c]));
            }
            Show(doc, active);

            // 1. Repoint
            if (repoint)
                foreach (Feature f in (object[])doc.FeatureManager.GetFeatures(false) ?? new object[0])
                {
                    if (f.GetTypeName2() != "WeldMemberFeat") continue;
                    var d = f.GetDefinition() as StructuralMemberFeatureData;
                    if (d == null) { problems.Add(f.Name + ": no definition"); continue; }
                    string oldPath = d.WeldmentProfilePath, oldSize = d.ConfigurationName, featName = f.Name;
                    if (oldPath.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("ALREADY\t" + f.Name + "\t" + oldPath + " (" + oldSize + ")"); continue; }
                    int i = oldPath.IndexOf(@"DLAPEX_Members\", StringComparison.OrdinalIgnoreCase);
                    string rel = i < 0 ? null : oldPath.Substring(i + @"DLAPEX_Members\".Length);
                    string[] m; string newRel, newSize;
                    if (rel != null && map.TryGetValue(rel + "|" + oldSize, out m)) { newRel = m[0]; newSize = m[1]; }
                    else if (map.TryGetValue(oldPath + "|" + oldSize, out m)) { newRel = m[0]; newSize = m[1]; }
                    else if (rel != null) { newRel = rel; newSize = oldSize; }
                    else { problems.Add(f.Name + ": straggler " + oldPath + " (" + oldSize + ") has no MAP line"); continue; }
                    string newPath = Path.Combine(root, newRel);
                    if (!File.Exists(newPath)) { problems.Add(f.Name + ": no profile file " + newPath); continue; }
                    if (!d.AccessSelections(doc, null)) { problems.Add(f.Name + ": could not edit its definition"); continue; }
                    d.WeldmentProfilePath = newPath;
                    d.ConfigurationName = newSize;
                    if (!f.ModifyDefinition(d, doc, null)) { d.ReleaseSelectionAccess(); problems.Add(f.Name + ": SOLIDWORKS refused " + newPath + " (" + newSize + ")"); continue; }
                    var d2 = f.GetDefinition() as StructuralMemberFeatureData;
                    Console.WriteLine("REPOINT\t" + f.Name + "\t" + oldPath + " (" + oldSize + ") -> " + d2.WeldmentProfilePath + " (" + d2.ConfigurationName + ")");
                    if (!string.Equals(d2.WeldmentProfilePath, newPath, StringComparison.OrdinalIgnoreCase) || d2.ConfigurationName != newSize) problems.Add(f.Name + ": reads back " + d2.WeldmentProfilePath + " (" + d2.ConfigurationName + ")");
                    // SOLIDWORKS renames the member feature from the new profile ("Rhs RHS80X40X8(1)" -> "Rhs 80x40x8 RHS80X40X8(1)")
                    if (f.Name != featName)
                    {
                        Console.WriteLine("RENAMED\t" + featName + " -> " + f.Name);
                        if (memberDesc.ContainsKey(featName)) memberDesc[f.Name] = memberDesc[featName];
                    }
                    changes++;
                }

            // 2. Material
            var matSet = new HashSet<string>();
            if (matName != null)
                foreach (var c in cfgs)
                {
                    if (beforeMat[c] != "") continue;
                    Show(doc, c);
                    part.SetMaterialPropertyName2(c, matDb, matName);
                    string db; string now = part.GetMaterialPropertyName2(c, out db) ?? "";
                    Console.WriteLine("MATERIAL\t" + c + "\t(none) -> " + now + " (" + db + ")");
                    if (now != matName) problems.Add(c + ": material reads " + now);
                    matSet.Add(c); changes++;
                }
            if (matProp)
            {
                bool has = false;
                foreach (var lvl in new[] { "" }.Concat(cfgs))
                {
                    var mgr = doc.Extension.CustomPropertyManager[lvl];
                    if (((string[])mgr.GetNames() ?? new string[0]).Any(n => n.Equals("Material", StringComparison.OrdinalIgnoreCase))) { has = true; break; }
                }
                if (!has)
                {
                    string v = "\"SW-Material@" + Path.GetFileName(file) + "\"";
                    int r = doc.Extension.CustomPropertyManager[""].Add3("Material", (int)swCustomInfoType_e.swCustomInfoText, v, (int)swCustomPropertyAddOption_e.swCustomPropertyOnlyIfNew);
                    Console.WriteLine("MATPROP\t(file)\tMaterial = " + v + (r == (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged ? "" : " NOT SET (" + r + ")"));
                    if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged) problems.Add("Material property not added (" + r + ")");
                    else changes++;
                }
                else Console.WriteLine("MATPROP\talready has a Material property");
            }

            // 3. Cut-list Descriptions: collect wanted values over every configuration, then set once per folder
            var wanted = new Dictionary<string, Dictionary<string, List<string>>>(StringComparer.Ordinal);   // folder -> value -> configs
            var targeted = new HashSet<string>(StringComparer.Ordinal);
            var replacedFrom = new HashSet<string>(StringComparer.Ordinal); var replacedTo = new HashSet<string>(StringComparer.Ordinal);   // SOLIDWORKS default text replaced
            if (memberDesc.Count > 0 || capDesc || plateDesc.Count > 0)
            {
                foreach (var c in cfgs)
                {
                    Show(doc, c);
                    doc.ForceRebuild3(false);
                    UpdateCut(doc);
                    foreach (var fo in Folders(doc))
                    {
                        var bodies = Bodies(fo);
                        if (bodies.Count == 0) continue;
                        var src = bodies.Select(Source).ToList();
                        string want = null;
                        if (src.All(s => s.Item1 == "WeldMemberFeat") && src.Select(s => s.Item2).Distinct().Count() == 1 && memberDesc.ContainsKey(src[0].Item2)) want = memberDesc[src[0].Item2];
                        else if (src.All(s => s.Item1 == "WeldMemberFeat") && memberDesc.Count > 0 && src.Select(s => s.Item2).Distinct().All(memberDesc.ContainsKey)
                                 && src.Select(s => memberDesc[s.Item2]).Distinct().Count() == 1) want = memberDesc[src[0].Item2];
                        else if (capDesc && src.All(s => s.Item1 == "EndCap") && System.Text.RegularExpressions.Regex.IsMatch(Desc(fo), @"^(RHS|SHS|EA|RSA)\s*\d"))
                        {   // Admin 16 Nov (08B04-KBP-01): a cap in an item labelled as a member length; changing it regroups the cut list
                            skips.Add(c + ": " + fo.Name + ": end cap in a cut-list item described " + Desc(fo) + " (a member's label): not changed"); continue;
                        }
                        else if (capDesc && src.All(s => s.Item1 == "EndCap"))
                        {
                            var sizes = bodies.Select(b => Plate(b)).Distinct().ToList();
                            if (sizes.Count != 1 || sizes[0] == null) { skips.Add(c + ": " + fo.Name + ": end caps of different sizes in one cut-list item: " + string.Join(" / ", sizes)); continue; }
                            var p = sizes[0].Split('x').Select(x => x.Trim()).ToList();   // t x w x l
                            want = "END CAP " + p[2] + "x" + p[1] + "x" + p[0];
                        }
                        else if (src.Any(s => s.Item1 == "EndCap") && src.Any(s => s.Item1 == "WeldMemberFeat") && capDesc)
                        { skips.Add(c + ": " + fo.Name + ": end cap and member bodies in one cut-list item (" + Desc(fo) + "): not changed"); continue; }
                        else if (plateDesc.Count > 0 && src.All(s => s.Item1 != "WeldMemberFeat" && s.Item1 != "EndCap"))
                        {
                            string pm = Plate(bodies[0]);
                            if (pm != null && plateDesc.ContainsKey(pm)) want = plateDesc[pm];
                        }
                        if (want == null) continue;
                        if (!wanted.ContainsKey(fo.Name)) wanted[fo.Name] = new Dictionary<string, List<string>>();
                        if (!wanted[fo.Name].ContainsKey(want)) wanted[fo.Name][want] = new List<string>();
                        wanted[fo.Name][want].Add(c);
                    }
                }
                foreach (var kv in wanted)
                {
                    if (kv.Value.Count > 1) { skips.Add(kv.Key + ": wanted as " + string.Join(" / ", kv.Value.Select(v => v.Key + " (" + string.Join(", ", v.Value) + ")")) + ": one Description serves every configuration, not changed"); continue; }
                    string want = kv.Value.Keys.First(), cfgShow = kv.Value[want][0];
                    Show(doc, cfgShow);
                    var fo = Folders(doc).FirstOrDefault(x => x.Name == kv.Key);
                    if (fo == null) { problems.Add(kv.Key + ": folder not found again in " + cfgShow); continue; }
                    string was = Desc(fo);
                    if (was == want) { Console.WriteLine("DESC\t" + kv.Key + "\talready " + want); continue; }
                    string raw, res; bool wr, lk;
                    fo.CustomPropertyManager.Get6("Description", false, out raw, out res, out wr, out lk);
                    // SOLIDWORKS's own end-cap/plate text, e.g. PLATE, "SW-Thickness" x "SW-Width" x "SW-Length": step 12 replaces it
                    bool swDefault = raw != null && raw.IndexOf("\"SW-", StringComparison.Ordinal) >= 0
                        && System.Text.RegularExpressions.Regex.Replace(raw, "\"SW-(Thickness|Width|Length)\"", "").IndexOf('"') < 0 && raw.IndexOf('@') < 0;
                    if (raw != null && raw != "" && !swDefault && (raw.IndexOf('@') >= 0 || raw.IndexOf('"') >= 0 || raw.StartsWith("$PRP", StringComparison.OrdinalIgnoreCase)))
                    { skips.Add(kv.Key + ": Description is an expression (" + raw + "), not changed"); continue; }
                    int r = fo.CustomPropertyManager.Add3("Description", (int)swCustomInfoType_e.swCustomInfoText, want, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged && swDefault)
                    {   // SOLIDWORKS's own expression refuses a replace: delete it, then add the text
                        if (fo.CustomPropertyManager.Set2("Description", want) == 0) r = (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged;
                        if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged) { skips.Add(kv.Key + ": SOLIDWORKS keeps its own Description (" + raw + "); not changed"); continue; }
                    }
                    if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged) { problems.Add(kv.Key + ": Description not set (" + r + ")"); continue; }
                    Console.WriteLine("DESC\t" + kv.Key + "\t" + (was == "" ? "(blank)" : was) + " -> " + want + "\t(" + string.Join(", ", kv.Value[want]) + ")");
                    targeted.Add(want);
                    if (swDefault) { replacedFrom.Add(was); replacedTo.Add(want); }
                    changes++;
                }
            }

            // 4. Checks
            foreach (var c in cfgs)
            {
                Show(doc, c);
                doc.ForceRebuild3(false);
                UpdateCut(doc);
                after[c] = Measure(doc);
                int er = Errors(doc);
                var cut = CutSig(doc, false);
                string db; string mat = part.GetMaterialPropertyName2(c, out db) ?? "";
                int st; var mpv = doc.Extension.GetMassProperties2(1, out st, false) as double[];   // cx, cy, cz, volume, area, mass, ...
                double mass = mpv == null ? 0 : mpv[5], dens = mpv != null && mpv[3] > 0 ? mpv[5] / mpv[3] : 0;
                Console.WriteLine("AFTER\t" + c + "\t" + after[c] + "\terrors " + er + "\tmaterial " + (mat == "" ? "(none)" : mat) + string.Format(Inv, "\tmass {0:0.###} kg, density {1:0}", mass, dens));
                if (after[c] != before[c]) problems.Add(c + ": shape changed: " + before[c] + " -> " + after[c]);
                if (er > beforeErr[c]) problems.Add(c + ": rebuild errors " + beforeErr[c] + " -> " + er);
                if (matSet.Contains(c))
                {
                    double want = matName == "Plain Carbon Steel" ? 7800 : 0;
                    if (want > 0 && Math.Abs(dens - want) > want * 0.01) problems.Add(c + ": density " + dens.ToString("0", Inv) + ", not " + want);
                }
                // cut-list properties: everything but the Description must read as before; Descriptions only changed to targeted values
                var b4 = beforeCut[c]; var now = cut;
                // Two exact consequences of the requested changes, nothing else: (1) step 9: an item's MATERIAL reads the material
                // just set where it read "Material <not specified>" (only in configurations given that material); (2) step 12:
                // an item whose SOLIDWORKS default text (PLATE, "SW-Thickness" x ...) was replaced loses SOLIDWORKS's
                // "3D-Bounding Box ..." properties, which belong to that default text.
                Func<string, string> noBox = x => System.Text.RegularExpressions.Regex.Replace(x, @"(?<=\| |; )3D-Bounding Box [^=;]*=[^;]*(; |$)", "").TrimEnd(' ', ';');
                var b4x = b4.Select(x => {
                        string s = StripDesc(x);
                        if (matSet.Contains(c)) s = s.Replace("MATERIAL=Material <not specified>", "MATERIAL=" + matName);
                        if (replacedFrom.Contains(DescOf(x))) s = noBox(s);
                        return s; }).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var nowx = now.Select(x => replacedTo.Contains(DescOf(x)) ? noBox(StripDesc(x)) : StripDesc(x)).OrderBy(x => x, StringComparer.Ordinal).ToList();
                // identical items merged or split (same properties, same total of bodies): a regrouping, reported, not a problem
                Func<List<string>, Dictionary<string, int>> totals = l => l.Select(x => {
                        int bar = x.IndexOf(" | "); int n = int.Parse(x.Substring(7, bar - 7));
                        string props = System.Text.RegularExpressions.Regex.Replace(x.Substring(bar + 3), @"(^|; )QUANTITY=[^;]*", "");
                        return Tuple.Create(props, n); })
                    .GroupBy(t => t.Item1).ToDictionary(g => g.Key, g => g.Sum(t => t.Item2));
                var tb = totals(b4x); var ta = totals(nowx);
                if (!b4x.SequenceEqual(nowx) && tb.Count == ta.Count && tb.All(kv => ta.ContainsKey(kv.Key) && ta[kv.Key] == kv.Value))
                    problems.Add(c + ": cut list REGROUPED: items " + b4x.Count + " -> " + nowx.Count + " (identical items merged or split; same bodies; changes BOM rows)");   // Admin 17 Nov: refused, listed for Grant
                else if (!b4x.SequenceEqual(nowx))
                {
                    var cb = b4x.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count()); var ca = nowx.GroupBy(x => x).ToDictionary(g => g.Key, g => g.Count());
                    var diff = cb.Keys.Union(ca.Keys).Where(k => (cb.ContainsKey(k) ? cb[k] : 0) != (ca.ContainsKey(k) ? ca[k] : 0))
                        .Select(k => (cb.ContainsKey(k) ? cb[k] : 0) + " -> " + (ca.ContainsKey(k) ? ca[k] : 0) + " x [" + k + "]");
                    problems.Add(c + ": cut-list items changed (other than Descriptions): " + string.Join(" ; ", diff));
                }
                foreach (var dsc in now.Select(DescOf).Except(b4.Select(DescOf)))
                    if (!targeted.Contains(dsc) && !memberDesc.Values.Contains(dsc)) problems.Add(c + ": a cut-list Description changed to " + dsc + " without being asked");
            }
            Show(doc, active);
            if (problems.Count == 0 && save && changes > 0)
            {
                int e = 0, w = 0;
                if (!doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref e, ref w)) problems.Add("save failed (errors " + e + ")");
                else { saved = true; Console.WriteLine("SAVED\t" + file); }
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }

        if (saved)
        {
            errs = 0; warns = 0;
            var rd = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
            if (rd == null) problems.Add("could not re-open after the save");
            else
                try
                {
                    foreach (var c in (string[])rd.GetConfigurationNames())
                    {
                        Show(rd, c);
                        string m = Measure(rd);
                        if (!after.ContainsKey(c) || m != after[c]) problems.Add("after the save, " + c + ": " + m);
                    }
                    Console.WriteLine("REOPENED\tsame shape in every configuration" + (problems.Count == 0 ? "" : " (see problems)"));
                }
                finally { sw.CloseDoc(rd.GetTitle()); }
        }
        foreach (var p in skips.Distinct()) Console.WriteLine("SKIP\t" + p);
        foreach (var p in problems) Console.WriteLine("PROBLEM\t" + p);
        Console.WriteLine(problems.Count == 0 ? (saved ? "OK (saved, " + changes + " changes)" : "OK (not saved, " + changes + " changes)") : (saved ? "SAVED WITH PROBLEMS" : "NOT SAVED"));
        return problems.Count == 0 ? 0 : 1;
    }

    static void Show(ModelDoc2 doc, string c) { if (doc.ConfigurationManager.ActiveConfiguration.Name != c) doc.ShowConfiguration2(c); }

    static void UpdateCut(ModelDoc2 doc)
    {
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
            if (f.GetTypeName2() == "SolidBodyFolder") { var bf = f.GetSpecificFeature2() as BodyFolder; if (bf != null) bf.UpdateCutList(); }
    }

    static List<Feature> Folders(ModelDoc2 doc)
    {
        var r = new List<Feature>();
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        {
            if (f.GetTypeName2() != "SolidBodyFolder") continue;
            for (var sub = f.GetFirstSubFeature() as Feature; sub != null; sub = sub.GetNextSubFeature() as Feature)
                if (sub.GetTypeName2() == "CutListFolder") r.Add(sub);
        }
        return r;
    }
    static List<Body2> Bodies(Feature fo)
    {
        var bf = fo.GetSpecificFeature2() as BodyFolder;
        return ((bf == null ? null : bf.GetBodies() as object[]) ?? new object[0]).Cast<Body2>().ToList();
    }
    static Tuple<string, string> Source(Body2 b)
    {
        foreach (Feature f in (object[])b.GetFeatures() ?? new object[0])
        {
            string t = f.GetTypeName2();
            if (t == "WeldMemberFeat" || t == "EndCap") return Tuple.Create(t, f.Name);
        }
        return Tuple.Create("", "");
    }
    static string Desc(Feature fo)
    {
        string raw, res; bool wr, lk;
        fo.CustomPropertyManager.Get6("Description", false, out raw, out res, out wr, out lk);
        return res ?? "";
    }

    // Each cut-list item as "bodies n | prop=resolved; ..." (Description listed first so it can be split off)
    static List<string> CutSig(ModelDoc2 doc, bool unused)
    {
        var r = new List<string>();
        foreach (var fo in Folders(doc))
        {
            var bodies = Bodies(fo);
            var pm = fo.CustomPropertyManager;
            var props = new List<string>();
            foreach (var n in ((pm.GetNames() as object[]) ?? new object[0]).Cast<string>().OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
            {
                if (n.Equals("Description", StringComparison.OrdinalIgnoreCase)) continue;
                // SOLIDWORKS's own measurements of the body, dropped when an item gets a Description (GNRIC-LM rehearsal,
                // 17 Nov); the bodies' volumes and box are compared exactly instead
                if (n.StartsWith("3D-Bounding Box", StringComparison.OrdinalIgnoreCase)) continue;
                string raw, res; bool wr, lk;
                pm.Get6(n, false, out raw, out res, out wr, out lk);
                props.Add(n + "=" + res);
            }
            r.Add("DESC=" + Desc(fo) + "\u0001bodies " + bodies.Count + " | " + string.Join("; ", props));
        }
        return r;
    }
    static string StripDesc(string s) { int i = s.IndexOf('\u0001'); return i < 0 ? s : s.Substring(i + 1); }
    static string DescOf(string s) { int i = s.IndexOf('\u0001'); return i < 0 ? "" : s.Substring(5, i - 5); }

    static int Errors(ModelDoc2 doc)
    {
        object feats, codes, warnFlags;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warnFlags);
        var ef = feats as object[]; var wf = warnFlags as bool[];
        int n = 0;
        if (ef != null) for (int i = 0; i < ef.Length; i++) if (wf == null || i >= wf.Length || !wf[i]) n++;
        return n;
    }

    static string Measure(ModelDoc2 doc)
    {
        var vols = new List<string>(); double[] box = null;
        foreach (Body2 b in ((object[])((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, true) ?? new object[0]))
        {
            var mp = b.GetMassProperties(1) as double[];
            vols.Add(mp == null ? "?" : (mp[3] * 1e9).ToString(VolFmt, Inv));
            var bb = (double[])b.GetBodyBox();
            if (box == null) box = (double[])bb.Clone();
            else for (int i = 0; i < 3; i++) { box[i] = Math.Min(box[i], bb[i]); box[3 + i] = Math.Max(box[3 + i], bb[3 + i]); }
        }
        vols.Sort(StringComparer.Ordinal);
        return vols.Count + " bodies (mm3 " + string.Join(", ", vols) + "); box " + (box == null ? "none" : Mm(box[0]) + ".." + Mm(box[3]) + ", " + Mm(box[1]) + ".." + Mm(box[4]) + ", " + Mm(box[2]) + ".." + Mm(box[5]));
    }

    // "t x w x l" of a plate-like body (as SwProfiles), or null
    static string Plate(Body2 b)
    {
        Face2 big = null; double area = 0;
        foreach (Face2 f in (object[])b.GetFaces() ?? new object[0])
            if (((Surface)f.GetSurface()).IsPlane() && f.GetArea() > area) { area = f.GetArea(); big = f; }
        if (big == null) return null;
        var n = (double[])big.Normal;
        double[] u = null; double best = 0;
        var pts = new List<double[]>();
        foreach (Edge e in (object[])b.GetEdges() ?? new object[0])
        {
            var cp = (double[])e.GetCurveParams2();
            pts.Add(new[] { cp[0], cp[1], cp[2] }); pts.Add(new[] { cp[3], cp[4], cp[5] });
            if (!((Curve)e.GetCurve()).IsLine()) continue;
            double dx = cp[3] - cp[0], dy = cp[4] - cp[1], dz = cp[5] - cp[2], len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-9 || Math.Abs((dx * n[0] + dy * n[1] + dz * n[2]) / len) > 1e-4) continue;
            if (len > best) { best = len; u = new[] { dx / len, dy / len, dz / len }; }
        }
        if (u == null) return null;
        var v = new[] { n[1] * u[2] - n[2] * u[1], n[2] * u[0] - n[0] * u[2], n[0] * u[1] - n[1] * u[0] };
        Func<double[], double> ext = d => { var pr = pts.Select(p => p[0] * d[0] + p[1] * d[1] + p[2] * d[2]).ToList(); return pr.Max() - pr.Min(); };
        double t = ext(n), a = ext(u), c = ext(v);
        return Mm(t) + " x " + Mm(Math.Min(a, c)) + " x " + Mm(Math.Max(a, c));
    }
}
