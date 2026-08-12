using System;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 表示以元素为单位的不可变 pitched memory 子区域 / Represents an immutable pitched-memory subregion measured in elements.
/// </summary>
public readonly struct HipMemoryRegion : IEquatable<HipMemoryRegion>
{
    /// <summary>创建子区域 / Creates a subregion.</summary>
    /// <param name="offset">区域起点，单位为元素 / Region origin in elements.</param>
    /// <param name="extent">区域大小，单位为元素 / Region extent in elements.</param>
    public HipMemoryRegion(HipMemoryOffset offset, HipMemoryExtent extent)
    {
        Offset = offset;
        Extent = extent;
    }

    /// <summary>获取区域起点，单位为元素 / Gets the region origin in elements.</summary>
    public HipMemoryOffset Offset { get; }

    /// <summary>获取区域大小，单位为元素 / Gets the region extent in elements.</summary>
    public HipMemoryExtent Extent { get; }

    /// <summary>判断两个区域是否相等 / Determines whether two regions are equal.</summary>
    public bool Equals(HipMemoryRegion other) => Offset == other.Offset && Extent == other.Extent;

    /// <summary>判断对象是否表示相同区域 / Determines whether an object represents the same region.</summary>
    public override bool Equals(object? obj) => obj is HipMemoryRegion other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() => (Offset.GetHashCode() * 397) ^ Extent.GetHashCode();

    /// <summary>判断两个区域是否相等 / Determines whether two regions are equal.</summary>
    public static bool operator ==(HipMemoryRegion left, HipMemoryRegion right) => left.Equals(right);

    /// <summary>判断两个区域是否不相等 / Determines whether two regions differ.</summary>
    public static bool operator !=(HipMemoryRegion left, HipMemoryRegion right) => !left.Equals(right);
}
