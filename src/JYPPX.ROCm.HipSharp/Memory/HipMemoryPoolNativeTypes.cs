using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>表示 pinned HIP 7.2.1 <c>hipMemPoolProps</c> ABI / Represents the pinned HIP 7.2.1 <c>hipMemPoolProps</c> ABI.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HipMemoryPoolPropertiesNative
{
    internal int AllocationType;
    internal int HandleTypes;
    internal HipMemLocation Location;
    internal IntPtr Win32SecurityAttributes;
    internal UIntPtr MaximumSize;
    internal ulong Reserved0;
    internal ulong Reserved1;
    internal ulong Reserved2;
    internal ulong Reserved3;
    internal ulong Reserved4;
    internal ulong Reserved5;
    internal ulong Reserved6;

    internal static HipMemoryPoolPropertiesNative ForDevice(int deviceOrdinal, UIntPtr maximumSize) => new()
    {
        AllocationType = 1,
        HandleTypes = 0,
        Location = new HipMemLocation(1, deviceOrdinal),
        MaximumSize = maximumSize,
    };
}

/// <summary>表示 pinned HIP 7.2.1 <c>hipMemAccessDesc</c> ABI / Represents the pinned HIP 7.2.1 <c>hipMemAccessDesc</c> ABI.</summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly struct HipMemoryPoolAccessDescriptorNative
{
    internal HipMemoryPoolAccessDescriptorNative(int deviceOrdinal, HipMemoryPoolAccess access)
    {
        Location = new HipMemLocation(1, deviceOrdinal);
        Access = access;
    }

    internal readonly HipMemLocation Location;
    internal readonly HipMemoryPoolAccess Access;
}
