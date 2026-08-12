using System;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示给定 block 配置的资源常驻估算；它不是性能承诺 / Represents a residency estimate for a block configuration; it is not a performance promise.
/// </summary>
public readonly struct HipOccupancyInfo : IEquatable<HipOccupancyInfo>
{
    /// <summary>创建 occupancy 信息 / Creates occupancy information.</summary>
    /// <param name="blockSize">每个 block 的线程数 / Threads per block.</param>
    /// <param name="dynamicSharedMemoryBytes">每个 block 的动态共享内存字节数 / Dynamic shared-memory bytes per block.</param>
    /// <param name="activeBlocksPerMultiprocessor">每个 multiprocessor 的最大 active block 数 / Maximum active blocks per multiprocessor.</param>
    /// <param name="multiprocessorCount">设备的 multiprocessor 数 / Device multiprocessor count.</param>
    /// <exception cref="ArgumentOutOfRangeException">任一计数不是正数 / Any count is not positive.</exception>
    public HipOccupancyInfo(
        int blockSize,
        ulong dynamicSharedMemoryBytes,
        int activeBlocksPerMultiprocessor,
        int multiprocessorCount)
    {
        if (blockSize <= 0) throw new ArgumentOutOfRangeException(nameof(blockSize));
        if (activeBlocksPerMultiprocessor <= 0) throw new ArgumentOutOfRangeException(nameof(activeBlocksPerMultiprocessor));
        if (multiprocessorCount <= 0) throw new ArgumentOutOfRangeException(nameof(multiprocessorCount));
        BlockSize = blockSize;
        DynamicSharedMemoryBytes = dynamicSharedMemoryBytes;
        ActiveBlocksPerMultiprocessor = activeBlocksPerMultiprocessor;
        MultiprocessorCount = multiprocessorCount;
        MaximumResidentBlocks = checked((long)activeBlocksPerMultiprocessor * multiprocessorCount);
    }

    /// <summary>获取每个 block 的线程数 / Gets threads per block.</summary>
    public int BlockSize { get; }

    /// <summary>获取每个 block 的动态共享内存字节数 / Gets dynamic shared-memory bytes per block.</summary>
    public ulong DynamicSharedMemoryBytes { get; }

    /// <summary>获取每个 multiprocessor 的最大 active block 数 / Gets maximum active blocks per multiprocessor.</summary>
    public int ActiveBlocksPerMultiprocessor { get; }

    /// <summary>获取设备的 multiprocessor 数 / Gets the device multiprocessor count.</summary>
    public int MultiprocessorCount { get; }

    /// <summary>获取全设备最大常驻 block 数 / Gets maximum resident blocks across the device.</summary>
    public long MaximumResidentBlocks { get; }

    /// <summary>判断两个 occupancy 结果是否相等 / Determines whether two occupancy results are equal.</summary>
    public bool Equals(HipOccupancyInfo other) =>
        BlockSize == other.BlockSize &&
        DynamicSharedMemoryBytes == other.DynamicSharedMemoryBytes &&
        ActiveBlocksPerMultiprocessor == other.ActiveBlocksPerMultiprocessor &&
        MultiprocessorCount == other.MultiprocessorCount;

    /// <summary>判断对象是否表示相同 occupancy 结果 / Determines whether an object represents the same occupancy result.</summary>
    public override bool Equals(object? obj) => obj is HipOccupancyInfo other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() =>
        (((BlockSize * 397) ^ DynamicSharedMemoryBytes.GetHashCode()) * 397 ^ ActiveBlocksPerMultiprocessor) * 397 ^ MultiprocessorCount;

    /// <summary>判断两个 occupancy 结果是否相等 / Determines whether two occupancy results are equal.</summary>
    public static bool operator ==(HipOccupancyInfo left, HipOccupancyInfo right) => left.Equals(right);

    /// <summary>判断两个 occupancy 结果是否不相等 / Determines whether two occupancy results differ.</summary>
    public static bool operator !=(HipOccupancyInfo left, HipOccupancyInfo right) => !left.Equals(right);
}
