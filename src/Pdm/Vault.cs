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
