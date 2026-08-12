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

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId) =>
        HipNativeMethods.DeviceGetAttribute(out value, attribute, deviceId);

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes)
    {
        UIntPtr free = UIntPtr.Zero;
        UIntPtr total = UIntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MemGetInfo(out free, out total));
        freeBytes = free;
        totalBytes = total;
        return error;
    }

    public HipError MallocPitch(out IntPtr pointer, out UIntPtr pitch, UIntPtr widthBytes, UIntPtr height)
    {
        IntPtr value = IntPtr.Zero;
        UIntPtr rowPitch = UIntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocPitch(out value, out rowPitch, widthBytes, height));
        pointer = value;
        pitch = rowPitch;
        return error;
    }

    public HipError Malloc3D(out HipPitchedPtr pitchedPointer, HipExtent extent)
    {
        HipPitchedPtr value = default;
        HipError error = Optional(() => HipNativeMethods.Malloc3D(out value, extent));
        pitchedPointer = value;
        return error;
    }

    public HipError MallocManaged(out IntPtr pointer, UIntPtr byteCount, uint flags)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocManaged(out value, byteCount, flags));
        pointer = value;
        return error;
    }

    public HipError MemPrefetchAsync(IntPtr pointer, UIntPtr byteCount, int device, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemPrefetchAsync(pointer, byteCount, device, stream));

    public HipError MemAdvise(IntPtr pointer, UIntPtr byteCount, HipMemoryAdvise advice, int device) =>
        Optional(() => HipNativeMethods.MemAdvise(pointer, byteCount, advice, device));

    public HipError MallocAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr stream)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocAsync(out value, byteCount, stream));
        pointer = value;
        return error;
    }

    public HipError FreeAsync(IntPtr pointer, IntPtr stream) => Optional(() => HipNativeMethods.FreeAsync(pointer, stream));

    public HipError DeviceCanAccessPeer(out int canAccessPeer, int deviceId, int peerDeviceId)
    {
        int value = 0;
        HipError error = Optional(() => HipNativeMethods.DeviceCanAccessPeer(out value, deviceId, peerDeviceId));
        canAccessPeer = value;
        return error;
    }

    public HipError DeviceEnablePeerAccess(int peerDeviceId, uint flags) => Optional(() => HipNativeMethods.DeviceEnablePeerAccess(peerDeviceId, flags));

    public HipError DeviceDisablePeerAccess(int peerDeviceId) => Optional(() => HipNativeMethods.DeviceDisablePeerAccess(peerDeviceId));

    public HipError MemcpyPeerAsync(IntPtr destination, int destinationDevice, IntPtr source, int sourceDevice, UIntPtr byteCount, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemcpyPeerAsync(destination, destinationDevice, source, sourceDevice, byteCount, stream));

    public HipError StreamBeginCapture(IntPtr stream, HipStreamCaptureMode mode) => Optional(() => HipNativeMethods.StreamBeginCapture(stream, mode));

    public HipError StreamEndCapture(IntPtr stream, out IntPtr graph)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.StreamEndCapture(stream, out value));
        graph = value;
        return error;
    }

    public HipError GraphDestroy(IntPtr graph) => Optional(() => HipNativeMethods.GraphDestroy(graph));

    public HipError GraphInstantiateWithFlags(out IntPtr graphExec, IntPtr graph, ulong flags)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphInstantiateWithFlags(out value, graph, flags));
        graphExec = value;
        return error;
    }

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream) => Optional(() => HipNativeMethods.GraphLaunch(graphExec, stream));

    public HipError GraphExecDestroy(IntPtr graphExec) => Optional(() => HipNativeMethods.GraphExecDestroy(graphExec));

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount) => HipNativeMethods.Malloc(out pointer, byteCount);

    public HipError Free(IntPtr pointer) => HipNativeMethods.Free(pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind) =>
        HipNativeMethods.Memcpy(destination, source, byteCount, kind);

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream) =>
        HipNativeMethods.MemcpyAsync(destination, source, byteCount, kind, stream);

    public HipError Memset(IntPtr destination, int value, UIntPtr byteCount) =>
        Optional(() => HipNativeMethods.Memset(destination, value, byteCount));

    public HipError MemsetAsync(IntPtr destination, int value, UIntPtr byteCount, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemsetAsync(destination, value, byteCount, stream));

    public HipError Memset2D(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height) =>
        Optional(() => HipNativeMethods.Memset2D(destination, pitch, value, widthBytes, height));

    public HipError Memset2DAsync(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memset2DAsync(destination, pitch, value, widthBytes, height, stream));

    public HipError Memset3D(HipPitchedPtr destination, int value, HipExtent extent) =>
        Optional(() => HipNativeMethods.Memset3D(destination, value, extent));

    public HipError Memset3DAsync(HipPitchedPtr destination, int value, HipExtent extent, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memset3DAsync(destination, value, extent, stream));

    public HipError Memcpy2D(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind) =>
        Optional(() => HipNativeMethods.Memcpy2D(destination, destinationPitch, source, sourcePitch, widthBytes, height, kind));

    public HipError Memcpy2DAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memcpy2DAsync(destination, destinationPitch, source, sourcePitch, widthBytes, height, kind, stream));

    public HipError Memcpy3D(ref HipMemcpy3DParameters parameters)
    {
        HipMemcpy3DParameters value = parameters;
        HipError error = Optional(() => HipNativeMethods.Memcpy3D(ref value));
        parameters = value;
        return error;
    }

    public HipError Memcpy3DAsync(ref HipMemcpy3DParameters parameters, IntPtr stream)
    {
        HipMemcpy3DParameters value = parameters;
        HipError error = Optional(() => HipNativeMethods.Memcpy3DAsync(ref value, stream));
        parameters = value;
        return error;
    }

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags) =>
        HipNativeMethods.HostMalloc(out pointer, byteCount, flags);

    public HipError HostFree(IntPtr pointer) => HipNativeMethods.HostFree(pointer);

    public HipError DeviceSynchronize() => HipNativeMethods.DeviceSynchronize();

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags) =>
        HipNativeMethods.StreamCreateWithFlags(out stream, flags);

    public HipError StreamDestroy(IntPtr stream) => HipNativeMethods.StreamDestroy(stream);

    public HipError StreamSynchronize(IntPtr stream) => HipNativeMethods.StreamSynchronize(stream);

    public HipError StreamQuery(IntPtr stream) => HipNativeMethods.StreamQuery(stream);

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags) =>
        HipNativeMethods.EventCreateWithFlags(out eventHandle, flags);

    public HipError EventDestroy(IntPtr eventHandle) => HipNativeMethods.EventDestroy(eventHandle);

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream) => HipNativeMethods.EventRecord(eventHandle, stream);

    public HipError EventSynchronize(IntPtr eventHandle) => HipNativeMethods.EventSynchronize(eventHandle);

    public HipError EventQuery(IntPtr eventHandle) => HipNativeMethods.EventQuery(eventHandle);

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end) =>
        HipNativeMethods.EventElapsedTime(out milliseconds, start, end);

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
        IntPtr stream,
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
            stream,
            kernelParameters,
            IntPtr.Zero);

    public string GetErrorName(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorName(error));

    public string GetErrorString(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorString(error));

    private static string ReadBorrowedString(IntPtr pointer) =>
        pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;

    private static HipError Optional(Func<HipError> call)
    {
        try
        {
            return call();
        }
        catch (EntryPointNotFoundException)
        {
            return HipError.NotSupported;
        }
    }
}
