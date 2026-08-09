using System;
using System.Reflection;
using System.Runtime.Versioning;
#if NETCOREAPP3_1_OR_GREATER
using System.Runtime.InteropServices;
#endif

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 提供原生库探测所需的平台事实 / Provides platform facts required by native-library probing.
/// </summary>
internal sealed class HipPlatformInfo
{
    internal HipPlatformInfo(bool isWindows, bool isLinux, string operatingSystem, string processArchitecture, string targetFramework, string runtimeIdentifier)
    {
        IsWindows = isWindows;
        IsLinux = isLinux;
        OperatingSystem = operatingSystem;
        ProcessArchitecture = processArchitecture;
        TargetFramework = targetFramework;
        RuntimeIdentifier = runtimeIdentifier;
    }

    internal bool IsWindows { get; }

    internal bool IsLinux { get; }

    internal string OperatingSystem { get; }

    internal string ProcessArchitecture { get; }

    internal string TargetFramework { get; }

    internal string RuntimeIdentifier { get; }

    internal static HipPlatformInfo Current()
    {
#if NETCOREAPP3_1_OR_GREATER
        bool isWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool isLinux = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);
        string operatingSystem = RuntimeInformation.OSDescription;
        string architecture = RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant();
#else
        bool isWindows = Environment.OSVersion.Platform == PlatformID.Win32NT;
        bool isLinux = false;
        string operatingSystem = Environment.OSVersion.VersionString;
        string architecture = IntPtr.Size == 8 ? "x64" : "x86";
#endif
        string tfm = typeof(HipPlatformInfo).Assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "unknown";
        string rid = (isWindows ? "win-" : isLinux ? "linux-" : "unknown-") + architecture;
        return new HipPlatformInfo(isWindows, isLinux, operatingSystem, architecture, tfm, rid);
    }
}
