// Draw: copy-then-repair, part 1 (brief draw-1 step 3): switch a COPY of a finished drawing to a twin model and
// report, item by item, what survives, what changes value and what comes loose. Nothing is saved (Grant's D1:
// never save a drawing with a dangling dimension); a PDF of the switched drawing is exported to look at.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    class Item { public string View, Name, Kind, Text; public double Value = double.NaN; public double[] Pos; public bool Dangling; }

    static List<Item> Items(DrawingDoc drw)
    {
        var r = new List<Item>();
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView())
            foreach (Annotation a in (v.GetAnnotations() as object[]) ?? new object[0])
            {
                var it = new Item { View = v.GetName2(), Name = a.GetName(), Kind = TypeName(a.GetType()).Replace("sw", ""), Pos = a.GetPosition() as double[], Dangling = a.IsDangling() };
                if (a.GetType() == (int)swAnnotationType_e.swDisplayDimension)
                {
                    var dd = (DisplayDimension)a.GetSpecificAnnotation();
                    var d = (Dimension)dd.GetDimension2(0);
                    if (d != null) it.Value = d.GetSystemValue2("") * 1000;
                    it.Text = (dd.ShowParenthesis ? "(" : "") + Flat(dd.GetText((int)swDimensionTextParts_e.swDimensionTextPrefix)) + "|" + Flat(dd.GetText((int)swDimensionTextParts_e.swDimensionTextSuffix));
                }
                else if (a.GetType() == (int)swAnnotationType_e.swNote) it.Text = Flat(((Note)a.GetSpecificAnnotation()).GetText());
                r.Add(it);
            }
        return r;
    }

    static int CopySwitch(string src, string newModel, string output)
    {
        if (File.Exists(output)) { Console.Error.WriteLine("REFUSED: already exists, not overwritten: " + output); return 2; }
        if (!File.Exists(newModel)) { Console.Error.WriteLine("REFUSED: no such model " + newModel); return 2; }
        string pdfOut = Path.ChangeExtension(output, ".switched.pdf");
        if (File.Exists(pdfOut)) { Console.Error.WriteLine("REFUSED: already exists: " + pdfOut); return 2; }
        var t0 = DateTime.Now;
        File.Copy(src, output);
        File.SetAttributes(output, FileAttributes.Normal);
        var doc = OpenDoc(output, null, false);
        var drw = (DrawingDoc)doc;
        var before = Items(drw);
        var views = new List<View>();
        for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView()) views.Add(v);
        Console.WriteLine("Copy of " + Path.GetFileName(src) + ": " + views.Count + " views, " + before.Count + " annotations ("
            + string.Join(", ", before.GroupBy(i => i.Kind).Select(g => g.Count() + " " + g.Key)) + "); dangling before the switch: " + before.Count(i => i.Dangling));
        foreach (var v in views) Console.WriteLine("  view " + v.GetName2() + " '" + v.GetOrientationName() + "' " + Path.GetFileName(v.GetReferencedModelName()) + " [" + v.ReferencedConfiguration + "]");

        // Switch every view that shows the old top model to the new model (Tools > Replace Model).
        string oldModel = views.Select(v => v.GetReferencedModelName()).GroupBy(x => x, StringComparer.OrdinalIgnoreCase).OrderByDescending(g => g.Count()).First().Key;
        var targets = views.Where(v => string.Equals(v.GetReferencedModelName(), oldModel, StringComparison.OrdinalIgnoreCase)).ToList();
        var vw = targets.Select(v => new DispatchWrapper(v)).ToArray();
        var inst = targets.Select(v => { var rc = v.RootDrawingComponent; return new DispatchWrapper(rc != null ? rc.Component : null); }).ToArray();
        bool ok = drw.ReplaceViewModel(newModel, vw, inst);
        if (!ok) { Console.WriteLine("ReplaceViewModel with root components refused; trying without instances"); ok = drw.ReplaceViewModel(newModel, vw, null); }
        Console.WriteLine("Switched " + targets.Count + " views from " + Path.GetFileName(oldModel) + " to " + Path.GetFileName(newModel) + ": " + ok);
        if (!ok) { problems.Add("model not replaced"); return 1; }
        doc.ForceRebuild3(false);
        foreach (var v in views) Console.WriteLine("  view " + v.GetName2() + " now " + Path.GetFileName(v.GetReferencedModelName()) + " [" + v.ReferencedConfiguration + "]");

        var after = Items(drw);
        var byName = after.GroupBy(i => i.View + "/" + i.Name).ToDictionary(g => g.Key, g => g.First());
        int same = 0, changed = 0, loose = 0, gone = 0;
        var rows = new List<string>();
        foreach (var b in before)
        {
            Item a;
            string verdict;
            if (!byName.TryGetValue(b.View + "/" + b.Name, out a)) { gone++; verdict = "GONE"; }
            else if (a.Dangling) { loose++; verdict = "LOOSE"; }
            else if (!double.IsNaN(b.Value) && Math.Abs(a.Value - b.Value) > 0.01) { changed++; verdict = "CHANGED " + b.Value.ToString("0.##") + " -> " + a.Value.ToString("0.##"); }
            else { same++; verdict = "same"; }
            rows.Add(b.View + "\t" + b.Kind + "\t" + b.Name + "\t" + (double.IsNaN(b.Value) ? "" : b.Value.ToString("0.##")) + "\t" + (b.Text ?? "") + "\t" + verdict);
        }
        Console.WriteLine("After the switch: " + same + " unchanged, " + changed + " changed value, " + loose + " loose (dangling), " + gone + " gone; new items " + (after.Count - before.Count + gone));
        foreach (var g in rows.GroupBy(r => r.Split('\t')[0] + " " + r.Split('\t')[1]))
            Console.WriteLine("  " + g.Key + ": " + string.Join(", ", g.GroupBy(r => r.Split('\t')[5].Split(' ')[0]).Select(x => x.Count() + " " + x.Key)));
        string report = Path.ChangeExtension(output, ".switch-report.tsv");
        File.WriteAllLines(report, new[] { "view\tkind\tname\tvalue before\ttext\tafter" }.Concat(rows));
        Console.WriteLine("Item list: " + report);

        int err = 0, warn = 0;
        ok = doc.Extension.SaveAs3(pdfOut, (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)(swSaveAsOptions_e.swSaveAsOptions_Silent | swSaveAsOptions_e.swSaveAsOptions_Copy), null, null, ref err, ref warn);
        Console.WriteLine(ok ? "PDF of the switched drawing (loose items show in their dangling colour): " + pdfOut : "PDF failed " + err);
        Console.WriteLine("The switched drawing is left open, NOT saved" + (loose + gone > 0 ? " (it has loose dimensions)" : "") + ". Time " + (DateTime.Now - t0).TotalSeconds.ToString("0") + " s.");
        return 0;
    }
}
