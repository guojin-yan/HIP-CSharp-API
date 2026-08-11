using System;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>
/// 拥有由 stream capture 产生的 HIP graph / Owns a HIP graph produced by stream capture.
/// </summary>
public sealed class HipGraph : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphHandle _handle;
    private readonly object _lifetimeSync = new();
    private bool _disposeRequested;

    internal HipGraph(IHipNativeApi nativeApi, IntPtr handle, HipGraphCaptureResources captureResources)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("A HIP graph handle cannot be null.", nameof(handle));
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _handle = new HipGraphHandle(nativeApi, handle, captureResources ?? throw new ArgumentNullException(nameof(captureResources)));
    }

    /// <summary>获取 graph 是否已释放 / Gets whether the graph is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>
    /// 创建 graph executable；graph 与 executable 是独立 owner / Creates a graph executable; the graph and executable are independent owners.
    /// </summary>
    /// <param name="flags">官方 graph instantiate flags；当前只接受零 / Official graph instantiate flags; zero is currently required.</param>
    /// <returns>拥有 executable 的对象 / An object owning the executable.</returns>
    public HipGraphExec Instantiate(ulong flags = 0)
    {
        if (flags != 0) throw new ArgumentOutOfRangeException(nameof(flags));
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            IDisposable? captureReference = _handle.AcquireCaptureReference();
            try
            {
                HipError error = _nativeApi.GraphInstantiateWithFlags(out IntPtr executable, _handle.DangerousGetHandle(), flags);
                if (error != HipError.Success && executable != IntPtr.Zero)
                {
                    var partialHandle = new HipGraphExecHandle(_nativeApi, executable, null);
                    if (partialHandle.ReleaseChecked() == HipError.Success) partialHandle.Dispose();
                }
                HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphInstantiateWithFlags");
                if (executable == IntPtr.Zero) throw new InvalidOperationException("hipGraphInstantiateWithFlags succeeded but returned a null executable.");
                var result = new HipGraphExec(_nativeApi, executable, captureReference);
                captureReference = null;
                return result;
            }
            finally
            {
                captureReference?.Dispose();
            }
        }
    }

    /// <summary>释放 graph；重复调用安全 / Disposes the graph; repeated calls are safe.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            _disposeRequested = true;
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphDestroy");
            _handle.Dispose();
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;
    internal IntPtr DangerousGetHandle()
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipGraph));
    }
}
