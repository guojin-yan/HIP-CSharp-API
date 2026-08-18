using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>拥有 HIP 虚拟地址预留 / Owns a HIP virtual-address reservation.</summary>
public sealed class HipVirtualMemoryReservation : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipVirtualAddressHandle _handle;
    private readonly object _sync = new();
    private int _mappingCount;

    internal HipVirtualMemoryReservation(IHipNativeApi nativeApi, IntPtr address, ulong byteLength)
    {
        _nativeApi = nativeApi;
        _handle = new HipVirtualAddressHandle(nativeApi, address, byteLength);
        ByteLength = byteLength;
    }

    /// <summary>获取该值 / Gets the reserved byte length.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取该值 / Gets whether the reservation has been released.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取该值 / Gets the reserved native address. The caller must not free it.</summary>
    public IntPtr DangerousGetAddress()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>映射该资源 / Maps an owning physical allocation at this reservation's base address.</summary>
    public HipVirtualMemoryMapping Map(HipPhysicalMemoryAllocation allocation, ulong byteLength, ulong allocationOffset = 0)
    {
        if (allocation is null) throw new ArgumentNullException(nameof(allocation));
        if (!ReferenceEquals(_nativeApi, allocation.NativeApi)) throw new ArgumentException("Reservation and allocation belong to different HIP Runtime clients.", nameof(allocation));
        if (byteLength == 0 || byteLength > ByteLength || byteLength > allocation.ByteLength) throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (allocationOffset != 0) throw new ArgumentOutOfRangeException(nameof(allocationOffset), "HIP 7.2.1 currently requires a zero allocation offset.");
        lock (_sync)
        {
            ThrowIfDisposed();
            allocation.AcquireMapping();
            bool reservationAcquired = false;
            try
            {
                _mappingCount++;
                reservationAcquired = true;
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemMap(
                    _handle.DangerousGetHandle(),
                    HipDeviceMemory.ToUIntPtr(byteLength, nameof(byteLength)),
                    UIntPtr.Zero,
                    allocation.DangerousGetHandle(),
                    0), "hipMemMap");
                return new HipVirtualMemoryMapping(this, allocation, byteLength);
            }
            catch
            {
                if (reservationAcquired) _mappingCount--;
                allocation.ReleaseMapping();
                throw;
            }
        }
    }

    /// <summary>设置该值 / Sets virtual-memory access policies for the reservation base range.</summary>
    public unsafe void SetAccess(ulong byteLength, params HipVirtualMemoryAccessDescriptor[] descriptors)
    {
        if (byteLength == 0 || byteLength > ByteLength) throw new ArgumentOutOfRangeException(nameof(byteLength));
        if (descriptors is null || descriptors.Length == 0) throw new ArgumentException("At least one access descriptor is required.", nameof(descriptors));
        lock (_sync)
        {
            ThrowIfDisposed();
            fixed (HipVirtualMemoryAccessDescriptor* nativeDescriptors = descriptors)
            {
                UIntPtr count = UIntPtr.Size == 4 ? new UIntPtr(checked((uint)descriptors.Length)) : new UIntPtr((ulong)descriptors.Length);
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemSetAccess(_handle.DangerousGetHandle(), HipDeviceMemory.ToUIntPtr(byteLength, nameof(byteLength)), (IntPtr)nativeDescriptors, count), "hipMemSetAccess");
            }
        }
    }

    /// <summary>获取该值 / Gets access flags for a location at this reservation's base address.</summary>
    public unsafe HipMemoryAccessFlags GetAccess(HipMemLocation location)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            ulong flags = 0;
            HipMemLocation nativeLocation = location;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemGetAccess((IntPtr)(&flags), (IntPtr)(&nativeLocation), _handle.DangerousGetHandle()), "hipMemGetAccess");
            return (HipMemoryAccessFlags)flags;
        }
    }

    /// <summary>释放该资源 / Releases the reservation after all mappings have been disposed.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (IsDisposed) return;
            if (_mappingCount != 0) throw new InvalidOperationException("Dispose all virtual-memory mappings before releasing the reservation.");
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipMemAddressFree");
            _handle.Dispose();
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal void ReleaseMapping()
    {
        lock (_sync)
        {
            if (_mappingCount > 0) _mappingCount--;
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipVirtualMemoryReservation));
    }
}

/// <summary>拥有该资源 / Owns a HIP physical-memory allocation handle.</summary>
public sealed class HipPhysicalMemoryAllocation : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipPhysicalMemoryHandle _handle;
    private readonly object _sync = new();
    private int _mappingCount;

    internal HipPhysicalMemoryAllocation(IHipNativeApi nativeApi, IntPtr handle, ulong byteLength)
    {
        _nativeApi = nativeApi;
        _handle = new HipPhysicalMemoryHandle(nativeApi, handle);
        ByteLength = byteLength;
    }

    /// <summary>获取该值 / Gets the allocation byte length.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取该值 / Gets whether the allocation handle has been released.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>导出共享句柄 / Exports an operating-system shareable handle when supported by HIP and the current platform.</summary>
    public unsafe IntPtr ExportShareableHandle(HipMemoryAllocationHandleType handleType)
    {
        if (handleType == HipMemoryAllocationHandleType.None) throw new ArgumentOutOfRangeException(nameof(handleType));
        lock (_sync)
        {
            ThrowIfDisposed();
            IntPtr result = IntPtr.Zero;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemExportToShareableHandle((IntPtr)(&result), _handle.DangerousGetHandle(), (int)handleType, 0), "hipMemExportToShareableHandle");
            if (result == IntPtr.Zero) throw new InvalidOperationException("hipMemExportToShareableHandle succeeded but returned a null handle.");
            return result;
        }
    }

    /// <summary>释放该资源 / Releases the allocation handle after all mappings have been disposed.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (IsDisposed) return;
            if (_mappingCount != 0) throw new InvalidOperationException("Dispose all virtual-memory mappings before releasing the physical allocation.");
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipMemRelease");
            _handle.Dispose();
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal IntPtr DangerousGetHandle()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    internal void AcquireMapping()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _mappingCount++;
        }
    }

    internal void ReleaseMapping()
    {
        lock (_sync)
        {
            if (_mappingCount > 0) _mappingCount--;
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipPhysicalMemoryAllocation));
    }
}

/// <summary>拥有该资源 / Owns one mapping between a reservation and a physical allocation.</summary>
public sealed class HipVirtualMemoryMapping : IDisposable
{
    private HipVirtualMemoryReservation? _reservation;
    private HipPhysicalMemoryAllocation? _allocation;
    private readonly ulong _byteLength;

    internal HipVirtualMemoryMapping(HipVirtualMemoryReservation reservation, HipPhysicalMemoryAllocation allocation, ulong byteLength)
    {
        _reservation = reservation;
        _allocation = allocation;
        _byteLength = byteLength;
    }

    /// <summary>获取该值 / Gets the mapped byte length.</summary>
    public ulong ByteLength => _byteLength;

    /// <summary>获取该值 / Gets whether the mapping has been released.</summary>
    public bool IsDisposed => _reservation is null;

    /// <summary>说明该托管接口 / Unmaps the range and releases its reservation/allocation leases.</summary>
    public void Dispose()
    {
        HipVirtualMemoryReservation? reservation = _reservation;
        HipPhysicalMemoryAllocation? allocation = _allocation;
        if (reservation is null || allocation is null) return;
        HipCall.ThrowIfFailed(reservation.NativeApi, reservation.NativeApi.MemUnmap(reservation.DangerousGetAddress(), HipDeviceMemory.ToUIntPtr(_byteLength, nameof(ByteLength))), "hipMemUnmap");
        _reservation = null;
        _allocation = null;
        reservation.ReleaseMapping();
        allocation.ReleaseMapping();
    }
}

internal sealed class HipVirtualAddressHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly UIntPtr _size;

    internal HipVirtualAddressHandle(IHipNativeApi nativeApi, IntPtr address, ulong byteLength) : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _size = HipDeviceMemory.ToUIntPtr(byteLength, nameof(byteLength));
        SetHandle(address);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = _nativeApi.MemAddressFree(handle, _size);
        if (error == HipError.Success) SetHandleAsInvalid();
        return error;
    }

    protected override bool ReleaseHandle() => _nativeApi.MemAddressFree(handle, _size) == HipError.Success;
}

internal sealed class HipPhysicalMemoryHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;

    internal HipPhysicalMemoryHandle(IHipNativeApi nativeApi, IntPtr handle) : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = _nativeApi.MemRelease(handle);
        if (error == HipError.Success) SetHandleAsInvalid();
        return error;
    }

    protected override bool ReleaseHandle() => _nativeApi.MemRelease(handle) == HipError.Success;
}
