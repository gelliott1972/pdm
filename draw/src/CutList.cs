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
}
