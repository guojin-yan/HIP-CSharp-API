using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Streams;

/// <summary>
/// 拥有 HIP event 并记录所属 stream/client 关系 / Owns a HIP event and records its stream/client relationship.
/// </summary>
public sealed class HipEvent : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipEventHandle _handle;
    private readonly object _sync = new();
    private HipStream? _recordedStream;

    internal HipEvent(IHipNativeApi nativeApi, IntPtr handle, HipEventFlags flags)
    {
        _nativeApi = nativeApi;
        _handle = new HipEventHandle(nativeApi, handle);
        Flags = flags;
    }

    /// <summary>获取创建标志 / Gets the creation flags.</summary>
    public HipEventFlags Flags { get; }

    /// <summary>获取 event 是否已释放 / Gets whether the event is disposed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>在指定 stream 记录 event / Records the event on a stream.</summary>
    /// <param name="stream">所属 stream / Owning stream.</param>
    /// <exception cref="ArgumentNullException">stream 为 null / stream is null.</exception>
    /// <exception cref="ArgumentException">stream 来自其他 Runtime / stream belongs to another Runtime.</exception>
    public void Record(HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Event and stream belong to different HIP Runtime clients.", nameof(stream));
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.EventRecord(_handle.DangerousGetHandle(), stream.DangerousGetHandle()), "hipEventRecord");
            _recordedStream = stream;
        }
    }

    /// <summary>等待 event 完成 / Waits for the event to complete.</summary>
    public void Synchronize()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.EventSynchronize(_handle.DangerousGetHandle()), "hipEventSynchronize");
        }
    }

    /// <summary>查询 event 是否完成 / Queries whether the event is complete.</summary>
    public bool Query()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            HipError error = _nativeApi.EventQuery(_handle.DangerousGetHandle());
            if (error == HipError.Success) return true;
            if (error == HipError.NotReady) return false;
            HipCall.ThrowIfFailed(_nativeApi, error, "hipEventQuery");
            return false;
        }
    }

    /// <summary>返回两个已完成 event 的毫秒差 / Returns elapsed milliseconds between two completed events.</summary>
    public static float ElapsedTime(HipEvent start, HipEvent end)
    {
        if (start is null) throw new ArgumentNullException(nameof(start));
        if (end is null) throw new ArgumentNullException(nameof(end));
        if (!ReferenceEquals(start._nativeApi, end._nativeApi)) throw new ArgumentException("Events belong to different HIP Runtime clients.");
        lock (start._sync)
        {
            start.ThrowIfDisposed();
            lock (end._sync)
            {
                end.ThrowIfDisposed();
                HipCall.ThrowIfFailed(start._nativeApi, start._nativeApi.EventElapsedTime(out float milliseconds, start._handle.DangerousGetHandle(), end._handle.DangerousGetHandle()), "hipEventElapsedTime");
                return milliseconds;
            }
        }
    }

    /// <summary>释放 event；未完成 event 会先同步 / Disposes the event; an incomplete event is synchronized first.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (IsDisposed) return;
            HipError syncError = _nativeApi.EventSynchronize(_handle.DangerousGetHandle());
            if (syncError != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, syncError, "hipEventSynchronize");
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipEventDestroy");
            _handle.Dispose();
            _recordedStream = null;
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipEvent));
    }
}
