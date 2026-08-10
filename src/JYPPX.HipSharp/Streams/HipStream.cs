using System;
using System.Collections.Generic;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Streams;

/// <summary>
/// 拥有 HIP stream，并在同步或完成查询后释放异步 leases / Owns a HIP stream and releases async leases after synchronization or completion queries.
/// </summary>
public sealed class HipStream : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStreamHandle _handle;
    private readonly object _sync = new();
    private readonly List<IDisposable> _pending = new();

    internal HipStream(IHipNativeApi nativeApi, IntPtr handle, HipStreamFlags flags)
    {
        _nativeApi = nativeApi;
        _handle = new HipStreamHandle(nativeApi, handle);
        Flags = flags;
    }

    /// <summary>获取创建标志 / Gets the creation flags.</summary>
    public HipStreamFlags Flags { get; }

    /// <summary>获取 stream 是否已释放 / Gets whether the stream is disposed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>
    /// 等待 stream 完成并释放所有 pending leases / Waits for completion and releases all pending leases.
    /// </summary>
    /// <exception cref="HipException">同步失败 / Synchronization fails.</exception>
    public void Synchronize()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamSynchronize(_handle.DangerousGetHandle()), "hipStreamSynchronize");
            ClearPending();
        }
    }

    /// <summary>
    /// 非阻塞查询 stream；完成时释放 leases / Queries without blocking and releases leases when complete.
    /// </summary>
    /// <returns>是否已完成 / Whether the stream has completed.</returns>
    /// <exception cref="HipException">查询失败且不是 NotReady / The query failed with an error other than NotReady.</exception>
    public bool Query()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            HipError error = _nativeApi.StreamQuery(_handle.DangerousGetHandle());
            if (error == HipError.Success) { ClearPending(); return true; }
            if (error == HipError.NotReady) return false;
            HipCall.ThrowIfFailed(_nativeApi, error, "hipStreamQuery");
            return false;
        }
    }

    /// <summary>释放 stream；显式释放会报告 native 错误，终结器路径不会抛出 / Disposes the stream; explicit disposal reports native errors while finalization never throws.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (IsDisposed) return;
            HipError syncError = _nativeApi.StreamSynchronize(_handle.DangerousGetHandle());
            if (syncError != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, syncError, "hipStreamSynchronize");
            ClearPending();
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipStreamDestroy");
            _handle.Dispose();
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;
    internal IntPtr DangerousGetHandle() { lock (_sync) { ThrowIfDisposed(); return _handle.DangerousGetHandle(); } }

    internal void AddPendingLease(IDisposable lease)
    {
        if (lease is null) throw new ArgumentNullException(nameof(lease));
        lock (_sync)
        {
            if (IsDisposed) { lease.Dispose(); throw new ObjectDisposedException(nameof(HipStream)); }
            _pending.Add(lease);
        }
    }

    private void ClearPending()
    {
        for (int index = _pending.Count - 1; index >= 0; index--) _pending[index].Dispose();
        _pending.Clear();
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipStream));
    }
}
