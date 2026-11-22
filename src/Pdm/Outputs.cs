using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.S3;
using Amazon.S3.Model;

namespace Pdm;

// PDM Outputs (brief docs\briefs\outputs.md, Grant 22 Nov): files made FROM the vault (square-edge STEPs, Revit
// wireframes, engineering calcs), kept in their own bucket (Settings.OutputsBucket), outside the vault's file records
// and never on P:\. Only the admin adds (CLI, pdm-admin SSO); everyone reads. Admin's answers, 22 Nov:
//   one set per screen, pin and kind: <screen>/<pin>/<kind>/<files> and its record, <screen>/<pin>/<kind>/.pdm-set.json
//   kinds STEP, Revit, Calc; screens LS-<2 digits><shaft letters>-<2 digits>; a set is closed once added (never
//   overwritten or added to); removing one only marks its record (who, when, why), nothing is deleted.
// The pin is a UTC time (2026-11-21T07:51:56Z); in keys and folders it's written without colons (2026-11-21T075156Z).
public sealed class Outputs(IAmazonS3 s3, string bucket)
{
    public const string RecordName = ".pdm-set.json";
    public static readonly string[] Kinds = ["STEP", "Revit", "Calc"];
    static readonly Regex ScreenPattern = new(@"^LS-\d{2}[A-Z]+-\d{2}$", RegexOptions.CultureInvariant);
    static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };

    public sealed record OutputFile(string Name, long Size, string Md5);
    public sealed class OutputSet
    {
        public string Screen { get; set; } = "";
        public string Pin { get; set; } = "";       // 2026-11-21T07:51:56Z
        public string Kind { get; set; } = "";
        public string Note { get; set; } = "";
        public string AddedBy { get; set; } = "";
        public string AddedAt { get; set; } = "";
        public List<OutputFile> Files { get; set; } = new();
        public string? RemovedAt { get; set; }
        public string? RemovedBy { get; set; }
        public string? RemovedReason { get; set; }
        public string Prefix => $"{Screen}/{PinFolder(Pin)}/{Kind}/";
    }

    // Checks and tidies what's typed: an error, or null. Screen and kind come back in their one spelling.
    public static string? CheckScreen(ref string screen)
    {
        var s = screen.Trim().ToUpperInvariant();
        if (!ScreenPattern.IsMatch(s)) return $"\"{screen}\" isn't a screen name: LS-<2 digits><shaft letters>-<2 digits>, e.g. LS-08A-11 or LS-08BC-06.";
        screen = s;
        return null;
    }

    public static string? CheckKind(ref string kind)
    {
        var k = kind.Trim();
        var known = Kinds.FirstOrDefault(x => x.Equals(k, StringComparison.OrdinalIgnoreCase));
        if (known == null) return $"\"{kind}\" isn't a kind: {string.Join(", ", Kinds)}.";
        kind = known;
        return null;
    }

    // A pin: UTC, to the second, not in the future. Accepts 2026-11-21T07:51:56Z or the folder form 2026-11-21T075156Z.
    public static string? CheckPin(ref string pin)
    {
        var p = pin.Trim();
        if (!DateTime.TryParseExact(p, ["yyyy-MM-ddTHH:mm:ssZ", "yyyy-MM-ddTHHmmssZ"], CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var t))
            return $"\"{pin}\" isn't a pin: a UTC time like 2026-11-21T07:51:56Z.";
        if (t > DateTime.UtcNow.AddMinutes(5)) return $"The pin {pin} is in the future.";
        pin = t.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
        return null;
    }

    public static string PinFolder(string pin) => pin.Replace(":", "");

    // Adds a set: checks everything first, uploads the files (each checked against its MD5), then writes the record
    // only if there's none (so two adds of the same set can't both win). A set with files but no record is an add that
    // stopped part way: nothing lists it, and the next add of that set stops and says so.
    public async Task<OutputSet?> AddAsync(string screen, string pin, string kind, IReadOnlyList<string> files, string note, string by, bool dryRun, Action<string> say)
    {
        foreach (var err in new[] { CheckScreen(ref screen), CheckPin(ref pin), CheckKind(ref kind) })
            if (err != null) { say(err); return null; }
        note = note.Trim();
        if (note == "") { say("Give a note (--note \"...\"): what this set is and how it was made."); return null; }
        if (files.Count == 0) { say("Name the files to add."); return null; }
        var full = files.Select(Path.GetFullPath).ToList();
        foreach (var f in full)
        {
            if (Directory.Exists(f)) { say($"{f} is a folder: name the files in it."); return null; }
            if (!File.Exists(f)) { say($"Not found: {f}"); return null; }
            if (Path.GetFileName(f).Equals(RecordName, StringComparison.OrdinalIgnoreCase)) { say($"{f}: that name is kept for the set's record."); return null; }
            if (f.StartsWith(@"P:\", StringComparison.OrdinalIgnoreCase)) { say($"{f} is on P:\\. Outputs are files made from the vault, not vault files."); return null; }
        }
        var twice = full.GroupBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase).FirstOrDefault(g => g.Count() > 1);
        if (twice != null) { say($"Two files are named {twice.Key}: a set holds each name once."); return null; }

        var set = new OutputSet { Screen = screen, Pin = pin, Kind = kind, Note = note, AddedBy = by };
        var there = await KeysAsync(set.Prefix);
        if (there.Count > 0)
        {
            say(there.Contains(set.Prefix + RecordName)
                ? $"Not added: {screen} {pin} {kind} is already there (added by {(await ReadAsync(set.Prefix + RecordName))?.AddedBy}). A set is never changed; a new pin makes a new set."
                : $"Not added: {set.Prefix} has files but no record (an add that stopped part way). Tell Grant; nothing was changed.");
            return null;
        }
        foreach (var f in full) set.Files.Add(new OutputFile(Path.GetFileName(f), new FileInfo(f).Length, Vault.Md5(f)));
        say($"{(dryRun ? "Would add" : "Adding")} {screen} / pin {pin} / {kind}: {set.Files.Count} file(s), {Size(set.Files.Sum(x => x.Size))}");
        foreach (var x in set.Files) say($"  {x.Name}  {Size(x.Size)}");
        if (dryRun) { say("Dry run: nothing added."); return set; }

        foreach (var f in full)
        {
            var name = Path.GetFileName(f);
            var md5 = set.Files.First(x => x.Name == name).Md5;
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = set.Prefix + name, FilePath = f,
                MD5Digest = Convert.ToBase64String(Convert.FromHexString(md5)) // S3 refuses it if the bytes arrive different
            });
            say($"  uploaded {name}");
        }
        set.AddedAt = Index.Now();
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = set.Prefix + RecordName, ContentBody = JsonSerializer.Serialize(set, Json),
                ContentType = "application/json", IfNoneMatch = "*"
            });
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is System.Net.HttpStatusCode.PreconditionFailed or System.Net.HttpStatusCode.Conflict)
        {
            say($"Not added: someone added {screen} {pin} {kind} at the same moment. Its files may now be this PC's; tell Grant.");
            return null;
        }
        say($"Added {screen} / pin {pin} / {kind}.");
        return set;
    }

    // Every set's record (removed ones too: the caller decides), newest pin first within each screen.
    public async Task<List<OutputSet>> ListAsync(string? screen = null)
    {
        var keys = (await KeysAsync(screen == null ? "" : screen + "/")).Where(k => k.EndsWith("/" + RecordName, StringComparison.Ordinal)).ToList();
        var sets = new List<OutputSet>();
        await Parallel.ForEachAsync(keys, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (k, _) =>
        {
            if (await ReadAsync(k) is { } s) lock (sets) sets.Add(s);
        });
        return sets.OrderBy(s => s.Screen, StringComparer.Ordinal).ThenByDescending(s => s.Pin, StringComparer.Ordinal).ThenBy(s => Array.IndexOf(Kinds, s.Kind)).ToList();
    }

    // Marks a set removed (admin): its record says who, when and why; the files stay. Only if the record is still the
    // one just read.
    public async Task<OutputSet?> RemoveAsync(string screen, string pin, string kind, string reason, string by, bool dryRun, Action<string> say)
    {
        foreach (var err in new[] { CheckScreen(ref screen), CheckPin(ref pin), CheckKind(ref kind) })
            if (err != null) { say(err); return null; }
        reason = reason.Trim();
        if (reason == "") { say("Give a reason (--reason \"...\")."); return null; }
        var key = $"{screen}/{PinFolder(pin)}/{kind}/{RecordName}";
        GetObjectResponse r;
        try { r = await s3.GetObjectAsync(bucket, key); }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { say($"No set {screen} {pin} {kind}."); return null; }
        OutputSet set;
        string etag;
        using (r)
        using (var reader = new StreamReader(r.ResponseStream))
        {
            set = JsonSerializer.Deserialize<OutputSet>(await reader.ReadToEndAsync(), Json)!;
            etag = r.ETag;
        }
        if (set.RemovedAt != null) { say($"{screen} {pin} {kind} was already removed by {set.RemovedBy} ({set.RemovedReason})."); return null; }
        if (dryRun) { say($"Would remove {screen} / pin {pin} / {kind} ({reason}). Dry run: nothing changed."); return set; }
        (set.RemovedAt, set.RemovedBy, set.RemovedReason) = (Index.Now(), by, reason);
        try
        {
            await s3.PutObjectAsync(new PutObjectRequest
            {
                BucketName = bucket, Key = key, ContentBody = JsonSerializer.Serialize(set, Json), ContentType = "application/json", IfMatch = etag
            });
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode is System.Net.HttpStatusCode.PreconditionFailed or System.Net.HttpStatusCode.Conflict)
        {
            say("Not removed: the set's record changed meanwhile. Try again.");
            return null;
        }
        say($"Removed {screen} / pin {pin} / {kind} ({reason}). Its files stay in the bucket.");
        return set;
    }

    // One file of a set, to a local path (through a temporary file), checked against the set's MD5.
    public async Task DownloadAsync(OutputSet set, OutputFile file, string localPath)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);
        var temp = localPath + ".pdm-download";
        using (var r = await s3.GetObjectAsync(bucket, set.Prefix + file.Name))
        await using (var f = File.Create(temp))
            await r.ResponseStream.CopyToAsync(f);
        if (!Vault.Md5(temp).Equals(file.Md5, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(temp);
            throw new IOException($"{file.Name} arrived different from what was added (MD5). Nothing was kept; try again.");
        }
        if (File.Exists(localPath)) File.SetAttributes(localPath, File.GetAttributes(localPath) & ~FileAttributes.ReadOnly);
        File.Move(temp, localPath, overwrite: true);
    }

    async Task<OutputSet?> ReadAsync(string key)
    {
        try
        {
            using var r = await s3.GetObjectAsync(bucket, key);
            using var reader = new StreamReader(r.ResponseStream);
            return JsonSerializer.Deserialize<OutputSet>(await reader.ReadToEndAsync(), Json);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound) { return null; }
        catch (JsonException) { return null; }
    }

    async Task<List<string>> KeysAsync(string prefix)
    {
        var keys = new List<string>();
        var req = new ListObjectsV2Request { BucketName = bucket, Prefix = prefix };
        ListObjectsV2Response r;
        do
        {
            r = await s3.ListObjectsV2Async(req);
            keys.AddRange((r.S3Objects ?? []).Select(o => o.Key));
            req.ContinuationToken = r.NextContinuationToken;
        } while (r.IsTruncated == true);
        return keys;
    }

    // The Dropbox copy (stage 3; Admin's answers, 22 Nov): the PDM is the master, the mirror a read-only copy with the
    // tab's layout, <mirror>\<screen>\<pin>\<kind>\<files>. Writes only inside the mirror folder (which must already
    // exist) and never deletes a file: a removed set's copy is moved to <mirror>\_removed\<screen>\<pin>\<kind>\.
    // Files in the mirror that no set has are left alone and listed. only: just this set (after an add or remove).
    public async Task<int> MirrorAsync(string mirror, OutputSet? only, bool dryRun, Action<string> say)
    {
        if (mirror == "") { say("No mirror folder set (outputsMirror in pdm.settings.json): nothing copied."); return 0; }
        var root = Path.GetFullPath(Environment.ExpandEnvironmentVariables(mirror)).TrimEnd('\\');
        if (!Directory.Exists(root)) { say($"The mirror folder {root} isn't on this PC: nothing copied."); return 0; }
        string Inside(params string[] parts)
        {
            var full = Path.GetFullPath(Path.Combine([root, .. parts]));
            if (!full.StartsWith(root + "\\", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException($"{full} is outside the mirror folder.");
            return full;
        }
        var sets = only != null ? [only] : await ListAsync();
        int copied = 0, same = 0, moved = 0;
        foreach (var set in sets)
        {
            var folder = Inside(set.Screen, PinFolder(set.Pin), set.Kind);
            if (set.RemovedAt != null)
            {
                if (!Directory.Exists(folder)) continue;
                var to = Inside("_removed", set.Screen, PinFolder(set.Pin), set.Kind);
                if (Directory.Exists(to)) { say($"  Not moved: {folder} (removed), as {to} is already there. Look at both."); continue; }
                say($"  {(dryRun ? "would move" : "moved")} {set.Screen} {set.Pin} {set.Kind} (removed: {set.RemovedReason}) to _removed");
                if (!dryRun)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(to)!);
                    Directory.Move(folder, to);
                    foreach (var up in new[] { Path.GetDirectoryName(folder)!, Path.GetDirectoryName(Path.GetDirectoryName(folder)!)! }) // the pin and screen folders, if now empty
                        if (Directory.Exists(up) && !Directory.EnumerateFileSystemEntries(up).Any()) Directory.Delete(up);
                }
                moved++;
                continue;
            }
            foreach (var f in set.Files)
            {
                var local = Inside(set.Screen, PinFolder(set.Pin), set.Kind, f.Name);
                if (File.Exists(local) && new FileInfo(local).Length == f.Size && Vault.Md5(local).Equals(f.Md5, StringComparison.OrdinalIgnoreCase)) { same++; continue; }
                say($"  {(dryRun ? "would copy" : "copied")} {set.Screen} {set.Pin} {set.Kind} {f.Name}{(File.Exists(local) ? " (the copy there was different)" : "")}");
                if (!dryRun)
                {
                    await DownloadAsync(set, f, local);
                    File.SetAttributes(local, File.GetAttributes(local) | FileAttributes.ReadOnly);
                }
                copied++;
            }
        }
        if (only == null)
        {
            var known = sets.Where(x => x.RemovedAt == null).SelectMany(x => x.Files.Select(f => Inside(x.Screen, PinFolder(x.Pin), x.Kind, f.Name))).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removedDir = Inside("_removed") + "\\";
            foreach (var stray in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
                .Where(f => !known.Contains(f) && !f.StartsWith(removedDir, StringComparison.OrdinalIgnoreCase) && !f.EndsWith(".pdm-download", StringComparison.OrdinalIgnoreCase)))
                say($"  Not from the PDM (left alone): {stray}");
        }
        say($"Mirror {root}: {(dryRun ? "would copy" : "copied")} {copied} file(s), {same} already the same, {(dryRun ? "would move" : "moved")} {moved} removed set(s).{(dryRun ? " Dry run: nothing written." : "")}");
        return copied + moved;
    }

    public static string Size(long bytes) => bytes >= 1 << 20 ? $"{bytes / 1048576.0:0.0} MB" : bytes >= 1024 ? $"{bytes / 1024.0:0} KB" : $"{bytes} B";
}
