// set-material (SW for Admin, 14 Nov: 08X04-BKP-01 had no material, 0.15 kg instead of ~1.2 kg).
//   SwCmd set-material <part> <material> [--db <database>] (--config <cfg> | --all-configs) [--save] [--checked-out]
// Without --save: old -> new material and mass per configuration, then the part is closed again unsaved (if this
// command opened it). With --save: saved only if every configuration took the material and nothing else is wrong,
// then closed, re-opened read-only and read back (material and mass per configuration).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class SwCmd
{
    const string DefaultMaterialDb = "SOLIDWORKS Materials";

    static string MaterialOf(PartDoc part, string cfg)
    {
        string db;
        string name = part.GetMaterialPropertyName2(cfg, out db);
        return string.IsNullOrEmpty(name) ? "(none)" : name + " [" + db + "]";
    }

    static double MassNow(ModelDoc2 doc)
    {
        doc.ForceRebuild3(false);
        var mp = doc.Extension.CreateMassProperty() as MassProperty;
        if (mp == null) return double.NaN;
        mp.UseSystemUnits = true;
        return mp.Mass;
    }

    static string Kg(double m) { return double.IsNaN(m) ? "?" : m.ToString("0.###", Inv) + " kg"; }

    // Bodies with a material of their own keep it whatever the part's material is: listed, so a mass that doesn't
    // move can be explained.
    static List<string> BodyMaterials(PartDoc part, string cfg)
    {
        var r = new List<string>();
        foreach (var b in SolidBodies(part))
        {
            string db;
            string n = b.GetMaterialPropertyName(cfg, out db);
            if (!string.IsNullOrEmpty(n)) r.Add(b.Name + ": " + n + " [" + db + "]");
        }
        return r;
    }

    static int SetMaterial(ISldWorks sw, string path, string material, string db, string config, bool allConfigs, bool save, bool checkedOut)
    {
        if (Kind(path) != (int)swDocumentTypes_e.swDocPART) { problems.Add("set-material is for parts"); return Finish(); }
        if ((config == null) == !allConfigs) { problems.Add("set-material needs exactly one of --config <cfg> or --all-configs"); return Finish(); }
        db = string.IsNullOrEmpty(db) ? DefaultMaterialDb : db;
        bool wasOpen;
        var doc = OpenOrActivate(sw, path, null, out wasOpen);
        if (doc == null) return Finish();
        if (save && ReadOnlyOpen(doc)) return Finish();
        if (wasOpen && doc.GetSaveFlag()) { problems.Add(path + " is open with unsaved changes: close it first, nothing changed"); return Finish(); }
        var part = (PartDoc)doc;
        string active = ((Configuration)doc.GetActiveConfiguration()).Name;
        var all = ConfigNames(doc);
        List<string> cfgs;
        if (allConfigs) cfgs = all;
        else
        {
            var hit = all.FirstOrDefault(n => string.Equals(n, config, StringComparison.OrdinalIgnoreCase));
            if (hit == null) { problems.Add("no configuration " + config + " (has: " + string.Join(", ", all) + ")"); return Finish(); }
            cfgs = new List<string> { hit };
        }

        var oldMat = new Dictionary<string, string>();
        var oldMass = new Dictionary<string, double>();
        foreach (var c in cfgs)
        {
            if (!doc.ShowConfiguration2(c)) { problems.Add("could not show configuration " + c); continue; }
            oldMat[c] = MaterialOf(part, c);
            oldMass[c] = MassNow(doc);
        }
        if (problems.Count > 0) { Console.Error.WriteLine("Nothing changed: problems above."); return Finish(); }

        foreach (var c in cfgs) part.SetMaterialPropertyName2(c, db, material);
        string want = material + " [" + db + "]";
        Console.WriteLine("Material " + want + " set in " + cfgs.Count + " of " + all.Count + " configuration(s):");
        foreach (var c in cfgs)
        {
            doc.ShowConfiguration2(c);
            string now = MaterialOf(part, c);
            double m = MassNow(doc);
            Console.WriteLine("MATERIAL\t" + c + "\t" + oldMat[c] + " -> " + now + "\t" + Kg(oldMass[c]) + " -> " + Kg(m));
            if (!string.Equals(now, want, StringComparison.OrdinalIgnoreCase))
                problems.Add(c + ": the material reads " + now + ", not " + want + " (is the name in that database exactly?)");
            if (!(m > 0)) problems.Add(c + ": mass is " + Kg(m));
            foreach (var bm in BodyMaterials(part, c)) Console.WriteLine("  " + c + ": body with its own material (not changed): " + bm);
        }
        foreach (var c in all.Where(n => !cfgs.Contains(n))) Console.WriteLine("  not changed: " + c + " (" + MaterialOf(part, c) + ")");
        doc.ShowConfiguration2(active);
        ReportWhatsWrong(doc);

        if (!save)
        {
            Console.WriteLine("Not saved.");
            if (!wasOpen) { sw.CloseDoc(doc.GetTitle()); Console.WriteLine("Closed it again without saving: the file on disk is unchanged."); }
            else Console.WriteLine("It was open before this command, so it stays open WITH the change: close it without saving if it was a look.");
            return Finish();
        }
        if (problems.Count > 0) { Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
        bool rebuilt = doc.Extension.ForceRebuildAll();   // every configuration's stored mass/Weight current before the save
        if (!rebuilt) { problems.Add("ForceRebuildAll failed"); Console.Error.WriteLine("Not saved: problems above."); DiscardIfOpened(sw, doc, wasOpen); return Finish(); }
        if (!SaveDoc(sw, doc, checkedOut)) { DiscardIfOpened(sw, doc, wasOpen); return 2; }
        if (problems.Count > 0) return Finish();

        // Read back from disk.
        sw.CloseDoc(doc.GetTitle());
        bool again;
        var back = OpenPartIn(sw, path, null, true, out again);
        if (back == null) { problems.Add("could not re-open " + path + " to read it back"); return Finish(); }
        Console.WriteLine("Read back from disk:");
        foreach (var c in cfgs)
        {
            if (!back.ShowConfiguration2(c)) { problems.Add("read back: could not show " + c); continue; }
            string now = MaterialOf((PartDoc)back, c);
            double m = MassNow(back);
            Console.WriteLine("SAVED\t" + c + "\t" + now + "\t" + Kg(m));
            if (!string.Equals(now, want, StringComparison.OrdinalIgnoreCase)) problems.Add("read back: " + c + " has " + now);
            if (!(m > 0)) problems.Add("read back: " + c + " mass " + Kg(m));
        }
        sw.CloseDoc(back.GetTitle());
        Console.WriteLine("Closed it again (opened read-only for the read-back).");
        return Finish();
    }
}
