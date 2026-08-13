using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>拥有按指定 stream 分配的设备内存；释放严格排入同一 stream / Owns device memory allocated on a specific stream; release is ordered on that same stream.</summary>
public sealed class HipAsyncDeviceMemory : IDisposable, IHipPointerOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStreamOrderedMemoryCore _core;

    internal HipAsyncDeviceMemory(IHipNativeApi nativeApi, IntPtr pointer, ulong byteLength, HipStream allocationStream, IDisposable ownerLease)
    {
        _nativeApi = nativeApi;
        var handle = new HipAsyncDeviceMemoryHandle(nativeApi, pointer, allocationStream.DangerousGetHandle(), ownerLease);
        _core = new HipStreamOrderedMemoryCore(nativeApi, handle, handle.ReleaseAsyncChecked, byteLength, allocationStream, nameof(HipAsyncDeviceMemory));
    }

    /// <summary>获取分配字节数 / Gets the allocation size in bytes.</summary>
    public ulong ByteLength => _core.ByteLength;

    /// <summary>获取创建和释放顺序所绑定的 stream / Gets the stream that owns allocation and release ordering.</summary>
    public HipStream AllocationStream => _core.AllocationStream;

    /// <summary>获取分配所在设备的序号 / Gets the ordinal of the device on which the allocation was created.</summary>
    public int DeviceOrdinal => AllocationStream.DeviceOrdinal;

    /// <summary>获取资源是否已释放 / Gets whether the resource is disposed.</summary>
    public bool IsDisposed => _core.IsDisposed;

    /// <summary>获取原生指针但不转移所有权 / Gets the native pointer without transferring ownership.</summary>
    public IntPtr DangerousGetHandle() => _core.DangerousGetHandle();

    /// <summary>在创建 stream 上异步复制到设备 / Asynchronously copies to the device on the allocation stream.</summary>
    public void CopyFromAsync(byte[] source) => _core.CopyFromAsync(source);

    /// <summary>在创建 stream 上异步复制回主机 / Asynchronously copies to the host on the allocation stream.</summary>
    public void CopyToAsync(byte[] destination) => _core.CopyToAsync(destination);

    /// <summary>将释放操作排入创建 stream；失败时保留 owner 以便重试 / Enqueues release on the allocation stream; failures preserve ownership for retry.</summary>
    public void Dispose() => _core.Dispose();

    internal IHipNativeApi NativeApi => _nativeApi;
    internal IntPtr AcquirePointer(out bool addedReference) => _core.AcquirePointer(out addedReference);
    internal void ReleasePointer() => _core.ReleasePointer();

    IHipNativeApi IHipPointerOwner.NativeApi => _nativeApi;
    int? IHipPointerOwner.DeviceOrdinal => DeviceOrdinal;
    HipStream? IHipPointerOwner.RequiredStream => AllocationStream;
    IntPtr IHipPointerOwner.AcquirePointer(out bool addedReference) => AcquirePointer(out addedReference);
    void IHipPointerOwner.ReleasePointer() => ReleasePointer();
}
