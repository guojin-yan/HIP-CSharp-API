using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;

namespace JYPPX.HipSharp.Memory;

/// <summary>拥有从指定 memory pool 和 stream 分配的设备内存 / Owns device memory allocated from a specific memory pool and stream.</summary>
public sealed class HipPooledDeviceMemory : IDisposable, IHipPointerOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStreamOrderedMemoryCore _core;

    internal HipPooledDeviceMemory(IHipNativeApi nativeApi, HipPooledDeviceMemoryHandle handle, ulong byteLength, HipStream allocationStream, HipMemoryPool pool)
    {
        _nativeApi = nativeApi;
        Pool = pool;
        _core = new HipStreamOrderedMemoryCore(nativeApi, handle, handle.ReleaseAsyncChecked, byteLength, allocationStream, nameof(HipPooledDeviceMemory));
    }

    /// <summary>获取分配字节数 / Gets the allocation size in bytes.</summary>
    public ulong ByteLength => _core.ByteLength;

    /// <summary>获取创建 allocation 的 memory pool / Gets the memory pool that created the allocation.</summary>
    public HipMemoryPool Pool { get; }

    /// <summary>获取分配和释放顺序绑定的 stream / Gets the stream that orders allocation and release.</summary>
    public HipStream AllocationStream => _core.AllocationStream;

    /// <summary>获取 allocation 所在设备序号 / Gets the allocation device ordinal.</summary>
    public int DeviceOrdinal => AllocationStream.DeviceOrdinal;

    /// <summary>获取 allocation 是否已请求或完成释放 / Gets whether release has been requested or completed.</summary>
    public bool IsDisposed => _core.IsDisposed;

    /// <summary>获取原生指针但不转移所有权 / Gets the native pointer without transferring ownership.</summary>
    public IntPtr DangerousGetHandle() => _core.DangerousGetHandle();

    /// <summary>在 allocation stream 上异步复制到设备 / Asynchronously copies to the device on the allocation stream.</summary>
    public void CopyFromAsync(byte[] source) => _core.CopyFromAsync(source);

    /// <summary>在 allocation stream 上异步复制回主机 / Asynchronously copies to the host on the allocation stream.</summary>
    public void CopyToAsync(byte[] destination) => _core.CopyToAsync(destination);

    /// <summary>将释放排入 allocation stream；pool 在完成前保持存活 / Enqueues release on the allocation stream; the pool remains alive until completion.</summary>
    public void Dispose() => _core.Dispose();

    IHipNativeApi IHipPointerOwner.NativeApi => _nativeApi;
    HipStream? IHipPointerOwner.RequiredStream => AllocationStream;
    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => _core.AcquirePointer(out addedReference);
    void IHipPointerOwner.ReleasePointer() => _core.ReleasePointer();
}
