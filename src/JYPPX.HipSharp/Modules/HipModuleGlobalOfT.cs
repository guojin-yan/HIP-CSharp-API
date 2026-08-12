using System;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 表示由 module 拥有、以非托管元素计数的 global symbol borrowed view / Represents a module-owned borrowed global-symbol view measured in unmanaged elements.
/// </summary>
/// <typeparam name="T">symbol 元素类型 / Symbol element type.</typeparam>
public unsafe sealed class HipModuleGlobal<T> where T : unmanaged
{
    private readonly HipModuleGlobal _global;
    private readonly ulong _elementSize = (ulong)sizeof(T);

    internal HipModuleGlobal(HipModuleGlobal global, ulong elementCount)
    {
        _global = global;
        ElementCount = elementCount;
    }

    /// <summary>获取 symbol 名称 / Gets the symbol name.</summary>
    public string Name => _global.Name;

    /// <summary>获取元素数量 / Gets the number of elements.</summary>
    public ulong ElementCount { get; }

    /// <summary>获取总字节数 / Gets the total byte length.</summary>
    public ulong ByteLength => _global.ByteLength;

    /// <summary>获取 view 是否仍由有效 module 支撑 / Gets whether the view is still backed by a valid module.</summary>
    public bool IsValid => _global.IsValid;

    /// <summary>同步复制整个数组到 symbol element offset / Synchronously copies an entire array to a symbol element offset.</summary>
    /// <param name="source">源元素数组 / Source element array.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换溢出 / The element range or byte conversion overflows.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(T[] source, ulong destinationOffset = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        _global.CopyTypedArray(source, ArrayBytes(source, nameof(source)), Bytes(destinationOffset, nameof(destinationOffset)), true, null);
    }

    /// <summary>从 symbol element offset 同步复制到整个数组 / Synchronously copies from a symbol element offset to an entire array.</summary>
    /// <param name="destination">目标元素数组 / Destination element array.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换溢出 / The element range or byte conversion overflows.</exception>
    /// <exception cref="ObjectDisposedException">module 已释放 / The module is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(T[] destination, ulong sourceOffset = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        _global.CopyTypedArray(destination, ArrayBytes(destination, nameof(destination)), Bytes(sourceOffset, nameof(sourceOffset)), false, null);
    }

    /// <summary>在显式 stream 上异步复制整个数组到 symbol，并保留 pin 与 module / Asynchronously copies an entire array to the symbol on an explicit stream, retaining its pin and the module.</summary>
    /// <param name="source">源元素数组 / Source element array.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <exception cref="ArgumentException">stream 的 Runtime 或 device 不匹配 / The stream Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换溢出 / The element range or byte conversion overflows.</exception>
    /// <exception cref="ObjectDisposedException">module 或 stream 已释放 / The module or stream is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(T[] source, HipStream stream, ulong destinationOffset = 0)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));
        _global.CopyTypedArray(source, ArrayBytes(source, nameof(source)), Bytes(destinationOffset, nameof(destinationOffset)), true, stream ?? throw new ArgumentNullException(nameof(stream)));
    }

    /// <summary>在显式 stream 上从 symbol 异步复制到整个数组 / Asynchronously copies from the symbol to an entire array on an explicit stream.</summary>
    /// <param name="destination">目标元素数组 / Destination element array.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <exception cref="ArgumentException">stream 的 Runtime 或 device 不匹配 / The stream Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换溢出 / The element range or byte conversion overflows.</exception>
    /// <exception cref="ObjectDisposedException">module 或 stream 已释放 / The module or stream is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(T[] destination, HipStream stream, ulong sourceOffset = 0)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        _global.CopyTypedArray(destination, ArrayBytes(destination, nameof(destination)), Bytes(sourceOffset, nameof(sourceOffset)), false, stream ?? throw new ArgumentNullException(nameof(stream)));
    }

    /// <summary>以元素为单位从 pinned host owner 同步复制到 symbol / Synchronously copies from a pinned host owner to the symbol in element units.</summary>
    /// <param name="source">pinned 源 owner / Pinned source owner.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <param name="sourceOffset">pinned source 元素偏移 / Element offset in the pinned source.</param>
    /// <exception cref="ArgumentException">owner 的 Runtime 不匹配 / The owner Runtime does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 owner 已释放 / The module or owner is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(HipPinnedMemory source, ulong elementCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        _global.CopyFrom(source, Bytes(elementCount, nameof(elementCount)), Bytes(destinationOffset, nameof(destinationOffset)), Bytes(sourceOffset, nameof(sourceOffset)));

    /// <summary>以元素为单位从 symbol 同步复制到 pinned host owner / Synchronously copies from the symbol to a pinned host owner in element units.</summary>
    /// <param name="destination">pinned 目标 owner / Pinned destination owner.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <param name="destinationOffset">pinned destination 元素偏移 / Element offset in the pinned destination.</param>
    /// <exception cref="ArgumentException">owner 的 Runtime 不匹配 / The owner Runtime does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 owner 已释放 / The module or owner is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(HipPinnedMemory destination, ulong elementCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        _global.CopyTo(destination, Bytes(elementCount, nameof(elementCount)), Bytes(sourceOffset, nameof(sourceOffset)), Bytes(destinationOffset, nameof(destinationOffset)));

    /// <summary>以元素为单位在显式 stream 上从 pinned host owner 异步复制到 symbol / Asynchronously copies from a pinned host owner to the symbol in element units on an explicit stream.</summary>
    /// <param name="source">pinned 源 owner / Pinned source owner.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <param name="sourceOffset">pinned source 元素偏移 / Element offset in the pinned source.</param>
    /// <exception cref="ArgumentException">owner、stream、Runtime 或 device 不匹配 / Owner, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 owner 已释放 / The module, stream, or owner is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(HipPinnedMemory source, HipStream stream, ulong elementCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        _global.CopyFromAsync(source, stream, Bytes(elementCount, nameof(elementCount)), Bytes(destinationOffset, nameof(destinationOffset)), Bytes(sourceOffset, nameof(sourceOffset)));

    /// <summary>以元素为单位在显式 stream 上从 symbol 异步复制到 pinned host owner / Asynchronously copies from the symbol to a pinned host owner in element units on an explicit stream.</summary>
    /// <param name="destination">pinned 目标 owner / Pinned destination owner.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <param name="destinationOffset">pinned destination 元素偏移 / Element offset in the pinned destination.</param>
    /// <exception cref="ArgumentException">owner、stream、Runtime 或 device 不匹配 / Owner, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 owner 已释放 / The module, stream, or owner is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(HipPinnedMemory destination, HipStream stream, ulong elementCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        _global.CopyToAsync(destination, stream, Bytes(elementCount, nameof(elementCount)), Bytes(sourceOffset, nameof(sourceOffset)), Bytes(destinationOffset, nameof(destinationOffset)));

    /// <summary>以元素为单位从 owned device allocation 同步复制到 symbol / Synchronously copies from an owned device allocation to the symbol in element units.</summary>
    /// <param name="source">源 device allocation / Source device allocation.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <param name="sourceOffset">device source 元素偏移 / Element offset in the device source.</param>
    /// <exception cref="ArgumentException">allocation 的 Runtime 或 device 不匹配 / The allocation Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 allocation 已释放 / The module or allocation is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyFrom(HipDeviceMemory source, ulong elementCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        _global.CopyFrom(source, Bytes(elementCount, nameof(elementCount)), Bytes(destinationOffset, nameof(destinationOffset)), Bytes(sourceOffset, nameof(sourceOffset)));

    /// <summary>以元素为单位从 symbol 同步复制到 owned device allocation / Synchronously copies from the symbol to an owned device allocation in element units.</summary>
    /// <param name="destination">目标 device allocation / Destination device allocation.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <param name="destinationOffset">device destination 元素偏移 / Element offset in the device destination.</param>
    /// <exception cref="ArgumentException">allocation 的 Runtime 或 device 不匹配 / The allocation Runtime or device does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module 或 allocation 已释放 / The module or allocation is disposed.</exception>
    /// <exception cref="HipException">同步复制失败 / The synchronous copy fails.</exception>
    public void CopyTo(HipDeviceMemory destination, ulong elementCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        _global.CopyTo(destination, Bytes(elementCount, nameof(elementCount)), Bytes(sourceOffset, nameof(sourceOffset)), Bytes(destinationOffset, nameof(destinationOffset)));

    /// <summary>以元素为单位在显式 stream 上从 owned device allocation 异步复制到 symbol / Asynchronously copies from an owned device allocation to the symbol in element units on an explicit stream.</summary>
    /// <param name="source">源 device allocation / Source device allocation.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="destinationOffset">symbol 目标元素偏移 / Destination element offset in the symbol.</param>
    /// <param name="sourceOffset">device source 元素偏移 / Element offset in the device source.</param>
    /// <exception cref="ArgumentException">allocation、stream、Runtime 或 device 不匹配 / Allocation, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 allocation 已释放 / The module, stream, or allocation is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyFromAsync(HipDeviceMemory source, HipStream stream, ulong elementCount, ulong destinationOffset = 0, ulong sourceOffset = 0) =>
        _global.CopyFromAsync(source, stream, Bytes(elementCount, nameof(elementCount)), Bytes(destinationOffset, nameof(destinationOffset)), Bytes(sourceOffset, nameof(sourceOffset)));

    /// <summary>以元素为单位在显式 stream 上从 symbol 异步复制到 owned device allocation / Asynchronously copies from the symbol to an owned device allocation in element units on an explicit stream.</summary>
    /// <param name="destination">目标 device allocation / Destination device allocation.</param>
    /// <param name="stream">目标 stream / Destination stream.</param>
    /// <param name="elementCount">复制元素数 / Number of elements to copy.</param>
    /// <param name="sourceOffset">symbol 源元素偏移 / Source element offset in the symbol.</param>
    /// <param name="destinationOffset">device destination 元素偏移 / Element offset in the device destination.</param>
    /// <exception cref="ArgumentException">allocation、stream、Runtime 或 device 不匹配 / Allocation, stream, Runtime, or device identity does not match.</exception>
    /// <exception cref="ArgumentOutOfRangeException">element range 或 byte 转换无效 / The element range or byte conversion is invalid.</exception>
    /// <exception cref="ObjectDisposedException">module、stream 或 allocation 已释放 / The module, stream, or allocation is disposed.</exception>
    /// <exception cref="HipException">异步提交失败 / The asynchronous submission fails.</exception>
    public void CopyToAsync(HipDeviceMemory destination, HipStream stream, ulong elementCount, ulong sourceOffset = 0, ulong destinationOffset = 0) =>
        _global.CopyToAsync(destination, stream, Bytes(elementCount, nameof(elementCount)), Bytes(sourceOffset, nameof(sourceOffset)), Bytes(destinationOffset, nameof(destinationOffset)));

    private ulong ArrayBytes(T[] array, string parameterName)
    {
        try { return checked((ulong)array.LongLength * _elementSize); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(parameterName, "The array byte length overflows UInt64."); }
    }

    private ulong Bytes(ulong elementCount, string parameterName)
    {
        try { return checked(elementCount * _elementSize); }
        catch (OverflowException) { throw new ArgumentOutOfRangeException(parameterName, "The element range overflows UInt64 bytes."); }
    }
}
