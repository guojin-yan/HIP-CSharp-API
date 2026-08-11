using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 拥有一段 HIP 设备内存并提供同步复制操作 / Owns a HIP device-memory allocation and provides synchronous copy operations.
/// </summary>
public sealed class HipDeviceMemory : IDisposable, IHipPointerOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipDeviceMemoryHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;

    internal HipDeviceMemory(IHipNativeApi nativeApi, IntPtr pointer, ulong byteLength)
    {
        _nativeApi = nativeApi;
        _handle = new HipDeviceMemoryHandle(nativeApi, pointer);
        ByteLength = byteLength;
    }

    /// <summary>获取分配的字节数 / Gets the allocation size in bytes.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取资源是否已经释放 / Gets whether the resource has been released.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>
    /// 获取原生设备指针；调用方不得释放它 / Gets the native device pointer; the caller must not free it.
    /// </summary>
    /// <returns>原生设备指针 / The native device pointer.</returns>
    /// <exception cref="ObjectDisposedException">内存已经释放 / The memory has been released.</exception>
    public IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    /// <summary>
    /// 从托管字节数组同步复制到设备 / Copies synchronously from a managed byte array to the device.
    /// </summary>
    /// <param name="source">源字节数组 / Source byte array.</param>
    /// <exception cref="ArgumentNullException">源数组为 null / The source array is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">源数组大于设备分配 / The source array is larger than the device allocation.</exception>
    /// <exception cref="ObjectDisposedException">设备内存已经释放 / The device memory has been released.</exception>
    /// <exception cref="HipException">HIP 复制失败 / The HIP copy fails.</exception>
    public void CopyFrom(byte[] source)
    {
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        ThrowIfDisposed();
        ValidateByteCount((ulong)source.LongLength, ByteLength, nameof(source));
        if (source.Length == 0)
        {
            return;
        }

        GCHandle pinned = GCHandle.Alloc(source, GCHandleType.Pinned);
        try
        {
            Copy(DangerousGetHandle(), pinned.AddrOfPinnedObject(), (ulong)source.LongLength, HipMemoryCopyKind.HostToDevice, "hipMemcpy(host-to-device)");
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// 从设备同步复制到托管字节数组 / Copies synchronously from the device to a managed byte array.
    /// </summary>
    /// <param name="destination">目标字节数组 / Destination byte array.</param>
    /// <exception cref="ArgumentNullException">目标数组为 null / The destination array is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">目标数组大于设备分配 / The destination array is larger than the device allocation.</exception>
    /// <exception cref="ObjectDisposedException">设备内存已经释放 / The device memory has been released.</exception>
    /// <exception cref="HipException">HIP 复制失败 / The HIP copy fails.</exception>
    public void CopyTo(byte[] destination)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        ThrowIfDisposed();
        ValidateByteCount((ulong)destination.LongLength, ByteLength, nameof(destination));
        if (destination.Length == 0)
        {
            return;
        }

        GCHandle pinned = GCHandle.Alloc(destination, GCHandleType.Pinned);
        try
        {
            Copy(pinned.AddrOfPinnedObject(), DangerousGetHandle(), (ulong)destination.LongLength, HipMemoryCopyKind.DeviceToHost, "hipMemcpy(device-to-host)");
        }
        finally
        {
            pinned.Free();
        }
    }

    /// <summary>
    /// 将指定字节数同步复制到另一段设备内存 / Copies a number of bytes synchronously to another device allocation.
    /// </summary>
    /// <param name="destination">目标设备内存 / Destination device memory.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <exception cref="ArgumentNullException">目标设备内存为 null / The destination device memory is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">两个分配不属于同一 runtime 客户端 / The allocations do not belong to the same runtime client.</exception>
    /// <exception cref="ArgumentOutOfRangeException">复制字节数超过任一分配 / The byte count exceeds either allocation.</exception>
    /// <exception cref="ObjectDisposedException">任一设备内存已经释放 / Either device allocation has been released.</exception>
    /// <exception cref="HipException">HIP 复制失败 / The HIP copy fails.</exception>
    public void CopyTo(HipDeviceMemory destination, ulong byteCount)
    {
        if (destination is null)
        {
            throw new ArgumentNullException(nameof(destination));
        }

        ThrowIfDisposed();
        destination.ThrowIfDisposed();
        if (!ReferenceEquals(_nativeApi, destination._nativeApi))
        {
            throw new ArgumentException("Device allocations belong to different HIP Runtime clients.", nameof(destination));
        }

        ValidateByteCount(byteCount, ByteLength, nameof(byteCount));
        ValidateByteCount(byteCount, destination.ByteLength, nameof(byteCount));
        if (byteCount != 0)
        {
            Copy(destination.DangerousGetHandle(), DangerousGetHandle(), byteCount, HipMemoryCopyKind.DeviceToDevice, "hipMemcpy(device-to-device)");
        }
    }

    /// <summary>
    /// 在指定 stream 异步复制托管数组到设备，并由 stream 保持数组和设备指针有效 / Asynchronously copies a managed array to the device while the stream retains the array and device pointer.
    /// </summary>
    public void CopyFromAsync(byte[] source, HipStream stream)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        QueueAsync(source, stream, true, (ulong)source.LongLength);
    }

    /// <summary>
    /// 在指定 stream 异步复制设备数据到托管数组 / Asynchronously copies device data to a managed array on a stream.
    /// </summary>
    public void CopyToAsync(byte[] destination, HipStream stream)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        QueueAsync(destination, stream, false, (ulong)destination.LongLength);
    }

    /// <summary>使用 pinned owner 异步复制到设备 / Asynchronously copies from an owned pinned buffer.</summary>
    public void CopyFromAsync(HipPinnedMemory source, HipStream stream, ulong byteCount = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        QueuePinnedAsync(source, stream, true, byteCount == 0 ? source.ByteLength : byteCount);
    }

    /// <summary>使用 pinned owner 异步复制到主机 / Asynchronously copies to an owned pinned buffer.</summary>
    public void CopyToAsync(HipPinnedMemory destination, HipStream stream, ulong byteCount = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        QueuePinnedAsync(destination, stream, false, byteCount == 0 ? destination.ByteLength : byteCount);
    }

    /// <summary>
    /// 释放设备内存；重复调用不会重复释放 / Releases the device memory; repeated calls do not free it twice.
    /// </summary>
    /// <exception cref="HipException">HIP 无法释放内存；此时可重试释放 / HIP cannot free the memory; disposal can be retried.</exception>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_asyncReferences != 0)
            {
                _handle.Dispose();
                return;
            }
        }
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }

        HipCall.ThrowIfFailed(_nativeApi, error, "hipFree");
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    IHipNativeApi IHipPointerOwner.NativeApi => _nativeApi;
    HipStream? IHipPointerOwner.RequiredStream => null;

    internal IntPtr DangerousAcquireHandle(out bool addedReference)
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

    internal void DangerousReleaseHandle()
    {
        lock (_lifetimeSync)
        {
            _handle.DangerousRelease();
            if (_asyncReferences > 0) _asyncReferences--;
        }
    }

    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => DangerousAcquireHandle(out addedReference);
    void IHipPointerOwner.ReleasePointer() => DangerousReleaseHandle();

    internal static UIntPtr ToUIntPtr(ulong value, string parameterName)
    {
        if (UIntPtr.Size == 4 && value > uint.MaxValue)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The byte count exceeds the current process address size.");
        }

        return UIntPtr.Size == 4 ? new UIntPtr((uint)value) : new UIntPtr(value);
    }

    private void Copy(IntPtr destination, IntPtr source, ulong byteCount, HipMemoryCopyKind kind, string operation) =>
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.Memcpy(destination, source, ToUIntPtr(byteCount, nameof(byteCount)), kind), operation);

    private static void ValidateByteCount(ulong byteCount, ulong capacity, string parameterName)
    {
        if (byteCount > capacity)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The copy exceeds the allocation capacity.");
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed)
        {
            throw new ObjectDisposedException(nameof(HipDeviceMemory));
        }
    }

    private void QueueAsync(byte[] host, HipStream stream, bool hostToDevice, ulong byteCount)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        ThrowIfDisposed();
        ValidateByteCount(byteCount, ByteLength, nameof(host));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Memory and stream belong to different HIP Runtime clients.", nameof(stream));
        if (byteCount == 0) return;
        GCHandle pinned = GCHandle.Alloc(host, GCHandleType.Pinned);
        bool deviceReference = false;
        try
        {
            IntPtr devicePointer = DangerousAcquireHandle(out deviceReference);
            HipError error = _nativeApi.MemcpyAsync(
                hostToDevice ? devicePointer : pinned.AddrOfPinnedObject(),
                hostToDevice ? pinned.AddrOfPinnedObject() : devicePointer,
                ToUIntPtr(byteCount, nameof(byteCount)),
                hostToDevice ? HipMemoryCopyKind.HostToDevice : HipMemoryCopyKind.DeviceToHost,
                stream.DangerousGetHandle());
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipMemcpyAsync");
            stream.AddPendingLease(new HipAsyncLease(() =>
            {
                pinned.Free();
                if (deviceReference) DangerousReleaseHandle();
            }));
        }
        catch
        {
            if (deviceReference) DangerousReleaseHandle();
            if (pinned.IsAllocated) pinned.Free();
            throw;
        }
    }

    private void QueuePinnedAsync(HipPinnedMemory host, HipStream stream, bool hostToDevice, ulong byteCount)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        ThrowIfDisposed();
        if (!ReferenceEquals(_nativeApi, stream.NativeApi) || !ReferenceEquals(_nativeApi, host.NativeApi)) throw new ArgumentException("Memory, pinned buffer, and stream must belong to one HIP Runtime client.", nameof(stream));
        ValidateByteCount(byteCount, ByteLength, nameof(byteCount));
        ValidateByteCount(byteCount, host.ByteLength, nameof(byteCount));
        if (byteCount == 0) return;
        bool deviceReference = false;
        bool hostReference = false;
        try
        {
            IntPtr devicePointer = DangerousAcquireHandle(out deviceReference);
            IntPtr hostPointer = host.AcquireHandle(out hostReference);
            HipError error = _nativeApi.MemcpyAsync(
                hostToDevice ? devicePointer : hostPointer,
                hostToDevice ? hostPointer : devicePointer,
                ToUIntPtr(byteCount, nameof(byteCount)),
                hostToDevice ? HipMemoryCopyKind.HostToDevice : HipMemoryCopyKind.DeviceToHost,
                stream.DangerousGetHandle());
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipMemcpyAsync");
            stream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (hostReference) host.ReleaseHandle();
                if (deviceReference) DangerousReleaseHandle();
            }));
        }
        catch
        {
            if (hostReference) host.ReleaseHandle();
            if (deviceReference) DangerousReleaseHandle();
            throw;
        }
    }
}
