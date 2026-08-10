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

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount);

    public HipError Free(IntPtr pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream);

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
