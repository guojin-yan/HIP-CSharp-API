using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.ProjectQuality.Tests;

[TestClass]
public sealed class RepositoryQualityTests
{
    private const string ExpectedFrameworks = "net46;net461;net462;net47;net471;net472;net48;net481;netcoreapp3.1;net5.0;net6.0;net7.0;net8.0;net9.0;net10.0";
    private static readonly string[] LedgerDispositionStatuses = { "managed-next", "raw-only-reviewed", "deferred-capability" };
    private static readonly string[] LedgerExportStatuses = { "found-historical", "missing-reviewed" };
    private static readonly string[] RuntimeProjectFiles =
    {
        "JYPPX.ROCm.HipSharp.Runtime.ubuntu.24.04-x64.csproj",
        "JYPPX.ROCm.HipSharp.Runtime.win-x64.csproj",
    };
    private static readonly string[] NewRtcEntries =
    {
        "hiprtcAddNameExpression", "hiprtcGetLoweredName", "hiprtcGetBitcodeSize", "hiprtcGetBitcode",
        "hiprtcLinkCreate", "hiprtcLinkAddFile", "hiprtcLinkAddData", "hiprtcLinkComplete", "hiprtcLinkDestroy",
    };
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
        XDocument project = XDocument.Load(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "JYPPX.ROCm.HipSharp.csproj"));
        XDocument props = XDocument.Load(Path.Combine(RepositoryRoot, "Directory.Build.props"));
        XDocument versions = XDocument.Load(Path.Combine(RepositoryRoot, "eng", "Versions.props"));

        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API", project.Descendants("PackageId").Single().Value);
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API", project.Descendants("AssemblyName").Single().Value);
        Assert.AreEqual("0.10.0", versions.Descendants("HipSharpCoreVersion").Single().Value);
        Assert.AreEqual("7.2.1", versions.Descendants("HipSharpUbuntu2404RuntimeVersion").Single().Value);
        Assert.AreEqual("7.2.0", versions.Descendants("HipSharpWindowsRuntimeVersion").Single().Value);
        Assert.AreEqual("$(HipSharpCoreVersion)", props.Descendants("VersionPrefix").Single().Value);
        Assert.AreEqual("$(VersionPrefix)", props.Descendants("PackageVersion").Single().Value);
        Assert.IsFalse(props.Descendants("VersionSuffix").Any());
        Assert.AreEqual("https://github.com/guojin-yan/HIP-CSharp-API", props.Descendants("RepositoryUrl").Single().Value);
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "logo.jpg")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "nuget", "README.md")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "LICENSE")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "HipSharp.sln")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "JYPPX.ROCm.HipSharp.sln")));
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
                Assert.IsTrue(match.Groups[1].Value.StartsWith("JYPPX.ROCm.HipSharp", StringComparison.Ordinal), $"Unexpected namespace in {file}");
            }
        }
    }

    [TestMethod]
    public void RocmFamilyNamingMigrationIsComplete()
    {
        XDocument core = XDocument.Load(CoreProject());
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API", core.Descendants("AssemblyName").Single().Value);
        Assert.AreEqual("JYPPX.ROCm.HipSharp", core.Descendants("RootNamespace").Single().Value);
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API", core.Descendants("PackageId").Single().Value);

        string[] sourceRoots = { "src", "tests", "samples", "tools", "eng", "pack", "nuget", ".github" };
        string[] forbiddenTokens =
        {
            "JYPPX" + ".HipSharp",
            "JYPPX" + ".HIP.CSharp.API",
            "JYPPX" + ".Rocm",
            "JYPPX" + ".MIGraphX",
        };
        string[] forbiddenEscapedTokens =
        {
            "JYPPX" + @"\.HipSharp",
            "JYPPX" + @"\\.HipSharp",
        };
        string[] ignoredDirectoryNames = { "artifacts", "bin", "obj", "native-assets" };

        IEnumerable<string> files = sourceRoots
            .Select(root => Path.Combine(RepositoryRoot, root))
            .Where(Directory.Exists)
            .SelectMany(root => Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
            .Append(Path.Combine(RepositoryRoot, "HipSharp.sln"))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Any(segment => ignoredDirectoryNames.Contains(segment, StringComparer.OrdinalIgnoreCase)));

        foreach (string file in files)
        {
            string text;
            try
            {
                text = File.ReadAllText(file);
            }
            catch (DecoderFallbackException)
            {
                continue;
            }

            foreach (string token in forbiddenTokens.Concat(forbiddenEscapedTokens))
            {
                Assert.IsFalse(text.Contains(token, StringComparison.Ordinal), $"Legacy product name '{token}' remains in {file}");
            }
        }

        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.Native")));
        Assert.IsFalse(Directory.Exists(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.Common")));
        Assert.IsFalse(Directory.EnumerateFiles(RepositoryRoot, "*.csproj", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText)
            .Any(text => text.Contains("JYPPX.ROCm.Native", StringComparison.Ordinal) || text.Contains("JYPPX.ROCm.Common", StringComparison.Ordinal)));

        using JsonDocument schema = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "nuget", "runtime-manifests", "runtime-manifest.schema.json")));
        Assert.AreEqual("^JYPPX\\.ROCm\\.HIP\\.CSharp\\.API\\.Runtime\\.(?:[a-z][a-z0-9-]*\\.[0-9]+(?:\\.[0-9]+)*|win)-x64$", schema.RootElement.GetProperty("properties").GetProperty("packageId").GetProperty("pattern").GetString());
        using JsonDocument linuxManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "nuget", "runtime-manifests", "ubuntu.24.04-x64.json")));
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64", linuxManifest.RootElement.GetProperty("packageId").GetString());
        Assert.AreEqual("linux-x64", linuxManifest.RootElement.GetProperty("rid").GetString());
        using JsonDocument windowsManifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "nuget", "runtime-manifests", "win-x64.json")));
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64", windowsManifest.RootElement.GetProperty("packageId").GetString());

        string generator = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "JYPPX.ROCm.HipSharp.BindingGenerator", "Program.cs"));
        string generated = string.Join("\n", Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated"), "*.g.cs").Select(File.ReadAllText));
        StringAssert.Contains(generator, "JYPPX.ROCm.HipSharp");
        StringAssert.Contains(generated, "JYPPX.ROCm.HipSharp");
        Assert.IsFalse(generated.Contains("JYPPX" + ".HipSharp", StringComparison.Ordinal));
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
            "hipDeviceGetDefaultMemPool", "hipDeviceGetMemPool", "hipDeviceSetMemPool",
            "hipMemPoolCreate", "hipMemPoolDestroy", "hipMemPoolTrimTo", "hipMemPoolGetAttribute",
            "hipMemPoolSetAttribute", "hipMemPoolSetAccess", "hipMemPoolGetAccess", "hipMallocFromPoolAsync",
            "hipDeviceCanAccessPeer", "hipDeviceEnablePeerAccess", "hipDeviceDisablePeerAccess", "hipMemcpyPeerAsync",
            "hipStreamBeginCapture", "hipStreamEndCapture", "hipGraphCreate", "hipGraphAddEmptyNode",
            "hipGraphAddDependencies", "hipGraphRemoveDependencies",
            "hipGraphAddKernelNode", "hipGraphExecKernelNodeSetParams", "hipGraphAddMemcpyNode1D",
            "hipGraphExecMemcpyNodeSetParams1D", "hipGraphAddMemsetNode", "hipGraphExecMemsetNodeSetParams",
            "hipGraphAddMemAllocNode", "hipGraphAddMemFreeNode", "hipGraphUpload", "hipGraphDestroyNode",
            "hipGraphDestroy", "hipGraphInstantiateWithFlags", "hipGraphLaunch", "hipGraphExecDestroy",
            "hipStreamCreateWithFlags", "hipStreamDestroy", "hipStreamSynchronize", "hipStreamQuery",
            "hipEventCreateWithFlags", "hipEventDestroy", "hipEventRecord", "hipEventSynchronize", "hipEventQuery", "hipEventElapsedTime",
            "hipMalloc", "hipMemGetInfo", "hipMallocPitch", "hipMalloc3D", "hipFree", "hipMemcpy", "hipMemcpyAsync",
            "hipMemset", "hipMemsetAsync", "hipMemset2D", "hipMemset2DAsync", "hipMemset3D", "hipMemset3DAsync",
            "hipMemcpy2D", "hipMemcpy2DAsync", "hipMemcpy3D", "hipMemcpy3DAsync",
            "hipHostMalloc", "hipHostFree", "hipDeviceSynchronize",
            "hipGetErrorName", "hipGetErrorString", "hipModuleLoadData", "hipModuleUnload", "hipModuleGetFunction", "hipModuleGetGlobal",
            "hipFuncGetAttribute", "hipModuleOccupancyMaxActiveBlocksPerMultiprocessor",
            "hipModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags", "hipModuleOccupancyMaxPotentialBlockSize",
            "hipModuleOccupancyMaxPotentialBlockSizeWithFlags", "hipModuleLaunchCooperativeKernel",
            "hipModuleLaunchKernel", "hiprtcVersion", "hiprtcGetErrorString", "hiprtcCreateProgram",
            "hiprtcDestroyProgram", "hiprtcCompileProgram", "hiprtcGetProgramLogSize", "hiprtcGetProgramLog",
            "hiprtcGetCodeSize", "hiprtcGetCode",
            "hiprtcAddNameExpression", "hiprtcGetLoweredName", "hiprtcGetBitcodeSize", "hiprtcGetBitcode",
            "hiprtcLinkCreate", "hiprtcLinkAddFile", "hiprtcLinkAddData", "hiprtcLinkComplete", "hiprtcLinkDestroy",
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
        Assert.AreEqual(109, expectedEntryPoints.Length);
        Assert.AreEqual(expectedEntryPoints.Length, functions.GetArrayLength());
        CollectionAssert.AreEqual(
            expectedEntryPoints,
            functions.EnumerateArray().Select(function => function.GetProperty("entryPoint").GetString()).ToArray());
        Assert.AreEqual(60, functions.EnumerateArray().Count(function => function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(49, functions.EnumerateArray().Count(function => !function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(91, functions.EnumerateArray().Count(function => function.GetProperty("library").GetString() == "amdhip64"));
        Assert.AreEqual(18, functions.EnumerateArray().Count(function => function.GetProperty("library").GetString() == "hiprtc"));

        string abiProbe = File.ReadAllText(Path.Combine(RepositoryRoot, "native", "abi-probe", "hip_abi_probe.cpp"));
        StringAssert.Contains(abiProbe, "static_cast<HipMallocAsyncSignature>(&hipMallocAsync)");
        StringAssert.Contains(abiProbe, "static_cast<HipMallocFromPoolAsyncSignature>(&hipMallocFromPoolAsync)");
        Assert.IsFalse(abiProbe.Contains("decltype(&hipMallocFromPoolAsync)", StringComparison.Ordinal));

        string generated = File.ReadAllText(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipNativeMethods.g.cs"));
        StringAssert.Contains(generated, "NET7_0_OR_GREATER");
        StringAssert.Contains(generated, "LibraryImport");
        StringAssert.Contains(generated, "DllImport");
        StringAssert.Contains(generated, "HipNativeLibraryNames.RuntimeImportName");
        StringAssert.Contains(generated, "HipNativeLibraryNames.RtcImportName");
        foreach (string entryPoint in expectedEntryPoints)
        {
            StringAssert.Contains(generated, "EntryPoint = \"" + entryPoint + "\"");
        }

        string completeModelPath = Path.Combine(RepositoryRoot, "eng", "interop", "complete-api-model.json");
        using JsonDocument completeModel = JsonDocument.Parse(File.ReadAllText(completeModelPath));
        JsonElement completeRoot = completeModel.RootElement;
        JsonElement runtimeFunctions = completeRoot.GetProperty("runtimeFunctions");
        JsonElement rtcFunctions = completeRoot.GetProperty("rtcFunctions");
        Assert.AreEqual(1, completeRoot.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("rocm-7.2.1", completeRoot.GetProperty("rocmTag").GetString());
        Assert.AreEqual(459, runtimeFunctions.GetArrayLength());
        Assert.AreEqual(18, rtcFunctions.GetArrayLength());
        CollectionAssert.AreEqual(
            manifest.RootElement.GetProperty("verifiedHeaders").EnumerateArray()
                .Select(header => header.GetProperty("sha256").GetString()).OrderBy(value => value).ToArray(),
            completeRoot.GetProperty("headers").EnumerateArray()
                .Select(header => header.GetProperty("sha256").GetString()).OrderBy(value => value).ToArray());
        Assert.AreEqual(459, runtimeFunctions.EnumerateArray().Select(function => function.GetProperty("entryPoint").GetString()).Distinct().Count());
        Assert.AreEqual(18, rtcFunctions.EnumerateArray().Select(function => function.GetProperty("entryPoint").GetString()).Distinct().Count());
        Assert.IsFalse(runtimeFunctions.EnumerateArray().Any(function =>
            function.GetProperty("entryPoint").GetString()!.StartsWith("__hip", StringComparison.Ordinal) ||
            function.GetProperty("entryPoint").GetString() == "hip_init"));

        string completeRuntimeGenerated = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipRuntimeNativeApi.g.cs"));
        string completeRtcGenerated = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "HipRtcNativeApi.g.cs"));
        foreach (JsonElement function in runtimeFunctions.EnumerateArray())
        {
            StringAssert.Contains(completeRuntimeGenerated, "EntryPoint = \"" + function.GetProperty("entryPoint").GetString() + "\"");
        }
        foreach (JsonElement function in rtcFunctions.EnumerateArray())
        {
            StringAssert.Contains(completeRtcGenerated, "EntryPoint = \"" + function.GetProperty("entryPoint").GetString() + "\"");
        }

        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "generate-interop.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "interop", "normalized-model.json")));
        Assert.IsTrue(File.Exists(completeModelPath));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-symbols.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "native", "abi-probe", "abi-evidence.schema.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "tools", "JYPPX.ROCm.HipSharp.BindingGenerator", "JYPPX.ROCm.HipSharp.BindingGenerator.csproj")));
        Assert.IsFalse(File.Exists(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "Generated", "InteropCompileProbe.g.cs")));

        foreach (string framework in new[] { "net46", "netcoreapp3.1", "net7.0", "net10.0" })
        {
            AssertBuilt(framework);
        }
    }

    [TestMethod]
    public void InterfaceCoverageLedgerIsDeterministicAndClosed()
    {
        string ledgerPath = Path.Combine(RepositoryRoot, "eng", "interface-coverage", "interface-coverage.jsonl");
        string summaryPath = Path.Combine(RepositoryRoot, "eng", "interface-coverage", "interface-coverage.md");
        string generatorPath = Path.Combine(RepositoryRoot, "eng", "interface-coverage", "generate-interface-coverage.ps1");
        string reviewPath = Path.Combine(RepositoryRoot, "eng", "interface-coverage", "reviewed-classification.json");
        string managedInterfaceMapPath = Path.Combine(RepositoryRoot, "eng", "interface-coverage", "managed-interface-map.json");
        Assert.IsTrue(File.Exists(ledgerPath));
        Assert.IsTrue(File.Exists(summaryPath));
        Assert.IsTrue(File.Exists(generatorPath));
        Assert.IsTrue(File.Exists(reviewPath));
        Assert.IsTrue(File.Exists(managedInterfaceMapPath));

        string before = File.ReadAllText(ledgerPath);
        ProcessResult generated = RunProcess("pwsh", "-NoProfile", "-File", generatorPath);
        Assert.AreEqual(0, generated.ExitCode, generated.Output);
        Assert.AreEqual(before, File.ReadAllText(ledgerPath), "Ledger generation must be byte deterministic.");

        using JsonDocument complete = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "interop", "complete-api-model.json")));
        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "interop", "interop-manifest.json")));
        using JsonDocument managedInterfaceMap = JsonDocument.Parse(File.ReadAllText(managedInterfaceMapPath));
        HashSet<string> completeEntries = complete.RootElement.GetProperty("runtimeFunctions").EnumerateArray()
            .Concat(complete.RootElement.GetProperty("rtcFunctions").EnumerateArray())
            .Select(item => item.GetProperty("entryPoint").GetString()!)
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, string> completeLibraries = complete.RootElement.GetProperty("runtimeFunctions").EnumerateArray()
            .Concat(complete.RootElement.GetProperty("rtcFunctions").EnumerateArray())
            .ToDictionary(item => item.GetProperty("entryPoint").GetString()!, item => item.GetProperty("library").GetString()!, StringComparer.Ordinal);
        Dictionary<string, string> managedLibraries = manifest.RootElement.GetProperty("functions").EnumerateArray()
            .ToDictionary(item => item.GetProperty("entryPoint").GetString()!, item => item.GetProperty("library").GetString()!, StringComparer.Ordinal);
        string[] promotedEntries = managedInterfaceMap.RootElement.GetProperty("groups").EnumerateArray()
            .SelectMany(group => group.GetProperty("entries").EnumerateArray())
            .Select(entry => entry.GetString()!)
            .ToArray();
        Assert.AreEqual(82, promotedEntries.Length);
        Assert.AreEqual(82, promotedEntries.Distinct(StringComparer.Ordinal).Count());
        foreach (string entryPoint in promotedEntries)
        {
            Assert.IsTrue(completeEntries.Contains(entryPoint), $"Promoted interface is absent from complete model: {entryPoint}");
            managedLibraries.Add(entryPoint, completeLibraries[entryPoint]);
        }
        Assert.AreEqual(477, completeEntries.Count);
        Assert.AreEqual(191, managedLibraries.Count);
        Assert.IsTrue(managedLibraries.Keys.All(completeEntries.Contains), "Every managed manifest entry must exist in the complete model.");
        var unvalidatedManagedEntries = new HashSet<string>(NewRtcEntries.Concat(promotedEntries), StringComparer.Ordinal);

        string[] lines = File.ReadAllLines(ledgerPath);
        Assert.AreEqual(477, lines.Length);
        HashSet<string> seen = new(StringComparer.Ordinal);
        string? previousKey = null;
        string[] required =
        {
            "library", "entryPoint", "binding", "cloudExport", "abi", "managedDisposition",
            "unitCoverage", "cloudFunctionCoverage", "negativeCoverage", "capabilitySkip",
            "evidenceRecord", "articleTopic",
        };
        foreach (string line in lines)
        {
            using JsonDocument ledger = JsonDocument.Parse(line);
            JsonElement item = ledger.RootElement;
            foreach (string field in required)
            {
                Assert.IsTrue(item.TryGetProperty(field, out _), $"Missing required field {field}.");
            }

            string library = item.GetProperty("library").GetString()!;
            string entryPoint = item.GetProperty("entryPoint").GetString()!;
            Assert.IsTrue(completeEntries.Contains(entryPoint), $"Ledger entry is absent from complete model: {entryPoint}");
            Assert.AreEqual(completeLibraries[entryPoint], library);
            Assert.IsTrue(seen.Add(entryPoint), $"Duplicate ledger entry: {entryPoint}");
            string key = library + "\0" + entryPoint;
            Assert.IsTrue(previousKey is null || string.CompareOrdinal(previousKey, key) < 0, "Ledger order must be library then entryPoint.");
            previousKey = key;

            string disposition = item.GetProperty("managedDisposition").GetProperty("status").GetString()!;
            bool managed = managedLibraries.ContainsKey(entryPoint);
            Assert.AreEqual(managed, disposition == "managed", $"Manifest/disposition mismatch for {entryPoint}");
            if (managed)
            {
                Assert.AreEqual(managedLibraries[entryPoint], library);
                Assert.AreEqual("covered", item.GetProperty("unitCoverage").GetProperty("status").GetString());
                JsonElement cloudCoverage = item.GetProperty("cloudFunctionCoverage");
                if (unvalidatedManagedEntries.Contains(entryPoint))
                {
                    Assert.AreEqual("not-tested", cloudCoverage.GetProperty("status").GetString());
                    StringAssert.Contains(cloudCoverage.GetProperty("reason").GetString()!, "exact-SHA");
                }
                else
                {
                    Assert.AreEqual("passed-historical", cloudCoverage.GetProperty("status").GetString());
                    Assert.AreEqual("63f33cf2061b6b7ed4b1865e2266bed0a1d707c8", cloudCoverage.GetProperty("exactSha").GetString());
                    StringAssert.Contains(cloudCoverage.GetProperty("scope").GetString()!, "not current SHA");
                }
            }
            else
            {
                CollectionAssert.Contains(LedgerDispositionStatuses, disposition);
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("managedDisposition").GetProperty("reviewRule").GetString()));
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("managedDisposition").GetProperty("reason").GetString()));
                Assert.AreEqual("not-tested", item.GetProperty("unitCoverage").GetProperty("status").GetString());
                Assert.AreEqual("not-tested", item.GetProperty("cloudFunctionCoverage").GetProperty("status").GetString());
                Assert.AreEqual("not-tested", item.GetProperty("negativeCoverage").GetProperty("status").GetString());
                Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("capabilitySkip").GetProperty("reason").GetString()));
            }

            string exportStatus = item.GetProperty("cloudExport").GetProperty("status").GetString()!;
            CollectionAssert.Contains(LedgerExportStatuses, exportStatus);
            if (exportStatus == "missing-reviewed")
            {
                Assert.AreEqual("hipExternalMemoryGetMappedMipmappedArray", entryPoint);
            }
            Assert.IsTrue(item.GetProperty("abi").GetProperty("parameterCount").GetInt32() >= 0);
            Assert.IsFalse(string.IsNullOrWhiteSpace(item.GetProperty("articleTopic").GetString()));
        }
        CollectionAssert.AreEquivalent(completeEntries.ToArray(), seen.ToArray());
        Assert.AreEqual(1, lines.Count(line => JsonDocument.Parse(line).RootElement.GetProperty("cloudExport").GetProperty("status").GetString() == "missing-reviewed"));
        Assert.AreEqual(191, lines.Count(line => JsonDocument.Parse(line).RootElement.GetProperty("managedDisposition").GetProperty("status").GetString() == "managed"));
        Assert.AreEqual(286, lines.Count(line => JsonDocument.Parse(line).RootElement.GetProperty("managedDisposition").GetProperty("status").GetString() != "managed"));
    }

    [TestMethod]
    public void RuntimeManifestsUseDistributionSpecificLinuxPackageIdentity()
    {
        string manifestDirectory = Path.Combine(RepositoryRoot, "nuget", "runtime-manifests");
        XDocument versions = XDocument.Load(Path.Combine(RepositoryRoot, "eng", "Versions.props"));
        using JsonDocument promotionLock = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "promotion", "ubuntu.24.04-x64-promotion-lock.json")));
        long promotedPackageBytes = promotionLock.RootElement.GetProperty("inputs").GetProperty("runtimeCandidate").GetProperty("size").GetInt64();
        using (JsonDocument linux = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.json"))))
        {
            JsonElement root = linux.RootElement;
            Assert.AreEqual(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64", root.GetProperty("packageId").GetString());
            Assert.AreEqual(versions.Descendants("HipSharpUbuntu2404RuntimeVersion").Single().Value, root.GetProperty("packageVersion").GetString());
            Assert.AreEqual("linux-x64", root.GetProperty("rid").GetString());
            Assert.AreEqual("runtimes/linux-x64/native", root.GetProperty("nativeAssetPath").GetString());
            Assert.AreEqual("ubuntu", root.GetProperty("distribution").GetProperty("id").GetString());
            Assert.AreEqual("24.04", root.GetProperty("distribution").GetProperty("version").GetString());
            Assert.AreEqual("noble", root.GetProperty("distribution").GetProperty("codename").GetString());
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
            JsonElement verification = root.GetProperty("verification");
            Assert.IsTrue(verification.GetProperty("packageAuditVerified").GetBoolean());
            Assert.IsTrue(verification.GetProperty("gpuValidated").GetBoolean());
            Assert.AreEqual(64, verification.GetProperty("validationSha256").GetString()!.Length);
            Assert.AreEqual(JsonValueKind.Object, verification.GetProperty("environment").ValueKind);
            Assert.IsTrue(verification.TryGetProperty("promotionReceipt", out JsonElement promotionReceipt));
            Assert.AreEqual(verification.GetProperty("validationSha256").GetString(), promotionReceipt.GetProperty("sha256").GetString());
            Assert.AreEqual(promotedPackageBytes, root.GetProperty("size").GetProperty("packageBytes").GetInt64());
            StringAssert.Contains(verification.GetProperty("reason").GetString()!, "distribution-specific Ubuntu 24.04 Runtime candidate passed");
        }

        using (JsonDocument windows = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory, "win-x64.json"))))
        {
            Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64", windows.RootElement.GetProperty("packageId").GetString());
            Assert.IsFalse(windows.RootElement.GetProperty("packEnabled").GetBoolean());
            Assert.IsFalse(windows.RootElement.GetProperty("verified").GetBoolean());
            Assert.AreEqual(0, windows.RootElement.GetProperty("files").GetArrayLength());
            Assert.AreEqual("local-inventory-unavailable", windows.RootElement.GetProperty("source").GetProperty("status").GetString());
            Assert.AreEqual("amdhip64_7.dll", windows.RootElement.GetProperty("source").GetProperty("officialFileNames").GetProperty("runtime").GetString());
            Assert.AreEqual("hiprtc0702.dll", windows.RootElement.GetProperty("source").GetProperty("officialFileNames").GetProperty("rtc").GetString());
        }

        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "runtime-manifest.schema.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "promotion-receipt.schema.json")));
        Assert.IsFalse(File.Exists(Path.Combine(manifestDirectory, "linux-x64.json")));
        Assert.IsFalse(File.Exists(Path.Combine(manifestDirectory, "linux-x64.promotion-receipt.json")));
        string[] runtimeProjects = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "pack"), "*.Runtime.*.csproj")
            .Select(path => Path.GetFileName(path)!)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEquivalent(RuntimeProjectFiles, runtimeProjects);
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.cdx.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.provenance.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.dependency-closure.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.licenses.json")));
        Assert.IsTrue(File.Exists(Path.Combine(manifestDirectory, "ubuntu.24.04-x64.sizes.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "prepare-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "pack-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-runtime-package.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-runtime-supply-chain.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-promotion.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "compare-promoted-packages.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "create-release-envelope.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-runtime-source.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-windows-runtime.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "test-windows-runtime-skeleton.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "pack", "JYPPX.ROCm.HipSharp.Runtime.ubuntu.24.04-x64.packages.lock.json")));
        string runtimeReleaseWorkflow = File.ReadAllText(Path.Combine(RepositoryRoot, ".github", "workflows", "runtime-ubuntu-24.04-release.yml"));
        StringAssert.Contains(runtimeReleaseWorkflow, "runtime-ubuntu.24.04-v*.*.*");
        StringAssert.Contains(runtimeReleaseWorkflow, "JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64");
        StringAssert.Contains(runtimeReleaseWorkflow, "dotnet nuget push");
        StringAssert.Contains(runtimeReleaseWorkflow, "--skip-duplicate");
        StringAssert.Contains(runtimeReleaseWorkflow, "dotnet restore ./HipSharp.sln --locked-mode");
        StringAssert.Contains(runtimeReleaseWorkflow, "dotnet build ./HipSharp.sln --configuration Release --no-restore");
        StringAssert.Contains(runtimeReleaseWorkflow, "Runtime-Package-SHA256:");
        StringAssert.Contains(runtimeReleaseWorkflow, "Runtime-Package-Size:");
        StringAssert.Contains(runtimeReleaseWorkflow, "exact cloud-validated bytes bound by the annotated tag");
        StringAssert.Contains(runtimeReleaseWorkflow, "JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64");
        StringAssert.Contains(runtimeReleaseWorkflow, "dotnet nuget delete");
        StringAssert.Contains(runtimeReleaseWorkflow, "gh release");

        string attributes = File.ReadAllText(Path.Combine(RepositoryRoot, ".gitattributes"));
        foreach (string metadata in new[]
        {
            "ubuntu.24.04-x64.json",
            "ubuntu.24.04-x64.cdx.json",
            "ubuntu.24.04-x64.dependency-closure.json",
            "ubuntu.24.04-x64.licenses.json",
            "ubuntu.24.04-x64.provenance.json",
            "ubuntu.24.04-x64.sizes.json",
        })
        {
            StringAssert.Contains(attributes, $"nuget/runtime-manifests/{metadata} text eol=crlf");
            byte[] bytes = File.ReadAllBytes(Path.Combine(manifestDirectory, metadata));
            for (int index = 0; index < bytes.Length; index++)
            {
                if (bytes[index] == (byte)'\n')
                {
                    Assert.IsTrue(index > 0 && bytes[index - 1] == (byte)'\r', $"{metadata} must use promotion-locked CRLF bytes.");
                }
                else if (bytes[index] == (byte)'\r')
                {
                    Assert.IsTrue(index + 1 < bytes.Length && bytes[index + 1] == (byte)'\n', $"{metadata} contains a bare carriage return.");
                }
            }
        }

        string runtimePackScript = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "pack-runtime.ps1"));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "normalize-nupkg.ps1")));
        StringAssert.Contains(runtimePackScript, "normalize-nupkg.ps1");
        StringAssert.Contains(runtimePackScript, "stagingDigestSha256");
        StringAssert.Contains(runtimePackScript, "publishable = $false");
        StringAssert.Contains(runtimePackScript, "Assert-TextLineEndings $sourceManifestPath CRLF");
        StringAssert.Contains(runtimePackScript, "Assert-TextLineEndings $receiptPath LF");
        string promotionScript = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "promote-runtime-manifest.ps1"));
        StringAssert.Contains(promotionScript, "$runtimeCandidateInput = $lock[\"inputs\"][\"runtimeCandidate\"]");
        StringAssert.Contains(promotionScript, "$manifestValue[\"size\"][\"packageBytes\"] = [int64]$runtimeCandidateInput[\"size\"]");
        StringAssert.Contains(promotionScript, "generate-runtime-metadata.ps1");
        string promotionVerifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-promotion.ps1"));
        StringAssert.Contains(promotionVerifier, "TrackedReceiptOnly");
        StringAssert.Contains(promotionVerifier, "Tracked promotion receipt passed without ignored candidate artifacts");
        string runtimePackageVerifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-runtime-package.ps1"));
        StringAssert.Contains(runtimePackageVerifier, "runtimeProtectedPayload");
        StringAssert.Contains(runtimePackageVerifier, "Final Runtime protected payload does not match the promoted candidate receipt");
        string runtimeTargets = File.ReadAllText(Path.Combine(RepositoryRoot, "pack", "Directory.Build.targets"));
        StringAssert.Contains(runtimeTargets, "RuntimeCandidateAttestationPath");
        StringAssert.Contains(runtimeTargets, "RuntimeCandidateAttestationSha256");
        StringAssert.Contains(runtimeTargets, "RuntimePromotionReceiptPath");
        StringAssert.Contains(runtimeTargets, "RuntimePromotionReceiptSha256");
        StringAssert.Contains(runtimeTargets, "RuntimeFinalAttestationPath");
        StringAssert.Contains(runtimeTargets, "RuntimeFinalAttestationSha256");
        XDocument runtimeTargetsDocument = XDocument.Load(Path.Combine(RepositoryRoot, "pack", "Directory.Build.targets"));
        XElement nativePackItem = runtimeTargetsDocument.Descendants("None")
            .Single(item => item.Attribute("Include")?.Value.EndsWith("\\native\\*", StringComparison.Ordinal) == true);
        Assert.AreEqual("runtimes\\$(RuntimeAssetRid)\\native", nativePackItem.Attribute("PackagePath")?.Value,
            "A trailing separator creates double-slash native paths in Linux-built nupkg archives.");

        XDocument linuxProject = XDocument.Load(Path.Combine(RepositoryRoot, "pack", "JYPPX.ROCm.HipSharp.Runtime.ubuntu.24.04-x64.csproj"));
        Assert.AreEqual("JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64", linuxProject.Descendants("PackageId").Single().Value);
        Assert.AreEqual("$(HipSharpUbuntu2404RuntimeVersion)", linuxProject.Descendants("PackageVersion").Single().Value);
        Assert.AreEqual("linux-x64", linuxProject.Descendants("RuntimeAssetRid").Single().Value);

        string runtimeProject = Path.Combine(RepositoryRoot, "pack", "JYPPX.ROCm.HipSharp.Runtime.ubuntu.24.04-x64.csproj");
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepositoryRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        startInfo.ArgumentList.Add("pack");
        startInfo.ArgumentList.Add(runtimeProject);
        startInfo.ArgumentList.Add("--configuration");
        startInfo.ArgumentList.Add("Release");

        using Process process = Process.Start(startInfo)!;
        string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.AreNotEqual(0, process.ExitCode, "Direct final packaging must reject calls that do not carry the tracked promotion receipt.\n" + output);
        StringAssert.Contains(output, "HIPSHARP1001");

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
    public void IsolatedRuntimeGateIncludesTheM6AdvancedPackageConsumer()
    {
        string gate = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "runtime-gate.sh"));
        StringAssert.Contains(gate, "make_consumer advanced-features validation/AdvancedReliabilityStress");
        StringAssert.Contains(gate, "-RequireOptional");
        StringAssert.Contains(gate, "advanced-features-run.txt");
        StringAssert.Contains(gate, "advanced-features-stress-run.txt");
        StringAssert.Contains(gate, "--stress-rounds 10 --stress-streams 4 --stress-length 4194304");
        StringAssert.Contains(gate, "M8.8 isolated runtime ${package_mode} gate passed");
        StringAssert.Contains(gate, "Final exact package did not reproduce the M8.7 1127-comparison");
        StringAssert.Contains(gate, "${package_mode}\" == \"regression");
        StringAssert.Contains(gate, "-ExpectedRepositoryCommit \"${runtime_package_commit}\"");
        StringAssert.Contains(gate, "DOTNET_CLI_USE_MSBUILD_SERVER=0");
        StringAssert.Contains(gate, "UseSharedCompilation=false");

        string verifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-runtime-package.ps1"));
        StringAssert.Contains(verifier, "merge-base --is-ancestor");
        StringAssert.Contains(verifier, "historical-regression");
        StringAssert.Contains(verifier, "releaseAuthorized = $false");
        StringAssert.Contains(verifier, "dotnet nuget verify --all");
        StringAssert.Contains(verifier, "repositorySignature = $repositorySignature");

        string rtcSample = File.ReadAllText(Path.Combine(RepositoryRoot, "samples", "tutorials", "04-Kernel", "HipRtcVectorAdd", "Program.cs"));
        StringAssert.Contains(rtcSample, "HipRtcResult.NameExpressionNotValid");
        StringAssert.Contains(rtcSample, "\"lowered-name-before-compile\"");
        StringAssert.Contains(rtcSample, "const string postCompilationNameExpression = \"VectorAddTemplate<double>\";");
        StringAssert.Contains(rtcSample, "ExpectException<InvalidOperationException>(");
        StringAssert.Contains(rtcSample, "() => program.AddNameExpression(postCompilationNameExpression)");
        Assert.IsFalse(rtcSample.Contains("HipRtcResult.NoNameExpressionsAfterCompilation", StringComparison.Ordinal));
        Assert.IsFalse(rtcSample.Contains("() => program.AddNameExpression(nameExpression)", StringComparison.Ordinal));

        string sample = File.ReadAllText(Path.Combine(RepositoryRoot, "samples", "validation", "AdvancedReliabilityStress", "Program.cs"));
        StringAssert.Contains(sample, "peer=passed(1->0");
        StringAssert.Contains(sample, "Peer-copy mismatch");
        StringAssert.Contains(sample, "stress=passed(rounds=");
        StringAssert.Contains(sample, "maxInFlightDeviceBytes=");
        StringAssert.Contains(sample, "performanceClaim=false");

        string cloudGate = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "cloud-test.sh"));
        StringAssert.Contains(cloudGate, "${actual_commit}/${run_stamp}");
        StringAssert.Contains(cloudGate, "HIPSHARP_CLOUD_EVIDENCE_DIR");
        StringAssert.Contains(cloudGate, "cloud-stress.sh \"${actual_commit}\"");
        StringAssert.Contains(cloudGate, "-getProperty:HipSharpCoreVersion");
        StringAssert.Contains(cloudGate, "complete-api-model.json");
        StringAssert.Contains(cloudGate, "hipExternalMemoryGetMappedMipmappedArray");
        StringAssert.Contains(cloudGate, "Runtime=458/459");
        StringAssert.Contains(cloudGate, "HIPRTC=18/18");
        Assert.IsFalse(cloudGate.Contains("json.load(sys.stdin)", StringComparison.Ordinal));

        string stressGatePath = Path.Combine(RepositoryRoot, "tools", "radeon", "cloud-stress.sh");
        Assert.IsTrue(File.Exists(stressGatePath));
        string stressGate = File.ReadAllText(stressGatePath);
        StringAssert.Contains(stressGate, "HIPSHARP_STRESS_ROUNDS");
        StringAssert.Contains(stressGate, "HIPSHARP_STRESS_STREAMS");
        StringAssert.Contains(stressGate, "HIPSHARP_STRESS_LENGTH");
        StringAssert.Contains(stressGate, "cloud-stress-summary.json");
        StringAssert.Contains(stressGate, "\"performanceClaim\": False");
    }

    [TestMethod]
    public void TamperedRuntimePackageGateAcceptsOnlyExpectedVerificationRejections()
    {
        string verifier = Path.Combine(RepositoryRoot, "eng", "verify-runtime-tamper-failure.ps1");
        Assert.IsTrue(File.Exists(verifier));
        string gate = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "runtime-gate.sh"));
        StringAssert.Contains(gate, "verify-runtime-tamper-failure.ps1");

        string fixtureDirectory = Path.Combine(Path.GetTempPath(), "hipsharp-tamper-verifier-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(fixtureDirectory);
        try
        {
            string hashSize = Path.Combine(fixtureDirectory, "hash-size.txt");
            File.WriteAllText(hashSize, "Runtime package hash/size mismatch: runtimes/linux-x64/native/libhsa-runtime64.so.1");
            ProcessResult hashSizeResult = RunProcess("pwsh", "-NoProfile", "-File", verifier, "-ExitCode", "1", "-EvidencePath", hashSize);
            Assert.AreEqual(0, hashSizeResult.ExitCode, hashSizeResult.Output);
            StringAssert.Contains(hashSizeResult.Output, "hash/size verification");

            string signature = Path.Combine(fixtureDirectory, "signature.txt");
            File.WriteAllText(signature, "Runtime package repository signature verification failed:\nerror: NU3005: The package signature file entry is invalid. Package signature validation failed.");
            ProcessResult signatureResult = RunProcess("pwsh", "-NoProfile", "-File", verifier, "-ExitCode", "1", "-EvidencePath", signature);
            Assert.AreEqual(0, signatureResult.ExitCode, signatureResult.Output);
            StringAssert.Contains(signatureResult.Output, "NuGet signature verification (NU3005)");

            string unrelated = Path.Combine(fixtureDirectory, "unrelated.txt");
            File.WriteAllText(unrelated, "pwsh: command not found");
            ProcessResult unrelatedResult = RunProcess("pwsh", "-NoProfile", "-File", verifier, "-ExitCode", "1", "-EvidencePath", unrelated);
            Assert.AreNotEqual(0, unrelatedResult.ExitCode, "An unrelated nonzero failure must not satisfy the tamper negative.");
            // PowerShell may wrap exception messages at the host console width.
            StringAssert.Contains(unrelatedResult.Output, "Tampered Runtime package did not fail through");
            StringAssert.Contains(unrelatedResult.Output, "verification path.");

            ProcessResult zeroExitResult = RunProcess("pwsh", "-NoProfile", "-File", verifier, "-ExitCode", "0", "-EvidencePath", hashSize);
            Assert.AreNotEqual(0, zeroExitResult.ExitCode, "A successful verification command must not satisfy the tamper negative.");
            StringAssert.Contains(zeroExitResult.Output, "unexpectedly succeeded");
        }
        finally
        {
            Directory.Delete(fixtureDirectory, recursive: true);
        }
    }

    [TestMethod]
    public void ManagedExpansionSampleAndBothLinuxGatesUseTheVersionedResultContract()
    {
        string sampleDirectory = Path.Combine(RepositoryRoot, "samples", "validation", "HipManagedExpansionValidation");
        string program = File.ReadAllText(Path.Combine(sampleDirectory, "Program.cs"));
        string model = File.ReadAllText(Path.Combine(sampleDirectory, "ValidationResult.cs"));
        string project = File.ReadAllText(Path.Combine(sampleDirectory, "HipManagedExpansionValidation.csproj"));
        string verifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-managed-expansion.ps1"));
        string cloudGate = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "cloud-test.sh"));
        string runtimeGate = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "runtime-gate.sh"));

        foreach (string stage in new[]
        {
            "m8.2-pitched-memory",
            "m8.3-memory-pool",
            "m8.4-explicit-graph",
            "m8.5-kernel-occupancy",
            "m8.6-module-globals",
        })
        {
            StringAssert.Contains(program + model, stage);
            StringAssert.Contains(verifier, stage);
            StringAssert.Contains(cloudGate, stage);
            StringAssert.Contains(runtimeGate, stage);
        }

        StringAssert.Contains(project, "<TargetFramework>net10.0</TargetFramework>");
        StringAssert.Contains(program, "GetGlobal<int>(\"validation_values\")");
        StringAssert.Contains(program, "GetGlobal(\"validation_bytes\")");
        StringAssert.Contains(program, "LaunchCooperative");
        StringAssert.Contains(program, "AddMemoryAllocation");
        StringAssert.Contains(program, "CreateMemoryPool");
        StringAssert.Contains(program, "Allocate3D<int>");
        StringAssert.Contains(program, "IsMemoryPoolNotSupported");
        StringAssert.Contains(program, "IsGraphMemoryNodeNotSupported");
        StringAssert.Contains(program, "attributes-occupancy");
        Assert.IsFalse(program.Contains("#include <hip/", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("cooperative_groups", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("catch (HipException exception) when (exception.Error == HipError.NotSupported)", StringComparison.Ordinal));
        StringAssert.Contains(model, "PerformanceClaim");
        StringAssert.Contains(model, "Iterations");
        StringAssert.Contains(model, "Capability");
        StringAssert.Contains(verifier, "--self-test");
        StringAssert.Contains(verifier, "--self-test-failure");
        StringAssert.Contains(verifier, "not-an-architecture");
        StringAssert.Contains(verifier, "--unknown-option");
        StringAssert.Contains(cloudGate, "--environment official-host");
        StringAssert.Contains(runtimeGate, "--environment package-only");
        StringAssert.Contains(runtimeGate, "make_multi_file_consumer managed-expansion validation/HipManagedExpansionValidation");
        StringAssert.Contains(runtimeGate, "--program-linker-validation");
        StringAssert.Contains(runtimeGate, "hiprtc-program-linker-run.json");
        StringAssert.Contains(runtimeGate, "validate-hiprtc-program-linker.py");
        StringAssert.Contains(cloudGate, "evidence[\"schemaVersion\"] != 7 or len(evidence.get(\"functions\", [])) != 109");
        StringAssert.Contains(cloudGate, "0.10.0 managed HIPRTC exports are missing");
        StringAssert.Contains(cloudGate, "--program-linker-validation");
        StringAssert.Contains(cloudGate, "hiprtc-program-linker.json");
        string radeonReadme = File.ReadAllText(Path.Combine(RepositoryRoot, "tools", "radeon", "README.md"));
        StringAssert.Contains(radeonReadme, "109 managed-manifest exports");
        StringAssert.Contains(radeonReadme, "91 Runtime and 18 HIPRTC managed-manifest exports");
        StringAssert.Contains(radeonReadme, "HIPRTC Program/Linker exact-package workload");
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "tools", "radeon", "validate-hiprtc-program-linker.py")));
        string rtcSample = File.ReadAllText(Path.Combine(RepositoryRoot, "samples", "tutorials", "04-Kernel", "HipRtcVectorAdd", "Program.cs"));
        StringAssert.Contains(rtcSample, "CompileToBitcode");
        StringAssert.Contains(rtcSample, "GetLoweredName");
        StringAssert.Contains(rtcSample, "HipRtcJitInputType.LlvmBitcode");
        StringAssert.Contains(rtcSample, "AddFile");
        StringAssert.Contains(rtcSample, "hiprtc-program-linker-0.10.0");
        StringAssert.Contains(rtcSample, "performanceClaim = false");
        Assert.IsFalse(program.Contains("IntPtr", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("JYPPX.ROCm.HipSharp.LowLevel", StringComparison.Ordinal));
        Assert.IsFalse(program.Contains("DangerousGetHandle", StringComparison.Ordinal));
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
        string docsScript = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "docs.ps1"));
        StringAssert.Contains(docsScript, "Resolve-RepositoryOutputDirectory");
        StringAssert.Contains(docsScript, "Remove-DocumentationOutput");
        StringAssert.Contains(docsScript, "docs/api");
        StringAssert.Contains(docsScript, "_site");
        StringAssert.Contains(docsScript, "Legacy API namespace pages remain");
        string docsTest = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "test-docs.ps1"));
        StringAssert.Contains(docsTest, "$legacyNamespace.LegacySentinel");
        StringAssert.Contains(docsTest, "legacy API pages=0");
        Assert.IsTrue(docfx.RootElement.GetProperty("build").GetProperty("content")[1].GetProperty("files")
            .EnumerateArray().Any(item => item.GetString() == "guides/**/*.md"));

        var declaration = new Regex(@"^\s*(?:internal|public)\s+(?:static\s+|sealed\s+|partial\s+)*(?:class|struct|enum|interface)\s+", RegexOptions.Multiline | RegexOptions.CultureInvariant);
        var chineseText = new Regex(@"[\u4e00-\u9fff]", RegexOptions.CultureInvariant);
        IEnumerable<string> files = Directory.EnumerateFiles(Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp"), "*.cs", SearchOption.AllDirectories)
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
    public void PublicApiFreezeInputsAreVersionedAndReproducible()
    {
        string snapshot = Path.Combine(RepositoryRoot, "eng", "public-api", "JYPPX.ROCm.HipSharp.0.10.0.txt");
        Assert.IsTrue(File.Exists(snapshot));
        string currentSurface = File.ReadAllText(snapshot);
        StringAssert.StartsWith(currentSurface, "# HipSharp public API snapshot schema 1");
        StringAssert.Contains(currentSurface, "JYPPX.ROCm.HipSharp.Rtc.HipRtcLinker");
        StringAssert.Contains(currentSurface, "JYPPX.ROCm.HipSharp.Rtc.HipRtcJitInputType");
        StringAssert.Contains(currentSurface, "JYPPX.ROCm.HipSharp.Memory.HipVirtualMemoryReservation");
        StringAssert.Contains(currentSurface, "JYPPX.ROCm.HipSharp.Textures.HipTextureObject");
        StringAssert.Contains(currentSurface, "JYPPX.ROCm.HipSharp.Types.HipComputeCapability");
        string historicalSnapshot = Path.Combine(RepositoryRoot, "eng", "public-api", "JYPPX.ROCm.HipSharp.0.9.2.txt");
        Assert.IsTrue(File.Exists(historicalSnapshot));
        Assert.AreNotEqual(File.ReadAllText(historicalSnapshot), currentSurface);
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "public-api", "categories.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-public-api.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "tools", "JYPPX.ROCm.HipSharp.ApiSurface", "Program.cs")));

        string suppressionsPath = Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "CompatibilitySuppressions.xml");
        XDocument suppressions = XDocument.Load(suppressionsPath);
        XElement[] entries = suppressions.Descendants("Suppression").ToArray();
        string[] expectedTargets =
        {
            "T:JYPPX.ROCm.HipSharp.Graphs.HipGraphKind",
            "T:JYPPX.ROCm.HipSharp.Graphs.HipGraphNodeType",
            "T:JYPPX.ROCm.HipSharp.Memory.HipMemoryCopyKind",
            "T:JYPPX.ROCm.HipSharp.Memory.HipMemoryPoolAccess",
            "T:JYPPX.ROCm.HipSharp.Modules.HipOccupancyFlags",
            "T:JYPPX.ROCm.HipSharp.Rtc.HipRtcJitInputType",
            "T:JYPPX.ROCm.HipSharp.Rtc.HipRtcResult",
            "T:JYPPX.ROCm.HipSharp.Types.HipArrayFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipArrayFormat",
            "T:JYPPX.ROCm.HipSharp.Types.HipChannelFormatKind",
            "T:JYPPX.ROCm.HipSharp.Types.HipDeviceAttribute",
            "T:JYPPX.ROCm.HipSharp.Types.HipDeviceCacheConfig",
            "T:JYPPX.ROCm.HipSharp.Types.HipError",
            "T:JYPPX.ROCm.HipSharp.Types.HipEventFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipManagedMemoryFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipMemoryAccessFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipMemoryAdvise",
            "T:JYPPX.ROCm.HipSharp.Types.HipMemoryAllocationHandleType",
            "T:JYPPX.ROCm.HipSharp.Types.HipResourceViewFormat",
            "T:JYPPX.ROCm.HipSharp.Types.HipSharedMemoryConfig",
            "T:JYPPX.ROCm.HipSharp.Types.HipStreamCaptureMode",
            "T:JYPPX.ROCm.HipSharp.Types.HipStreamCaptureStatus",
            "T:JYPPX.ROCm.HipSharp.Types.HipStreamFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipStreamWaitValueFlags",
            "T:JYPPX.ROCm.HipSharp.Types.HipTextureAddressMode",
            "T:JYPPX.ROCm.HipSharp.Types.HipTextureFilterMode",
            "T:JYPPX.ROCm.HipSharp.Types.HipTextureReadMode",
            "T:JYPPX.ROCm.HipSharp.Types.HipTextureResourceKind",
        };
        CollectionAssert.AreEqual(expectedTargets, entries.Select(entry => entry.Element("Target")?.Value).ToArray());
        Assert.IsTrue(entries.All(entry => entry.Element("DiagnosticId")?.Value == "CP0008"));
        Assert.IsTrue(entries.All(entry => entry.Element("Left")?.Value == "lib/net7.0/JYPPX.ROCm.HIP.CSharp.API.dll"));
        Assert.IsTrue(entries.All(entry => entry.Element("Right")?.Value == "lib/net8.0/JYPPX.ROCm.HIP.CSharp.API.dll"));
        string packageVerifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-package.ps1"));
        StringAssert.Contains(packageVerifier, "core-0.10.0-hiprtc-program-linker; local-package-gates-passed; fresh-exact-package-gpu-validation-required");
        StringAssert.Contains(packageVerifier, "releaseAuthorized = $false");
        string pairingGate = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "test-core-runtime-pairing.ps1"));
        StringAssert.Contains(pairingGate, "JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64");
        StringAssert.Contains(pairingGate, "RuntimePackagePath is required");
        StringAssert.Contains(pairingGate, "source-mapped-local-target-packages-plus-nuget-framework-packs");
        StringAssert.Contains(pairingGate, "packageSourceMapping");
        StringAssert.Contains(pairingGate, "native assets=14");
        StringAssert.Contains(pairingGate, "system-native Core-only native assets=0");
        string candidateAttestation = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "create-core-candidate-attestation.ps1"));
        StringAssert.Contains(candidateAttestation, "normalizedContentSha256");
        StringAssert.Contains(candidateAttestation, "protectedContentSha256");
        StringAssert.Contains(candidateAttestation, "equalToHistorical = $true");
        StringAssert.Contains(candidateAttestation, "gpuValidated = $false");
        StringAssert.Contains(candidateAttestation, "publishable = $false");
        StringAssert.Contains(candidateAttestation, "releaseAuthorized = $false");
        string windowsBuild = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "build.ps1"));
        string linuxBuild = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "build.sh"));
        StringAssert.Contains(windowsBuild, "-p:ContinuousIntegrationBuild=true");
        StringAssert.Contains(linuxBuild, "-p:ContinuousIntegrationBuild=true");
    }

    [TestMethod]
    public void AssemblySemanticSnapshotHandlesConstructors()
    {
        string tool = Path.Combine(RepositoryRoot, "tools", "JYPPX.ROCm.HipSharp.ApiSurface", "JYPPX.ROCm.HipSharp.ApiSurface.csproj");
        string assembly = Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "bin", "Release", "net10.0", "JYPPX.ROCm.HIP.CSharp.API.dll");
        string snapshot = Path.Combine(RepositoryRoot, "artifacts", "project-quality", "semantic-net10.0.txt");
        ProcessResult result = RunProcess("dotnet", "run", "--project", tool, "--configuration", "Release", "--no-build", "--no-restore", "--", "--assembly", assembly, "--semantic", snapshot);

        Assert.AreEqual(0, result.ExitCode, result.Output);
        string text = File.ReadAllText(snapshot);
        StringAssert.StartsWith(text, "# HipSharp assembly semantic snapshot schema 1");
        StringAssert.Contains(text, "|.ctor|generic=0|");
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
        return Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "JYPPX.ROCm.HipSharp.csproj");
    }

    private static void AssertBuilt(string framework)
    {
        string path = Path.Combine(RepositoryRoot, "src", "JYPPX.ROCm.HipSharp", "bin", "Release", framework, "JYPPX.ROCm.HIP.CSharp.API.dll");
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
