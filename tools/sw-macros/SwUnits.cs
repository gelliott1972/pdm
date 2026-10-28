// SwUnits: report the document units of SOLIDWORKS files (unit system, length, mass and their decimals) and the
// templates SOLIDWORKS uses on this PC. Opens every file read-only and never saves. Used for James's J4 (grams to kg).
// Usage: SwUnits.exe <file-or-folder> [...]           folders are searched for .sldprt/.sldasm/.slddrw
//        SwUnits.exe --try-custom <file> [...]        work copies only: in memory, switch to Custom and set mass to kg,
//                                                      then report again, so the side effects can be seen. Never saves.
// Output: tab-separated, one line per file. Refuses to run while SOLIDWORKS is already open, and refuses Dropbox.
// Build (from tools\sw-macros):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwUnits.exe SwUnits.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwUnits
{
    static readonly string[] UnitSystems = { "?", "CGS", "MKS", "IPS", "Custom", "MMGS" };
    static readonly string[] Masses = { "?", "mg", "g", "kg", "lb" };
    static readonly string[] Lengths = { "mm", "cm", "m", "in", "ft", "ft-in", "A", "nm", "um", "mil", "uin" };

    // name, preference
    static readonly KeyValuePair<string, swUserPreferenceIntegerValue_e>[] Prefs =
    {
        P("system", swUserPreferenceIntegerValue_e.swUnitSystem),
        P("length", swUserPreferenceIntegerValue_e.swUnitsLinear),
        P("length_dp", swUserPreferenceIntegerValue_e.swUnitsLinearDecimalPlaces),
        P("dual", swUserPreferenceIntegerValue_e.swUnitsDualLinear),
        P("dual_dp", swUserPreferenceIntegerValue_e.swUnitsDualLinearDecimalPlaces),
        P("angle_dp", swUserPreferenceIntegerValue_e.swUnitsAngularDecimalPlaces),
        P("mp_length", swUserPreferenceIntegerValue_e.swUnitsMassPropLength),
        P("mass", swUserPreferenceIntegerValue_e.swUnitsMassPropMass),
        P("volume", swUserPreferenceIntegerValue_e.swUnitsMassPropVolume),
        P("mass_dp", swUserPreferenceIntegerValue_e.swUnitsMassPropDecimalPlaces),
        P("force", swUserPreferenceIntegerValue_e.swUnitsForce),
        P("rounding", swUserPreferenceIntegerValue_e.swUnitsDecimalRounding),
    };
    static KeyValuePair<string, swUserPreferenceIntegerValue_e> P(string n, swUserPreferenceIntegerValue_e p)
    { return new KeyValuePair<string, swUserPreferenceIntegerValue_e>(n, p); }

    static int Main(string[] args)
    {
        bool tryCustom = args.Length > 0 && args[0] == "--try-custom";
        var inputs = args.Skip(tryCustom ? 1 : 0).ToList();
        if (inputs.Count == 0) { Console.Error.WriteLine("Usage: SwUnits [--try-custom] <file-or-folder> [...]"); return 2; }
        var files = new List<string>();
        foreach (var a in inputs)
        {
            string p = Path.GetFullPath(a);
            if (p.IndexOf("dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || p.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("Refused (Dropbox): " + p); return 2; }
            if (tryCustom && (p.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || p.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase)))
            { Console.Error.WriteLine("Refused (--try-custom is for work copies, not the vault): " + p); return 2; }
            if (Directory.Exists(p))
                files.AddRange(Directory.GetFiles(p, "*.*", SearchOption.AllDirectories)
                    .Where(f => Kind(f) != 0 && f.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) < 0 && !Path.GetFileName(f).StartsWith("~$"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            else if (File.Exists(p) && Kind(p) != 0) files.Add(p);
            else { Console.Error.WriteLine("Not found or not a SOLIDWORKS file: " + p); return 2; }
        }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        Console.Error.WriteLine("SOLIDWORKS " + sw.RevisionNumber() + " started; " + files.Count + " file(s).");
        try
        {
            Console.WriteLine("# templates");
            Console.WriteLine("default_part\t" + sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplatePart));
            Console.WriteLine("default_assembly\t" + sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateAssembly));
            Console.WriteLine("default_drawing\t" + sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swDefaultTemplateDrawing));
            Console.WriteLine("template_folders\t" + sw.GetUserPreferenceStringValue((int)swUserPreferenceStringValue_e.swFileLocationsDocumentTemplates));
            Console.WriteLine("always_use_default_templates\t" + sw.GetUserPreferenceToggle((int)swUserPreferenceToggle_e.swAlwaysUseDefaultTemplates));
            Console.WriteLine("# files");
            Console.WriteLine("file\topen_err\tstate\t" + string.Join("\t", Prefs.Select(p => p.Key)) + "\tWeight(file) raw\tWeight(file) value");
            foreach (var f in files)
            {
                int errs = 0, warns = 0;
                var doc = sw.OpenDoc6(f, Kind(f), (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
                if (doc == null) { Console.WriteLine(f + "\t" + errs + "\tNOT OPENED"); continue; }
                Console.WriteLine(f + "\t" + errs + "\tas is\t" + Row(doc));
                if (tryCustom)
                {
                    var x = doc.Extension;
                    bool a = x.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0, (int)swUnitSystem_e.swUnitSystem_Custom);
                    Console.WriteLine(f + "\t\tCustom (set " + a + ")\t" + Row(doc));
                    bool b = x.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitsMassPropMass, 0, (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms);
                    doc.ForceRebuild3(false);
                    Console.WriteLine(f + "\t\tCustom+kg (set " + b + ")\t" + Row(doc));
                }
                sw.CloseAllDocuments(true);
            }
        }
        finally
        {
            try { sw.CloseAllDocuments(true); sw.ExitApp(); } catch { }
            System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw);
            if (started != null && !started.WaitForExit(30000))
                try { started.Kill(); started.WaitForExit(10000); } catch { }
        }
        return 0;
    }

    static string Row(IModelDoc2 doc)
    {
        var x = doc.Extension;
        var cells = new List<string>();
        foreach (var p in Prefs)
        {
            int v = x.GetUserPreferenceInteger((int)p.Value, 0);
            string s = v.ToString();
            if (p.Key == "system" && v >= 0 && v < UnitSystems.Length) s = UnitSystems[v];
            else if (p.Key == "mass" && v >= 0 && v < Masses.Length) s = Masses[v];
            else if ((p.Key == "length" || p.Key == "dual" || p.Key == "mp_length") && v >= 0 && v < Lengths.Length) s = Lengths[v];
            cells.Add(s);
        }
        string raw, val; bool wasRes, linked;
        var cpm = x.CustomPropertyManager[""];
        int r = cpm.Get6("Weight", false, out raw, out val, out wasRes, out linked);
        cells.Add(r == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent ? "-" : raw);
        cells.Add(r == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent ? "-" : val);
        return string.Join("\t", cells);
    }

    static int Kind(string f)
    {
        switch (Path.GetExtension(f).ToLowerInvariant())
        {
            case ".sldprt": case ".prtdot": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": case ".asmdot": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": case ".drwdot": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    static Type NewestSolidWorks()
    {
        int best = -1;
        foreach (var n in Microsoft.Win32.Registry.ClassesRoot.GetSubKeyNames())
        {
            var m = System.Text.RegularExpressions.Regex.Match(n, @"^SldWorks\.Application\.(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            int v;
            if (m.Success && int.TryParse(m.Groups[1].Value, out v) && v > best) best = v;
        }
        return Type.GetTypeFromProgID(best >= 0 ? "SldWorks.Application." + best : "SldWorks.Application");
    }
}
