// SwPickAt: read-only: what is at a point on a drawing sheet (Grant, 21 Nov 2026: white strokes left in a PDF). Opens the
// drawing read-only, selects at each sheet point (mm) and prints the selected object's type and, where it has them, its
// layer, colour and owner. Never saves.
// Usage: SwPickAt.exe <drawing> <x_mm,y_mm> [<x_mm,y_mm> ...]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwPickAt.exe SwPickAt.cs

using System;
using System.Globalization;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwPickAt
{
    static string Rgb(int c) { return string.Format("{0},{1},{2}", c & 255, (c >> 8) & 255, (c >> 16) & 255); }

    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]);
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open " + e); return 3; }
        try
        {
            sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref e);
            var sm = (SelectionMgr)doc.SelectionManager;
            for (int i = 1; i < a.Length; i++)
            {
                var xy = a[i].Split(',');
                double x = double.Parse(xy[0], CultureInfo.InvariantCulture) / 1000, y = double.Parse(xy[1], CultureInfo.InvariantCulture) / 1000;
                doc.ClearSelection2(true);
                bool ok = doc.Extension.SelectByID2("", "", x, y, 0, false, 0, null, 0);
                Console.WriteLine("AT\t" + a[i] + " mm\tselected " + ok + ", " + sm.GetSelectedObjectCount2(-1));
                for (int k = 1; k <= sm.GetSelectedObjectCount2(-1); k++)
                {
                    int t = sm.GetSelectedObjectType3(k, -1);
                    object o = sm.GetSelectedObject6(k, -1);
                    string info = "";
                    var ss = o as SketchSegment;
                    if (ss != null) info = "sketch " + (swSketchSegments_e)ss.GetType() + ", layer '" + ss.Layer + "', colour " + Rgb(ss.Color) + ", construction " + ss.ConstructionGeometry + ", length " + Math.Round(ss.GetLength() * 1000, 2) + " mm, sketch " + ((Feature)((Sketch)ss.GetSketch())).Name;
                    var an = o as Annotation;
                    if (an == null) { try { var ga = o.GetType().InvokeMember("GetAnnotation", System.Reflection.BindingFlags.InvokeMethod, null, o, null) as Annotation; an = ga; } catch { } }
                    if (an != null) info += " annotation " + (swAnnotationType_e)an.GetType() + " '" + an.GetName() + "', layer '" + an.Layer + "', colour " + Rgb(an.Color) + ", override " + ((an.LayerOverride & 1) != 0);
                    var ed = o as Edge;
                    if (ed != null) info += " model edge";
                    Console.WriteLine("  " + (swSelectType_e)t + "\t" + info);
                }
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }
}
