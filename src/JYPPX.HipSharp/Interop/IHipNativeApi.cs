using System;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 定义托管层使用的可替换 HIP Runtime 边界 / Defines the replaceable HIP Runtime boundary used by the managed layer.
/// </summary>
internal interface IHipNativeApi
{
    public HipError Init(uint flags);

    public HipError RuntimeGetVersion(out int runtimeVersion);

    public HipError DriverGetVersion(out int driverVersion);

    public HipError GetDeviceCount(out int count);

    public HipError GetDevice(out int deviceId);

    public HipError SetDevice(int deviceId);

    public HipError DeviceGetName(int deviceId, out string name);

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId);

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes);

    public HipError MallocPitch(out IntPtr pointer, out UIntPtr pitch, UIntPtr widthBytes, UIntPtr height);

    public HipError Malloc3D(out HipPitchedPtr pitchedPointer, HipExtent extent);

    public HipError MallocManaged(out IntPtr pointer, UIntPtr byteCount, uint flags);

    public HipError MemPrefetchAsync(IntPtr pointer, UIntPtr byteCount, int device, IntPtr stream);

    public HipError MemAdvise(IntPtr pointer, UIntPtr byteCount, HipMemoryAdvise advice, int device);

    public HipError MallocAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr stream);

    public HipError FreeAsync(IntPtr pointer, IntPtr stream);

    public HipError DeviceGetDefaultMemPool(out IntPtr memoryPool, int deviceOrdinal);

    public HipError DeviceGetMemPool(out IntPtr memoryPool, int deviceOrdinal);

    public HipError DeviceSetMemPool(int deviceOrdinal, IntPtr memoryPool);

    public HipError MemPoolCreate(out IntPtr memoryPool, ref HipMemoryPoolPropertiesNative properties);

    public HipError MemPoolDestroy(IntPtr memoryPool);

    public HipError MemPoolTrimTo(IntPtr memoryPool, UIntPtr minimumBytesToKeep);

    public HipError MemPoolGetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value);

    public HipError MemPoolSetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value);

    public HipError MemPoolSetAccess(IntPtr memoryPool, HipMemoryPoolAccessDescriptorNative[] descriptors);

    public HipError MemPoolGetAccess(out HipMemoryPoolAccess access, IntPtr memoryPool, ref HipMemLocation location);

    public HipError MallocFromPoolAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr memoryPool, IntPtr stream);

    public HipError DeviceCanAccessPeer(out int canAccessPeer, int deviceId, int peerDeviceId);

    public HipError DeviceEnablePeerAccess(int peerDeviceId, uint flags);

    public HipError DeviceDisablePeerAccess(int peerDeviceId);

    public HipError MemcpyPeerAsync(IntPtr destination, int destinationDevice, IntPtr source, int sourceDevice, UIntPtr byteCount, IntPtr stream);

    public HipError StreamBeginCapture(IntPtr stream, HipStreamCaptureMode mode);

    public HipError StreamEndCapture(IntPtr stream, out IntPtr graph);

    public HipError GraphDestroy(IntPtr graph);

    public HipError GraphInstantiateWithFlags(out IntPtr graphExec, IntPtr graph, ulong flags);

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream);

    public HipError GraphExecDestroy(IntPtr graphExec);

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount);

    public HipError Free(IntPtr pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream);

    public HipError Memset(IntPtr destination, int value, UIntPtr byteCount);

    public HipError MemsetAsync(IntPtr destination, int value, UIntPtr byteCount, IntPtr stream);

    public HipError Memset2D(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height);

    public HipError Memset2DAsync(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height, IntPtr stream);

    public HipError Memset3D(HipPitchedPtr destination, int value, HipExtent extent);

    public HipError Memset3DAsync(HipPitchedPtr destination, int value, HipExtent extent, IntPtr stream);

    public HipError Memcpy2D(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind);

    public HipError Memcpy2DAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind, IntPtr stream);

    public HipError Memcpy3D(ref HipMemcpy3DParameters parameters);

    public HipError Memcpy3DAsync(ref HipMemcpy3DParameters parameters, IntPtr stream);

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags);

    public HipError HostFree(IntPtr pointer);

    public HipError DeviceSynchronize();

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags);

    public HipError StreamDestroy(IntPtr stream);

    public HipError StreamSynchronize(IntPtr stream);

    public HipError StreamQuery(IntPtr stream);

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags);

    public HipError EventDestroy(IntPtr eventHandle);

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream);

    public HipError EventSynchronize(IntPtr eventHandle);

    public HipError EventQuery(IntPtr eventHandle);

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end);

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module);

    public HipError ModuleUnload(IntPtr module);

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function);

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
        IntPtr kernelParameters);

    public string GetErrorName(HipError error);

    public string GetErrorString(HipError error);
}
