using System;
using System.Collections.Generic;
using System.IO;

namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 执行 HIP 原生组件定位并构造可操作的失败诊断 / Locates a HIP native component and builds actionable failure diagnostics.
/// </summary>
internal sealed class HipNativeLibraryLoader
{
    private readonly HipPlatformInfo _platform;
    private readonly HipLibraryLocator _locator;
    private readonly INativeLibraryBackend _backend;
    private readonly HipNativeLibraryKind _libraryKind;

    internal HipNativeLibraryLoader(HipNativeLibraryKind libraryKind)
        : this(HipPlatformInfo.Current(), null, null, libraryKind)
    {
    }

    internal HipNativeLibraryLoader(
        HipPlatformInfo platform,
        HipLibraryLocator? locator,
        INativeLibraryBackend? backend,
        HipNativeLibraryKind libraryKind = HipNativeLibraryKind.Runtime)
    {
        _platform = platform;
        _libraryKind = libraryKind;
        _locator = locator ?? new HipLibraryLocator(platform, AppContext.BaseDirectory, Environment.GetEnvironmentVariable, libraryKind);
        _backend = backend ?? new NativeLibraryBackend();
    }

    internal HipNativeLibraryLoadResult Load(string? explicitLibraryPath)
    {
        var attempts = new List<HipLibraryLoadAttempt>();
        if (!_platform.IsWindows && !_platform.IsLinux)
        {
            attempts.Add(new HipLibraryLoadAttempt(GetLogicalName(), "platform-check", false, "Only Windows and Linux are supported."));
            throw CreateException(attempts);
        }

        foreach (HipLibraryCandidate candidate in _locator.GetCandidates(explicitLibraryPath))
        {
            bool succeeded = _backend.TryLoad(candidate.Value, out IntPtr handle, out string detail);
            if (succeeded && !_backend.TryGetExport(handle, GetProbeExport(), out _, out string exportDetail))
            {
                _backend.Free(handle);
                handle = IntPtr.Zero;
                succeeded = false;
                detail += "; " + exportDetail;
            }
            string displayCandidate = RedactCandidate(candidate);
            string displayDetail = detail.Replace(candidate.Value, displayCandidate);
            if (Path.IsPathRooted(candidate.Value) && File.Exists(candidate.Value))
            {
                displayDetail = "file-exists; " + displayDetail;
            }

            attempts.Add(new HipLibraryLoadAttempt(displayCandidate, candidate.Source, succeeded, displayDetail));
            if (succeeded)
            {
                return new HipNativeLibraryLoadResult(handle, candidate);
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
            GetLogicalName(),
            attempts));

    private string GetLogicalName() => _libraryKind == HipNativeLibraryKind.Runtime ? "amdhip64" : "hiprtc";

    private string GetProbeExport() => _libraryKind == HipNativeLibraryKind.Runtime ? "hipInit" : "hiprtcVersion";

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
