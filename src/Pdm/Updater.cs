using Amazon.S3;
using Amazon.S3.Model;
using Velopack;
using Velopack.Logging;
using Velopack.Sources;

namespace Pdm;

// Velopack releases kept in a private S3 bucket, read with the logged-in user's credentials.
// The bucket holds what "vpk pack" makes: releases.<channel>.json and the .nupkg files it lists.
public sealed class S3UpdateSource(IAmazonS3 s3, string bucket) : IUpdateSource
{
    public async Task<VelopackAssetFeed> GetReleaseFeed(IVelopackLogger logger, string? appId, string channel, Guid? stagingId = null, VelopackAsset? latestLocalRelease = null)
    {
        using var r = await s3.GetObjectAsync(bucket, $"releases.{(string.IsNullOrEmpty(channel) ? "win" : channel)}.json");
        using var reader = new StreamReader(r.ResponseStream);
        return VelopackAssetFeed.FromJson(await reader.ReadToEndAsync());
    }

    public async Task DownloadReleaseEntry(IVelopackLogger logger, VelopackAsset releaseEntry, string localFile, Action<int> progress, CancellationToken cancelToken = default)
    {
        using var r = await s3.GetObjectAsync(new GetObjectRequest { BucketName = bucket, Key = releaseEntry.FileName }, cancelToken);
        await using var output = File.Create(localFile);
        var buffer = new byte[1 << 16];
        long done = 0, total = r.ContentLength > 0 ? r.ContentLength : releaseEntry.Size;
        int n;
        while ((n = await r.ResponseStream.ReadAsync(buffer, cancelToken)) > 0)
        {
            await output.WriteAsync(buffer.AsMemory(0, n), cancelToken);
            done += n;
            if (total > 0) progress((int)(done * 100 / total));
        }
    }
}

// Checks for a newer release after login and downloads it in the background.
// The update is only applied when the user clicks Restart on the page.
public sealed class Updater
{
    UpdateManager? manager;
    VelopackAsset? ready;
    Task? running;

    // Set by the publish script (-p:Version=...); a plain build says 0.0.0.
    public static string Version =>
        System.Reflection.CustomAttributeExtensions.GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>(typeof(Updater).Assembly)
            ?.InformationalVersion.Split('+')[0] ?? "0.0.0";

    public string? ReadyVersion => ready?.Version?.ToString();

    public void Start(IAmazonS3 s3, Settings s, Action<string> say)
    {
        // Checks again at each login, unless a check is still running or an update is already waiting.
        if (running is { IsCompleted: false } || ready != null || string.IsNullOrEmpty(s.ReleasesBucket)) return;
        manager = new UpdateManager(new S3UpdateSource(s3, s.ReleasesBucket));
        if (!manager.IsInstalled) { manager = null; return; } // a build from source, not an installed copy
        running = Task.Run(async () =>
        {
            try
            {
                var info = await manager.CheckForUpdatesAsync();
                if (info == null) return;
                await manager.DownloadUpdatesAsync(info);
                ready = info.TargetFullRelease;
                say($"Update {ReadyVersion} downloaded; restart from the page to use it.");
            }
            catch (Exception ex)
            {
                say($"Update check failed: {ex.Message}");
            }
        });
    }

    // Closes this app, swaps in the new version and starts it again (without opening another browser tab).
    public void ApplyAndRestart()
    {
        if (manager == null || ready == null) return;
        manager.ApplyUpdatesAndRestart(ready, ["page", "--no-open"]);
    }
}
