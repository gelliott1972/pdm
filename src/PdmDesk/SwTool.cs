// Runs Dev's SwCmd for sw-job tickets. Only three fixed commands (agreed with Dev, 7 Nov): close-all (never --exit),
// open <file> [--config <name>], status. Arguments are passed as a list, never through a shell.
using System.Diagnostics;
using System.Text;

namespace Desk;

public record SwResult(int Code, string Output, bool TimedOut);

public class SwTool
{
    public string Exe { get; } = Environment.GetEnvironmentVariable("PDM_SWCMD") is { Length: > 0 } e ? e : @"C:\code\pdm\tools\SwCmd\bin\SwCmd.exe";
    readonly SemaphoreSlim one = new(1, 1); // one SOLIDWORKS job at a time

    public bool TryBegin() => one.Wait(0);
    public void End() => one.Release();

    static readonly string[] Kinds = [".sldprt", ".sldasm", ".slddrw"];

    // The Desk's own checks on a ticket's file before SwCmd sees it (SwCmd has its own rules too).
    public static string? Refusal(string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return "a file has no path";
        if (!Path.IsPathFullyQualified(path)) return $"not a full path: {path}";
        if (!Kinds.Contains(Path.GetExtension(path).ToLowerInvariant())) return $"not a SOLIDWORKS file: {path}";
        if (path.Replace('/', '\\').Split('\\').Any(p => p.Equals(".pdm", StringComparison.OrdinalIgnoreCase))) return $"inside .pdm: {path}";
        if (!File.Exists(path)) return $"no such file: {path}";
        return null;
    }

    public Task<SwResult> CloseAll(TimeSpan timeout) => Run(["close-all"], timeout);
    public Task<SwResult> Status(TimeSpan timeout) => Run(["status"], timeout);
    public Task<SwResult> Open(string path, string? configuration, TimeSpan timeout) =>
        Run(string.IsNullOrWhiteSpace(configuration) ? ["open", path] : ["open", path, "--config", configuration], timeout);

    async Task<SwResult> Run(string[] args, TimeSpan timeout)
    {
        if (!File.Exists(Exe)) return new SwResult(2, $"SwCmd isn't built on this PC ({Exe}). See the build line at the top of tools\\SwCmd\\SwCmd.cs.", false);
        var psi = new ProcessStartInfo(Exe) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var a in args) psi.ArgumentList.Add(a);
        var output = new StringBuilder();
        using var p = new Process { StartInfo = psi };
        p.OutputDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        p.ErrorDataReceived += (_, e) => { if (e.Data != null) lock (output) output.AppendLine(e.Data); };
        try { p.Start(); }
        catch (Exception ex) { return new SwResult(2, $"Couldn't start SwCmd: {ex.Message}", false); }
        p.BeginOutputReadLine(); p.BeginErrorReadLine();
        using var cts = new CancellationTokenSource(timeout);
        try { await p.WaitForExitAsync(cts.Token); }
        catch (OperationCanceledException)
        {
            try { p.Kill(entireProcessTree: false); } catch { } // stops SwCmd only; SOLIDWORKS is left alone
            lock (output) return new SwResult(3, output + $"Timed out after {timeout.TotalMinutes:0} minutes; SOLIDWORKS was left as it is.", true);
        }
        p.WaitForExit(); // flush the output events
        lock (output) return new SwResult(p.ExitCode, output.ToString(), false);
    }
}
