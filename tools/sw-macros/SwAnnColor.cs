// SwAnnColor: read-only look at annotation colours in drawings (Grant, 20 Nov 2026: balloons and dimensions show WHITE
// on Grant's PC, black on James's). For each drawing: its layers (name, colour, visible), and every annotation on every
// sheet (sheet format included) counted by type, layer, its own colour and whether it overrides the layer colour, with
// the colour it should show ("effective": the layer's unless it has no layer or overrides the colour).
// Opens each drawing read-only, never saves, closes what it opened. Run under SwCmd's lock (--as sw).
// Usage: SwAnnColor.exe <drawing> [<drawing> ...]
// Output: LAYER drawing name RGB visible | ANN drawing sheet type layer own-RGB override effective-RGB count | SUMMARY drawing annotations white
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwAnnColor.exe SwAnnColor.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwAnnColor
{
    static string Rgb(int c) { return c < 0 ? "default(" + c + ")" : string.Format("{0},{1},{2}", c & 255, (c >> 8) & 255, (c >> 16) & 255); }

    static int Main(string[] args)
    {
        // --fix (Grant 20 Nov): on balloons and display dimensions whose own colour is white with a colour override, remove the
        // override (back to the default colour). Nothing else is touched; every annotation's position is compared before and
        // after. Saves only with --save; a vault drawing needs --checked-out and must be writable.
        var list = args.ToList();
        bool fix = list.Remove("--fix"), save = list.Remove("--save"), checkedOut = list.Remove("--checked-out");
        args = list.ToArray();
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwAnnColor <drawing> [<drawing> ...] [--fix [--save] [--checked-out]]"); return 2; }
        if (save && !fix) { Console.Error.WriteLine("--save needs --fix"); return 2; }
        int bad = 0;
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        Console.WriteLine("SYSTEM\ttext " + Rgb(sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSystemColorsText)) +
            "\timported driving " + Rgb(sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSystemColorsImportedDrivingAnnotation)) +
            "\timported driven " + Rgb(sw.GetUserPreferenceIntegerValue((int)swUserPreferenceIntegerValue_e.swSystemColorsImportedDrivenAnnotation)));
        foreach (var a in args)
        {
            string file = Path.GetFullPath(a);
            var open = sw.GetOpenDocumentByName(file) as ModelDoc2;
            int e = 0, w = 0;
            if (save)
            {
                bool vault = file.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase);
                if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || (vault && (!checkedOut || (File.GetAttributes(file) & FileAttributes.ReadOnly) != 0))
                    || (!vault && !file.StartsWith(@"C:\code\pdm\scratch\", StringComparison.OrdinalIgnoreCase)))
                { Console.WriteLine("REFUSED\t" + file + "\ta vault drawing only when checked out (--checked-out, writable), else scratch only"); bad++; continue; }
            }
            if (fix && open != null) { Console.WriteLine("REFUSED\t" + file + "\talready open; Close All first"); bad++; continue; }
            var doc = open ?? sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING,
                (int)swOpenDocOptions_e.swOpenDocOptions_Silent | (save ? 0 : (int)swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
            if (doc == null) { Console.WriteLine("ERROR\t" + file + "\tcould not open, errors " + e); continue; }
            try
            {
                var lm = (LayerMgr)doc.GetLayerManager();
                var layers = new Dictionary<string, Layer>(StringComparer.OrdinalIgnoreCase);
                foreach (string ln in (string[])lm.GetLayerList() ?? new string[0])
                {
                    var l = (Layer)lm.GetLayer(ln);
                    if (l == null) continue;
                    layers[ln] = l;
                    Console.WriteLine("LAYER\t" + file + "\t" + ln + "\t" + Rgb(l.Color) + "\t" + (l.Visible ? "visible" : "hidden"));
                }
                var dd = (DrawingDoc)doc;
                int total = 0, white = 0, fixedN = 0;
                var posBefore = new List<string>(); var posAfter = new List<string>();
                foreach (string sheet in (string[])dd.GetSheetNames())
                {
                    dd.ActivateSheet(sheet);
                    var counts = new Dictionary<string, int>();
                    for (var v = (View)dd.GetFirstView(); v != null; v = (View)v.GetNextView())   // the first is the sheet itself
                        foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                        {
                            string ln = an.Layer ?? "";
                            bool over = (an.LayerOverride & (int)swLayerOverride_e.swLayerOverrideColor) != 0;
                            int own = an.Color;
                            Layer l;
                            int eff = (ln == "" || over || !layers.TryGetValue(ln, out l)) ? own : l.Color;
                            string type = ((swAnnotationType_e)an.GetType()).ToString();
                            if (an.GetType() == (int)swAnnotationType_e.swNote)
                            {
                                var nt = an.GetSpecificAnnotation() as Note;
                                if (nt != null && nt.GetBalloonStyle() != (int)swBalloonStyle_e.swBS_None) type += "(balloon)";
                            }
                            string key = sheet + "\t" + type + "\t" + (ln == "" ? "(none)" : ln) + "\t" + Rgb(own) + "\t" + (over ? "colour override" : "-") + "\t" + Rgb(eff);
                            int n;
                            counts[key] = counts.TryGetValue(key, out n) ? n + 1 : 1;
                            total++;
                            if (eff == 0xFFFFFF) white++;
                            if (fix)
                            {
                                posBefore.Add(sheet + " " + v.Name + " " + type + " " + Pos(an));
                                if ((type == "swNote(balloon)" || type == "swDisplayDimension") && over && own == 0xFFFFFF)
                                {
                                    an.LayerOverride = an.LayerOverride & ~(int)swLayerOverride_e.swLayerOverrideColor;
                                    fixedN++;
                                    bool still = (an.LayerOverride & (int)swLayerOverride_e.swLayerOverrideColor) != 0;
                                    Console.WriteLine("FIXED\t" + file + "\t" + sheet + "\t" + v.Name + "\t" + type + "\t" + an.GetName() + "\tnow own " + Rgb(an.Color) + ", override " + (still ? "STILL ON" : "off"));
                                    if (still) bad++;
                                }
                                posAfter.Add(sheet + " " + v.Name + " " + type + " " + Pos(an));
                            }
                        }
                    foreach (var kv in counts.OrderBy(k => k.Key)) Console.WriteLine("ANN\t" + file + "\t" + kv.Key + "\t" + kv.Value);
                }
                Console.WriteLine("SUMMARY\t" + file + "\tannotations " + total + "\twhite " + white + (fix ? "\tfixed " + fixedN : ""));
                if (fix)
                {
                    bool same = posBefore.SequenceEqual(posAfter);
                    Console.WriteLine("POSITIONS\t" + file + "\t" + (same ? "all " + posBefore.Count + " unchanged" : "CHANGED"));
                    if (!same) bad++;
                    if (save && fixedN > 0 && same && bad == 0)
                    {
                        int se = 0, sw2 = 0;
                        bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                        Console.WriteLine(ok ? "SAVED\t" + file : "SAVE FAILED\t" + file + "\t" + se);
                        if (!ok) bad++;
                    }
                    else if (save) Console.WriteLine("NOT SAVED\t" + file + "\t" + (fixedN == 0 ? "nothing to fix" : "a check failed"));
                }
            }
            finally { if (open == null) sw.CloseDoc(doc.GetTitle()); }
        }
        return bad == 0 ? 0 : 1;
    }

    static string Pos(Annotation an)
    {
        var p = an.GetPosition() as double[];
        return p == null ? "-" : string.Join(",", p.Select(x => Math.Round(x * 1000, 4).ToString(System.Globalization.CultureInfo.InvariantCulture)));
    }
}
