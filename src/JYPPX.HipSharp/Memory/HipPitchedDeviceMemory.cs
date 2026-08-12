using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 独占一段二维或三维 pitched device memory，并以元素坐标提供类型安全操作 / Exclusively owns a two- or three-dimensional pitched device allocation and provides type-safe element-coordinate operations.
/// </summary>
/// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
public sealed unsafe class HipPitchedDeviceMemory<T> : IDisposable, IHipPointerOwner where T : unmanaged
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipDeviceMemoryHandle _handle;
    private readonly ulong _nativeXSizeBytes;
    private readonly ulong _nativeYSize;
    private readonly object _lifetimeSync = new();
    private int _references;
    private bool _disposeRequested;

    internal HipPitchedDeviceMemory(
        IHipNativeApi nativeApi,
        IntPtr pointer,
        HipMemoryExtent extent,
        ulong pitchBytes,
        ulong nativeXSizeBytes,
        ulong nativeYSize,
        int deviceOrdinal)
    {
        ulong slicePitchBytes = CheckedMultiply(pitchBytes, nativeYSize, nameof(extent));
        ulong byteLength = CheckedMultiply(slicePitchBytes, extent.Depth, nameof(extent));

        _nativeApi = nativeApi;
        Extent = extent;
        PitchBytes = pitchBytes;
        _nativeXSizeBytes = nativeXSizeBytes;
        _nativeYSize = nativeYSize;
        SlicePitchBytes = slicePitchBytes;
        ByteLength = byteLength;
        DeviceOrdinal = deviceOrdinal;
        _handle = new HipDeviceMemoryHandle(nativeApi, pointer);
    }

    /// <summary>获取逻辑范围，所有维度均以 <typeparamref name="T"/> 元素为单位 / Gets the logical extent with every dimension measured in <typeparamref name="T"/> elements.</summary>
    public HipMemoryExtent Extent { get; }

    /// <summary>获取逻辑元素宽度 / Gets the logical width in elements.</summary>
    public ulong Width => Extent.Width;

    /// <summary>获取逻辑元素高度 / Gets the logical height in elements.</summary>
    public ulong Height => Extent.Height;

    /// <summary>获取逻辑元素深度 / Gets the logical depth in elements.</summary>
    public ulong Depth => Extent.Depth;

    /// <summary>获取每行的物理跨度，单位为字节 / Gets the physical row pitch in bytes.</summary>
    public ulong PitchBytes { get; }

    /// <summary>获取每个 Z slice 的物理跨度，单位为字节 / Gets the physical Z-slice pitch in bytes.</summary>
    public ulong SlicePitchBytes { get; }

    /// <summary>获取包含 padding 的物理分配长度，单位为字节 / Gets the physical allocation length including padding, in bytes.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取元素字节数 / Gets the element size in bytes.</summary>
    public int ElementSize => sizeof(T);

    /// <summary>获取创建分配的设备序号 / Gets the ordinal of the device on which the allocation was created.</summary>
    public int DeviceOrdinal { get; }

    /// <summary>获取资源是否已请求或完成释放 / Gets whether release has been requested or completed.</summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lifetimeSync)
            {
                return _disposeRequested || _handle.IsClosed || _handle.IsInvalid;
            }
        }
    }

    /// <summary>
    /// 获取 borrowed 原生指针；调用方不得释放或越过 owner 生命周期保存它 / Gets the borrowed native pointer; the caller must not free it or retain it beyond the owner lifetime.
    /// </summary>
    /// <exception cref="ObjectDisposedException">内存已经释放 / The memory has been disposed.</exception>
    public IntPtr DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetHandle();
    }

    /// <summary>将整个逻辑范围的每个字节同步设为零 / Synchronously sets every byte in the logical extent to zero.</summary>
    public void SetZero() => SetByte(0, WholeRegion);

    /// <summary>将指定元素区域的每个字节同步设为零 / Synchronously sets every byte in an element region to zero.</summary>
    /// <param name="region">元素区域 / Region in elements.</param>
    public void SetZero(HipMemoryRegion region) => SetByte(0, region);

    /// <summary>在 stream 上将整个逻辑范围的每个字节异步设为零；owner 保活至 stream 完成 / Asynchronously sets every byte in the logical extent to zero and retains the owner until stream completion.</summary>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void SetZeroAsync(HipStream stream) => SetByteAsync(0, WholeRegion, stream);

    /// <summary>在 stream 上将指定元素区域的每个字节异步设为零；owner 保活至 stream 完成 / Asynchronously sets every byte in an element region to zero and retains the owner until stream completion.</summary>
    /// <param name="region">元素区域 / Region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void SetZeroAsync(HipMemoryRegion region, HipStream stream) => SetByteAsync(0, region, stream);

    /// <summary>
    /// 将整个逻辑范围的每个字节同步设为同一 8-bit pattern；这不是按 <typeparamref name="T"/> 值填充 / Synchronously sets every byte in the logical extent to one 8-bit pattern; this does not fill by <typeparamref name="T"/> value.
    /// </summary>
    /// <param name="value">8-bit byte pattern / 8-bit byte pattern.</param>
    public void SetByte(byte value) => SetByte(value, WholeRegion);

    /// <summary>
    /// 将元素区域的每个字节同步设为同一 8-bit pattern；这不是按 <typeparamref name="T"/> 值填充 / Synchronously sets every byte in an element region to one 8-bit pattern; this does not fill by <typeparamref name="T"/> value.
    /// </summary>
    /// <param name="value">8-bit byte pattern / 8-bit byte pattern.</param>
    /// <param name="region">元素区域 / Region in elements.</param>
    public void SetByte(byte value, HipMemoryRegion region)
    {
        ValidateReady();
        ValidateRegion(region, nameof(region));
        bool reference = false;
        try
        {
            IntPtr pointer = AcquirePointer(out reference);
            HipCall.ThrowIfFailed(_nativeApi, InvokeMemset(pointer, value, region, IntPtr.Zero, false), MemsetOperation(region, false));
        }
        finally
        {
            if (reference) ReleasePointer();
        }
    }

    /// <summary>
    /// 在 stream 上将整个逻辑范围的每个字节异步设为同一 8-bit pattern，并保活 owner / Asynchronously sets every byte in the logical extent to one 8-bit pattern and retains the owner.
    /// </summary>
    /// <param name="value">8-bit byte pattern / 8-bit byte pattern.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void SetByteAsync(byte value, HipStream stream) => SetByteAsync(value, WholeRegion, stream);

    /// <summary>
    /// 在 stream 上将元素区域的每个字节异步设为同一 8-bit pattern，并保活 owner 至完成 / Asynchronously sets every byte in an element region to one 8-bit pattern and retains the owner until completion.
    /// </summary>
    /// <param name="value">8-bit byte pattern / 8-bit byte pattern.</param>
    /// <param name="region">元素区域 / Region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void SetByteAsync(byte value, HipMemoryRegion region, HipStream stream)
    {
        ValidateRegion(region, nameof(region));
        ValidateStream(stream);
        bool reference = false;
        try
        {
            IntPtr pointer = AcquirePointer(out reference);
            HipError error = InvokeMemset(pointer, value, region, stream.DangerousGetHandle(), true);
            HipCall.ThrowIfFailed(_nativeApi, error, MemsetOperation(region, true));
            AddLease(stream, () =>
            {
                if (reference)
                {
                    ReleasePointer();
                    reference = false;
                }
            });
        }
        catch
        {
            if (reference) ReleasePointer();
            throw;
        }
    }

    /// <summary>从同类型 pitched owner 同步复制其完整逻辑范围 / Synchronously copies the complete logical extent from another pitched owner of the same type.</summary>
    /// <param name="source">源 owner；仅在调用期间借用 / Source owner, borrowed only for the call.</param>
    public void CopyFrom(HipPitchedDeviceMemory<T> source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        CopyFrom(source, new HipMemoryRegion(default, source.Extent), default);
    }

    /// <summary>从同类型 pitched owner 的元素区域同步复制到指定元素偏移 / Synchronously copies an element region from another pitched owner to an element offset.</summary>
    /// <param name="source">源 owner；仅在调用期间借用 / Source owner, borrowed only for the call.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    /// <param name="destinationOffset">目标元素偏移 / Destination offset in elements.</param>
    public void CopyFrom(HipPitchedDeviceMemory<T> source, HipMemoryRegion sourceRegion, HipMemoryOffset destinationOffset)
    {
        ValidateDeviceCopy(source, sourceRegion, destinationOffset, null);
        bool destinationReference = false;
        bool sourceReference = false;
        try
        {
            IntPtr destinationPointer = AcquirePointer(out destinationReference);
            IntPtr sourcePointer = source.AcquirePointer(out sourceReference);
            HipError error = InvokeDeviceCopy(destinationPointer, sourcePointer, source, sourceRegion, destinationOffset, IntPtr.Zero, false);
            HipCall.ThrowIfFailed(_nativeApi, error, CopyOperation(sourceRegion.Extent, false));
        }
        finally
        {
            if (sourceReference) source.ReleasePointer();
            if (destinationReference) ReleasePointer();
        }
    }

    /// <summary>在 stream 上从同类型 pitched owner 异步复制其完整逻辑范围，并保活两个 owner / Asynchronously copies the complete logical extent from another pitched owner and retains both owners.</summary>
    /// <param name="source">源 owner / Source owner.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyFromAsync(HipPitchedDeviceMemory<T> source, HipStream stream)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        CopyFromAsync(source, new HipMemoryRegion(default, source.Extent), default, stream);
    }

    /// <summary>在 stream 上从同类型 pitched owner 的元素区域异步复制，并保活两个 owner / Asynchronously copies an element region from another pitched owner and retains both owners.</summary>
    /// <param name="source">源 owner / Source owner.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    /// <param name="destinationOffset">目标元素偏移 / Destination offset in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyFromAsync(HipPitchedDeviceMemory<T> source, HipMemoryRegion sourceRegion, HipMemoryOffset destinationOffset, HipStream stream)
    {
        ValidateDeviceCopy(source, sourceRegion, destinationOffset, stream);
        bool destinationReference = false;
        bool sourceReference = false;
        try
        {
            IntPtr destinationPointer = AcquirePointer(out destinationReference);
            IntPtr sourcePointer = source.AcquirePointer(out sourceReference);
            HipError error = InvokeDeviceCopy(destinationPointer, sourcePointer, source, sourceRegion, destinationOffset, stream.DangerousGetHandle(), true);
            HipCall.ThrowIfFailed(_nativeApi, error, CopyOperation(sourceRegion.Extent, true));
            AddLease(stream, () =>
            {
                if (sourceReference)
                {
                    source.ReleasePointer();
                    sourceReference = false;
                }
                if (destinationReference)
                {
                    ReleasePointer();
                    destinationReference = false;
                }
            });
        }
        catch
        {
            if (sourceReference) source.ReleasePointer();
            if (destinationReference) ReleasePointer();
            throw;
        }
    }

    /// <summary>从紧密排列的托管数组同步复制到整个逻辑范围 / Synchronously copies a tightly packed managed array into the complete logical extent.</summary>
    /// <param name="source">源数组；仅在调用期间 pinned / Source array, pinned only for the call.</param>
    public void CopyFrom(T[] source) => CopyFrom(source, WholeRegion);

    /// <summary>从紧密排列的托管数组同步复制到元素区域 / Synchronously copies a tightly packed managed array into an element region.</summary>
    /// <param name="source">至少包含区域元素数的源数组；仅在调用期间 pinned / Source array containing at least the region element count, pinned only for the call.</param>
    /// <param name="destinationRegion">目标元素区域 / Destination region in elements.</param>
    public void CopyFrom(T[] source, HipMemoryRegion destinationRegion) => CopyManaged(source, destinationRegion, true, null);

    /// <summary>从整个逻辑范围同步复制到紧密排列的托管数组 / Synchronously copies the complete logical extent into a tightly packed managed array.</summary>
    /// <param name="destination">目标数组；仅在调用期间 pinned / Destination array, pinned only for the call.</param>
    public void CopyTo(T[] destination) => CopyTo(destination, WholeRegion);

    /// <summary>从元素区域同步复制到紧密排列的托管数组 / Synchronously copies an element region into a tightly packed managed array.</summary>
    /// <param name="destination">至少包含区域元素数的目标数组；仅在调用期间 pinned / Destination array containing at least the region element count, pinned only for the call.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    public void CopyTo(T[] destination, HipMemoryRegion sourceRegion) => CopyManaged(destination, sourceRegion, false, null);

    /// <summary>在 stream 上从 pinned 托管数组异步复制到整个逻辑范围 / Asynchronously copies a pinned managed array into the complete logical extent.</summary>
    /// <param name="source">在完成前由 stream 保持 pinned 的源数组 / Source array kept pinned by the stream until completion.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyFromAsync(T[] source, HipStream stream) => CopyFromAsync(source, WholeRegion, stream);

    /// <summary>在 stream 上从 pinned 托管数组异步复制到元素区域 / Asynchronously copies a pinned managed array into an element region.</summary>
    /// <param name="source">在完成前由 stream 保持 pinned 的源数组 / Source array kept pinned by the stream until completion.</param>
    /// <param name="destinationRegion">目标元素区域 / Destination region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyFromAsync(T[] source, HipMemoryRegion destinationRegion, HipStream stream) => CopyManaged(source, destinationRegion, true, stream);

    /// <summary>在 stream 上从整个逻辑范围异步复制到 pinned 托管数组 / Asynchronously copies the complete logical extent into a pinned managed array.</summary>
    /// <param name="destination">在完成前由 stream 保持 pinned 的目标数组 / Destination array kept pinned by the stream until completion.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyToAsync(T[] destination, HipStream stream) => CopyToAsync(destination, WholeRegion, stream);

    /// <summary>在 stream 上从元素区域异步复制到 pinned 托管数组 / Asynchronously copies an element region into a pinned managed array.</summary>
    /// <param name="destination">在完成前由 stream 保持 pinned 的目标数组 / Destination array kept pinned by the stream until completion.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    public void CopyToAsync(T[] destination, HipMemoryRegion sourceRegion, HipStream stream) => CopyManaged(destination, sourceRegion, false, stream);

    /// <summary>从 pinned host owner 同步复制到整个逻辑范围 / Synchronously copies from a pinned host owner into the complete logical extent.</summary>
    /// <param name="source">pinned host source，调用期借用 / Pinned host source, borrowed for the call.</param>
    /// <param name="sourceByteOffset">源 byte offset / Source offset in bytes.</param>
    public void CopyFrom(HipPinnedMemory source, ulong sourceByteOffset = 0) => CopyFrom(source, WholeRegion, sourceByteOffset);

    /// <summary>从 pinned host owner 同步复制到元素区域 / Synchronously copies from a pinned host owner into an element region.</summary>
    /// <param name="source">pinned host source，调用期借用 / Pinned host source, borrowed for the call.</param>
    /// <param name="destinationRegion">目标元素区域 / Destination region in elements.</param>
    /// <param name="sourceByteOffset">源 byte offset / Source offset in bytes.</param>
    public void CopyFrom(HipPinnedMemory source, HipMemoryRegion destinationRegion, ulong sourceByteOffset = 0) => CopyPinned(source, destinationRegion, sourceByteOffset, true, null);

    /// <summary>从整个逻辑范围同步复制到 pinned host owner / Synchronously copies the complete logical extent into a pinned host owner.</summary>
    /// <param name="destination">pinned host destination，调用期借用 / Pinned host destination, borrowed for the call.</param>
    /// <param name="destinationByteOffset">目标 byte offset / Destination offset in bytes.</param>
    public void CopyTo(HipPinnedMemory destination, ulong destinationByteOffset = 0) => CopyTo(destination, WholeRegion, destinationByteOffset);

    /// <summary>从元素区域同步复制到 pinned host owner / Synchronously copies an element region into a pinned host owner.</summary>
    /// <param name="destination">pinned host destination，调用期借用 / Pinned host destination, borrowed for the call.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    /// <param name="destinationByteOffset">目标 byte offset / Destination offset in bytes.</param>
    public void CopyTo(HipPinnedMemory destination, HipMemoryRegion sourceRegion, ulong destinationByteOffset = 0) => CopyPinned(destination, sourceRegion, destinationByteOffset, false, null);

    /// <summary>在 stream 上从 pinned host owner 异步复制到整个逻辑范围，并保活两个 owner / Asynchronously copies from a pinned host owner into the complete logical extent and retains both owners.</summary>
    /// <param name="source">pinned host source / Pinned host source.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    /// <param name="sourceByteOffset">源 byte offset / Source offset in bytes.</param>
    public void CopyFromAsync(HipPinnedMemory source, HipStream stream, ulong sourceByteOffset = 0) => CopyFromAsync(source, WholeRegion, stream, sourceByteOffset);

    /// <summary>在 stream 上从 pinned host owner 异步复制到元素区域，并保活两个 owner / Asynchronously copies from a pinned host owner into an element region and retains both owners.</summary>
    /// <param name="source">pinned host source / Pinned host source.</param>
    /// <param name="destinationRegion">目标元素区域 / Destination region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    /// <param name="sourceByteOffset">源 byte offset / Source offset in bytes.</param>
    public void CopyFromAsync(HipPinnedMemory source, HipMemoryRegion destinationRegion, HipStream stream, ulong sourceByteOffset = 0) => CopyPinned(source, destinationRegion, sourceByteOffset, true, stream);

    /// <summary>在 stream 上从整个逻辑范围异步复制到 pinned host owner，并保活两个 owner / Asynchronously copies the complete logical extent into a pinned host owner and retains both owners.</summary>
    /// <param name="destination">pinned host destination / Pinned host destination.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    /// <param name="destinationByteOffset">目标 byte offset / Destination offset in bytes.</param>
    public void CopyToAsync(HipPinnedMemory destination, HipStream stream, ulong destinationByteOffset = 0) => CopyToAsync(destination, WholeRegion, stream, destinationByteOffset);

    /// <summary>在 stream 上从元素区域异步复制到 pinned host owner，并保活两个 owner / Asynchronously copies an element region into a pinned host owner and retains both owners.</summary>
    /// <param name="destination">pinned host destination / Pinned host destination.</param>
    /// <param name="sourceRegion">源元素区域 / Source region in elements.</param>
    /// <param name="stream">同一 runtime 和设备上的 stream / Stream on the same runtime and device.</param>
    /// <param name="destinationByteOffset">目标 byte offset / Destination offset in bytes.</param>
    public void CopyToAsync(HipPinnedMemory destination, HipMemoryRegion sourceRegion, HipStream stream, ulong destinationByteOffset = 0) => CopyPinned(destination, sourceRegion, destinationByteOffset, false, stream);

    /// <summary>释放 allocation；重复调用幂等，pending 异步借用会延迟实际释放 / Releases the allocation; repeated calls are idempotent and pending asynchronous borrows delay native release.</summary>
    /// <exception cref="HipException">显式 <c>hipFree</c> 失败；可重试 / Explicit <c>hipFree</c> fails; disposal can be retried.</exception>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_references != 0) return;
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

    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => AcquirePointer(out addedReference);
    void IHipPointerOwner.ReleasePointer() => ReleasePointer();

    private HipMemoryRegion WholeRegion => new(default, Extent);

    private IntPtr AcquirePointer(out bool addedReference)
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

    private void ReleasePointer()
    {
        bool release;
        lock (_lifetimeSync)
        {
            if (_references > 0)
            {
                _handle.DangerousRelease();
                _references--;
            }
            release = _disposeRequested && _references == 0;
        }

        if (release)
        {
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipFree");
            _handle.Dispose();
        }
    }

    private void CopyManaged(T[] array, HipMemoryRegion region, bool hostToDevice, HipStream? stream)
    {
        if (array is null) throw new ArgumentNullException(hostToDevice ? "source" : "destination");
        ValidateRegion(region, nameof(region));
        ValidateArrayLength(array, region.Extent);
        if (stream is null) ValidateReady(); else ValidateStream(stream);

        GCHandle pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
        bool memoryReference = false;
        try
        {
            IntPtr memoryPointer = AcquirePointer(out memoryReference);
            HipError error = InvokeHostCopy(
                memoryPointer,
                pinned.AddrOfPinnedObject(),
                region,
                hostToDevice,
                stream?.DangerousGetHandle() ?? IntPtr.Zero,
                stream is not null);
            HipCall.ThrowIfFailed(_nativeApi, error, HostCopyOperation(region.Extent, hostToDevice, stream is not null));
            if (stream is not null)
            {
                AddLease(stream, () =>
                {
                    if (pinned.IsAllocated) pinned.Free();
                    if (memoryReference)
                    {
                        ReleasePointer();
                        memoryReference = false;
                    }
                });
            }
        }
        catch
        {
            if (memoryReference)
            {
                ReleasePointer();
                memoryReference = false;
            }
            if (pinned.IsAllocated) pinned.Free();
            throw;
        }
        finally
        {
            if (stream is null)
            {
                if (memoryReference) ReleasePointer();
                if (pinned.IsAllocated) pinned.Free();
            }
        }
    }

    private void CopyPinned(HipPinnedMemory host, HipMemoryRegion region, ulong hostByteOffset, bool hostToDevice, HipStream? stream)
    {
        if (host is null) throw new ArgumentNullException(hostToDevice ? "source" : "destination");
        ValidateRegion(region, nameof(region));
        if (!ReferenceEquals(_nativeApi, host.NativeApi)) throw new ArgumentException("Memory and pinned buffer belong to different HIP Runtime clients.", hostToDevice ? "source" : "destination");
        ulong packedBytes = PackedByteCount(region.Extent);
        if (hostByteOffset > host.ByteLength || packedBytes > host.ByteLength - hostByteOffset)
            throw new ArgumentOutOfRangeException(nameof(hostByteOffset), "The copy exceeds the pinned buffer capacity.");
        HipDeviceMemory.ToUIntPtr(hostByteOffset, nameof(hostByteOffset));
        if (stream is null) ValidateReady(); else ValidateStream(stream);

        bool memoryReference = false;
        bool hostReference = false;
        try
        {
            IntPtr memoryPointer = AcquirePointer(out memoryReference);
            IntPtr hostPointer = host.AcquireHandle(out hostReference);
            hostPointer = AddBytes(hostPointer, hostByteOffset);
            HipError error = InvokeHostCopy(
                memoryPointer,
                hostPointer,
                region,
                hostToDevice,
                stream?.DangerousGetHandle() ?? IntPtr.Zero,
                stream is not null);
            HipCall.ThrowIfFailed(_nativeApi, error, HostCopyOperation(region.Extent, hostToDevice, stream is not null));
            if (stream is not null)
            {
                AddLease(stream, () =>
                {
                    if (hostReference)
                    {
                        host.ReleaseHandle();
                        hostReference = false;
                    }
                    if (memoryReference)
                    {
                        ReleasePointer();
                        memoryReference = false;
                    }
                });
            }
        }
        catch
        {
            if (hostReference)
            {
                host.ReleaseHandle();
                hostReference = false;
            }
            if (memoryReference)
            {
                ReleasePointer();
                memoryReference = false;
            }
            throw;
        }
        finally
        {
            if (stream is null)
            {
                if (hostReference) host.ReleaseHandle();
                if (memoryReference) ReleasePointer();
            }
        }
    }

    private HipError InvokeMemset(IntPtr pointer, byte value, HipMemoryRegion region, IntPtr stream, bool async)
    {
        ulong widthBytes = WidthBytes(region.Extent.Width, nameof(region));
        IntPtr address = AddressAt(pointer, region.Offset);
        UIntPtr nativeWidth = HipDeviceMemory.ToUIntPtr(widthBytes, nameof(region));
        UIntPtr nativeHeight = HipDeviceMemory.ToUIntPtr(region.Extent.Height, nameof(region));
        if (region.Extent.Height == 1 && region.Extent.Depth == 1)
            return async ? _nativeApi.MemsetAsync(address, value, nativeWidth, stream) : _nativeApi.Memset(address, value, nativeWidth);
        if (region.Extent.Depth == 1)
            return async
                ? _nativeApi.Memset2DAsync(address, NativePitch, value, nativeWidth, nativeHeight, stream)
                : _nativeApi.Memset2D(address, NativePitch, value, nativeWidth, nativeHeight);

        var pitched = new HipPitchedPtr(
            address,
            NativePitch,
            HipDeviceMemory.ToUIntPtr(_nativeXSizeBytes, nameof(Width)),
            HipDeviceMemory.ToUIntPtr(_nativeYSize, nameof(Height)));
        HipExtent nativeExtent = NativeExtent(region.Extent, nameof(region));
        return async ? _nativeApi.Memset3DAsync(pitched, value, nativeExtent, stream) : _nativeApi.Memset3D(pitched, value, nativeExtent);
    }

    private HipError InvokeDeviceCopy(
        IntPtr destinationPointer,
        IntPtr sourcePointer,
        HipPitchedDeviceMemory<T> source,
        HipMemoryRegion sourceRegion,
        HipMemoryOffset destinationOffset,
        IntPtr stream,
        bool async)
    {
        HipMemoryExtent copyExtent = sourceRegion.Extent;
        UIntPtr width = HipDeviceMemory.ToUIntPtr(WidthBytes(copyExtent.Width, nameof(sourceRegion)), nameof(sourceRegion));
        UIntPtr height = HipDeviceMemory.ToUIntPtr(copyExtent.Height, nameof(sourceRegion));
        if (copyExtent.Depth == 1)
        {
            IntPtr destination = AddressAt(destinationPointer, destinationOffset);
            IntPtr sourceAddress = source.AddressAt(sourcePointer, sourceRegion.Offset);
            return async
                ? _nativeApi.Memcpy2DAsync(destination, NativePitch, sourceAddress, source.NativePitch, width, height, HipMemoryCopyKind.DeviceToDevice, stream)
                : _nativeApi.Memcpy2D(destination, NativePitch, sourceAddress, source.NativePitch, width, height, HipMemoryCopyKind.DeviceToDevice);
        }

        HipMemcpy3DParameters parameters = Create3DParameters(
            sourcePointer,
            source.NativePitch,
            source._nativeXSizeBytes,
            source._nativeYSize,
            sourceRegion.Offset,
            destinationPointer,
            NativePitch,
            _nativeXSizeBytes,
            _nativeYSize,
            destinationOffset,
            copyExtent,
            HipMemoryCopyKind.DeviceToDevice);
        return async ? _nativeApi.Memcpy3DAsync(ref parameters, stream) : _nativeApi.Memcpy3D(ref parameters);
    }

    private HipError InvokeHostCopy(IntPtr memoryPointer, IntPtr hostPointer, HipMemoryRegion region, bool hostToDevice, IntPtr stream, bool async)
    {
        HipMemoryExtent copyExtent = region.Extent;
        ulong widthBytes = WidthBytes(copyExtent.Width, nameof(region));
        UIntPtr width = HipDeviceMemory.ToUIntPtr(widthBytes, nameof(region));
        UIntPtr height = HipDeviceMemory.ToUIntPtr(copyExtent.Height, nameof(region));
        HipMemoryCopyKind kind = hostToDevice ? HipMemoryCopyKind.HostToDevice : HipMemoryCopyKind.DeviceToHost;
        if (copyExtent.Depth == 1)
        {
            IntPtr memoryAddress = AddressAt(memoryPointer, region.Offset);
            return async
                ? _nativeApi.Memcpy2DAsync(
                    hostToDevice ? memoryAddress : hostPointer,
                    hostToDevice ? NativePitch : width,
                    hostToDevice ? hostPointer : memoryAddress,
                    hostToDevice ? width : NativePitch,
                    width,
                    height,
                    kind,
                    stream)
                : _nativeApi.Memcpy2D(
                    hostToDevice ? memoryAddress : hostPointer,
                    hostToDevice ? NativePitch : width,
                    hostToDevice ? hostPointer : memoryAddress,
                    hostToDevice ? width : NativePitch,
                    width,
                    height,
                    kind);
        }

        HipPitchedPtr hostDescriptor = new(hostPointer, width, width, height);
        HipPitchedPtr memoryDescriptor = Descriptor(memoryPointer);
        HipMemcpy3DParameters parameters = new()
        {
            SourceArray = IntPtr.Zero,
            SourcePosition = hostToDevice ? default : NativePosition(region.Offset, nameof(region)),
            SourcePointer = hostToDevice ? hostDescriptor : memoryDescriptor,
            DestinationArray = IntPtr.Zero,
            DestinationPosition = hostToDevice ? NativePosition(region.Offset, nameof(region)) : default,
            DestinationPointer = hostToDevice ? memoryDescriptor : hostDescriptor,
            Extent = NativeExtent(copyExtent, nameof(region)),
            Kind = kind,
        };
        return async ? _nativeApi.Memcpy3DAsync(ref parameters, stream) : _nativeApi.Memcpy3D(ref parameters);
    }

    private void ValidateDeviceCopy(HipPitchedDeviceMemory<T> source, HipMemoryRegion sourceRegion, HipMemoryOffset destinationOffset, HipStream? stream)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        ThrowIfDisposed();
        source.ThrowIfDisposed();
        if (!ReferenceEquals(_nativeApi, source._nativeApi)) throw new ArgumentException("Pitched allocations belong to different HIP Runtime clients.", nameof(source));
        if (DeviceOrdinal != source.DeviceOrdinal) throw new ArgumentException("Pitched allocations belong to different HIP devices.", nameof(source));
        source.ValidateRegion(sourceRegion, nameof(sourceRegion));
        ValidateDestination(destinationOffset, sourceRegion.Extent, nameof(destinationOffset));
        if (stream is null) ValidateReady(); else ValidateStream(stream);
    }

    private void ValidateStream(HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        ThrowIfDisposed();
        if (stream.IsDisposed) throw new ObjectDisposedException(nameof(HipStream));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Memory and stream belong to different HIP Runtime clients.", nameof(stream));
        if (stream.DeviceOrdinal != DeviceOrdinal) throw new ArgumentException("Memory and stream belong to different HIP devices.", nameof(stream));
        ValidateCurrentDevice();
    }

    private void ValidateReady()
    {
        ThrowIfDisposed();
        ValidateCurrentDevice();
    }

    private void ValidateCurrentDevice()
    {
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetDevice(out int currentDevice), "hipGetDevice");
        if (currentDevice != DeviceOrdinal)
            throw new InvalidOperationException("The current HIP device does not match the pitched allocation device.");
    }

    private void ValidateRegion(HipMemoryRegion region, string parameterName)
    {
        HipMemoryExtent extent = region.Extent;
        if (extent.Width == 0 || extent.Height == 0 || extent.Depth == 0)
            throw new ArgumentOutOfRangeException(parameterName, "A memory region extent must be positive.");
        ValidateDestination(region.Offset, extent, parameterName);
        WidthBytes(extent.Width, parameterName);
        PackedByteCount(extent);
    }

    private void ValidateDestination(HipMemoryOffset offset, HipMemoryExtent extent, string parameterName)
    {
        if (offset.X > Width || extent.Width > Width - offset.X ||
            offset.Y > Height || extent.Height > Height - offset.Y ||
            offset.Z > Depth || extent.Depth > Depth - offset.Z)
            throw new ArgumentOutOfRangeException(parameterName, "The memory region exceeds the pitched allocation.");
        AddressOffset(offset, parameterName);
    }

    private void ValidateArrayLength(T[] array, HipMemoryExtent extent)
    {
        ulong elementCount;
        try
        {
            elementCount = extent.ElementCount;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(extent), "The region element count overflows UInt64.");
        }
        if ((ulong)array.LongLength < elementCount)
            throw new ArgumentOutOfRangeException(nameof(array), "The managed array is smaller than the copy region.");
    }

    private ulong PackedByteCount(HipMemoryExtent extent)
    {
        try
        {
            ulong rows = checked(extent.Height * extent.Depth);
            ulong bytes = checked(WidthBytes(extent.Width, nameof(extent)) * rows);
            HipDeviceMemory.ToUIntPtr(bytes, nameof(extent));
            return bytes;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(extent), "The packed region byte count overflows UInt64.");
        }
    }

    private static ulong WidthBytes(ulong elementWidth, string parameterName)
    {
        try
        {
            ulong bytes = checked(elementWidth * (ulong)sizeof(T));
            HipDeviceMemory.ToUIntPtr(bytes, parameterName);
            return bytes;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The element width in bytes overflows UInt64.");
        }
    }

    private ulong AddressOffset(HipMemoryOffset offset, string parameterName)
    {
        try
        {
            ulong xBytes = WidthBytes(offset.X, parameterName);
            ulong yBytes = checked(offset.Y * PitchBytes);
            ulong zBytes = checked(offset.Z * SlicePitchBytes);
            ulong result = checked(checked(xBytes + yBytes) + zBytes);
            HipDeviceMemory.ToUIntPtr(result, parameterName);
            return result;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The pitched byte offset overflows UInt64.");
        }
    }

    private IntPtr AddressAt(IntPtr pointer, HipMemoryOffset offset) => AddBytes(pointer, AddressOffset(offset, nameof(offset)));

    private static IntPtr AddBytes(IntPtr pointer, ulong byteOffset)
    {
        if (UIntPtr.Size == 4)
        {
            uint address = unchecked((uint)pointer.ToInt32());
            uint result = checked(address + checked((uint)byteOffset));
            return new IntPtr(unchecked((int)result));
        }
        else
        {
            ulong address = unchecked((ulong)pointer.ToInt64());
            ulong result = checked(address + byteOffset);
            return new IntPtr(unchecked((long)result));
        }
    }

    private HipPitchedPtr Descriptor(IntPtr pointer) => new(
        pointer,
        NativePitch,
        HipDeviceMemory.ToUIntPtr(_nativeXSizeBytes, nameof(Width)),
        HipDeviceMemory.ToUIntPtr(_nativeYSize, nameof(Height)));

    private static HipMemcpy3DParameters Create3DParameters(
        IntPtr source,
        UIntPtr sourcePitch,
        ulong sourceXSizeBytes,
        ulong sourceYSize,
        HipMemoryOffset sourceOffset,
        IntPtr destination,
        UIntPtr destinationPitch,
        ulong destinationXSizeBytes,
        ulong destinationYSize,
        HipMemoryOffset destinationOffset,
        HipMemoryExtent extent,
        HipMemoryCopyKind kind) => new()
        {
            SourceArray = IntPtr.Zero,
            SourcePosition = NativePosition(sourceOffset, nameof(sourceOffset)),
            SourcePointer = new HipPitchedPtr(source, sourcePitch, HipDeviceMemory.ToUIntPtr(sourceXSizeBytes, nameof(sourceXSizeBytes)), HipDeviceMemory.ToUIntPtr(sourceYSize, nameof(sourceYSize))),
            DestinationArray = IntPtr.Zero,
            DestinationPosition = NativePosition(destinationOffset, nameof(destinationOffset)),
            DestinationPointer = new HipPitchedPtr(destination, destinationPitch, HipDeviceMemory.ToUIntPtr(destinationXSizeBytes, nameof(destinationXSizeBytes)), HipDeviceMemory.ToUIntPtr(destinationYSize, nameof(destinationYSize))),
            Extent = NativeExtent(extent, nameof(extent)),
            Kind = kind,
        };

    private static HipPos NativePosition(HipMemoryOffset offset, string parameterName) => new(
        HipDeviceMemory.ToUIntPtr(WidthBytes(offset.X, parameterName), parameterName),
        HipDeviceMemory.ToUIntPtr(offset.Y, parameterName),
        HipDeviceMemory.ToUIntPtr(offset.Z, parameterName));

    private static HipExtent NativeExtent(HipMemoryExtent extent, string parameterName) => new(
        HipDeviceMemory.ToUIntPtr(WidthBytes(extent.Width, parameterName), parameterName),
        HipDeviceMemory.ToUIntPtr(extent.Height, parameterName),
        HipDeviceMemory.ToUIntPtr(extent.Depth, parameterName));

    private UIntPtr NativePitch => HipDeviceMemory.ToUIntPtr(PitchBytes, nameof(PitchBytes));

    private static ulong CheckedMultiply(ulong left, ulong right, string parameterName)
    {
        try
        {
            ulong result = checked(left * right);
            HipDeviceMemory.ToUIntPtr(result, parameterName);
            return result;
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(parameterName, "The pitched allocation size overflows UInt64.");
        }
    }

    private static void AddLease(HipStream stream, Action release) => stream.AddPendingLease(new HipAsyncLease(release));

    private static string MemsetOperation(HipMemoryRegion region, bool async)
    {
        string suffix = async ? "Async" : string.Empty;
        if (region.Extent.Height == 1 && region.Extent.Depth == 1) return "hipMemset" + suffix;
        return region.Extent.Depth == 1 ? "hipMemset2D" + suffix : "hipMemset3D" + suffix;
    }

    private static string CopyOperation(HipMemoryExtent extent, bool async) =>
        extent.Depth == 1 ? (async ? "hipMemcpy2DAsync" : "hipMemcpy2D") : (async ? "hipMemcpy3DAsync" : "hipMemcpy3D");

    private static string HostCopyOperation(HipMemoryExtent extent, bool hostToDevice, bool async) =>
        CopyOperation(extent, async) + (hostToDevice ? "(host-to-device)" : "(device-to-host)");

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipPitchedDeviceMemory<T>));
    }
}
