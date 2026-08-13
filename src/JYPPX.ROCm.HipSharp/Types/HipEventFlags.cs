namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// HIP event 创建标志 / HIP event creation flags.
/// </summary>
public enum HipEventFlags : uint
{
    /// <summary>默认 event / Default event.</summary>
    Default = 0,

    /// <summary>禁用计时 / Disable timing.</summary>
    DisableTiming = 2,

    /// <summary>跨进程 event / Interprocess event.</summary>
    Interprocess = 4,
}
