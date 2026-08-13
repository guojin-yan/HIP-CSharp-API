using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>收敛 stream-ordered allocation owner 的 copy 和 pointer lease / Centralizes copy and pointer leases for stream-ordered allocation owners.</summary>
internal sealed class HipStreamOrderedMemoryCore
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStream _allocationStream;
    private readonly SafeHandle _handle;
    private readonly Func<HipError> _releaseChecked;
    private readonly string _objectName;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipStreamOrderedMemoryCore(IHipNativeApi nativeApi, SafeHandle handle, Func<HipError> releaseChecked, ulong byteLength, HipStream allocationStream, string objectName)
    {
        _nativeApi = nativeApi;
        _handle = handle;
        _releaseChecked = releaseChecked;
        ByteLength = byteLength;
        _allocationStream = allocationStream;
        _objectName = objectName;
    }

    internal ulong ByteLength { get; }
    internal HipStream AllocationStream => _allocationStream;
    internal bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    internal IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    internal void CopyFromAsync(byte[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ValidateCount((ulong)source.LongLength);
        if (source.Length == 0) return;
        CopyManagedArray(source, source.LongLength, HipMemoryCopyKind.HostToDevice, true);
    }

    internal void CopyToAsync(byte[] destination)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ValidateCount((ulong)destination.LongLength);
        if (destination.Length == 0) return;
        CopyManagedArray(destination, destination.LongLength, HipMemoryCopyKind.DeviceToHost, false);
    }

    internal void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_asyncReferences != 0) return;
        }

        ReleaseChecked();
    }

    internal IntPtr AcquirePointer(out bool addedReference)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            addedReference = false;
            _handle.DangerousAddRef(ref addedReference);
            if (addedReference) _asyncReferences++;
            return _handle.DangerousGetHandle();
        }
    }

    internal void ReleasePointer()
    {
        bool releaseChecked;
        lock (_lifetimeSync)
        {
            if (_asyncReferences > 0)
            {
                _handle.DangerousRelease();
                _asyncReferences--;
            }
            releaseChecked = _disposeRequested && _asyncReferences == 0;
        }
        if (releaseChecked) ReleaseChecked();
    }

    private void CopyManagedArray(byte[] array, long length, HipMemoryCopyKind kind, bool hostToDevice)
    {
        GCHandle pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
        bool addedReference = false;
        bool transferred = false;
        try
        {
            IntPtr pointer = AcquirePointer(out addedReference);
            IntPtr host = pinned.AddrOfPinnedObject();
            IntPtr destination = hostToDevice ? pointer : host;
            IntPtr source = hostToDevice ? host : pointer;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemcpyAsync(destination, source, HipDeviceMemory.ToUIntPtr((ulong)length, nameof(array)), kind, _allocationStream.DangerousGetHandle()), "hipMemcpyAsync");
            _allocationStream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (addedReference)
                {
                    ReleasePointer();
                    addedReference = false;
                }
                if (pinned.IsAllocated) pinned.Free();
            }));
            transferred = true;
        }
        finally
        {
            if (!transferred && addedReference) ReleasePointer();
            if (!transferred && pinned.IsAllocated) pinned.Free();
        }
    }

    private void ReleaseChecked()
    {
        HipError error = _releaseChecked();
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipFreeAsync");
        _handle.Dispose();
    }

    private void ValidateCount(ulong count)
    {
        ThrowIfDisposed();
        if (count > ByteLength) throw new ArgumentOutOfRangeException(nameof(count), "The operation exceeds the allocation capacity.");
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(_objectName);
    }
}
