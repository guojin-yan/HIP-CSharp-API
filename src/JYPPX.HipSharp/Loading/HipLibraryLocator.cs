using System;
using System.Collections.Generic;
using System.IO;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 以稳定顺序生成 HIP Runtime 库候选项 / Produces HIP Runtime library candidates in a stable order.
/// </summary>
internal sealed class HipLibraryLocator
{
    private readonly HipPlatformInfo _platform;
    private readonly string _applicationBase;
    private readonly Func<string, string?> _environmentReader;

    internal HipLibraryLocator(HipPlatformInfo platform, string applicationBase, Func<string, string?> environmentReader)
    {
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _applicationBase = applicationBase ?? throw new ArgumentNullException(nameof(applicationBase));
        _environmentReader = environmentReader ?? throw new ArgumentNullException(nameof(environmentReader));
    }

    internal IList<HipLibraryCandidate> GetCandidates(string? explicitLibraryPath)
    {
        var candidates = new List<HipLibraryCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string fileName = _platform.IsWindows ? "amdhip64_7.dll" : "libamdhip64.so";

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
            Add(candidates, seen, "amdhip64_7.dll", "operating-system-search");
        }
        else
        {
            Add(candidates, seen, Path.Combine("/opt/rocm/lib", fileName), "standard-rocm");
            Add(candidates, seen, Path.Combine("/opt/rocm/lib64", fileName), "standard-rocm");
            Add(candidates, seen, "libamdhip64.so.7", "operating-system-search");
            Add(candidates, seen, fileName, "operating-system-search");
        }

        return candidates;
    }

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
