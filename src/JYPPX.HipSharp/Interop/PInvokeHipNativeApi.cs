using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Generated;
using JYPPX.HipSharp.Loading;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 将语义化原生边界连接到生成的 P/Invoke 声明 / Connects the semantic native boundary to generated P/Invoke declarations.
/// </summary>
internal sealed class PInvokeHipNativeApi : IHipNativeApi
{
    internal PInvokeHipNativeApi(string? explicitLibraryPath)
    {
        HipImportResolver.EnsureLoaded(HipNativeLibraryKind.Runtime, explicitLibraryPath);
    }

    public HipError Init(uint flags) => HipNativeMethods.Init(flags);

    public HipError RuntimeGetVersion(out int runtimeVersion) => HipNativeMethods.RuntimeGetVersion(out runtimeVersion);

    public HipError DriverGetVersion(out int driverVersion) => HipNativeMethods.DriverGetVersion(out driverVersion);

    public HipError GetDeviceCount(out int count) => HipNativeMethods.GetDeviceCount(out count);

    public HipError GetDevice(out int deviceId) => HipNativeMethods.GetDevice(out deviceId);

    public HipError SetDevice(int deviceId) => HipNativeMethods.SetDevice(deviceId);

    public HipError DeviceGetName(int deviceId, out string name)
    {
        const int bufferLength = 256;
        IntPtr buffer = Marshal.AllocHGlobal(bufferLength);
        try
        {
            Marshal.WriteByte(buffer, 0);
            HipError error = HipNativeMethods.DeviceGetName(buffer, bufferLength, deviceId);
            name = error == HipError.Success ? Marshal.PtrToStringAnsi(buffer) ?? string.Empty : string.Empty;
            return error;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount) => HipNativeMethods.Malloc(out pointer, byteCount);

    public HipError Free(IntPtr pointer) => HipNativeMethods.Free(pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind) =>
        HipNativeMethods.Memcpy(destination, source, byteCount, kind);

    public HipError DeviceSynchronize() => HipNativeMethods.DeviceSynchronize();

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module)
    {
        GCHandle pinned = GCHandle.Alloc(codeObject, GCHandleType.Pinned);
        try
        {
            return HipNativeMethods.ModuleLoadData(out module, pinned.AddrOfPinnedObject());
        }
        finally
        {
            pinned.Free();
        }
    }

    public HipError ModuleUnload(IntPtr module) => HipNativeMethods.ModuleUnload(module);

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function)
    {
        using (var nativeName = new Utf8NativeString(kernelName, nameof(kernelName)))
        {
            return HipNativeMethods.ModuleGetFunction(out function, module, nativeName.Pointer);
        }
    }

    public HipError ModuleLaunchKernel(
        IntPtr function,
        uint gridX,
        uint gridY,
        uint gridZ,
        uint blockX,
        uint blockY,
        uint blockZ,
        uint sharedMemoryBytes,
        IntPtr kernelParameters) =>
        HipNativeMethods.ModuleLaunchKernel(
            function,
            gridX,
            gridY,
            gridZ,
            blockX,
            blockY,
            blockZ,
            sharedMemoryBytes,
            IntPtr.Zero,
            kernelParameters,
            IntPtr.Zero);

    public string GetErrorName(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorName(error));

    public string GetErrorString(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorString(error));

    private static string ReadBorrowedString(IntPtr pointer) =>
        pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
}
