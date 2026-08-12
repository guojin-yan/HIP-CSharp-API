using System;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 表示当前设备的不可变显存统计，单位为字节 / Represents immutable memory statistics for the current device, in bytes.
/// </summary>
public readonly struct HipMemoryInfo : IEquatable<HipMemoryInfo>
{
    /// <summary>
    /// 创建显存统计 / Creates memory statistics.
    /// </summary>
    /// <param name="freeBytes">可用字节数 / Number of free bytes.</param>
    /// <param name="totalBytes">总字节数 / Total number of bytes.</param>
    /// <exception cref="ArgumentOutOfRangeException">可用字节数大于总字节数 / Free bytes exceed total bytes.</exception>
    public HipMemoryInfo(ulong freeBytes, ulong totalBytes)
    {
        if (freeBytes > totalBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(freeBytes), "Free memory cannot exceed total memory.");
        }

        FreeBytes = freeBytes;
        TotalBytes = totalBytes;
    }

    /// <summary>获取可用字节数 / Gets the number of free bytes.</summary>
    public ulong FreeBytes { get; }

    /// <summary>获取总字节数 / Gets the total number of bytes.</summary>
    public ulong TotalBytes { get; }

    /// <summary>获取已用字节数 / Gets the number of used bytes.</summary>
    public ulong UsedBytes => TotalBytes - FreeBytes;

    /// <summary>判断两个显存统计是否相等 / Determines whether two memory statistics are equal.</summary>
    public bool Equals(HipMemoryInfo other) => FreeBytes == other.FreeBytes && TotalBytes == other.TotalBytes;

    /// <summary>判断对象是否表示相同显存统计 / Determines whether an object represents the same memory statistics.</summary>
    public override bool Equals(object? obj) => obj is HipMemoryInfo other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() => (FreeBytes.GetHashCode() * 397) ^ TotalBytes.GetHashCode();

    /// <summary>判断两个显存统计是否相等 / Determines whether two memory statistics are equal.</summary>
    public static bool operator ==(HipMemoryInfo left, HipMemoryInfo right) => left.Equals(right);

    /// <summary>判断两个显存统计是否不相等 / Determines whether two memory statistics differ.</summary>
    public static bool operator !=(HipMemoryInfo left, HipMemoryInfo right) => !left.Equals(right);
}
