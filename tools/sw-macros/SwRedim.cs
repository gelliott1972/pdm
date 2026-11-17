// SwRedim: test on a SCRATCH copy of a drawing whether dimensions left dangling by a profile repoint can be put back
// (Admin overnight 17-18 Nov, item 4: the 27 parts). For each view with dangling dimensions: note each one's name and
// position, delete it, insert the model's dimensions into that view only (marked and not marked for drawing), keep the
// new ones with the same names and move them to the old positions, and delete every other dimension the insert added.
// --hold <part>: the repointed part copy, opened read-only first so the drawing uses it.
// Prints DELETED / KEPT / EXTRA / MISSING lines. Saves the drawing copy only with --save. Refuses anything outside
// C:\code\pdm\scratch\.
// Usage: SwRedim.exe <drawing copy> --hold <part copy> [--save]
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwRedim.exe SwRedim.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwRedim
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double v) { return v.ToString("0.####", Inv); }

    // What a dimension looks like, copied from the dangling original onto its replacement (the first run on 17-18 Nov
    // showed the re-inserted ones take default precision: 6.3 printed as 6, 40 as 40.0).
    class Lost
    {
        public string Name, FullName; public double[] Pos;
        public bool DocPrecision; public int P, A, PT, AT;
        public string[] Text = new string[9];
        public int ArrowSide, WitnessVis, LeaderVis; public bool CenterText, OffsetText, SmartWitness, BrokenLeader;
    }

    static Lost Read(DisplayDimension d, Annotation an)
    {
        var dm = (Dimension)d.GetDimension2(0);
        var l = new Lost
        {
            Name = dm.Name, FullName = dm.FullName, Pos = (double[])an.GetPosition(),
            DocPrecision = d.GetUseDocPrecision(), P = d.GetPrimaryPrecision2(), A = d.GetAlternatePrecision2(),
            PT = d.GetPrimaryTolPrecision2(), AT = d.GetAlternateTolPrecision2(),
            ArrowSide = d.ArrowSide, WitnessVis = d.WitnessVisibility, LeaderVis = d.LeaderVisibility,
            CenterText = d.CenterText, OffsetText = d.OffsetText, SmartWitness = d.SmartWitness, BrokenLeader = d.BrokenLeader
        };
        for (int i = 1; i <= 4; i++) l.Text[i] = d.GetText(i);
        return l;
    }

    static string Apply(DisplayDimension d, Annotation an, Lost l)
    {
        an.SetPosition2(l.Pos[0], l.Pos[1], l.Pos.Length > 2 ? l.Pos[2] : 0);
        const int Doc = (int)swDimensionPrecisionSettings_e.swPrecisionFollowsDocumentSetting;
        if (l.DocPrecision) d.SetPrecision3(Doc, Doc, Doc, Doc); else d.SetPrecision3(l.P, l.A, l.PT, l.AT);
        for (int i = 1; i <= 4; i++) if (d.GetText(i) != l.Text[i]) d.SetText(i, l.Text[i]);
        d.ArrowSide = l.ArrowSide; d.WitnessVisibility = l.WitnessVis; d.LeaderVisibility = l.LeaderVis;
        d.CenterText = l.CenterText; d.OffsetText = l.OffsetText; d.SmartWitness = l.SmartWitness; d.BrokenLeader = l.BrokenLeader;
        return "precision " + (l.DocPrecision ? "document" : l.P.ToString()) + ", now " + d.GetPrimaryPrecision2();
    }

    static int Main(string[] a)
    {
        var args = a.ToList();
        bool save = args.Remove("--save");
        string hold = null;
        int hi = args.IndexOf("--hold");
        if (hi >= 0) { hold = args[hi + 1]; args.RemoveRange(hi, 2); }
        if (args.Count != 1 || hold == null) { Console.Error.WriteLine("Usage: SwRedim <drawing copy> --hold <part copy> [--save]"); return 2; }
        string drw = Path.GetFullPath(args[0]);
        const string Scratch = @"C:\code\pdm\scratch\";
        if (!drw.StartsWith(Scratch, StringComparison.OrdinalIgnoreCase) || !Path.GetFullPath(hold).StartsWith(Scratch, StringComparison.OrdinalIgnoreCase))
        { Console.Error.WriteLine("refused: scratch copies only"); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var held = sw.OpenDoc6(hold, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (held == null) { Console.Error.WriteLine("could not open the part copy, errors " + e); return 3; }
        var doc = sw.OpenDoc6(drw, (int)swDocumentTypes_e.swDocDRAWING, (int)swOpenDocOptions_e.swOpenDocOptions_Silent, "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open the drawing copy, errors " + e); sw.CloseDoc(held.GetTitle()); return 3; }
        int missing = 0;
        try
        {
            sw.ActivateDoc3(doc.GetTitle(), false, (int)swRebuildOnActivation_e.swDontRebuildActiveDoc, ref e);
            doc.ForceRebuild3(false);
            var dd = (DrawingDoc)doc;
            foreach (string sheet in (string[])dd.GetSheetNames())
            {
                dd.ActivateSheet(sheet);
                var views = ((object[])((Sheet)dd.GetCurrentSheet()).GetViews() ?? new object[0]).Cast<View>().ToList();
                foreach (var v in views)
                {
                    var lost = new List<Lost>();
                    var dangling = new List<Annotation>();
                    for (var d = (DisplayDimension)v.GetFirstDisplayDimension5(); d != null; d = (DisplayDimension)d.GetNext5())
                    {
                        var an = (Annotation)d.GetAnnotation();
                        if (!an.IsDangling()) continue;
                        lost.Add(Read(d, an));
                        dangling.Add(an);
                    }
                    if (lost.Count == 0) continue;
                    var model = v.ReferencedDocument;
                    Console.WriteLine("VIEW\t" + sheet + "\t" + v.Name + "\tmodel " + (model == null ? "?" : model.GetTitle()) + " [" + v.ReferencedConfiguration + "]\t" + lost.Count + " dangling");
                    doc.ClearSelection2(true);
                    foreach (var an in dangling) an.Select3(true, null);
                    doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                    foreach (var l in lost) Console.WriteLine("DELETED\t" + v.Name + "\t" + l.FullName + "\tat " + F(l.Pos[0] * 1000) + ", " + F(l.Pos[1] * 1000));

                    doc.ClearSelection2(true);
                    doc.Extension.SelectByID2(v.Name, "DRAWINGVIEW", 0, 0, 0, false, 0, null, 0);
                    var added = (object[])dd.InsertModelAnnotations3((int)swImportModelItemsSource_e.swImportModelItemsFromEntireModel,
                        (int)(swInsertAnnotation_e.swInsertDimensionsMarkedForDrawing | swInsertAnnotation_e.swInsertDimensionsNotMarkedForDrawing),
                        false, true, false, false) ?? new object[0];
                    var extra = new List<Annotation>();
                    var want = lost.ToList();
                    foreach (Annotation an in added)
                    {
                        var d = an.GetType() == (int)swAnnotationType_e.swDisplayDimension ? (DisplayDimension)an.GetSpecificAnnotation() : null;
                        var dm = d == null ? null : (Dimension)d.GetDimension2(0);
                        var match = dm == null ? null : want.FirstOrDefault(l => l.Name == dm.Name);
                        if (match == null) { extra.Add(an); continue; }
                        want.Remove(match);
                        string how = Apply(d, an, match);
                        Console.WriteLine("KEPT\t" + v.Name + "\t" + dm.FullName + "\tvalue " + F(dm.Value) + "\t" + how);
                    }
                    if (extra.Count > 0)
                    {
                        doc.ClearSelection2(true);
                        foreach (var an in extra) an.Select3(true, null);
                        doc.Extension.DeleteSelection2((int)swDeleteSelectionOptions_e.swDelete_Absorbed);
                    }
                    Console.WriteLine("EXTRA\t" + v.Name + "\t" + extra.Count + " other dimensions the insert added, deleted again");
                    foreach (var l in want) { Console.WriteLine("MISSING\t" + v.Name + "\t" + l.FullName); missing++; }
                }
            }
            doc.ForceRebuild3(false);
            if (save)
            {
                int se = 0, sw2 = 0;
                bool ok = doc.Save3((int)swSaveAsOptions_e.swSaveAsOptions_Silent, ref se, ref sw2);
                Console.WriteLine(ok ? "SAVED\t" + drw : "SAVE FAILED " + se);
            }
            else Console.WriteLine("Not saved (no --save).");
        }
        finally
        {
            sw.CloseDoc(doc.GetTitle());
            sw.CloseDoc(held.GetTitle());
        }
        return missing == 0 ? 0 : 1;
    }
}
