namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 表示 HIP managed-memory 使用提示；提示不保证迁移或性能结果 / Represents HIP managed-memory usage hints; hints do not guarantee migration or performance.
/// </summary>
public enum HipMemoryAdvise
{
    /// <summary>设置主要只读提示 / Sets the read-mostly hint.</summary>
    SetReadMostly = 1,
    /// <summary>撤销主要只读提示 / Unsets the read-mostly hint.</summary>
    UnsetReadMostly = 2,
    /// <summary>设置首选位置 / Sets the preferred location.</summary>
    SetPreferredLocation = 3,
    /// <summary>撤销首选位置 / Unsets the preferred location.</summary>
    UnsetPreferredLocation = 4,
    /// <summary>设置将由指定设备访问 / Sets the accessed-by hint for a device.</summary>
    SetAccessedBy = 5,
    /// <summary>撤销指定设备访问提示 / Unsets the accessed-by hint for a device.</summary>
    UnsetAccessedBy = 6,
    /// <summary>设置 coarse-grain 一致性 / Sets coarse-grain coherency.</summary>
    SetCoarseGrain = 100,
    /// <summary>恢复 fine-grain 一致性 / Restores fine-grain coherency.</summary>
    UnsetCoarseGrain = 101,
}
