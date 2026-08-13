using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>表示 owned custom 或 borrowed HIP memory pool view / Represents an owned custom or borrowed HIP memory-pool view.</summary>
public sealed class HipMemoryPool : IDisposable
{
    private readonly HipRuntime _runtime;
    private readonly IHipNativeApi _nativeApi;
    private readonly HipMemoryPoolHandle? _ownedHandle;
    private readonly IntPtr _borrowedHandle;
    private readonly object _lifetimeSync = new();
    private int _childCount;
    private int _currentUseCount;
    private bool _disposed;

    internal HipMemoryPool(HipRuntime runtime, IHipNativeApi nativeApi, IntPtr handle, int deviceOrdinal, bool ownsHandle)
    {
        _runtime = runtime;
        _nativeApi = nativeApi;
        DeviceOrdinal = deviceOrdinal;
        OwnsHandle = ownsHandle;
        if (ownsHandle) _ownedHandle = new HipMemoryPoolHandle(nativeApi, handle);
        else _borrowedHandle = handle;
    }

    /// <summary>获取 backing device 序号 / Gets the backing-device ordinal.</summary>
    public int DeviceOrdinal { get; }

    /// <summary>获取此 view 是否拥有并销毁原生 pool / Gets whether this view owns and destroys the native pool.</summary>
    public bool OwnsHandle { get; }

    /// <summary>获取此 managed view 是否已释放 / Gets whether this managed view is disposed.</summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lifetimeSync)
            {
                return _disposed || (_ownedHandle is not null && (_ownedHandle.IsClosed || _ownedHandle.IsInvalid));
            }
        }
    }

    /// <summary>获取或设置同步时尝试归还 OS 前保留的字节阈值 / Gets or sets the byte threshold retained before synchronization attempts to return memory to the OS.</summary>
    public ulong ReleaseThresholdBytes
    {
        get => GetUInt64Attribute(HipMemoryPoolAttributeNative.ReleaseThreshold);
        set => SetUInt64Attribute(HipMemoryPoolAttributeNative.ReleaseThreshold, value);
    }

    /// <summary>获取或设置是否复用具有 event dependency 的异步释放 / Gets or sets whether event-dependent asynchronous frees may be reused.</summary>
    public bool AllowEventDependencyReuse
    {
        get => GetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseFollowEventDependencies);
        set => SetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseFollowEventDependencies, value);
    }

    /// <summary>获取或设置是否机会式复用已完成的释放 / Gets or sets whether completed frees may be reused opportunistically.</summary>
    public bool AllowOpportunisticReuse
    {
        get => GetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseAllowOpportunistic);
        set => SetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseAllowOpportunistic, value);
    }

    /// <summary>获取或设置是否允许 allocator 插入内部 stream dependency / Gets or sets whether the allocator may insert internal stream dependencies.</summary>
    public bool AllowInternalDependencyReuse
    {
        get => GetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseAllowInternalDependencies);
        set => SetBooleanAttribute(HipMemoryPoolAttributeNative.ReuseAllowInternalDependencies, value);
    }

    /// <summary>读取当前和高水位字节统计 / Reads current and high-watermark byte statistics.</summary>
    public HipMemoryPoolStatistics GetStatistics() => new(
        GetUInt64Attribute(HipMemoryPoolAttributeNative.ReservedMemCurrent),
        GetUInt64Attribute(HipMemoryPoolAttributeNative.ReservedMemHigh),
        GetUInt64Attribute(HipMemoryPoolAttributeNative.UsedMemCurrent),
        GetUInt64Attribute(HipMemoryPoolAttributeNative.UsedMemHigh));

    /// <summary>将 reserved-memory 高水位重置为零 / Resets the reserved-memory high watermark to zero.</summary>
    public void ResetReservedHighWatermark() => SetUInt64Attribute(HipMemoryPoolAttributeNative.ReservedMemHigh, 0);

    /// <summary>将 used-memory 高水位重置为零 / Resets the used-memory high watermark to zero.</summary>
    public void ResetUsedHighWatermark() => SetUInt64Attribute(HipMemoryPoolAttributeNative.UsedMemHigh, 0);

    /// <summary>设置一个设备的 pool allocation 访问权限 / Sets one device's access to pool allocations.</summary>
    public void SetAccess(HipDevice device, HipMemoryPoolAccess access) => SetAccess(new HipMemoryPoolAccessDescriptor(device, access));

    /// <summary>批量设置设备的 pool allocation 访问权限 / Sets access to pool allocations for multiple devices.</summary>
    public void SetAccess(params HipMemoryPoolAccessDescriptor[] descriptors)
    {
        if (descriptors is null) throw new ArgumentNullException(nameof(descriptors));
        if (descriptors.Length == 0) throw new ArgumentException("At least one access descriptor is required.", nameof(descriptors));
        IntPtr handle = DangerousGetHandle();
        var nativeDescriptors = new HipMemoryPoolAccessDescriptorNative[descriptors.Length];
        for (int index = 0; index < descriptors.Length; index++)
        {
            HipMemoryPoolAccessDescriptor descriptor = descriptors[index];
            ValidateDevice(descriptor.Device, nameof(descriptors));
            if (descriptor.Access != HipMemoryPoolAccess.None && descriptor.Access != HipMemoryPoolAccess.ReadWrite)
                throw new ArgumentOutOfRangeException(nameof(descriptors), "Access flags are invalid.");
            nativeDescriptors[index] = new HipMemoryPoolAccessDescriptorNative(descriptor.Device.Ordinal, descriptor.Access);
        }
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolSetAccess(handle, nativeDescriptors), "hipMemPoolSetAccess");
    }

    /// <summary>读取一个设备的 pool allocation 访问权限 / Gets one device's access to pool allocations.</summary>
    public HipMemoryPoolAccess GetAccess(HipDevice device)
    {
        ValidateDevice(device, nameof(device));
        IntPtr handle = DangerousGetHandle();
        var location = new HipMemLocation(1, device.Ordinal);
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolGetAccess(out HipMemoryPoolAccess access, handle, ref location), "hipMemPoolGetAccess");
        if (access != HipMemoryPoolAccess.None && access != HipMemoryPoolAccess.ReadWrite)
            throw new InvalidOperationException("hipMemPoolGetAccess returned unsupported access flags.");
        return access;
    }

    /// <summary>在指定 stream 从此 pool 异步分配内存 / Asynchronously allocates memory from this pool on a stream.</summary>
    /// <param name="byteCount">字节数，必须大于零 / Byte count, which must be greater than zero.</param>
    /// <param name="stream">分配和释放顺序绑定的 stream / Stream that orders allocation and release.</param>
    public HipPooledDeviceMemory AllocateAsync(ulong byteCount, HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (stream.IsDisposed) throw new ObjectDisposedException(nameof(stream));
        UIntPtr nativeByteCount = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
        IntPtr poolHandle = DangerousGetHandle();
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Stream belongs to a different HIP Runtime client.", nameof(stream));
        if (stream.DeviceOrdinal != DeviceOrdinal) throw new ArgumentException("Stream was created on a different device.", nameof(stream));
        if (stream.IsCapturing) throw new InvalidOperationException("Pool allocations are not supported during graph capture.");

        IDisposable streamOwner = stream.RegisterOwnedResource();
        IDisposable poolChild = RegisterChild();
        bool transferred = false;
        try
        {
            IntPtr streamHandle = stream.DangerousGetHandle();
            HipError error = _nativeApi.MallocFromPoolAsync(out IntPtr pointer, nativeByteCount, poolHandle, streamHandle);
            if (pointer != IntPtr.Zero)
            {
                var partial = new HipPooledDeviceMemoryHandle(_nativeApi, pointer, stream, streamOwner, poolChild);
                transferred = true;
                if (error == HipError.Success)
                    return new HipPooledDeviceMemory(_nativeApi, partial, byteCount, stream, this);
                if (partial.ReleaseAsyncChecked() == HipError.Success) partial.Dispose();
            }
            HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocFromPoolAsync");
            throw new InvalidOperationException("hipMallocFromPoolAsync succeeded but returned a null pointer.");
        }
        finally
        {
            if (!transferred)
            {
                poolChild.Dispose();
                streamOwner.Dispose();
            }
        }
    }

    /// <summary>请求 pool 至少保留指定字节数；此调用不执行 stream 同步 / Requests that the pool retain at least the specified bytes; this call does not synchronize streams.</summary>
    public void TrimTo(ulong minimumBytesToKeep)
    {
        UIntPtr minimum = HipDeviceMemory.ToUIntPtr(minimumBytesToKeep, nameof(minimumBytesToKeep));
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolTrimTo(DangerousGetHandle(), minimum), "hipMemPoolTrimTo");
    }

    /// <summary>将 custom pool 在其 backing device 上设为 current，并在 scope 结束时恢复 previous pool / Makes the custom pool current on its backing device and restores the previous pool when the scope ends.</summary>
    public HipMemoryPoolCurrentScope UseAsCurrent()
    {
        if (!OwnsHandle) throw new InvalidOperationException("Only an owned custom memory pool can be selected as current through this managed scope.");
        DangerousGetHandle();
        return _runtime.BeginMemoryPoolCurrentScope(this);
    }

    /// <summary>释放 view；owned pool 有 child 或仍为 current 时拒绝销毁 / Disposes the view; an owned pool refuses destruction while it has children or remains current.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_disposed || (_ownedHandle is not null && (_ownedHandle.IsClosed || _ownedHandle.IsInvalid))) return;
            if (!OwnsHandle)
            {
                _disposed = true;
                return;
            }
            if (_childCount != 0) throw new InvalidOperationException("Dispose all pool allocations and complete their stream-ordered frees before disposing the pool.");
            if (_currentUseCount != 0) throw new InvalidOperationException("Dispose the current-pool scope before disposing the pool.");
            HipError error = _ownedHandle!.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipMemPoolDestroy");
            _ownedHandle.Dispose();
            _disposed = true;
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal IntPtr DangerousGetHandle()
    {
        lock (_lifetimeSync)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HipMemoryPool));
            IntPtr handle = _ownedHandle?.DangerousGetHandle() ?? _borrowedHandle;
            if (handle == IntPtr.Zero) throw new ObjectDisposedException(nameof(HipMemoryPool));
            return handle;
        }
    }

    internal void AcquireCurrentUse()
    {
        lock (_lifetimeSync)
        {
            if (!OwnsHandle) throw new InvalidOperationException("A borrowed pool cannot own current-pool lifetime.");
            DangerousGetHandle();
            _currentUseCount++;
        }
    }

    internal void ReleaseCurrentUse()
    {
        lock (_lifetimeSync)
        {
            if (_currentUseCount > 0) _currentUseCount--;
        }
    }

    private ChildLease RegisterChild()
    {
        lock (_lifetimeSync)
        {
            DangerousGetHandle();
            _childCount++;
            return new ChildLease(this);
        }
    }

    private void ReleaseChild()
    {
        lock (_lifetimeSync)
        {
            if (_childCount > 0) _childCount--;
        }
    }

    private void ValidateDevice(HipDevice device, string parameterName)
    {
        if (device is null) throw new ArgumentNullException(parameterName);
        if (!ReferenceEquals(_nativeApi, device.NativeApi)) throw new ArgumentException("Device belongs to a different HIP Runtime client.", parameterName);
    }

    private unsafe bool GetBooleanAttribute(HipMemoryPoolAttributeNative attribute)
    {
        int value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolGetAttribute(DangerousGetHandle(), attribute, (IntPtr)(&value)), "hipMemPoolGetAttribute");
        if (value != 0 && value != 1) throw new InvalidOperationException("hipMemPoolGetAttribute returned an invalid boolean value.");
        return value != 0;
    }

    private unsafe void SetBooleanAttribute(HipMemoryPoolAttributeNative attribute, bool value)
    {
        int nativeValue = value ? 1 : 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolSetAttribute(DangerousGetHandle(), attribute, (IntPtr)(&nativeValue)), "hipMemPoolSetAttribute");
    }

    private unsafe ulong GetUInt64Attribute(HipMemoryPoolAttributeNative attribute)
    {
        ulong value = 0;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolGetAttribute(DangerousGetHandle(), attribute, (IntPtr)(&value)), "hipMemPoolGetAttribute");
        return value;
    }

    private unsafe void SetUInt64Attribute(HipMemoryPoolAttributeNative attribute, ulong value)
    {
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPoolSetAttribute(DangerousGetHandle(), attribute, (IntPtr)(&value)), "hipMemPoolSetAttribute");
    }

    private sealed class ChildLease : IDisposable
    {
        private HipMemoryPool? _pool;
        internal ChildLease(HipMemoryPool pool) => _pool = pool;
        public void Dispose() => System.Threading.Interlocked.Exchange(ref _pool, null)?.ReleaseChild();
    }
}
