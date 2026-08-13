using System;
using System.Collections.Generic;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Streams;

/// <summary>
/// 拥有 HIP stream，并在同步或完成查询后释放异步 leases / Owns a HIP stream and releases async leases after synchronization or completion queries.
/// </summary>
public sealed class HipStream : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStreamHandle _handle;
    private readonly object _sync = new();
    private readonly List<IDisposable> _pending = new();
    private List<IDisposable>? _captureLeases;
    private int _ownedResourceCount;
    private bool _captureActive;

    internal HipStream(IHipNativeApi nativeApi, IntPtr handle, HipStreamFlags flags, int deviceOrdinal)
    {
        _nativeApi = nativeApi;
        _handle = new HipStreamHandle(nativeApi, handle);
        Flags = flags;
        DeviceOrdinal = deviceOrdinal;
    }

    /// <summary>获取创建标志 / Gets the creation flags.</summary>
    public HipStreamFlags Flags { get; }

    /// <summary>获取创建 stream 的设备序号 / Gets the ordinal of the device on which the stream was created.</summary>
    public int DeviceOrdinal { get; }

    /// <summary>获取 stream 是否已释放 / Gets whether the stream is disposed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取 stream 是否处于 graph capture / Gets whether the stream is in graph capture.</summary>
    public bool IsCapturing { get { lock (_sync) return _captureActive; } }

    /// <summary>开始 graph capture / Begins graph capture.</summary>
    public void BeginCapture(HipStreamCaptureMode mode = HipStreamCaptureMode.Global)
    {
        if (mode < HipStreamCaptureMode.Global || mode > HipStreamCaptureMode.Relaxed)
        {
            throw new ArgumentOutOfRangeException(nameof(mode));
        }
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_captureActive) throw new InvalidOperationException("This stream is already capturing a graph.");
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamBeginCapture(_handle.DangerousGetHandle(), mode), "hipStreamBeginCapture");
            _captureLeases = new List<IDisposable>();
            _captureActive = true;
        }
    }

    /// <summary>结束 graph capture 并转移 graph 所有权 / Ends graph capture and transfers graph ownership.</summary>
    public HipGraph EndCapture()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!_captureActive) throw new InvalidOperationException("This stream is not capturing a graph.");
            try
            {
                HipError error = _nativeApi.StreamEndCapture(_handle.DangerousGetHandle(), out IntPtr graph);
                if (error != HipError.Success && graph != IntPtr.Zero)
                {
                    var partialHandle = new HipGraphHandle(_nativeApi, graph);
                    if (partialHandle.ReleaseChecked() == HipError.Success) partialHandle.Dispose();
                }
                if (error != HipError.Success)
                {
                    MoveCaptureLeasesToPending();
                }
                HipCall.ThrowIfFailed(_nativeApi, error, "hipStreamEndCapture");
                if (graph == IntPtr.Zero)
                {
                    MoveCaptureLeasesToPending();
                    throw new InvalidOperationException("hipStreamEndCapture succeeded but returned a null graph.");
                }

                List<IDisposable> captureLeases = _captureLeases ?? new List<IDisposable>();
                _captureLeases = null;
                return new HipGraph(_nativeApi, graph, new HipGraphResources(captureLeases), HipGraphKind.Captured, DeviceOrdinal);
            }
            finally
            {
                _captureActive = false;
            }
        }
    }

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
            while (ClearPending())
            {
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamSynchronize(_handle.DangerousGetHandle()), "hipStreamSynchronize");
            }
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
            if (error == HipError.Success)
            {
                while (ClearPending())
                {
                    HipError afterRelease = _nativeApi.StreamQuery(_handle.DangerousGetHandle());
                    if (afterRelease == HipError.NotReady) return false;
                    HipCall.ThrowIfFailed(_nativeApi, afterRelease, "hipStreamQuery");
                }
                return true;
            }
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
            if (_ownedResourceCount != 0)
            {
                throw new InvalidOperationException("The stream owns active stream-ordered resources; dispose those resources before disposing the stream.");
            }
            if (_captureActive)
            {
                throw new InvalidOperationException("End graph capture before disposing the stream.");
            }
            HipError syncError = _nativeApi.StreamSynchronize(_handle.DangerousGetHandle());
            if (syncError != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, syncError, "hipStreamSynchronize");
            while (ClearPending())
            {
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamSynchronize(_handle.DangerousGetHandle()), "hipStreamSynchronize");
            }
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
            if (_captureActive)
            {
                (_captureLeases ??= new List<IDisposable>()).Add(lease);
            }
            else
            {
                _pending.Add(lease);
            }
        }
    }

    internal IDisposable RegisterOwnedResource()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            _ownedResourceCount++;
            return new OwnerLease(this);
        }
    }

    private void ReleaseOwnedResource()
    {
        lock (_sync)
        {
            if (_ownedResourceCount > 0) _ownedResourceCount--;
        }
    }

    private sealed class OwnerLease : IDisposable
    {
        private HipStream? _stream;
        internal OwnerLease(HipStream stream) => _stream = stream;
        public void Dispose()
        {
            HipStream? stream = System.Threading.Interlocked.Exchange(ref _stream, null);
            stream?.ReleaseOwnedResource();
        }
    }

    private bool ClearPending()
    {
        bool hadPending = _pending.Count != 0;
        for (int index = _pending.Count - 1; index >= 0; index--)
        {
            _pending[index].Dispose();
            _pending.RemoveAt(index);
        }
        return hadPending;
    }

    private void MoveCaptureLeasesToPending()
    {
        if (_captureLeases is null) return;
        _pending.AddRange(_captureLeases);
        _captureLeases = null;
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipStream));
    }
}
