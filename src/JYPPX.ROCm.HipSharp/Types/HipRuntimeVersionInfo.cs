namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 包含 HIP Runtime 与驱动版本 / Contains HIP Runtime and driver versions.
/// </summary>
public sealed class HipRuntimeVersionInfo
{
    internal HipRuntimeVersionInfo(HipVersion runtimeVersion, HipVersion driverVersion)
    {
        RuntimeVersion = runtimeVersion;
        DriverVersion = driverVersion;
    }

    /// <summary>获取 HIP Runtime 版本 / Gets the HIP Runtime version.</summary>
    public HipVersion RuntimeVersion { get; }

    /// <summary>获取 HIP 驱动版本 / Gets the HIP driver version.</summary>
    public HipVersion DriverVersion { get; }
}
