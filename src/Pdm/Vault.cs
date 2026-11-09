using System.Security.Cryptography;
using Amazon.S3;
using Amazon.S3.Model;

namespace Pdm;

// The S3 bucket. Keys mirror the P: tree with forward slashes; each object carries its fileId.
public sealed class Vault(IAmazonS3 s3, Settings s)
{
    public const string FileIdMeta = "x-amz-meta-pdm-file-id";

    // Uploads a file and returns its new S3 version id and ETag (the MD5, as single-part uploads are).
    public async Task<(string VersionId, string ETag)> UploadAsync(string localPath, string key, string fileId)
    {
        var request = new PutObjectRequest { BucketName = s.Bucket, Key = key, FilePath = localPath };
        request.Metadata.Add(FileIdMeta, fileId);
        var r = await s3.PutObjectAsync(request);
        return (r.VersionId, r.ETag.Trim('"'));
    }

    // Small text objects under .pdm/ (the activity log): written, listed and read whole.
    public Task PutTextAsync(string key, string text) =>
        s3.PutObjectAsync(new PutObjectRequest { BucketName = s.Bucket, Key = key, ContentBody = text, ContentType = "application/json" });

    public async Task<List<string>> ListKeysAsync(string prefix)
    {
        var keys = new List<string>();
        var req = new ListObjectsV2Request { BucketName = s.Bucket, Prefix = prefix };
        ListObjectsV2Response r;
        do
        {
            r = await s3.ListObjectsV2Async(req);
            keys.AddRange((r.S3Objects ?? []).Select(o => o.Key));
            req.ContinuationToken = r.NextContinuationToken;
        } while (r.IsTruncated == true);
        return keys;
    }

    public async Task<string> GetTextAsync(string key)
    {
        using var r = await s3.GetObjectAsync(s.Bucket, key);
        using var reader = new StreamReader(r.ResponseStream);
        return await reader.ReadToEndAsync();
    }

    // Every object version in the bucket (not delete markers), for the activity backfill.
    public async Task<List<(string Key, string VersionId, DateTime At)>> AllVersionsAsync()
    {
        var list = new List<(string, string, DateTime)>();
        var req = new ListVersionsRequest { BucketName = s.Bucket };
        ListVersionsResponse r;
        do
        {
            r = await s3.ListVersionsAsync(req);
            foreach (var v in r.Versions ?? [])
                if (v.IsDeleteMarker != true) list.Add((v.Key, v.VersionId, (v.LastModified ?? DateTime.MinValue).ToUniversalTime()));
            req.KeyMarker = r.NextKeyMarker; req.VersionIdMarker = r.NextVersionIdMarker;
        } while (r.IsTruncated == true);
        return list;
    }

    // Copies one version to a new key inside the bucket (a move): nothing goes through this PC. Keeps the fileId metadata.
    public async Task<(string VersionId, string ETag)> CopyAsync(string fromKey, string versionId, string toKey)
    {
        var r = await s3.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = s.Bucket, SourceKey = fromKey, SourceVersionId = versionId,
            DestinationBucket = s.Bucket, DestinationKey = toKey, MetadataDirective = S3MetadataDirective.COPY
        });
        return (r.VersionId, r.ETag.Trim('"'));
    }

    // The oldest app version allowed to change files or download (Admin sets it after a change older versions
    // can't follow, such as moves). Kept in the bucket at .pdm/min-version.txt, a key no vault file can have.
    public const string MinVersionKey = ".pdm/min-version.txt";
    public async Task<string?> MinVersionAsync()
    {
        try
        {
            using var r = await s3.GetObjectAsync(s.Bucket, MinVersionKey);
            using var reader = new StreamReader(r.ResponseStream);
            return (await reader.ReadToEndAsync()).Trim();
        }
        catch (AmazonS3Exception) { return null; } // none set (or not readable): no gate
    }

    // True when this version is older than the minimum. A build from source (0.0.0) is never stopped.
    public static bool TooOld(string version, string? min) =>
        version != "0.0.0" && System.Version.TryParse(version, out var v) && System.Version.TryParse(min, out var m) && v < m;

    // Downloads one version to a temporary file, then swaps it in, so a failed download never leaves half a file.
    public async Task DownloadAsync(string key, string versionId, string localPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        var temp = localPath + ".pdm-download";
        using (var r = await s3.GetObjectAsync(new GetObjectRequest { BucketName = s.Bucket, Key = key, VersionId = versionId }))
        await using (var file = File.Create(temp))
            await r.ResponseStream.CopyToAsync(file);
        if (File.Exists(localPath)) File.SetAttributes(localPath, File.GetAttributes(localPath) & ~FileAttributes.ReadOnly);
        File.Move(temp, localPath, overwrite: true);
    }

    // Shares read and write, so a file SOLIDWORKS has open for editing can still be read.
    // Remembered while the file's size and time stay the same, so the 30 s refresh doesn't re-read every changed file.
    public static string Md5(string path)
    {
        var info = new FileInfo(path);
        if (Hashes.TryGetValue(path, out var h) && h.Size == info.Length && h.Time == info.LastWriteTimeUtc) return h.Md5;
        using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        var md5 = Convert.ToHexString(MD5.HashData(stream)).ToLowerInvariant();
        Hashes[path] = (info.Length, info.LastWriteTimeUtc, md5);
        return md5;
    }
    static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (long Size, DateTime Time, string Md5)> Hashes = new(StringComparer.OrdinalIgnoreCase);
}
