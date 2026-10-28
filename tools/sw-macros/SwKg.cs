// SwKg: set a SOLIDWORKS file's mass unit to kilograms (James's J4), keeping every other unit setting as it was.
// MMGS fixes mass to grams, so a file not already Custom is switched to Custom first; the tool then checks that
// length, angle, decimals and the rest read back unchanged before it sets kg. Rebuilds every configuration and saves.
// Parts, assemblies and drawings. Each file gets its own SOLIDWORKS start, with one retry if that start crashes
// (as SwResave). A file already in kg is left alone and not saved.
//
// Usage:
//   SwKg.exe [--dry] [--vault] <file-or-folder> [...]
//       Folders are searched for .sldprt/.sldasm/.slddrw. Order: parts, then assemblies, then drawings.
//       Refuses Dropbox and anything under .pdm always; refuses the vault (P:\, C:\PDM\) unless --vault is given.
//       A file that is read-only on disk (not checked out) is reported NOT SAVED. --dry opens nothing, lists the order.
// Exit code: 0 all done, 1 any failure, 2 refused or bad arguments.
// Refuses to start while SOLIDWORKS is already open. Build (from tools\sw-macros):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwKg.exe SwKg.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwKg
{
    // Every unit setting compared before and after the switch to Custom (system and mass are the ones we change).
    static readonly swUserPreferenceIntegerValue_e[] Kept =
    {
        swUserPreferenceIntegerValue_e.swUnitsLinear, swUserPreferenceIntegerValue_e.swUnitsLinearDecimalDisplay,
        swUserPreferenceIntegerValue_e.swUnitsLinearDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsLinearFractionDenominator,
        swUserPreferenceIntegerValue_e.swUnitsAngular, swUserPreferenceIntegerValue_e.swUnitsAngularDecimalPlaces,
        swUserPreferenceIntegerValue_e.swUnitsDualLinear, swUserPreferenceIntegerValue_e.swUnitsDualLinearDecimalDisplay,
        swUserPreferenceIntegerValue_e.swUnitsDualLinearDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsDualLinearFractionDenominator,
        swUserPreferenceIntegerValue_e.swUnitsMassPropLength, swUserPreferenceIntegerValue_e.swUnitsMassPropVolume,
        swUserPreferenceIntegerValue_e.swUnitsMassPropDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsForce,
        swUserPreferenceIntegerValue_e.swUnitsForceDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsTimeUnits,
        swUserPreferenceIntegerValue_e.swUnitsTimeDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsEnergyUnits,
        swUserPreferenceIntegerValue_e.swUnitsEnergyDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsPowerUnits,
        swUserPreferenceIntegerValue_e.swUnitsPowerDecimalPlaces, swUserPreferenceIntegerValue_e.swUnitsDecimalRounding,
    };
    static readonly string[] UnitSystems = { "?", "CGS", "MKS", "IPS", "Custom", "MMGS" };
    static readonly string[] Masses = { "?", "mg", "g", "kg", "lb" };

    static int Main(string[] args)
    {
        if (args.Length < 1) return Usage();
        if (args[0] == "--one")
        {
            // Internal: one file in this process. 2 = refused (no retry), 3 = SOLIDWORKS failed mid-run (retried once).
            if (args.Length != 3 || (args[2] != "vault" && args[2] != "work")) return Usage();
            string f;
            try { f = Path.GetFullPath(args[1]); } catch (Exception ex) { Console.Error.WriteLine("Bad arguments: " + ex.Message); return 2; }
            string why = Refusal(f, args[2] == "vault");
            if (why != null) { Console.Error.WriteLine("REFUSED (" + why + "): " + f); return 2; }
            return One(f);
        }

        bool dry = args.Contains("--dry"), vault = args.Contains("--vault");
        var unknown = args.Where(a => a.StartsWith("--") && a != "--dry" && a != "--vault").ToList();
        if (unknown.Count > 0) { Console.Error.WriteLine("Unknown option: " + unknown[0]); return 2; }
        var files = new List<string>();
        foreach (var a in args.Where(a => !a.StartsWith("--")))
        {
            string p = Path.GetFullPath(a);
            if (Directory.Exists(p))
            {
                string why0 = Refusal(p.TrimEnd('\\') + "\\", vault);
                if (why0 != null) { Console.Error.WriteLine("REFUSED (" + why0 + "): " + p); return 2; }
                files.AddRange(Directory.GetFiles(p, "*.*", SearchOption.AllDirectories)
                    .Where(x => Kind(x) != 0 && x.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) < 0 && !Path.GetFileName(x).StartsWith("~$")));
            }
            else if (File.Exists(p) && Kind(p) != 0) files.Add(p);
            else { Console.Error.WriteLine("Not found or not a SOLIDWORKS file: " + p); return 2; }
        }
        files = files.Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => Order(f)).ThenBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var f in files)
        {
            string why = Refusal(f, vault);
            if (why != null) { Console.Error.WriteLine("REFUSED (" + why + "): " + f); return 2; }
        }
        Console.WriteLine("SwKg: " + files.Count + " file(s), parts then assemblies then drawings" + (vault ? " (vault allowed)" : ""));
        if (dry) { for (int i = 0; i < files.Count; i++) Console.WriteLine(string.Format("  {0,3}. {1}", i + 1, files[i])); return 0; }

        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }
        string self = Process.GetCurrentProcess().MainModule.FileName;
        int problems = 0;
        foreach (var f in files)
        {
            int rc = -1;
            for (int attempt = 1; attempt <= 2; attempt++)
            {
                if (!WaitForNoSolidWorks()) { Console.WriteLine(f + ": SOLIDWORKS still running from the previous file  ** NOT SAVED **"); rc = 1; break; }
                rc = RunChild(self, new[] { "--one", Quote(f), vault ? "vault" : "work" });
                if (rc == 0 || rc == 1) break;
                if (rc == 2) { Console.WriteLine(f + ": refused or bad arguments (see above), not retried"); break; }
                Console.WriteLine(f + ": SOLIDWORKS failed mid-run (exit " + rc + ")" + (attempt == 1 ? ", retrying once" : ""));
            }
            if (rc != 0) problems++;
        }
        Console.WriteLine(problems == 0 ? "RESULT: all done" : "RESULT: " + problems + " problem(s)");
        return problems == 0 ? 0 : 1;
    }

    static int Usage()
    {
        Console.Error.WriteLine("Usage: SwKg [--dry] [--vault] <file-or-folder> [...]");
        return 2;
    }

    static int One(string path)
    {
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }
        if ((File.GetAttributes(path) & FileAttributes.ReadOnly) != 0) { Console.WriteLine(path + ": read-only on disk (not checked out?)  ** NOT SAVED **"); return 1; }
        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        var started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        try
        {
            int errs = 0, warns = 0;
            var doc = sw.OpenDoc6(path, Kind(path), (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref errs, ref warns);
            Console.Write(path + ": open errors=" + errs + " warnings=" + warns);
            if (doc == null || errs != 0) { Console.WriteLine("  ** NOT SAVED **"); return 1; }
            var x = doc.Extension;
            int sys0 = Get(x, swUserPreferenceIntegerValue_e.swUnitSystem), mass0 = Get(x, swUserPreferenceIntegerValue_e.swUnitsMassPropMass);
            var kept0 = Kept.Select(p => Get(x, p)).ToArray();
            Console.Write("  was " + Name(UnitSystems, sys0) + "/" + Name(Masses, mass0) + ", mass dp " + Get(x, swUserPreferenceIntegerValue_e.swUnitsMassPropDecimalPlaces));
            if (mass0 == (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms) { Console.WriteLine("  already kg, not saved"); return 0; }

            if (sys0 != (int)swUnitSystem_e.swUnitSystem_Custom)
                x.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitSystem, 0, (int)swUnitSystem_e.swUnitSystem_Custom);
            var changed = Kept.Where((p, i) => Get(x, p) != kept0[i]).ToList();
            if (Get(x, swUserPreferenceIntegerValue_e.swUnitSystem) != (int)swUnitSystem_e.swUnitSystem_Custom || changed.Count > 0)
            {
                Console.WriteLine("  ** switching to Custom changed " + (changed.Count == 0 ? "nothing else but did not take" : string.Join(", ", changed)) + "; NOT SAVED **");
                return 1;
            }
            x.SetUserPreferenceInteger((int)swUserPreferenceIntegerValue_e.swUnitsMassPropMass, 0, (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms);
            changed = Kept.Where((p, i) => Get(x, p) != kept0[i]).ToList();
            if (Get(x, swUserPreferenceIntegerValue_e.swUnitsMassPropMass) != (int)swUnitsMassPropMass_e.swUnitsMassPropMass_Kilograms || changed.Count > 0)
            {
                Console.WriteLine("  ** kg did not take" + (changed.Count == 0 ? "" : " or changed " + string.Join(", ", changed)) + "; NOT SAVED **");
                return 1;
            }

            int nconf = 1;
            if (doc.GetType() != (int)swDocumentTypes_e.swDocDRAWING)
            {
                bool asm = doc.GetType() == (int)swDocumentTypes_e.swDocASSEMBLY;
                string first = ((IConfiguration)doc.GetActiveConfiguration()).Name;
                var names = (string[])doc.GetConfigurationNames();
                foreach (string cn in names)
                {
                    doc.ShowConfiguration2(cn);
                    if (asm) ((IAssemblyDoc)doc).ResolveAllLightWeightComponents(false);
                    doc.ForceRebuild3(false);
                }
                doc.ShowConfiguration2(first);
                nconf = names.Length;
            }
            else doc.ForceRebuild3(false);

            int serr = 0, swarn = 0;
            bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref serr, ref swarn);
            Console.WriteLine("  -> Custom/kg, rebuilt " + nconf + " config(s), save " + (ok ? "ok" : "FAILED") + " (errors=" + serr + " warnings=" + swarn + ")");
            ReportWeights(doc);
            return ok ? 0 : 1;
        }
        catch (System.Runtime.InteropServices.COMException ex)
        {
            Console.WriteLine();
            Console.WriteLine(path + ": SOLIDWORKS error 0x" + ex.ErrorCode.ToString("X8") + "  ** NOT SAVED **");
            return 3;
        }
        finally
        {
            try { sw.CloseAllDocuments(true); } catch { }
            try { sw.ExitApp(); } catch { }
            try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw); } catch { }
            if (started != null && !started.WaitForExit(30000))
                try { started.Kill(); started.WaitForExit(10000); } catch { }
        }
    }

    // The evaluated Weight property, file-level and per configuration (Document Manager only sees the formula text).
    // A configuration is shown before it is read, so its value is the one SOLIDWORKS computes in kg.
    static void ReportWeights(IModelDoc2 doc)
    {
        Console.WriteLine("    WEIGHT\t@file\t" + Weight(doc.Extension.CustomPropertyManager[""]));
        if (doc.GetType() == (int)swDocumentTypes_e.swDocDRAWING) return;
        string first = ((IConfiguration)doc.GetActiveConfiguration()).Name;
        foreach (string cn in (string[])doc.GetConfigurationNames())
        {
            doc.ShowConfiguration2(cn);
            Console.WriteLine("    WEIGHT\t" + cn + "\t" + Weight(doc.Extension.CustomPropertyManager[cn]));
        }
        doc.ShowConfiguration2(first);
    }

    static string Weight(ICustomPropertyManager cpm)
    {
        string raw, val; bool wasResolved, linked;
        int r = cpm.Get6("Weight", false, out raw, out val, out wasResolved, out linked);
        if (r == (int)swCustomInfoGetResult_e.swCustomInfoGetResult_NotPresent) return "-";
        return raw + "\t" + val + (wasResolved ? "" : "\t(not resolved)");
    }

    static int Get(IModelDocExtension x, swUserPreferenceIntegerValue_e p) { return x.GetUserPreferenceInteger((int)p, 0); }
    static string Name(string[] names, int v) { return v >= 0 && v < names.Length ? names[v] : v.ToString(); }

    // Null if allowed, else the reason.
    static string Refusal(string p, bool vault)
    {
        if (p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || p.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase)) return "Dropbox";
        if (p.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0) return ".pdm";
        bool inVault = p.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || p.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (inVault && !vault) return "vault path needs --vault";
        return null;
    }

    static int Kind(string f)
    {
        switch (Path.GetExtension(f).ToLowerInvariant())
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
            default: return 0;
        }
    }

    static int Order(string f)
    {
        int k = Kind(f);
        return k == (int)swDocumentTypes_e.swDocPART ? 0 : k == (int)swDocumentTypes_e.swDocASSEMBLY ? 1 : 2;
    }

    static int RunChild(string self, IEnumerable<string> args)
    {
        var psi = new ProcessStartInfo(self, string.Join(" ", args)) { UseShellExecute = false };
        using (var p = Process.Start(psi))
        {
            p.WaitForExit();
            return p.ExitCode;
        }
    }

    static bool WaitForNoSolidWorks()
    {
        for (int i = 0; i < 60; i++)
        {
            if (Process.GetProcessesByName("SLDWORKS").Length == 0) return true;
            System.Threading.Thread.Sleep(1000);
        }
        return false;
    }

    // Quote one argument for the Windows command line (backslashes before a quote or at the end are doubled).
    static string Quote(string s)
    {
        var b = new System.Text.StringBuilder("\"");
        int slashes = 0;
        foreach (char ch in s)
        {
            if (ch == '\\') { slashes++; continue; }
            if (ch == '"') { b.Append('\\', slashes * 2 + 1); b.Append('"'); }
            else { b.Append('\\', slashes); b.Append(ch); }
            slashes = 0;
        }
        b.Append('\\', slashes * 2);
        return b.Append('"').ToString();
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
