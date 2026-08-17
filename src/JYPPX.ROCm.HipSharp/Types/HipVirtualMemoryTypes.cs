using System;
using System.Runtime.InteropServices;

namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>表示原生 hipMemAccessFlags 数值 / Represents native hipMemAccessFlags values.</summary>
public enum HipMemoryAccessFlags : ulong
{
    /// <summary>说明该托管接口 / Memory is inaccessible from the location.</summary>
    None = 0,
    /// <summary>说明该托管接口 / Memory is readable from the location.</summary>
    Read = 1,
    /// <summary>说明该托管接口 / Memory is readable and writable from the location.</summary>
    ReadWrite = 3,
}

/// <summary>说明该托管接口 / Native hipMemAllocationHandleType values.</summary>
[Flags]
public enum HipMemoryAllocationHandleType
{
    /// <summary>说明该托管接口 / No shareable handle is requested.</summary>
    None = 0,
    /// <summary>说明该托管接口 / A POSIX file descriptor handle.</summary>
    PosixFileDescriptor = 1,
    /// <summary>说明该托管接口 / A Win32 NT handle.</summary>
    Win32 = 2,
    /// <summary>说明该托管接口 / A Win32 KMT handle.</summary>
    Win32Kmt = 4,
}

/// <summary>描述该资源 / Describes an access policy applied to a virtual-memory range.</summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct HipVirtualMemoryAccessDescriptor
{
    /// <summary>初始化该对象 / Initializes an access descriptor.</summary>
    public HipVirtualMemoryAccessDescriptor(HipMemLocation location, HipMemoryAccessFlags flags)
    {
        Location = location;
        Flags = flags;
    }

    /// <summary>获取该值 / Gets the location receiving the policy.</summary>
    public HipMemLocation Location { get; }

    /// <summary>获取该值 / Gets the access policy.</summary>
    public HipMemoryAccessFlags Flags { get; }
}

/// <summary>说明该托管接口 / Specifies the physical-memory allocation location and shareability.</summary>
public sealed class HipVirtualMemoryAllocationOptions
{
    /// <summary>初始化该对象 / Initializes an allocation request for a device.</summary>
    public HipVirtualMemoryAllocationOptions(int deviceOrdinal)
    {
        if (deviceOrdinal < 0) throw new ArgumentOutOfRangeException(nameof(deviceOrdinal));
        DeviceOrdinal = deviceOrdinal;
    }

    /// <summary>获取该值 / Gets the owning device ordinal.</summary>
    public int DeviceOrdinal { get; }

    /// <summary>获取该值 / Gets or sets explicitly requested cross-process handle types.</summary>
    public HipMemoryAllocationHandleType RequestedHandleTypes { get; set; }
}
