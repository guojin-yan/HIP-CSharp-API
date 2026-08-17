using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>拥有 HIP array 或表示带 lease 的 mipmap level view / Owns a HIP array or represents a leased mipmap-level view.</summary>
public sealed class HipArray : IDisposable, IHipTextureResourceOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipArrayHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _references;
    private bool _disposeRequested;

    internal HipArray(IHipNativeApi nativeApi, IntPtr handle, HipArrayInfo info, HipArrayReleaseKind releaseKind, IDisposable? parentLease = null)
    {
        _nativeApi = nativeApi;
        _handle = new HipArrayHandle(nativeApi, handle, releaseKind, parentLease);
        Info = info;
    }

    /// <summary>获取该值 / Gets the allocation shape, format, and flags captured when the wrapper was created.</summary>
    public HipArrayInfo Info { get; }

    /// <summary>获取该值 / Gets whether release has been requested or completed.</summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid;
        }
    }

    /// <summary>获取该值 / Gets the borrowed native array handle; the caller must not release it.</summary>
    public IntPtr DangerousGetHandle()
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>查询该状态 / Queries the runtime-style channel format, extent, and flags.</summary>
    public unsafe HipArrayInfo GetInfo()
    {
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipChannelFormatDescriptor descriptor = default;
            HipExtent extent = default;
            uint flags = 0;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ArrayGetInfo(
                (IntPtr)(&descriptor), (IntPtr)(&extent), (IntPtr)(&flags), handle), "hipArrayGetInfo");
            return new HipArrayInfo(descriptor, extent.Width.ToUInt64(), extent.Height.ToUInt64(), extent.Depth.ToUInt64(), (HipArrayFlags)flags);
        }
        finally
        {
            if (reference) ReleaseHandle();
        }
    }

    /// <summary>查询该状态 / Queries the driver-style one- or two-dimensional descriptor.</summary>
    public unsafe HipArrayDescriptor GetDriverDescriptor()
    {
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipArrayDescriptorNative descriptor = default;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ArrayGetDescriptor((IntPtr)(&descriptor), handle), "hipArrayGetDescriptor");
            return HipArrayDescriptor.FromNative(descriptor);
        }
        finally
        {
            if (reference) ReleaseHandle();
        }
    }

    /// <summary>查询该状态 / Queries the driver-style three-dimensional descriptor.</summary>
    public unsafe HipArray3DDescriptor GetDriver3DDescriptor()
    {
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipArray3DDescriptorNative descriptor = default;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Array3DGetDescriptor((IntPtr)(&descriptor), handle), "hipArray3DGetDescriptor");
            return HipArray3DDescriptor.FromNative(descriptor);
        }
        finally
        {
            if (reference) ReleaseHandle();
        }
    }

    /// <summary>复制数据 / Copies a contiguous managed buffer into one array row.</summary>
    public void CopyFrom(byte[] source, ulong destinationByteOffset = 0, ulong destinationRow = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ValidateLinearRegion(destinationByteOffset, destinationRow, (ulong)source.LongLength);
        if (source.Length == 0) return;
        GCHandle pinned = GCHandle.Alloc(source, GCHandleType.Pinned);
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyToArray(
                handle,
                HipDeviceMemory.ToUIntPtr(destinationByteOffset, nameof(destinationByteOffset)),
                HipDeviceMemory.ToUIntPtr(destinationRow, nameof(destinationRow)),
                pinned.AddrOfPinnedObject(),
                HipDeviceMemory.ToUIntPtr((ulong)source.LongLength, nameof(source)),
                (int)HipMemoryCopyKind.HostToDevice), "hipMemcpyToArray");
        }
        finally
        {
            if (reference) ReleaseHandle();
            if (pinned.IsAllocated) pinned.Free();
        }
    }

    /// <summary>复制数据 / Copies one contiguous array row into a managed buffer.</summary>
    public void CopyTo(byte[] destination, ulong sourceByteOffset = 0, ulong sourceRow = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ValidateLinearRegion(sourceByteOffset, sourceRow, (ulong)destination.LongLength);
        if (destination.Length == 0) return;
        GCHandle pinned = GCHandle.Alloc(destination, GCHandleType.Pinned);
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyFromArray(
                pinned.AddrOfPinnedObject(),
                handle,
                HipDeviceMemory.ToUIntPtr(sourceByteOffset, nameof(sourceByteOffset)),
                HipDeviceMemory.ToUIntPtr(sourceRow, nameof(sourceRow)),
                HipDeviceMemory.ToUIntPtr((ulong)destination.LongLength, nameof(destination)),
                (int)HipMemoryCopyKind.DeviceToHost), "hipMemcpyFromArray");
        }
        finally
        {
            if (reference) ReleaseHandle();
            if (pinned.IsAllocated) pinned.Free();
        }
    }

    /// <summary>复制数据 / Copies a pitched two-dimensional managed buffer into the array.</summary>
    public void Copy2DFrom(byte[] source, ulong widthBytes, ulong height, ulong sourcePitch = 0, ulong destinationX = 0, ulong destinationY = 0)
    {
        Copy2DManaged(source, widthBytes, height, sourcePitch, destinationX, destinationY, true, null);
    }

    /// <summary>复制数据 / Copies a two-dimensional region from the array into a pitched managed buffer.</summary>
    public void Copy2DTo(byte[] destination, ulong widthBytes, ulong height, ulong destinationPitch = 0, ulong sourceX = 0, ulong sourceY = 0)
    {
        Copy2DManaged(destination, widthBytes, height, destinationPitch, sourceX, sourceY, false, null);
    }

    /// <summary>排队执行该操作 / Queues a pitched two-dimensional managed-to-array copy and retains both resources until stream completion.</summary>
    public void Copy2DFromAsync(byte[] source, ulong widthBytes, ulong height, HipStream stream, ulong sourcePitch = 0, ulong destinationX = 0, ulong destinationY = 0)
    {
        Copy2DManaged(source, widthBytes, height, sourcePitch, destinationX, destinationY, true, stream);
    }

    /// <summary>排队执行该操作 / Queues a pitched two-dimensional array-to-managed copy and retains both resources until stream completion.</summary>
    public void Copy2DToAsync(byte[] destination, ulong widthBytes, ulong height, HipStream stream, ulong destinationPitch = 0, ulong sourceX = 0, ulong sourceY = 0)
    {
        Copy2DManaged(destination, widthBytes, height, destinationPitch, sourceX, sourceY, false, stream);
    }

    /// <summary>复制数据 / Copies a two-dimensional region to another array.</summary>
    public void Copy2DTo(HipArray destination, ulong widthBytes, ulong height, ulong sourceX = 0, ulong sourceY = 0, ulong destinationX = 0, ulong destinationY = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if (!ReferenceEquals(_nativeApi, destination._nativeApi)) throw new ArgumentException("Arrays belong to different HIP Runtime clients.", nameof(destination));
        Validate2DRegion(sourceX, sourceY, widthBytes, height);
        destination.Validate2DRegion(destinationX, destinationY, widthBytes, height);
        bool sourceReference = false;
        bool destinationReference = false;
        try
        {
            IntPtr source = AcquireHandle(out sourceReference);
            IntPtr target = destination.AcquireHandle(out destinationReference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Memcpy2DArrayToArray(
                target,
                HipDeviceMemory.ToUIntPtr(destinationX, nameof(destinationX)),
                HipDeviceMemory.ToUIntPtr(destinationY, nameof(destinationY)),
                source,
                HipDeviceMemory.ToUIntPtr(sourceX, nameof(sourceX)),
                HipDeviceMemory.ToUIntPtr(sourceY, nameof(sourceY)),
                HipDeviceMemory.ToUIntPtr(widthBytes, nameof(widthBytes)),
                HipDeviceMemory.ToUIntPtr(height, nameof(height)),
                (int)HipMemoryCopyKind.DeviceToDevice), "hipMemcpy2DArrayToArray");
        }
        finally
        {
            if (destinationReference) destination.ReleaseHandle();
            if (sourceReference) ReleaseHandle();
        }
    }

    /// <summary>释放该资源 / Releases the array after all asynchronous, texture, surface, and borrowed-view leases complete.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_references != 0) return;
        }

        ReleaseOwner();
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    IHipNativeApi IHipTextureResourceOwner.NativeApi => _nativeApi;
    HipTextureResourceKind IHipTextureResourceOwner.ResourceKind => HipTextureResourceKind.Array;
    IntPtr IHipTextureResourceOwner.AcquireHandle(out bool addedReference) => AcquireHandle(out addedReference);
    void IHipTextureResourceOwner.ReleaseHandle() => ReleaseHandle();

    internal IDisposable AcquireLease()
    {
        _ = AcquireHandle(out bool reference);
        if (!reference) throw new InvalidOperationException("Unable to acquire the HIP array handle.");
        return new HipArrayLease(this);
    }

    internal IntPtr AcquireHandle(out bool addedReference)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            addedReference = false;
            _handle.DangerousAddRef(ref addedReference);
            if (addedReference) _references++;
            return _handle.DangerousGetHandle();
        }
    }

    internal void ReleaseHandle()
    {
        bool releaseOwner;
        lock (_lifetimeSync)
        {
            if (_references > 0)
            {
                _handle.DangerousRelease();
                _references--;
            }
            releaseOwner = _disposeRequested && _references == 0 && !_handle.IsClosed && !_handle.IsInvalid;
        }

        if (releaseOwner) ReleaseOwner();
    }

    private void Copy2DManaged(byte[] buffer, ulong widthBytes, ulong height, ulong pitch, ulong arrayX, ulong arrayY, bool hostToArray, HipStream? stream)
    {
        if (buffer is null) throw new ArgumentNullException(nameof(buffer));
        if (stream is not null && !ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Array and stream belong to different HIP Runtime clients.", nameof(stream));
        if (pitch == 0) pitch = widthBytes;
        Validate2DRegion(arrayX, arrayY, widthBytes, height);
        if (pitch < widthBytes) throw new ArgumentOutOfRangeException(nameof(pitch));
        ulong required = height == 0 ? 0 : checked(checked(pitch * (height - 1)) + widthBytes);
        if (required > (ulong)buffer.LongLength) throw new ArgumentOutOfRangeException(nameof(buffer), "The managed buffer is smaller than the pitched copy region.");
        if (required == 0) return;

        GCHandle pinned = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        bool reference = false;
        try
        {
            IntPtr handle = AcquireHandle(out reference);
            HipError error;
            if (hostToArray)
            {
                error = stream is null
                    ? _nativeApi.Memcpy2DToArray(handle, ToUIntPtr(arrayX), ToUIntPtr(arrayY), pinned.AddrOfPinnedObject(), ToUIntPtr(pitch), ToUIntPtr(widthBytes), ToUIntPtr(height), (int)HipMemoryCopyKind.HostToDevice)
                    : _nativeApi.Memcpy2DToArrayAsync(handle, ToUIntPtr(arrayX), ToUIntPtr(arrayY), pinned.AddrOfPinnedObject(), ToUIntPtr(pitch), ToUIntPtr(widthBytes), ToUIntPtr(height), (int)HipMemoryCopyKind.HostToDevice, stream.DangerousGetHandle());
            }
            else
            {
                error = stream is null
                    ? _nativeApi.Memcpy2DFromArray(pinned.AddrOfPinnedObject(), ToUIntPtr(pitch), handle, ToUIntPtr(arrayX), ToUIntPtr(arrayY), ToUIntPtr(widthBytes), ToUIntPtr(height), (int)HipMemoryCopyKind.DeviceToHost)
                    : _nativeApi.Memcpy2DFromArrayAsync(pinned.AddrOfPinnedObject(), ToUIntPtr(pitch), handle, ToUIntPtr(arrayX), ToUIntPtr(arrayY), ToUIntPtr(widthBytes), ToUIntPtr(height), (int)HipMemoryCopyKind.DeviceToHost, stream.DangerousGetHandle());
            }
            string operation = hostToArray
                ? (stream is null ? "hipMemcpy2DToArray" : "hipMemcpy2DToArrayAsync")
                : (stream is null ? "hipMemcpy2DFromArray" : "hipMemcpy2DFromArrayAsync");
            HipCall.ThrowIfFailed(_nativeApi, error, operation);

            if (stream is not null)
            {
                stream.AddPendingLease(new HipAsyncLease(() =>
                {
                    if (pinned.IsAllocated) pinned.Free();
                    if (reference)
                    {
                        ReleaseHandle();
                        reference = false;
                    }
                }));
            }
        }
        catch
        {
            if (reference)
            {
                ReleaseHandle();
                reference = false;
            }
            if (pinned.IsAllocated) pinned.Free();
            throw;
        }
        finally
        {
            if (stream is null)
            {
                if (reference) ReleaseHandle();
                if (pinned.IsAllocated) pinned.Free();
            }
        }
    }

    private void ValidateLinearRegion(ulong byteOffset, ulong row, ulong byteCount)
    {
        ulong rowBytes = GetRowBytes();
        ulong rowCount = Math.Max(1UL, Info.Height);
        if (row >= rowCount) throw new ArgumentOutOfRangeException(nameof(row));
        if (byteOffset > rowBytes || byteCount > rowBytes - byteOffset) throw new ArgumentOutOfRangeException(nameof(byteCount));
    }

    private void Validate2DRegion(ulong x, ulong y, ulong widthBytes, ulong height)
    {
        if (widthBytes == 0) throw new ArgumentOutOfRangeException(nameof(widthBytes));
        if (height == 0) throw new ArgumentOutOfRangeException(nameof(height));
        ulong rowBytes = GetRowBytes();
        ulong rowCount = Math.Max(1UL, Info.Height);
        if (x > rowBytes || widthBytes > rowBytes - x) throw new ArgumentOutOfRangeException(nameof(widthBytes));
        if (y > rowCount || height > rowCount - y) throw new ArgumentOutOfRangeException(nameof(height));
    }

    private ulong GetRowBytes() => checked(Info.Width * Info.ChannelFormat.GetBytesPerElement());

    private static UIntPtr ToUIntPtr(ulong value) => HipDeviceMemory.ToUIntPtr(value, nameof(value));

    private void ReleaseOwner()
    {
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }
        HipCall.ThrowIfFailed(_nativeApi, error, _handle.ReleaseOperation);
    }

    private void ThrowIfDisposed()
    {
        if (_disposeRequested || _handle.IsClosed || _handle.IsInvalid) throw new ObjectDisposedException(nameof(HipArray));
    }
}

internal interface IHipTextureResourceOwner
{
    public IHipNativeApi NativeApi { get; }
    public HipTextureResourceKind ResourceKind { get; }
    public IntPtr AcquireHandle(out bool addedReference);
    public void ReleaseHandle();
}

internal enum HipArrayReleaseKind
{
    FreeArray,
    DestroyArray,
    Borrowed,
}

internal sealed class HipArrayHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipArrayReleaseKind _releaseKind;
    private IDisposable? _parentLease;

    internal HipArrayHandle(IHipNativeApi nativeApi, IntPtr handle, HipArrayReleaseKind releaseKind, IDisposable? parentLease)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _releaseKind = releaseKind;
        _parentLease = parentLease;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal string ReleaseOperation => _releaseKind == HipArrayReleaseKind.DestroyArray ? "hipArrayDestroy" : "hipFreeArray";

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = ReleaseNative();
        if (error == HipError.Success)
        {
            SetHandleAsInvalid();
            IDisposable? lease = _parentLease;
            _parentLease = null;
            lease?.Dispose();
        }
        return error;
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            HipError error = ReleaseNative();
            if (error != HipError.Success) return false;
            IDisposable? lease = _parentLease;
            _parentLease = null;
            lease?.Dispose();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private HipError ReleaseNative() => _releaseKind switch
    {
        HipArrayReleaseKind.FreeArray => _nativeApi.FreeArray(handle),
        HipArrayReleaseKind.DestroyArray => _nativeApi.ArrayDestroy(handle),
        _ => HipError.Success,
    };
}

internal sealed class HipArrayLease : IDisposable
{
    private HipArray? _array;

    internal HipArrayLease(HipArray array) => _array = array;

    public void Dispose()
    {
        HipArray? array = _array;
        _array = null;
        array?.ReleaseHandle();
    }
}
