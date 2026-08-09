using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp;

/// <summary>
/// 表示可被设为当前设备的 HIP 设备 / Represents a HIP device that can be made current.
/// </summary>
public sealed class HipDevice
{
    private readonly IHipNativeApi _nativeApi;

    internal HipDevice(IHipNativeApi nativeApi, HipDeviceInfo info)
    {
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        Info = info ?? throw new ArgumentNullException(nameof(info));
    }

    /// <summary>获取基础设备信息 / Gets basic device information.</summary>
    public HipDeviceInfo Info { get; }

    /// <summary>获取进程内设备序号 / Gets the process-local device ordinal.</summary>
    public int Ordinal => Info.Ordinal;

    /// <summary>获取设备名称 / Gets the device name.</summary>
    public string Name => Info.Name;

    /// <summary>
    /// 将此设备设为当前设备 / Makes this device current.
    /// </summary>
    /// <exception cref="HipException">HIP 拒绝切换设备 / HIP rejects the device switch.</exception>
    public void MakeCurrent() => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.SetDevice(Ordinal), "hipSetDevice");

    /// <summary>获取“序号: 名称”格式的设备说明 / Gets a device description in "ordinal: name" form.</summary>
    /// <returns>设备说明 / Device description.</returns>
    public override string ToString() => Ordinal + ": " + Name;
}
