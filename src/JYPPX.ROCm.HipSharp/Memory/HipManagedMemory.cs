using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>
/// 拥有 CPU/GPU 共享地址的 HIP managed memory；调用方负责在 host/device 访问之间同步 / Owns HIP managed memory with a shared CPU/GPU address; callers synchronize host and device access.
/// </summary>
public sealed class HipManagedMemory : IDisposable, IHipPointerOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipDeviceMemoryHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipManagedMemory(IHipNativeApi nativeApi, IntPtr pointer, ulong byteLength, HipManagedMemoryFlags flags)
    {
        _nativeApi = nativeApi;
        _handle = new HipDeviceMemoryHandle(nativeApi, pointer);
        ByteLength = byteLength;
        Flags = flags;
    }

    /// <summary>获取字节数 / Gets the byte length.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取初始可见性标志 / Gets initial visibility flags.</summary>
    public HipManagedMemoryFlags Flags { get; }

    /// <summary>获取资源是否已释放 / Gets whether the allocation is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>获取 host/device 共享指针但不转移所有权 / Gets the shared host/device pointer without transferring ownership.</summary>
    public IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    /// <summary>从 host 数组写入 managed memory；调用前必须完成相关 GPU 工作 / Writes a host array to managed memory; related GPU work must complete first.</summary>
    public void CopyFromHost(byte[] source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ValidateCount((ulong)source.LongLength, nameof(source));
        if (source.Length != 0) WithBorrowedPointer(pointer => Marshal.Copy(source, 0, pointer, source.Length));
    }

    /// <summary>读取 managed memory 到 host 数组；调用前必须完成相关 GPU 工作 / Reads managed memory to a host array; related GPU work must complete first.</summary>
    public void CopyToHost(byte[] destination)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        ValidateCount((ulong)destination.LongLength, nameof(destination));
        if (destination.Length != 0) WithBorrowedPointer(pointer => Marshal.Copy(pointer, destination, 0, destination.Length));
    }

    /// <summary>
    /// 设置使用提示；提示不执行同步也不保证迁移或性能 / Sets a usage hint; advice neither synchronizes nor guarantees migration or performance.
    /// </summary>
    public void Advise(HipMemoryAdvise advice, int device, ulong byteCount = 0)
    {
        int adviceValue = (int)advice;
        if ((adviceValue < 1 || adviceValue > 6) && adviceValue != 100 && adviceValue != 101)
        {
            throw new ArgumentOutOfRangeException(nameof(advice));
        }
        if (device < -1) throw new ArgumentOutOfRangeException(nameof(device));
        ulong count = byteCount == 0 ? ByteLength : byteCount;
        ValidateCount(count, nameof(byteCount));
        WithBorrowedPointer(pointer =>
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemAdvise(pointer, HipDeviceMemory.ToUIntPtr(count, nameof(byteCount)), advice, device), "hipMemAdvise"));
    }

    /// <summary>
    /// 把预取提示排入 stream；stream 完成前 owner 保持指针有效 / Enqueues a prefetch hint and retains the pointer until stream completion.
    /// </summary>
    public void PrefetchAsync(int device, HipStream stream, ulong byteCount = 0)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (device < -1) throw new ArgumentOutOfRangeException(nameof(device));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Managed memory and stream belong to different HIP Runtime clients.", nameof(stream));
        ulong count = byteCount == 0 ? ByteLength : byteCount;
        ValidateCount(count, nameof(byteCount));
        bool addedReference = false;
        try
        {
            IntPtr pointer = AcquirePointer(out addedReference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.MemPrefetchAsync(pointer, HipDeviceMemory.ToUIntPtr(count, nameof(byteCount)), device, stream.DangerousGetHandle()), "hipMemPrefetchAsync");
            stream.AddPendingLease(new HipAsyncLease(ReleasePointer));
            addedReference = false;
        }
        finally
        {
            if (addedReference) ReleasePointer();
        }
    }

    /// <summary>释放 managed memory；重复调用安全 / Disposes managed memory; repeated calls are safe.</summary>
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
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipFree(managed)");
        _handle.Dispose();
    }

    internal IHipNativeApi NativeApi => _nativeApi;

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

        if (releaseChecked)
        {
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipFree(managed)");
            _handle.Dispose();
        }
    }

    IHipNativeApi IHipPointerOwner.NativeApi => _nativeApi;
    int? IHipPointerOwner.DeviceOrdinal => null;
    HipStream? IHipPointerOwner.RequiredStream => null;
    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => AcquirePointer(out addedReference);
    void IHipPointerOwner.ReleasePointer() => ReleasePointer();

    private void WithBorrowedPointer(Action<IntPtr> action)
    {
        bool addedReference = false;
        try
        {
            IntPtr pointer = AcquirePointer(out addedReference);
            action(pointer);
        }
        finally
        {
            if (addedReference) ReleasePointer();
        }
    }

    private void ValidateCount(ulong count, string name)
    {
        ThrowIfDisposed();
        if (count > ByteLength) throw new ArgumentOutOfRangeException(name, "The operation exceeds the allocation capacity.");
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipManagedMemory));
    }
}
