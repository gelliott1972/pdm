using SolidWorks.Interop.swdocumentmgr;

namespace Pdm;

public sealed record PartNoEntry(string Config, string PartNo, string Status);

public sealed class DocInfo
{
    public List<string> References { get; } = new();
    public List<PartNoEntry> PartNos { get; } = new();
}

// Reads references and PartNo values with the SOLIDWORKS Document Manager (read-only, never saves).
// The licence key comes from the SW_DM_KEY environment variable and is never printed.
public sealed class DocReader
{
    readonly ISwDMApplication app;

    public DocReader()
    {
        var key = Environment.GetEnvironmentVariable("SW_DM_KEY");
        if (string.IsNullOrEmpty(key)) throw new InvalidOperationException("SW_DM_KEY is not set.");
        var factory = (ISwDMClassFactory)new SwDMClassFactory();
        app = factory.GetApplication(key) ?? throw new InvalidOperationException("Document Manager rejected the key.");
    }

    public static bool IsSolidWorks(string path) => TypeOf(path) != SwDmDocumentType.swDmDocumentUnknown;

    public DocInfo Read(string path)
    {
        var info = new DocInfo();
        var doc = app.GetDocument(path, TypeOf(path), true, out var err) as ISwDMDocument31
            ?? throw new InvalidOperationException($"Document Manager could not open {Path.GetFileName(path)}: {err}");
        try
        {
            info.References.AddRange(References(doc));

            var filePartNo = GetProp(doc, null, "PartNo");
            if (!string.IsNullOrEmpty(filePartNo))
                info.PartNos.Add(new PartNoEntry("", filePartNo, GetProp(doc, null, "PartNoStatus") ?? ""));
            // Drawings have no configurations: the Document Manager fails (E_FAIL) rather than returning none.
            var configs = TypeOf(path) == SwDmDocumentType.swDmDocumentDrawing ? [] : (string[])doc.ConfigurationManager.GetConfigurationNames();
            foreach (var cn in configs)
            {
                var partNo = GetProp(doc, cn, "PartNo");
                if (!string.IsNullOrEmpty(partNo))
                    info.PartNos.Add(new PartNoEntry(cn, partNo, GetProp(doc, cn, "PartNoStatus") ?? ""));
            }
            return info;
        }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    // The model a drawing's title block reads its properties from: sheet 1's "use custom property values from model
    // shown in" view (or its first view showing a model), as (model file name, configuration). Null when it has none.
    public (string Model, string Config)? TitleBlockModel(string drawing)
    {
        var doc = app.GetDocument(drawing, SwDmDocumentType.swDmDocumentDrawing, true, out var err) as ISwDMDocument10
            ?? throw new InvalidOperationException($"Document Manager could not open {Path.GetFileName(drawing)}: {err}");
        try
        {
            if ((doc.GetSheets() as object[])?.FirstOrDefault() is not ISwDMSheet4 sheet) return null;
            var views = ((sheet.GetViews() as object[]) ?? []).OfType<ISwDMView>().Where(v => !string.IsNullOrEmpty(v.ReferencedDocument)).ToList();
            string named = Convert.ToString(sheet.CustomPropertyView) ?? "";
            var v = views.FirstOrDefault(x => string.Equals(Convert.ToString(x.Name), named, StringComparison.OrdinalIgnoreCase)) ?? views.FirstOrDefault();
            if (v == null) return null;
            string model = Convert.ToString(v.ReferencedDocument) ?? "", config = Convert.ToString(v.ReferencedConfiguration) ?? "";
            return (Path.GetFileName(model.Replace('/', '\\')), config);
        }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    // A drawing's number of sheets.
    public int Sheets(string drawing)
    {
        var doc = app.GetDocument(drawing, SwDmDocumentType.swDmDocumentDrawing, true, out var err) as ISwDMDocument10
            ?? throw new InvalidOperationException($"Document Manager could not open {Path.GetFileName(drawing)}: {err}");
        try { return doc.GetSheetCount(); }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    // A custom property as a title block shows it: the configuration's value first, else the file's. Resolved values.
    public string? ModelProp(string model, string config, string name)
    {
        var doc = app.GetDocument(model, TypeOf(model), true, out var err) as ISwDMDocument31
            ?? throw new InvalidOperationException($"Document Manager could not open {Path.GetFileName(model)}: {err}");
        try
        {
            string? v = null;
            if (config != "") v = GetResolved(doc, config, name);
            if (string.IsNullOrEmpty(v)) v = GetResolved(doc, null, name);
            return string.IsNullOrEmpty(v) ? null : v;
        }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    static string? GetResolved(ISwDMDocument31 doc, string? config, string name)
    {
        try
        {
            if (config == null)
            {
                var n = ((doc.GetCustomPropertyNames() as string[]) ?? []).FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
                if (n == null) return null;
                return doc.GetCustomPropertyValues(n, out _, out _); // the evaluated value; the out is the expression
            }
            var cfg = (doc.ConfigurationManager.GetConfigurationByName(config) ?? doc.ConfigurationManager.GetConfigurationByName(config.Split('<')[0])) as ISwDMConfiguration18;
            var cn = ((cfg?.GetCustomPropertyNames() as string[]) ?? []).FirstOrDefault(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));
            if (cn == null) return null;
            return cfg!.GetCustomPropertyValues(cn, out _, out _);
        }
        catch { return null; }
    }

    // What a model is, as far as the Document Manager can see without SOLIDWORKS, one line each: its references (by
    // file name), configurations, custom properties (file and each configuration, evaluated), each configuration's
    // mass properties (as last saved; geometry; without them, null), and an assembly's components (file, configuration, suppressed,
    // position). Two versions with the same lines differ only in things a drawing doesn't show: a re-save
    // (Needs drawing, Grant 21 Nov). Null when something can't be read, so the caller doesn't guess.
    public List<string>? Fingerprint(string path)
    {
        var doc = app.GetDocument(path, TypeOf(path), true, out _) as ISwDMDocument31;
        if (doc == null) return null;
        try
        {
            var lines = new List<string>();
            lines.AddRange(References(doc).Select(r => "ref " + Path.GetFileName(r.Replace('/', '\\')).ToUpperInvariant()).Order(StringComparer.Ordinal));
            lines.AddRange(Props("", (out object n, out object t, out object l, out object v) => doc.GetAllCustomPropertyNamesAndValues(out n, out t, out l, out v)));
            foreach (var cn in ((string[])doc.ConfigurationManager.GetConfigurationNames()).Order(StringComparer.Ordinal))
            {
                if (doc.ConfigurationManager.GetConfigurationByName(cn) is not ISwDMConfiguration18 cfg) return null;
                lines.Add($"config {cn}");
                lines.AddRange(Props(cn, (out object n, out object t, out object l, out object v) => cfg.GetAllCustomPropertyNamesAndValues(out n, out t, out l, out v)));
                var mass = cfg.GetMassProperties(out var err) as double[];
                if (err != SwDmMassPropError.swDmMassPropErrorNone || mass == null) return null; // no geometry to compare: not decided
                lines.Add($"mass[{cn}] {Nums(mass)}");
                foreach (var c in ((cfg.GetComponents() as object[]) ?? []).OfType<ISwDMComponent10>()
                    .Select(c => $"comp[{cn}] {c.Name2} {Path.GetFileName(Convert.ToString(c.PathName)?.Replace('/', '\\') ?? "").ToUpperInvariant()} <{c.ConfigurationName}>"
                        + $"{(c.IsSuppressed() ? " suppressed" : "")} at {(c.Transform is double[] t ? Nums(t) : "?")}").Order(StringComparer.Ordinal))
                    lines.Add(c);
            }
            return lines;
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidCastException or InvalidOperationException) { return null; }
        finally { try { doc.CloseDoc(); } catch { } }
    }

    delegate void AllProps(out object names, out object types, out object linkedTo, out object values);

    static IEnumerable<string> Props(string config, AllProps read)
    {
        read(out var n, out _, out var l, out var v);
        var names = n as string[] ?? [];
        var linked = l as string[] ?? [];
        var values = v as string[] ?? [];
        return names.Select((x, i) => $"prop[{config}] {x} = {(i < linked.Length ? linked[i] : "")} -> {(i < values.Length ? values[i] : "")}").Order(StringComparer.Ordinal);
    }

    // Rounded, so the same geometry saved again reads the same (6 significant figures).
    static string Nums(double[] a) => string.Join(" ", a.Select(x => (Math.Abs(x) < 1e-9 ? 0 : x).ToString("G6", System.Globalization.CultureInfo.InvariantCulture)));

    // The resolved reference list (what SOLIDWORKS loads), without virtual components.
    IEnumerable<string> References(ISwDMDocument31 doc)
    {
        var search = app.GetSearchOptionObject();
        search.SearchFilters = (int)(SwDmSearchFilters.SwDmSearchForPart | SwDmSearchFilters.SwDmSearchForAssembly
            | SwDmSearchFilters.SwDmSearchForDrawing | SwDmSearchFilters.SwDmSearchExternalReference
            | SwDmSearchFilters.SwDmSearchInContextReference | SwDmSearchFilters.SwDmSearchPartToBaseAssemblyReference);
        var refs = doc.GetAllExternalReferences5(search, out _, out var isVirtual, out _, out var imported) as string[];
        var virt = isVirtual as bool[];
        var result = new List<string>();
        if (refs != null)
            for (int i = 0; i < refs.Length; i++)
                if (!(virt != null && i < virt.Length && virt[i]) && !string.IsNullOrEmpty(refs[i])) result.Add(refs[i]);
        if (imported is string[] imp) result.AddRange(imp.Where(p => !string.IsNullOrEmpty(p)));
        return result.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    static string? GetProp(ISwDMDocument31 doc, string? config, string name)
    {
        if (config == null)
            return Has(doc.GetCustomPropertyNames(), name) ? doc.GetCustomProperty2(name, out _) : null;
        ISwDMConfiguration18? cfg;
        try { cfg = doc.ConfigurationManager.GetConfigurationByName(config) as ISwDMConfiguration18; } catch { return null; }
        if (cfg == null || !Has(cfg.GetCustomPropertyNames(), name)) return null;
        return cfg.GetCustomProperty2(name, out _);
    }

    static bool Has(object names, string name) =>
        names is string[] a && a.Any(x => x.Equals(name, StringComparison.OrdinalIgnoreCase));

    static SwDmDocumentType TypeOf(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".sldprt" => SwDmDocumentType.swDmDocumentPart,
        ".sldasm" => SwDmDocumentType.swDmDocumentAssembly,
        ".slddrw" => SwDmDocumentType.swDmDocumentDrawing,
        _ => SwDmDocumentType.swDmDocumentUnknown
    };
}
