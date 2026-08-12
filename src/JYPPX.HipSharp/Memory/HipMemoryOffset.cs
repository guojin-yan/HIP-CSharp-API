using System;
using System.Globalization;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 表示以元素为单位的不可变三维偏移 / Represents an immutable three-dimensional offset measured in elements.
/// </summary>
public readonly struct HipMemoryOffset : IEquatable<HipMemoryOffset>
{
    /// <summary>创建元素偏移 / Creates an element offset.</summary>
    /// <param name="x">X 元素偏移 / X offset in elements.</param>
    /// <param name="y">Y 元素偏移 / Y offset in elements.</param>
    /// <param name="z">Z 元素偏移 / Z offset in elements.</param>
    public HipMemoryOffset(ulong x, ulong y = 0, ulong z = 0)
    {
        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>获取 X 元素偏移 / Gets the X offset in elements.</summary>
    public ulong X { get; }

    /// <summary>获取 Y 元素偏移 / Gets the Y offset in elements.</summary>
    public ulong Y { get; }

    /// <summary>获取 Z 元素偏移 / Gets the Z offset in elements.</summary>
    public ulong Z { get; }

    /// <summary>判断两个偏移是否相等 / Determines whether two offsets are equal.</summary>
    public bool Equals(HipMemoryOffset other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>判断对象是否表示相同偏移 / Determines whether an object represents the same offset.</summary>
    public override bool Equals(object? obj) => obj is HipMemoryOffset other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode() => ((X.GetHashCode() * 397) ^ Y.GetHashCode()) * 397 ^ Z.GetHashCode();

    /// <summary>以 X,Y,Z 格式返回偏移 / Returns the offset in X,Y,Z form.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", X, Y, Z);

    /// <summary>判断两个偏移是否相等 / Determines whether two offsets are equal.</summary>
    public static bool operator ==(HipMemoryOffset left, HipMemoryOffset right) => left.Equals(right);

    /// <summary>判断两个偏移是否不相等 / Determines whether two offsets differ.</summary>
    public static bool operator !=(HipMemoryOffset left, HipMemoryOffset right) => !left.Equals(right);
}
