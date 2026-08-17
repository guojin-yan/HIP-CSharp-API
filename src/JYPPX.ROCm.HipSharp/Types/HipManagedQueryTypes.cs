using System;
using System.Runtime.InteropServices;

namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>表示 HIP device compute capability 数值对 / Represents a HIP device compute capability pair.</summary>
public readonly struct HipComputeCapability
{
    /// <summary>初始化该对象 / Initializes a compute capability pair.</summary>
    public HipComputeCapability(int major, int minor)
    {
        Major = major;
        Minor = minor;
    }

    /// <summary>获取该值 / Gets the major capability value.</summary>
    public int Major { get; }

    /// <summary>获取该值 / Gets the minor capability value.</summary>
    public int Minor { get; }

    /// <summary>将 compute capability 格式化为主版本和次版本 / Formats the compute capability as major and minor versions.</summary>
    public override string ToString() => Major + "." + Minor;
}

/// <summary>表示该原生概念 / Represents the least and greatest stream priorities supported by a device.</summary>
public readonly struct HipStreamPriorityRange
{
    /// <summary>初始化该对象 / Initializes a stream-priority range.</summary>
    public HipStreamPriorityRange(int leastPriority, int greatestPriority)
    {
        LeastPriority = leastPriority;
        GreatestPriority = greatestPriority;
    }

    /// <summary>获取该值 / Gets the least-prioritized value.</summary>
    public int LeastPriority { get; }

    /// <summary>获取该值 / Gets the greatest-prioritized value.</summary>
    public int GreatestPriority { get; }
}

/// <summary>表示该原生概念 / Represents the 16-byte native HIP device UUID without changing its byte order.</summary>
public readonly struct HipDeviceUuid
{
    private readonly byte[] _bytes;

    internal HipDeviceUuid(byte[] bytes)
    {
        if (bytes is null || bytes.Length != 16) throw new ArgumentException("A HIP device UUID must contain exactly 16 bytes.", nameof(bytes));
        _bytes = (byte[])bytes.Clone();
    }

    /// <summary>复制数据 / Copies the native UUID bytes.</summary>
    public byte[] ToArray() => _bytes is null ? new byte[16] : (byte[])_bytes.Clone();

    /// <summary>将 UUID 格式化为大写十六进制字符串 / Formats the UUID as an uppercase hexadecimal string.</summary>
    public override string ToString()
    {
        byte[] bytes = ToArray();
        char[] chars = new char[32];
        const string Hex = "0123456789ABCDEF";
        for (int index = 0; index < bytes.Length; index++)
        {
            chars[index * 2] = Hex[bytes[index] >> 4];
            chars[index * 2 + 1] = Hex[bytes[index] & 0x0F];
        }
        return new string(chars);
    }
}

/// <summary>说明该托管接口 / Native values returned by hipDeviceGetCacheConfig.</summary>
public enum HipDeviceCacheConfig
{
    /// <summary>说明该托管接口 / Use the runtime default.</summary>
    Default = 0,
    /// <summary>说明该托管接口 / Prefer shared memory.</summary>
    PreferShared = 1,
    /// <summary>说明该托管接口 / Prefer L1 cache.</summary>
    PreferL1 = 2,
    /// <summary>说明该托管接口 / Prefer equal shared-memory and L1-cache partitioning.</summary>
    PreferEqual = 3,
}

/// <summary>说明该托管接口 / Native values returned by hipDeviceGetSharedMemConfig.</summary>
public enum HipSharedMemoryConfig
{
    /// <summary>说明该托管接口 / Use the runtime default.</summary>
    DefaultBankSize = 0,
    /// <summary>说明该托管接口 / Use four-byte banks.</summary>
    FourByteBankSize = 1,
    /// <summary>说明该托管接口 / Use eight-byte banks.</summary>
    EightByteBankSize = 2,
}

/// <summary>表示该原生概念 / Represents the native hipPointerAttribute_t value.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct HipPointerAttributes
{
    /// <summary>获取该值 / Gets the native hipMemoryType value.</summary>
    public int MemoryType;

    /// <summary>获取该值 / Gets the associated device ordinal.</summary>
    public int DeviceOrdinal;

    /// <summary>获取该值 / Gets the device pointer, when present.</summary>
    public IntPtr DevicePointer;

    /// <summary>获取该值 / Gets the host pointer, when present.</summary>
    public IntPtr HostPointer;

    /// <summary>获取该值 / Gets whether HIP reports managed memory.</summary>
    public int IsManaged;

    /// <summary>获取该值 / Gets allocation flags returned by HIP.</summary>
    public uint AllocationFlags;
}

/// <summary>说明该托管接口 / Native capture states returned by HIP stream capture queries.</summary>
public enum HipStreamCaptureStatus
{
    /// <summary>说明该托管接口 / The stream is not capturing.</summary>
    None = 0,
    /// <summary>说明该托管接口 / The stream is actively capturing.</summary>
    Active = 1,
    /// <summary>说明该托管接口 / The capture was invalidated.</summary>
    Invalidated = 2,
}

/// <summary>表示该原生概念 / Represents the stable portion of hipStreamGetCaptureInfo output.</summary>
public readonly struct HipStreamCaptureInfo
{
    /// <summary>初始化该对象 / Initializes capture information.</summary>
    public HipStreamCaptureInfo(HipStreamCaptureStatus status, ulong identifier)
    {
        Status = status;
        Identifier = identifier;
    }

    /// <summary>获取该值 / Gets the capture status.</summary>
    public HipStreamCaptureStatus Status { get; }

    /// <summary>获取该值 / Gets the capture identifier.</summary>
    public ulong Identifier { get; }
}

/// <summary>表示该原生概念 / Represents hipStreamGetCaptureInfo_v2 output with borrowed native handles.</summary>
public readonly struct HipStreamCaptureInfoV2
{
    /// <summary>初始化该对象 / Initializes capture information.</summary>
    public HipStreamCaptureInfoV2(HipStreamCaptureStatus status, ulong identifier, IntPtr graphHandle, IntPtr dependencyHandles, ulong dependencyCount)
    {
        Status = status;
        Identifier = identifier;
        GraphHandle = graphHandle;
        DependencyHandles = dependencyHandles;
        DependencyCount = dependencyCount;
    }

    /// <summary>获取该值 / Gets the capture status.</summary>
    public HipStreamCaptureStatus Status { get; }

    /// <summary>获取该值 / Gets the capture identifier.</summary>
    public ulong Identifier { get; }

    /// <summary>获取该值 / Gets a borrowed graph handle. The caller must not destroy it.</summary>
    public IntPtr GraphHandle { get; }

    /// <summary>获取该值 / Gets a borrowed pointer to dependency handles. It is valid only for the native call contract.</summary>
    public IntPtr DependencyHandles { get; }

    /// <summary>获取该值 / Gets the number of borrowed dependency handles.</summary>
    public ulong DependencyCount { get; }
}

/// <summary>表示该原生概念 / Represents the 64-byte hipStreamAttrValue union.</summary>
[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct HipStreamAttributeValue
{
    /// <summary>说明该托管接口 / Reads or writes the first four bytes as an integer.</summary>
    [FieldOffset(0)]
    public int IntegerValue;

    /// <summary>说明该托管接口 / Reads or writes the first eight bytes as an integer.</summary>
    [FieldOffset(0)]
    public long WideIntegerValue;

    /// <summary>说明该托管接口 / Reads or writes the first pointer-sized value.</summary>
    [FieldOffset(0)]
    public IntPtr Address;
}

/// <summary>说明该托管接口 / Flags accepted by hipStreamWaitValue32 and hipStreamWaitValue64.</summary>
[Flags]
public enum HipStreamWaitValueFlags : uint
{
    /// <summary>说明该托管接口 / Use equality comparison.</summary>
    Equal = 0,
    /// <summary>说明该托管接口 / Use greater-than-or-equal comparison.</summary>
    GreaterOrEqual = 1,
    /// <summary>说明该托管接口 / Use bitwise-and comparison.</summary>
    And = 2,
    /// <summary>说明该托管接口 / Flush remote writes before waiting.</summary>
    NoMemoryBarrier = 4,
}
