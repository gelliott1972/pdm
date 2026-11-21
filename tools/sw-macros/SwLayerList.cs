// SwLayerList: read-only list of everything on one layer of an OPEN drawing (Grant, 21 Nov 2026: pink items on the DRAFT
// layer of signed-off drawings): sketch segments and points in every view's sketch (the sheet's included), block instances,
// and annotations. Never saves.
// Usage: SwLayerList.exe <drawing (open)> <layer>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwLayerList.exe SwLayerList.cs

using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwLayerList
{
    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]), layer = a[1];
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        var doc = sw.GetOpenDocumentByName(file) as ModelDoc2;
        if (doc == null) { Console.Error.WriteLine("not open: " + file); return 3; }
        var dd = (DrawingDoc)doc;
        int n = 0;
        foreach (string sheet in (string[])dd.GetSheetNames())
            for (var v = (View)dd.GetFirstView(); v != null; v = (View)v.GetNextView())
            {
                var sk = v.GetSketch() as Sketch;
                if (sk != null)
                {
                    foreach (SketchSegment s in (object[])sk.GetSketchSegments() ?? new object[0])
                        if (string.Equals(s.Layer, layer, StringComparison.OrdinalIgnoreCase))
                        { n++; Console.WriteLine("SEGMENT\t" + sheet + "\t" + v.Name + "\t" + (swSketchSegments_e)s.GetType() + "\tlength " + Math.Round(s.GetLength() * 1000, 1) + " mm\tconstruction " + s.ConstructionGeometry); }
                    foreach (SketchPoint p in (object[])sk.GetSketchPoints2() ?? new object[0])
                        if (string.Equals(p.Layer, layer, StringComparison.OrdinalIgnoreCase))
                        { n++; Console.WriteLine("POINT\t" + sheet + "\t" + v.Name + "\tat " + Math.Round(p.X * 1000, 1) + ", " + Math.Round(p.Y * 1000, 1)); }
                }
                foreach (Annotation an in (object[])v.GetAnnotations() ?? new object[0])
                    if (string.Equals(an.Layer, layer, StringComparison.OrdinalIgnoreCase))
                    { n++; var p = an.GetPosition() as double[]; Console.WriteLine("ANNOTATION\t" + sheet + "\t" + v.Name + "\t" + (swAnnotationType_e)an.GetType() + "\t" + an.GetName() + (p == null ? "" : "\tat " + Math.Round(p[0] * 1000, 1) + ", " + Math.Round(p[1] * 1000, 1))); }
            }
        foreach (SketchBlockDefinition bd in (object[])doc.SketchManager.GetSketchBlockDefinitions() ?? new object[0])
            foreach (SketchBlockInstance bi in (object[])bd.GetInstances() ?? new object[0])
                if (string.Equals(bi.Layer, layer, StringComparison.OrdinalIgnoreCase))
                { n++; Console.WriteLine("BLOCK\t" + bi.Name); }
        Console.WriteLine("TOTAL\t" + n + " items on " + layer);
        return 0;
    }
}
