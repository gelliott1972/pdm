// DmRepoint: no SOLIDWORKS (Grant, SW window, 22 Nov 2026, square-edge STEP sandbox): point a sandbox file's references
// from one folder to another with Document Manager, e.g. a squared part kept in the cache, made in an earlier sandbox,
// copied into a new one: its link to the skeleton part must point into the new sandbox. Only files under
// C:\PDM-work\sestp\ (refuses anything else, so never P:\ or Dropbox). A reference is changed only when the new file
// exists. Lists the references before and after; the licence key comes from SW_DM_KEY and is never printed.
// Usage: DmRepoint.exe <file> <old folder> <new folder>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.swdocumentmgr.dll /out:bin\DmRepoint.exe DmRepoint.cs

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.swdocumentmgr;

static class DmRepoint
{
    const string Root = @"C:\PDM-work\sestp\";

    // comp <asm> <old file name> <new path> [oldCfg=newCfg ...] [--checked-out] (anchors step, 22 Nov): every component of
    // <asm> using that file now uses <new path>, with its configuration renamed by the map. Writes a sandbox copy under
    // C:\PDM-work\grout\, or with --checked-out a P:\ assembly that is checked out here (writable). Never Dropbox or .pdm.
    static int Comp(string[] a)
    {
        bool checkedOut = a.Contains("--checked-out");
        a = a.Where(x => x != "--checked-out").ToArray();
        if (a.Length < 3) { Console.Error.WriteLine("Usage: DmRepoint comp <asm> <old file name> <new path> [oldCfg=newCfg ...] [--checked-out]"); return 2; }
        string asm = Path.GetFullPath(a[0]), oldName = a[1], newPath = Path.GetFullPath(a[2]);
        if (asm.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || asm.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || asm.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0)
        { Console.Error.WriteLine("REFUSED: never Dropbox or .pdm"); return 2; }
        bool vault = asm.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase) || asm.StartsWith(@"C:\PDM\", StringComparison.OrdinalIgnoreCase);
        if (vault && (!checkedOut || (File.GetAttributes(asm) & FileAttributes.ReadOnly) != 0)) { Console.Error.WriteLine("REFUSED: a vault file only with --checked-out and only if checked out (writable): " + asm); return 2; }
        if (!vault && !asm.StartsWith(@"C:\PDM-work\grout\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: copies under C:\\PDM-work\\grout only"); return 2; }
        if (!File.Exists(newPath)) { Console.Error.WriteLine("no file " + newPath); return 2; }
        var map = a.Skip(3).Select(m => m.Split(new[] { '=' }, 2)).ToDictionary(p => p[0], p => p[1]);
        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        var app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key);
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(asm, SwDmDocumentType.swDmDocumentAssembly, false, out err) as ISwDMDocument31;
        if (doc == null) { Console.WriteLine("FAILED\topen " + err); return 3; }
        int n = 0, bad = 0;
        try
        {
            var cm = doc.ConfigurationManager;
            foreach (string cn in (string[])cm.GetConfigurationNames())
                foreach (var o in (object[])((ISwDMConfiguration18)cm.GetConfigurationByName(cn)).GetComponents() ?? new object[0])
                {
                    var c = o as ISwDMComponent12;
                    if (c == null || !string.Equals(Path.GetFileName(c.PathName), oldName, StringComparison.OrdinalIgnoreCase)) continue;
                    string oc = c.ConfigurationName, nc;
                    if (!map.TryGetValue(oc, out nc)) nc = oc;
                    var res = c.Replace(newPath, nc, true);
                    Console.WriteLine("REPLACE\t" + cn + "\t" + c.Name2 + "\t" + oldName + " <" + oc + "> -> " + Path.GetFileName(newPath) + " <" + nc + ">\t" + res);
                    if (!(res.ToString() == "True" || res.ToString().EndsWith("NoError"))) bad++;
                    n++;
                }
            if (n > 0 && bad == 0) { var se = doc.Save(); Console.WriteLine("SAVE\t" + se); if (se != SwDmDocumentSaveError.swDmDocumentSaveErrorNone) bad++; }
        }
        finally { doc.CloseDoc(); }
        Console.WriteLine("REPLACED\t" + n + "\tproblems " + bad);
        return n > 0 && bad == 0 ? 0 : 1;
    }

    static string[] Refs(ISwDMApplication app, ISwDMDocument31 doc)
    {
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly | SwDmSearchFilters.SwDmSearchForDrawing
            | SwDmSearchFilters.SwDmSearchExternalReference | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);
        object broken, isVirtual, stamps, imported;
        return doc.GetAllExternalReferences5(search, out broken, out isVirtual, out stamps, out imported) as string[] ?? new string[0];
    }

    static int Main(string[] a)
    {
        if (a.Length > 0 && a[0] == "comp") return Comp(a.Skip(1).ToArray());
        if (a.Length != 3) { Console.Error.WriteLine("Usage: DmRepoint <file> <old folder> <new folder> | comp <asm> <old file name> <new path> [oldCfg=newCfg ...] [--checked-out]"); return 2; }
        string file = Path.GetFullPath(a[0]);
        string oldDir = Path.GetFullPath(a[1]).TrimEnd('\\') + "\\", newDir = Path.GetFullPath(a[2]).TrimEnd('\\') + "\\";
        if (!file.StartsWith(Root, StringComparison.OrdinalIgnoreCase) || !newDir.StartsWith(Root, StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("REFUSED: sandbox files only (" + Root + ")"); return 2; }
        string key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) { Console.Error.WriteLine("SW_DM_KEY is not set."); return 2; }
        var app = ((ISwDMClassFactory)new SwDMClassFactory()).GetApplication(key);
        string ext = Path.GetExtension(file).ToUpperInvariant();
        var type = ext == ".SLDASM" ? SwDmDocumentType.swDmDocumentAssembly : ext == ".SLDDRW" ? SwDmDocumentType.swDmDocumentDrawing : SwDmDocumentType.swDmDocumentPart;
        SwDmDocumentOpenError err;
        var doc = app.GetDocument(file, type, false, out err) as ISwDMDocument31;
        if (doc == null) { Console.WriteLine("FAILED\topen " + err); return 3; }
        int changed = 0, problems = 0;
        try
        {
            foreach (var r in Refs(app, doc))
            {
                Console.WriteLine("BEFORE\t" + r);
                if (!r.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase)) continue;
                string n = newDir + r.Substring(oldDir.Length);
                if (!File.Exists(n)) { Console.WriteLine("PROBLEM\tno file " + n); problems++; continue; }
                doc.ReplaceReference(r, n);
                changed++;
            }
            if (changed > 0 && problems == 0)
            {
                var se = doc.Save();
                Console.WriteLine("SAVE\t" + se);
                if (se != SwDmDocumentSaveError.swDmDocumentSaveErrorNone) problems++;
            }
        }
        finally { doc.CloseDoc(); }
        // check by reading the file again
        doc = app.GetDocument(file, type, true, out err) as ISwDMDocument31;
        int left = 0;
        try
        {
            foreach (var r in Refs(app, doc))
            {
                Console.WriteLine("AFTER\t" + r);
                if (r.StartsWith(oldDir, StringComparison.OrdinalIgnoreCase)) left++;
            }
        }
        finally { doc.CloseDoc(); }
        Console.WriteLine("REPOINTED\t" + changed + "\tstill in the old folder " + left + "\tproblems " + problems);
        return left == 0 && problems == 0 ? 0 : 1;
    }
}
