using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示由 module 拥有的全局符号 borrowed byte range；它不是 allocation 且不得释放 / Represents a module-owned borrowed global-symbol byte range; it is not an allocation and must not be freed.
/// </summary>
/// <remarks>
/// module 释放后该 view 立即失效；异步复制会保留 module 和相关内存 owner 直到 stream 完成 / The view is invalid immediately after module disposal; asynchronous copies retain the module and relevant memory owners until stream completion.
/// </remarks>
public sealed class HipModuleGlobal
{
    private readonly HipModule _module;
    private readonly IntPtr _pointer;

    internal HipModuleGlobal(HipModule module, IntPtr pointer, ulong byteLength, string name)
    {
        _module = module;
        _pointer = pointer;
        ByteLength = byteLength;
        Name = name;
    }

    /// <summary>获取 symbol 名称 / Gets the symbol name.</summary>
    public string Name { get; }

    /// <summary>获取 borrowed range 的字节数 / Gets the borrowed range length in bytes.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取 view 是否仍由有效 module 支撑 / Gets whether the view is still backed by a valid module.</summary>
    public bool IsValid => !_module.IsDisposed;

    /// <summary>从托管数组同步复制全部字节到 symbol / Synchronously copies all bytes from a managed array to the symbol.</summary>
    /// <param name="source">源数组 / Source array.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset within the symbol.</param>
    /// <exception cref="ArgumentNullException">数组为 <see langword="null"/> / The array is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">偏移和数组长度超出 symbol / The offset and array length exceed the symbol.</exception>
    /// <exception cref="InvalidOperationException">module 设备不是当前设备 / The module device is not current.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(byte[] source, ulong destinationOffset = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        CopyArray(source, (ulong)source.LongLength, destinationOffset, true, null);
    }

    /// <summary>从 symbol 同步复制字节到整个托管数组 / Synchronously copies bytes from the symbol to an entire managed array.</summary>
    /// <param name="destination">目标数组 / Destination array.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset within the symbol.</param>
    /// <exception cref="ArgumentNullException">数组为 <see langword="null"/> / The array is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">偏移和数组长度超出 symbol / The offset and array length exceed the symbol.</exception>
    /// <exception cref="InvalidOperationException">module 设备不是当前设备 / The module device is not current.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(byte[] destination, ulong sourceOffset = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        CopyArray(destination, (ulong)destination.LongLength, sourceOffset, false, null);
    }

    /// <summary>在显式 stream 上从托管数组异步复制到 symbol，并保留数组 pin 与 module / Asynchronously copies from a managed array on an explicit stream, retaining the array pin and module.</summary>
    /// <param name="source">源数组 / Source array.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset within the symbol.</param>
    /// <exception cref="ArgumentException">stream 属于其他 Runtime 或设备 / The stream belongs to another Runtime or device.</exception>
    /// <exception cref="ArgumentOutOfRangeException">复制范围超出 symbol / The copy range exceeds the symbol.</exception>
    /// <exception cref="ObjectDisposedException">module 或 stream 已释放 / The module or stream is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(byte[] source, HipStream stream, ulong destinationOffset = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        CopyArray(source, (ulong)source.LongLength, destinationOffset, true, stream ?? throw new ArgumentNullException(nameof(stream)));
    }

    /// <summary>在显式 stream 上从 symbol 异步复制到托管数组 / Asynchronously copies from the symbol to a managed array on an explicit stream.</summary>
    /// <param name="destination">目标数组；完成前由 stream 保持 pin / Destination array pinned by the stream until completion.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset within the symbol.</param>
    /// <exception cref="ArgumentException">stream 属于其他 Runtime 或设备 / The stream belongs to another Runtime or device.</exception>
    /// <exception cref="ArgumentOutOfRangeException">复制范围超出 symbol / The copy range exceeds the symbol.</exception>
    /// <exception cref="ObjectDisposedException">module 或 stream 已释放 / The module or stream is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(byte[] destination, HipStream stream, ulong sourceOffset = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        CopyArray(destination, (ulong)destination.LongLength, sourceOffset, false, stream ?? throw new ArgumentNullException(nameof(stream)));
    }

    /// <summary>从 owned pinned host memory 同步复制到 symbol / Synchronously copies from owned pinned host memory to the symbol.</summary>
    /// <param name="source">pinned 源 owner / Pinned source owner.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset in the symbol.</param>
    /// <param name="sourceOffset">pinned source 字节偏移 / Byte offset in the pinned source.</param>
    /// <exception cref="ArgumentException">owner 的 Runtime 不匹配 / The owner Runtime does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 owner 已释放 / The module or owner is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(HipPinnedMemory source, ulong byteCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        CopyPinned(source, byteCount, destinationOffset, sourceOffset, true, null);

    /// <summary>从 symbol 同步复制到 owned pinned host memory / Synchronously copies from the symbol to owned pinned host memory.</summary>
    /// <param name="destination">pinned 目标 owner / Pinned destination owner.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset in the symbol.</param>
    /// <param name="destinationOffset">pinned destination 字节偏移 / Byte offset in the pinned destination.</param>
    /// <exception cref="ArgumentException">owner 的 Runtime 不匹配 / The owner Runtime does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 owner 已释放 / The module or owner is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(HipPinnedMemory destination, ulong byteCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        CopyPinned(destination, byteCount, sourceOffset, destinationOffset, false, null);

    /// <summary>在显式 stream 上从 pinned owner 异步复制到 symbol / Asynchronously copies from a pinned owner to the symbol on an explicit stream.</summary>
    /// <param name="source">pinned 源 owner / Pinned source owner.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset in the symbol.</param>
    /// <param name="sourceOffset">pinned source 字节偏移 / Byte offset in the pinned source.</param>
    /// <exception cref="ArgumentException">owner、stream、Runtime 或 device 不匹配 / Owner, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 owner 已释放 / The module, stream, or owner is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(HipPinnedMemory source, HipStream stream, ulong byteCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        CopyPinned(source, byteCount, destinationOffset, sourceOffset, true, stream ?? throw new ArgumentNullException(nameof(stream)));

    /// <summary>在显式 stream 上从 symbol 异步复制到 pinned owner / Asynchronously copies from the symbol to a pinned owner on an explicit stream.</summary>
    /// <param name="destination">pinned 目标 owner / Pinned destination owner.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset in the symbol.</param>
    /// <param name="destinationOffset">pinned destination 字节偏移 / Byte offset in the pinned destination.</param>
    /// <exception cref="ArgumentException">owner、stream、Runtime 或 device 不匹配 / Owner, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 owner 已释放 / The module, stream, or owner is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(HipPinnedMemory destination, HipStream stream, ulong byteCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        CopyPinned(destination, byteCount, sourceOffset, destinationOffset, false, stream ?? throw new ArgumentNullException(nameof(stream)));

    /// <summary>从 owned device allocation 同步复制到 symbol / Synchronously copies from an owned device allocation to the symbol.</summary>
    /// <param name="source">源 device allocation / Source device allocation.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset in the symbol.</param>
    /// <param name="sourceOffset">device source 字节偏移 / Byte offset in the device source.</param>
    /// <exception cref="ArgumentException">allocation 的 Runtime 或 device 不匹配 / The allocation Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 allocation 已释放 / The module or allocation is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(HipDeviceMemory source, ulong byteCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        CopyDevice(source, byteCount, destinationOffset, sourceOffset, true, null);

    /// <summary>从 symbol 同步复制到 owned device allocation / Synchronously copies from the symbol to an owned device allocation.</summary>
    /// <param name="destination">目标 device allocation / Destination device allocation.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset in the symbol.</param>
    /// <param name="destinationOffset">device destination 字节偏移 / Byte offset in the device destination.</param>
    /// <exception cref="ArgumentException">allocation 的 Runtime 或 device 不匹配 / The allocation Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 allocation 已释放 / The module or allocation is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(HipDeviceMemory destination, ulong byteCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        CopyDevice(destination, byteCount, sourceOffset, destinationOffset, false, null);

    /// <summary>在显式 stream 上从 owned device allocation 异步复制到 symbol / Asynchronously copies from an owned device allocation to the symbol on an explicit stream.</summary>
    /// <param name="source">源 device allocation / Source device allocation.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="destinationOffset">symbol 目标字节偏移 / Destination byte offset in the symbol.</param>
    /// <param name="sourceOffset">device source 字节偏移 / Byte offset in the device source.</param>
    /// <exception cref="ArgumentException">allocation、stream、Runtime 或 device 不匹配 / Allocation, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 allocation 已释放 / The module, stream, or allocation is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(HipDeviceMemory source, HipStream stream, ulong byteCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        CopyDevice(source, byteCount, destinationOffset, sourceOffset, true, stream ?? throw new ArgumentNullException(nameof(stream)));

    /// <summary>在显式 stream 上从 symbol 异步复制到 owned device allocation / Asynchronously copies from the symbol to an owned device allocation on an explicit stream.</summary>
    /// <param name="destination">目标 device allocation / Destination device allocation.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="byteCount">复制字节数 / Number of bytes to copy.</param>
    /// <param name="sourceOffset">symbol 源字节偏移 / Source byte offset in the symbol.</param>
    /// <param name="destinationOffset">device destination 字节偏移 / Byte offset in the device destination.</param>
    /// <exception cref="ArgumentException">allocation、stream、Runtime 或 device 不匹配 / Allocation, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">任一 byte range 无效 / Either byte range is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 allocation 已释放 / The module, stream, or allocation is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(HipDeviceMemory destination, HipStream stream, ulong byteCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        CopyDevice(destination, byteCount, sourceOffset, destinationOffset, false, stream ?? throw new ArgumentNullException(nameof(stream)));

    internal void CopyTypedArray(Array array, ulong byteCount, ulong symbolOffset, bool hostToSymbol, HipStream? stream) =>
        CopyArray(array, byteCount, symbolOffset, hostToSymbol, stream);

    internal static void ValidateNativeRange(IntPtr pointer, ulong byteLength)
    {
        if (pointer == IntPtr.Zero) throw new InvalidOperationException("hipModuleGetGlobal succeeded but returned a null pointer.");
        if (byteLength == 0) throw new InvalidOperationException("hipModuleGetGlobal succeeded but returned a zero byte extent.");
        ulong address = IntPtr.Size == 4 ? unchecked((uint)pointer.ToInt32()) : unchecked((ulong)pointer.ToInt64());
        ulong last;
        try { last = checked(address + byteLength - 1); }
        catch (OverflowException) { throw new InvalidOperationException("hipModuleGetGlobal returned a range that exceeds the process address space."); }
        if (IntPtr.Size == 4 && last > uint.MaxValue)
            throw new InvalidOperationException("hipModuleGetGlobal returned a range that exceeds the process address space.");
    }

    private void CopyArray(Array array, ulong byteCount, ulong symbolOffset, bool hostToSymbol, HipStream? stream)
    {
        ValidateRange(symbolOffset, byteCount, nameof(symbolOffset));
        if (stream is not null) ValidateStream(stream);
        if (byteCount == 0)
        {
            _module.Invoke(_ => 0);
            return;
        }
        if (stream is null)
        {
            _module.Invoke(_ =>
            {
                EnsureCurrentDevice();
                GCHandle pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
                try { CopyNative(Add(_pointer, symbolOffset), pinned.AddrOfPinnedObject(), byteCount, hostToSymbol, null); }
                finally { pinned.Free(); }
                return 0;
            });
            return;
        }
        _module.Invoke(_ =>
        {
            EnsureCurrentDevice();
            QueueArray(array, byteCount, symbolOffset, hostToSymbol, stream);
            return 0;
        });
    }

    private void CopyPinned(HipPinnedMemory host, ulong byteCount, ulong symbolOffset, ulong hostOffset, bool hostToSymbol, HipStream? stream)
    {
        if (host is null) throw new ArgumentNullException(hostToSymbol ? "source" : "destination");
        if (host.IsDisposed) throw new ObjectDisposedException(nameof(HipPinnedMemory));
        ValidateOwner(host.NativeApi, null, host.ByteLength, hostOffset, byteCount, hostToSymbol ? "sourceOffset" : "destinationOffset");
        ValidateRange(symbolOffset, byteCount, hostToSymbol ? "destinationOffset" : "sourceOffset");
        if (stream is not null) ValidateStream(stream);
        _module.Invoke(_ =>
        {
            EnsureCurrentDevice();
            if (byteCount == 0) return 0;
            if (stream is null) CopyPinnedSync(host, byteCount, symbolOffset, hostOffset, hostToSymbol);
            else QueuePinned(host, byteCount, symbolOffset, hostOffset, hostToSymbol, stream);
            return 0;
        });
    }

    private void CopyDevice(HipDeviceMemory memory, ulong byteCount, ulong symbolOffset, ulong memoryOffset, bool deviceToSymbol, HipStream? stream)
    {
        if (memory is null) throw new ArgumentNullException(deviceToSymbol ? "source" : "destination");
        if (memory.IsDisposed) throw new ObjectDisposedException(nameof(HipDeviceMemory));
        ValidateOwner(memory.NativeApi, memory.DeviceOrdinal, memory.ByteLength, memoryOffset, byteCount, deviceToSymbol ? "sourceOffset" : "destinationOffset");
        ValidateRange(symbolOffset, byteCount, deviceToSymbol ? "destinationOffset" : "sourceOffset");
        if (stream is not null) ValidateStream(stream);
        _module.Invoke(_ =>
        {
            EnsureCurrentDevice();
            if (byteCount == 0) return 0;
            if (stream is null) CopyDeviceSync(memory, byteCount, symbolOffset, memoryOffset, deviceToSymbol);
            else QueueDevice(memory, byteCount, symbolOffset, memoryOffset, deviceToSymbol, stream);
            return 0;
        });
    }

    private void CopyPinnedSync(HipPinnedMemory host, ulong byteCount, ulong symbolOffset, ulong hostOffset, bool hostToSymbol)
    {
        bool reference = false;
        try
        {
            IntPtr hostPointer = Add(host.AcquireHandle(out reference), hostOffset);
            CopyNative(Add(_pointer, symbolOffset), hostPointer, byteCount, hostToSymbol, null);
        }
        finally { if (reference) host.ReleaseHandle(); }
    }

    private void CopyDeviceSync(HipDeviceMemory memory, ulong byteCount, ulong symbolOffset, ulong memoryOffset, bool deviceToSymbol)
    {
        bool reference = false;
        try
        {
            IntPtr memoryPointer = Add(memory.DangerousAcquireHandle(out reference), memoryOffset);
            IntPtr symbolPointer = Add(_pointer, symbolOffset);
            HipCall.ThrowIfFailed(_module.NativeApi, _module.NativeApi.Memcpy(
                deviceToSymbol ? symbolPointer : memoryPointer,
                deviceToSymbol ? memoryPointer : symbolPointer,
                HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)),
                HipMemoryCopyKind.DeviceToDevice), "hipMemcpy(device-to-device module global)");
        }
        finally { if (reference) memory.DangerousReleaseHandle(); }
    }

    private void QueueArray(Array array, ulong byteCount, ulong symbolOffset, bool hostToSymbol, HipStream stream)
    {
        GCHandle pinned = default;
        bool moduleReference = false;
        bool transferred = false;
        try
        {
            _module.AcquireAsyncReference();
            moduleReference = true;
            pinned = GCHandle.Alloc(array, GCHandleType.Pinned);
            CopyNative(Add(_pointer, symbolOffset), pinned.AddrOfPinnedObject(), byteCount, hostToSymbol, stream);
            stream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (pinned.IsAllocated) pinned.Free();
                if (moduleReference) { _module.ReleaseAsyncReference(); moduleReference = false; }
            }));
            transferred = true;
        }
        finally
        {
            if (!transferred)
            {
                if (pinned.IsAllocated) pinned.Free();
                if (moduleReference) _module.ReleaseAsyncReference();
            }
        }
    }

    private void QueuePinned(HipPinnedMemory host, ulong byteCount, ulong symbolOffset, ulong hostOffset, bool hostToSymbol, HipStream stream)
    {
        bool moduleReference = false;
        bool hostReference = false;
        bool transferred = false;
        try
        {
            _module.AcquireAsyncReference();
            moduleReference = true;
            IntPtr hostPointer = Add(host.AcquireHandle(out hostReference), hostOffset);
            CopyNative(Add(_pointer, symbolOffset), hostPointer, byteCount, hostToSymbol, stream);
            stream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (hostReference) { host.ReleaseHandle(); hostReference = false; }
                if (moduleReference) { _module.ReleaseAsyncReference(); moduleReference = false; }
            }));
            transferred = true;
        }
        finally
        {
            if (!transferred)
            {
                if (hostReference) host.ReleaseHandle();
                if (moduleReference) _module.ReleaseAsyncReference();
            }
        }
    }

    private void QueueDevice(HipDeviceMemory memory, ulong byteCount, ulong symbolOffset, ulong memoryOffset, bool deviceToSymbol, HipStream stream)
    {
        bool moduleReference = false;
        bool memoryReference = false;
        bool transferred = false;
        try
        {
            _module.AcquireAsyncReference();
            moduleReference = true;
            IntPtr memoryPointer = Add(memory.DangerousAcquireHandle(out memoryReference), memoryOffset);
            IntPtr symbolPointer = Add(_pointer, symbolOffset);
            HipCall.ThrowIfFailed(_module.NativeApi, _module.NativeApi.MemcpyAsync(
                deviceToSymbol ? symbolPointer : memoryPointer,
                deviceToSymbol ? memoryPointer : symbolPointer,
                HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)),
                HipMemoryCopyKind.DeviceToDevice,
                stream.DangerousGetHandle()), "hipMemcpyAsync(device-to-device module global)");
            stream.AddPendingLease(new HipAsyncLease(() =>
            {
                if (memoryReference) { memory.DangerousReleaseHandle(); memoryReference = false; }
                if (moduleReference) { _module.ReleaseAsyncReference(); moduleReference = false; }
            }));
            transferred = true;
        }
        finally
        {
            if (!transferred)
            {
                if (memoryReference) memory.DangerousReleaseHandle();
                if (moduleReference) _module.ReleaseAsyncReference();
            }
        }
    }

    private void CopyNative(IntPtr symbolPointer, IntPtr hostPointer, ulong byteCount, bool hostToSymbol, HipStream? stream)
    {
        HipMemoryCopyKind kind = hostToSymbol ? HipMemoryCopyKind.HostToDevice : HipMemoryCopyKind.DeviceToHost;
        IntPtr destination = hostToSymbol ? symbolPointer : hostPointer;
        IntPtr source = hostToSymbol ? hostPointer : symbolPointer;
        HipError error = stream is null
            ? _module.NativeApi.Memcpy(destination, source, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), kind)
            : _module.NativeApi.MemcpyAsync(destination, source, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), kind, stream.DangerousGetHandle());
        HipCall.ThrowIfFailed(_module.NativeApi, error, stream is null ? "hipMemcpy(module global)" : "hipMemcpyAsync(module global)");
    }

    private void ValidateOwner(IHipNativeApi nativeApi, int? deviceOrdinal, ulong capacity, ulong offset, ulong count, string offsetName)
    {
        if (!ReferenceEquals(_module.NativeApi, nativeApi)) throw new ArgumentException("The module global and memory owner belong to different HIP Runtime clients.");
        if (deviceOrdinal.HasValue && deviceOrdinal.Value != _module.DeviceOrdinal) throw new ArgumentException("The module global and device allocation belong to different devices.");
        ValidateRange(offset, count, capacity, offsetName);
    }

    private void ValidateStream(HipStream stream)
    {
        if (!ReferenceEquals(_module.NativeApi, stream.NativeApi)) throw new ArgumentException("The module global and stream belong to different HIP Runtime clients.", nameof(stream));
        if (stream.DeviceOrdinal != _module.DeviceOrdinal) throw new ArgumentException("The module global and stream belong to different devices.", nameof(stream));
        _ = stream.DangerousGetHandle();
    }

    private void EnsureCurrentDevice()
    {
        HipCall.ThrowIfFailed(_module.NativeApi, _module.NativeApi.GetDevice(out int currentDevice), "hipGetDevice");
        if (currentDevice != _module.DeviceOrdinal) throw new InvalidOperationException("The device on which the module was loaded must be current for module-global copies.");
    }

    private void ValidateRange(ulong offset, ulong count, string parameterName) => ValidateRange(offset, count, ByteLength, parameterName);

    private static void ValidateRange(ulong offset, ulong count, ulong capacity, string parameterName)
    {
        ulong end;
        try { end = checked(offset + count); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(parameterName, "The copy range overflows UInt64."); }
        if (end > capacity) throw new ArgumentOutOfRangeException(parameterName, "The copy range exceeds the available extent.");
    }

    private static IntPtr Add(IntPtr pointer, ulong offset)
    {
        if (offset == 0) return pointer;
        ulong address = IntPtr.Size == 4 ? unchecked((uint)pointer.ToInt32()) : unchecked((ulong)pointer.ToInt64());
        ulong result;
        try { result = checked(address + offset); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(nameof(offset), "The pointer offset exceeds the process address space."); }
        if (IntPtr.Size == 4 && result > uint.MaxValue) throw new ArgumentOutOfRangeException(nameof(offset), "The pointer offset exceeds the process address space.");
        return IntPtr.Size == 4 ? new IntPtr(unchecked((int)(uint)result)) : new IntPtr(unchecked((long)result));
    }
}
