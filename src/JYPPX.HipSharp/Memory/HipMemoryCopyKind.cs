namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 指定 HIP 同步内存复制方向 / Specifies the direction of a synchronous HIP memory copy.
/// </summary>
public enum HipMemoryCopyKind
{
    /// <summary>从主机内存复制到主机内存 / Copies from host memory to host memory.</summary>
    HostToHost = 0,

    /// <summary>从主机内存复制到设备内存 / Copies from host memory to device memory.</summary>
    HostToDevice = 1,

    /// <summary>从设备内存复制到主机内存 / Copies from device memory to host memory.</summary>
    DeviceToHost = 2,

    /// <summary>从设备内存复制到设备内存 / Copies from device memory to device memory.</summary>
    DeviceToDevice = 3,

    /// <summary>由 HIP Runtime 推断复制方向 / Lets the HIP Runtime infer the copy direction.</summary>
    Default = 4,
}
