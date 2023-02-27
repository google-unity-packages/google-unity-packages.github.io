using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.TextTasks;

partial class Build
{
    static AbsolutePath DistPath => RootDirectory / "dist";

    Target GenerateRegistry => _ => _
        .Executes(ExecuteGenerateRegistry);

    void ExecuteGenerateRegistry()
    {
        SchemaRoot.GlobDirectories("*").ForEach(GeneratePackageDirectory);
    }

    void GeneratePackageDirectory(AbsolutePath packagePath)
    {
        var packageName = packagePath.Name;

        var versions = new List<(Version version, JObject package)>();
        foreach (var versionPath in packagePath.GlobDirectories("*"))
        {
            var version = GeneratePackageVersion(versionPath);
            versions.Add(version);
        }

        versions = versions
            .OrderBy(x => x.version.Major)
            .ThenBy(x => x.version.Minor)
            .ThenBy(x => x.version.Build)
            .ToList();

        dynamic manifest = new JObject();
        manifest.name = packageName;
        manifest.versions = new JObject();
        foreach (var version in versions)
        {
            manifest.versions[version.version.ToString()] = version.package;
        }

        var now = DateTime.UtcNow.ToString("O");
        manifest.time = new JObject();
        manifest.time.modified = now;
        manifest.time.created = now;
        foreach (var version in versions)
        {
            manifest.time[version.version.ToString()] = now;
        }

        manifest.users = new JObject();
        manifest["dist-tags"] = new JObject();
        manifest["dist-tags"].latest = versions.Last().version.ToString();
        manifest._id = packageName;
        manifest.readme = "ERROR: No README data found!";
        manifest._attachments = new JObject();

        var htmlPath = DistPath / packageName / "index.html";
        WriteAllText(htmlPath, manifest.ToString());
    }

    (Version version, JObject package) GeneratePackageVersion(AbsolutePath versionPath)
    {
        dynamic dist = JObject.Parse(ReadAllText(versionPath / "dist.json"));
        dynamic package = JObject.Parse(ReadAllText(versionPath / "package.json"));
        package.publishConfig = new JObject();
        package.publishConfig.registry = GitHubActions?.Repository?.Split("/")[1]
                                      ?? "google-unity-packages.github.io";
        package.dist = dist;
        package._id = $"{package.name}@{package.version}";
        package._nodeVersion = "12.22.12";
        package._npmVersion = "8.7.0";
        package.contributors = new JArray();
        // :kekw:
        package.gitHead = dist.shasum;

        var version = versionPath.Name;
        var packageName = versionPath.Parent.Name;
        var htmlPath = DistPath / packageName / version + ".html";
        WriteAllText(htmlPath, package.ToString());

        return (Version.Parse(version), package);
    }
}