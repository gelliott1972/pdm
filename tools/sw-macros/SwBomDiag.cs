// SwBomDiag: read-only look at a drawing's BOM tables: table type and configuration, what each column reads
// (column type, custom property, "use title as part number"), and per row the component(s), their referenced
// configuration and that configuration's BOM part-number source. Opens the drawing read-only; never saves.
// --hold <part>: open that part read-only first, so the drawing uses it (a rehearsal copy with the same file name).
// Usage: SwBomDiag.exe <drawing> [--hold <part>]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwBomDiag.exe SwBomDiag.cs

using System;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwBomDiag
{
    static int Main(string[] a)
    {
        var args = a.ToList();
        string hold = null;
        int hi = args.IndexOf("--hold");
        if (hi >= 0) { hold = args[hi + 1]; args.RemoveRange(hi, 2); }
        if (args.Count != 1) { Console.Error.WriteLine("Usage: SwBomDiag <drawing> [--hold <part>]"); return 2; }
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        ModelDoc2 held = null;
        if (hold != null)
        {
            held = sw.OpenDoc6(hold, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
            Console.WriteLine("HOLD\t" + hold + "\t" + (held != null ? "open" : "failed " + e));
        }
        var doc = sw.OpenDoc6(args[0], (int)swDocumentTypes_e.swDocDRAWING, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open, errors " + e); return 3; }
        try
        {
            doc.ForceRebuild3(false);
            for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
            {
                if (f.GetTypeName2() != "BomFeat") continue;
                var bf = (BomFeature)f.GetSpecificFeature2();
                object vis = null;
                var cfgs = (string[])bf.GetConfigurations(true, ref vis) ?? new string[0];
                Console.WriteLine("BOM\t" + f.Name + "\ttype " + (swBomType_e)bf.TableType + "\tconfigs " + string.Join(",", cfgs) + "\tdisplay " + bf.DisplayAsOneItem + "\tpart-config grouping " + bf.PartConfigurationGrouping + "\tref model " + bf.GetReferencedModelName());
                foreach (BomTableAnnotation bt in (object[])bf.GetTableAnnotations())
                {
                    var ta = (TableAnnotation)bt;
                    // All columns, hidden ones included (Draw, 18 Nov). Tried on 08BXX-MSA-01.SLDDRW: the last argument true
                    // indexes all columns (hidden included), false only the shown ones; ColumnCount counts shown columns only.
                    for (int c = 0; c < ta.TotalColumnCount; c++)
                        Console.WriteLine("COLUMN\t" + c + "\t" + ta.GetColumnTitle2(c, true) + "\t" + (ta.ColumnHidden[c] ? "HIDDEN" : "shown") + "\ttype " + (swTableColumnTypes_e)ta.GetColumnType2(c, true));
                    foreach (var cfg in cfgs)
                        for (int r = 0; r < ta.RowCount; r++)
                        {
                            string text = string.Join(" | ", Enumerable.Range(0, ta.TotalColumnCount).Select(c => (ta.ColumnHidden[c] ? "(hidden) " : "") + ta.Text2[r, c, true]));
                            string itemNo, partNo;
                            bt.GetComponentsCount2(r, cfg, out itemNo, out partNo);
                            var comps = (object[])bt.GetComponents2(r, cfg);
                            string cs = comps == null ? "" : string.Join("; ", comps.Cast<Component2>().Select(k =>
                            {
                                var m = (ModelDoc2)k.GetModelDoc2();
                                string src = "";
                                if (m != null)
                                {
                                    var conf = (Configuration)m.GetConfigurationByName(k.ReferencedConfiguration);
                                    if (conf != null) src = "BOMPartNoSource " + (swBOMPartNumberSource_e)conf.BOMPartNoSource + ", AlternateName '" + conf.AlternateName + "', UseAlternateNameInBOM " + conf.UseAlternateNameInBOM;
                                }
                                return k.Name2 + " [" + k.ReferencedConfiguration + "] " + src;
                            }));
                            Console.WriteLine("ROW\t" + cfg + "\t" + r + "\titem " + itemNo + "\tpartno " + partNo + "\t" + text + "\t" + cs);
                        }
                }
            }
        }
        finally
        {
            sw.CloseDoc(doc.GetTitle());
            if (held != null) sw.CloseDoc(held.GetTitle());
        }
        return 0;
    }
}
