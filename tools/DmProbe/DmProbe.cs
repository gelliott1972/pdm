// DmProbe: read-only inventory of a folder of SOLIDWORKS files using the Document Manager.
// Phase 0, Test 2. Opens every file read-only, never saves.
// Usage: DmProbe.exe <inputFolder> <outputFolder>
// The licence key is read from the SW_DM_KEY environment variable and is never printed.
// Written in C# 5 so it compiles with the .NET Framework compiler built into Windows.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using SolidWorks.Interop.swdocumentmgr;

static class DmProbe
{
    static int Main(string[] args)
    {
        if (args.Length != 2)
        {
            Console.Error.WriteLine("Usage: DmProbe <inputFolder> <outputFolder>");
            return 2;
        }
        string root = Path.GetFullPath(args[0]).TrimEnd('\\');
        string outDir = Path.GetFullPath(args[1]);
        Directory.CreateDirectory(outDir);

        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key))
        {
            Console.Error.WriteLine("SW_DM_KEY is not set.");
            return 2;
        }

        ISwDMApplication app;
        try
        {
            var factory = (ISwDMClassFactory)new SwDMClassFactory();
            app = factory.GetApplication(key);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("Could not start Document Manager (check the key): " + ex.GetType().Name);
            return 3;
        }
        if (app == null)
        {
            Console.Error.WriteLine("Document Manager returned no application (key rejected?).");
            return 3;
        }

        // Every SOLIDWORKS file in the folder, indexed by bare file name for resolving references.
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".sldprt", ".sldasm", ".slddrw" };
        var allFiles = Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .Where(f => !Path.GetFileName(f).StartsWith("~$")).ToList();
        var files = allFiles.Where(f => exts.Contains(Path.GetExtension(f)))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase).ToList();
        // Every file counts for resolving references: linked equation files and imported STEP files too.
        var byName = allFiles.GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly
            | SwDmSearchFilters.SwDmSearchForDrawing | SwDmSearchFilters.SwDmSearchSubfolders
            | SwDmSearchFilters.SwDmSearchRootAssemblyFolder | SwDmSearchFilters.SwDmSearchExternalReference
            | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);
        // Without the last two flags, references from parts (skeletons, derived parts, imported STEP files) are missed.
        search.AddSearchPath(root);

        var fileRows = new Csv("file", "folder", "type", "opened", "toolbox", "sw_version", "config_count", "active_config", "file_PartNo", "config_PartNos", "file_prop_names");
        var cfgRows = new Csv("file", "config", "parent_config", "PartNo", "prop_count");
        var propRows = new Csv("file", "config", "name", "type", "expression", "value");
        var compRows = new Csv("assembly", "assembly_config", "component_file", "component_config", "stored_path", "stored_exists", "found_in_inbox", "suppressed", "virtual", "exclude_from_bom");
        var refRows = new Csv("file", "kind", "reference", "broken", "virtual", "found_in_inbox");

        int ok = 0, failed = 0;
        foreach (var path in files)
        {
            string rel = path.Substring(root.Length).TrimStart('\\');
            string folder = Path.GetDirectoryName(rel);
            var type = TypeOf(path);
            SwDmDocumentOpenError err;
            SwDMDocument doc = null;
            try { doc = app.GetDocument(path, type, true, out err); }
            catch (Exception ex) { err = SwDmDocumentOpenError.swDmDocumentOpenErrorFail; Console.Error.WriteLine("  open threw on " + rel + ": " + ex.Message); }

            if (doc == null)
            {
                failed++;
                fileRows.Add(Path.GetFileName(path), folder, Short(type), "no: " + err, "", "", "", "", "", "", "");
                continue;
            }
            ok++;
            var d = (ISwDMDocument31)doc;
            try
            {
                string toolbox = "";
                try { toolbox = d.ToolboxPart.ToString().Replace("swDm", ""); } catch { toolbox = "?"; }
                string version = "";
                try { version = d.GetVersion().ToString(); } catch { }

                // File-level custom properties.
                var fileProps = Names(d.GetCustomPropertyNames());
                string filePartNo = "";
                foreach (var n in fileProps)
                {
                    SwDmCustomInfoType t;
                    string expr = Safe(() => d.GetCustomProperty(n, out t));
                    SwDmCustomInfoType t2;
                    string val = Safe(() => d.GetCustomProperty2(n, out t2));
                    propRows.Add(Path.GetFileName(path), "(file)", n, "", expr, val);
                    if (n.Equals("PartNo", StringComparison.OrdinalIgnoreCase)) filePartNo = val != "" ? val : expr;
                }

                // Configurations and their properties.
                var cm = d.ConfigurationManager;
                List<string> cfgNames;
                try { cfgNames = Names(cm.GetConfigurationNames()); }
                catch (Exception ex) { cfgNames = new List<string>(); Console.Error.WriteLine("  configurations unreadable on " + rel + ": " + ex.Message); }
                string active = Safe(() => cm.GetActiveConfigurationName());
                var cfgPartNos = new List<string>();
                foreach (var cn in cfgNames)
                {
                    var cfg = (ISwDMConfiguration18)cm.GetConfigurationByName(cn);
                    if (cfg == null) continue;
                    string parent = Safe(() => cfg.GetParentConfigurationName());
                    var cprops = Names(cfg.GetCustomPropertyNames());
                    string partNo = "";
                    foreach (var n in cprops)
                    {
                        SwDmCustomInfoType t;
                        string expr = Safe(() => cfg.GetCustomProperty(n, out t));
                        SwDmCustomInfoType t2;
                        string val = Safe(() => cfg.GetCustomProperty2(n, out t2));
                        propRows.Add(Path.GetFileName(path), cn, n, "", expr, val);
                        if (n.Equals("PartNo", StringComparison.OrdinalIgnoreCase)) partNo = val != "" ? val : expr;
                    }
                    if (partNo != "") cfgPartNos.Add(cn + "=" + partNo);
                    cfgRows.Add(Path.GetFileName(path), cn, parent, partNo, cprops.Count.ToString());

                    // Components per configuration (assemblies only).
                    if (type == SwDmDocumentType.swDmDocumentAssembly)
                    {
                        object comps = null;
                        try { comps = cfg.GetComponents(); } catch { }
                        var arr = comps as object[];
                        if (arr == null) continue;
                        foreach (var o in arr)
                        {
                            var c = o as ISwDMComponent12;
                            if (c == null) continue;
                            string stored = Safe(() => c.PathName);
                            bool exists = stored != "" && File.Exists(stored);
                            compRows.Add(Path.GetFileName(path), cn, Path.GetFileName(stored), Safe(() => c.ConfigurationName),
                                stored, exists ? "yes" : "no", FoundIn(byName, root, stored),
                                Safe(() => c.IsSuppressed() ? "yes" : "no"), Safe(() => c.IsVirtual ? "yes" : "no"),
                                Safe(() => c.ExcludeFromBOM.ToString()));
                        }
                    }
                }

                // Every external reference: assembly components, models in drawings, and files a part is built
                // from (skeletons, derived parts, imported STEP files).
                object broken = null, isVirtual = null, stamps = null, imported = null;
                object refsObj = null;
                try { refsObj = d.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported); } catch (Exception ex) { Console.Error.WriteLine("  refs failed on " + rel + ": " + ex.Message); }
                var refs = refsObj as string[];
                var brokenArr = broken as int[];
                var virtArr = isVirtual as bool[];
                if (refs != null)
                {
                    for (int i = 0; i < refs.Length; i++)
                    {
                        string b = brokenArr != null && i < brokenArr.Length ? brokenArr[i].ToString() : "";
                        string v = virtArr != null && i < virtArr.Length ? (virtArr[i] ? "yes" : "no") : "";
                        refRows.Add(Path.GetFileName(path), RefKind(type, refs[i]), refs[i], b, v, FoundIn(byName, root, refs[i]));
                    }
                }
                var importedArr = imported as string[];
                if (importedArr != null)
                    foreach (var ip in importedArr.Where(p => !string.IsNullOrEmpty(p)))
                        refRows.Add(Path.GetFileName(path), "imported", ip, "", "", FoundIn(byName, root, ip));

                fileRows.Add(Path.GetFileName(path), folder, Short(type), "yes", toolbox, version, cfgNames.Count.ToString(), active,
                    filePartNo, string.Join("; ", cfgPartNos), string.Join("; ", fileProps));
            }
            finally
            {
                try { d.CloseDoc(); } catch { }
            }
        }

        fileRows.Save(Path.Combine(outDir, "files.csv"));
        cfgRows.Save(Path.Combine(outDir, "configs.csv"));
        propRows.Save(Path.Combine(outDir, "properties.csv"));
        compRows.Save(Path.Combine(outDir, "components.csv"));
        refRows.Save(Path.Combine(outDir, "references.csv"));

        Console.WriteLine("Files found:   " + files.Count);
        Console.WriteLine("Opened:        " + ok);
        Console.WriteLine("Failed:        " + failed);
        Console.WriteLine("Configs:       " + cfgRows.Count);
        Console.WriteLine("Properties:    " + propRows.Count);
        Console.WriteLine("Components:    " + compRows.Count);
        Console.WriteLine("References:    " + refRows.Count);
        Console.WriteLine("Output:        " + outDir);
        return failed == 0 ? 0 : 1;
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

    // component = part of an assembly; model = shown in a drawing; part link = a part built from another
    // SOLIDWORKS file (skeleton, derived part); linked file = anything else (STEP import, equations file).
    static string RefKind(SwDmDocumentType owner, string reference)
    {
        var e = Path.GetExtension(reference).ToLowerInvariant();
        if (e != ".sldprt" && e != ".sldasm" && e != ".slddrw") return "linked file";
        if (owner == SwDmDocumentType.swDmDocumentAssembly) return "component";
        if (owner == SwDmDocumentType.swDmDocumentDrawing) return "model";
        return "part link";
    }

    static string Short(SwDmDocumentType t)
    {
        return t == SwDmDocumentType.swDmDocumentPart ? "part" : t == SwDmDocumentType.swDmDocumentAssembly ? "assembly" : t == SwDmDocumentType.swDmDocumentDrawing ? "drawing" : "unknown";
    }

    // Where SOLIDWORKS would most likely find this reference inside the inbox: the stored path if it is
    // inside the inbox, otherwise a file of the same name anywhere in the inbox.
    static string FoundIn(Dictionary<string, List<string>> byName, string root, string stored)
    {
        if (string.IsNullOrEmpty(stored)) return "";
        if (stored.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase) && File.Exists(stored))
            return stored.Substring(root.Length).TrimStart('\\');
        List<string> hits;
        if (byName.TryGetValue(Path.GetFileName(stored), out hits))
            return string.Join(" | ", hits.Select(h => h.Substring(root.Length).TrimStart('\\'))) + (hits.Count > 1 ? " (AMBIGUOUS)" : "");
        return "MISSING";
    }

    static List<string> Names(object o)
    {
        var a = o as string[];
        return a == null ? new List<string>() : a.ToList();
    }

    static string Safe(Func<string> f)
    {
        try { var s = f(); return s ?? ""; } catch { return ""; }
    }
}

class Csv
{
    readonly List<string[]> rows = new List<string[]>();
    readonly string[] header;
    public Csv(params string[] header) { this.header = header; }
    public int Count { get { return rows.Count; } }
    public void Add(params string[] values) { rows.Add(values); }
    public void Save(string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine(string.Join(",", header.Select(Esc)));
        foreach (var r in rows) sb.AppendLine(string.Join(",", r.Select(Esc)));
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }
    static string Esc(string s)
    {
        s = s ?? "";
        return s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
