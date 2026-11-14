using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class PackageModuleAuditTests
{
    private string _root = null!;
    private string _app = null!;
    private string _runtime = null!;
    private string _rtc = null!;
    private Dictionary<string, string> _expected = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Environment.GetEnvironmentVariable("HIPSHARP_OUTPUT_ROOT") ?? Path.GetTempPath(),
            "artifacts", "module-audit-tests", Guid.NewGuid().ToString("N"));
        _app = Path.Combine(_root, "consumer");
        Directory.CreateDirectory(_app);
        _runtime = Path.Combine(_app, OperatingSystem.IsWindows() ? "amdhip64_7.dll" : "libamdhip64.so.7");
        _rtc = Path.Combine(_app, OperatingSystem.IsWindows() ? "hiprtc0716.dll" : "libhiprtc.so.7");
        File.WriteAllText(_runtime, "runtime fixture");
        File.WriteAllText(_rtc, "compiler fixture");
        _expected = new Dictionary<string, string>
        {
            [Path.GetFileName(_runtime)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_runtime))),
            [Path.GetFileName(_rtc)] = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(_rtc))),
        };
    }

    [TestCleanup]
    public void Cleanup() => Directory.Delete(_root, true);

    [TestMethod]
    public void ExactNamedFilesAndHarmlessOsModulesPass()
    {
        var actual = PackageModuleAudit.Verify(_app, _expected, new[] { _runtime, _rtc, _runtime, Path.Combine(_root, "system.dll") });
        CollectionAssert.AreEqual(new[] { Path.GetFullPath(_runtime), Path.GetFullPath(_rtc) }, actual);
    }

    [TestMethod]
    public void SwappingTwoAllowlistedFilesIsRejected()
    {
        byte[] bytes = File.ReadAllBytes(_runtime);
        File.Copy(_rtc, _runtime, true);
        File.WriteAllBytes(_rtc, bytes);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(
            () => PackageModuleAudit.Verify(_app, _expected, new[] { _runtime, _rtc })).Message, "filename/hash mismatch");
    }

    [TestMethod]
    public void SiblingWithApplicationPrefixIsRejected()
    {
        string sibling = _app + "-other";
        Directory.CreateDirectory(sibling);
        string path = Path.Combine(sibling, Path.GetFileName(_runtime));
        File.Copy(_runtime, path);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(
            () => PackageModuleAudit.Verify(_app, _expected, new[] { path })).Message, "outside application");
    }

    [TestMethod]
    public void UnlistedVersionOfHipLibraryIsRejected()
    {
        string other = Path.Combine(_root, OperatingSystem.IsWindows() ? "hiprtc0702.dll" : "libhiprtc.so.99");
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(
            () => PackageModuleAudit.Verify(_app, _expected, new[] { _runtime, other })).Message, "Unlisted HIP closure");
    }

    [TestMethod]
    public void NoObservedPackagedModuleIsRejected()
    {
        Assert.ThrowsExactly<InvalidOperationException>(() => PackageModuleAudit.Verify(_app, _expected, Array.Empty<string>()));
    }

    [TestMethod]
    public void InvalidManifestHashIsRejected()
    {
        _expected[Path.GetFileName(_runtime)] = "not a sha256";
        Assert.ThrowsExactly<InvalidOperationException>(() => PackageModuleAudit.Verify(_app, _expected, new[] { _runtime }));
    }

    [TestMethod]
    public void DirectoryCaseComparisonMatchesHostSemantics()
    {
        string other = Path.Combine(_root, "CONSUMER");
        string path = Path.Combine(other, Path.GetFileName(_runtime));
        if (OperatingSystem.IsWindows())
        {
            Assert.HasCount(1, PackageModuleAudit.Verify(_app, _expected, new[] { path }));
            return;
        }
        // Prefix rejection precedes file access, including on a WSL NTFS mount
        // whose physical file lookup may itself ignore case.
        Assert.ThrowsExactly<InvalidOperationException>(() => PackageModuleAudit.Verify(_app, _expected, new[] { path }));
    }

    [TestMethod]
    public void LinkedNativeFileOrParentDirectoryIsRejected()
    {
        if (OperatingSystem.IsWindows())
        {
            string external = Path.Combine(_root, "external");
            Directory.CreateDirectory(external);
            File.Copy(_runtime, Path.Combine(external, Path.GetFileName(_runtime)));
            string link = Path.Combine(_app, "linked");
            var start = new ProcessStartInfo("cmd.exe") { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (string arg in new[] { "/c", "mklink", "/J", Path.GetFullPath(link), Path.GetFullPath(external) }) start.ArgumentList.Add(arg);
            using Process process = Process.Start(start)!;
            process.WaitForExit();
            Assert.AreEqual(0, process.ExitCode, process.StandardError.ReadToEnd());
            try
            {
                StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(() => PackageModuleAudit.Verify(
                    _app, _expected, new[] { Path.Combine(link, Path.GetFileName(_runtime)) })).Message, "uses a link");
            }
            finally { Directory.Delete(link); }
            return;
        }
        string outside = Path.Combine(_root, "external.so");
        File.Move(_runtime, outside);
        File.CreateSymbolicLink(_runtime, outside);
        StringAssert.Contains(Assert.ThrowsExactly<InvalidOperationException>(
            () => PackageModuleAudit.Verify(_app, _expected, new[] { _runtime })).Message, "uses a link");
    }
}
