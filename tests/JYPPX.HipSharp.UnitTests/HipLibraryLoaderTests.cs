using System;
using System.Collections.Generic;
using System.Linq;
using JYPPX.HipSharp.Loading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipLibraryLoaderTests
{
    [TestMethod]
    public void LocatorUsesExplicitApplicationRuntimeEnvironmentAndOsOrder()
    {
        var platform = new HipPlatformInfo(true, false, "Windows", "x64", ".NET 10", "win-x64");
        var locator = new HipLibraryLocator(
            platform,
            @"C:\app",
            name => name == "ROCM_PATH" ? @"C:\rocm" : null);

        HipLibraryCandidate[] candidates = locator.GetCandidates(@"C:\custom\amdhip64_7.dll").ToArray();

        Assert.AreEqual(@"C:\custom\amdhip64_7.dll", candidates[0].Value);
        Assert.AreEqual("explicit-path", candidates[0].Source);
        CollectionAssert.Contains(candidates.Select(candidate => candidate.Value).ToArray(), @"C:\app\runtimes\win-x64\native\amdhip64_7.dll");
        CollectionAssert.Contains(candidates.Select(candidate => candidate.Value).ToArray(), @"C:\rocm\bin\amdhip64_7.dll");
        Assert.AreEqual("amdhip64_7.dll", candidates[candidates.Length - 1].Value);
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
        Assert.HasCount(7, exception.Diagnostics.Attempts);
        Assert.IsTrue(exception.Diagnostics.Attempts.All(attempt => !attempt.Succeeded));
        Assert.AreEqual(exception.Diagnostics.Attempts.Count, backend.Candidates.Count);
        Assert.AreEqual("<explicit-path>/libamdhip64.so", exception.Diagnostics.Attempts[0].Candidate);
        Assert.IsFalse(exception.Diagnostics.Attempts.Any(attempt => attempt.Candidate.Contains("/private/home", StringComparison.Ordinal)));
        StringAssert.Contains(exception.Message, "Architecture=x64");
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
    }
}
