using System;
using System.Collections.Generic;
using System.IO;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 执行 HIP Runtime 原生库定位并构造可操作的失败诊断 / Locates the HIP Runtime native library and builds actionable failure diagnostics.
/// </summary>
internal sealed class HipNativeLibraryLoader
{
    private readonly HipPlatformInfo _platform;
    private readonly HipLibraryLocator _locator;
    private readonly INativeLibraryBackend _backend;

    internal HipNativeLibraryLoader()
        : this(HipPlatformInfo.Current(), null, null)
    {
    }

    internal HipNativeLibraryLoader(HipPlatformInfo platform, HipLibraryLocator? locator, INativeLibraryBackend? backend)
    {
        _platform = platform;
        _locator = locator ?? new HipLibraryLocator(platform, AppContext.BaseDirectory, Environment.GetEnvironmentVariable);
        _backend = backend ?? new NativeLibraryBackend();
    }

    internal IntPtr Load(string? explicitLibraryPath)
    {
        var attempts = new List<HipLibraryLoadAttempt>();
        if (!_platform.IsWindows && !_platform.IsLinux)
        {
            attempts.Add(new HipLibraryLoadAttempt("amdhip64", "platform-check", false, "Only Windows and Linux are supported."));
            throw CreateException(attempts);
        }

        foreach (HipLibraryCandidate candidate in _locator.GetCandidates(explicitLibraryPath))
        {
            bool succeeded = _backend.TryLoad(candidate.Value, out IntPtr handle, out string detail);
            string displayCandidate = RedactCandidate(candidate);
            string displayDetail = detail.Replace(candidate.Value, displayCandidate);
            if (Path.IsPathRooted(candidate.Value) && File.Exists(candidate.Value))
            {
                displayDetail = "file-exists; " + displayDetail;
            }

            attempts.Add(new HipLibraryLoadAttempt(displayCandidate, candidate.Source, succeeded, displayDetail));
            if (succeeded)
            {
                return handle;
            }
        }

        throw CreateException(attempts);
    }

    private HipLibraryLoadException CreateException(IList<HipLibraryLoadAttempt> attempts) =>
        new(new HipLibraryLoadDiagnostics(
            _platform.OperatingSystem,
            _platform.ProcessArchitecture,
            _platform.TargetFramework,
            _platform.RuntimeIdentifier,
            attempts));

    private string RedactCandidate(HipLibraryCandidate candidate)
    {
        string fileName = Path.GetFileName(candidate.Value);
        return candidate.Source switch
        {
            "explicit-path" => "<explicit-path>/" + fileName,
            "application-base" => "<application-base>/" + fileName,
            "runtime-asset" => "<application-base>/runtimes/" + _platform.RuntimeIdentifier + "/native/" + fileName,
            "ROCM_PATH" => "<ROCM_PATH>/.../" + fileName,
            "HIP_PATH" => "<HIP_PATH>/.../" + fileName,
            "standard-rocm" => "<rocm-default>/.../" + fileName,
            _ => candidate.Value,
        };
    }
}
