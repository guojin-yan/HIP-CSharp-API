using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

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
    private readonly Func<string, IEnumerable<string>> _fileEnumerator;

    internal HipLibraryLocator(
        HipPlatformInfo platform,
        string applicationBase,
        Func<string, string?> environmentReader,
        HipNativeLibraryKind libraryKind = HipNativeLibraryKind.Runtime,
        Func<string, IEnumerable<string>>? directoryEnumerator = null,
        Func<string, IEnumerable<string>>? fileEnumerator = null)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _applicationBase = applicationBase ?? throw new ArgumentNullException(nameof(applicationBase));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
        _libraryKind = libraryKind;
        _directoryEnumerator = directoryEnumerator ?? EnumerateDirectories;
        _fileEnumerator = fileEnumerator ?? EnumerateRtcFiles;
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

        AddDirectory(candidates, seen, _applicationBase, fileName, "application-base");
        AddDirectory(candidates, seen, Path.Combine(_applicationBase, "runtimes", _platform.RuntimeIdentifier, "native"), fileName, "runtime-asset");

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
                .OrderByDescending(path => Version.TryParse(Path.GetFileName(path), out Version? version) ? version : new Version())
                .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            installedRoots = Array.Empty<string>();
        }

        foreach (string installedRoot in installedRoots)
        {
            AddDirectory(candidates, seen, Path.Combine(installedRoot, "bin"), fileName, "standard-rocm");
            AddDirectory(candidates, seen, Path.Combine(installedRoot, "HIP", "bin"), fileName, "standard-rocm");
        }

        Add(candidates, seen, Path.Combine(rocmRoot, "bin", fileName), "standard-rocm");
    }

    private static IEnumerable<string> EnumerateDirectories(string path) =>
        Directory.Exists(path) ? Directory.EnumerateDirectories(path) : Array.Empty<string>();

    private static IEnumerable<string> EnumerateRtcFiles(string path) =>
        Directory.Exists(path) ? Directory.EnumerateFiles(path, "hiprtc*.dll") : Array.Empty<string>();

    private void AddDirectory(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string directory, string fileName, string source)
    {
        if (_platform.IsWindows && _libraryKind == HipNativeLibraryKind.Rtc)
        {
            directory = NormalizeDirectory(directory);
            try
            {
                string[] files = _fileEnumerator(directory)
                    .Where(path => Path.IsPathRooted(path) && string.Equals(NormalizeDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!), directory, StringComparison.OrdinalIgnoreCase))
                    .Where(path => Regex.IsMatch(Path.GetFileName(path), @"^hiprtc[0-9]{4}\.dll$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
                    .OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase).ToArray();
                foreach (string file in files) Add(candidates, seen, file, source);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
            {
                // Keep the legacy candidate and subsequent roots available for diagnostics.
            }
        }
        Add(candidates, seen, Path.Combine(directory, fileName), source);
    }

    private static string NormalizeDirectory(string directory)
    {
        string fullPath = Path.GetFullPath(directory);
        int rootLength = Path.GetPathRoot(fullPath)!.Length;
        while (fullPath.Length > rootLength &&
               (fullPath[fullPath.Length - 1] == Path.DirectorySeparatorChar || fullPath[fullPath.Length - 1] == Path.AltDirectorySeparatorChar))
        {
            fullPath = fullPath.Substring(0, fullPath.Length - 1);
        }
        return fullPath;
    }

    private void AddEnvironmentRoot(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string variableName, string fileName)
    {
        string? root = _environmentReader(variableName);
        if (string.IsNullOrWhiteSpace(root) || !Path.IsPathRooted(root))
        {
            return;
        }

        string subdirectory = _platform.IsWindows ? "bin" : "lib";
        AddDirectory(candidates, seen, Path.Combine(root, subdirectory), fileName, variableName);
        AddDirectory(candidates, seen, root!, fileName, variableName);
    }

    private static void Add(ICollection<HipLibraryCandidate> candidates, ISet<string> seen, string value, string source)
    {
        if (seen.Add(value))
        {
            candidates.Add(new HipLibraryCandidate(value, source));
        }
    }
}
