namespace JYPPX.HipSharp.Types;

/// <summary>
/// HIP stream 创建标志 / HIP stream creation flags.
/// </summary>
public enum HipStreamFlags : uint
{
    /// <summary>默认 stream 行为 / Default stream behavior.</summary>
    Default = 0,

    /// <summary>非阻塞 stream / Non-blocking stream.</summary>
    NonBlocking = 1,
}
