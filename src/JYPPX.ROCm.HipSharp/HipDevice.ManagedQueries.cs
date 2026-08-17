using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>提供设备查询与纹理能力信息 / Provides device queries and texture capability information.</summary>
public sealed partial class HipDevice
{
    /// <summary>获取该值 / Gets this device's compute capability.</summary>
    public unsafe HipComputeCapability GetComputeCapability()
    {
        int major = 0;
        int minor = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceComputeCapability((IntPtr)(&major), (IntPtr)(&minor), Ordinal), "hipDeviceComputeCapability");
        if (major < 0 || minor < 0) throw new InvalidOperationException("HIP returned a negative compute capability value.");
        return new HipComputeCapability(major, minor);
    }

    /// <summary>获取该值 / Gets this device's PCI bus identifier.</summary>
    public string GetPciBusId()
    {
        const int BufferLength = 256;
        IntPtr buffer = Marshal.AllocHGlobal(BufferLength);
        try
        {
            Marshal.WriteByte(buffer, 0);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetPCIBusId(buffer, BufferLength, Ordinal), "hipDeviceGetPCIBusId");
            string value = Marshal.PtrToStringAnsi(buffer) ?? string.Empty;
            if (value.Length == 0) throw new InvalidOperationException("hipDeviceGetPCIBusId succeeded but returned an empty identifier.");
            return value;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>获取该值 / Gets this device's native 16-byte UUID.</summary>
    public HipDeviceUuid GetUuid()
    {
        IntPtr buffer = Marshal.AllocHGlobal(16);
        try
        {
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetUuid(buffer, Ordinal), "hipDeviceGetUuid");
            var value = new byte[16];
            Marshal.Copy(buffer, value, 0, value.Length);
            return new HipDeviceUuid(value);
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    /// <summary>获取该值 / Gets total memory visible to this device, in bytes.</summary>
    public unsafe ulong GetTotalMemory()
    {
        UIntPtr bytes = UIntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceTotalMem((IntPtr)(&bytes), Ordinal), "hipDeviceTotalMem");
        return bytes.ToUInt64();
    }

    /// <summary>获取该值 / Gets the maximum width of a one-dimensional linear texture for this device and channel format.</summary>
    public unsafe ulong GetTexture1DLinearMaximumWidth(HipChannelFormatDescriptor channelFormat)
    {
        UIntPtr width = UIntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.DeviceGetTexture1DLinearMaxWidth((IntPtr)(&width), (IntPtr)(&channelFormat), Ordinal), "hipDeviceGetTexture1DLinearMaxWidth");
        return width.ToUInt64();
    }
}
