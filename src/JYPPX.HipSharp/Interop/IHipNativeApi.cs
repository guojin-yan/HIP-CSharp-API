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

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount);

    public HipError Free(IntPtr pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError DeviceSynchronize();

    public string GetErrorName(HipError error);

    public string GetErrorString(HipError error);
}
