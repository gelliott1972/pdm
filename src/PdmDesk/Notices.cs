// Answer notices (Admin, 24 Nov): when Grant answers or discusses a ticket on the Desk, the owning session is told by
// a file in desk\notices\<session>\. The session's watch (desk-notices.ps1, run with Monitor) prints one line per notice
// and moves the file into seen\, so each notice is told once. Written whole (Store.Write), never in /api output.
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Desk;

public sealed partial class Notices(Store store, Settings settings)
{
    public string Folder => Path.Combine(store.Root, "notices");
    readonly List<string> problems = [];

    [GeneratedRegex("^[a-z0-9][a-z0-9-]{0,63}$")]
    private static partial Regex SessionPattern();

    // Notices that couldn't be written, for the page's problems line (the last five).
    public List<string> Problems() { lock (problems) return [.. problems]; }
    void Problem(string s) { lock (problems) { problems.Add(s); if (problems.Count > 5) problems.RemoveAt(0); } }

    // Tell the owner of ticket `id` about its new state ("answered" or "discuss"). Never throws: the answer is already
    // saved, so a notice that can't be written only goes on the problems line. `said` is the discuss comment.
    public void Tell(string id, JsonObject t, string by, string? said = null)
    {
        try
        {
            var status = (string?)t["status"] ?? "";
            if (status is not ("answered" or "discuss")) return;
            var owner = ((string?)t["owner"] ?? "").Trim();
            var session = (settings.NoticeRoutes.TryGetValue(owner, out var to) ? to : owner).ToLowerInvariant();
            if (!SessionPattern().IsMatch(session))
            {
                Problem($"Ticket {id}: no answer notice, because its owner \"{owner}\" isn't a session name.");
                return;
            }
            var title = (string?)t["title"] ?? "";
            var type = (string?)t["type"] ?? "";
            var answer = t["answer"] as JsonObject;
            string? choice = null, label = null, note;
            int? code = null;
            if (status == "discuss") note = said ?? "";
            else
            {
                choice = (string?)answer?["choice"] ?? "";
                note = (string?)answer?["note"] ?? "";
                code = answer?["code"] is JsonValue cv && cv.TryGetValue<int>(out var c) ? c : null;
                label = type switch
                {
                    "decision" => choice == "" ? "a note only"
                        : (t["options"] as JsonArray ?? []).OfType<JsonObject>().FirstOrDefault(o => (string?)o["key"] == choice)?["label"]?.ToString() ?? choice,
                    "gate" => settings.Outcome((string?)t["gate"], choice, code)?.Label ?? choice,
                    "draft-review" => choice switch { "accept" => "Accepted", "redo" => "Redo", _ => choice },
                    "sw-job" => choice switch { "done" => "Done", "problem" => "Problem", _ => choice },
                    _ => choice,
                };
            }
            var at = DateTime.UtcNow;
            var summary = status == "discuss"
                ? $"{id} ({title}): {by} wrote: {note}"
                : $"{id} ({title}) answered by {by}: {label}." + (note != "" ? $" Note: {note}" : "");
            summary = Regex.Replace(summary, @"\s+", " ").Trim();
            if (summary.Length > 400) summary = summary[..400] + "... (more in the notice file)";
            var n = new JsonObject
            {
                ["ticket"] = id, ["title"] = title, ["type"] = type, ["owner"] = owner, ["session"] = session,
                ["status"] = status, ["choice"] = choice, ["choiceLabel"] = label, ["code"] = code, ["note"] = note,
                ["by"] = by, ["at"] = at.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                ["screen"] = (string?)t["screen"], ["gate"] = (string?)t["gate"], ["summary"] = summary,
            };
            var dir = Path.Combine(Folder, session);
            Directory.CreateDirectory(dir);
            var stem = $"{at:yyyyMMddTHHmmssfff}Z-{id}";
            var file = Path.Combine(dir, stem + ".json");
            for (var i = 2; File.Exists(file) || File.Exists(Path.Combine(dir, "seen", Path.GetFileName(file))); i++)
                file = Path.Combine(dir, $"{stem}-{i}.json");
            store.Write(file, n);
        }
        catch (Exception ex)
        {
            Problem($"Ticket {id}: the answer notice couldn't be written ({ex.GetType().Name}).");
        }
    }

    // At start: seen notices more than 30 days old are deleted.
    public void Prune()
    {
        try
        {
            if (!Directory.Exists(Folder)) return;
            var cutoff = DateTime.UtcNow.AddDays(-30);
            foreach (var seen in Directory.GetDirectories(Folder).Select(d => Path.Combine(d, "seen")).Where(Directory.Exists))
                foreach (var f in Directory.GetFiles(seen, "*.json"))
                    if (File.GetLastWriteTimeUtc(f) < cutoff) try { File.Delete(f); } catch { }
        }
        catch { }
    }
}
