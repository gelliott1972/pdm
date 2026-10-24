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
