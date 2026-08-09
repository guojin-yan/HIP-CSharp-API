using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 拥有一段 HIP 设备内存并提供同步复制操作 / Owns a HIP device-memory allocation and provides synchronous copy operations.
/// </summary>
public sealed class HipDeviceMemory : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipDeviceMemoryHandle _handle;

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
    /// 释放设备内存；重复调用不会重复释放 / Releases the device memory; repeated calls do not free it twice.
    /// </summary>
    /// <exception cref="HipException">HIP 无法释放内存；此时可重试释放 / HIP cannot free the memory; disposal can be retried.</exception>
    public void Dispose()
    {
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }

        HipCall.ThrowIfFailed(_nativeApi, error, "hipFree");
    }

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
}
