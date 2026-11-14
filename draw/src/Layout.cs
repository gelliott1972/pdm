// Draw: sheet layout learned from drawings Grant (or James) has laid out by hand (Grant 14 Nov: "use my drawings to
// get the feel of the layout, don't hard code them").
//
// learn-layout <drawing> ... measures each example as proportions of the sheet's free space, so the feel carries to
// parts of any size and scale, and keeps them in draw\.claude\skills\drawing-standards\layout-examples.json (one
// entry per drawing, replaced when the same drawing is learned again). Layout() uses the average of all examples.
// Only the Unilumin sheet format's own geometry (border, title block, revision table) is fixed here.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

static partial class Draw
{
    // Unilumin A3 sheet format (measured from the format in _Templates: zone letters at 10/410, border inside them,
    // title block bottom right, revision table bottom left).
    const double BorderX0 = 0.0125, BorderX1 = 0.4075, BorderY0 = 0.0125, BorderY1 = 0.2845;   // James's revision table and BOM sit on these corners
    const double TitleLeft = 0.205, TitleTop = 0.065, RevTop = 0.022;

    static string LayoutFile { get { return @"C:\code\pdm\draw\.claude\skills\drawing-standards\layout-examples.json"; } }

    // One example's measurements (all proportions or paper millimetres, never sheet positions).
    class LayoutExample
    {
        public string Drawing;
        public double RowX;      // centre of face+side views across the border, 0 = left, 1 = right
        public double RowY;      // centre of face+side views up the border, 0 = bottom, 1 = top
        public double GapMm;     // clear paper between the face view's dimensions and the side view's
        public double IsoX;      // isometric+note block centre across the free space left of the title block (0..1)
        public double IsoY;      // ... and up the free space between the revision table and the views above (0..1)
        public double NoteGapMm; // gap from the isometric to its note block
        public string Flags;     // corner of the CHECK flags: TL, TR, BL, BR, or ""
    }

    static readonly LayoutExample DefaultStyle = new LayoutExample { RowX = 0.5, RowY = 0.6, GapMm = 35, IsoX = 0.4, IsoY = 0.4, NoteGapMm = 12, Flags = "TL" };

    static LayoutExample Style()
    {
        var all = LoadExamples();
        if (all.Count == 0) { Console.WriteLine("Layout: no learned examples, using defaults"); return DefaultStyle; }
        var s = new LayoutExample
        {
            RowX = all.Average(e => e.RowX), RowY = all.Average(e => e.RowY), GapMm = all.Average(e => e.GapMm),
            IsoX = all.Average(e => e.IsoX), IsoY = all.Average(e => e.IsoY), NoteGapMm = all.Average(e => e.NoteGapMm),
            Flags = all.Where(e => e.Flags != "").GroupBy(e => e.Flags).OrderByDescending(g => g.Count()).Select(g => g.Key).FirstOrDefault() ?? "TL"
        };
        Console.WriteLine("Layout: learned from " + all.Count + " drawing(s): views at " + s.RowX.ToString("0.00") + " across, " + s.RowY.ToString("0.00")
            + " up; gap " + s.GapMm.ToString("0") + " mm; isometric at " + s.IsoX.ToString("0.00") + ", " + s.IsoY.ToString("0.00") + " of its corner; flags " + s.Flags);
        return s;
    }

    // ---------- learning ----------
    static int LearnLayout(List<string> files)
    {
        var all = LoadExamples();
        foreach (var f in files)
        {
            var doc = (ModelDoc2)sw.GetOpenDocumentByName(f) ?? OpenDoc(f, null, true);
            var drw = (DrawingDoc)doc;
            var views = new List<View>();
            for (var v = (View)((View)drw.GetFirstView()).GetNextView(); v != null; v = (View)v.GetNextView()) views.Add(v);
            var iso = views.FirstOrDefault(v => v.GetOrientationName() == "*Isometric");
            // the face view: the one with the most dimensions; the side view: the other non-isometric one
            var rest = views.Where(v => v != iso).OrderByDescending(v => ((v.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>()
                .Count(a => a.GetType() == (int)swAnnotationType_e.swDisplayDimension)).ToList();
            if (rest.Count == 0) { Console.Error.WriteLine("no face view in " + f); continue; }
            var face = rest[0]; var side = rest.Count > 1 ? rest[1] : null;
            var fo = (double[])face.GetOutline(); var so = side != null ? (double[])side.GetOutline() : fo;
            var fe = Extent(face); var se = side != null ? Extent(side) : fe;
            double rx0 = Math.Min(fo[0], so[0]), rx1 = Math.Max(fo[2], so[2]), ry0 = Math.Min(fo[1], so[1]), ry1 = Math.Max(fo[3], so[3]);
            var e = new LayoutExample { Drawing = Path.GetFileName(f) };
            e.RowX = ((rx0 + rx1) / 2 - BorderX0) / (BorderX1 - BorderX0);
            e.RowY = ((ry0 + ry1) / 2 - BorderY0) / (BorderY1 - BorderY0);
            e.GapMm = side != null ? (Math.Max(se[0], fe[2]) == se[0] ? se[0] - fe[2] : fe[0] - se[2]) * 1000 : DefaultStyle.GapMm;
            if (iso != null)
            {
                var ie = Extent(iso); var io = (double[])iso.GetOutline();
                double top = Math.Min(fe[1], se[1]);                                 // the free corner: up to the views above
                e.IsoX = ((ie[0] + ie[2]) / 2 - BorderX0) / (TitleLeft - BorderX0);
                e.IsoY = ((ie[1] + ie[3]) / 2 - RevTop) / (top - RevTop);
                var note = ((iso.GetAnnotations() as object[]) ?? new object[0]).Cast<Annotation>().FirstOrDefault(a => a.GetType() == (int)swAnnotationType_e.swNote);
                e.NoteGapMm = note != null ? (((double[])note.GetPosition())[0] - io[2]) * 1000 : DefaultStyle.NoteGapMm;
            }
            else { e.IsoX = DefaultStyle.IsoX; e.IsoY = DefaultStyle.IsoY; e.NoteGapMm = DefaultStyle.NoteGapMm; }
            // CHECK flags: a sheet note starting "CHECK:"
            e.Flags = "";
            foreach (Annotation a in (((View)drw.GetFirstView()).GetAnnotations() as object[]) ?? new object[0])
            {
                if (a.GetType() != (int)swAnnotationType_e.swNote) continue;
                var n = (Note)a.GetSpecificAnnotation();
                if (!(n.GetText() ?? "").StartsWith("CHECK")) continue;
                var p = (double[])a.GetPosition();
                e.Flags = (p[1] > (BorderY0 + BorderY1) / 2 ? "T" : "B") + (p[0] < (BorderX0 + BorderX1) / 2 ? "L" : "R");
            }
            all.RemoveAll(x => x.Drawing == e.Drawing);
            all.Add(e);
            Console.WriteLine("Learned from " + e.Drawing + ": views at " + e.RowX.ToString("0.00") + " across, " + e.RowY.ToString("0.00") + " up; gap "
                + e.GapMm.ToString("0") + " mm; isometric at " + e.IsoX.ToString("0.00") + ", " + e.IsoY.ToString("0.00") + " of the free corner; note gap "
                + e.NoteGapMm.ToString("0") + " mm; flags " + (e.Flags == "" ? "none" : e.Flags));
        }
        SaveExamples(all);
        Console.WriteLine("Layout examples: " + all.Count + " in " + LayoutFile);
        return 0;
    }

    static List<LayoutExample> LoadExamples()
    {
        var r = new List<LayoutExample>();
        if (!File.Exists(LayoutFile)) return r;
        foreach (Match m in Regex.Matches(File.ReadAllText(LayoutFile), @"\{[^{}]*\}"))
        {
            Func<string, string> g = k => { var x = Regex.Match(m.Value, "\"" + k + "\"\\s*:\\s*(\"([^\"]*)\"|(-?[0-9.]+))"); return !x.Success ? null : x.Groups[2].Success && x.Groups[1].Value.StartsWith("\"") ? x.Groups[2].Value : x.Groups[3].Value; };
            Func<string, double> d = k => { double v; return double.TryParse(g(k), NumberStyles.Float, CultureInfo.InvariantCulture, out v) ? v : double.NaN; };
            var e = new LayoutExample { Drawing = g("drawing"), RowX = d("rowX"), RowY = d("rowY"), GapMm = d("gapMm"), IsoX = d("isoX"), IsoY = d("isoY"), NoteGapMm = d("noteGapMm"), Flags = g("flags") ?? "" };
            if (e.Drawing != null && !double.IsNaN(e.RowX)) r.Add(e);
        }
        return r;
    }

    static void SaveExamples(List<LayoutExample> all)
    {
        Func<double, string> n = x => x.ToString("0.###", CultureInfo.InvariantCulture);
        var lines = all.Select(e => "  {\"drawing\": " + J(e.Drawing) + ", \"rowX\": " + n(e.RowX) + ", \"rowY\": " + n(e.RowY) + ", \"gapMm\": " + n(e.GapMm)
            + ", \"isoX\": " + n(e.IsoX) + ", \"isoY\": " + n(e.IsoY) + ", \"noteGapMm\": " + n(e.NoteGapMm) + ", \"flags\": " + J(e.Flags) + "}");
        Directory.CreateDirectory(Path.GetDirectoryName(LayoutFile));
        File.WriteAllText(LayoutFile, "[\n" + string.Join(",\n", lines) + "\n]\n");
    }

    static string J(string s) { return "\"" + (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\""; }

    // ---------- applying ----------
    static bool columnMode;

    static void Layout(ModelDoc2 doc, View main, View side, View iso)
    {
        var st = Style();
        doc.ForceRebuild3(false);
        var mo = (double[])main.GetOutline(); var me = Extent(main);
        double[] so = side != null ? (double[])side.GetOutline() : null, se = side != null ? Extent(side) : null;
        // face view, then the side view GapMm of clear paper past the face view's dimensions (outline to outline)
        double gap = side == null ? 0 : (me[2] - mo[2]) + (so[0] - se[0]) + st.GapMm / 1000;
        double rowW = W(mo) + (side != null ? gap + W(so) : 0);
        double rowH = Math.Max(H(mo), side != null ? H(so) : 0);
        double cx = BorderX0 + st.RowX * (BorderX1 - BorderX0), cy = BorderY0 + st.RowY * (BorderY1 - BorderY0);
        double x0 = cx - rowW / 2;
        double dyMid = cy - (mo[1] + mo[3]) / 2;
        // keep the dimensions on the paper
        double topExt = Math.Max(me[3], se != null ? se[3] : me[3]) + dyMid, botExt = Math.Min(me[1], se != null ? se[1] : me[1]) + dyMid;
        if (topExt > BorderY1 - 0.003) dyMid -= topExt - (BorderY1 - 0.003);
        else if (botExt < TitleTop) dyMid += TitleTop - botExt;
        double leftExt = me[0] + (x0 - mo[0]), rightExt = (se != null ? se[2] - so[0] + x0 + W(mo) + gap : me[2] + x0 - mo[0]);
        if (leftExt < BorderX0 + 0.003) x0 += BorderX0 + 0.003 - leftExt; else if (rightExt > BorderX1 - 0.003) x0 -= rightExt - (BorderX1 - 0.003);
        // the row's outer edges (with dimensions) for a given x0
        Func<double, double> rowLeftAt = xx => xx - (mo[0] - me[0]);
        Func<double, double> rowRightAt = xx => xx + W(mo) + (side != null ? gap + (se[2] - so[0]) : me[2] - mo[2]);
        double rowTopNow = Math.Max(me[3], se != null ? se[3] : me[3]) + dyMid;
        // Will the isometric (with its note) fit under the views? If not, James's arrangement for big assemblies:
        // the views over to the right, the isometric down the left (under the BOM if there is one).
        columnMode = false;
        if (iso != null)
        {
            var ie0 = Extent(iso);
            double viewsBottom = Math.Min(me[1], se != null ? se[1] : me[1]) + dyMid;
            if (H(ie0) > viewsBottom - RowGap - RevTop)
            {
                columnMode = true;
                x0 += (BorderX1 - 0.003) - rowRightAt(x0);
                Console.WriteLine("Layout: the isometric is too tall to go under the views; views to the right, isometric down the left");
            }
        }
        // keep the row clear of the BOM: to its right if there's room, else below it
        if (LayoutBlocked != null && rowTopNow > LayoutBlocked[1] && rowLeftAt(x0) < LayoutBlocked[2] + 0.010)
        {
            double want = LayoutBlocked[2] + 0.010 + (mo[0] - me[0]);
            if (rowRightAt(want) <= BorderX1 - 0.003) x0 = want;
            else dyMid -= rowTopNow - (LayoutBlocked[1] - 0.010);
        }
        MoveOutline(doc, main, x0, (mo[1] + mo[3]) / 2 + dyMid);
        if (side != null) MoveOutline(doc, side, x0 + W(mo) + gap, double.NaN);   // a projected view stays level by itself
        var fa = Extent(main); var fb = side != null ? Extent(side) : null;
        if (iso != null)
        {
            // the note block first, NoteGapMm right of the isometric; then the pair in the free corner at IsoX, IsoY
            var io0 = (double[])iso.GetOutline();
            foreach (Annotation a in (iso.GetAnnotations() as object[]) ?? new object[0])
                if (a.GetType() == (int)swAnnotationType_e.swNote)
                {
                    var p0 = (double[])a.GetPosition();
                    a.SetPosition2(io0[2] + st.NoteGapMm / 1000, (io0[1] + io0[3]) / 2 + 0.012, 0);
                }
            doc.ForceRebuild3(false);
            var ie = Extent(iso);
            double top = Math.Min(fa[1], fb != null ? fb[1] : fa[1]) - RowGap;
            double rightLimit = TitleLeft;
            if (columnMode)
            {
                top = BorderY1 - 0.003;                               // the whole left column ...
                rightLimit = Math.Min(TitleLeft, Math.Min(fa[0], fb != null ? fb[0] : fa[0]) - RowGap);   // ... left of the views
            }
            if (LayoutBlocked != null && LayoutBlocked[0] < TitleLeft) top = Math.Min(top, LayoutBlocked[1] - RowGap);   // below the BOM
            if (top < RevTop + 0.030) top = Math.Max(fa[1], BorderY1 - 0.003);   // no room under the views: the whole left side
            double tcx = BorderX0 + st.IsoX * (rightLimit - BorderX0), tcy = RevTop + st.IsoY * (top - RevTop);
            double dx = tcx - (ie[0] + ie[2]) / 2, dy = tcy - (ie[1] + ie[3]) / 2;
            // keep it in its corner
            if (ie[0] + dx < BorderX0 + 0.003) dx = BorderX0 + 0.003 - ie[0];
            if (ie[2] + dx > rightLimit - 0.003) dx = rightLimit - 0.003 - ie[2];
            if (ie[1] + dy < RevTop + 0.003) dy = RevTop + 0.003 - ie[1];
            if (ie[3] + dy > top) dy = top - ie[3];
            var p = (double[])iso.Position;
            iso.Position = new[] { p[0] + dx, p[1] + dy };
            doc.ForceRebuild3(false);
        }
        var fc = iso != null ? Extent(iso) : null;
        Console.WriteLine("  face block " + Box(fa) + (fb != null ? ", side " + Box(fb) : "") + (fc != null ? ", isometric " + Box(fc) : ""));
        foreach (var x in new[] { fa, fb, fc })
            if (x != null && (x[0] < BorderX0 - 1e-3 || x[2] > BorderX1 + 1e-3 || x[1] < BorderY0 || x[3] > BorderY1 + 1e-3))
                problems.Add("layout: a view runs off the drawing area " + Box(x));
        if (fc != null && fc[2] > TitleLeft && fc[1] < TitleTop) problems.Add("layout: the isometric or its note block runs into the title block " + Box(fc));
        Func<double[], double[], bool> hit = (p, q) => p != null && q != null && p[0] < q[2] && p[2] > q[0] && p[1] < q[3] && p[3] > q[1];
        if (hit(fc, fa) || hit(fc, fb)) problems.Add("layout: the isometric block overlaps the views");
        if (hit(LayoutBlocked, fa) || hit(LayoutBlocked, fb) || hit(LayoutBlocked, fc)) problems.Add("layout: a view overlaps the BOM");
    }

    // Move a view so its outline's left edge is at x (and its centre at y, unless y is NaN).
    static void MoveOutline(ModelDoc2 doc, View v, double x, double y)
    {
        doc.ForceRebuild3(false);
        var o = (double[])v.GetOutline(); var p = (double[])v.Position;
        v.Position = new[] { p[0] + x - o[0], double.IsNaN(y) ? p[1] : p[1] + y - (o[1] + o[3]) / 2 };
        doc.ForceRebuild3(false);
    }

    // CHECK flags into the learned corner, else the first free one.
    static void PlaceFlags(Annotation note, string text, IEnumerable<double[]> taken)
    {
        var st = Style();
        var lines = text.Split('\n');
        double w = lines.Max(l => l.Length) * 0.0025 * 0.85 + 0.004, h = lines.Length * 0.0045 + 0.003, m = 0.005;
        var corners = new Dictionary<string, double[]> {
            { "TL", new[] { BorderX0 + m, BorderY1 - m - h, BorderX0 + m + w, BorderY1 - m } },
            { "TR", new[] { BorderX1 - m - w, BorderY1 - m - h, BorderX1 - m, BorderY1 - m } },
            { "BR", new[] { BorderX1 - m - w, TitleTop + m, BorderX1 - m, TitleTop + m + h } },
            { "BL", new[] { BorderX0 + m, RevTop + m, BorderX0 + m + w, RevTop + m + h } } };
        var order = new[] { st.Flags }.Concat(new[] { "TL", "TR", "BR", "BL" }.Where(k => k != st.Flags)).Where(k => corners.ContainsKey(k));
        foreach (var k in order)
        {
            var sp = corners[k];
            if (!taken.Any(t => t != null && sp[0] < t[2] && sp[2] > t[0] && sp[1] < t[3] && sp[3] > t[1]))
            { note.SetPosition2(sp[0], sp[3], 0); Console.WriteLine("Flags placed " + k); return; }
        }
        // no free corner: the clear spot nearest the preferred corner, searched over the sheet in 5 mm steps
        var pref = corners.ContainsKey(st.Flags) ? corners[st.Flags] : corners["TL"];
        var title = new[] { TitleLeft, BorderY0, BorderX1, TitleTop };
        var rev = new[] { BorderX0, BorderY0, TitleLeft, RevTop };
        double[] best = null; double bestD = double.MaxValue;
        for (double x = BorderX0 + m; x + w <= BorderX1 - m; x += 0.005)
            for (double y = RevTop + m; y + h <= BorderY1 - m; y += 0.005)
            {
                var sp = new[] { x, y, x + w, y + h };
                if (taken.Concat(new[] { title, rev }).Any(t => t != null && sp[0] < t[2] && sp[2] > t[0] && sp[1] < t[3] && sp[3] > t[1])) continue;
                double d = Math.Abs(x - pref[0]) + Math.Abs(y - pref[1]);
                if (d < bestD) { bestD = d; best = sp; }
            }
        if (best != null) { note.SetPosition2(best[0], best[3], 0); Console.WriteLine("Flags placed in the clear spot nearest " + st.Flags + " at " + Mm(best[0]) + "," + Mm(best[3])); return; }
        note.SetPosition2(corners["TL"][0], corners["TL"][3], 0);
        problems.Add("no clear space for the CHECK flags; they may overlap a view");
    }
}
