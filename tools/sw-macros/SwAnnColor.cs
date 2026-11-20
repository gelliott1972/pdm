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
        if (args.Length == 0) { Console.Error.WriteLine("Usage: SwAnnColor <drawing> [<drawing> ...]"); return 2; }
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
            var doc = open ?? sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING,
                (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
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
                int total = 0, white = 0;
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
                        }
                    foreach (var kv in counts.OrderBy(k => k.Key)) Console.WriteLine("ANN\t" + file + "\t" + kv.Key + "\t" + kv.Value);
                }
                Console.WriteLine("SUMMARY\t" + file + "\tannotations " + total + "\twhite " + white);
            }
            finally { if (open == null) sw.CloseDoc(doc.GetTitle()); }
        }
        return 0;
    }
}
