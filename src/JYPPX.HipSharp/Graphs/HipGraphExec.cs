using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>
/// 拥有可启动的 HIP graph executable / Owns a launchable HIP graph executable.
/// </summary>
public sealed class HipGraphExec : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphExecHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipGraphExec(IHipNativeApi nativeApi, IntPtr handle, IDisposable? captureReference = null)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("A HIP graph executable handle cannot be null.", nameof(handle));
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _handle = new HipGraphExecHandle(nativeApi, handle, captureReference);
    }

    /// <summary>获取 executable 是否已释放 / Gets whether the executable is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>
    /// 在指定 stream 上启动 executable；stream 完成前 executable 保持有效 / Launches on a stream while the executable remains valid until stream completion.
    /// </summary>
    /// <param name="stream">目标 stream / Target stream.</param>
    /// <exception cref="ArgumentNullException">stream 为 null / stream is null.</exception>
    /// <exception cref="ArgumentException">资源来自不同 Runtime / Resources belong to different runtimes.</exception>
    /// <exception cref="HipException">启动失败 / Launch fails.</exception>
    public void Launch(HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Graph executable and stream belong to different HIP Runtime clients.", nameof(stream));
        bool addedReference = false;
        try
        {
            lock (_lifetimeSync)
            {
                ThrowIfDisposed();
                _handle.DangerousAddRef(ref addedReference);
                if (!addedReference) throw new ObjectDisposedException(nameof(HipGraphExec));
                _asyncReferences++;
            }
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphLaunch(_handle.DangerousGetHandle(), stream.DangerousGetHandle()), "hipGraphLaunch");
            stream.AddPendingLease(new HipAsyncLease(ReleaseAsyncReference));
            addedReference = false;
        }
        finally
        {
            if (addedReference) ReleaseAsyncReference();
        }
    }

    /// <summary>释放 executable；重复调用安全 / Disposes the executable; repeated calls are safe.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed) return;
            _disposeRequested = true;
            if (_asyncReferences != 0)
            {
                return;
            }
        }
        HipError error = _handle.ReleaseChecked();
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphExecDestroy");
        _handle.Dispose();
    }

    private void ReleaseAsyncReference()
    {
        bool releaseChecked;
        lock (_lifetimeSync)
        {
            if (_asyncReferences > 0)
            {
                _handle.DangerousRelease();
                _asyncReferences--;
            }
            releaseChecked = _disposeRequested && _asyncReferences == 0;
        }

        if (releaseChecked)
        {
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphExecDestroy");
            _handle.Dispose();
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipGraphExec));
    }
}
