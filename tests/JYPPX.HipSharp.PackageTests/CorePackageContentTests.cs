using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.PackageTests;

[TestClass]
public sealed class CorePackageContentTests
{
    private static readonly string[] Frameworks =
    {
        "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
        "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0",
    };

    [TestMethod]
    public void CandidatePackageContainsCompleteAssetsAndNoForbiddenPayload()
    {
        string packagePath = LocatePackage();
        using ZipArchive archive = ZipFile.OpenRead(packagePath);
        HashSet<string> entries = archive.Entries.Select(entry => entry.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string framework in Frameworks)
        {
            string[] expectedFiles = { "JYPPX.HipSharp.dll", "JYPPX.HipSharp.xml" };
            foreach (string file in expectedFiles)
            {
                Assert.IsTrue(entries.Contains($"lib/{framework}/{file}"), $"Missing {framework} asset: {file}");
            }

            string[] frameworkEntries = entries
                .Where(entry => entry.StartsWith($"lib/{framework}/", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Assert.AreEqual(expectedFiles.Length, frameworkEntries.Length, $"Unexpected assets found for {framework}: {string.Join(", ", frameworkEntries)}");
        }

        foreach (string file in new[] { "README.md", "logo.jpg", "LICENSE" })
        {
            Assert.IsTrue(entries.Contains(file), $"Missing package file: {file}");
        }

        Assert.IsFalse(entries.Any(IsForbidden), "The package contains a forbidden path or binary.");

        ZipArchiveEntry nuspecEntry = archive.Entries.Single(entry => entry.FullName.EndsWith(".nuspec", StringComparison.OrdinalIgnoreCase));
        using Stream nuspecStream = nuspecEntry.Open();
        XDocument nuspec = XDocument.Load(nuspecStream);
        XNamespace ns = nuspec.Root!.Name.Namespace;
        XElement metadata = nuspec.Root.Element(ns + "metadata")!;
        Assert.AreEqual("JYPPX.HIP.CSharp.API", metadata.Element(ns + "id")!.Value);
        Assert.AreEqual(CoreVersion(), metadata.Element(ns + "version")!.Value);
        Assert.AreEqual("README.md", metadata.Element(ns + "readme")!.Value);
        Assert.AreEqual("logo.jpg", metadata.Element(ns + "icon")!.Value);
        Assert.AreEqual("LICENSE", metadata.Element(ns + "license")!.Value);
        Assert.AreEqual("https://github.com/guojin-yan/HIP-CSharp-API", metadata.Element(ns + "repository")!.Attribute("url")!.Value);
    }

    private static bool IsForbidden(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".so", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".dylib", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".hsaco", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".bc", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".hip", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".cpp", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
            || normalized.EndsWith(".h", StringComparison.OrdinalIgnoreCase)
            || (normalized.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
                && (normalized.Contains("amdhip", StringComparison.OrdinalIgnoreCase)
                    || normalized.Contains("hiprtc", StringComparison.OrdinalIgnoreCase)))
            || normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("plan/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("diary/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("Radeon_Cloud/", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("tests/", StringComparison.OrdinalIgnoreCase);
    }

    private static string LocatePackage()
    {
        string? configured = Environment.GetEnvironmentVariable("HIPSHARP_PACKAGE_PATH");
        if (!string.IsNullOrWhiteSpace(configured) && File.Exists(configured))
        {
            return Path.GetFullPath(configured);
        }

        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HipSharp.sln")))
        {
            directory = directory.Parent;
        }

        string packageDirectory = Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root."), "artifacts", "packages");
        return Directory.EnumerateFiles(packageDirectory, "JYPPX.HIP.CSharp.API.*.nupkg")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .FirstOrDefault() ?? throw new AssertFailedException("Build the core candidate package before running package tests.");
    }

    private static string CoreVersion()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HipSharp.sln")))
        {
            directory = directory.Parent;
        }

        XDocument versions = XDocument.Load(Path.Combine(directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root."), "eng", "Versions.props"));
        return versions.Descendants("HipSharpCoreVersion").Single().Value;
    }
}
