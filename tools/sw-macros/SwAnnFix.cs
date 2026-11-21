// SwAnnFix: one open per drawing (Grant, 21 Nov 2026): every annotation whose own colour override is WHITE or BLACK goes back
// to Default (override removed). Any other override colour is left alone and listed (FLAG) for Grant. Layers are not touched.
// In the same open, before and after: annotation count, every display dimension's name and value, every annotation's
// position, and the dangling count must be unchanged, or nothing is saved. The sheet that was active when opened is made
// active again before the save. A drawing with nothing to change is closed without saving.
// Usage: SwAnnFix.exe <drawing> [<drawing> ...] [--save] [--checked-out]
//   --save writes only checked-out (writable) vault drawings with --checked-out, or copies under C:\code\pdm\scratch.
// Output per drawing: FLAG lines, then RESULT <file> CHANGED n SAVED | NOCHANGE | CHANGED n (not saved) | FAIL <why>
// Run under SwCmd's lock (--as sw). Build:
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwAnnFix.exe SwAnnFix.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwAnnFix
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    const int Over = (int)swLayerOverride_e.swLayerOverrideColor;
    static string Rgb(int c) { return string.Format("{0},{1},{2}", c & 255, (c >> 8) & 255, (c >> 16) & 255); }

    static string TypeOf(Annotation an)
    {
        string t = ((swAnnotationType_e)an.GetType()).ToString().Replace("sw", "");
        if (an.GetType() == (int)swAnnotationType_e.swNote)
        {
            var n = an.GetSpecificAnnotation() as Note;
            if (n != null && n.GetBalloonStyle() != (int)swBalloonStyle_e.swBS_None) t = "Balloon";
        }
        return t;
    }

    // Everything that must not change: one line per annotation (sheet, view, type, name, position, dimension value), and the dangling count.
    static List<string> Snapshot(DrawingDoc dd, out int dangling)
    {
        var lines = new List<string>(); dangling = 0;
        foreach (string sheet in (string[])dd.GetSheetNames())
        {
            dd.ActivateSheet(sheet);
            for (var v = (View)dd.GetFirstView(); v != null; v = (View)v.GetNextView())
                foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                {
                    var p = an.GetPosition() as double[];
                    string val = "";
                    if (an.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                    {
                        var dm = ((DisplayDimension)an.GetSpecificAnnotation()).GetDimension2(0) as Dimension;
                        if (dm != null) val = dm.FullName + "=" + dm.GetSystemValue2("").ToString("R", Inv);
                    }
                    if (an.IsDangling()) dangling++;
                    lines.Add(sheet + "|" + v.Name + "|" + TypeOf(an) + "|" + an.GetName() + "|" +
                        (p == null ? "-" : string.Join(",", p.Select(x => Math.Round(x * 1000, 4).ToString(Inv)))) + "|" + val);
                }
        }
        return lines;
    }

    static int Main(string[] a)
    {
        var args = a.ToList();
        bool save = args.Remove("--save"), checkedOut = args.Remove("--checked-out");
        bool look = args.Remove("--look");   // change nothing (for a --pdf of the drawing as it is); never saves
        // --delete <name,name> (Grant 21 Nov: stub centre lines James had hidden in white): delete exactly these annotations
        // (by name, e.g. DetailItem2242); the before/after check then allows only these to be missing.
        var del = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int di = args.IndexOf("--delete");
        if (di >= 0) { foreach (var n in args[di + 1].Split(',')) del.Add(n.Trim()); args.RemoveRange(di, 2); }
        if (look) save = false;
        // --pdf <folder> (Grant 21 Nov): in the same open, after the fix, save all sheets as <folder>\<drawing>.pdf with
        // SOLIDWORKS' own PDF export (no recolouring), so a script can look for white or coloured text in what prints.
        string pdfDir = null;
        int pi = args.IndexOf("--pdf");
        if (pi >= 0) { pdfDir = Path.GetFullPath(args[pi + 1]); args.RemoveRange(pi, 2); Directory.CreateDirectory(pdfDir); }
        if (args.Count == 0) { Console.Error.WriteLine("Usage: SwAnnFix <drawing> [...] [--save] [--checked-out]"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int bad = 0;
        foreach (var arg in args)
        {
            string file = Path.GetFullPath(arg);
            bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase);
            if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.IndexOf(@"\.pdm\", StringComparison.OrdinalIgnoreCase) >= 0)
            { Console.WriteLine("RESULT\t" + file + "\tFAIL refused"); bad++; continue; }
            if (save && (vault ? (!checkedOut || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0) : !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase)))
            { Console.WriteLine("RESULT\t" + file + "\tFAIL refused: not checked out (vault) or not scratch"); bad++; continue; }
            if (sw.GetOpenDocumentByName(file) != null) { Console.WriteLine("RESULT\t" + file + "\tFAIL already open; Close All first"); bad++; continue; }
            int e = 0, w = 0;
            var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
            if (doc == null) { Console.WriteLine("RESULT\t" + file + "\tFAIL could not open, errors " + e); bad++; continue; }
            try
            {
                var dd = (DrawingDoc)doc;
                string active = ((Sheet)dd.GetCurrentSheet()).GetName();
                int dBefore, dAfter;
                var before = Snapshot(dd, out dBefore);
                int changed = 0, flagged = 0;
                if (del.Count > 0)
                {
                    var found = new List<Annotation>();
                    foreach (string sheet in (string[])dd.GetSheetNames())
                    {
                        dd.ActivateSheet(sheet);
                        for (var v = (View)dd.GetFirstView(); v != null; v = (View)v.GetNextView())
                            foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                                if (del.Contains(an.GetName()))
                                {
                                    Console.WriteLine("DELETE\t" + file + "\t" + sheet + "\t" + v.Name + "\t" + TypeOf(an) + "\t" + an.GetName());
                                    doc.ClearSelection2(true);
                                    an.Select3(false, null);
                                    doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                                    found.Add(an);
                                }
                    }
                    if (found.Count != del.Count) Console.WriteLine("DELETE\t" + file + "\tfound " + found.Count + " of " + del.Count);
                    before = before.Where(l => !del.Contains(l.Split('|')[3])).ToList();
                    changed += found.Count;
                    if (found.Count != del.Count) bad++;
                }
                foreach (string sheet in (string[])dd.GetSheetNames())
                {
                    dd.ActivateSheet(sheet);
                    for (var v = (View)dd.GetFirstView(); v != null; v = (View)v.GetNextView())
                        foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                        {
                            if (look || (an.LayerOverride & Over) == 0) continue;
                            int c = an.Color & 0xFFFFFF;
                            if (c == 0xFFFFFF || c == 0)
                            {
                                an.LayerOverride = an.LayerOverride & ~Over;
                                if ((an.LayerOverride & Over) != 0) { Console.WriteLine("STUCK\t" + file + "\t" + sheet + "\t" + v.Name + "\t" + TypeOf(an) + "\t" + an.GetName()); bad++; }
                                else
                                {
                                    changed++;
                                    var ip = an.GetPosition() as double[];
                                    Console.WriteLine("ITEM\t" + file + "\t" + sheet + "\t" + v.Name + "\t" + TypeOf(an) + "\t" + an.GetName() + "\twas " + (c == 0 ? "black" : "white") +
                                        (ip == null ? "" : "\tat " + Math.Round(ip[0] * 1000, 1).ToString(Inv) + ", " + Math.Round(ip[1] * 1000, 1).ToString(Inv) + " mm"));
                                }
                            }
                            else
                            {
                                flagged++;
                                Console.WriteLine("FLAG\t" + file + "\t" + sheet + "\t" + v.Name + "\t" + TypeOf(an) + "\t" + an.GetName() + "\t" + Rgb(c) + "\t(left as it is)");
                            }
                        }
                }
                var after = Snapshot(dd, out dAfter);
                var why = new List<string>();
                if (before.Count != after.Count) why.Add("annotations " + before.Count + " -> " + after.Count);
                else if (!before.SequenceEqual(after))
                {
                    int i = Enumerable.Range(0, before.Count).First(k => before[k] != after[k]);
                    why.Add("changed: " + before[i] + " -> " + after[i]);
                }
                if (dAfter != dBefore) why.Add("dangling " + dBefore + " -> " + dAfter);
                dd.ActivateSheet(active);
                string tail = "\tannotations " + before.Count + "\tdangling " + dBefore + "\tflagged " + flagged;
                if (why.Count > 0) { Console.WriteLine("RESULT\t" + file + "\tFAIL " + string.Join("; ", why) + tail); bad++; }
                else if (changed == 0) Console.WriteLine("RESULT\t" + file + "\tNOCHANGE" + tail);
                else if (!save) Console.WriteLine("RESULT\t" + file + "\tCHANGED " + changed + " (not saved)" + tail);
                else
                {
                    int se = 0, sw2 = 0;
                    bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                    Console.WriteLine("RESULT\t" + file + "\t" + (ok ? "CHANGED " + changed + " SAVED" : "FAIL save error " + se) + tail);
                    if (!ok) bad++;
                }
                if (pdfDir != null)
                {
                    try
                    {
                        // Note (21 Nov, GNRIC-BRP-11): a PDF made in the same open as a change still drew some of the old
                        // white (redraw and rebuild did not help); a fresh open prints right. So after a change, make the
                        // PDF from a fresh --look open (sw\white\step.py does that).
                        string pdf = Path.Combine(pdfDir, Path.GetFileNameWithoutExtension(file) + ".pdf");
                        if (File.Exists(pdf)) File.Delete(pdf);
                        var exd = (ExportPdfData)sw.GetExportFileData((int)swExportDataFileType_e.swExportPdfData);
                        exd.SetSheets((int)swExportDataSheetsToExport_e.swExportData_ExportAllSheets, null);
                        int pe = 0, pw = 0;
                        bool pok = doc.Extension.SaveAs3(pdf, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent, exd, null, ref pe, ref pw);
                        Console.WriteLine("PDF\t" + file + "\t" + (pok && File.Exists(pdf) ? pdf : "FAILED " + pe));
                    }
                    catch (Exception ex2) { Console.WriteLine("PDF\t" + file + "\tFAILED " + ex2.Message.Replace('\n', ' ')); }
                }
            }
            catch (Exception ex) { Console.WriteLine("RESULT\t" + file + "\tFAIL " + ex.Message.Replace('\n', ' ')); bad++; }
            finally { sw.CloseDoc(doc.GetTitle()); }
        }
        return bad == 0 ? 0 : 1;
    }
}
