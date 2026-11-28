// SwPic: read-only pictures and an interference list around chosen components of an assembly (28 Nov 2026, LS-08A-07
// doubled-tube question for Grant). Opens the assembly READ-ONLY in the running SOLIDWORKS 2026 (SldWorks.Application.34),
// changes colours / adds a section only in memory, never saves the model, closes it at the end.
// Usage: SwPic.exe <asm> <config> <outDir> <compA> <compB> [margin mm]
//   writes <outDir>\interference.txt (every interference that involves compA or compB: components, overlap volume) and
//   components.txt (name, file, configuration, world box of each component near the two), and PNGs:
//   iso.png, iso-colour.png (compA red, compB blue, the rest grey), section.png, section-colour.png (horizontal section at
//   compA's mid-height, looking down), front.png / front-colour.png.
// Run under SwCmd's lock. Refuses Dropbox and .pdm; output only under C:\PDM-work.
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwPic.exe SwPic.cs
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwPic
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    static string F(double m) { return (m * 1000).ToString("0.#", Inv); }

    static double[] Box(Component2 c) { var b = c.GetBox(false, false) as double[]; return b; }

    static int Main(string[] a)
    {
        if (a.Length < 5) { Console.Error.WriteLine("Usage: SwPic <asm> <config> <outDir> <compA> <compB> [margin mm]"); return 2; }
        string file = Path.GetFullPath(a[0]), cfg = a[1], outDir = Path.GetFullPath(a[2]);
        double margin = (a.Length > 5 ? double.Parse(a[5], Inv) : 150) / 1000.0;
        if (file.StartsWith(@"M:\", StringComparison.OrdinalIgnoreCase) || file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0 || file.IndexOf(@"\.pdm", StringComparison.OrdinalIgnoreCase) >= 0)
        { Console.Error.WriteLine("REFUSED: never Dropbox or .pdm"); return 2; }
        if (!outDir.StartsWith(@"C:\PDM-work\", StringComparison.OrdinalIgnoreCase)) { Console.Error.WriteLine("REFUSED: output only under C:\\PDM-work"); return 2; }
        Directory.CreateDirectory(outDir);
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocASSEMBLY, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly | swOpenDocOptions_e.swOpenDocOptions_OverrideDefaultLoadLightweight), cfg, ref e, ref w);
        if (doc == null) { Console.WriteLine("FAILED\topen " + e); return 3; }
        int rc = 0;
        try
        {
            doc.ShowConfiguration2(cfg);
            var asm = (AssemblyDoc)doc;
            asm.ResolveAllLightWeightComponents(false);
            doc.ForceRebuild3(false);
            Console.WriteLine("OPENED\t" + file + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name + "\tread-only " + doc.IsOpenedReadOnly());
            var ca = (Component2)asm.GetComponentByName(a[3]); var cb = (Component2)asm.GetComponentByName(a[4]);
            if (ca == null || cb == null) throw new Exception("component not found: " + (ca == null ? a[3] : a[4]));
            var ba = Box(ca); var bb = Box(cb);
            double[] zone = { Math.Min(ba[0], bb[0]) - margin, Math.Min(ba[1], bb[1]) - margin, Math.Min(ba[2], bb[2]) - margin, Math.Max(ba[3], bb[3]) + margin, Math.Max(ba[4], bb[4]) + margin, Math.Max(ba[5], bb[5]) + margin };
            var comps = ((object[])asm.GetComponents(false) ?? new object[0]).Cast<Component2>().Where(c => c.GetSuppression2() != (int)swComponentSuppressionState_e.swComponentSuppressed).ToList();
            var lines = new List<string>();
            foreach (var c in comps)
            {
                var b = Box(c); if (b == null) continue;
                bool near = b[3] >= ba[0] - margin && b[0] <= ba[3] + margin && b[4] >= ba[1] - margin && b[1] <= ba[4] + margin && b[5] >= ba[2] - margin && b[2] <= ba[5] + margin;
                if (!near) continue;
                var t = (double[])((MathTransform)c.Transform2).ArrayData;
                lines.Add(c.Name2 + "\t" + Path.GetFileName(c.GetPathName()) + "\t" + c.ReferencedConfiguration + "\tbox X " + F(b[0]) + ".." + F(b[3]) + " Y " + F(b[1]) + ".." + F(b[4]) + " Z " + F(b[2]) + ".." + F(b[5]) + "\torigin " + F(t[9]) + " " + F(t[10]) + " " + F(t[11]));
            }
            File.WriteAllLines(Path.Combine(outDir, "components.txt"), lines);
            var nearNames = new HashSet<string>(lines.Select(l => l.Split('	')[0]));
            Console.WriteLine("NEAR\t" + lines.Count + " components within " + F(margin) + " mm of " + a[3]);

            // interference detection over the whole assembly, then keep what involves A or B
            var idm = asm.InterferenceDetectionManager;
            idm.TreatCoincidenceAsInterference = false; idm.TreatSubAssembliesAsComponents = false; idm.IncludeMultibodyPartInterferences = true;
            idm.MakeInterferingPartsTransparent = false; idm.CreateFastenersFolder = false; idm.IgnoreHiddenBodies = true; idm.ShowIgnoredInterferences = false; idm.UseTransform = true;
            var ints = idm.GetInterferences() as object[] ?? new object[0];
            var il = new List<string>();
            foreach (Interference it in ints)
            {
                var ic = ((object[])it.Components ?? new object[0]).Cast<Component2>().ToList();
                if (!ic.Any(k => k.Name2 == ca.Name2 || k.Name2 == cb.Name2)) continue;
                il.Add(string.Join(" | ", ic.Select(k => k.Name2 + " (" + Path.GetFileName(k.GetPathName()) + " <" + k.ReferencedConfiguration + ">)")) + "\toverlap mm3 " + (it.Volume * 1e9).ToString("0.0", Inv));
            }
            idm.Done();
            File.WriteAllLines(Path.Combine(outDir, "interference.txt"), il.Count > 0 ? il : new List<string> { "none involving " + a[3] + " or " + a[4] });
            Console.WriteLine("INTERFERENCES\t" + ints.Length + " in all\t" + il.Count + " involve " + a[3] + " or " + a[4]);
            foreach (var l in il) Console.WriteLine("  " + l);

            var ext = doc.Extension;
            Action<string> save = n => { int se = 0, swn = 0; bool ok = ext.SaveAs3(Path.Combine(outDir, n), (int)swSaveAsVersion_e.swSaveAsCurrentVersion, (int)swSaveAsOptions_e.swSaveAsOptions_Silent | (int)swSaveAsOptions_e.swSaveAsOptions_Copy, null, null, ref se, ref swn); Console.WriteLine((ok ? "PICTURE\t" : "PICTURE FAILED\t") + n + (ok ? "" : " " + se)); };
            Action<string> view = v =>
            {
                doc.ShowNamedView2(v, -1);
                doc.ViewZoomtofit2();
                doc.GraphicsRedraw2();
            };
            Action colour = () =>
            {
                foreach (var c in comps)
                {
                    double[] mp;
                    if (c.Name2 == ca.Name2) mp = new double[] { 0.85, 0.1, 0.1, 1, 1, 0.5, 0.4, 0, 0 };
                    else if (c.Name2 == cb.Name2) mp = new double[] { 0.1, 0.3, 0.9, 1, 1, 0.5, 0.4, 0.55, 0 };
                    else mp = new double[] { 0.75, 0.75, 0.75, 1, 1, 0.5, 0.4, 0.8, 0 };
                    c.SetMaterialPropertyValues2(mp, (int)swInConfigurationOpts_e.swThisConfiguration, null);
                    try { c.MaterialPropertyValues = mp; } catch { }
                    if (c.Name2 == ca.Name2 || c.Name2 == cb.Name2)
                    {
                        var got = c.GetMaterialPropertyValues2((int)swInConfigurationOpts_e.swThisConfiguration, null) as double[];
                        Console.WriteLine("COLOUR	" + c.Name2 + "	" + (got == null ? "none" : string.Join(" ", got.Take(3).Select(v => v.ToString("0.00", Inv)))));
                    }
                }
                doc.GraphicsRedraw2();
            };
            doc.SetUserPreferenceToggle((int)swUserPreferenceToggle_e.swDisplayPlanes, false);
            int hidden = 0;
            foreach (var c in comps) if (!nearNames.Contains(c.Name2) && ((object[])c.GetChildren() ?? new object[0]).Length == 0) { c.Visible = (int)swComponentVisibilityState_e.swComponentHidden; hidden++; }
            Console.WriteLine("HIDDEN	" + hidden + " components away from the spot (in memory only)");
            view("*Isometric"); save("iso.png");
            view("*Front"); save("front.png");
            // colours do not show in the saved pictures (part appearances win), so each tube is also shown on its own
            cb.Visible = (int)swComponentVisibilityState_e.swComponentHidden;
            view("*Isometric"); save("iso-only-" + ca.Name2 + ".png");
            cb.Visible = (int)swComponentVisibilityState_e.swComponentVisible; ca.Visible = (int)swComponentVisibilityState_e.swComponentHidden;
            view("*Isometric"); save("iso-only-" + cb.Name2 + ".png");
            ca.Visible = (int)swComponentVisibilityState_e.swComponentVisible;
            // section: a temporary plane at A's mid-height (in memory only; the model is read-only and never saved)
            double ymid = (ba[1] + ba[4]) / 2;
            doc.ClearSelection2(true);
            doc.Extension.SelectByID2("Top Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
            var pl = (RefPlane)doc.FeatureManager.InsertRefPlane((int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance, Math.Abs(ymid), 0, 0, 0, 0);
            Feature plf = pl as Feature;
            if (plf == null) { Console.WriteLine("PROBLEM\tno temporary plane"); rc = 1; }
            else
            {
                // flip if the plane went the wrong way
                var pp = (double[])((MathTransform)pl.Transform).ArrayData;
                Console.WriteLine("PLANE\tY " + F(pp[10]) + " (wanted " + F(ymid) + ")");
                if (Math.Abs(pp[10] - ymid) > 1e-4)
                {
                    doc.ClearSelection2(true); plf.Select2(false, 0); doc.EditDelete();
                    doc.Extension.SelectByID2("Top Plane", "PLANE", 0, 0, 0, false, 0, null, 0);
                    pl = (RefPlane)doc.FeatureManager.InsertRefPlane((int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_Distance | (int)swRefPlaneReferenceConstraints_e.swRefPlaneReferenceConstraint_OptionFlip, Math.Abs(ymid), 0, 0, 0, 0);
                    plf = pl as Feature;
                    pp = (double[])((MathTransform)pl.Transform).ArrayData;
                    Console.WriteLine("PLANE\tflipped: Y " + F(pp[10]));
                }
                doc.ClearSelection2(true);
                plf.Select2(false, 0);
                var svd = (SectionViewData)doc.ModelViewManager.CreateSectionViewData();
                svd.FirstPlane = plf; svd.FirstReverseDirection = false; svd.ShowSectionCap = true; svd.KeepCapColor = false;
                bool sv = doc.ModelViewManager.CreateSectionView(svd);
                Console.WriteLine("SECTION\t" + sv);
                doc.ShowNamedView2("*Top", -1);
                doc.ViewZoomtofit2();
                doc.GraphicsRedraw2();
                save("section.png");
            }
        }
        catch (Exception ex) { Console.WriteLine("FAILED\t" + ex.Message); rc = 3; }
        finally { sw.CloseDoc(doc.GetTitle()); }
        Console.WriteLine("DONE\texit " + rc);
        return rc;
    }
}
