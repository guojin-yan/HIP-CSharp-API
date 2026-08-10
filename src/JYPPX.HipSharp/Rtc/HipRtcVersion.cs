using System;
using System.Globalization;

namespace JYPPX.HipSharp.Rtc;

/// <summary>
/// 表示 HIPRTC 主版本号和次版本号 / Represents HIPRTC major and minor version numbers.
/// </summary>
public readonly struct HipRtcVersion : IEquatable<HipRtcVersion>
{
    /// <summary>
    /// 创建 HIPRTC 版本 / Creates a HIPRTC version.
    /// </summary>
    /// <param name="major">主版本号 / Major version number.</param>
    /// <param name="minor">次版本号 / Minor version number.</param>
    public HipRtcVersion(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    /// <summary>获取主版本号 / Gets the major version number.</summary>
    public int Major { get; }

    /// <summary>获取次版本号 / Gets the minor version number.</summary>
    public int Minor { get; }

    /// <summary>判断两个版本是否相等 / Determines whether two versions are equal.</summary>
    /// <param name="other">另一个版本 / The other version.</param>
    /// <returns>相等时为 true / <see langword="true"/> when equal.</returns>
    public bool Equals(HipRtcVersion other) => Major == other.Major && Minor == other.Minor;

    /// <summary>判断对象是否表示相同版本 / Determines whether an object represents the same version.</summary>
    /// <param name="obj">待比较对象 / Object to compare.</param>
    /// <returns>相等时为 true / <see langword="true"/> when equal.</returns>
    public override bool Equals(object? obj) => obj is HipRtcVersion other && Equals(other);

    /// <summary>获取版本哈希码 / Gets the version hash code.</summary>
    /// <returns>哈希码 / Hash code.</returns>
    public override int GetHashCode() => (Major * 397) ^ Minor;

    /// <summary>以 major.minor 格式返回版本 / Returns the version in major.minor form.</summary>
    /// <returns>版本字符串 / Version string.</returns>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}.{1}", Major, Minor);

    /// <summary>判断两个版本是否相等 / Determines whether two versions are equal.</summary>
    public static bool operator ==(HipRtcVersion left, HipRtcVersion right) => left.Equals(right);

    /// <summary>判断两个版本是否不相等 / Determines whether two versions differ.</summary>
    public static bool operator !=(HipRtcVersion left, HipRtcVersion right) => !left.Equals(right);
}
