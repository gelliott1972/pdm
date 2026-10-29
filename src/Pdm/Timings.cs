using System.Collections.Concurrent;
using System.Diagnostics;

namespace Pdm;

// Seconds spent per kind of work (index reads, uploads, Document Manager...), so a slow check-in shows where the time went.
// Work done in parallel adds up to more than the time it took.
public sealed class Timings
{
    readonly ConcurrentDictionary<string, double> seconds = new();

    public async Task<T> Of<T>(string what, Func<Task<T>> work)
    {
        var watch = Stopwatch.StartNew();
        try { return await work(); }
        finally { Add(what, watch); }
    }

    public async Task Of(string what, Func<Task> work)
    {
        var watch = Stopwatch.StartNew();
        try { await work(); }
        finally { Add(what, watch); }
    }

    public T Of<T>(string what, Func<T> work)
    {
        var watch = Stopwatch.StartNew();
        try { return work(); }
        finally { Add(what, watch); }
    }

    void Add(string what, Stopwatch watch) => seconds.AddOrUpdate(what, watch.Elapsed.TotalSeconds, (_, s) => s + watch.Elapsed.TotalSeconds);

    public Dictionary<string, double> Snapshot() => seconds.ToDictionary(kv => kv.Key, kv => Math.Round(kv.Value, 2));
}
