// SwTree: read-only (Grant 22 Nov 2026, angles B2): list a part's features in order with type, suppression, parents,
// and details for Insert Part (source, configuration, transfer flags) and body mirror / move features, plus the solid
// bodies and the cut-list folders. Opens read-only, never saves.
// Usage: SwTree.exe <part>
// Build: csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwTree.exe SwTree.cs

using System;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwTree
{
    static string S(Func<string> f) { try { return f() ?? ""; } catch (Exception ex) { return "(" + ex.GetType().Name + ")"; } }

    static int Main(string[] a)
    {
        string file = Path.GetFullPath(a[0]);
        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026"); return 2; }
        int e = 0, w = 0;
        var doc = sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), "", ref e, ref w);
        if (doc == null) { Console.Error.WriteLine("could not open " + e); return 3; }
        try
        {
            Console.WriteLine("CONFIGS\t" + string.Join(", ", (string[])doc.GetConfigurationNames()) + "\tactive " + ((Configuration)doc.GetActiveConfiguration()).Name);
            for (var f = (Feature)doc.FirstFeature(); f != null; f = (Feature)f.GetNextFeature())
            {
                string t = f.GetTypeName2();
                if (t == "HistoryFolder" || t == "SensorFolder" || t == "DocsFolder" || t == "DetailCabinet" || t == "SurfaceBodyFolder" || t == "EnvFolder" || t == "InkMarkupFolder" || t == "EqnFolder" || t == "MaterialFolder" || t == "OriginProfileFeature" || t == "SelectionSetFolder" || t == "CommentsFolder" || t == "FavoriteFolder" || t == "MateReferenceGroupFolder" || t == "BlockFolder") continue;
                var parents = ((object[])f.GetParents() ?? new object[0]).Cast<Feature>().Select(p => p.Name);
                Console.WriteLine("FEAT\t" + f.Name + "\t" + t + (f.IsSuppressed() ? "\tSUPPRESSED" : "") + "\tparents: " + string.Join(", ", parents));
                for (var dd = (DisplayDimension)f.GetFirstDisplayDimension(); dd != null; dd = (DisplayDimension)f.GetNextDisplayDimension(dd))
                {
                    var dim = dd.GetDimension2(0);
                    Console.WriteLine("  DIM\t" + S(() => dim.FullName) + "\t" + S(() => (((double[])dim.GetSystemValue3((int)swInConfigurationOpts_e.swThisConfiguration, null))[0] * (dim.GetType() == (int)swDimensionParamType_e.swDimensionParamTypeDoubleAngular ? 180 / Math.PI : 1000)).ToString("0.###")) + "\tdriven " + S(() => dim.DrivenState.ToString()));
                }
                if (t == "ProfileFeature" || t == "3DProfileFeature")
                {
                    var sk = f.GetSpecificFeature2() as Sketch;
                    if (sk != null)
                    {
                        foreach (SketchSegment seg in (object[])sk.GetSketchSegments() ?? new object[0])
                        {
                            string g = "";
                            var ln = seg as SketchLine; var arc = seg as SketchArc;
                            Func<SketchPoint, string> P = p => "(" + (p.X * 1000).ToString("0.##") + ", " + (p.Y * 1000).ToString("0.##") + ", " + (p.Z * 1000).ToString("0.##") + ")";
                            if (ln != null) g = "line " + P((SketchPoint)ln.GetStartPoint2()) + " -> " + P((SketchPoint)ln.GetEndPoint2());
                            else if (arc != null) g = (arc.IsCircle() == 1 ? "circle" : "arc") + " centre " + P((SketchPoint)arc.GetCenterPoint2()) + " r " + (arc.GetRadius() * 1000).ToString("0.##");
                            else g = ((swSketchSegments_e)seg.GetType()).ToString();
                            Console.WriteLine("  SEG\t" + seg.GetName() + "\t" + g + (seg.ConstructionGeometry ? "\tconstruction" : ""));
                        }
                    }
                }
                if (t == "Stock") { var d = f.GetDefinition() as DerivedPartFeatureData; if (d != null) Console.WriteLine("  INSERTPART	" + d.PathName + " [" + d.PartConfiguration + "]	cutlist " + d.ImportCutListProperties); }
                if (t == "SolidBodyFolder")
                    for (var s = (Feature)f.GetFirstSubFeature(); s != null; s = (Feature)s.GetNextSubFeature())
                    {
                        var bf = s.GetSpecificFeature2() as BodyFolder;
                        Console.WriteLine("  CUTLIST\t" + s.Name + "\t" + s.GetTypeName2() + "\tbodies " + (bf == null ? "" : string.Join(", ", ((object[])bf.GetBodies() ?? new object[0]).Cast<Body2>().Select(b => b.Name))));
                    }
            }
        }
        finally { sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }
}
