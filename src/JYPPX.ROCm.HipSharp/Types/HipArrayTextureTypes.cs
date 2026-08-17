using System;
using System.Runtime.InteropServices;

namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>描述 HIP array channel 的标量解释 / Describes the scalar interpretation of a HIP array channel.</summary>
public enum HipChannelFormatKind
{
    /// <summary>说明该托管接口 / Signed integer channels.</summary>
    SignedInteger = 0,
    /// <summary>说明该托管接口 / Unsigned integer channels.</summary>
    UnsignedInteger = 1,
    /// <summary>说明该托管接口 / Floating-point channels.</summary>
    FloatingPoint = 2,
    /// <summary>说明该托管接口 / No channel format.</summary>
    None = 3,
}

/// <summary>描述该资源 / Describes one to four channels in a HIP array element.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipChannelFormatDescriptor
{
    private readonly int _xBits;
    private readonly int _yBits;
    private readonly int _zBits;
    private readonly int _wBits;
    private readonly HipChannelFormatKind _kind;

    /// <summary>创建该对象 / Creates a channel descriptor.</summary>
    public HipChannelFormatDescriptor(int xBits, int yBits, int zBits, int wBits, HipChannelFormatKind kind)
    {
        ValidateChannel(xBits, nameof(xBits));
        ValidateChannel(yBits, nameof(yBits));
        ValidateChannel(zBits, nameof(zBits));
        ValidateChannel(wBits, nameof(wBits));
        if (kind < HipChannelFormatKind.SignedInteger || kind > HipChannelFormatKind.None) throw new ArgumentOutOfRangeException(nameof(kind));
        int bitCount = checked(xBits + yBits + zBits + wBits);
        if (kind == HipChannelFormatKind.None ? bitCount != 0 : bitCount == 0)
        {
            throw new ArgumentException("None requires zero channel bits; every other format requires at least one bit.", nameof(kind));
        }

        _xBits = xBits;
        _yBits = yBits;
        _zBits = zBits;
        _wBits = wBits;
        _kind = kind;
    }

    /// <summary>获取该值 / Gets the X-channel bit width.</summary>
    public int XBits => _xBits;
    /// <summary>获取该值 / Gets the Y-channel bit width.</summary>
    public int YBits => _yBits;
    /// <summary>获取该值 / Gets the Z-channel bit width.</summary>
    public int ZBits => _zBits;
    /// <summary>获取该值 / Gets the W-channel bit width.</summary>
    public int WBits => _wBits;
    /// <summary>获取该值 / Gets the scalar channel kind.</summary>
    public HipChannelFormatKind Kind => _kind;
    /// <summary>获取该值 / Gets the total bits in one array element.</summary>
    public int BitsPerElement => checked(_xBits + _yBits + _zBits + _wBits);

    internal ulong GetBytesPerElement()
    {
        int bits = BitsPerElement;
        if (bits == 0 || bits % 8 != 0) throw new InvalidOperationException("Managed byte-copy operations require a byte-aligned channel descriptor.");
        return checked((ulong)(bits / 8));
    }

    private static void ValidateChannel(int value, string parameterName)
    {
        if (value < 0 || value > 32) throw new ArgumentOutOfRangeException(parameterName);
    }
}

/// <summary>说明该托管接口 / Native driver-style HIP array element formats.</summary>
public enum HipArrayFormat
{
    /// <summary>说明该托管接口 / Unsigned 8-bit channels.</summary>
    UnsignedInt8 = 0x01,
    /// <summary>说明该托管接口 / Unsigned 16-bit channels.</summary>
    UnsignedInt16 = 0x02,
    /// <summary>说明该托管接口 / Unsigned 32-bit channels.</summary>
    UnsignedInt32 = 0x03,
    /// <summary>说明该托管接口 / Signed 8-bit channels.</summary>
    SignedInt8 = 0x08,
    /// <summary>说明该托管接口 / Signed 16-bit channels.</summary>
    SignedInt16 = 0x09,
    /// <summary>说明该托管接口 / Signed 32-bit channels.</summary>
    SignedInt32 = 0x0a,
    /// <summary>说明该托管接口 / 16-bit floating-point channels.</summary>
    Half = 0x10,
    /// <summary>说明该托管接口 / 32-bit floating-point channels.</summary>
    FloatingPoint32 = 0x20,
}

/// <summary>说明该托管接口 / Flags controlling HIP array allocation and use.</summary>
[Flags]
public enum HipArrayFlags : uint
{
    /// <summary>说明该托管接口 / Default array behavior.</summary>
    Default = 0,
    /// <summary>说明该托管接口 / Layered array.</summary>
    Layered = 0x01,
    /// <summary>说明该托管接口 / Array can back a surface object.</summary>
    SurfaceLoadStore = 0x02,
    /// <summary>说明该托管接口 / Cubemap array.</summary>
    Cubemap = 0x04,
    /// <summary>说明该托管接口 / Array supports texture gather.</summary>
    TextureGather = 0x08,
}

/// <summary>描述该资源 / Describes a one- or two-dimensional driver-style HIP array.</summary>
public readonly struct HipArrayDescriptor
{
    /// <summary>创建该对象 / Creates an array descriptor.</summary>
    public HipArrayDescriptor(ulong width, ulong height, HipArrayFormat format, uint channelCount)
    {
        ValidateDriverDescriptor(width, height, format, channelCount);
        Width = width;
        Height = height;
        Format = format;
        ChannelCount = channelCount;
    }

    /// <summary>获取该值 / Gets the width in elements.</summary>
    public ulong Width { get; }
    /// <summary>获取该值 / Gets the height in elements; zero denotes a one-dimensional array.</summary>
    public ulong Height { get; }
    /// <summary>获取该值 / Gets the channel scalar format.</summary>
    public HipArrayFormat Format { get; }
    /// <summary>获取该值 / Gets the number of channels per element.</summary>
    public uint ChannelCount { get; }

    internal HipArrayDescriptorNative ToNative() => new(
        ToUIntPtr(Width, nameof(Width)), ToUIntPtr(Height, nameof(Height)), Format, ChannelCount);

    internal static HipArrayDescriptor FromNative(HipArrayDescriptorNative value) => new(
        value.Width.ToUInt64(), value.Height.ToUInt64(), value.Format, value.ChannelCount);

    internal static void ValidateDriverDescriptor(ulong width, ulong height, HipArrayFormat format, uint channelCount)
    {
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (!IsSupportedFormat(format)) throw new ArgumentOutOfRangeException(nameof(format));
        if (channelCount != 1 && channelCount != 2 && channelCount != 4) throw new ArgumentOutOfRangeException(nameof(channelCount));
        _ = ToUIntPtr(height, nameof(height));
        _ = ToUIntPtr(width, nameof(width));
    }

    private static bool IsSupportedFormat(HipArrayFormat format) => format == HipArrayFormat.UnsignedInt8
        || format == HipArrayFormat.UnsignedInt16
        || format == HipArrayFormat.UnsignedInt32
        || format == HipArrayFormat.SignedInt8
        || format == HipArrayFormat.SignedInt16
        || format == HipArrayFormat.SignedInt32
        || format == HipArrayFormat.Half
        || format == HipArrayFormat.FloatingPoint32;

    internal static UIntPtr ToUIntPtr(ulong value, string parameterName)
    {
        if (UIntPtr.Size == 4 && value > uint.MaxValue) throw new ArgumentOutOfRangeException(parameterName);
        return UIntPtr.Size == 4 ? new UIntPtr((uint)value) : new UIntPtr(value);
    }
}

/// <summary>描述该资源 / Describes a driver-style HIP array with up to three dimensions.</summary>
public readonly struct HipArray3DDescriptor
{
    /// <summary>创建该对象 / Creates a three-dimensional array descriptor.</summary>
    public HipArray3DDescriptor(ulong width, ulong height, ulong depth, HipArrayFormat format, uint channelCount, HipArrayFlags flags = HipArrayFlags.Default)
    {
        HipArrayDescriptor.ValidateDriverDescriptor(width, height, format, channelCount);
        _ = HipArrayDescriptor.ToUIntPtr(depth, nameof(depth));
        ValidateFlags(flags);
        Width = width;
        Height = height;
        Depth = depth;
        Format = format;
        ChannelCount = channelCount;
        Flags = flags;
    }

    /// <summary>获取该值 / Gets the width in elements.</summary>
    public ulong Width { get; }
    /// <summary>获取该值 / Gets the height in elements.</summary>
    public ulong Height { get; }
    /// <summary>获取该值 / Gets the depth in elements.</summary>
    public ulong Depth { get; }
    /// <summary>获取该值 / Gets the channel scalar format.</summary>
    public HipArrayFormat Format { get; }
    /// <summary>获取该值 / Gets the number of channels per element.</summary>
    public uint ChannelCount { get; }
    /// <summary>获取该值 / Gets the allocation flags.</summary>
    public HipArrayFlags Flags { get; }

    internal HipArray3DDescriptorNative ToNative() => new(
        HipArrayDescriptor.ToUIntPtr(Width, nameof(Width)),
        HipArrayDescriptor.ToUIntPtr(Height, nameof(Height)),
        HipArrayDescriptor.ToUIntPtr(Depth, nameof(Depth)),
        Format,
        ChannelCount,
        Flags);

    internal static HipArray3DDescriptor FromNative(HipArray3DDescriptorNative value) => new(
        value.Width.ToUInt64(), value.Height.ToUInt64(), value.Depth.ToUInt64(), value.Format, value.ChannelCount, value.Flags);

    internal static void ValidateFlags(HipArrayFlags flags)
    {
        const HipArrayFlags valid = HipArrayFlags.Layered | HipArrayFlags.SurfaceLoadStore | HipArrayFlags.Cubemap | HipArrayFlags.TextureGather;
        if ((flags & ~valid) != 0) throw new ArgumentOutOfRangeException(nameof(flags));
    }
}

/// <summary>描述该资源 / Describes the shape and channel layout returned for a HIP array.</summary>
public readonly struct HipArrayInfo
{
    /// <summary>创建该对象 / Creates array information.</summary>
    public HipArrayInfo(HipChannelFormatDescriptor channelFormat, ulong width, ulong height, ulong depth, HipArrayFlags flags)
    {
        ChannelFormat = channelFormat;
        Width = width;
        Height = height;
        Depth = depth;
        Flags = flags;
    }

    /// <summary>获取该值 / Gets the channel format.</summary>
    public HipChannelFormatDescriptor ChannelFormat { get; }
    /// <summary>获取该值 / Gets the width in elements.</summary>
    public ulong Width { get; }
    /// <summary>获取该值 / Gets the height in elements.</summary>
    public ulong Height { get; }
    /// <summary>获取该值 / Gets the depth in elements.</summary>
    public ulong Depth { get; }
    /// <summary>获取该值 / Gets the array flags.</summary>
    public HipArrayFlags Flags { get; }
}

/// <summary>说明该托管接口 / Texture coordinate address modes.</summary>
public enum HipTextureAddressMode
{
    /// <summary>说明该托管接口 / Wrap coordinates.</summary>
    Wrap = 0,
    /// <summary>说明该托管接口 / Clamp coordinates.</summary>
    Clamp = 1,
    /// <summary>说明该托管接口 / Mirror coordinates.</summary>
    Mirror = 2,
    /// <summary>说明该托管接口 / Use the border color.</summary>
    Border = 3,
}

/// <summary>说明该托管接口 / Texture filtering modes.</summary>
public enum HipTextureFilterMode
{
    /// <summary>说明该托管接口 / Point filtering.</summary>
    Point = 0,
    /// <summary>说明该托管接口 / Linear filtering.</summary>
    Linear = 1,
}

/// <summary>说明该托管接口 / Texture read conversion modes.</summary>
public enum HipTextureReadMode
{
    /// <summary>说明该托管接口 / Return the element type.</summary>
    ElementType = 0,
    /// <summary>说明该托管接口 / Return normalized floating-point values.</summary>
    NormalizedFloat = 1,
}

/// <summary>说明该托管接口 / Types of resources that can back texture and surface objects.</summary>
public enum HipTextureResourceKind
{
    /// <summary>说明该托管接口 / A HIP array.</summary>
    Array = 0,
    /// <summary>说明该托管接口 / A HIP mipmapped array.</summary>
    MipmappedArray = 1,
    /// <summary>说明该托管接口 / Linear device memory.</summary>
    Linear = 2,
    /// <summary>说明该托管接口 / Pitched two-dimensional device memory.</summary>
    Pitch2D = 3,
}

/// <summary>说明该托管接口 / Texture resource-view formats.</summary>
public enum HipResourceViewFormat
{
    /// <summary>说明该托管接口 / Use the resource's underlying format.</summary>
    None = 0x00,
    /// <summary>说明该托管接口 / One unsigned 8-bit channel.</summary>
    UnsignedChar1 = 0x01,
    /// <summary>说明该托管接口 / Two unsigned 8-bit channels.</summary>
    UnsignedChar2 = 0x02,
    /// <summary>说明该托管接口 / Four unsigned 8-bit channels.</summary>
    UnsignedChar4 = 0x03,
    /// <summary>说明该托管接口 / One signed 8-bit channel.</summary>
    SignedChar1 = 0x04,
    /// <summary>说明该托管接口 / Two signed 8-bit channels.</summary>
    SignedChar2 = 0x05,
    /// <summary>说明该托管接口 / Four signed 8-bit channels.</summary>
    SignedChar4 = 0x06,
    /// <summary>说明该托管接口 / One unsigned 16-bit channel.</summary>
    UnsignedShort1 = 0x07,
    /// <summary>说明该托管接口 / Two unsigned 16-bit channels.</summary>
    UnsignedShort2 = 0x08,
    /// <summary>说明该托管接口 / Four unsigned 16-bit channels.</summary>
    UnsignedShort4 = 0x09,
    /// <summary>说明该托管接口 / One signed 16-bit channel.</summary>
    SignedShort1 = 0x0a,
    /// <summary>说明该托管接口 / Two signed 16-bit channels.</summary>
    SignedShort2 = 0x0b,
    /// <summary>说明该托管接口 / Four signed 16-bit channels.</summary>
    SignedShort4 = 0x0c,
    /// <summary>说明该托管接口 / One unsigned 32-bit channel.</summary>
    UnsignedInt1 = 0x0d,
    /// <summary>说明该托管接口 / Two unsigned 32-bit channels.</summary>
    UnsignedInt2 = 0x0e,
    /// <summary>说明该托管接口 / Four unsigned 32-bit channels.</summary>
    UnsignedInt4 = 0x0f,
    /// <summary>说明该托管接口 / One signed 32-bit channel.</summary>
    SignedInt1 = 0x10,
    /// <summary>说明该托管接口 / Two signed 32-bit channels.</summary>
    SignedInt2 = 0x11,
    /// <summary>说明该托管接口 / Four signed 32-bit channels.</summary>
    SignedInt4 = 0x12,
    /// <summary>说明该托管接口 / One 16-bit floating-point channel.</summary>
    Half1 = 0x13,
    /// <summary>说明该托管接口 / Two 16-bit floating-point channels.</summary>
    Half2 = 0x14,
    /// <summary>说明该托管接口 / Four 16-bit floating-point channels.</summary>
    Half4 = 0x15,
    /// <summary>说明该托管接口 / One 32-bit floating-point channel.</summary>
    Float1 = 0x16,
    /// <summary>说明该托管接口 / Two 32-bit floating-point channels.</summary>
    Float2 = 0x17,
    /// <summary>说明该托管接口 / Four 32-bit floating-point channels.</summary>
    Float4 = 0x18,
}

/// <summary>配置该资源 / Configures sampling behavior for a HIP texture object.</summary>
public sealed class HipTextureDescriptor
{
    /// <summary>获取该值 / Gets or sets the X address mode.</summary>
    public HipTextureAddressMode AddressModeX { get; set; } = HipTextureAddressMode.Clamp;
    /// <summary>获取该值 / Gets or sets the Y address mode.</summary>
    public HipTextureAddressMode AddressModeY { get; set; } = HipTextureAddressMode.Clamp;
    /// <summary>获取该值 / Gets or sets the Z address mode.</summary>
    public HipTextureAddressMode AddressModeZ { get; set; } = HipTextureAddressMode.Clamp;
    /// <summary>获取该值 / Gets or sets the base-level filter mode.</summary>
    public HipTextureFilterMode FilterMode { get; set; }
    /// <summary>获取该值 / Gets or sets the read conversion mode.</summary>
    public HipTextureReadMode ReadMode { get; set; }
    /// <summary>获取该值 / Gets or sets whether sRGB conversion is enabled.</summary>
    public bool Srgb { get; set; }
    /// <summary>获取该值 / Gets or sets the red border component.</summary>
    public float BorderColorRed { get; set; }
    /// <summary>获取该值 / Gets or sets the green border component.</summary>
    public float BorderColorGreen { get; set; }
    /// <summary>获取该值 / Gets or sets the blue border component.</summary>
    public float BorderColorBlue { get; set; }
    /// <summary>获取该值 / Gets or sets the alpha border component.</summary>
    public float BorderColorAlpha { get; set; }
    /// <summary>获取该值 / Gets or sets whether coordinates are normalized.</summary>
    public bool NormalizedCoordinates { get; set; }
    /// <summary>获取该值 / Gets or sets the maximum anisotropy ratio.</summary>
    public uint MaximumAnisotropy { get; set; }
    /// <summary>获取该值 / Gets or sets the mipmap filter mode.</summary>
    public HipTextureFilterMode MipmapFilterMode { get; set; }
    /// <summary>获取该值 / Gets or sets the mipmap level bias.</summary>
    public float MipmapLevelBias { get; set; }
    /// <summary>获取该值 / Gets or sets the minimum mipmap level clamp.</summary>
    public float MinimumMipmapLevelClamp { get; set; }
    /// <summary>获取该值 / Gets or sets the maximum mipmap level clamp.</summary>
    public float MaximumMipmapLevelClamp { get; set; }

    internal HipTextureDescriptorNative ToNative()
    {
        ValidateAddressMode(AddressModeX, nameof(AddressModeX));
        ValidateAddressMode(AddressModeY, nameof(AddressModeY));
        ValidateAddressMode(AddressModeZ, nameof(AddressModeZ));
        ValidateFilterMode(FilterMode, nameof(FilterMode));
        if (ReadMode < HipTextureReadMode.ElementType || ReadMode > HipTextureReadMode.NormalizedFloat) throw new ArgumentOutOfRangeException(nameof(ReadMode));
        ValidateFilterMode(MipmapFilterMode, nameof(MipmapFilterMode));
        if (float.IsNaN(MipmapLevelBias) || float.IsNaN(MinimumMipmapLevelClamp) || float.IsNaN(MaximumMipmapLevelClamp))
            throw new ArgumentException("Mipmap values cannot be NaN.");
        if (MinimumMipmapLevelClamp > MaximumMipmapLevelClamp)
            throw new ArgumentOutOfRangeException(nameof(MinimumMipmapLevelClamp));

        return new HipTextureDescriptorNative(
            AddressModeX, AddressModeY, AddressModeZ, FilterMode, ReadMode, Srgb,
            BorderColorRed, BorderColorGreen, BorderColorBlue, BorderColorAlpha,
            NormalizedCoordinates, MaximumAnisotropy, MipmapFilterMode,
            MipmapLevelBias, MinimumMipmapLevelClamp, MaximumMipmapLevelClamp);
    }

    internal static HipTextureDescriptor FromNative(HipTextureDescriptorNative value) => new()
    {
        AddressModeX = value.AddressModeX,
        AddressModeY = value.AddressModeY,
        AddressModeZ = value.AddressModeZ,
        FilterMode = value.FilterMode,
        ReadMode = value.ReadMode,
        Srgb = value.Srgb != 0,
        BorderColorRed = value.BorderColorRed,
        BorderColorGreen = value.BorderColorGreen,
        BorderColorBlue = value.BorderColorBlue,
        BorderColorAlpha = value.BorderColorAlpha,
        NormalizedCoordinates = value.NormalizedCoordinates != 0,
        MaximumAnisotropy = value.MaximumAnisotropy,
        MipmapFilterMode = value.MipmapFilterMode,
        MipmapLevelBias = value.MipmapLevelBias,
        MinimumMipmapLevelClamp = value.MinimumMipmapLevelClamp,
        MaximumMipmapLevelClamp = value.MaximumMipmapLevelClamp,
    };

    private static void ValidateAddressMode(HipTextureAddressMode value, string parameterName)
    {
        if (value < HipTextureAddressMode.Wrap || value > HipTextureAddressMode.Border) throw new ArgumentOutOfRangeException(parameterName);
    }

    private static void ValidateFilterMode(HipTextureFilterMode value, string parameterName)
    {
        if (value < HipTextureFilterMode.Point || value > HipTextureFilterMode.Linear) throw new ArgumentOutOfRangeException(parameterName);
    }
}

/// <summary>描述该资源 / Describes an optional view over a texture resource.</summary>
public sealed class HipResourceViewDescriptor
{
    /// <summary>获取该值 / Gets or sets the view format.</summary>
    public HipResourceViewFormat Format { get; set; }
    /// <summary>获取该值 / Gets or sets the width in elements.</summary>
    public ulong Width { get; set; }
    /// <summary>获取该值 / Gets or sets the height in elements.</summary>
    public ulong Height { get; set; }
    /// <summary>获取该值 / Gets or sets the depth in elements.</summary>
    public ulong Depth { get; set; }
    /// <summary>获取该值 / Gets or sets the first mipmap level.</summary>
    public uint FirstMipmapLevel { get; set; }
    /// <summary>获取该值 / Gets or sets the last mipmap level.</summary>
    public uint LastMipmapLevel { get; set; }
    /// <summary>获取该值 / Gets or sets the first layer.</summary>
    public uint FirstLayer { get; set; }
    /// <summary>获取该值 / Gets or sets the last layer.</summary>
    public uint LastLayer { get; set; }

    internal HipResourceViewDescriptorNative ToNative()
    {
        if (Format < HipResourceViewFormat.None || Format > HipResourceViewFormat.Float4) throw new ArgumentOutOfRangeException(nameof(Format));
        if (Width == 0) throw new ArgumentOutOfRangeException(nameof(Width));
        if (LastMipmapLevel < FirstMipmapLevel) throw new ArgumentOutOfRangeException(nameof(LastMipmapLevel));
        if (LastLayer < FirstLayer) throw new ArgumentOutOfRangeException(nameof(LastLayer));
        return new HipResourceViewDescriptorNative(
            Format,
            HipArrayDescriptor.ToUIntPtr(Width, nameof(Width)),
            HipArrayDescriptor.ToUIntPtr(Height, nameof(Height)),
            HipArrayDescriptor.ToUIntPtr(Depth, nameof(Depth)),
            FirstMipmapLevel,
            LastMipmapLevel,
            FirstLayer,
            LastLayer);
    }

    internal static HipResourceViewDescriptor FromNative(HipResourceViewDescriptorNative value) => new()
    {
        Format = value.Format,
        Width = value.Width.ToUInt64(),
        Height = value.Height.ToUInt64(),
        Depth = value.Depth.ToUInt64(),
        FirstMipmapLevel = value.FirstMipmapLevel,
        LastMipmapLevel = value.LastMipmapLevel,
        FirstLayer = value.FirstLayer,
        LastLayer = value.LastLayer,
    };
}

/// <summary>描述该资源 / Describes the borrowed resource handle returned for a texture object.</summary>
public readonly struct HipTextureResourceInfo
{
    /// <summary>创建该对象 / Creates resource information.</summary>
    public HipTextureResourceInfo(HipTextureResourceKind kind, IntPtr borrowedHandle)
    {
        Kind = kind;
        BorrowedHandle = borrowedHandle;
    }

    /// <summary>获取该值 / Gets the resource kind.</summary>
    public HipTextureResourceKind Kind { get; }
    /// <summary>获取该值 / Gets the borrowed native handle; the caller must not release it.</summary>
    public IntPtr BorrowedHandle { get; }
}

/// <summary>表示 legacy <c>hipTexObjectGetTextureDesc</c> 返回的 driver-style texture descriptor / Represents the driver-style texture descriptor returned by legacy <c>hipTexObjectGetTextureDesc</c>.</summary>
public readonly struct HipDriverTextureDescriptor
{
    internal HipDriverTextureDescriptor(HipDriverTextureDescriptorNative value)
    {
        AddressModeX = value.AddressModeX;
        AddressModeY = value.AddressModeY;
        AddressModeZ = value.AddressModeZ;
        FilterMode = value.FilterMode;
        Flags = value.Flags;
        MaximumAnisotropy = value.MaximumAnisotropy;
        MipmapFilterMode = value.MipmapFilterMode;
        MipmapLevelBias = value.MipmapLevelBias;
        MinimumMipmapLevelClamp = value.MinimumMipmapLevelClamp;
        MaximumMipmapLevelClamp = value.MaximumMipmapLevelClamp;
    }

    /// <summary>获取该值 / Gets the X address mode.</summary>
    public HipTextureAddressMode AddressModeX { get; }
    /// <summary>获取该值 / Gets the Y address mode.</summary>
    public HipTextureAddressMode AddressModeY { get; }
    /// <summary>获取该值 / Gets the Z address mode.</summary>
    public HipTextureAddressMode AddressModeZ { get; }
    /// <summary>获取该值 / Gets the filter mode.</summary>
    public HipTextureFilterMode FilterMode { get; }
    /// <summary>获取该值 / Gets native legacy flags.</summary>
    public uint Flags { get; }
    /// <summary>获取该值 / Gets maximum anisotropy.</summary>
    public uint MaximumAnisotropy { get; }
    /// <summary>获取该值 / Gets mipmap filtering.</summary>
    public HipTextureFilterMode MipmapFilterMode { get; }
    /// <summary>获取该值 / Gets mipmap level bias.</summary>
    public float MipmapLevelBias { get; }
    /// <summary>获取该值 / Gets minimum mipmap clamp.</summary>
    public float MinimumMipmapLevelClamp { get; }
    /// <summary>获取该值 / Gets maximum mipmap clamp.</summary>
    public float MaximumMipmapLevelClamp { get; }
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HipArrayDescriptorNative
{
    internal HipArrayDescriptorNative(UIntPtr width, UIntPtr height, HipArrayFormat format, uint channelCount)
    {
        Width = width;
        Height = height;
        Format = format;
        ChannelCount = channelCount;
    }

    internal readonly UIntPtr Width;
    internal readonly UIntPtr Height;
    internal readonly HipArrayFormat Format;
    internal readonly uint ChannelCount;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HipArray3DDescriptorNative
{
    internal HipArray3DDescriptorNative(UIntPtr width, UIntPtr height, UIntPtr depth, HipArrayFormat format, uint channelCount, HipArrayFlags flags)
    {
        Width = width;
        Height = height;
        Depth = depth;
        Format = format;
        ChannelCount = channelCount;
        Flags = flags;
    }

    internal readonly UIntPtr Width;
    internal readonly UIntPtr Height;
    internal readonly UIntPtr Depth;
    internal readonly HipArrayFormat Format;
    internal readonly uint ChannelCount;
    internal readonly HipArrayFlags Flags;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HipTextureDescriptorNative
{
    internal HipTextureDescriptorNative(
        HipTextureAddressMode addressModeX,
        HipTextureAddressMode addressModeY,
        HipTextureAddressMode addressModeZ,
        HipTextureFilterMode filterMode,
        HipTextureReadMode readMode,
        bool srgb,
        float borderColorRed,
        float borderColorGreen,
        float borderColorBlue,
        float borderColorAlpha,
        bool normalizedCoordinates,
        uint maximumAnisotropy,
        HipTextureFilterMode mipmapFilterMode,
        float mipmapLevelBias,
        float minimumMipmapLevelClamp,
        float maximumMipmapLevelClamp)
    {
        AddressModeX = addressModeX;
        AddressModeY = addressModeY;
        AddressModeZ = addressModeZ;
        FilterMode = filterMode;
        ReadMode = readMode;
        Srgb = srgb ? 1 : 0;
        BorderColorRed = borderColorRed;
        BorderColorGreen = borderColorGreen;
        BorderColorBlue = borderColorBlue;
        BorderColorAlpha = borderColorAlpha;
        NormalizedCoordinates = normalizedCoordinates ? 1 : 0;
        MaximumAnisotropy = maximumAnisotropy;
        MipmapFilterMode = mipmapFilterMode;
        MipmapLevelBias = mipmapLevelBias;
        MinimumMipmapLevelClamp = minimumMipmapLevelClamp;
        MaximumMipmapLevelClamp = maximumMipmapLevelClamp;
    }

    internal readonly HipTextureAddressMode AddressModeX;
    internal readonly HipTextureAddressMode AddressModeY;
    internal readonly HipTextureAddressMode AddressModeZ;
    internal readonly HipTextureFilterMode FilterMode;
    internal readonly HipTextureReadMode ReadMode;
    internal readonly int Srgb;
    internal readonly float BorderColorRed;
    internal readonly float BorderColorGreen;
    internal readonly float BorderColorBlue;
    internal readonly float BorderColorAlpha;
    internal readonly int NormalizedCoordinates;
    internal readonly uint MaximumAnisotropy;
    internal readonly HipTextureFilterMode MipmapFilterMode;
    internal readonly float MipmapLevelBias;
    internal readonly float MinimumMipmapLevelClamp;
    internal readonly float MaximumMipmapLevelClamp;
}

[StructLayout(LayoutKind.Explicit, Size = 56)]
internal struct HipResourceUnionNative
{
    [FieldOffset(0)] internal IntPtr Handle;
    [FieldOffset(8)] internal HipChannelFormatDescriptor ChannelFormat;
    [FieldOffset(32)] internal UIntPtr SizeInBytes;
    [FieldOffset(32)] internal UIntPtr Width;
    [FieldOffset(40)] internal UIntPtr Height;
    [FieldOffset(48)] internal UIntPtr PitchInBytes;
}

[StructLayout(LayoutKind.Sequential)]
internal struct HipResourceDescriptorNative
{
    internal HipResourceDescriptorNative(HipTextureResourceKind kind, IntPtr handle)
    {
        ResourceType = kind;
        Padding = 0;
        Resource = new HipResourceUnionNative { Handle = handle };
    }

    internal static HipResourceDescriptorNative ForLinear(IntPtr pointer, HipChannelFormatDescriptor channelFormat, UIntPtr sizeInBytes)
    {
        var descriptor = new HipResourceDescriptorNative(HipTextureResourceKind.Linear, pointer);
        descriptor.Resource.ChannelFormat = channelFormat;
        descriptor.Resource.SizeInBytes = sizeInBytes;
        return descriptor;
    }

    internal static HipResourceDescriptorNative ForPitch2D(IntPtr pointer, HipChannelFormatDescriptor channelFormat, UIntPtr width, UIntPtr height, UIntPtr pitchInBytes)
    {
        var descriptor = new HipResourceDescriptorNative(HipTextureResourceKind.Pitch2D, pointer);
        descriptor.Resource.ChannelFormat = channelFormat;
        descriptor.Resource.Width = width;
        descriptor.Resource.Height = height;
        descriptor.Resource.PitchInBytes = pitchInBytes;
        return descriptor;
    }

    internal HipTextureResourceKind ResourceType;
    private int Padding;
    internal HipResourceUnionNative Resource;
}

[StructLayout(LayoutKind.Sequential)]
internal readonly struct HipResourceViewDescriptorNative
{
    internal HipResourceViewDescriptorNative(HipResourceViewFormat format, UIntPtr width, UIntPtr height, UIntPtr depth, uint firstMipmapLevel, uint lastMipmapLevel, uint firstLayer, uint lastLayer)
    {
        Format = format;
        Width = width;
        Height = height;
        Depth = depth;
        FirstMipmapLevel = firstMipmapLevel;
        LastMipmapLevel = lastMipmapLevel;
        FirstLayer = firstLayer;
        LastLayer = lastLayer;
    }

    internal readonly HipResourceViewFormat Format;
    internal readonly UIntPtr Width;
    internal readonly UIntPtr Height;
    internal readonly UIntPtr Depth;
    internal readonly uint FirstMipmapLevel;
    internal readonly uint LastMipmapLevel;
    internal readonly uint FirstLayer;
    internal readonly uint LastLayer;
}

[StructLayout(LayoutKind.Explicit, Size = 104)]
internal struct HipDriverTextureDescriptorNative
{
    [FieldOffset(0)] internal HipTextureAddressMode AddressModeX;
    [FieldOffset(4)] internal HipTextureAddressMode AddressModeY;
    [FieldOffset(8)] internal HipTextureAddressMode AddressModeZ;
    [FieldOffset(12)] internal HipTextureFilterMode FilterMode;
    [FieldOffset(16)] internal uint Flags;
    [FieldOffset(20)] internal uint MaximumAnisotropy;
    [FieldOffset(24)] internal HipTextureFilterMode MipmapFilterMode;
    [FieldOffset(28)] internal float MipmapLevelBias;
    [FieldOffset(32)] internal float MinimumMipmapLevelClamp;
    [FieldOffset(36)] internal float MaximumMipmapLevelClamp;
}
