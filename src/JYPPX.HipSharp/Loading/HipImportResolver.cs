using System;
#if NETCOREAPP3_1_OR_GREATER
using System.Reflection;
using System.Runtime.InteropServices;
#endif
using JYPPX.HipSharp.Generated;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 将已验证的原生库句柄绑定到生成的 P/Invoke 声明 / Binds a verified native-library handle to generated P/Invoke declarations.
/// </summary>
internal static class HipImportResolver
{
    private static readonly object Sync = new();
    private static bool _loaded;
    private static string? _explicitLibraryPath;
#if NETCOREAPP3_1_OR_GREATER
    private static IntPtr _handle;
#endif

    internal static void EnsureLoaded(string? explicitLibraryPath)
    {
        lock (Sync)
        {
            if (_loaded)
            {
                if (!string.IsNullOrWhiteSpace(explicitLibraryPath) &&
                    !string.Equals(_explicitLibraryPath, explicitLibraryPath, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("The HIP Runtime library has already been loaded from another location.");
                }

                return;
            }

            IntPtr handle = new HipNativeLibraryLoader().Load(explicitLibraryPath);
#if NETCOREAPP3_1_OR_GREATER
            NativeLibrary.SetDllImportResolver(typeof(HipNativeMethods).Assembly, Resolve);
            _handle = handle;
#endif
            _explicitLibraryPath = explicitLibraryPath;
            _loaded = true;
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    private static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath) =>
        string.Equals(libraryName, HipNativeLibraryNames.RuntimeImportName, StringComparison.Ordinal) ? _handle : IntPtr.Zero;
#endif
}
