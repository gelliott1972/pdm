using System.IO.Compression;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;

namespace Pdm;

// A plain one-sheet .xlsx, written directly (no library): a bold header row, then the rows; column widths; whole
// numbers as numbers and yyyy-MM-dd values as dates shown 09-Nov-2026. Nothing else: no title, fills, filters, notes,
// document properties or author (Grant, 9 Nov: plain, nothing that looks generated).
// With a template (.xlsx Grant has formatted and saved), its look is used instead: its styles, its column widths and
// each column's header (row 1) and data (row 2) formats.
public static partial class Xlsx
{
    public static void Write(string path, string sheetName, string[] headers, IEnumerable<string[]> rows, double[] widths, string? template = null,
        string[]? above = null)
    {
        // Optional plain lines above the header (the first bold), then a blank row, then the header.
        above ??= [];
        var hr = above.Length == 0 ? 1 : above.Length + 2;
        var all = rows.ToList();
        var t = template != null && File.Exists(template) ? Template.Read(template, hr) : null;
        var sb = new StringBuilder();
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>")
          .Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">")
          .Append($"<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{hr}\" topLeftCell=\"A{hr + 1}\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>")
          .Append("<sheetFormatPr defaultRowHeight=\"15\"/>");
        if (t?.Cols != null) sb.Append(t.Cols);
        else
        {
            sb.Append("<cols>");
            for (var c = 0; c < headers.Length; c++)
                sb.Append($"<col min=\"{c + 1}\" max=\"{c + 1}\" width=\"{(c < widths.Length ? widths[c] : 14):0.#}\" customWidth=\"1\"/>");
            sb.Append("</cols>");
        }
        sb.Append("<sheetData>");
        for (var a = 0; a < above.Length; a++)
            sb.Append($"<row r=\"{a + 1}\">{Text($"A{a + 1}", above[a], t?.AboveStyle(a) ?? (a == 0 ? 1 : 0))}</row>");
        sb.Append($"<row r=\"{hr}\">");
        for (var c = 0; c < headers.Length; c++) sb.Append(Text($"{Col(c)}{hr}", headers[c], t?.HeadStyle(c) ?? 1));
        sb.Append("</row>");
        for (var r = 0; r < all.Count; r++)
        {
            var n = hr + 1 + r;
            sb.Append($"<row r=\"{n}\">");
            for (var c = 0; c < headers.Length; c++)
                if (c < all[r].Length && !string.IsNullOrEmpty(all[r][c])) sb.Append(Cell($"{Col(c)}{n}", all[r][c], t?.DataStyle(c)));
            sb.Append("</row>");
        }
        sb.Append("</sheetData><pageMargins left=\"0.7\" right=\"0.7\" top=\"0.75\" bottom=\"0.75\" header=\"0.3\" footer=\"0.3\"/></worksheet>");

        if (File.Exists(path)) File.Delete(path);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Put(zip, "[Content_Types].xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">"
            + "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>"
            + "<Default Extension=\"xml\" ContentType=\"application/xml\"/>"
            + "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>"
            + "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"
            + "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>"
            + "<Override PartName=\"/docProps/core.xml\" ContentType=\"application/vnd.openxmlformats-package.core-properties+xml\"/>"
            + "</Types>");
        Put(zip, "_rels/.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>"
            + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties\" Target=\"docProps/core.xml\"/>"
            + "</Relationships>");
        // Document properties written explicitly empty (author, last saved by), so no reader fills in its own default.
        // No app.xml: no application name.
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");
        Put(zip, "docProps/core.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<cp:coreProperties xmlns:cp=\"http://schemas.openxmlformats.org/package/2006/metadata/core-properties\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\""
            + " xmlns:dcterms=\"http://purl.org/dc/terms/\" xmlns:dcmitype=\"http://purl.org/dc/dcmitype/\" xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">"
            + "<dc:creator></dc:creator><cp:lastModifiedBy></cp:lastModifiedBy>"
            + $"<dcterms:created xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:created><dcterms:modified xsi:type=\"dcterms:W3CDTF\">{now}</dcterms:modified>"
            + "</cp:coreProperties>");
        Put(zip, "xl/workbook.xml", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">"
            + $"<sheets><sheet name=\"{Esc(SheetName(sheetName))}\" sheetId=\"1\" r:id=\"rId1\"/></sheets></workbook>");
        Put(zip, "xl/_rels/workbook.xml.rels", "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">"
            + "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>"
            + "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>"
            + "</Relationships>");
        // Styles: 0 normal, 1 bold (header), 2 date dd-mmm-yyyy. Calibri 11, Excel's usual.
        Put(zip, "xl/styles.xml", t?.Styles ?? ("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>"
            + "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">"
            + "<numFmts count=\"1\"><numFmt numFmtId=\"164\" formatCode=\"dd\\-mmm\\-yyyy\"/></numFmts>"
            + "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/><family val=\"2\"/></font></fonts>"
            + "<fills count=\"2\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill></fills>"
            + "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>"
            + "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>"
            + "<cellXfs count=\"3\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>"
            + "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyFont=\"1\"/>"
            + "<xf numFmtId=\"164\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\" applyNumberFormat=\"1\"/></cellXfs>"
            + "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>"
            + "</styleSheet>"));
        Put(zip, "xl/worksheets/sheet1.xml", sb.ToString());
    }

    [GeneratedRegex(@"^\d{4}-\d{2}-\d{2}$")] private static partial Regex IsoDate();

    // A value as a cell: a date, a whole number, or text. style: the template's format for the column, if any.
    static string Cell(string at, string v, int? style)
    {
        if (IsoDate().IsMatch(v) && DateTime.TryParse(v, out var d))
            return $"<c r=\"{at}\" s=\"{style ?? 2}\"><v>{(d - new DateTime(1899, 12, 30)).Days}</v></c>";
        var s = style is > 0 ? $" s=\"{style}\"" : "";
        if (v.Length < 10 && v.All(char.IsAsciiDigit) && !v.StartsWith('0')) return $"<c r=\"{at}\"{s}><v>{v}</v></c>";
        return Text(at, v, style ?? 0);
    }

    static string Text(string at, string v, int style) =>
        $"<c r=\"{at}\"{(style > 0 ? $" s=\"{style}\"" : "")} t=\"inlineStr\"><is><t xml:space=\"preserve\">{Esc(v)}</t></is></c>";

    static string Col(int c) => c < 26 ? ((char)('A' + c)).ToString() : Col(c / 26 - 1) + (char)('A' + c % 26);
    static string Esc(string s) => SecurityElement.Escape(new string(s.Where(ch => ch >= ' ' || ch is '\t' or '\n').ToArray()))!;
    static string SheetName(string s) { var n = new string(s.Where(ch => !"[]:*?/\\".Contains(ch)).ToArray()); return n.Length > 31 ? n[..31] : n; }

    static void Put(ZipArchive zip, string name, string text)
    {
        var e = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
        w.Write(text);
    }

    // The look of a template .xlsx: styles.xml, the first sheet's <cols>, and row 1's and row 2's style per column.
    sealed partial class Template
    {
        public string? Styles, Cols;
        readonly Dictionary<int, int> head = new(), data = new(), above = new();
        public int? HeadStyle(int c) => head.TryGetValue(c, out var s) ? s : null;
        public int? DataStyle(int c) => data.TryGetValue(c, out var s) ? s : null;
        public int? AboveStyle(int r) => above.TryGetValue(r, out var s) ? s : null;

        [GeneratedRegex(@"<cols>.*?</cols>", RegexOptions.Singleline)] private static partial Regex ColsRx();
        [GeneratedRegex(@"<row [^>]*\br=""(\d+)""[^>]*>(.*?)</row>", RegexOptions.Singleline)] private static partial Regex RowRx();
        [GeneratedRegex(@"<c\b([^>]*)>")] private static partial Regex CellRx();
        [GeneratedRegex(@"\br=""([A-Z]+)\d+""")] private static partial Regex RefRx();
        [GeneratedRegex(@"\bs=""(\d+)""")] private static partial Regex StyleRx();

        // headerRow: where the header is (the lines above it take their style from the template's column A there).
        public static Template Read(string path, int headerRow)
        {
            using var zip = ZipFile.OpenRead(path);
            string Text(string name) { using var r = new StreamReader(zip.GetEntry(name)!.Open()); return r.ReadToEnd(); }
            var t = new Template { Styles = Text("xl/styles.xml") };
            var sheet = Text("xl/worksheets/sheet1.xml");
            t.Cols = ColsRx().Match(sheet) is { Success: true } m ? m.Value : null;
            foreach (Match row in RowRx().Matches(sheet))
            {
                var rn = int.Parse(row.Groups[1].Value);
                var map = rn == headerRow ? t.head : rn == headerRow + 1 ? t.data : null;
                if (rn < headerRow - 1)
                {
                    // A line above the header: column A's style.
                    var a = CellRx().Matches(row.Groups[2].Value).Cast<Match>().FirstOrDefault(c => RefRx().Match(c.Groups[1].Value).Groups[1].Value == "A");
                    if (a != null) t.above[rn - 1] = StyleRx().Match(a.Groups[1].Value) is { Success: true } sm ? int.Parse(sm.Groups[1].Value) : 0;
                    continue;
                }
                if (map == null) continue;
                foreach (Match c in CellRx().Matches(row.Groups[2].Value))
                {
                    var at = RefRx().Match(c.Groups[1].Value);
                    if (!at.Success) continue;
                    var st = StyleRx().Match(c.Groups[1].Value);
                    map[at.Groups[1].Value.Aggregate(0, (a, ch) => a * 26 + ch - 'A' + 1) - 1] = st.Success ? int.Parse(st.Groups[1].Value) : 0;
                }
            }
            return t;
        }
    }
}
