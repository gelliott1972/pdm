// Outstanding: the items still without a drawing, worked out from a folder (P:\) with the Document Manager, read-only,
// never saves. Same rule as `pdm coverage` (src\Pdm\Coverage.cs, brief draw-2 section 1): one item per PartNo (file
// level and per configuration); left out FAST-*, MDL-*, K-beam (KBA, KBP, KB-SKEL), files without a PartNo, and
// PartNos that exist only as As Welded configurations. Drawn: a drawing named by the PartNo, or whose title block
// (sheet 1's custom-property view, else its first model view) shows that PartNo (configuration's value, else the file's).
// Usage: Outstanding.exe <root> <out.tsv>    The key comes from SW_DM_KEY and is never printed.
// Writes one line per item: status, PartNo, type, folder, file, configurations, description, how drawn.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.swdocumentmgr;

static class Outstanding
{
    static ISwDMApplication app;
    static readonly Regex LeftOut = new Regex(@"^(FAST-|MDL-)|-(KBA|KBP)-|-KB-SKEL", RegexOptions.IgnoreCase);

    static int Main(string[] args)
    {
        if (args.Length != 2) { Console.Error.WriteLine("Usage: Outstanding <root> <out.tsv>"); return 2; }
        string root = Path.GetFullPath(args[0]).TrimEnd('\\');
        var key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        app = new SwDMClassFactory().GetApplication(key);
        if (app == null) { Console.Error.WriteLine("Document Manager rejected the key."); return 2; }

        var files = Directory.EnumerateFiles(root, "*.*", SearchOption.AllDirectories)
            .Where(f => f.IndexOf("\\.pdm\\", StringComparison.OrdinalIgnoreCase) < 0 && !Path.GetFileName(f).StartsWith("~$"))
            .Where(f => TypeOf(f) != SwDmDocumentType.swDmDocumentUnknown).ToList();
        var models = files.Where(f => TypeOf(f) != SwDmDocumentType.swDmDocumentDrawing).ToList();
        var drawings = files.Where(f => TypeOf(f) == SwDmDocumentType.swDmDocumentDrawing).ToList();
        Console.WriteLine("Models " + models.Count + ", drawings " + drawings.Count);

        // items: PartNo -> (file, configs, description)
        var items = new Dictionary<string, Item>(StringComparer.OrdinalIgnoreCase);
        int failed = 0;
        foreach (var f in models)
        {
            if (LeftOut.IsMatch(Path.GetFileName(f))) continue;
            ISwDMDocument31 doc = null;
            try
            {
                SwDmDocumentOpenError err;
                doc = app.GetDocument(f, TypeOf(f), true, out err) as ISwDMDocument31;
                if (doc == null) { Console.WriteLine("NOT READ " + f + " (" + err + ")"); failed++; continue; }
                var entries = new List<Tuple<string, string, string>>();   // config, PartNo, description
                string fpn = Prop(doc, null, "PartNo");
                if (!string.IsNullOrWhiteSpace(fpn)) entries.Add(Tuple.Create("", fpn.Trim(), Prop(doc, null, "Description") ?? ""));
                foreach (string cn in (string[])doc.ConfigurationManager.GetConfigurationNames() ?? new string[0])
                {
                    string pn = Prop(doc, cn, "PartNo");
                    if (!string.IsNullOrWhiteSpace(pn)) entries.Add(Tuple.Create(cn, pn.Trim(), Prop(doc, cn, "Description") ?? Prop(doc, null, "Description") ?? ""));
                }
                foreach (var g in entries.Where(e => !LeftOut.IsMatch(e.Item2)).GroupBy(e => e.Item2, StringComparer.OrdinalIgnoreCase))
                {
                    if (g.All(e => e.Item1.IndexOf("As Welded", StringComparison.OrdinalIgnoreCase) >= 0)) continue;
                    Item it;
                    if (!items.TryGetValue(g.Key, out it)) { it = new Item { PartNo = g.Key, File = f }; items[g.Key] = it; }
                    else if (!string.Equals(it.File, f, StringComparison.OrdinalIgnoreCase)) it.Others.Add(f);
                    if (string.Equals(it.File, f, StringComparison.OrdinalIgnoreCase))
                        foreach (var e in g) { if (e.Item1 != "") it.Configs.Add(e.Item1); if (it.Description == "" && e.Item3 != "") it.Description = e.Item3; }
                }
            }
            catch (Exception ex) { Console.WriteLine("NOT READ " + f + " (" + ex.Message + ")"); failed++; }
            finally { if (doc != null) try { doc.CloseDoc(); } catch { } }
        }

        // drawn: by name, and by title block
        var drawnHow = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in drawings) drawnHow[Path.GetFileNameWithoutExtension(d)] = "named " + Rel(root, d);
        var modelsByName = models.GroupBy(m => Path.GetFileName(m), StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);
        int tbFailed = 0;
        foreach (var d in drawings)
        {
            ISwDMDocument10 doc = null;
            try
            {
                SwDmDocumentOpenError err;
                doc = app.GetDocument(d, SwDmDocumentType.swDmDocumentDrawing, true, out err) as ISwDMDocument10;
                if (doc == null) { tbFailed++; continue; }
                var sheets = doc.GetSheets() as object[];
                var sheet = sheets == null ? null : sheets.FirstOrDefault() as ISwDMSheet4;
                if (sheet == null) continue;
                var views = ((sheet.GetViews() as object[]) ?? new object[0]).OfType<ISwDMView>().Where(v => !string.IsNullOrEmpty(v.ReferencedDocument)).ToList();
                string named = Convert.ToString(sheet.CustomPropertyView) ?? "";
                var view = views.FirstOrDefault(x => string.Equals(Convert.ToString(x.Name), named, StringComparison.OrdinalIgnoreCase)) ?? views.FirstOrDefault();
                if (view == null) continue;
                string refPath = (Convert.ToString(view.ReferencedDocument) ?? "").Replace('/', '\\');
                string cfg = Convert.ToString(view.ReferencedConfiguration) ?? "";
                List<string> cands;
                if (!modelsByName.TryGetValue(Path.GetFileName(refPath), out cands)) continue;
                // the same file name in several folders: the one nearest the drawing
                string model = cands.OrderByDescending(m => CommonPrefix(m, d)).First();
                string pn = ModelProp(model, cfg, "PartNo");
                if (!string.IsNullOrWhiteSpace(pn) && !drawnHow.ContainsKey(pn.Trim())) drawnHow[pn.Trim()] = "title block of " + Rel(root, d);
            }
            catch (Exception) { tbFailed++; }
            finally { if (doc != null) try { doc.CloseDoc(); } catch { } }
        }

        using (var w = new StreamWriter(args[1]))
        {
            w.WriteLine("status\tPartNo\ttype\tfolder\tfile\tconfigurations\tdescription\tdrawn by\talso in");
            foreach (var it in items.Values.OrderBy(i => i.PartNo, StringComparer.OrdinalIgnoreCase))
            {
                string how;
                bool drawn = drawnHow.TryGetValue(it.PartNo, out how);
                var m = Regex.Match(it.PartNo, @"^[^-]+-([A-Z]+)", RegexOptions.IgnoreCase);
                w.WriteLine(string.Join("\t", drawn ? "drawn" : "OUTSTANDING", it.PartNo, m.Success ? m.Groups[1].Value.ToUpper() : "?",
                    Rel(root, Path.GetDirectoryName(it.File)), Path.GetFileName(it.File), string.Join(" | ", it.Configs.Distinct()),
                    it.Description.Replace('\t', ' ').Replace("\r", " ").Replace("\n", " "), drawn ? how : "", string.Join(" | ", it.Others.Select(o => Rel(root, o)))));
            }
        }
        Console.WriteLine("Items " + items.Count + ", drawn " + items.Values.Count(i => drawnHow.ContainsKey(i.PartNo)) + ", models not read " + failed + ", title blocks not read " + tbFailed);
        return 0;
    }

    class Item { public string PartNo, File, Description = ""; public List<string> Configs = new List<string>(); public List<string> Others = new List<string>(); }

    static int CommonPrefix(string a, string b) { int i = 0; while (i < a.Length && i < b.Length && char.ToUpperInvariant(a[i]) == char.ToUpperInvariant(b[i])) i++; return i; }
    static string Rel(string root, string p) { return p.StartsWith(root, StringComparison.OrdinalIgnoreCase) ? p.Substring(root.Length).TrimStart('\\') : p; }

    // Unevaluated value (as the app's item list reads it), file level when config is null
    static string Prop(ISwDMDocument31 doc, string config, string name)
    {
        try
        {
            SwDmCustomInfoType t;
            if (config == null)
                return Has(doc.GetCustomPropertyNames(), name) ? doc.GetCustomProperty2(name, out t) : null;
            var cfg = doc.ConfigurationManager.GetConfigurationByName(config) as ISwDMConfiguration18;
            if (cfg == null || !Has(cfg.GetCustomPropertyNames(), name)) return null;
            return cfg.GetCustomProperty2(name, out t);
        }
        catch { return null; }
    }

    // As a title block shows it: the configuration's resolved value, else the file's
    static string ModelProp(string model, string config, string name)
    {
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(model, TypeOf(model), true, out err) as ISwDMDocument31;
        if (doc == null) return null;
        try
        {
            string v = null, expr, res;
            bool linked;
            if (config != "")
            {
                var cfg = (doc.ConfigurationManager.GetConfigurationByName(config) ?? doc.ConfigurationManager.GetConfigurationByName(config.Split('<')[0])) as ISwDMConfiguration18;
                var cn = cfg == null ? null : ((cfg.GetCustomPropertyNames() as string[]) ?? new string[0]).FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (cn != null) { SwDmCustomInfoType t; v = cfg.GetCustomPropertyValues(cn, out t, out expr); }
            }
            if (string.IsNullOrEmpty(v))
            {
                var n = ((doc.GetCustomPropertyNames() as string[]) ?? new string[0]).FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (n != null) { SwDmCustomInfoType t; v = doc.GetCustomPropertyValues(n, out t, out expr); }
            }
            return string.IsNullOrEmpty(v) ? null : v;
        }
        catch { return null; }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    static bool Has(object names, string name) { var a = names as string[]; return a != null && a.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase)); }

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
}
