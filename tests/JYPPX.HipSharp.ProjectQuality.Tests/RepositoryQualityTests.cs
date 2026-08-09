using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.ProjectQuality.Tests;

[TestClass]
public sealed class RepositoryQualityTests
{
    private const string ExpectedFrameworks = "net46;net461;net462;net47;net471;net472;net48;net481;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0";
    private static readonly string RepositoryRoot = FindRepositoryRoot();

    [TestMethod]
    public void CoreProjectsUseTheExactTargetFrameworkMatrix()
    {
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        Assert.AreEqual(ExpectedFrameworks, props.Descendants("JYPPXManagedTargetFrameworks").Single().Value);

        foreach (string project in CoreProjects())
        {
            XDocument document = XDocument.Load(project);
            Assert.AreEqual("$(JYPPXManagedTargetFrameworks)", document.Descendants("TargetFrameworks").Single().Value);
        }
    }

    [TestMethod]
    public void PackageAndRepositoryMetadataAreFixed()
    {
        XDocument project = XDocument.Load(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "JYPPX.HipSharp.csproj"));
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));

        Assert.AreEqual("JYPPX.HIP.CSharp.API", project.Descendants("PackageId").Single().Value);
        Assert.AreEqual("JYPPX.HipSharp", project.Descendants("AssemblyName").Single().Value);
        Assert.AreEqual("0.0.0", props.Descendants("VersionPrefix").Single().Value);
        Assert.AreEqual("preview.1", props.Descendants("VersionSuffix").Single().Value);
        Assert.AreEqual("https://github.com/guojin-yan/HIP-CSharp-API", props.Descendants("RepositoryUrl").Single().Value);
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "logo.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "LICENSE")));
    }

    [TestMethod]
    public void SourceNamespacesStayUnderTheProjectRootNamespace()
    {
        var declaration = new Regex(@"^\s*namespace\s+([^\s;{]+)", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (string file in files)
        {
            foreach (Match match in declaration.Matches(File.ReadAllText(file)))
            {
                Assert.IsTrue(match.Groups[1].Value.StartsWith("JYPPX.HipSharp", StringComparison.Ordinal), $"Unexpected namespace in {file}");
            }
        }
    }

    [TestMethod]
    public void InteropBranchesComeFromOneManifestAndCompileForRepresentativeTargets()
    {
        string manifestPath = Path.Combine(RepositoryRoot, "eng", "interop", "interop-manifest.json");
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        Assert.AreEqual(0, manifest.RootElement.GetProperty("functions").GetArrayLength());
        Assert.IsTrue(manifest.RootElement.GetProperty("compileProbe").GetProperty("enabled").GetBoolean());
        Assert.IsFalse(manifest.RootElement.GetProperty("compileProbe").GetProperty("invoked").GetBoolean());

        string generated = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp.Native", "Generated", "InteropCompileProbe.g.cs"));
        StringAssert.Contains(generated, "NET7_0_OR_GREATER");
        StringAssert.Contains(generated, "LibraryImport");
        StringAssert.Contains(generated, "DllImport");

        foreach (string framework in new[] { "net46", "netcoreapp3.1", "net7.0", "net10.0" })
        {
            AssertBuilt("JYPPX.HipSharp.Native", framework);
            AssertBuilt("JYPPX.HipSharp", framework);
        }
    }

    [TestMethod]
    public void RuntimeManifestsAreExplicitlyDisabledAndPackIsBlocked()
    {
        foreach (string manifestPath in Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "nuget", "runtime-manifests"), "*.json"))
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            Assert.IsFalse(manifest.RootElement.GetProperty("packEnabled").GetBoolean());
            Assert.IsFalse(manifest.RootElement.GetProperty("verified").GetBoolean());
            Assert.AreEqual(0, manifest.RootElement.GetProperty("files").GetArrayLength());
            Assert.IsTrue(manifest.RootElement.GetProperty("nativeAssetPath").GetString()!.StartsWith("runtimes/", StringComparison.Ordinal));
        }

        string runtimeProject = Path.Combine(RepositoryRoot, "pack", "JYPPX.HipSharp.Runtime.linux-x64.rocm7.2.1.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("pack");
        startInfo.ArgumentList.Add(runtimeProject);
        startInfo.ArgumentList.Add("--no-restore");
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreNotEqual(0, process.ExitCode, "An unverified runtime package must not be created.");
        StringAssert.Contains(output, "HIPSHARP1001");
    }

    [TestMethod]
    public void ProgramRepositoryCannotReachPrivatePlanningDirectories()
    {
        string[] forbidden = { "plan", "diary", "Radeon_Cloud" };
        foreach (string directory in forbidden)
        {
            Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, directory)));
        }
    }

    private static IEnumerable<string> CoreProjects()
    {
        yield return Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp.Native", "JYPPX.HipSharp.Native.csproj");
        yield return Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "JYPPX.HipSharp.csproj");
    }

    private static void AssertBuilt(string assembly, string framework)
    {
        string path = Path.Combine(RepositoryRoot, "src", assembly, "bin", "Release", framework, $"{assembly}.dll");
        Assert.IsTrue(File.Exists(path), $"Representative compile output is missing: {path}");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "JYPPX.HipSharp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
