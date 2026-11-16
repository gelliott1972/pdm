// SwRefCheck: read-only before/after checks for the sw-profiles run (brief step 7 and 8, 16 Nov 2026).
//   drawing (.SLDDRW): dimensions (display dimensions in all views), dangling annotations, rebuild errors, and every table's
//                      text (BOM and cut-list tables) as TABLE lines, so a BOM text change can be listed
//   assembly (.SLDASM): rebuild errors after a full rebuild of every configuration
// Opens each file read-only in SOLIDWORKS 2026, never saves, closes what it opened.
// Usage: SwRefCheck.exe <file> [<file> ...]   Run under SwCmd's lock (--as sw).
// Build (from tools\sw-macros, in PowerShell):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwRefCheck.exe SwRefCheck.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwRefCheck
{
    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwRefCheck <file> [...]"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int rc = 0;
        foreach (var a in args)
        {
            string file = Path.GetFullPath(a);
            if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.StartsWith("M:", StringComparison.OrdinalIgnoreCase)) { Console.WriteLine("ERROR\t" + file + "\trefused (Dropbox)"); continue; }
            string ext = Path.GetExtension(file).ToLowerInvariant();
            int type = ext == ".slddrw" ? (int)swDocumentTypes_e.swDocDRAWING : ext == ".sldasm" ? (int)swDocumentTypes_e.swDocASSEMBLY : (int)swDocumentTypes_e.swDocPART;
            bool wasOpen = sw.GetOpenDocumentByName(file) != null;
            int errs = 0, warns = 0;
            var doc = wasOpen ? (ModelDoc2)sw.GetOpenDocumentByName(file)
                : sw.OpenDoc6(file, type, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
            if (doc == null) { Console.WriteLine("ERROR\t" + file + "\tcould not open (errors " + errs + ")"); rc = 3; continue; }
            try
            {
                if (type == (int)swDocumentTypes_e.swDocDRAWING)
                {
                    doc.ForceRebuild3(false);
                    var drw = (DrawingDoc)doc;
                    int dims = 0, dangling = 0;
                    var tables = new List<string>();
                    foreach (string sh in (string[])drw.GetSheetNames())
                    {
                        drw.ActivateSheet(sh);
                        for (var v = (View)drw.GetFirstView(); v != null; v = (View)v.GetNextView())
                        {
                            for (var dd = (DisplayDimension)v.GetFirstDisplayDimension5(); dd != null; dd = (DisplayDimension)dd.GetNext5()) dims++;
                            foreach (Annotation an in (v.GetAnnotations() as object[]) ?? new object[0]) if (an.IsDangling()) dangling++;
                            foreach (TableAnnotation t in (v.GetTableAnnotations() as object[]) ?? new object[0])
                            {
                                for (int r = 0; r < t.RowCount; r++)
                                {
                                    var cells = new List<string>();
                                    for (int c = 0; c < t.ColumnCount; c++) cells.Add((t.Text2[r, c, true] ?? "").Replace("\t", " ").Replace("\r", " ").Replace("\n", " "));
                                    tables.Add(sh + "\t" + v.Name + "\t" + r + "\t" + string.Join(" | ", cells));
                                }
                            }
                        }
                    }
                    Console.WriteLine("DRAWING\t" + file + "\tdims " + dims + "\tdangling " + dangling + "\terrors " + Errors(doc));
                    foreach (var t in tables) Console.WriteLine("TABLE\t" + file + "\t" + t);
                }
                else
                {
                    var cfgs = (string[])doc.GetConfigurationNames();
                    string active = doc.ConfigurationManager.ActiveConfiguration.Name;
                    var per = new List<string>();
                    foreach (var c in cfgs)
                    {
                        if (doc.ConfigurationManager.ActiveConfiguration.Name != c) doc.ShowConfiguration2(c);
                        doc.ForceRebuild3(false);
                        per.Add(c + "=" + Errors(doc));
                    }
                    if (doc.ConfigurationManager.ActiveConfiguration.Name != active) doc.ShowConfiguration2(active);
                    Console.WriteLine("MODEL\t" + file + "\terrors " + string.Join("; ", per));
                }
            }
            catch (Exception e) { Console.WriteLine("ERROR\t" + file + "\t" + e.Message); rc = 3; }
            finally { if (!wasOpen) sw.CloseDoc(doc.GetTitle()); }
            Console.Out.Flush();
        }
        return rc;
    }

    static int Errors(ModelDoc2 doc)
    {
        object feats, codes, warnFlags;
        doc.Extension.GetWhatsWrong(out feats, out codes, out warnFlags);
        var ef = feats as object[]; var wf = warnFlags as bool[];
        int n = 0;
        if (ef != null) for (int i = 0; i < ef.Length; i++) if (wf == null || i >= wf.Length || !wf[i]) n++;
        return n;
    }
}
