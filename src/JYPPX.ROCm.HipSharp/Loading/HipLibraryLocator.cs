using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 以稳定顺序生成 HIP 原生组件候选项 / Produces HIP native-component candidates in a stable order.
/// </summary>
internal sealed class HipLibraryLocator
{
    private readonly HipPlatformInfo _platform;
    private readonly string _applicationBase;
    private readonly Func<string, string?> _environmentReader;
    private readonly Func<string, IEnumerable<string>> _directoryEnumerator;
    private readonly HipNativeLibraryKind _libraryKind;

    internal HipLibraryLocator(
        HipPlatformInfo platform,
        string applicationBase,
        Func<string, string?> environmentReader,
        HipNativeLibraryKind libraryKind = HipNativeLibraryKind.Runtime,
        Func<string, IEnumerable<string>>? directoryEnumerator = null)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _applicationBase = applicationBase ?? throw new ArgumentNullException(nameof(applicationBase));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
        _libraryKind = libraryKind;
        _directoryEnumerator = directoryEnumerator ?? EnumerateDirectories;
    }

    internal IList<HipLibraryCandidate> GetCandidates(string? explicitLibraryPath)
    {
        var candidates = new List<HipLibraryCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string fileName = GetFileName();

        if (!string.IsNullOrWhiteSpace(explicitLibraryPath))
        {
            if (!Path.IsPathRooted(explicitLibraryPath))
            {
                throw new ArgumentException("The explicit HIP library path must be absolute.", nameof(explicitLibraryPath));
            }

            Add(candidates, seen, Path.GetFullPath(explicitLibraryPath), "explicit-path");
        }

        Add(candidates, seen, Path.Combine(_applicationBase, fileName), "application-base");
        Add(candidates, seen, Path.Combine(_applicationBase, "runtimes", _platform.RuntimeIdentifier, "native", fileName), "runtime-asset");

        AddEnvironmentRoot(candidates, seen, "ROCM_PATH", fileName);
        AddEnvironmentRoot(candidates, seen, "HIP_PATH", fileName);

        if (_platform.IsWindows)
        {
            AddWindowsSdkCandidates(candidates, seen, fileName);
            Add(candidates, seen, fileName, "operating-system-search");
        }
        else
        {
            Add(candidates, seen, Path.Combine("/opt/rocm/lib", fileName), "standard-rocm");
            Add(candidates, seen, Path.Combine("/opt/rocm/lib64", fileName), "standard-rocm");
            Add(candidates, seen, _libraryKind == HipNativeLibraryKind.Runtime ? "libamdhip64.so.7" : "libhiprtc.so.7", "operating-system-search");
            Add(candidates, seen, fileName, "operating-system-search");
        }

        return candidates;
    }

    private string GetFileName()
    {
        if (_libraryKind == HipNativeLibraryKind.Runtime)
        {
            return _platform.IsWindows ? HipWindowsSdkLayout.RuntimeFileName : "libamdhip64.so";
        }

        return _platform.IsWindows ? HipWindowsSdkLayout.RtcFileName : "libhiprtc.so";
    }

    private void AddWindowsSdkCandidates(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string fileName)
    {
        string? programFiles = _environmentReader("ProgramFiles");
        if (string.IsNullOrWhiteSpace(programFiles) || !Path.IsPathRooted(programFiles)) return;

        string rocmRoot = Path.Combine(programFiles, "AMD", "ROCm");
        string[] installedRoots;
        try
        {
            installedRoots = _directoryEnumerator(rocmRoot)
                .Where(Path.IsPathRooted)
                .OrderByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            installedRoots = Array.Empty<string>();
        }

        foreach (string installedRoot in installedRoots)
        {
            Add(candidates, seen, Path.Combine(installedRoot, "bin", fileName), "standard-rocm");
        }

        Add(candidates, seen, Path.Combine(rocmRoot, "bin", fileName), "standard-rocm");
    }

    private static IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.EnumerateDirectories(path) : Array.Empty<string>();

    private void AddEnvironmentRoot(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string variableName, string fileName)
    {
        string? root = _environmentReader(variableName);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            return;
        }

        string subdirectory = _platform.IsWindows ? "bin" : "lib";
        Add(candidates, seen, Path.Combine(root, subdirectory, fileName), variableName);
        Add(candidates, seen, Path.Combine(root, fileName), variableName);
    }

    private static void Add(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string value, string source)
    {
        if (seen.Add(value))
        {
            candidates.Add(new HipLibraryCandidate(value, source));
        }
    }
}
