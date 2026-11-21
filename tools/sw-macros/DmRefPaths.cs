// DmRefPaths: read-only, no SOLIDWORKS (calc-08a-11 / Grant 21 Nov 2026): the reference paths SOLIDWORKS files hold,
// to find any that point at a local folder instead of P:\. Document Manager, files opened read-only, never saved.
// Prints, per file:
//   COMP  <file> <asm config> <component name> <referenced config> <stored path> <suppressed> <transform (raw array)>
//   VIEW  <file> <sheet> <view> <referenced config> <stored path>          (drawings)
//   REF   <file> <path as Document Manager reports it, no search folders> <broken>
// and a NOTP line for every path not under P:\ (C:\PDM\ is reported too: same files, but not the P:\ spelling).
// The licence key comes from the SW_DM_KEY environment variable and is never printed.
// Usage: DmRefPaths.exe <file|@list.txt> [...]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.swdocumentmgr.dll /out:bin\DmRefPaths.exe DmRefPaths.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.swdocumentmgr;

static class DmRefPaths
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static string S(Func<string> f) { try { return f() ?? ""; } catch { return "(error)"; } }

    static void Check(string file, string kind, string path)
    {
        if (string.IsNullOrEmpty(path) || path == "(error)") return;
        if (!path.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase)) Console.WriteLine("NOTP\t" + file + "\t" + kind + "\t" + path);
    }

    static int Main(string[] a)
    {
        var files = new List<string>();
        foreach (var x in a)
            if (x.StartsWith("@")) files.AddRange(File.ReadAllLines(x.Substring(1)).Select(l => l.Trim()).Where(l => l.Length > 0));
            else files.Add(x);
        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        var app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key);
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchForDrawing
            | SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);   // no search folders added
        int bad = 0;
        foreach (var f in files)
        {
            string ext = Path.GetExtension(f).ToUpperInvariant();
            var type = ext == ".SLDASM" ? SwDmDocumentType.swDmDocumentAssembly : ext == ".SLDDRW" ? SwDmDocumentType.swDmDocumentDrawing : SwDmDocumentType.swDmDocumentPart;
            SwDmDocumentOpenError err;
            var doc = app.GetDocument(f, type, true, out err) as ISwDMDocument31;
            if (doc == null) { Console.WriteLine("FAILED\t" + f + "\t" + err); bad++; continue; }
            try
            {
                if (type == SwDmDocumentType.swDmDocumentAssembly)
                {
                    var cm = doc.ConfigurationManager;
                    foreach (string cn in (string[])cm.GetConfigurationNames())
                    {
                        var cfg = (ISwDMConfiguration18)cm.GetConfigurationByName(cn);
                        foreach (var o in (object[])cfg.GetComponents() ?? new object[0])
                        {
                            var c = o as ISwDMComponent12;
                            if (c == null) continue;
                            string p = S(() => c.PathName);
                            string sup = S(() => c.IsSuppressed() ? "suppressed" : "");
                            string xf = "";
                            try
                            {
                                var t = c.Transform as double[];
                                if (t != null) xf = string.Join(" ", t.Select(v => Math.Round(v, 6).ToString("0.######", Inv)));   // raw, as stored
                            }
                            catch { xf = "(error)"; }
                            Console.WriteLine("COMP\t" + f + "\t" + cn + "\t" + S(() => c.Name2) + "\t" + S(() => c.ConfigurationName) + "\t" + p + "\t" + sup + "\t" + xf);
                            Check(f, "component in " + cn, p);
                        }
                    }
                }
                if (type == SwDmDocumentType.swDmDocumentDrawing)
                {
                    var dd = doc as ISwDMDocument10;
                    foreach (var o in (object[])dd.GetViews() ?? new object[0])
                    {
                        var v = o as ISwDMView;
                        if (v == null) continue;
                        string p = S(() => v.ReferencedDocument);
                        Console.WriteLine("VIEW\t" + f + "\t" + S(() => v.SheetName) + "\t" + S(() => v.Name) + "\t" + S(() => v.ReferencedConfiguration) + "\t" + p);
                        Check(f, "view " + S(() => v.Name), p);
                    }
                }
                object broken, isVirtual, stamps, imported;
                var refs = doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[];
                var b = broken as int[];
                if (refs != null)
                    for (int i = 0; i < refs.Length; i++)
                    {
                        Console.WriteLine("REF\t" + f + "\t" + refs[i] + "\t" + (b != null && i < b.Length ? b[i].ToString() : "?"));
                        Check(f, "reference", refs[i]);
                    }
            }
            finally { doc.CloseDoc(); }
        }
        return bad == 0 ? 0 : 1;
    }
}
