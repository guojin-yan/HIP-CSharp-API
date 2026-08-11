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
    public void CoreProjectUsesTheExactTargetFrameworkMatrix()
    {
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        Assert.AreEqual(ExpectedFrameworks, props.Descendants("JYPPXManagedTargetFrameworks").Single().Value);

        XDocument document = XDocument.Load(CoreProject());
        Assert.AreEqual("$(JYPPXManagedTargetFrameworks)", document.Descendants("TargetFrameworks").Single().Value);
    }

    [TestMethod]
    public void PackageAndRepositoryMetadataAreFixed()
    {
        XDocument project = XDocument.Load(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "JYPPX.HipSharp.csproj"));
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));

        Assert.AreEqual("JYPPX.HIP.CSharp.API", project.Descendants("PackageId").Single().Value);
        Assert.AreEqual("JYPPX.HipSharp", project.Descendants("AssemblyName").Single().Value);
        Assert.AreEqual("0.0.0", props.Descendants("VersionPrefix").Single().Value);
        Assert.AreEqual("$(VersionPrefix)", props.Descendants("PackageVersion").Single().Value);
        Assert.IsFalse(props.Descendants("VersionSuffix").Any());
        Assert.AreEqual("https://github.com/guojin-yan/HIP-CSharp-API", props.Descendants("RepositoryUrl").Single().Value);
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "logo.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "LICENSE")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "HipSharp.sln")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "JYPPX.HipSharp.sln")));
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
        string[] expectedEntryPoints =
        {
            "hipInit", "hipRuntimeGetVersion", "hipDriverGetVersion", "hipGetDeviceCount", "hipGetDevice",
            "hipSetDevice", "hipDeviceGetName", "hipDeviceGetAttribute",
            "hipMallocManaged", "hipMemPrefetchAsync", "hipMemAdvise", "hipMallocAsync", "hipFreeAsync",
            "hipDeviceCanAccessPeer", "hipDeviceEnablePeerAccess", "hipDeviceDisablePeerAccess", "hipMemcpyPeerAsync",
            "hipStreamBeginCapture", "hipStreamEndCapture", "hipGraphDestroy", "hipGraphInstantiateWithFlags",
            "hipGraphLaunch", "hipGraphExecDestroy",
            "hipStreamCreateWithFlags", "hipStreamDestroy", "hipStreamSynchronize", "hipStreamQuery",
            "hipEventCreateWithFlags", "hipEventDestroy", "hipEventRecord", "hipEventSynchronize", "hipEventQuery", "hipEventElapsedTime",
            "hipMalloc", "hipFree", "hipMemcpy", "hipMemcpyAsync", "hipHostMalloc", "hipHostFree", "hipDeviceSynchronize",
            "hipGetErrorName", "hipGetErrorString", "hipModuleLoadData", "hipModuleUnload", "hipModuleGetFunction",
            "hipModuleLaunchKernel", "hiprtcVersion", "hiprtcGetErrorString", "hiprtcCreateProgram",
            "hiprtcDestroyProgram", "hiprtcCompileProgram", "hiprtcGetProgramLogSize", "hiprtcGetProgramLog",
            "hiprtcGetCodeSize", "hiprtcGetCode",
        };
        JsonElement functions = manifest.RootElement.GetProperty("functions");
        Assert.AreEqual(5, manifest.RootElement.GetProperty("schemaVersion").GetInt32());
        StringAssert.Contains(manifest.RootElement.GetProperty("generatorVersion").GetString()!, ".");
        Assert.AreEqual("rocm-7.2.1", manifest.RootElement.GetProperty("rocmTag").GetString());
        Assert.IsTrue(manifest.RootElement.GetProperty("preprocessorMacros").GetArrayLength() > 0);
        JsonElement verifiedHeaders = manifest.RootElement.GetProperty("verifiedHeaders");
        Assert.AreEqual(2, verifiedHeaders.GetArrayLength());
        Assert.IsTrue(verifiedHeaders.EnumerateArray().All(header =>
            Regex.IsMatch(header.GetProperty("sha256").GetString()!, "^[0-9A-F]{64}$", RegexOptions.CultureInvariant)));
        Assert.IsTrue(verifiedHeaders.EnumerateArray().All(header =>
            header.GetProperty("source").GetString()!.Contains("/ROCm/HIP/", StringComparison.Ordinal)));
        Assert.AreEqual(55, expectedEntryPoints.Length);
        Assert.AreEqual(expectedEntryPoints.Length, functions.GetArrayLength());
        CollectionAssert.AreEqual(
            expectedEntryPoints,
            functions.EnumerateArray().Select(function => function.GetProperty("entryPoint").GetString()).ToArray());
        Assert.AreEqual(15, functions.EnumerateArray().Count(function => function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(40, functions.EnumerateArray().Count(function => !function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(46, functions.EnumerateArray().Count(function => function.GetProperty("library").GetString() == "amdhip64"));
        Assert.AreEqual(9, functions.EnumerateArray().Count(function => function.GetProperty("library").GetString() == "hiprtc"));

        string generated = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "Generated", "HipNativeMethods.g.cs"));
        StringAssert.Contains(generated, "NET7_0_OR_GREATER");
        StringAssert.Contains(generated, "LibraryImport");
        StringAssert.Contains(generated, "DllImport");
        StringAssert.Contains(generated, "HipNativeLibraryNames.RuntimeImportName");
        StringAssert.Contains(generated, "HipNativeLibraryNames.RtcImportName");
        foreach (string entryPoint in expectedEntryPoints)
        {
            StringAssert.Contains(generated, "EntryPoint = \"" + entryPoint + "\"");
        }

        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "generate-interop.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "interop", "normalized-model.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-symbols.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "native", "abi-probe", "abi-evidence.schema.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "tools", "JYPPX.HipSharp.BindingGenerator", "JYPPX.HipSharp.BindingGenerator.csproj")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "Generated", "InteropCompileProbe.g.cs")));

        foreach (string framework in new[] { "net46", "netcoreapp3.1", "net7.0", "net10.0" })
        {
            AssertBuilt(framework);
        }
    }

    [TestMethod]
    public void RuntimeManifestsUseAuditedLinuxSchemaAndEnableOnlyVerifiedLinuxPackaging()
    {
        string manifestDirectory = Path.Combine(RepositoryRoot, "nuget", "runtime-manifests");
        using (JsonDocument linux = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory, "linux-x64.json"))))
        {
            JsonElement root = linux.RootElement;
            Assert.AreEqual(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("JYPPX.HipSharp.Runtime.linux-x64", root.GetProperty("packageId").GetString());
            Assert.AreEqual("7.2.1", root.GetProperty("packageVersion").GetString());
            Assert.IsTrue(root.GetProperty("packEnabled").GetBoolean());
            Assert.IsTrue(root.GetProperty("verified").GetBoolean());
            Assert.IsTrue(root.GetProperty("packages").GetArrayLength() >= 6);
            Assert.IsTrue(root.GetProperty("files").GetArrayLength() >= 6);
            Assert.IsTrue(root.GetProperty("licenses").GetArrayLength() >= 4);
            Assert.IsTrue(root.GetProperty("source").GetProperty("repositoryUrl").GetString()!.StartsWith("https://repo.radeon.com/", StringComparison.Ordinal));
            Assert.IsTrue(root.GetProperty("verification").GetProperty("provenanceVerified").GetBoolean());
            Assert.IsTrue(root.GetProperty("verification").GetProperty("closureVerified").GetBoolean());
            Assert.IsTrue(root.GetProperty("verification").GetProperty("licensesVerified").GetBoolean());
            Assert.IsTrue(root.GetProperty("verification").GetProperty("sbomVerified").GetBoolean());
            Assert.IsTrue(root.GetProperty("verification").GetProperty("packageAuditVerified").GetBoolean());
            Assert.IsTrue(root.GetProperty("verification").GetProperty("gpuValidated").GetBoolean());
            Assert.AreEqual(64, root.GetProperty("verification").GetProperty("validationSha256").GetString()!.Length);
            Assert.AreEqual("x86_64", root.GetProperty("verification").GetProperty("environment").GetProperty("architecture").GetString());
        }

        using (JsonDocument windows = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory, "win-x64.json"))))
        {
            Assert.AreEqual("JYPPX.HipSharp.Runtime.win-x64", windows.RootElement.GetProperty("packageId").GetString());
            Assert.IsFalse(windows.RootElement.GetProperty("packEnabled").GetBoolean());
            Assert.IsFalse(windows.RootElement.GetProperty("verified").GetBoolean());
            Assert.AreEqual(0, windows.RootElement.GetProperty("files").GetArrayLength());
            Assert.AreEqual("local-inventory-unavailable", windows.RootElement.GetProperty("source").GetProperty("status").GetString());
            Assert.AreEqual("amdhip64_7.dll", windows.RootElement.GetProperty("source").GetProperty("officialFileNames").GetProperty("runtime").GetString());
            Assert.AreEqual("hiprtc0702.dll", windows.RootElement.GetProperty("source").GetProperty("officialFileNames").GetProperty("rtc").GetString());
        }

        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "runtime-manifest.schema.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "linux-x64.cdx.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "linux-x64.provenance.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "linux-x64.dependency-closure.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "linux-x64.licenses.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "linux-x64.sizes.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "prepare-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "pack-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-runtime-package.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-runtime-supply-chain.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-runtime-source.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-windows-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-windows-runtime-skeleton.ps1")));

        string runtimePackScript = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "pack-runtime.ps1"));
        StringAssert.Contains(runtimePackScript, "stagingDigestSha256");
        StringAssert.Contains(runtimePackScript, "publishable = $false");
        string runtimeTargets = File.ReadAllText(Path.Combine(RepositoryRoot, "pack", "Directory.Build.targets"));
        StringAssert.Contains(runtimeTargets, "RuntimeCandidateAttestationPath");
        StringAssert.Contains(runtimeTargets, "RuntimeCandidateAttestationSha256");

        XDocument linuxProject = XDocument.Load(Path.Combine(RepositoryRoot, "pack", "JYPPX.HipSharp.Runtime.linux-x64.csproj"));
        Assert.AreEqual("JYPPX.HipSharp.Runtime.linux-x64", linuxProject.Descendants("PackageId").Single().Value);
        Assert.AreEqual("7.2.1", linuxProject.Descendants("PackageVersion").Single().Value);

        string runtimeProject = Path.Combine(RepositoryRoot, "pack", "JYPPX.HipSharp.Runtime.linux-x64.csproj");
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
        Assert.AreEqual(0, process.ExitCode, "A verified runtime manifest must allow guarded package creation.\n" + output);
        StringAssert.Contains(output, "Runtime manifest validation passed");
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "pack", "bin", "JYPPX.HipSharp.Runtime.linux-x64", "Release", "JYPPX.HipSharp.Runtime.linux-x64.7.2.1.nupkg")));

        startInfo.ArgumentList.Add("-p:RuntimeCandidateAttestationPath=artifacts/fake-attestation.json");
        using Process incompleteCandidate = Process.Start(startInfo)!;
        string incompleteOutput = incompleteCandidate.StandardOutput.ReadToEnd() + incompleteCandidate.StandardError.ReadToEnd();
        incompleteCandidate.WaitForExit();
        Assert.AreNotEqual(0, incompleteCandidate.ExitCode, "An incomplete candidate attestation must not bypass the guard.");
        StringAssert.Contains(incompleteOutput, "HIPSHARP1001");
    }

    [TestMethod]
    public void WindowsRuntimeStaticAuditPassesFixturesAndRejectsDirectPackaging()
    {
        string verifier = Path.Combine(RepositoryRoot, "eng", "verify-windows-runtime.ps1");
        string selfTest = Path.Combine(RepositoryRoot, "eng", "test-windows-runtime-skeleton.ps1");
        ProcessResult fixtures = RunProcess("pwsh", "-NoProfile", "-File", selfTest);
        Assert.AreEqual(0, fixtures.ExitCode, fixtures.Output);
        StringAssert.Contains(fixtures.Output, "12 rejection cases");

        ProcessResult guarded = RunProcess("pwsh", "-NoProfile", "-File", verifier, "-RequirePackable");
        Assert.AreNotEqual(0, guarded.ExitCode, "The disabled Windows runtime skeleton must fail a packable audit.");
        StringAssert.Contains(guarded.Output, "HIPSHARP1001");
    }

    [TestMethod]
    public void SourceDocumentationIsBilingualAndDocFxReady()
    {
        string docfxPath = Path.Combine(RepositoryRoot, "docs", "docfx.json");
        using JsonDocument docfx = JsonDocument.Parse(File.ReadAllText(docfxPath));
        JsonElement metadata = docfx.RootElement.GetProperty("metadata")[0];
        Assert.AreEqual("net10.0", metadata.GetProperty("properties").GetProperty("TargetFramework").GetString());
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, ".config", "dotnet-tools.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "docs", "toc.yml")));
        Assert.IsTrue(docfx.RootElement.GetProperty("build").GetProperty("content")[1].GetProperty("files")
            .EnumerateArray().Any(item => item.GetString() == "guides/**/*.md"));

        var declaration = new Regex(@"^\s*(?:internal|public)\s+(?:static\s+|sealed\s+|partial\s+)*(?:class|struct|enum|interface)\s+", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var chineseText = new Regex(@"[\u4e00-\u9fff]", RegexOptions.CultureInvariant);
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}Properties{Path.DirectorySeparatorChar}", StringComparison.Ordinal));

        foreach (string file in files)
        {
            string source = File.ReadAllText(file);
            if (!declaration.IsMatch(source))
            {
                continue;
            }

            StringAssert.Contains(source, "/// <summary>", $"Missing XML summary in {file}");
            Assert.IsTrue(chineseText.IsMatch(source), $"Missing Chinese API documentation in {file}");
            StringAssert.Contains(source, " / ", $"Missing Chinese/English documentation separator in {file}");
        }
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

    private static string CoreProject()
    {
        return Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "JYPPX.HipSharp.csproj");
    }

    private static void AssertBuilt(string framework)
    {
        string path = Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "bin", "Release", framework, "JYPPX.HipSharp.dll");
        Assert.IsTrue(File.Exists(path), $"Representative compile output is missing: {path}");
    }

    private static ProcessResult RunProcess(string fileName, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo(fileName)
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output);
    }

    private readonly struct ProcessResult
    {
        internal ProcessResult(int exitCode, string output)
        {
            ExitCode = exitCode;
            Output = output;
        }

        internal int ExitCode { get; }
        internal string Output { get; }
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "HipSharp.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not locate repository root.");
    }
}
