// SwMaterial: read-only report of the SOLIDWORKS material on parts: per configuration the part material
// (database and name), each solid body's own material if it has one, and the mass, volume and density SOLIDWORKS uses.
// For checks like "what material do the sibling brackets use?" (08X04-BKP-01, 14 Nov). Opens each part read-only in the
// running SOLIDWORKS 2026, never saves, closes what it opened.
// Usage: SwMaterial.exe <part> [<part> ...]
// Run under SwCmd's lock: SwCmd lock --as sw first, SwCmd unlock --as sw after. Refuses Dropbox.
// Build (from tools\sw-macros):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwMaterial.exe SwMaterial.cs

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwMaterial
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwMaterial <part> [<part> ...]"); return 2; }
        if (System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length == 0) { Console.Error.WriteLine("SOLIDWORKS is not running; start it with SwCmd status/open first."); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026: " + sw.RevisionNumber()); return 2; }
        int rc = 0;
        foreach (var a in args)
        {
            string file = Path.GetFullPath(a);
            if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED (Dropbox): " + file); rc = 2; continue; }
            bool wasOpen = sw.GetOpenDocumentByName(file) != null;
            int errs = 0, warns = 0;
            var doc = wasOpen ? (ModelDoc2)sw.GetOpenDocumentByName(file)
                : sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
            if (doc == null) { Console.Error.WriteLine("Could not open " + file + " (errors " + errs + ")"); rc = 3; continue; }
            try
            {
                var part = (PartDoc)doc;
                string active = ((Configuration)doc.GetActiveConfiguration()).Name;
                foreach (string cfg in (string[])doc.GetConfigurationNames())
                {
                    if (((Configuration)doc.GetActiveConfiguration()).Name != cfg) doc.ShowConfiguration2(cfg);
                    string db;
                    string mat = part.GetMaterialPropertyName2(cfg, out db);
                    var mp = (MassProperty)doc.Extension.CreateMassProperty();
                    string mass = mp == null ? "no mass" : string.Format(Inv, "mass {0:0.###} kg, volume {1:0.###} cm3, density {2:0} kg/m3",
                        mp.Mass, mp.Volume * 1e6, mp.Volume > 0 ? mp.Mass / mp.Volume : 0);
                    Console.WriteLine("CFG\t" + Path.GetFileName(file) + "\t" + cfg + "\tmaterial \"" + (mat ?? "") + "\" (" + (db ?? "") + ")\t" + mass);
                    foreach (var b in ((part.GetBodies2((int)swBodyType_e.swSolidBody, false) as object[]) ?? new object[0]).Cast<Body2>())
                    {
                        string bdb;
                        string bm = b.GetMaterialPropertyName(cfg, out bdb);
                        if (!string.IsNullOrEmpty(bm)) Console.WriteLine("  BODY\t" + b.Name + "\tmaterial \"" + bm + "\" (" + bdb + ")");
                    }
                }
                if (((Configuration)doc.GetActiveConfiguration()).Name != active) doc.ShowConfiguration2(active);
            }
            finally { if (!wasOpen) sw.CloseDoc(doc.GetTitle()); }
        }
        return rc;
    }
}
