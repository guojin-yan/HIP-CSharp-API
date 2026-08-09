namespace JYPPX.HipSharp.Types;

/// <summary>
/// 包含基础 HIP 设备信息 / Contains basic HIP device information.
/// </summary>
public sealed class HipDeviceInfo
{
    internal HipDeviceInfo(int ordinal, string name)
    {
        Ordinal = ordinal;
        Name = name;
    }

    /// <summary>获取进程内设备序号 / Gets the process-local device ordinal.</summary>
    public int Ordinal { get; }

    /// <summary>获取设备名称 / Gets the device name.</summary>
    public string Name { get; }
}
