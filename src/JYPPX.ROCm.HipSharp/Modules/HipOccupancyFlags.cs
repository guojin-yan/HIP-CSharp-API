using System;

namespace JYPPX.ROCm.HipSharp.Modules;

/// <summary>
/// 控制 HIP module occupancy 查询 / Controls HIP module occupancy queries.
/// </summary>
[Flags]
public enum HipOccupancyFlags
{
    /// <summary>使用默认 occupancy 行为 / Uses default occupancy behavior.</summary>
    Default = 0,

    /// <summary>禁用缓存不可用时的 occupancy override / Disables the caching override when caching cannot be used.</summary>
    DisableCachingOverride = 1,
}
