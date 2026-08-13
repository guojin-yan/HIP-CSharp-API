using System;
using System.Globalization;

namespace JYPPX.ROCm.HipSharp.Modules;

/// <summary>
/// 表示 kernel 启动的三维网格或线程块尺寸 / Represents three-dimensional grid or block dimensions for a kernel launch.
/// </summary>
public readonly struct HipLaunchDimensions : IEquatable<HipLaunchDimensions>
{
    /// <summary>
    /// 创建启动尺寸 / Creates launch dimensions.
    /// </summary>
    /// <param name="x">X 维度，必须大于零 / X dimension, which must be positive.</param>
    /// <param name="y">Y 维度，必须大于零 / Y dimension, which must be positive.</param>
    /// <param name="z">Z 维度，必须大于零 / Z dimension, which must be positive.</param>
    /// <exception cref="ArgumentOutOfRangeException">任一维度为零 / Any dimension is zero.</exception>
    public HipLaunchDimensions(uint x, uint y = 1, uint z = 1)
    {
        if (x == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(x));
        }

        if (y == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(y));
        }

        if (z == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(z));
        }

        X = x;
        Y = y;
        Z = z;
    }

    /// <summary>获取 X 维度 / Gets the X dimension.</summary>
    public uint X { get; }

    /// <summary>获取 Y 维度 / Gets the Y dimension.</summary>
    public uint Y { get; }

    /// <summary>获取 Z 维度 / Gets the Z dimension.</summary>
    public uint Z { get; }

    /// <summary>判断两个尺寸是否相等 / Determines whether two dimensions are equal.</summary>
    public bool Equals(HipLaunchDimensions other) => X == other.X && Y == other.Y && Z == other.Z;

    /// <summary>判断对象是否表示相同尺寸 / Determines whether an object represents the same dimensions.</summary>
    public override bool Equals(object? obj) => obj is HipLaunchDimensions other && Equals(other);

    /// <summary>获取尺寸哈希码 / Gets the dimensions hash code.</summary>
    public override int GetHashCode() => ((int)X * 397) ^ ((int)Y * 31) ^ (int)Z;

    /// <summary>以 XxYxZ 格式返回尺寸 / Returns dimensions in XxYxZ form.</summary>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}x{1}x{2}", X, Y, Z);

    /// <summary>判断两个尺寸是否相等 / Determines whether two dimensions are equal.</summary>
    public static bool operator ==(HipLaunchDimensions left, HipLaunchDimensions right) => left.Equals(right);

    /// <summary>判断两个尺寸是否不相等 / Determines whether two dimensions differ.</summary>
    public static bool operator !=(HipLaunchDimensions left, HipLaunchDimensions right) => !left.Equals(right);
}
