using System;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示 HIP 建议的 occupancy launch plan；它不是最快配置的承诺 / Represents a HIP-suggested occupancy launch plan; it does not promise the fastest configuration.
/// </summary>
public readonly struct HipOccupancyPlan : IEquatable<HipOccupancyPlan>
{
    /// <summary>创建 occupancy launch plan / Creates an occupancy launch plan.</summary>
    /// <param name="minimumGridSize">建议的最小 grid block 数 / Suggested minimum grid size, in blocks.</param>
    /// <param name="occupancy">建议 block 的 occupancy 信息 / Occupancy information for the suggested block.</param>
    /// <exception cref="ArgumentOutOfRangeException">最小 grid size 不是正数 / The minimum grid size is not positive.</exception>
    public HipOccupancyPlan(int minimumGridSize, HipOccupancyInfo occupancy)
    {
        if (minimumGridSize <= 0) throw new ArgumentOutOfRangeException(nameof(minimumGridSize));
        if (occupancy.BlockSize <= 0) throw new ArgumentOutOfRangeException(nameof(occupancy));
        MinimumGridSize = minimumGridSize;
        Occupancy = occupancy;
    }

    /// <summary>获取建议的最小 grid block 数 / Gets the suggested minimum grid size, in blocks.</summary>
    public int MinimumGridSize { get; }

    /// <summary>获取建议的每个 block 线程数 / Gets suggested threads per block.</summary>
    public int BlockSize => Occupancy.BlockSize;

    /// <summary>获取建议 block 的 occupancy 信息 / Gets occupancy information for the suggested block.</summary>
    public HipOccupancyInfo Occupancy { get; }

    /// <summary>判断两个 plan 是否相等 / Determines whether two plans are equal.</summary>
    public bool Equals(HipOccupancyPlan other) => MinimumGridSize == other.MinimumGridSize && Occupancy == other.Occupancy;

    /// <summary>判断对象是否表示相同 plan / Determines whether an object represents the same plan.</summary>
    public override bool Equals(object? obj) => obj is HipOccupancyPlan other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() => (MinimumGridSize * 397) ^ Occupancy.GetHashCode();

    /// <summary>判断两个 plan 是否相等 / Determines whether two plans are equal.</summary>
    public static bool operator ==(HipOccupancyPlan left, HipOccupancyPlan right) => left.Equals(right);

    /// <summary>判断两个 plan 是否不相等 / Determines whether two plans differ.</summary>
    public static bool operator !=(HipOccupancyPlan left, HipOccupancyPlan right) => !left.Equals(right);
}
