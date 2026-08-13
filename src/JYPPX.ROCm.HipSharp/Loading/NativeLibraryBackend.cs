using System;
#if NETFRAMEWORK
using System.ComponentModel;
using System.Runtime.InteropServices;
#endif

namespace JYPPX.ROCm.HipSharp.Loading;

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

    public bool TryGetExport(IntPtr handle, string entryPoint, out IntPtr address, out string detail)
    {
#if NETCOREAPP3_1_OR_GREATER
        bool found = System.Runtime.InteropServices.NativeLibrary.TryGetExport(handle, entryPoint, out address);
        detail = found ? "export-found" : "export-not-found: " + entryPoint;
        return found;
#else
        IntPtr nativeEntryPoint = Marshal.StringToHGlobalAnsi(entryPoint);
        try
        {
            address = GetProcAddress(handle, nativeEntryPoint);
        }
        finally
        {
            Marshal.FreeHGlobal(nativeEntryPoint);
        }
        if (address != IntPtr.Zero)
        {
            detail = "export-found";
            return true;
        }

        int error = Marshal.GetLastWin32Error();
        detail = "export-not-found: " + entryPoint + "; Win32Error " + error + ": " + new Win32Exception(error).Message;
        return false;
#endif
    }

    public void Free(IntPtr handle)
    {
        if (handle == IntPtr.Zero) return;
#if NETCOREAPP3_1_OR_GREATER
        System.Runtime.InteropServices.NativeLibrary.Free(handle);
#else
        _ = FreeLibrary(handle);
#endif
    }

#if NETFRAMEWORK
    [DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true, EntryPoint = "LoadLibraryExW")]
    private static extern IntPtr LoadLibraryEx(string fileName, IntPtr file, uint flags);

    [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
    private static extern IntPtr GetProcAddress(IntPtr module, IntPtr procedureName);

    [DllImport("kernel32", SetLastError = true, ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool FreeLibrary(IntPtr module);
#endif
}
