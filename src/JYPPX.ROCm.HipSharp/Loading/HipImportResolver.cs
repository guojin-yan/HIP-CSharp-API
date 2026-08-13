using System;
#if NETCOREAPP3_1_OR_GREATER
using System.Reflection;
using System.Runtime.InteropServices;
#endif
using JYPPX.ROCm.HipSharp.Generated;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 将已验证的原生库句柄绑定到生成的 P/Invoke 声明 / Binds a verified native-library handle to generated P/Invoke declarations.
/// </summary>
internal static class HipImportResolver
{
    private static readonly object Sync = new();
    private static bool _runtimeLoaded;
    private static bool _rtcLoaded;
    private static string? _runtimeExplicitLibraryPath;
    private static string? _rtcExplicitLibraryPath;
    private static string? _closureIdentity;
#if NETCOREAPP3_1_OR_GREATER
    private static bool _resolverInstalled;
    private static IntPtr _runtimeHandle;
    private static IntPtr _rtcHandle;
#endif

    internal static void EnsureLoaded(HipNativeLibraryKind libraryKind, string? explicitLibraryPath)
    {
        lock (Sync)
        {
            bool loaded = libraryKind == HipNativeLibraryKind.Runtime ? _runtimeLoaded : _rtcLoaded;
            string? loadedPath = libraryKind == HipNativeLibraryKind.Runtime ? _runtimeExplicitLibraryPath : _rtcExplicitLibraryPath;
            if (loaded)
            {
                if (!string.IsNullOrWhiteSpace(explicitLibraryPath) &&
                    !string.Equals(loadedPath, explicitLibraryPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The HIP native library has already been loaded from another location.");
                }

                return;
            }

            HipNativeLibraryLoadResult result = new HipNativeLibraryLoader(libraryKind).Load(explicitLibraryPath);
            if (_closureIdentity is not null && !string.Equals(_closureIdentity, result.ClosureIdentity, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "HIP Runtime and HIPRTC must be loaded from the same user-mode closure. " +
                    "A package-local/system ROCm mix or two different native directories is not allowed.");
            }

            IntPtr handle = result.Handle;
#if NETCOREAPP3_1_OR_GREATER
            if (!_resolverInstalled)
            {
                NativeLibrary.SetDllImportResolver(typeof(HipNativeMethods).Assembly, Resolve);
                _resolverInstalled = true;
            }
#endif
            if (libraryKind == HipNativeLibraryKind.Runtime)
            {
#if NETCOREAPP3_1_OR_GREATER
                _runtimeHandle = handle;
#endif
                _runtimeExplicitLibraryPath = explicitLibraryPath;
                _runtimeLoaded = true;
            }
            else
            {
#if NETCOREAPP3_1_OR_GREATER
                _rtcHandle = handle;
#endif
                _rtcExplicitLibraryPath = explicitLibraryPath;
                _rtcLoaded = true;
            }

            _closureIdentity ??= result.ClosureIdentity;
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (string.Equals(libraryName, HipNativeLibraryNames.RuntimeImportName, StringComparison.Ordinal))
        {
            return _runtimeHandle;
        }

        return string.Equals(libraryName, HipNativeLibraryNames.RtcImportName, StringComparison.Ordinal) ? _rtcHandle : IntPtr.Zero;
    }
#endif
}
