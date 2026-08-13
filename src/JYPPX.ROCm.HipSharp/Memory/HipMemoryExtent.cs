using System;
using System.Globalization;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>
/// 表示以元素为单位的不可变 pitched memory 范围 / Represents an immutable pitched-memory extent measured in elements.
/// </summary>
public readonly struct HipMemoryExtent : IEquatable<HipMemoryExtent>
{
    /// <summary>
    /// 创建元素范围 / Creates an element extent.
    /// </summary>
    /// <param name="width">元素宽度，必须大于零 / Width in elements; must be positive.</param>
    /// <param name="height">元素高度，必须大于零 / Height in elements; must be positive.</param>
    /// <param name="depth">元素深度，必须大于零 / Depth in elements; must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">任一维度为零 / Any dimension is zero.</exception>
    public HipMemoryExtent(ulong width, ulong height, ulong depth = 1)
    {
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (height == 0) throw new ArgumentOutOfRangeException(nameof(height));
        if (depth == 0) throw new ArgumentOutOfRangeException(nameof(depth));
        Width = width;
        Height = height;
        Depth = depth;
    }

    /// <summary>获取元素宽度 / Gets the width in elements.</summary>
    public ulong Width { get; }

    /// <summary>获取元素高度 / Gets the height in elements.</summary>
    public ulong Height { get; }

    /// <summary>获取元素深度 / Gets the depth in elements.</summary>
    public ulong Depth { get; }

    /// <summary>获取元素总数；溢出时抛出异常 / Gets the element count and throws if it overflows.</summary>
    /// <exception cref="OverflowException">元素总数超过 <see cref="ulong"/> / The element count exceeds <see cref="ulong"/>.</exception>
    public ulong ElementCount => checked(checked(Width * Height) * Depth);

    /// <summary>判断两个范围是否相等 / Determines whether two extents are equal.</summary>
    public bool Equals(HipMemoryExtent other) => Width == other.Width && Height == other.Height && Depth == other.Depth;

    /// <summary>判断对象是否表示相同范围 / Determines whether an object represents the same extent.</summary>
    public override bool Equals(object? obj) => obj is HipMemoryExtent other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() => ((Width.GetHashCode() * 397) ^ Height.GetHashCode()) * 397 ^ Depth.GetHashCode();

    /// <summary>以 WidthxHeightxDepth 格式返回范围 / Returns the extent in WidthxHeightxDepth form.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}x{1}x{2}", Width, Height, Depth);

    /// <summary>判断两个范围是否相等 / Determines whether two extents are equal.</summary>
    public static bool operator ==(HipMemoryExtent left, HipMemoryExtent right) => left.Equals(right);

    /// <summary>判断两个范围是否不相等 / Determines whether two extents differ.</summary>
    public static bool operator !=(HipMemoryExtent left, HipMemoryExtent right) => !left.Equals(right);
}
