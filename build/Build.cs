using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.SharpZipLib.Tar;
using Newtonsoft.Json.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.TextTasks;

partial class Build : NukeBuild
{
    public static int Main() => Execute<Build>(x => x.GenerateSchema);

    Target GenerateSchema => _ => _
        .Executes(GenerateSchemaAsync);

    static async Task GenerateSchemaAsync()
    {
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMinutes(5));
        try
        {
            var urls = await GetUrls(cts.Token);
            if (urls.Count == 0)
            {
                throw new Exception("Invalid archive page response");
            }

            await UpdateSchema(urls, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // Do nothing
        }
    }

    static async Task<List<string>> GetUrls(CancellationToken ct)
    {
        var body = await DownloadString("https://developers.google.com/unity/archive", ct);
        var regex = new Regex(
            "https?:\\/\\/(www\\.)?[-a-zA-Z0-9@:%._\\+~#=]{1,256}\\.[a-zA-Z0-9()]{1,6}\\b([-a-zA-Z0-9()@:%_\\+.~#?&//=]*)");
        return regex.Matches(body)
            .Select(x => x.Value)
            .Where(x => x.EndsWith(".tgz") && x.Contains("dl.google.com"))
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    static async Task UpdateSchema(List<string> urls, CancellationToken ct)
    {
        foreach (var url in urls)
        {
            await UpdatePackageSchema(url, ct);
        }
    }

    static async Task UpdatePackageSchema(string url, CancellationToken ct)
    {
        var root = RootDirectory / "schema";
        var fileName = Path.GetFileNameWithoutExtension(url);
        var semverRegex = new Regex("(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)");
        var version = semverRegex.Match(fileName).Value;
        var packageName = fileName.Replace($"-{version}", "");
        var packagePath = root / packageName / version;
        var manifestPath = packagePath / "package.json";
        var distPath = packagePath / "dist.json";
        if (manifestPath.Exists() && distPath.Exists())
        {
            return;
        }

        EnsureExistingDirectory(packagePath);
        var file = await DownloadFile(url, ct);
        var buffer = new MemoryStream();
        await file.CopyToAsync(buffer, ct);
        await ExtractPackageJson(buffer, manifestPath, ct);
        CreateDistFile(url, file, distPath);
    }

    static async Task ExtractPackageJson(Stream file, AbsolutePath path, CancellationToken ct)
    {
        file.Position = 0;
        await using var gzip = new GZipStream(file, CompressionMode.Decompress);
        using var unzippedStream = new MemoryStream();
        await gzip.CopyToAsync(unzippedStream, ct);
        unzippedStream.Seek(0, SeekOrigin.Begin);

        await using var tarInputStream = new TarInputStream(unzippedStream);
        var foundPackageJson = false;
        while (tarInputStream.GetNextEntry() is { } entry)
        {
            if (entry.Name != "package/package.json")
            {
                continue;
            }

            foundPackageJson = true;
            await using var fileStream = new FileStream(path, FileMode.OpenOrCreate);
            tarInputStream.CopyEntryContents(fileStream);
        }

        if (!foundPackageJson)
        {
            throw new Exception($"Did not found package.json for package {path}");
        }
    }

    static void CreateDistFile(string url, Stream file, AbsolutePath path)
    {
        dynamic json = new JObject();
        json.tarball = url;

        file.Position = 0;
        using var sha1 = SHA1.Create();
        var hash = sha1.ComputeHash(file);
        var sb = new StringBuilder(2 * hash.Length);
        foreach (var b in hash)
        {
            sb.Append($"{b:x2}");
        }

        json.shasum = sb.ToString();

        file.Position = 0;
        using var sha512 = SHA512.Create();
        hash = sha512.ComputeHash(file);
        json.integrity = "sha512-" + Convert.ToBase64String(hash);

        WriteAllText(path, json.ToString());
    }
}