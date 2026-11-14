// Draw: Draw session's SOLIDWORKS drawing tool (brief docs\briefs\draw-1.md, 14 Nov 2026).
// Works only on copies in C:\code\pdm\scratch\draw-test. Runs only while Draw holds the SwCmd round lock
// (tools\SwCmd\bin\SwCmd.exe lock --as draw --purpose "...") and Grant has not paused SOLIDWORKS.
// SOLIDWORKS 2026 only (SldWorks.Application.34).
//
// Usage: Draw.exe <command> [args]
//   inspect <drawing>                         report sheet, layers, views, notes, dimensions, tables
//   extract-templates <drawing> <outFolder>   James's sheet format (.slddrt) and drawing template (.drwdot) from a copy
//   save-table-templates <drawing> <outFolder>  every table on the sheet saved as a table template (BOM / cut list)
//   make-plate <part> <config> <out.SLDDRW> <template.drwdot>   first draft of a plate drawing (DRAFT layer, magenta)
//   accept <drawing> [--all]                  move what is selected in SOLIDWORKS (or everything) off layer DRAFT
//   select-draft <drawing>                    select every item still on layer DRAFT in the open drawing
//   views <part> <config|-> <out.pdf>         a look at a model: three views + isometric to a PDF, faces listed
//   make-section <part> <config> <out.SLDDRW> <template.drwdot>  first draft of a straight member (angle, RHS)
//                                             with holes/slots: face view, end view, isometric (DRAFT, magenta)
//   learn-layout <drawing> ...                learn the layout's feel from drawings laid out by hand (Layout.cs)
//   copy-switch <drawing> <newModel> <out.SLDDRW>  copy a drawing, switch it to a twin model, report what survives
//   pdf <drawing> <out.pdf>                   export a PDF
//   close-all                                 close every document without saving
//
// Build (from draw\src): see build.cmd
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    const string TestRoot = @"C:\code\pdm\scratch\draw-test";
    static ISldWorks sw;
    static List<string> problems = new List<string>();

    static int Main(string[] args)
    {
        if (args.Length < 1) return Usage();
        string cmd = args[0].ToLowerInvariant();
        var pos = args.Skip(1).Where(a => !a.StartsWith("--")).Select(a => a).ToList();
        var flags = new HashSet<string>(args.Skip(1).Where(a => a.StartsWith("--")).Select(a => a.Substring(2).ToLowerInvariant()));

        // Every path must be inside the test folder: never P:\, M:\, C:\PDM-work or Dropbox.
        for (int i = 0; i < pos.Count; i++)
        {
            if ((cmd == "make-plate" || cmd == "make-section" || cmd == "views") && i == 1) continue;   // configuration name
            string full;
            try { full = Path.GetFullPath(pos[i]); } catch (Exception ex) { Console.Error.WriteLine("Bad path: " + ex.Message); return 2; }
            if (!full.StartsWith(TestRoot + "\\", StringComparison.OrdinalIgnoreCase))
            { Console.Error.WriteLine("REFUSED: Draw works only inside " + TestRoot + ": " + full); return 2; }
            pos[i] = full;
        }
        string gate = Gate();
        if (gate != null) { Console.Error.WriteLine(gate); return 5; }
        try { sw = Attach(); }
        catch (Exception ex) { Console.Error.WriteLine("Could not reach SOLIDWORKS 2026: " + ex.Message); return 3; }
        int rc;
        try
        {
            switch (cmd)
            {
                case "inspect": if (pos.Count != 1) return Usage(); rc = Inspect(pos[0]); break;
                case "extract-templates": if (pos.Count != 2) return Usage(); rc = ExtractTemplates(pos[0], pos[1]); break;
                case "save-table-templates": if (pos.Count != 2) return Usage(); rc = SaveTableTemplates(pos[0], pos[1]); break;
                case "make-plate": if (pos.Count != 4) return Usage(); rc = MakePlate(pos[0], pos[1], pos[2], pos[3], flags); break;
                case "accept": if (pos.Count != 1) return Usage(); rc = Accept(pos[0], flags.Contains("all")); break;
                case "select-draft": if (pos.Count != 1) return Usage(); rc = SelectDraft(pos[0]); break;
                case "views": if (pos.Count != 3) return Usage(); rc = Views(pos[0], pos[1] == "-" ? null : pos[1], pos[2]); break;
                case "make-section": if (pos.Count != 4) return Usage(); rc = MakeSection(pos[0], pos[1], pos[2], pos[3]); break;
                case "learn-layout": if (pos.Count < 1) return Usage(); rc = LearnLayout(pos); break;
                case "copy-switch": if (pos.Count != 3) return Usage(); rc = CopySwitch(pos[0], pos[1], pos[2]); break;
                case "pdf": if (pos.Count != 2) return Usage(); rc = Pdf(pos[0], pos[1]); break;
                case "close-all": sw.CloseAllDocuments(true); Console.WriteLine("Closed all documents (not saved)."); rc = 0; break;
                default: return Usage();
            }
        }
        catch (Exception ex) { Console.Error.WriteLine("SOLIDWORKS error: " + ex); return 3; }
        if (problems.Count > 0) { Console.WriteLine("PROBLEMS:"); foreach (var p in problems) Console.WriteLine("  " + p); if (rc == 0) rc = 1; }
        return rc;
    }

    static int Usage()
    {
        Console.Error.WriteLine("Draw.exe inspect|extract-templates|save-table-templates|make-plate|pdf|close-all ... (see Draw.cs)");
        return 2;
    }

    // ---------- lock: Draw must hold the SwCmd round lock; Grant's pause stops everything ----------
    static string Gate()
    {
        string desk = System.Environment.GetEnvironmentVariable("PDM_DESK_DIR");
        string dir = Path.Combine(string.IsNullOrEmpty(desk) ? @"C:\code\pdm\desk" : desk, "sw");
        if (File.Exists(Path.Combine(dir, "pause.json"))) return "PAUSED: Grant has asked for SOLIDWORKS. Nothing done.";
        string lf = Path.Combine(dir, "lock.json");
        if (!File.Exists(lf)) return "NO LOCK: take it first: SwCmd lock --as draw --purpose \"...\". Nothing done.";
        string t = File.ReadAllText(lf);
        var h = System.Text.RegularExpressions.Regex.Match(t, "\"holder\"\\s*:\\s*\"([^\"]*)\"");
        var k = System.Text.RegularExpressions.Regex.Match(t, "\"kind\"\\s*:\\s*\"([^\"]*)\"");
        if (!h.Success || h.Groups[1].Value.ToLowerInvariant() != "draw" || !k.Success || k.Groups[1].Value != "round")
            return "IN USE: the SOLIDWORKS lock is not a Draw round (" + t.Trim() + "). Nothing done.";
        return null;
    }

    static ISldWorks Attach()
    {
        object o = null;
        try { o = Marshal.GetActiveObject("SldWorks.Application.34"); } catch (COMException) { }
        if (o == null)
        {
            if (System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length > 0)
                throw new Exception("a SOLIDWORKS is running but it is not 2026 (or not reachable); not starting another");
            o = Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
            Console.WriteLine("Started SOLIDWORKS 2026 (it stays open).");
        }
        var s = (ISldWorks)o;
        s.Visible = true;
        string rev = s.RevisionNumber();
        if (!rev.StartsWith("34.")) throw new Exception("not SOLIDWORKS 2026: revision " + rev);
        return s;
    }

    // ---------- open helpers ----------
    static ModelDoc2 OpenDoc(string file, string config, bool readOnly)
    {
        int type = Kind(file);
        int err = 0, warn = 0;
        int opt = (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (readOnly ? (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly : 0);
        var doc = (ModelDoc2)sw.OpenDoc6(file, type, opt, config ?? "", ref err, ref warn);
        if (doc == null) throw new Exception("could not open " + file + " (error " + err + ", warning " + warn + ")");
        if (err != 0 || warn != 0) Console.WriteLine("open " + Path.GetFileName(file) + ": error " + err + ", warning " + warn);
        // Every reference must resolve inside the test folder.
        object deps = sw.GetDocumentDependencies2(file, false, true, false);
        if (deps is object[])
        {
            var d = (object[])deps;
            for (int i = 1; i < d.Length; i += 2)
            {
                string r = d[i] as string;
                if (r != null && !r.StartsWith(TestRoot, StringComparison.OrdinalIgnoreCase)) Console.WriteLine("  reference (as stored): " + r);
            }
        }
        return doc;
    }

    static int Kind(string f)
    {
        switch (Path.GetExtension(f).ToLowerInvariant())
        {
            case ".sldprt": return (int)swDocumentTypes_e.swDocPART;
            case ".sldasm": return (int)swDocumentTypes_e.swDocASSEMBLY;
            case ".slddrw": return (int)swDocumentTypes_e.swDocDRAWING;
            default: throw new Exception("not a SOLIDWORKS model or drawing: " + f);
        }
    }

    static string Mm(double m) { return (m * 1000).ToString("0.##"); }

    // ---------- inspect ----------
    static int Inspect(string file)
    {
        var doc = OpenDoc(file, null, true);
        var drw = (DrawingDoc)doc;
        var sheet = (Sheet)drw.GetCurrentSheet();
        var p = (double[])sheet.GetProperties2();
        Console.WriteLine("Sheet " + sheet.GetName() + ": paper " + p[0] + ", scale " + p[2] + ":" + p[3] + ", first angle " + p[4]
            + ", " + Mm(p[5]) + " x " + Mm(p[6]) + " mm, format " + sheet.GetTemplateName() + ", property view '" + sheet.CustomPropertyView + "'");
        Console.WriteLine("Sheet format visible: " + drw.IGetCurrentSheet().SheetFormatVisible);

        var lm = (LayerMgr)doc.GetLayerManager();
        var names = lm.GetLayerList() as object[];
        Console.WriteLine("Layers (current '" + lm.GetCurrentLayer() + "'):");
        if (names != null) foreach (string n in names)
            {
                var l = (Layer)lm.GetLayer(n);
                Console.WriteLine("  " + n + ": colour " + ColourHex(l.Color) + ", visible " + l.Visible + ", printable " + l.Printable + ", style " + l.Style + ", width " + l.Width);
            }

        var v = (View)drw.GetFirstView();   // the sheet itself
        bool first = true;
        while (v != null)
        {
            if (first) Console.WriteLine("SHEET VIEW " + v.GetName2());
            else
            {
                var pos = (double[])v.Position;
                var outl = (double[])v.GetOutline();
                Console.WriteLine("VIEW " + v.GetName2() + ": type " + v.Type + ", orientation '" + v.GetOrientationName() + "', model "
                    + v.GetReferencedModelName() + " [" + v.ReferencedConfiguration + "], scale " + v.ScaleDecimal
                    + ", at " + Mm(pos[0]) + "," + Mm(pos[1]) + " outline " + Mm(outl[0]) + "," + Mm(outl[1]) + " - " + Mm(outl[2]) + "," + Mm(outl[3])
                    + ", display " + v.GetDisplayMode2() + ", tangent " + v.GetDisplayTangentEdges2());
            }
            var anns = v.GetAnnotations() as object[];
            if (anns != null) foreach (Annotation a in anns) DescribeAnnotation(a);
            first = false;
            v = (View)v.GetNextView();
        }
        var tables = new List<string>();
        return 0;
    }

    static void DescribeAnnotation(Annotation a)
    {
        var pos = a.GetPosition() as double[];
        string where = pos != null ? Mm(pos[0]) + "," + Mm(pos[1]) : "?";
        string head = "    " + TypeName(a.GetType()) + " '" + a.GetName() + "' layer '" + a.Layer + "' colour " + ColourHex(a.Color) + " at " + where
            + (a.IsDangling() ? " DANGLING" : "");
        switch (a.GetType())
        {
            case (int)swAnnotationType_e.swNote:
                var n = (Note)a.GetSpecificAnnotation();
                Console.WriteLine(head + ": \"" + Flat(n.GetText()) + "\" linked \"" + Flat(n.PropertyLinkedText) + "\" font " + FontOf(a) + " leader " + (n.GetLeaderInfo() != null));
                break;
            case (int)swAnnotationType_e.swDisplayDimension:
                var dd = (DisplayDimension)a.GetSpecificAnnotation();
                var dim = (Dimension)dd.GetDimension2(0);
                Console.WriteLine(head + ": value " + (dim != null ? (dim.GetSystemValue2("") * 1000).ToString("0.###") : "?") + " type " + dd.GetType()
                    + " paren " + dd.ShowParenthesis + " driven " + (dim != null ? dim.DrivenState.ToString() : "?")
                    + " text [" + Flat(dd.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix)) + "|" + Flat(dd.GetText((int)swDimensionTextParts_e.swDimensionTextCalloutAbove)) + "|"
                    + Flat(dd.GetText((int)swDimensionTextParts_e.swDimensionTextSuffix)) + "] font " + FontOf(a));
                break;
            case (int)swAnnotationType_e.swCenterMarkSym:
                var cm = (CenterMark)a.GetSpecificAnnotation();
                Console.WriteLine(head + ": size " + Mm(cm.Size) + " gap " + Mm(cm.Gap) + " lines " + cm.ShowLines + " docDefaults " + cm.UseDocDisplaySettings
                    + " style " + cm.Style + " centreline font " + cm.CenterLineFont + " extended " + string.Join("/", Enumerable.Range(0, 4).Select(i => Mm(cm.GetExtendedLength(0, i)))));
                break;
            case (int)swAnnotationType_e.swTableAnnotation:
                var ta = (TableAnnotation)a.GetSpecificAnnotation();
                Console.WriteLine(head + ": table type " + ta.Type + ", " + ta.RowCount + " x " + ta.ColumnCount);
                for (int r = 0; r < ta.RowCount; r++)
                    Console.WriteLine("      row " + r + ": " + string.Join(" | ", Enumerable.Range(0, ta.ColumnCount).Select(c => Flat(ta.Text[r, c]))));
                break;
            default:
                Console.WriteLine(head);
                break;
        }
    }

    static string FontOf(Annotation a)
    {
        try
        {
            var tf = (TextFormat)a.GetTextFormat(0);
            return tf == null ? "?" : tf.TypeFaceName + " " + Mm(tf.CharHeight) + "mm" + (tf.Bold ? " bold" : "");
        }
        catch { return "?"; }
    }

    static string TypeName(int t) { return Enum.GetName(typeof(swAnnotationType_e), t) ?? t.ToString(); }
    static string Flat(string s) { return s == null ? "" : s.Replace("\r", "").Replace("\n", "\\n"); }
    static string ColourHex(int c) { return "#" + (c & 0xFF).ToString("X2") + ((c >> 8) & 0xFF).ToString("X2") + ((c >> 16) & 0xFF).ToString("X2"); }
}
