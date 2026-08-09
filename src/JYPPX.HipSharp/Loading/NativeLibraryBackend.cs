using System;
#if NETFRAMEWORK
using System.ComponentModel;
using System.Runtime.InteropServices;
#endif

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 使用当前运行时的安全原生库 API 加载候选项 / Loads candidates with the current runtime's safe native-library API.
/// </summary>
internal sealed class NativeLibraryBackend : INativeLibraryBackend
{
    public bool TryLoad(string candidate, out IntPtr handle, out string detail)
    {
#if NETCOREAPP3_1_OR_GREATER
        try
        {
            handle = System.Runtime.InteropServices.NativeLibrary.Load(candidate);
            detail = "loaded";
            return true;
        }
        catch (Exception exception) when (exception is DllNotFoundException || exception is BadImageFormatException)
        {
            handle = IntPtr.Zero;
            detail = exception.GetType().Name + ": " + exception.Message;
            return false;
        }
#else
        const uint LoadLibrarySearchDllLoadDirectory = 0x00000100;
        const uint LoadLibrarySearchDefaultDirectories = 0x00001000;
        uint flags = System.IO.Path.IsPathRooted(candidate)
            ? LoadLibrarySearchDllLoadDirectory | LoadLibrarySearchDefaultDirectories
            : LoadLibrarySearchDefaultDirectories;
        handle = LoadLibraryEx(candidate, IntPtr.Zero, flags);
        if (handle != IntPtr.Zero)
        {
            detail = "loaded";
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        detail = "Win32Error " + error + ": " + new Win32Exception(error).Message;
        return false;
#endif
    }

#if NETFRAMEWORK
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);
#endif
}
