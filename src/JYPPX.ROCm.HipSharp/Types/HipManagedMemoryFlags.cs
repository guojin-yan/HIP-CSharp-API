using System;

namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 指定 managed-memory 的初始可见性 / Specifies initial managed-memory visibility.
/// </summary>
[Flags]
public enum HipManagedMemoryFlags : uint
{
    /// <summary>从所有 stream 可见 / Visible from all streams.</summary>
    Global = 0x01,
    /// <summary>只从创建 stream 可见 / Visible only from the creating stream.</summary>
    Host = 0x02,
}
