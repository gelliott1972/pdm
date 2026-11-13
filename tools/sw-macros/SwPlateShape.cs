// SwPlateShape: describe flat plate bodies by their holes and slots, so plates in different parts can be compared
// (plates-0804, P2: are all the 180 x 140 x 10 plates the same?). Read-only: opens each part read-only in the running
// SOLIDWORKS 2026, never saves, closes what it opened.
// Usage: SwPlateShape.exe <part> [--config <cfg>] [<body name> ...]     no body names = every body 5..20 mm thick
// Per body: its frame as in SwCmd bodies (w = normal of the largest flat face, u = longest straight edge square to w,
// origin = box centre), then every cylindrical face as radius and axis position (u, v) in mm from the plate centre,
// sorted. A slot shows as two half-cylinders of the same radius; a round hole as one or two faces at one position.
// The SIG line is the same list with signs dropped (|u|, |v|), so plates turned or flipped compare equal.
// Run under SwCmd's lock: SwCmd lock --as sw first, SwCmd unlock --as sw after. Refuses Dropbox.
// Build (from tools\sw-macros):
//   csc /platform:x64 /r:bin\SolidWorks.Interop.sldworks.dll /r:bin\SolidWorks.Interop.swconst.dll /out:bin\SwPlateShape.exe SwPlateShape.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static class SwPlateShape
{
    static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    static int Main(string[] args)
    {
        string config = null; var names = new List<string>(); string file = null;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length) config = args[++i];
            else if (file == null) file = Path.GetFullPath(args[i]);
            else names.Add(args[i]);
        }
        if (file == null) { Console.Error.WriteLine("Usage: SwPlateShape <part> [--config <cfg>] [<body> ...]"); return 2; }
        if (file.IndexOf("Dropbox", StringComparison.OrdinalIgnoreCase) >= 0) { Console.Error.WriteLine("REFUSED (Dropbox): " + file); return 2; }
        if (System.Diagnostics.Process.GetProcessesByName("SLDWORKS").Length == 0) { Console.Error.WriteLine("SOLIDWORKS is not running; start it with SwCmd status/open first."); return 2; }

        var sw = (ISldWorks)Activator.CreateInstance(Type.GetTypeFromProgID("SldWorks.Application.34"));
        if (!sw.RevisionNumber().StartsWith("34.")) { Console.Error.WriteLine("Not SOLIDWORKS 2026: " + sw.RevisionNumber()); return 2; }
        bool wasOpen = sw.GetOpenDocumentByName(file) != null;
        int errs = 0, warns = 0;
        var doc = wasOpen ? (ModelDoc2)sw.GetOpenDocumentByName(file)
            : sw.OpenDoc6(file, (int)swDocumentTypes_e.swDocPART, (int)(swOpenDocOptions_e.swOpenDocOptions_Silent | swOpenDocOptions_e.swOpenDocOptions_ReadOnly), config ?? "", ref errs, ref warns);
        if (doc == null) { Console.Error.WriteLine("Could not open " + file + " (errors " + errs + ")"); return 3; }
        try
        {
            if (config != null && ((Configuration)doc.GetActiveConfiguration()).Name != config) doc.ShowConfiguration2(config);
            doc.ForceRebuild3(false);
            Console.WriteLine("FILE\t" + Path.GetFileName(file) + "\t" + ((Configuration)doc.GetActiveConfiguration()).Name);
            var bodies = ((((PartDoc)doc).GetBodies2((int)swBodyType_e.swSolidBody, false) as object[]) ?? new object[0]).Cast<Body2>();
            foreach (var b in bodies)
            {
                if (names.Count > 0 && !names.Any(n => string.Equals(n, b.Name, StringComparison.OrdinalIgnoreCase))) continue;
                double[] O, U, V, W; double lu, lv, lw;
                string why = Frame(b, out O, out U, out V, out W, out lu, out lv, out lw);
                if (why != null) { if (names.Count > 0) Console.WriteLine("BODY\t" + b.Name + "\t" + why); continue; }
                if (names.Count == 0 && (lw < 0.005 || lw > 0.020)) continue;
                var cyl = new List<string>(); var sig = new List<string>();
                foreach (var face in (b.GetFaces() as object[] ?? new object[0]).Cast<Face2>())
                {
                    var s = face.GetSurface() as Surface;
                    if (s == null || !s.IsCylinder()) continue;
                    var cp = (double[])s.CylinderParams;
                    var p = new[] { cp[0] - O[0], cp[1] - O[1], cp[2] - O[2] };
                    var ax = new[] { cp[3], cp[4], cp[5] };
                    double r = cp[6] * 1000, u = Dot(p, U) * 1000, v = Dot(p, V) * 1000;
                    string along = Math.Abs(Dot(ax, W)) > 0.999 ? "" : " axis-not-through-plate";
                    cyl.Add(string.Format(Inv, "r{0:0.##} ({1:0.##}, {2:0.##}){3}", r, u, v, along));
                    sig.Add(string.Format(Inv, "r{0:0.##}@{1:0.#},{2:0.#}{3}", r, Math.Abs(u), Math.Abs(v), along));
                }
                cyl.Sort(StringComparer.Ordinal); sig.Sort(StringComparer.Ordinal);
                Console.WriteLine(string.Format(Inv, "BODY\t{0}\t{1:0.##} x {2:0.##} x {3:0.##}\tfaces {4}\tcyl {5}", b.Name, lu * 1000, lv * 1000, lw * 1000, b.GetFaceCount(), cyl.Count));
                Console.WriteLine("  CYL\t" + string.Join("; ", cyl));
                Console.WriteLine("  SIG\t" + string.Join(" ", sig.Distinct()));
            }
        }
        finally { if (!wasOpen) sw.CloseDoc(doc.GetTitle()); }
        return 0;
    }

    static double Dot(double[] a, double[] b) { return a[0] * b[0] + a[1] * b[1] + a[2] * b[2]; }
    static double[] Sub(double[] a, double[] b) { return new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] }; }
    static double Len(double[] a) { return Math.Sqrt(Dot(a, a)); }
    static double[] Unit(double[] a) { double l = Len(a); return new[] { a[0] / l, a[1] / l, a[2] / l }; }
    static double[] Cross(double[] a, double[] b) { return new[] { a[1] * b[2] - a[2] * b[1], a[2] * b[0] - a[0] * b[2], a[0] * b[1] - a[1] * b[0] }; }

    // Same frame as SwCmd bodies (Place.cs BodyFrame).
    static string Frame(Body2 b, out double[] O, out double[] U, out double[] V, out double[] W, out double lu, out double lv, out double lw)
    {
        O = U = V = W = null; lu = lv = lw = 0;
        double bigArea = 0;
        foreach (var face in (b.GetFaces() as object[] ?? new object[0]).Cast<Face2>())
        {
            var s = face.GetSurface() as Surface;
            if (s == null || !s.IsPlane()) continue;
            double a = face.GetArea();
            if (a > bigArea + 1e-12) { bigArea = a; var pp = (double[])s.PlaneParams; W = Unit(new[] { pp[0], pp[1], pp[2] }); }
        }
        if (W == null) return "no planar face";
        double best = 0;
        foreach (var e in (b.GetEdges() as object[] ?? new object[0]).Cast<Edge>())
        {
            var c = e.GetCurve() as Curve;
            if (c == null || !c.IsLine()) continue;
            var sv = e.GetStartVertex() as Vertex; var ev = e.GetEndVertex() as Vertex;
            if (sv == null || ev == null) continue;
            var d = Sub((double[])ev.GetPoint(), (double[])sv.GetPoint());
            double l = Len(d);
            if (l < 1e-9 || Math.Abs(Dot(d, W)) / l > 1e-6) continue;
            if (l > best + 1e-9) { best = l; U = Unit(d); }
        }
        if (U == null) return "no straight edge square to the largest flat face";
        V = Cross(W, U);
        var pts = (b.GetVertices() as object[] ?? new object[0]).Cast<Vertex>().Select(x => (double[])x.GetPoint()).ToList();
        if (pts.Count == 0) return "no vertices";
        var u0 = U; var v0 = V; var w0 = W;
        double umin = pts.Min(p => Dot(p, u0)), umax = pts.Max(p => Dot(p, u0));
        double vmin = pts.Min(p => Dot(p, v0)), vmax = pts.Max(p => Dot(p, v0));
        double wmin = pts.Min(p => Dot(p, w0)), wmax = pts.Max(p => Dot(p, w0));
        lu = umax - umin; lv = vmax - vmin; lw = wmax - wmin;
        double cu = (umin + umax) / 2, cv = (vmin + vmax) / 2, cw = (wmin + wmax) / 2;
        O = new[] { cu * U[0] + cv * V[0] + cw * W[0], cu * U[1] + cv * V[1] + cw * W[1], cu * U[2] + cv * V[2] + cw * W[2] };
        return null;
    }
}
