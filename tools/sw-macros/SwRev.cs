// SwRev: report how SOLIDWORKS drawings carry their revision: the Revision custom property, the revision table on each
// sheet, and the title-block notes that show it. Opens every drawing read-only and never saves. For the release design.
// Usage: SwRev.exe <file-or-folder> [...]            folders are searched for .slddrw
// Output: "<path>: ..." per drawing, then indented, tab-separated PROP, SHEET, ROW and NOTE lines. NOTE lines are those
// whose link or text mentions a revision, or whose text is 3 characters or fewer. Sheets are activated and the sheet
// format is opened for edit in memory only, to reach its notes. Restarts SOLIDWORKS after a crash and tries that file
// once more. Refuses to run while SOLIDWORKS is already open, and refuses Dropbox.
// Build (from tools\sw-macros):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwRev.exe SwRev.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwRev
{
    static int Main(string[] args)
    {
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwRev <file-or-folder> [...]"); return 2; }
        var files = new List<string>();
        foreach (var a in args)
        {
            string p = Path.GetFullPath(a);
            if (p.IndexOf("dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || p.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("Refused (Dropbox): " + p); return 2; }
            if (Directory.Exists(p))
                files.AddRange(Directory.GetFiles(p, "*.slddrw", SearchOption.AllDirectories)
                    .Where(f => f.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) < 0 && !Path.GetFileName(f).StartsWith("~$"))
                    .OrderBy(f => f, StringComparer.OrdinalIgnoreCase));
            else if (File.Exists(p) && p.EndsWith(".slddrw", StringComparison.OrdinalIgnoreCase)) files.Add(p);
            else { Console.Error.WriteLine("Not found or not a drawing: " + p); return 2; }
        }
        if (Process.GetProcessesByName("SLDWORKS").Length > 0) { Console.Error.WriteLine("SOLIDWORKS is already running. Close it first."); return 2; }

        Process started;
        var sw = Start(out started);
        int crashed = 0;
        try
        {
            foreach (var f in files)
            {
                for (int attempt = 1; ; attempt++)
                {
                    var o = new StringBuilder();
                    try
                    {
                        One(sw, f, o);
                        Console.Write(o);
                        break;
                    }
                    catch (System.Runtime.InteropServices.COMException ex)
                    {
                        Console.Error.WriteLine("SOLIDWORKS failed on " + f + " (0x" + ex.ErrorCode.ToString("X8") + "); restarting it.");
                        Stop(sw, started);
                        sw = Start(out started);
                        if (attempt == 2)
                        {
                            Console.WriteLine(f + ": CRASHED TWICE, not read");
                            crashed++;
                            break;
                        }
                    }
                }
            }
        }
        finally { Stop(sw, started); }
        return crashed == 0 ? 0 : 1;
    }

    static void One(ISldWorks sw, string f, StringBuilder o)
    {
        int errs = 0, warns = 0;
        var doc = sw.OpenDoc6(f, (int)swDocumentTypes_e.swDocDRAWING, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref errs, ref warns);
        o.AppendLine(f + ": open errors=" + errs + " warnings=" + warns + (doc == null ? "  NOT OPENED" : ""));
        if (doc != null)
        {
            var cpm = doc.Extension.CustomPropertyManager[""];
            foreach (string name in (cpm.GetNames() as object[] ?? new object[0]).Cast<string>())
            {
                if (name.IndexOf("rev", StringComparison.OrdinalIgnoreCase) < 0) continue;
                string raw, val; bool wasResolved, linked;
                cpm.Get6(name, false, out raw, out val, out wasResolved, out linked);
                o.AppendLine("    PROP\t" + name + "\t" + raw + "\t" + val);
            }
            var drw = (IDrawingDoc)doc;
            foreach (string sn in (string[])drw.GetSheetNames())
            {
                drw.ActivateSheet(sn);
                var sheet = (ISheet)drw.GetCurrentSheet();
                var rt = sheet.RevisionTable;
                o.AppendLine("    SHEET\t" + sn + "\tformat=" + sheet.GetTemplateName() + "\ttable=" + (rt == null ? "no" : "yes\tcurrent=" + rt.CurrentRevision));
                if (rt != null)
                {
                    var t = (ITableAnnotation)rt;
                    for (int r = 0; r < t.RowCount; r++)
                        o.AppendLine("    ROW\t" + r + "\t" + string.Join(" | ", Enumerable.Range(0, t.ColumnCount).Select(c => Clean(t.Text[r, c]))));
                }
                Notes((IView)drw.GetFirstView(), "sheet", o);
                drw.EditTemplate();
                Notes((IView)drw.GetFirstView(), "format", o);
                drw.EditSheet();
            }
        }
        sw.CloseAllDocuments(true);
    }

    static void Notes(IView v, string where, StringBuilder o)
    {
        foreach (var n in (v.GetNotes() as object[] ?? new object[0]).Cast<INote>())
        {
            string text = n.GetText() ?? "", link = n.PropertyLinkedText ?? "";
            if (text.Trim().Length == 0 && link.Length == 0) continue;
            bool rev = (text + " " + link).IndexOf("rev", StringComparison.OrdinalIgnoreCase) >= 0;
            if (rev || text.Trim().Length <= 3)
                o.AppendLine("    NOTE\t" + where + "\ttext=" + Clean(text) + "\tlink=" + Clean(link));
        }
    }

    static string Clean(string s) { return (s ?? "").Replace("\r", " ").Replace("\n", " ").Replace("\t", " "); }

    static ISldWorks Start(out Process started)
    {
        var sw = (ISldWorks)Activator.CreateInstance(NewestSolidWorks());
        started = Process.GetProcessesByName("SLDWORKS").FirstOrDefault();
        return sw;
    }

    static void Stop(ISldWorks sw, Process started)
    {
        try { sw.CloseAllDocuments(true); sw.ExitApp(); } catch { }
        try { System.Runtime.InteropServices.Marshal.FinalReleaseComObject(sw); } catch { }
        if (started != null && !started.WaitForExit(30000))
            try { started.Kill(); started.WaitForExit(10000); } catch { }
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
