namespace Pdm;

// Paths whose file now lives elsewhere in the vault ("moved away"). A copy still at such a path on a PC is an old copy,
// never a new file. On 25 Nov James's page check-in added back 4 files Grant had moved that morning (APEX-A93): his PC
// still had the old copies, as Download latest hadn't followed the moves yet. Check-in and load both refuse such a path,
// and the page lists it as an old copy that can't be ticked.
// A path is moved away when no record holds it now and either a record's Moves[] has it as From, or this PC's
// state.json has it under a fileId whose record is now at another path.
public sealed class MovedAway
{
    public sealed record Hit(FileRecord Now, MoveEntry? Move);

    readonly HashSet<string> current;
    readonly Dictionary<string, Hit> byFrom = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, FileRecord> byId = new();
    readonly LocalState? state;

    public MovedAway(IEnumerable<FileRecord> all, LocalState? state)
    {
        var list = all.ToList();
        current = list.Select(r => r.Path).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var r in list)
        {
            byId.TryAdd(r.FileId, r);
            foreach (var m in r.Moves.Where(m => !m.From.Equals(r.Path, StringComparison.OrdinalIgnoreCase)))
                if (!byFrom.TryGetValue(m.From, out var had) || string.CompareOrdinal(had.Move?.At, m.At) < 0)
                    byFrom[m.From] = new Hit(r, m); // two files once at one path: the later move tells where it went
        }
        this.state = state;
    }

    // Where the file once at key is now, or null if key isn't a moved-away path (a path some record holds now never is).
    public Hit? Find(string key)
    {
        if (current.Contains(key)) return null;
        if (byFrom.TryGetValue(key, out var hit)) return hit;
        if (state != null && state.Files.TryGetValue(key, out var e) && byId.TryGetValue(e.FileId, out var now) && !now.Path.Equals(key, StringComparison.OrdinalIgnoreCase))
            return new Hit(now, null);
        return null;
    }

    public static string Where(Hit h) => h.Now.Path + (h.Now.Removed ? " (since removed from the vault)" : "");
    public static string Who(Hit h) => h.Move != null ? $"{h.Move.By}, {Engine.Local(h.Move.At)}" : $"{h.Now.UpdatedBy}, {Engine.Local(h.Now.UpdatedAt)}";

    // Dev's wording (27 Nov): the check-in's refusal.
    public static string Refusal(string key, Hit h) =>
        $"Not added: {key} was moved in the vault to {Where(h)} ({Who(h)}). Download latest tidies your copy; if it really is a new file, give it a new name.";
}
