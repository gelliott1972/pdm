// CutList: what a weldment part's cut list holds, per configuration (Document Manager, read-only, never saves).
// For an assembly, lists the parts it references instead. Admin 15 Nov (D7: stock reference from the cut list).
// Usage: CutList.exe <file> [configuration filter]    The key comes from SW_DM_KEY and is never printed.
using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.swdocumentmgr;

static class CutList
{
    static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("Usage: CutList <file> [config filter]"); return 2; }
        var key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        var app = new SwDMClassFactory().GetApplication(key);
        if (args[0] == "--survey") return Survey(app, args[1], args[2]);
        string f = args[0], filter = args.Length > 1 ? args[1] : "";
        var type = f.EndsWith(".sldasm", StringComparison.OrdinalIgnoreCase) ? SwDmDocumentType.swDmDocumentAssembly : SwDmDocumentType.swDmDocumentPart;
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(f, type, true, out err) as ISwDMDocument31;
        if (doc == null) { Console.Error.WriteLine("not opened: " + err); return 3; }
        try
        {
            Console.WriteLine(Path.GetFileName(f));
            if (type == SwDmDocumentType.swDmDocumentAssembly)
            {
                var search = app.GetSearchOptionObject();
                search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchSubfolders); search.AddSearchPath(Path.GetDirectoryName(Path.GetFullPath(f))); search.AddSearchPath(@"P:\APEX\GNRIC");
                object brokenRefs, isVirtual, timeStamp, imported;
                var refs = doc.GetAllExternalReferences5(search, out brokenRefs, out isVirtual, out timeStamp, out imported) as string[];
                foreach (var r in refs ?? new string[0]) Console.WriteLine("  ref " + r);
                return 0;
            }
            foreach (string cn in (string[])doc.ConfigurationManager.GetConfigurationNames())
            {
                if (filter != "" && cn.IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                var cfg = doc.ConfigurationManager.GetConfigurationByName(cn) as ISwDMConfiguration16;
                var items = cfg == null ? null : cfg.GetCutListItems() as object[];
                Console.WriteLine("config " + cn + ": " + (items == null ? 0 : items.Length) + " cut-list item(s)");
                foreach (var o in items ?? new object[0])
                {
                    var it = o as ISwDMCutListItem2;
                    if (it == null) continue;
                    Console.WriteLine("  item '" + it.Name + "' qty " + it.Quantity);
                    foreach (string pn in (it.GetCustomPropertyNames() as string[]) ?? new string[0])
                    {
                        SwDmCustomInfoType t; string linked;
                        string v = it.GetCustomPropertyValue2(pn, out t, out linked);
                        Console.WriteLine("    " + pn + " = " + v + (string.IsNullOrEmpty(linked) || linked == v ? "" : "   [" + linked + "]"));
                    }
                }
            }
        }
        finally { try { doc.CloseDoc(); } catch { } }
        return 0;
    }

    // --survey <root> <out.tsv>: every part's cut-list items, per configuration, one line each (Grant 15 Nov, for SW's
    // clean-up): file, configuration, PartNo, item, Description, profile file (from the linked properties), LENGTH, MATERIAL.
    static int Survey(ISwDMApplication app, string root, string outFile)
    {
        var parts = Directory.EnumerateFiles(root, "*.sldprt", SearchOption.AllDirectories)
            .Where(p => p.IndexOf("\\.pdm\\", StringComparison.OrdinalIgnoreCase) < 0 && !Path.GetFileName(p).StartsWith("~$")).ToList();
        int n = 0, failed = 0;
        using (var w = new StreamWriter(outFile))
        {
            w.WriteLine("file\tconfiguration\tPartNo\titem\tDescription\tprofile file\tLENGTH\tMATERIAL\tquantity");
            foreach (var f in parts)
            {
                SwDmDocumentOpenError err;
                var doc = app.GetDocument(f, SwDmDocumentType.swDmDocumentPart, true, out err) as ISwDMDocument31;
                if (doc == null) { failed++; continue; }
                try
                {
                    foreach (string cn in (string[])doc.ConfigurationManager.GetConfigurationNames() ?? new string[0])
                    {
                        var cfg = doc.ConfigurationManager.GetConfigurationByName(cn) as ISwDMConfiguration16;
                        var items = cfg == null ? null : cfg.GetCutListItems() as object[];
                        if (items == null || items.Length == 0) continue;
                        string pn = "";
                        try { SwDmCustomInfoType t0; var c18 = cfg as ISwDMConfiguration18; if (c18 != null && ((c18.GetCustomPropertyNames() as string[]) ?? new string[0]).Any(x => x.Equals("PartNo", StringComparison.OrdinalIgnoreCase))) pn = c18.GetCustomProperty2("PartNo", out t0); } catch { }
                        foreach (var o in items)
                        {
                            var it = o as ISwDMCutListItem2;
                            if (it == null) continue;
                            string desc = "", len = "", mat = "", prof = "";
                            foreach (string p in (it.GetCustomPropertyNames() as string[]) ?? new string[0])
                            {
                                SwDmCustomInfoType t; string linked;
                                string v = it.GetCustomPropertyValue2(p, out t, out linked) ?? "";
                                if (p.Equals("Description", StringComparison.OrdinalIgnoreCase)) desc = v;
                                else if (p.Equals("LENGTH", StringComparison.OrdinalIgnoreCase)) len = v;
                                else if (p.Equals("MATERIAL", StringComparison.OrdinalIgnoreCase)) mat = v;
                                int at = (linked ?? "").LastIndexOf('@');
                                if (prof == "" && at >= 0 && (linked ?? "").EndsWith(".SLDPRT\"", StringComparison.OrdinalIgnoreCase)) prof = linked.Substring(at + 1).TrimEnd('"');
                            }
                            w.WriteLine(string.Join("\t", f.Substring(root.TrimEnd('\\').Length + 1), cn, pn, it.Name, desc.Replace('\t', ' '), prof, len, mat, it.Quantity));
                            n++;
                        }
                    }
                }
                catch { failed++; }
                finally { try { doc.CloseDoc(); } catch { } }
            }
        }
        Console.WriteLine("Parts " + parts.Count + ", cut-list lines " + n + ", not read " + failed);
        return 0;
    }
}
