// Importer: bring one screen's Pack and Go into the vault with the Document Manager, following the reviewed
// import plan (tools\importplan\export_actions.py writes the two CSVs).
// Usage: Importer.exe <files.csv> <partnos.csv> <sourceRoot> <vaultRoot> <ledger.csv> <screen> <report.md> [--apply [--existing]]
// Without --apply it only checks and writes the report (a dry run). With --apply it copies each file into the
// vault under its new name, repoints every reference, writes PartNo (and PartNoStatus = Provisional), adds the
// ledger rows, and re-reads every vault file. Files with action "reuse" are already in the vault (from an earlier
// screen, by the ledger): they aren't copied or changed, only the new files' references are pointed at them. --existing re-runs everything after the copy on files already in the
// vault (no copy, no ledger rows), e.g. to try a new step on a vault from an earlier run. It never deletes, never edits the source, and refuses Dropbox paths.
// The licence key is read from the SW_DM_KEY environment variable and is never printed.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolidWorks.Interop.swdocumentmgr;

class Item
{
    public string Source, OldName, NewName, NewFolder, Target;
    public bool Import, Reuse;                                // Reuse: already in the vault, references point at it
    public bool InVault { get { return Import || Reuse; } }
    public List<string> Refs = new List<string>();           // every reference path the file holds
    public HashSet<string> UnsuppressedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
}

class PartNoRow { public string File, Config, Level, PartNo, Status, From; }

static class Importer
{
    static ISwDMApplication app;
    const string WorkRoot = @"C:\PDM-work\";   // work copies and the inbox: no vault file may point here
    static readonly List<string> errors = new List<string>(), warnings = new List<string>(), done = new List<string>(), bom = new List<string>();

    static int Main(string[] args)
    {
        if (args.Length < 7)
        {
            Console.Error.WriteLine("Usage: Importer <files.csv> <partnos.csv> <sourceRoot> <vaultRoot> <ledger.csv> <screen> <report.md> [--apply]");
            return 2;
        }
        // Roots are kept without a trailing backslash and always joined with one: a bare "P:" joined by
        // Path.Combine gives the drive-relative "P:APEX\...", which then gets written into the files.
        string sourceRoot = Path.GetFullPath(args[2]).TrimEnd('\\'), vaultRoot = Path.GetFullPath(args[3]).TrimEnd('\\');
        string ledgerPath = args[4], screen = args[5], reportPath = args[6];
        bool apply = args.Contains("--apply"), existing = args.Contains("--existing");
        foreach (var p in new[] { sourceRoot, vaultRoot, ledgerPath, reportPath })
            if (p.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED (inside Dropbox): " + p); return 2; }
        // A vault the PDM tracks: its files change only through checkout / checkin, never by re-applying an import.
        if (existing && Directory.Exists(vaultRoot + "\\.pdm")) { Console.Error.WriteLine("REFUSED (--existing on a PDM-tracked vault): " + vaultRoot); return 2; }

        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        try { app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key); }
        catch (Exception ex) { Console.Error.WriteLine("Could not start Document Manager (check the key): " + ex.GetType().Name); return 3; }
        if (app == null) { Console.Error.WriteLine("Document Manager returned no application (key rejected?)."); return 3; }

        // ---------------------------------------------------------------- read the plan
        var items = new List<Item>();
        foreach (var r in ReadCsv(args[0]))
        {
            var it = new Item { Source = sourceRoot + "\\" + r["source"], OldName = Path.GetFileName(r["source"]), Import = r["action"] == "import", Reuse = r["action"] == "reuse" };
            if (it.InVault)
            {
                it.NewName = r["new_name"];
                it.NewFolder = r["new_folder"];
                it.Target = vaultRoot + "\\" + it.NewFolder + "\\" + it.NewName;
            }
            items.Add(it);
        }
        var partnos = ReadCsv(args[1]).Select(r => new PartNoRow { File = r["file"], Config = r["config"], Level = r["level"], PartNo = r["partno"], Status = r["status"], From = r["from"] }).ToList();
        var byOld = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        foreach (var it in items)
        {
            if (byOld.ContainsKey(it.OldName)) errors.Add("Two source files are both called " + it.OldName + ": SOLIDWORKS can't tell them apart.");
            else byOld[it.OldName] = it;
        }
        var imports = items.Where(i => i.Import).ToList();
        var reused = items.Where(i => i.Reuse).ToList();
        foreach (var it in reused)
            if (!File.Exists(it.Target)) errors.Add("Reused file not in the vault: " + it.Target + " (for " + it.OldName + ").");

        // ---------------------------------------------------------------- checks before touching anything
        foreach (var g in imports.GroupBy(i => i.NewName, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1))
            errors.Add("Same new name " + g.Key + " for " + string.Join(", ", g.Select(i => i.OldName)) + ".");
        foreach (var it in imports)
        {
            if (!File.Exists(it.Source)) errors.Add("Source missing: " + it.Source);
            if (existing) { if (!File.Exists(it.Target)) errors.Add("Not in the vault (--existing): " + it.Target); }
            else if (File.Exists(it.Target)) errors.Add("Already in the vault: " + it.Target);
            else if (Directory.Exists(vaultRoot + "\\"))
            {
                var same = Directory.GetFiles(vaultRoot + "\\", it.NewName, SearchOption.AllDirectories)
                    .Where(f => !f.StartsWith(vaultRoot + "\\.pdm\\", StringComparison.OrdinalIgnoreCase)).ToArray();   // the PDM app's state
                if (same.Length > 0) errors.Add("A file named " + it.NewName + " is already in the vault at " + same[0] + ".");
            }
        }

        // Name agrees with PartNo: a single number is the file name; a family's numbers all start with it.
        foreach (var g in partnos.GroupBy(p => p.File, StringComparer.OrdinalIgnoreCase))
        {
            Item it;
            if (!byOld.TryGetValue(g.Key, out it) || !it.Import) continue;
            string stem = Path.GetFileNameWithoutExtension(it.NewName);
            if (stem.StartsWith("FAST-", StringComparison.OrdinalIgnoreCase)) continue;   // fasteners: sizes are configurations
            // A category-only family name (GNRIC-LM) may hold several types (GNRIC-LMP-.., GNRIC-LMW-..): decision 5.
            var categoryOnly = new System.Text.RegularExpressions.Regex("^" + System.Text.RegularExpressions.Regex.Escape(stem) + "[AWP]-", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (var p in g)
                if (!p.PartNo.Equals(stem, StringComparison.OrdinalIgnoreCase) && !p.PartNo.StartsWith(stem + "-", StringComparison.OrdinalIgnoreCase)
                    && !(System.Text.RegularExpressions.Regex.IsMatch(stem, @"^\w{5}-[A-Z]{2}$") && categoryOnly.IsMatch(p.PartNo)))
                {
                    // A mismatch decided for that file (e.g. 0806 B decision 9: PLATE ONLY = 08BXX-PLP-01 in 08BXX-MSW-01) is a note.
                    if ((p.From ?? "").StartsWith("decision", StringComparison.OrdinalIgnoreCase))
                        warnings.Add("Name and PartNo differ, as decided (" + p.From + "): " + it.NewName + " / " + p.Config + " = " + p.PartNo + ".");
                    else
                        errors.Add("Name and PartNo disagree: " + it.NewName + " / " + p.Config + " = " + p.PartNo + ".");
                }
        }
        // A drawing carries its model's number and has no PartNo of its own (SPEC; the PDM app ignores one).
        foreach (var p in partnos.Where(p => TypeOf(p.File) == SwDmDocumentType.swDmDocumentDrawing))
            errors.Add("PartNo on a drawing: " + p.File + " / " + p.Config + " = " + p.PartNo + ". A drawing carries its model's number.");
        // No number used twice.
        foreach (var g in partnos.Where(p => byOld.ContainsKey(p.File) && byOld[p.File].Import)
                     .GroupBy(p => p.PartNo, StringComparer.OrdinalIgnoreCase))
        {
            var places = g.Select(p => p.File.ToLowerInvariant() + "|" + (p.Level == "file" ? "@file" : p.Config)).Distinct().ToList();
            if (places.Count > 1)
                errors.Add("PartNo " + g.Key + " is on more than one file or configuration: " + string.Join(", ", g.Select(p => p.File + " / " + p.Config)) + ".");
        }

        // Every reference has somewhere to go.
        var readable = imports.Where(i => File.Exists(i.Source) && ReadRefs(i, sourceRoot)).ToList();
        foreach (var it in readable)
        {
            bool drawing = TypeOf(it.Source) == SwDmDocumentType.swDmDocumentDrawing;
            foreach (var r in it.Refs.Select(Path.GetFileName).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                Item target;
                if (byOld.TryGetValue(r, out target) && target.InVault) continue;
                // A drawing lists everything inside its model too; a left-out file it reaches through a model
                // being imported is that model's reference, checked there.
                var via = drawing && target != null ? readable.FirstOrDefault(o => o != it && TypeOf(o.Source) != SwDmDocumentType.swDmDocumentDrawing
                    && o.Refs.Any(x => Path.GetFileName(x).Equals(r, StringComparison.OrdinalIgnoreCase))) : null;
                if (via != null)
                    warnings.Add(it.OldName + " (drawing) lists " + r + " (left out) through its model " + via.OldName + ".");
                else if (target != null && !it.UnsuppressedNames.Contains(r))   // its Pack and Go path would stay and fail the check after import
                    errors.Add(it.OldName + " still refers to " + r + " (left out), though only suppressed components use it: the path into the Pack and Go would stay in the vault. Delete the component or import the file.");
                else if (target != null)
                    errors.Add(it.OldName + " uses " + r + ", which the plan leaves out.");
                else
                    errors.Add(it.OldName + " refers to " + r + ", which isn't in the Pack and Go.");
            }
        }

        if (errors.Count > 0 || !apply)
        {
            Report(reportPath, screen, apply, imports, items, partnos, vaultRoot);
            Console.WriteLine((errors.Count > 0 ? "STOPPED: " + errors.Count + " problem(s), nothing changed." : "Dry run passed: nothing changed.") + " Report: " + reportPath);
            return errors.Count > 0 ? 1 : 0;
        }

        // ---------------------------------------------------------------- apply
        if (!existing)
        {
            foreach (var it in imports)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(it.Target));
                File.Copy(it.Source, it.Target, false);
                File.SetAttributes(it.Target, File.GetAttributes(it.Target) & ~FileAttributes.ReadOnly);
            }
            done.Add("Copied " + imports.Count + " files into " + vaultRoot + "\\.");
        }

        // Old file name -> new, for formulas that name a file (Weight = "SW-Mass@OLD.SLDPRT").
        var renames = imports.Concat(reused).Where(i => !i.OldName.Equals(i.NewName, StringComparison.OrdinalIgnoreCase))
            .ToDictionary(i => i.OldName, i => i.NewName, StringComparer.OrdinalIgnoreCase);
        int repointed = 0, written = 0, formulas = 0;
        foreach (var it in imports)
        {
            SwDmDocumentOpenError err;
            var doc = app.GetDocument(it.Target, TypeOf(it.Target), false, out err) as ISwDMDocument31;
            if (doc == null) { errors.Add("Could not open the vault copy " + it.Target + ": " + err); continue; }
            try
            {
                // ReplaceReference wants the path exactly as the file stores it. Read from the source folder, the
                // Document Manager reports stored paths re-rooted to wherever the file now sits (a tidy copy's
                // E:\PDM-work\LS-08A-06\... shows as ...\LS-08A-06-r4\...), so the vault copy is read again here.
                foreach (var r in StoredRefs(doc, it.Target)) it.Refs.Add(r);
                foreach (var old in it.Refs.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    Item target;
                    if (!byOld.TryGetValue(Path.GetFileName(old), out target) || !target.InVault) continue;
                    if (old.Equals(target.Target, StringComparison.OrdinalIgnoreCase)) continue;
                    doc.ReplaceReference(old, target.Target);
                    repointed++;
                }
                if (doc.ToolboxPart != SwDmToolboxPartType.swDmNotAToolboxPart)
                {
                    doc.ToolboxPart = SwDmToolboxPartType.swDmNotAToolboxPart;
                    done.Add("Cleared the Toolbox flag on " + it.NewName + ".");
                }
                foreach (var p in partnos.Where(p => p.File.Equals(it.OldName, StringComparison.OrdinalIgnoreCase)))
                {
                    if (p.Level == "file" && partnos.Any(q => q != p && q.Level == "file" && q.File == p.File && partnos.IndexOf(q) < partnos.IndexOf(p))) continue;
                    if (SetProp(doc, p.Level == "file" ? null : p.Config, "PartNo", p.PartNo, it.NewName)) written++;
                    if (p.Status == "Provisional") SetProp(doc, p.Level == "file" ? null : p.Config, "PartNoStatus", "Provisional", it.NewName);
                }
                formulas += RewriteFormulas(doc, it.Target, renames);
                var saveErr = doc.Save();
                if (saveErr != SwDmDocumentSaveError.swDmDocumentSaveErrorNone) errors.Add("Save failed on " + it.Target + ": " + saveErr);
            }
            finally { try { doc.CloseDoc(); } catch { } }
        }
        done.Add("Repointed " + repointed + " stored references; wrote " + written + " PartNo values that differed from the file; rewrote " + formulas + " property formulas that named an old file.");

        if (!existing)
        {
            WriteLedger(ledgerPath, screen, imports, items, partnos, sourceRoot, vaultRoot);
            done.Add("Added " + (imports.Count + partnos.Count(p => byOld[p.File].Import)) + " rows to " + ledgerPath + ".");
        }

        // ---------------------------------------------------------------- check after
        int checkedRefs = 0;
        var leftOut = new HashSet<string>(items.Where(i => !i.InVault).Select(i => i.OldName), StringComparer.OrdinalIgnoreCase);
        foreach (var it in imports)
        {
            SwDmDocumentOpenError err;
            var doc = app.GetDocument(it.Target, TypeOf(it.Target), true, out err) as ISwDMDocument31;
            if (doc == null) { errors.Add("Could not re-open " + it.Target + ": " + err); continue; }
            try
            {
                foreach (var r in Resolved(doc, null))
                {
                    checkedRefs++;
                    bool inside = r.StartsWith(vaultRoot + "\\", StringComparison.OrdinalIgnoreCase) && File.Exists(r);
                    if (inside) continue;
                    // A path into the Pack and Go or a work copy is never acceptable, even to a left-out file.
                    bool work = r.StartsWith(sourceRoot + "\\", StringComparison.OrdinalIgnoreCase) || r.StartsWith(WorkRoot, StringComparison.OrdinalIgnoreCase);
                    if (!work && leftOut.Contains(Path.GetFileName(r))) continue;   // reported above
                    errors.Add("After import, " + it.NewName + " still points outside the vault: " + r);
                }
                foreach (var p in partnos.Where(p => p.File.Equals(it.OldName, StringComparison.OrdinalIgnoreCase)))
                {
                    string v = GetProp(doc, p.Level == "file" ? null : p.Config, "PartNo");
                    if (v != p.PartNo) errors.Add("PartNo didn't read back on " + it.NewName + " / " + p.Config + ": " + v);
                }
                foreach (var f in OldFormulas(doc, it.Target, renames)) errors.Add("After import, " + it.NewName + " still has a formula naming an old file: " + f);
                BomSettings(doc, it);
            }
            finally { try { doc.CloseDoc(); } catch { } }
        }
        done.Add("Re-read every vault file: " + checkedRefs + " references checked.");

        Report(reportPath, screen, apply, imports, items, partnos, vaultRoot);
        Console.WriteLine((errors.Count > 0 ? "DONE WITH " + errors.Count + " PROBLEM(S)." : "Import done, all checks passed.") + " Report: " + reportPath);
        return errors.Count > 0 ? 1 : 0;
    }

    // Every reference the source file holds, in every stored form, plus which files unsuppressed components use.
    static bool ReadRefs(Item it, string sourceRoot)
    {
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(it.Source, TypeOf(it.Source), true, out err) as ISwDMDocument31;
        if (doc == null) { errors.Add("Could not open " + it.Source + ": " + err); return false; }
        try
        {
            if (TypeOf(it.Source) == SwDmDocumentType.swDmDocumentAssembly)
            {
                var cm = doc.ConfigurationManager;
                foreach (var cn in (string[])cm.GetConfigurationNames())
                {
                    var arr = ((ISwDMConfiguration18)cm.GetConfigurationByName(cn)).GetComponents() as object[];
                    if (arr == null) continue;
                    foreach (var o in arr)
                    {
                        var c = o as ISwDMComponent12;
                        if (c == null) continue;
                        string p = Safe(() => c.PathName);
                        if (p == "") continue;
                        it.Refs.Add(p);
                        bool suppressed = false;
                        try { suppressed = c.IsSuppressed(); } catch { }
                        if (!suppressed) it.UnsuppressedNames.Add(Path.GetFileName(p));
                    }
                }
            }
            foreach (var r in Resolved(doc, null)) it.Refs.Add(r);
            foreach (var r in Resolved(doc, sourceRoot)) it.Refs.Add(r);
            // Part links (skeletons, inserted parts) always load.
            if (TypeOf(it.Source) != SwDmDocumentType.swDmDocumentAssembly)
                foreach (var r in it.Refs) it.UnsuppressedNames.Add(Path.GetFileName(r));
            return true;
        }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    static List<string> StoredRefs(ISwDMDocument31 doc, string path)
    {
        var result = new List<string>();
        if (TypeOf(path) == SwDmDocumentType.swDmDocumentAssembly)
        {
            var cm = doc.ConfigurationManager;
            foreach (var cn in (string[])cm.GetConfigurationNames())
            {
                var arr = ((ISwDMConfiguration18)cm.GetConfigurationByName(cn)).GetComponents() as object[];
                if (arr == null) continue;
                foreach (var o in arr)
                {
                    var c = o as ISwDMComponent12;
                    string p = c == null ? "" : Safe(() => c.PathName);
                    if (p != "") result.Add(p);
                }
            }
        }
        result.AddRange(Resolved(doc, null));
        return result;
    }

    static IEnumerable<string> Resolved(ISwDMDocument31 doc, string searchFolder)
    {
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly
            | SwDmSearchFilters.SwDmSearchForDrawing | SwDmSearchFilters.SwDmSearchExternalReference
            | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference
            | (searchFolder != null ? SwDmSearchFilters.SwDmSearchSubfolders : 0));
        if (searchFolder != null) search.AddSearchPath(searchFolder);
        object broken, isVirtual, stamps, imported;
        var refs = doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[];
        var virt = isVirtual as bool[];
        var result = new List<string>();
        if (refs != null)
            for (int i = 0; i < refs.Length; i++)
                if (!(virt != null && i < virt.Length && virt[i]) && !string.IsNullOrEmpty(refs[i])) result.Add(refs[i]);
        var imp = imported as string[];
        if (imp != null) result.AddRange(imp.Where(p => !string.IsNullOrEmpty(p)));
        return result;
    }

    // Every property, at file level (config null) and per configuration, with its expression.
    static List<Tuple<string, string, string>> Expressions(ISwDMDocument31 doc, string path)
    {
        var result = new List<Tuple<string, string, string>>();
        SwDmCustomInfoType t;
        var names = doc.GetCustomPropertyNames() as string[];
        if (names != null)
            foreach (var n in names) result.Add(Tuple.Create((string)null, n, Safe(() => doc.GetCustomProperty(n, out t))));
        foreach (var cn in ConfigNames(doc, path))
        {
            var cfg = Config(doc, cn);
            var cnames = cfg == null ? null : cfg.GetCustomPropertyNames() as string[];
            if (cnames == null) continue;
            foreach (var n in cnames) result.Add(Tuple.Create(cn, n, Safe(() => cfg.GetCustomProperty(n, out t))));
        }
        return result;
    }

    static string Renamed(string expr, Dictionary<string, string> renames)
    {
        foreach (var r in renames)
        {
            int i, from = 0;
            while ((i = expr.IndexOf("@" + r.Key, from, StringComparison.OrdinalIgnoreCase)) >= 0)
            {
                expr = expr.Substring(0, i + 1) + r.Value + expr.Substring(i + 1 + r.Key.Length);
                from = i + 1 + r.Value.Length;
            }
        }
        return expr;
    }

    // Formulas such as Weight = "SW-Mass@OLD.SLDPRT" keep the old file name after a rename. SOLIDWORKS fixes
    // its own name on the next save, but not until then, and not names of other files.
    static int RewriteFormulas(ISwDMDocument31 doc, string path, Dictionary<string, string> renames)
    {
        int n = 0;
        foreach (var e in Expressions(doc, path))
        {
            string fixedExpr = Renamed(e.Item3, renames);
            if (fixedExpr == e.Item3) continue;
            if (e.Item1 == null) doc.SetCustomProperty(e.Item2, fixedExpr);
            else Config(doc, e.Item1).SetCustomProperty(e.Item2, fixedExpr);
            n++;
        }
        return n;
    }

    static IEnumerable<string> OldFormulas(ISwDMDocument31 doc, string path, Dictionary<string, string> renames)
    {
        return Expressions(doc, path).Where(e => Renamed(e.Item3, renames) != e.Item3)
            .Select(e => (e.Item1 ?? "file") + " / " + e.Item2 + " = " + e.Item3);
    }

    // MDL- assemblies should promote their contents in the BOM; NONPART- parts should be excluded from the BOM
    // wherever they are placed. Reported only (decision 12).
    static void BomSettings(ISwDMDocument31 doc, Item it)
    {
        if (TypeOf(it.Target) != SwDmDocumentType.swDmDocumentAssembly) return;
        bool mdl = it.NewName.StartsWith("MDL-", StringComparison.OrdinalIgnoreCase);
        foreach (var cn in (string[])doc.ConfigurationManager.GetConfigurationNames())
        {
            var cfg = Config(doc, cn);
            if (cfg == null) continue;
            if (mdl)
            {
                var v = (swDmShowChildComponentsInBOMResult)cfg.ShowChildComponentsInBOM2;
                bool ok = v == swDmShowChildComponentsInBOMResult.swDmShowChildComponentsInBOM_Promote;
                bom.Add("| " + it.NewName + " | configuration " + cn + " | child components: " + v.ToString().Replace("swDmShowChildComponentsInBOM_", "") + " | Promote " + (ok ? "(ok)" : "**(differs)**") + " |");
            }
            var arr = cfg.GetComponents() as object[];
            if (arr == null) continue;
            foreach (var o in arr)
            {
                var c = o as ISwDMComponent12;
                string p = c == null ? "" : Safe(() => c.PathName);
                if (!Path.GetFileName(p).StartsWith("NONPART-", StringComparison.OrdinalIgnoreCase)) continue;
                var x = (swDmExcludeFromBOMResult)c.ExcludeFromBOM;
                bool ok = x == swDmExcludeFromBOMResult.swDmExcludeFromBOM_TRUE;
                bom.Add("| " + Path.GetFileName(p) + " | in " + it.NewName + " / " + cn + " | Exclude from BOM: " + x.ToString().Replace("swDmExcludeFromBOM_", "") + " | TRUE " + (ok ? "(ok)" : "**(differs)**") + " |");
            }
        }
    }

    // Writes only when the value differs. Returns true if it wrote.
    static bool SetProp(ISwDMDocument31 doc, string config, string name, string value, string where)
    {
        string current = GetProp(doc, config, name);
        if (current == value) return false;
        if (config == null)
        {
            if (current != null) doc.SetCustomProperty(name, value);
            else doc.AddCustomProperty(name, SwDmCustomInfoType.swDmCustomInfoText, value);
        }
        else
        {
            var cfg = Config(doc, config);
            if (cfg == null) { errors.Add("No configuration " + config + " in " + where + "."); return false; }
            if (current != null) cfg.SetCustomProperty(name, value);
            else cfg.AddCustomProperty(name, SwDmCustomInfoType.swDmCustomInfoText, value);
        }
        return true;
    }

    // null when not set.
    static string GetProp(ISwDMDocument31 doc, string config, string name)
    {
        SwDmCustomInfoType t;
        if (config == null)
            return Has(doc.GetCustomPropertyNames(), name) ? doc.GetCustomProperty2(name, out t) : null;
        var cfg = Config(doc, config);
        if (cfg == null || !Has(cfg.GetCustomPropertyNames(), name)) return null;
        return cfg.GetCustomProperty2(name, out t);
    }

    // Drawings have no configurations: the Document Manager fails (E_FAIL) rather than returning none.
    static string[] ConfigNames(ISwDMDocument31 doc, string path)
    {
        return TypeOf(path) == SwDmDocumentType.swDmDocumentDrawing ? new string[0] : (string[])doc.ConfigurationManager.GetConfigurationNames();
    }

    static ISwDMConfiguration18 Config(ISwDMDocument31 doc, string name)
    {
        try { return doc.ConfigurationManager.GetConfigurationByName(name) as ISwDMConfiguration18; } catch { return null; }
    }

    static bool Has(object names, string name)
    {
        var a = names as string[];
        return a != null && a.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
    }

    static void WriteLedger(string path, string screen, List<Item> imports, List<Item> items, List<PartNoRow> partnos, string sourceRoot, string vaultRoot)
    {
        var sb = new StringBuilder();
        string date = DateTime.Now.ToString("yyyy-MM-dd");
        var byOld = items.ToDictionary(i => i.OldName, StringComparer.OrdinalIgnoreCase);
        foreach (var it in imports)
            sb.AppendLine(Csv(date, screen, it.OldName, "", Path.GetDirectoryName(it.Source).Substring(sourceRoot.Length).TrimStart('\\'), it.NewName, "", it.NewFolder, "", "import plan", "importer"));
        foreach (var p in partnos.Where(p => byOld[p.File].Import))
        {
            var it = byOld[p.File];
            sb.AppendLine(Csv(date, screen, it.OldName, p.Level == "file" ? "" : p.Config, Path.GetDirectoryName(it.Source).Substring(sourceRoot.Length).TrimStart('\\'),
                it.NewName, p.Level == "file" ? "" : p.Config, it.NewFolder, p.PartNo, p.From, p.Status == "Provisional" ? "Provisional" : "confirmed"));
        }
        File.AppendAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    static void Report(string path, string screen, bool apply, List<Item> imports, List<Item> items, List<PartNoRow> partnos, string vaultRoot)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Import report: " + screen);
        sb.AppendLine();
        sb.AppendLine(DateTime.Now.ToString("d MMM yyyy HH:mm") + ", " + (apply ? "real run" : "dry run (nothing changed)") + ", vault `" + vaultRoot + "\\`");
        sb.AppendLine();
        sb.AppendLine("| | Count |");
        sb.AppendLine("| --- | --- |");
        sb.AppendLine("| Files to import | " + imports.Count + " |");
        sb.AppendLine("| Files reused from the vault | " + items.Count(i => i.Reuse) + " |");
        sb.AppendLine("| Files left out | " + items.Count(i => !i.InVault) + " |");
        sb.AppendLine("| PartNo values | " + partnos.Count + " |");
        sb.AppendLine("| Of which Provisional | " + partnos.Count(p => p.Status == "Provisional") + " |");
        sb.AppendLine("| Problems | " + errors.Count + " |");
        sb.AppendLine("| Notes | " + warnings.Count + " |");
        sb.AppendLine();
        if (errors.Count > 0) { sb.AppendLine("## Problems" + (apply ? "" : " (the import won't run until these are fixed)")); sb.AppendLine(); foreach (var e in errors) sb.AppendLine("- " + e); sb.AppendLine(); }
        if (done.Count > 0) { sb.AppendLine("## Done"); sb.AppendLine(); foreach (var d in done) sb.AppendLine("- " + d); sb.AppendLine(); }
        if (bom.Count > 0)
        {
            sb.AppendLine("## BOM settings on MDL- and NONPART- files (reported, not changed)");
            sb.AppendLine();
            sb.AppendLine("| File | Where | Setting | Expected |");
            sb.AppendLine("| --- | --- | --- | --- |");
            foreach (var b in bom.Distinct()) sb.AppendLine(b);
            sb.AppendLine();
        }
        if (warnings.Count > 0) { sb.AppendLine("## Notes"); sb.AppendLine(); foreach (var w in warnings.Distinct()) sb.AppendLine("- " + w); sb.AppendLine(); }
        sb.AppendLine("## Files");
        sb.AppendLine();
        sb.AppendLine("| Old name | New name | Folder |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var it in items.OrderBy(i => i.Reuse ? 1 : i.Import ? 0 : 2).ThenBy(i => i.NewFolder ?? "").ThenBy(i => i.NewName ?? i.OldName))
            sb.AppendLine("| " + it.OldName + " | " + (it.InVault ? it.NewName + (it.Reuse ? " *(reused)*" : "") : "*left out*") + " | " + (it.NewFolder ?? "") + " |");
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(false));
    }

    static List<Dictionary<string, string>> ReadCsv(string path)
    {
        var lines = ParseCsv(File.ReadAllText(path, Encoding.UTF8).TrimStart('﻿'));
        var header = lines[0];
        return lines.Skip(1).Where(l => l.Count > 1).Select(l => header.Select((h, i) => new { h, v = i < l.Count ? l[i] : "" }).ToDictionary(x => x.h, x => x.v)).ToList();
    }

    static List<List<string>> ParseCsv(string text)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];
            if (quoted)
            {
                if (ch == '"' && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                else if (ch == '"') quoted = false;
                else field.Append(ch);
            }
            else if (ch == '"') quoted = true;
            else if (ch == ',') { row.Add(field.ToString()); field.Clear(); }
            else if (ch == '\n') { row.Add(field.ToString()); field.Clear(); rows.Add(row); row = new List<string>(); }
            else if (ch != '\r') field.Append(ch);
        }
        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }

    static string Csv(params string[] values)
    {
        return string.Join(",", values.Select(s => (s ?? "").IndexOfAny(new[] { ',', '"', '\n' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : (s ?? "")));
    }

    static SwDmDocumentType TypeOf(string path)
    {
        switch (Path.GetExtension(path).ToLowerInvariant())
        {
            case ".sldprt": return SwDmDocumentType.swDmDocumentPart;
            case ".sldasm": return SwDmDocumentType.swDmDocumentAssembly;
            case ".slddrw": return SwDmDocumentType.swDmDocumentDrawing;
            default: return SwDmDocumentType.swDmDocumentUnknown;
        }
    }

    static string Safe(Func<string> f)
    {
        try { var s = f(); return s ?? ""; } catch { return ""; }
    }
}
