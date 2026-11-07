// The Desk's files: plain JSON under one folder, so every session can read and write them.
// Each file is written whole (temp file, then rename), so nobody reads half a file.
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Desk;

public partial class Store(string root)
{
    public string Root => root;
    public string Tickets => Path.Combine(root, "tickets");
    public string Status => Path.Combine(root, "status");
    public string Inbox => Path.Combine(root, "inbox");
    public string Activity => Path.Combine(root, "activity"); // written by each session's Claude Code hooks
    public string Sw => Path.Combine(root, "sw");             // lock.json (who has SOLIDWORKS), pause.json (Grant wants it)
    public readonly object Lock = new();

    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }; // readable files: & and ` stay as typed

    public void Ensure()
    {
        foreach (var d in new[] { Tickets, Status, Inbox }) Directory.CreateDirectory(d);
    }

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}$")]
    private static partial Regex IdPattern();
    public static bool IdOk(string? id) => id != null && IdPattern().IsMatch(id) && !id.Contains("..");

    public static string Now() => DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ");

    // Every readable *.json in a folder; files that can't be read are reported, not dropped silently.
    public List<JsonObject> All(string dir, List<string> problems)
    {
        var list = new List<JsonObject>();
        if (!Directory.Exists(dir)) return list;
        foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
        {
            var o = Read(file, out var why);
            if (o != null) { o["_file"] = Path.GetFileNameWithoutExtension(file); list.Add(o); }
            else problems.Add($"{Path.GetFileName(dir)}/{Path.GetFileName(file)}: {why}");
        }
        return list;
    }

    public JsonObject? Read(string file, out string why)
    {
        why = "";
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var f = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var r = new StreamReader(f);
                var node = JsonNode.Parse(r.ReadToEnd(), documentOptions: new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
                if (node is JsonObject o) return o;
                why = "not a JSON object";
                return null;
            }
            catch (FileNotFoundException) { why = "gone"; return null; }
            catch (Exception ex) when (attempt < 3) { why = ex.Message; Thread.Sleep(60); }
            catch (Exception ex) { why = ex is JsonException ? "not valid JSON" : ex.Message; return null; }
        }
    }

    public void Write(string file, JsonNode value)
    {
        var tmp = $"{file}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, value.ToJsonString(Pretty));
        for (var attempt = 0; ; attempt++)
        {
            try { File.Move(tmp, file, overwrite: true); return; }
            catch (IOException) when (attempt < 20) { Thread.Sleep(50); } // a reader without delete-sharing has it open
            catch { try { File.Delete(tmp); } catch { } throw; }
        }
    }

    // A file that may not exist: null when it doesn't; a problem only when it exists but can't be read.
    public JsonObject? ReadOptional(string file, List<string> problems)
    {
        if (!File.Exists(file)) return null;
        var o = Read(file, out var why);
        if (o == null && why != "gone") problems.Add($"{Path.GetFileName(Path.GetDirectoryName(file))}/{Path.GetFileName(file)}: {why}");
        return o;
    }

    public string TicketFile(string id) => Path.Combine(Tickets, id + ".json");
}
