// SwView: sets up a READ-ONLY view for Grant to look at in the running SOLIDWORKS 2026 and leaves it open (28 Nov 2026,
// Desk A33, LS-08A-07 doubled tube). Opens <part> (optional, read-only, in its configuration) and then <asm> read-only in
// <config>, makes <transparent comp> transparent, selects <select comp> (tree and graphics), shows the isometric view zoomed
// to the two components, and brings the assembly window to the front. Never saves. Changes only the in-memory display.
// Usage: SwView.exe <asm> <config> <select comp> <transparent comp> [<part> <part config>]
// Build: csc /platform:x64 /r:binSolidWorks.Interop.sldworks.dll /r:binSolidWorks.Interop.swconst.dll /out:binSwView.exe SwView.cs
using System;
using System.IO;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwView
{
    static int Main(string[] a)
    {
        if (a.Length < 4) { Console.Error.WriteLine("Usage: SwView <asm> <config> <select comp> <transparent comp> [<part> <part config>]"); return 2; }
        foreach (var f in new[] { a[0], a.Length > 4 ? a[4] : a[0] })
            if (f.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || f.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || f.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0)
            { Console.Error.WriteLine("REFUSED: never Dropbox or .pdm"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        sw.Visible = true;
        sw.UserControl = true;
        int e = 0, w = 0;
        int ro = (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly);
        if (a.Length > 5)
        {
            var pd = sw.OpenDoc6(Path.GetFullPath(a[4]), (int)swDocumentTypes_e.swDocPART, ro, a[5], ref e, ref w);
            if (pd == null) { Console.WriteLine("FAILED\topen part " + e); return 3; }
            pd.ShowConfiguration2(a[5]);
            pd.ShowNamedView2("*Isometric", -1); pd.ViewZoomtofit2();
            Console.WriteLine("OPEN\t" + pd.GetTitle() + "\tconfiguration " + ((Configuration)pd.GetActiveConfiguration()).Name + "\tread-only " + pd.IsOpenedReadOnly());
        }
        var doc = sw.OpenDoc6(Path.GetFullPath(a[0]), (int)swDocumentTypes_e.swDocASSEMBLY, ro | (int)swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight, a[1], ref e, ref w);
        if (doc == null) { Console.WriteLine("FAILED\topen assembly " + e); return 3; }
        sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref e);
        doc.ShowConfiguration2(a[1]);
        var asm = (AssemblyDoc)doc;
        asm.ResolveAllLightWeightComponents(false);
        doc.ForceRebuild3(false);
        var sel = (Component2)asm.GetComponentByName(a[2]);
        var tr = (Component2)asm.GetComponentByName(a[3]);
        if (sel == null || tr == null) { Console.WriteLine("FAILED\tcomponent not found"); return 3; }
        doc.ClearSelection2(true);
        tr.Select4(false, null, false);
        asm.SetComponentTransparent(true);
        doc.ClearSelection2(true);
        var b1 = (double[])sel.GetBox(false, false); var b2 = (double[])tr.GetBox(false, false);
        doc.ShowNamedView2("*Isometric", -1);
        double m = 0.25;
        doc.ViewZoomTo2(b1[0] - m, b1[1] - m, b1[2] - m, b1[3] + m, b1[4] + m, b1[5] + m);
        sel.Select4(false, null, false);
        doc.GraphicsRedraw2();
        Console.WriteLine("OPEN\t" + doc.GetTitle() + "\tconfiguration " + ((Configuration)doc.GetActiveConfiguration()).Name + "\tread-only " + doc.IsOpenedReadOnly() + "\tselected " + sel.Name2 + "\ttransparent " + tr.Name2);
        return 0;
    }
}
