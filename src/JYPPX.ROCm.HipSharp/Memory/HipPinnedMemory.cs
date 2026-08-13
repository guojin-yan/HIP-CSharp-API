using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>
/// 拥有 HIP pinned host memory，适合异步复制 / Owns HIP pinned host memory for asynchronous copies.
/// </summary>
public sealed class HipPinnedMemory : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipPinnedMemoryHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipPinnedMemory(IHipNativeApi nativeApi, IntPtr pointer, ulong byteLength)
    {
        _nativeApi = nativeApi;
        _handle = new HipPinnedMemoryHandle(nativeApi, pointer);
        ByteLength = byteLength;
    }

    /// <summary>获取字节长度 / Gets the byte length.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取是否已释放 / Gets whether this memory is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>获取 pinned 指针；调用方不得释放 / Gets the pinned pointer; callers must not free it.</summary>
    public IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    /// <summary>从字节数组复制到 pinned memory / Copies a byte array into pinned memory.</summary>
    public void CopyFrom(byte[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ThrowIfDisposed();
        if ((ulong)source.LongLength > ByteLength) throw new ArgumentOutOfRangeException(nameof(source));
        if (source.Length != 0) Marshal.Copy(source, 0, DangerousGetHandle(), source.Length);
    }

    /// <summary>从 pinned memory 复制到字节数组 / Copies pinned memory into a byte array.</summary>
    public void CopyTo(byte[] destination)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ThrowIfDisposed();
        if ((ulong)destination.LongLength > ByteLength) throw new ArgumentOutOfRangeException(nameof(destination));
        if (destination.Length != 0) Marshal.Copy(DangerousGetHandle(), destination, 0, destination.Length);
    }

    /// <summary>释放 pinned memory；重复调用幂等 / Releases pinned memory; repeated calls are idempotent.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_asyncReferences != 0)
            {
                return;
            }
        }
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success) { _handle.Dispose(); return; }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipHostFree");
    }

    internal IHipNativeApi NativeApi => _nativeApi;
    internal IntPtr AcquireHandle(out bool addedReference)
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
    internal void ReleaseHandle()
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

        if (releaseChecked)
        {
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipHostFree");
            _handle.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipPinnedMemory));
    }
}
