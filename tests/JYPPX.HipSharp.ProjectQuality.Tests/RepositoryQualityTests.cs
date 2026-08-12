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
        XDocument versions = XDocument.Load(Path.Combine(RepositoryRoot, "eng", "Versions.props"));

        Assert.AreEqual("JYPPX.HIP.CSharp.API", project.Descendants("PackageId").Single().Value);
        Assert.AreEqual("JYPPX.HipSharp", project.Descendants("AssemblyName").Single().Value);
        Assert.AreEqual("0.9.0", versions.Descendants("HipSharpCoreVersion").Single().Value);
        Assert.AreEqual("7.2.1", versions.Descendants("HipSharpLinuxRuntimeVersion").Single().Value);
        Assert.AreEqual("7.2.0", versions.Descendants("HipSharpWindowsRuntimeVersion").Single().Value);
        Assert.AreEqual("$(HipSharpCoreVersion)", props.Descendants("VersionPrefix").Single().Value);
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
        Assert.AreEqual(100, expectedEntryPoints.Length);
        Assert.AreEqual(expectedEntryPoints.Length, functions.GetArrayLength());
        CollectionAssert.AreEqual(
            expectedEntryPoints,
            functions.EnumerateArray().Select(function => function.GetProperty("entryPoint").GetString()).ToArray());
        Assert.AreEqual(60, functions.EnumerateArray().Count(function => function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(40, functions.EnumerateArray().Count(function => !function.GetProperty("optional").GetBoolean()));
        Assert.AreEqual(91, functions.EnumerateArray().Count(function => function.GetProperty("library").GetString() == "amdhip64"));
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
            RepositoryRoot, "src", "JYPPX.HipSharp", "Generated", "HipRuntimeNativeApi.g.cs"));
        string completeRtcGenerated = File.ReadAllText(Path.Combine(
            RepositoryRoot, "src", "JYPPX.HipSharp", "Generated", "HipRtcNativeApi.g.cs"));
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
        XDocument versions = XDocument.Load(Path.Combine(RepositoryRoot, "eng", "Versions.props"));
        using (JsonDocument linux = JsonDocument.Parse(File.ReadAllText(Path.Combine(manifestDirectory, "linux-x64.json"))))
        {
            JsonElement root = linux.RootElement;
            Assert.AreEqual(2, root.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual("JYPPX.HipSharp.Runtime.linux-x64", root.GetProperty("packageId").GetString());
            Assert.AreEqual(versions.Descendants("HipSharpLinuxRuntimeVersion").Single().Value, root.GetProperty("packageVersion").GetString());
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
        Assert.AreEqual("$(HipSharpLinuxRuntimeVersion)", linuxProject.Descendants("PackageVersion").Single().Value);

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
        string runtimeVersion = XDocument.Load(Path.Combine(RepositoryRoot, "eng", "Versions.props")).Descendants("HipSharpLinuxRuntimeVersion").Single().Value;
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "pack", "bin", "JYPPX.HipSharp.Runtime.linux-x64", "Release", $"JYPPX.HipSharp.Runtime.linux-x64.{runtimeVersion}.nupkg")));

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
        StringAssert.Contains(gate, "make_consumer advanced-features HipAdvancedFeatures");
        StringAssert.Contains(gate, "-RequireOptional");
        StringAssert.Contains(gate, "advanced-features-run.txt");
        StringAssert.Contains(gate, "advanced-features-stress-run.txt");
        StringAssert.Contains(gate, "--stress-rounds 10 --stress-streams 4 --stress-length 4194304");
        StringAssert.Contains(gate, "M8.1 isolated runtime ${package_mode} gate passed");
        StringAssert.Contains(gate, "${package_mode}\" == \"regression");
        StringAssert.Contains(gate, "-ExpectedRepositoryCommit \"${runtime_package_commit}\"");
        StringAssert.Contains(gate, "DOTNET_CLI_USE_MSBUILD_SERVER=0");
        StringAssert.Contains(gate, "UseSharedCompilation=false");

        string verifier = File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-runtime-package.ps1"));
        StringAssert.Contains(verifier, "merge-base --is-ancestor");
        StringAssert.Contains(verifier, "historical-regression");
        StringAssert.Contains(verifier, "publishable = (-not $Candidate) -and (-not $isRegression)");

        string sample = File.ReadAllText(Path.Combine(RepositoryRoot, "samples", "HipAdvancedFeatures", "Program.cs"));
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
    public void PublicApiFreezeInputsAreVersionedAndReproducible()
    {
        string snapshot = Path.Combine(RepositoryRoot, "eng", "public-api", "JYPPX.HipSharp.0.9.0.txt");
        Assert.IsTrue(File.Exists(snapshot));
        StringAssert.StartsWith(File.ReadAllText(snapshot), "# HipSharp public API snapshot schema 1");
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "public-api", "categories.json")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "eng", "verify-public-api.ps1")));
        Assert.IsTrue(File.Exists(Path.Combine(RepositoryRoot, "tools", "JYPPX.HipSharp.ApiSurface", "Program.cs")));

        string suppressionsPath = Path.Combine(RepositoryRoot, "src", "JYPPX.HipSharp", "CompatibilitySuppressions.xml");
        XDocument suppressions = XDocument.Load(suppressionsPath);
        XElement[] entries = suppressions.Descendants("Suppression").ToArray();
        string[] expectedTargets =
        {
            "T:JYPPX.HipSharp.Graphs.HipGraphKind",
            "T:JYPPX.HipSharp.Graphs.HipGraphNodeType",
            "T:JYPPX.HipSharp.Memory.HipMemoryCopyKind",
            "T:JYPPX.HipSharp.Memory.HipMemoryPoolAccess",
            "T:JYPPX.HipSharp.Modules.HipOccupancyFlags",
            "T:JYPPX.HipSharp.Rtc.HipRtcResult",
            "T:JYPPX.HipSharp.Types.HipDeviceAttribute",
            "T:JYPPX.HipSharp.Types.HipError",
            "T:JYPPX.HipSharp.Types.HipEventFlags",
            "T:JYPPX.HipSharp.Types.HipManagedMemoryFlags",
            "T:JYPPX.HipSharp.Types.HipMemoryAdvise",
            "T:JYPPX.HipSharp.Types.HipStreamCaptureMode",
            "T:JYPPX.HipSharp.Types.HipStreamFlags",
        };
        CollectionAssert.AreEqual(expectedTargets, entries.Select(entry => entry.Element("Target")?.Value).ToArray());
        Assert.IsTrue(entries.All(entry => entry.Element("DiagnosticId")?.Value == "CP0008"));
        Assert.IsTrue(entries.All(entry => entry.Element("Left")?.Value == "lib/net7.0/JYPPX.HipSharp.dll"));
        Assert.IsTrue(entries.All(entry => entry.Element("Right")?.Value == "lib/net8.0/JYPPX.HipSharp.dll"));
        StringAssert.Contains(
            File.ReadAllText(Path.Combine(RepositoryRoot, "eng", "verify-package.ps1")),
            "pending-owner-authorized-m8.6-module-global-symbol-runtime-gpu-validation");
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
