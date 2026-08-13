using System;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>指定设备对 memory pool allocation 的访问权限 / Specifies a device's access to memory-pool allocations.</summary>
public enum HipMemoryPoolAccess
{
    /// <summary>禁止访问 / Disallows access.</summary>
    None = 0,

    /// <summary>允许读写访问 / Allows read and write access.</summary>
    ReadWrite = 3,
}

/// <summary>描述一个设备的 memory pool 访问权限 / Describes memory-pool access for one device.</summary>
public readonly struct HipMemoryPoolAccessDescriptor
{
    /// <summary>创建设备访问描述 / Creates a device access descriptor.</summary>
    /// <param name="device">目标设备 / Target device.</param>
    /// <param name="access">访问权限 / Access permission.</param>
    public HipMemoryPoolAccessDescriptor(HipDevice device, HipMemoryPoolAccess access)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
        if (access != HipMemoryPoolAccess.None && access != HipMemoryPoolAccess.ReadWrite)
        {
            throw new ArgumentOutOfRangeException(nameof(access));
        }
        Access = access;
    }

    /// <summary>获取目标设备 / Gets the target device.</summary>
    public HipDevice Device { get; }

    /// <summary>获取访问权限 / Gets the access permission.</summary>
    public HipMemoryPoolAccess Access { get; }
}

/// <summary>表示 memory pool 的只读字节统计快照 / Represents a read-only byte-statistics snapshot for a memory pool.</summary>
public readonly struct HipMemoryPoolStatistics : IEquatable<HipMemoryPoolStatistics>
{
    /// <summary>创建统计快照 / Creates a statistics snapshot.</summary>
    public HipMemoryPoolStatistics(ulong reservedBytes, ulong reservedHighWatermarkBytes, ulong usedBytes, ulong usedHighWatermarkBytes)
    {
        ReservedBytes = reservedBytes;
        ReservedHighWatermarkBytes = reservedHighWatermarkBytes;
        UsedBytes = usedBytes;
        UsedHighWatermarkBytes = usedHighWatermarkBytes;
    }

    /// <summary>获取当前保留的 backing memory 字节数 / Gets currently reserved backing-memory bytes.</summary>
    public ulong ReservedBytes { get; }

    /// <summary>获取保留字节数的高水位 / Gets the reserved-byte high watermark.</summary>
    public ulong ReservedHighWatermarkBytes { get; }

    /// <summary>获取当前使用的字节数 / Gets currently used bytes.</summary>
    public ulong UsedBytes { get; }

    /// <summary>获取使用字节数的高水位 / Gets the used-byte high watermark.</summary>
    public ulong UsedHighWatermarkBytes { get; }

    /// <summary>比较统计快照 / Compares statistics snapshots.</summary>
    public bool Equals(HipMemoryPoolStatistics other) =>
        ReservedBytes == other.ReservedBytes &&
        ReservedHighWatermarkBytes == other.ReservedHighWatermarkBytes &&
        UsedBytes == other.UsedBytes &&
        UsedHighWatermarkBytes == other.UsedHighWatermarkBytes;

    /// <summary>比较统计快照 / Compares statistics snapshots.</summary>
    public override bool Equals(object? obj) => obj is HipMemoryPoolStatistics other && Equals(other);

    /// <summary>获取哈希码 / Gets a hash code.</summary>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = ReservedBytes.GetHashCode();
            hash = (hash * 397) ^ ReservedHighWatermarkBytes.GetHashCode();
            hash = (hash * 397) ^ UsedBytes.GetHashCode();
            return (hash * 397) ^ UsedHighWatermarkBytes.GetHashCode();
        }
    }

    /// <summary>比较统计快照 / Compares statistics snapshots.</summary>
    public static bool operator ==(HipMemoryPoolStatistics left, HipMemoryPoolStatistics right) => left.Equals(right);

    /// <summary>比较统计快照 / Compares statistics snapshots.</summary>
    public static bool operator !=(HipMemoryPoolStatistics left, HipMemoryPoolStatistics right) => !left.Equals(right);
}

internal enum HipMemoryPoolAttributeNative
{
    ReuseFollowEventDependencies = 1,
    ReuseAllowOpportunistic = 2,
    ReuseAllowInternalDependencies = 3,
    ReleaseThreshold = 4,
    ReservedMemCurrent = 5,
    ReservedMemHigh = 6,
    UsedMemCurrent = 7,
    UsedMemHigh = 8,
}
