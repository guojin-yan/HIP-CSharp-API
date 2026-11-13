using System;
using System.Runtime.InteropServices;
#if NETCOREAPP3_1_OR_GREATER
using System.Reflection;
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
    private static IntPtr _rtcHandle;
#if NETCOREAPP3_1_OR_GREATER
    private static bool _resolverInstalled;
    private static IntPtr _runtimeHandle;
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
                new NativeLibraryBackend().Free(result.Handle);
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
                _rtcHandle = handle;
                _rtcExplicitLibraryPath = explicitLibraryPath;
                _rtcLoaded = true;
            }

            _closureIdentity ??= result.ClosureIdentity;
        }
    }

#if NETFRAMEWORK
    internal static T GetRtcDelegate<T>(string entryPoint) where T : Delegate
    {
        lock (Sync)
        {
            if (!_rtcLoaded) throw new InvalidOperationException("Load HIPRTC before calling its entry points.");
            if (!new NativeLibraryBackend().TryGetExport(_rtcHandle, entryPoint, out IntPtr address, out string detail))
                throw new EntryPointNotFoundException(detail);
            return Marshal.GetDelegateForFunctionPointer<T>(address);
        }
    }
#endif

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
