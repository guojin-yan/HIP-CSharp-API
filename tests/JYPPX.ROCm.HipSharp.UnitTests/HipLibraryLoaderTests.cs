using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Loading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipLibraryLoaderTests
{
    [TestMethod]
    public void LocatorUsesExplicitApplicationRuntimeEnvironmentAndOsOrder()
    {
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        string root = Path.GetPathRoot(AppContext.BaseDirectory)!;
        string fileName = isWindows ? "amdhip64_7.dll" : "libamdhip64.so";
        string applicationBase = Path.Combine(root, "hipsharp-app");
        string rocmRoot = Path.Combine(root, "hipsharp-rocm");
        string explicitPath = Path.Combine(root, "hipsharp-custom", fileName);
        string rid = isWindows ? "win-x64" : "linux-x64";
        var platform = new HipPlatformInfo(isWindows, !isWindows, isWindows ? "Windows" : "Linux", "x64", ".NET 10", rid);
        var locator = new HipLibraryLocator(
            platform,
            applicationBase,
            name => name == "ROCM_PATH" ? rocmRoot : null);

        HipLibraryCandidate[] candidates = locator.GetCandidates(explicitPath).ToArray();

        Assert.AreEqual(explicitPath, candidates[0].Value);
        Assert.AreEqual("explicit-path", candidates[0].Source);
        CollectionAssert.Contains(candidates.Select(candidate => candidate.Value).ToArray(), Path.Combine(applicationBase, "runtimes", rid, "native", fileName));
        CollectionAssert.Contains(candidates.Select(candidate => candidate.Value).ToArray(), Path.Combine(rocmRoot, isWindows ? "bin" : "lib", fileName));
        Assert.AreEqual(fileName, candidates[candidates.Length - 1].Value);
    }

    [TestMethod]
    public void LoaderReportsEveryFailedCandidateAndPlatformFacts()
    {
        var platform = new HipPlatformInfo(false, true, "Linux test", "x64", ".NET 10", "linux-x64");
        var locator = new HipLibraryLocator(platform, "/opt/test-app", _ => null);
        var backend = new AlwaysFailBackend();
        var loader = new HipNativeLibraryLoader(platform, locator, backend);

        HipLibraryLoadException exception = Assert.ThrowsExactly<HipLibraryLoadException>(() => loader.Load("/private/home/libamdhip64.so"));

        Assert.AreEqual("Linux test", exception.Diagnostics.OperatingSystem);
        Assert.AreEqual("linux-x64", exception.Diagnostics.RuntimeIdentifier);
        Assert.AreEqual("amdhip64", exception.Diagnostics.LibraryName);
        Assert.HasCount(7, exception.Diagnostics.Attempts);
        Assert.IsTrue(exception.Diagnostics.Attempts.All(attempt => !attempt.Succeeded));
        Assert.AreEqual(exception.Diagnostics.Attempts.Count, backend.Candidates.Count);
        Assert.AreEqual("<explicit-path>/libamdhip64.so", exception.Diagnostics.Attempts[0].Candidate);
        Assert.IsFalse(exception.Diagnostics.Attempts.Any(attempt => attempt.Candidate.Contains("/private/home", StringComparison.Ordinal)));
        StringAssert.Contains(exception.Message, "Architecture=x64");
    }

    [TestMethod]
    public void RtcLocatorAndDiagnosticsRemainIndependentFromRuntime()
    {
        var platform = new HipPlatformInfo(false, true, "Linux test", "x64", ".NET 10", "linux-x64");
        var locator = new HipLibraryLocator(platform, "/opt/test-app", _ => null, HipNativeLibraryKind.Rtc);
        HipLibraryCandidate[] candidates = locator.GetCandidates(null).ToArray();

        Assert.AreEqual(Path.Combine("/opt/test-app", "libhiprtc.so"), candidates[0].Value);
        Assert.AreEqual("libhiprtc.so", candidates[candidates.Length - 1].Value);
        CollectionAssert.Contains(candidates.Select(candidate => candidate.Value).ToArray(), "libhiprtc.so.7");

        var loader = new HipNativeLibraryLoader(platform, locator, new AlwaysFailBackend(), HipNativeLibraryKind.Rtc);
        HipLibraryLoadException exception = Assert.ThrowsExactly<HipLibraryLoadException>(() => loader.Load(null));
        Assert.AreEqual("hiprtc", exception.Diagnostics.LibraryName);
        StringAssert.Contains(exception.Message, "hiprtc");
        Assert.IsFalse(exception.Diagnostics.Attempts.Any(attempt => attempt.Candidate.Contains("amdhip64", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void WindowsLocatorUsesPinnedRocm72NamesAndStableSdkOrder()
    {
        string root = Path.GetPathRoot(AppContext.BaseDirectory)!;
        string programFiles = Path.Combine(root, "Program Files");
        string rocmRoot = Path.Combine(programFiles, "AMD", "ROCm");
        string installed72 = Path.Combine(rocmRoot, "7.2");
        string installed710 = Path.Combine(rocmRoot, "7.1.0");
        var platform = new HipPlatformInfo(true, false, "Windows test", "x64", ".NET 10", "win-x64");
        var runtimeLocator = new HipLibraryLocator(platform, Path.Combine(root, "app"),
            name => name == "ProgramFiles" ? programFiles : null,
            directoryEnumerator: _ => new[] { installed710, installed72 });
        var rtcLocator = new HipLibraryLocator(platform, Path.Combine(root, "app"),
            name => name == "ProgramFiles" ? programFiles : null, HipNativeLibraryKind.Rtc,
            _ => new[] { installed710, installed72 });

        HipLibraryCandidate[] runtime = runtimeLocator.GetCandidates(null).ToArray();
        HipLibraryCandidate[] rtc = rtcLocator.GetCandidates(null).ToArray();

        CollectionAssert.Contains(runtime.Select(candidate => candidate.Value).ToArray(),
            Path.Combine(installed72, "bin", "amdhip64_7.dll"));
        Assert.IsTrue(Array.FindIndex(runtime, candidate => candidate.Value == Path.Combine(installed72, "bin", "amdhip64_7.dll")) <
            Array.FindIndex(runtime, candidate => candidate.Value == Path.Combine(installed710, "bin", "amdhip64_7.dll")));
        Assert.AreEqual("amdhip64_7.dll", runtime[^1].Value);
        Assert.AreEqual("hiprtc0702.dll", rtc[^1].Value);
        Assert.IsTrue(runtime.Take(runtime.Length - 1).All(candidate => candidate.Source != "operating-system-search"));
    }

    [TestMethod]
    public void SuccessfulLoadsRecordAClosureIdentityThatDistinguishesPackageAndSystemModes()
    {
        var platform = new HipPlatformInfo(false, true, "Linux test", "x64", ".NET 10", "linux-x64");
        var localLocator = new HipLibraryLocator(platform, "/app", _ => null);
        var localLoader = new HipNativeLibraryLoader(platform, localLocator, new FirstCandidateBackend());

        HipNativeLibraryLoadResult local = localLoader.Load(null);

        Assert.AreEqual("application-base", local.Candidate.Source);
        Assert.AreEqual("directory:" + Path.GetDirectoryName(Path.GetFullPath("/app/libamdhip64.so")), local.ClosureIdentity);

        var systemLocator = new HipLibraryLocator(platform, "/missing", _ => null);
        var systemLoader = new HipNativeLibraryLoader(platform, systemLocator, new NamedCandidateBackend("libamdhip64.so"));
        HipNativeLibraryLoadResult system = systemLoader.Load(null);

        Assert.AreEqual("operating-system-search", system.Candidate.Source);
        Assert.AreEqual("search:operating-system-search", system.ClosureIdentity);
        Assert.AreNotEqual(local.ClosureIdentity, system.ClosureIdentity);
    }

    [TestMethod]
    public void LoaderFreesALoadedCandidateWhenItsIdentityExportIsMissing()
    {
        var platform = new HipPlatformInfo(false, true, "Linux test", "x64", ".NET 10", "linux-x64");
        var locator = new HipLibraryLocator(platform, "/app", _ => null);
        var backend = new MissingFirstExportBackend();
        var loader = new HipNativeLibraryLoader(platform, locator, backend);

        HipNativeLibraryLoadResult result = loader.Load(null);
        string[] expectedProbes = { "hipInit", "hipInit" };

        Assert.AreEqual("runtime-asset", result.Candidate.Source);
        Assert.AreEqual(1, backend.FreeCount);
        CollectionAssert.AreEqual(expectedProbes, backend.Probes);
    }

    private sealed class AlwaysFailBackend : INativeLibraryBackend
    {
        internal List<string> Candidates { get; } = new();

        public bool TryLoad(string candidate, out IntPtr handle, out string detail)
        {
            Candidates.Add(candidate);
            handle = IntPtr.Zero;
            detail = "not found";
            return false;
        }

        public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail)
        {
            address = IntPtr.Zero;
            detail = "not loaded";
            return false;
        }

        public void Free(IntPtr handle) { }
    }

    private sealed class FirstCandidateBackend : INativeLibraryBackend
    {
        public bool TryLoad(string candidate, out IntPtr handle, out string detail)
        {
            handle = new IntPtr(1);
            detail = "loaded";
            return true;
        }

        public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail)
        {
            address = new IntPtr(2);
            detail = "export-found";
            return true;
        }

        public void Free(IntPtr handle) { }
    }

    private sealed class NamedCandidateBackend : INativeLibraryBackend
    {
        private readonly string _expected;

        internal NamedCandidateBackend(string expected) => _expected = expected;

        public bool TryLoad(string candidate, out IntPtr handle, out string detail)
        {
            bool succeeded = string.Equals(candidate, _expected, StringComparison.Ordinal);
            handle = succeeded ? new IntPtr(2) : IntPtr.Zero;
            detail = succeeded ? "loaded" : "not found";
            return succeeded;
        }

        public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail)
        {
            address = new IntPtr(3);
            detail = "export-found";
            return true;
        }

        public void Free(IntPtr handle) { }
    }

    private sealed class MissingFirstExportBackend : INativeLibraryBackend
    {
        private int _loadCount;
        internal int FreeCount { get; private set; }
        internal string[] Probes => _probes.ToArray();
        private readonly List<string> _probes = new();

        public bool TryLoad(string candidate, out IntPtr handle, out string detail)
        {
            _loadCount++;
            handle = new IntPtr(_loadCount);
            detail = "loaded";
            return true;
        }

        public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail)
        {
            _probes.Add(entryPoint);
            bool found = handle == new IntPtr(2);
            address = found ? new IntPtr(10) : IntPtr.Zero;
            detail = found ? "export-found" : "export-not-found";
            return found;
        }

        public void Free(IntPtr handle) => FreeCount++;
    }
}
