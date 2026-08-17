using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>提供 HIP 虚拟内存 owner 创建入口 / Provides HIP virtual-memory owner creation entry points.</summary>
public sealed partial class HipRuntime
{
    /// <summary>预留该资源 / Reserves a HIP virtual-address range.</summary>
    public unsafe HipVirtualMemoryReservation ReserveVirtualMemory(ulong byteLength, ulong alignment = 0, IntPtr requestedAddress = default)
    {
        ThrowIfDisposed();
        if (byteLength == 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
        IntPtr address = IntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemAddressReserve(
            (IntPtr)(&address),
            HipDeviceMemory.ToUIntPtr(byteLength, nameof(byteLength)),
            HipDeviceMemory.ToUIntPtr(alignment, nameof(alignment)),
            requestedAddress,
            0), "hipMemAddressReserve");
        if (address == IntPtr.Zero) throw new InvalidOperationException("hipMemAddressReserve succeeded but returned a null address.");
        return new HipVirtualMemoryReservation(_nativeApi, address, byteLength);
    }

    /// <summary>创建该对象 / Creates an owning physical-memory allocation handle.</summary>
    public unsafe HipPhysicalMemoryAllocation CreatePhysicalMemory(ulong byteLength, HipVirtualMemoryAllocationOptions options)
    {
        ThrowIfDisposed();
        if (byteLength == 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (options is null) throw new ArgumentNullException(nameof(options));
        IntPtr handle = IntPtr.Zero;
        HipVirtualMemoryAllocationPropertiesNative properties = new(options.DeviceOrdinal, options.RequestedHandleTypes);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemCreate(
            (IntPtr)(&handle),
            HipDeviceMemory.ToUIntPtr(byteLength, nameof(byteLength)),
            (IntPtr)(&properties),
            0), "hipMemCreate");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMemCreate succeeded but returned a null allocation handle.");
        return new HipPhysicalMemoryAllocation(_nativeApi, handle, byteLength);
    }

    /// <summary>导入共享句柄 / Imports an owning physical-memory allocation handle from a platform handle.</summary>
    public unsafe HipPhysicalMemoryAllocation ImportPhysicalMemory(IntPtr operatingSystemHandle, HipMemoryAllocationHandleType handleType, ulong byteLength)
    {
        ThrowIfDisposed();
        if (operatingSystemHandle == IntPtr.Zero) throw new ArgumentException("A non-null operating-system handle is required.", nameof(operatingSystemHandle));
        if (handleType == HipMemoryAllocationHandleType.None) throw new ArgumentOutOfRangeException(nameof(handleType));
        if (byteLength == 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
        IntPtr handle = IntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemImportFromShareableHandle((IntPtr)(&handle), operatingSystemHandle, (int)handleType), "hipMemImportFromShareableHandle");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMemImportFromShareableHandle succeeded but returned a null allocation handle.");
        return new HipPhysicalMemoryAllocation(_nativeApi, handle, byteLength);
    }

    /// <summary>保活该资源 / Retains an owning allocation handle for a mapped virtual address.</summary>
    public unsafe HipPhysicalMemoryAllocation RetainPhysicalMemory(IntPtr mappedAddress, ulong byteLength)
    {
        ThrowIfDisposed();
        if (mappedAddress == IntPtr.Zero) throw new ArgumentException("A non-null mapped address is required.", nameof(mappedAddress));
        if (byteLength == 0) throw new ArgumentOutOfRangeException(nameof(byteLength));
        IntPtr handle = IntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemRetainAllocationHandle((IntPtr)(&handle), mappedAddress), "hipMemRetainAllocationHandle");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMemRetainAllocationHandle succeeded but returned a null allocation handle.");
        return new HipPhysicalMemoryAllocation(_nativeApi, handle, byteLength);
    }

    /// <summary>提交该操作 / Submits native sparse-array map information asynchronously. HIP currently reports NotSupported on AMD GPUs.</summary>
    public void MapArrayAsync(IntPtr nativeMapInformation, uint count, HipStream stream)
    {
        ThrowIfDisposed();
        if (nativeMapInformation == IntPtr.Zero) throw new ArgumentException("A non-null native map-information address is required.", nameof(nativeMapInformation));
        if (count == 0) throw new ArgumentOutOfRangeException(nameof(count));
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Stream belongs to a different HIP Runtime client.", nameof(stream));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemMapArrayAsync(nativeMapInformation, count, stream.DangerousGetHandle()), "hipMemMapArrayAsync");
    }
}

[StructLayout(LayoutKind.Sequential)]
internal struct HipVirtualMemoryAllocationPropertiesNative
{
    internal HipVirtualMemoryAllocationPropertiesNative(int deviceOrdinal, HipMemoryAllocationHandleType handleTypes)
    {
        AllocationType = 1;
        RequestedHandleTypes = (int)handleTypes;
        Location = new HipMemLocation(1, deviceOrdinal);
        WindowsHandleMetadata = IntPtr.Zero;
        AllocationFlags = 0;
        Reserved = 0;
    }

    internal int AllocationType;
    internal int RequestedHandleTypes;
    internal HipMemLocation Location;
    internal IntPtr WindowsHandleMetadata;
    internal uint AllocationFlags;
    internal uint Reserved;
}
