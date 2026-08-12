using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Interop;

/// <summary>HIP graph internal ABI types / HIP graph 内部 ABI 类型.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HipKernelNodeParameters
{
    internal HipDim3 BlockDimensions;
    internal IntPtr Extra;
    internal IntPtr Function;
    internal HipDim3 GridDimensions;
    internal IntPtr KernelParameters;
    internal uint SharedMemoryBytes;
}

/// <summary>HIP graph memset internal ABI parameters / HIP graph memset 内部 ABI 参数.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HipMemsetNodeParameters
{
    internal IntPtr Destination;
    internal uint ElementSize;
    internal UIntPtr Height;
    internal UIntPtr Pitch;
    internal uint Value;
    internal UIntPtr Width;
}

/// <summary>HIP graph allocation internal ABI parameters / HIP graph allocation 内部 ABI 参数.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct HipMemoryAllocationNodeParameters
{
    internal HipMemoryPoolPropertiesNative PoolProperties;
    internal IntPtr AccessDescriptors;
    internal UIntPtr AccessDescriptorCount;
    internal UIntPtr ByteCount;
    internal IntPtr DevicePointer;
}

/// <summary>HIP graph executable update result ABI values / HIP graph executable update result ABI 值.</summary>
internal enum HipGraphExecUpdateResultNative
{
    Success = 0,
    Error = 1,
    TopologyChanged = 2,
    NodeTypeChanged = 3,
    FunctionChanged = 4,
    ParametersChanged = 5,
    NotSupported = 6,
    UnsupportedFunctionChange = 7,
}
