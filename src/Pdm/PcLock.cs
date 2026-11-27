using System.Text;

namespace Pdm;

// One PDM command at a time on this PC, for everything that writes the local root or state.json (Dev and Admin, 27 Nov:
// two "pdm get" runs overlapped, and the second's whole-file save of state.json dropped the first's new entries, so 14
// files then looked "changed on this PC"). Held as <root>\.pdm\pdm.lock opened for writing with no write sharing: the
// page and the command line both use it, and Windows lets go of it when a process ends, even one that crashed.
// The file holds who has it (command, process, since), so the one turned away can say what is running.
// Within one process it's counted: a command already holding it can take it again (the page runs one request at a time).
public sealed class PcLock : IDisposable
{
    static readonly object gate = new();
    static FileStream? held;
    static int depth;
    bool disposed;

    PcLock() { }

    static string FilePath(string root) => Path.Combine(root, ".pdm", "pdm.lock");

    // Takes the lock, waiting up to `wait`. Null, with why, when another PDM command still has it after that.
    public static PcLock? Take(string root, string what, TimeSpan wait, out string? why)
    {
        why = null;
        var deadline = DateTime.UtcNow + wait;
        while (true)
        {
            lock (gate)
            {
                if (held != null) { depth++; return new PcLock(); }
                try
                {
                    var path = FilePath(root);
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    var fs = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
                    var info = Encoding.UTF8.GetBytes($"{what}, process {Environment.ProcessId}, since {DateTime.Now:d MMM HH:mm:ss}");
                    fs.SetLength(0); fs.Write(info); fs.Flush();
                    held = fs; depth = 1;
                    return new PcLock();
                }
                catch (IOException) { } // another process has it
                catch (UnauthorizedAccessException) { }
            }
            if (DateTime.UtcNow >= deadline)
            {
                why = $"Not done: another PDM command is running on this PC ({Holder(root)}). Wait for it to finish, then try again.";
                return null;
            }
            Thread.Sleep(250);
        }
    }

    // What holds it, as written in the lock file.
    static string Holder(string root)
    {
        try
        {
            using var fs = new FileStream(FilePath(root), FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var r = new StreamReader(fs, Encoding.UTF8);
            var text = r.ReadToEnd().Trim();
            return text == "" ? "unknown" : text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return "unknown"; }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        lock (gate)
        {
            if (--depth > 0 || held == null) return;
            try { held.SetLength(0); } catch (IOException) { }
            held.Dispose();
            held = null;
        }
    }

    // For tests: whether this process holds it.
    public static bool HeldHere { get { lock (gate) return held != null; } }
}
