// set-cutlist-prop (Admin, 15 Nov: GNRIC-LMW's cut-list Description "RHS40x40x4" -> "SHS40x40x4"; later ~51 more
// RHS/SHS labels from draw\cutlist-survey.md).
//   SwCmd set-cutlist-prop <part> <property> <new value> [--body <body>] [--item <cut-list folder>] [--where-value <current>]
//         (--config <cfg> | --all-configs) [--save] [--checked-out]
// The cut-list items changed are those matching every selector given (at least one): a body of that name in the item,
// the item's folder name, the property's current value (exact). Items are known by their bodies' names, because the
// folder can be renamed from its Description. Only plain-text values are changed: a linked or expression value
// ($PRP, SW-..., "...@...") is refused. Before the save every cut-list property of every item, in every configuration
// done, is compared with before: only the targeted property of the targeted items may differ, and it must read the new
// value. After the save the file is re-opened read-only and compared again.
using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class SwCmd
{
    class CutItem
    {
        public Feature Feat;
        public string Folder, Key;
        public List<string> Bodies;
        public Dictionary<string, string[]> Props;   // name -> { raw, resolved, linked }
    }

    static List<CutItem> CutItems(ModelDoc2 doc)
    {
        var r = new List<CutItem>();
        // Rebuilt first: a stale cut list is regrouped (and bodies renamed) by the next rebuild, which then looks like a
        // change (15 Nov test, GNRIC-LMW EC-0+5M+0-EC: end caps in two items, one after the rebuild).
        doc.ForceRebuild3(false);
        for (var f = doc.FirstFeature() as Feature; f != null; f = f.GetNextFeature() as Feature)
        {
            if (f.GetTypeName2() != "SolidBodyFolder") continue;
            var bf = f.GetSpecificFeature2() as BodyFolder;
            if (bf != null) bf.UpdateCutList();
            for (var sub = f.GetFirstSubFeature() as Feature; sub != null; sub = sub.GetNextSubFeature() as Feature)
            {
                if (sub.GetTypeName2() != "CutListFolder") continue;
                var folder = sub.GetSpecificFeature2() as BodyFolder;
                var bodies = (folder != null ? (folder.GetBodies() as object[] ?? new object[0]) : new object[0]).Cast<Body2>().Select(b => b.Name).OrderBy(x => x, StringComparer.Ordinal).ToList();
                var props = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
                var pm = sub.CustomPropertyManager;
                foreach (var name in ((pm.GetNames() as object[]) ?? new object[0]).Cast<string>())
                {
                    string raw, res; bool wasRes, link;
                    pm.Get6(name, false, out raw, out res, out wasRes, out link);
                    props[name] = new[] { raw ?? "", res ?? "", link ? "linked" : "" };
                }
                r.Add(new CutItem { Feat = sub, Folder = sub.Name, Bodies = bodies, Key = string.Join(" + ", bodies), Props = props });
            }
        }
        // Two items with the same bodies' names (not expected): make the keys unique by order.
        foreach (var g in r.GroupBy(i => i.Key).Where(g => g.Count() > 1)) { int k = 0; foreach (var i in g) i.Key += " #" + (++k); }
        return r;
    }

    static bool PlainText(string[] v)
    {
        string raw = v[0];
        return v[2] == "" && raw == v[1] && raw.IndexOf('@') < 0 && raw.IndexOf('"') < 0 && !raw.StartsWith("$PRP", StringComparison.OrdinalIgnoreCase)
            && !raw.StartsWith("SW-", StringComparison.OrdinalIgnoreCase);
    }

    // Every cut-list property as "key | property" -> resolved value, for comparing.
    static Dictionary<string, string> CutSnapshot(List<CutItem> items)
    {
        var d = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var i in items)
            foreach (var kv in i.Props)
                d[i.Key + " | " + kv.Key] = kv.Value[0].Replace("@@@" + i.Folder + "@", "@@@<folder>@") + " => " + kv.Value[1];
        return d;
    }

    // Differences between two snapshots: only the targeted (key, property) may change, and to the new value.
    static void CompareCut(string cfg, Dictionary<string, string> before, Dictionary<string, string> after, HashSet<string> targets, string prop, string newValue, string when)
    {
        foreach (var k in before.Keys.Union(after.Keys).OrderBy(x => x, StringComparer.Ordinal))
        {
            string b, a;
            before.TryGetValue(k, out b); after.TryGetValue(k, out a);
            bool targeted = targets.Contains(k);
            if (targeted)
            {
                string want = newValue + " => " + newValue;
                if (a != want) problems.Add(when + cfg + ": " + k + " reads " + (a ?? "(missing)") + ", not " + newValue);
            }
            else if (a != b) problems.Add(when + cfg + ": " + k + " changed too: " + (b ?? "(missing)") + " -> " + (a ?? "(missing)"));
        }
    }

    static int SetCutListProp(ISldWorks sw, string path, string prop, string newValue, string body, string item, string whereValue,
        string config, bool allConfigs, bool save, bool checkedOut)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocPART) { problems.Add("set-cutlist-prop is for parts"); return Finish(); }
        if ((config == null) == !allConfigs) { problems.Add("set-cutlist-prop needs exactly one of --config <cfg> or --all-configs"); return Finish(); }
        if (body == null && item == null && whereValue == null) { problems.Add("set-cutlist-prop needs --body, --item or --where-value"); return Finish(); }
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (save && ReadOnlyOpen(doc)) return Finish();
        if (wasOpen && doc.GetSaveFlag()) { problems.Add(path + " is open with unsaved changes: close it first, nothing changed"); return Finish(); }
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        var all = ConfigNames(doc);
        List<string> cfgs;
        if (allConfigs) cfgs = all;
        else
        {
            var hit = all.FirstOrDefault(n => string.Equals(n, config, StringComparison.OrdinalIgnoreCase));
            if (hit == null) { problems.Add("no configuration " + config + " (has: " + string.Join(", ", all) + ")"); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
            cfgs = new List<string> { hit };
        }
        Console.WriteLine("Selecting cut-list items: " + string.Join(", ", new[] {
            body != null ? "body \"" + body + "\"" : null, item != null ? "folder \"" + item + "\"" : null,
            whereValue != null ? prop + " = \"" + whereValue + "\"" : null }.Where(x => x != null)));

        // 1. Read every configuration first (a cut-list value may be shared by all configurations: GNRIC-LMW's
        //    Description is, 15 Nov test), then 2. change, then 3. compare every configuration with 1.
        var before = new Dictionary<string, Dictionary<string, string>>();
        var targets = new Dictionary<string, HashSet<string>>();
        var folderBefore = new Dictionary<string, string>();
        int changed = 0;
        foreach (var c in cfgs)
        {
            if (!Show(doc, c)) { problems.Add("could not show configuration " + c); continue; }
            var items = CutItems(doc);
            before[c] = CutSnapshot(items);
            targets[c] = new HashSet<string>(StringComparer.Ordinal);
            if (items.Count == 0) { Console.WriteLine("  " + c + ": no cut list"); continue; }
            foreach (var it in items)
            {
                folderBefore[it.Key] = it.Folder;
                string[] v;
                it.Props.TryGetValue(prop, out v);
                bool m = (body == null || it.Bodies.Any(b => string.Equals(b, body, StringComparison.OrdinalIgnoreCase)))
                    && (item == null || string.Equals(it.Folder, item, StringComparison.OrdinalIgnoreCase))
                    && (whereValue == null || (v != null && v[1] == whereValue));
                if (!m) continue;
                string was = v == null ? "(no " + prop + ")" : v[1] + (v[0] != v[1] ? " (" + v[0] + ")" : "") + (v[2] != "" ? " [linked]" : "");
                if (v != null && !PlainText(v)) { problems.Add(c + ": " + it.Folder + " [" + it.Key + "]: " + prop + " is not plain text (" + was + "); not changed"); continue; }
                targets[c].Add(it.Key + " | " + prop);
                Console.WriteLine("MATCH\t" + c + "\t" + it.Folder + "\t[" + it.Key + "]\t" + prop + " = " + was);
            }
            if (targets[c].Count == 0) Console.WriteLine("  " + c + ": no cut-list item matches");
        }
        if (targets.Values.Sum(t => t.Count) == 0) problems.Add("no cut-list item matches in " + (allConfigs ? "any configuration" : cfgs[0]));
        if (problems.Count == 0)
            foreach (var c in cfgs.Where(x => targets.ContainsKey(x) && targets[x].Count > 0))
            {
                if (!Show(doc, c)) { problems.Add("could not show configuration " + c); continue; }
                foreach (var it in CutItems(doc).Where(x => targets[c].Contains(x.Key + " | " + prop)))
                {
                    string[] v;
                    it.Props.TryGetValue(prop, out v);
                    if (v != null && v[0] == newValue) continue;   // already (set in an earlier configuration, or was)
                    int r = it.Feat.CustomPropertyManager.Add3(prop, (int)swCustomInfoType_e.swCustomInfoText, newValue, (int)swCustomPropertyAddOption_e.swCustomPropertyReplaceValue);
                    if (r != (int)swCustomInfoAddResult_e.swCustomInfoAddResult_AddedOrChanged) problems.Add(c + ": " + it.Folder + ": SOLIDWORKS did not set " + prop + " (result " + r + ")");
                    Console.WriteLine("SET\t" + c + "\t" + it.Folder + "\t[" + it.Key + "]\t" + prop + ": " + (v == null ? "(none)" : v[1]) + " -> " + newValue);
                    changed++;
                }
            }


        var renamed = new HashSet<string>();
        // Compare every configuration done with before (setting in one configuration may change it in others).
        foreach (var c in cfgs.Where(before.ContainsKey))
        {
            if (!Show(doc, c)) { problems.Add("could not show configuration " + c); continue; }
            var items = CutItems(doc);
            foreach (var it in items)
            {
                if (targets[c].Contains(it.Key + " | " + prop)) Console.WriteLine("  " + c + ": " + it.Folder + " [" + it.Key + "] now " + prop + " = " + (it.Props.ContainsKey(prop) ? it.Props[prop][1] : "(missing)"));
                string fb;
                if (folderBefore.TryGetValue(it.Key, out fb) && fb != it.Folder && renamed.Add(fb)) Console.WriteLine("  cut-list folder renamed by SOLIDWORKS: " + fb + " -> " + it.Folder);
            }
            CompareCut(c, before[c], CutSnapshot(items), targets[c], prop, newValue, "");
        }
        Show(doc, active);
        Console.WriteLine(changed + " value(s) set; " + targets.Values.Sum(t => t.Count) + " targeted item(s) over " + cfgs.Count + " configuration(s)");

        if (!save)
        {
            Console.WriteLine("Not saved.");
            if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving: the file on disk is unchanged."); }
            else Console.WriteLine("It was open before this command, so it stays open WITH the change: close it without saving if it was a look.");
            return Finish();
        }
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
        if (changed == 0) { Console.WriteLine("Nothing to save: every targeted value already reads " + newValue + "."); if (!wasOpen) sw.CloseDoc(doc.GetTitle()); return Finish(); }
        bool rebuilt = doc.Extension.ForceRebuildAll();
        if (!rebuilt) { problems.Add("ForceRebuildAll failed"); Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
        if (!SaveDoc(sw, doc, checkedOut)) { DiscardIfOpened(sw, doc, wasOpen); return 2; }
        if (problems.Count > 0) return Finish();

        sw.CloseDoc(doc.GetTitle());
        bool again;
        var back = OpenPartIn(sw, path, null, true, out again);
        if (back == null) { problems.Add("could not re-open " + path + " to read it back"); return Finish(); }
        Console.WriteLine("Read back from disk:");
        foreach (var c in cfgs.Where(before.ContainsKey))
        {
            if (!Show(back, c)) { problems.Add("read back: could not show " + c); continue; }
            var items = CutItems(back);
            foreach (var it in items) if (targets[c].Contains(it.Key + " | " + prop)) Console.WriteLine("SAVED\t" + c + "\t" + it.Folder + "\t[" + it.Key + "]\t" + prop + " = " + (it.Props.ContainsKey(prop) ? it.Props[prop][1] : "(missing)"));
            CompareCut(c, before[c], CutSnapshot(items), targets[c], prop, newValue, "read back: ");
        }
        sw.CloseDoc(back.GetTitle());
        Console.WriteLine("Closed it again (opened read-only for the read-back).");
        return Finish();
    }
}
