using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 拥有按指定 stream 分配的设备内存；释放严格排入同一 stream / Owns device memory allocated on a specific stream; release is ordered on that same stream.
/// </summary>
public sealed class HipAsyncDeviceMemory : IDisposable, IHipPointerOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStream _allocationStream;
    private readonly HipAsyncDeviceMemoryHandle _handle;

    internal HipAsyncDeviceMemory(IHipNativeApi nativeApi, IntPtr pointer, ulong byteLength, HipStream allocationStream, IDisposable ownerLease)
    {
        _nativeApi = nativeApi;
        _allocationStream = allocationStream;
        _handle = new HipAsyncDeviceMemoryHandle(nativeApi, pointer, allocationStream.DangerousGetHandle(), ownerLease);
        ByteLength = byteLength;
    }

    /// <summary>获取分配字节数 / Gets the allocation size in bytes.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取创建和释放顺序所绑定的 stream / Gets the stream that owns allocation and release ordering.</summary>
    public HipStream AllocationStream => _allocationStream;

    /// <summary>获取资源是否已释放 / Gets whether the resource is disposed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取原生指针但不转移所有权 / Gets the native pointer without transferring ownership.</summary>
    public IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    /// <summary>在创建 stream 上异步复制到设备 / Asynchronously copies to the device on the allocation stream.</summary>
    public void CopyFromAsync(byte[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ValidateCount((ulong)source.LongLength);
        if (source.Length == 0) return;
        GCHandle pinned = GCHandle.Alloc(source, GCHandleType.Pinned);
        bool addedReference = false;
        bool transferred = false;
        try
        {
            _handle.DangerousAddRef(ref addedReference);
            if (!addedReference) throw new ObjectDisposedException(nameof(HipAsyncDeviceMemory));
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyAsync(_handle.DangerousGetHandle(), pinned.AddrOfPinnedObject(), HipDeviceMemory.ToUIntPtr((ulong)source.LongLength, nameof(source)), HipMemoryCopyKind.HostToDevice, _allocationStream.DangerousGetHandle()), "hipMemcpyAsync");
            bool referenceTransferred = addedReference;
            _allocationStream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (referenceTransferred) _handle.DangerousRelease();
                pinned.Free();
            }));
            addedReference = false;
            transferred = true;
        }
        finally
        {
            if (addedReference) _handle.DangerousRelease();
            if (!transferred && pinned.IsAllocated) pinned.Free();
        }
    }

    /// <summary>在创建 stream 上异步复制回主机 / Asynchronously copies to the host on the allocation stream.</summary>
    public void CopyToAsync(byte[] destination)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ValidateCount((ulong)destination.LongLength);
        if (destination.Length == 0) return;
        GCHandle pinned = GCHandle.Alloc(destination, GCHandleType.Pinned);
        bool addedReference = false;
        bool transferred = false;
        try
        {
            _handle.DangerousAddRef(ref addedReference);
            if (!addedReference) throw new ObjectDisposedException(nameof(HipAsyncDeviceMemory));
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyAsync(pinned.AddrOfPinnedObject(), _handle.DangerousGetHandle(), HipDeviceMemory.ToUIntPtr((ulong)destination.LongLength, nameof(destination)), HipMemoryCopyKind.DeviceToHost, _allocationStream.DangerousGetHandle()), "hipMemcpyAsync");
            bool referenceTransferred = addedReference;
            _allocationStream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (referenceTransferred) _handle.DangerousRelease();
                pinned.Free();
            }));
            addedReference = false;
            transferred = true;
        }
        finally
        {
            if (addedReference) _handle.DangerousRelease();
            if (!transferred && pinned.IsAllocated) pinned.Free();
        }
    }

    /// <summary>
    /// 将释放操作排入创建 stream；失败时保留 owner 以便重试 / Enqueues release on the allocation stream; failures preserve ownership for retry.
    /// </summary>
    public void Dispose()
    {
        HipError error = _handle.ReleaseAsyncChecked();
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipFreeAsync");
        _handle.Dispose();
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal IntPtr AcquirePointer(out bool addedReference)
    {
        ThrowIfDisposed();
        addedReference = false;
        _handle.DangerousAddRef(ref addedReference);
        return _handle.DangerousGetHandle();
    }

    internal void ReleasePointer() => _handle.DangerousRelease();

    IHipNativeApi IHipPointerOwner.NativeApi => _nativeApi;
    HipStream? IHipPointerOwner.RequiredStream => _allocationStream;
    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => AcquirePointer(out addedReference);
    void IHipPointerOwner.ReleasePointer() => ReleasePointer();

    private void ValidateCount(ulong count)
    {
        ThrowIfDisposed();
        if (count > ByteLength) throw new ArgumentOutOfRangeException(nameof(count), "The operation exceeds the allocation capacity.");
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipAsyncDeviceMemory));
    }
}
