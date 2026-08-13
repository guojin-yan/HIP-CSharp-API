using System;
using System.Globalization;

namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 表示 HIP 使用整数编码的版本号 / Represents a HIP version encoded as an integer.
/// </summary>
public sealed class HipVersion : IEquatable<HipVersion>
{
    /// <summary>
    /// 使用 HIP 原始整数版本创建实例 / Creates an instance from a raw HIP integer version.
    /// </summary>
    /// <param name="rawValue">HIP 返回的整数值 / Integer value returned by HIP.</param>
    /// <exception cref="ArgumentOutOfRangeException">版本值为负数 / The version value is negative.</exception>
    public HipVersion(int rawValue)
    {
        if (rawValue < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(rawValue));
        }

        RawValue = rawValue;
    }

    /// <summary>获取 HIP 原始整数版本 / Gets the raw HIP integer version.</summary>
    public int RawValue { get; }

    /// <summary>获取主版本号 / Gets the major version.</summary>
    public int Major => RawValue / 10000000;

    /// <summary>获取次版本号 / Gets the minor version.</summary>
    public int Minor => RawValue / 100000 % 100;

    /// <summary>获取补丁版本号 / Gets the patch version.</summary>
    public int Patch => RawValue % 100000;

    /// <summary>比较两个 HIP 版本值 / Compares two HIP version values.</summary>
    /// <param name="other">要比较的版本 / Version to compare.</param>
    /// <returns>原始版本值相同时为 true / <see langword="true"/> when the raw version values match.</returns>
    public bool Equals(HipVersion? other) => other is not null && RawValue == other.RawValue;

    /// <summary>比较对象与当前 HIP 版本 / Compares an object with the current HIP version.</summary>
    /// <param name="obj">要比较的对象 / Object to compare.</param>
    /// <returns>对象表示相同版本时为 true / <see langword="true"/> when the object represents the same version.</returns>
    public override bool Equals(object? obj) => Equals(obj as HipVersion);

    /// <summary>获取原始版本值的哈希码 / Gets a hash code for the raw version value.</summary>
    /// <returns>哈希码 / Hash code.</returns>
    public override int GetHashCode() => RawValue;

    /// <summary>获取 major.minor.patch 格式的版本 / Gets the version in major.minor.patch form.</summary>
    /// <returns>格式化版本 / Formatted version.</returns>
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", Major, Minor, Patch);
}
