using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Streams;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 以元素数量表达的设备内存视图 / A device-memory view expressed in elements.
/// </summary>
/// <typeparam name="T">非托管元素类型 / Unmanaged element type.</typeparam>
public unsafe sealed class HipTypedMemory<T> : IDisposable where T : unmanaged
{
    private readonly HipDeviceMemory _memory;

    internal HipTypedMemory(HipDeviceMemory memory, ulong elementCount)
    {
        _memory = memory;
        ElementCount = elementCount;
    }

    /// <summary>获取元素数量 / Gets the element count.</summary>
    public ulong ElementCount { get; }

    /// <summary>获取总字节数 / Gets the total byte length.</summary>
    public ulong ByteLength => _memory.ByteLength;

    /// <summary>同步复制到设备 / Copies synchronously to the device.</summary>
    public void CopyFrom(T[] source) => _memory.CopyFrom(ToBytes(source));

    /// <summary>同步复制回主机 / Copies synchronously back to the host.</summary>
    public void CopyTo(T[] destination)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if ((ulong)destination.LongLength > ElementCount) throw new ArgumentOutOfRangeException(nameof(destination));
        byte[] bytes = new byte[checked(destination.Length * sizeof(T))];
        _memory.CopyTo(bytes);
        if (bytes.Length != 0)
        {
            GCHandle pinned = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try { Marshal.Copy(bytes, 0, pinned.AddrOfPinnedObject(), bytes.Length); }
            finally { pinned.Free(); }
        }
    }

    /// <summary>在 stream 上异步复制到设备 / Copies asynchronously to the device on a stream.</summary>
    public void CopyFromAsync(T[] source, HipStream stream) => _memory.CopyFromAsync(ToBytes(source), stream);

    /// <summary>在 stream 上异步复制回主机 / Copies asynchronously back to the host on a stream.</summary>
    public void CopyToAsync(T[] destination, HipStream stream)
    {
        if (destination is null) throw new ArgumentNullException(nameof(destination));
        if ((ulong)destination.LongLength > ElementCount) throw new ArgumentOutOfRangeException(nameof(destination));
        byte[] bytes = new byte[checked(destination.Length * sizeof(T))];
        _memory.CopyToAsync(bytes, stream);
        stream.Synchronize();
        if (bytes.Length != 0)
        {
            GCHandle pinned = GCHandle.Alloc(destination, GCHandleType.Pinned);
            try { Marshal.Copy(bytes, 0, pinned.AddrOfPinnedObject(), bytes.Length); }
            finally { pinned.Free(); }
        }
    }

    /// <summary>释放视图及其设备内存 / Releases the view and its device memory.</summary>
    public void Dispose() => _memory.Dispose();

    private byte[] ToBytes(T[] values)
    {
        if (values is null) throw new ArgumentNullException(nameof(values));
        if ((ulong)values.LongLength > ElementCount) throw new ArgumentOutOfRangeException(nameof(values));
        var bytes = new byte[checked(values.Length * sizeof(T))];
        if (bytes.Length != 0)
        {
            GCHandle pinned = GCHandle.Alloc(values, GCHandleType.Pinned);
            try { Marshal.Copy(pinned.AddrOfPinnedObject(), bytes, 0, bytes.Length); }
            finally { pinned.Free(); }
        }
        return bytes;
    }
}
