// DmCutList: read-only, no SOLIDWORKS: a part's cut list per configuration with Document Manager (Admin, 21 Nov 2026:
// confirm 08X04-BKP's plate Description in the vault file). Opens the file read-only; the licence key comes from the
// SW_DM_KEY environment variable and is never printed.
// Usage: DmCutList.exe <part> [property | *]   (default property: Description; * = every cut-list property, with its link)
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.swdocumentmgr.dll /out:bin\DmCutList.exe DmCutList.cs

using System;
using System.IO;
using SolidWorks.Interop.swdocumentmgr;

static class DmCutList
{
    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]), prop = a.Length > 1 ? a[1] : "Description";
        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        var app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key);
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(file, SwDmDocumentType.swDmDocumentPart, true, out err);
        if (doc == null) { Console.Error.WriteLine("could not open: " + err); return 3; }
        try
        {
            var cm = doc.ConfigurationManager;
            foreach (string cn in (string[])cm.GetConfigurationNames())
            {
                var cfg = (ISwDMConfiguration16)cm.GetConfigurationByName(cn);
                foreach (ISwDMCutListItem4 it in (object[])cfg.GetCutListItems() ?? new object[0])
                {
                    var names = prop == "*" ? ((string[])it.GetCustomPropertyNames() ?? new string[0]) : new[] { prop };
                    foreach (var pn in names)
                    {
                        SwDmCustomInfoType t; string linked = null;
                        string val;
                        try { val = it.GetCustomPropertyValue2(pn, out t, out linked) ?? ""; } catch { val = "(none)"; }
                        Console.WriteLine(cn + "\t" + it.Name + "\tqty " + it.Quantity + "\t" + pn + " = " + val + (string.IsNullOrEmpty(linked) ? "" : "\t(linked to: " + linked + ")"));
                    }
                }
            }
        }
        finally { doc.CloseDoc(); }
        return 0;
    }
}
