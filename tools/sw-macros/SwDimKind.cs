// SwDimKind: read-only list of a drawing's dimensions with where each comes from (model or drawing), driving or driven,
// and its colour (Grant, 21 Nov 2026: why some show grey and some black). Uses the drawing if already open, else opens
// it read-only; never saves.
// Usage: SwDimKind.exe <drawing>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwDimKind.exe SwDimKind.cs

using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwDimKind
{
    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]);
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        var doc = sw.GetOpenDocumentByName(file) as ModelDoc2;
        bool opened = false;
        int e = 0, w = 0;
        if (doc == null) { doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocDRAWING, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w); opened = true; }
        var dd = (DrawingDoc)doc;
        // Design-table headers of every open part/assembly, so a dimension can be marked "in design table"
        var dtHeaders = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (ModelDoc2 m in (object[])sw.GetDocuments() ?? new object[0])
        {
            if (m.GetType() == (int)swDocumentTypes_e.swDocDRAWING) continue;
            var dt = m.GetDesignTable() as DesignTable;
            if (dt == null) continue;
            Console.WriteLine("DESIGNTABLE\t" + m.GetTitle());
            if (!dt.Attach()) continue;
            try
            {
                string title = Path.GetFileNameWithoutExtension(m.GetTitle());
                for (int c = 1; c <= dt.GetTotalColumnCount(); c++)
                {
                    string h = dt.GetHeaderText(c) ?? "";
                    if (h != "") { dtHeaders.Add(h); Console.WriteLine("HEADER\t" + title + "\t" + h); }
                }
            }
            finally { dt.Detach(); }
        }
        foreach (string sheet in (string[])dd.GetSheetNames())
        {
            for (var v = (View)((View)dd.GetFirstView()); v != null; v = (View)v.GetNextView())
                for (var d = (DisplayDimension)v.GetFirstDisplayDimension5(); d != null; d = (DisplayDimension)d.GetNext5())
                {
                    var dm = (Dimension)d.GetDimension2(0);
                    var an = (Annotation)d.GetAnnotation();
                    string owner = dm.FullName.Substring(dm.FullName.LastIndexOf('@') + 1);
                    Console.WriteLine(v.Name + "\t" + dm.FullName + "\t" + Math.Round(dm.Value, 3) +
                        "\tfrom " + (owner.EndsWith(".Drawing", StringComparison.OrdinalIgnoreCase) ? "DRAWING" : "MODEL") +
                        "\t" + (dm.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriven ? "driven" : dm.DrivenState == (int)swDimensionDrivenState_e.swDimensionDriving ? "driving" : "state " + dm.DrivenState) +
                        "\treference " + dm.IsReference() + "\tcolour override " + ((an.LayerOverride & 1) != 0) + "\thole callout " + d.IsHoleCallout() +
                        "\tin design table " + (dtHeaders.Contains(dm.FullName.Substring(0, dm.FullName.LastIndexOf('@'))) || dtHeaders.Contains("$STATE@" + dm.Name)));
                }
        }
        if (opened) sw.CloseDoc(doc.GetTitle());
        return 0;
    }
}
