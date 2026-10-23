using System.Text.Json;

namespace Pdm;

// Ids of the AWS resources and the local root. None of these are secrets.
public sealed class Settings
{
    public string Region { get; set; } = "";
    public string Bucket { get; set; } = "";
    public string FilesTable { get; set; } = "";
    public string PartNosTable { get; set; } = "";
    public string UserPoolId { get; set; } = "";
    public string AppClientId { get; set; } = "";
    public string IdentityPoolId { get; set; } = "";
    public string LocalRoot { get; set; } = "";
    public int PagePort { get; set; } = 5170;

    public static Settings Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "pdm.settings.json");
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<Settings>(File.ReadAllText(path), options)
            ?? throw new InvalidOperationException("pdm.settings.json is empty.");
    }
}
