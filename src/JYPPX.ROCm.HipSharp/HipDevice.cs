using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>
/// 表示可被设为当前设备的 HIP 设备 / Represents a HIP device that can be made current.
/// </summary>
public sealed partial class HipDevice
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
    /// 获取设备是否支持单设备 cooperative kernel launch / Gets whether the device supports single-device cooperative kernel launch.
    /// </summary>
    /// <exception cref="HipException">HIP 无法读取设备属性 / HIP cannot read the device attribute.</exception>
    /// <exception cref="InvalidOperationException">HIP 返回的 capability 值不是 0 或 1 / HIP returns a capability value other than zero or one.</exception>
    public bool SupportsCooperativeLaunch
    {
        get
        {
            int value = GetAttribute(HipDeviceAttribute.CooperativeLaunch);
            if (value != 0 && value != 1) throw new InvalidOperationException("HIP returned an invalid cooperative-launch capability value.");
            return value != 0;
        }
    }

    /// <summary>获取设备的 multiprocessor 数 / Gets the device multiprocessor count.</summary>
    /// <exception cref="HipException">HIP 无法读取设备属性 / HIP cannot read the device attribute.</exception>
    /// <exception cref="InvalidOperationException">HIP 返回非正数 / HIP returns a non-positive value.</exception>
    public int MultiprocessorCount => GetPositiveAttribute(HipDeviceAttribute.MultiprocessorCount);

    /// <summary>获取 warp 宽度（线程数） / Gets the warp width, in threads.</summary>
    /// <exception cref="HipException">HIP 无法读取设备属性 / HIP cannot read the device attribute.</exception>
    /// <exception cref="InvalidOperationException">HIP 返回非正数 / HIP returns a non-positive value.</exception>
    public int WarpSize => GetPositiveAttribute(HipDeviceAttribute.WarpSize);

    internal IHipNativeApi NativeApi => _nativeApi;

    /// <summary>
    /// 将此设备设为当前设备 / Makes this device current.
    /// </summary>
    /// <exception cref="HipException">HIP 拒绝切换设备 / HIP rejects the device switch.</exception>
    public void MakeCurrent() => HipCall.ThrowIfFailed(_nativeApi, _nativeApi.SetDevice(Ordinal), "hipSetDevice");

    /// <summary>获取“序号: 名称”格式的设备说明 / Gets a device description in "ordinal: name" form.</summary>
    /// <returns>设备说明 / Device description.</returns>
    public override string ToString() => Ordinal + ": " + Name;

    private int GetPositiveAttribute(HipDeviceAttribute attribute)
    {
        int value = GetAttribute(attribute);
        if (value <= 0) throw new InvalidOperationException("HIP returned a non-positive device attribute value for " + attribute + ".");
        return value;
    }

    private int GetAttribute(HipDeviceAttribute attribute)
    {
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetAttribute(out int value, attribute, Ordinal), "hipDeviceGetAttribute");
        return value;
    }
}
