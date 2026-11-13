using System;
using JYPPX.ROCm.HipSharp.Streams;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 为高级互操作及其异步借用提供可重试的原生资源所有权 / Shares retryable native ownership with advanced interop owners and asynchronous borrowers.
/// </summary>
internal sealed class HipInteropResource : IDisposable
{
    private readonly object _sync = new();
    private readonly Action<IntPtr> _release;
    private readonly IDisposable? _parent;
    private IntPtr _handle;
    private int _borrowers;
    private bool _disposeRequested;

    internal HipInteropResource(IntPtr handle, Action<IntPtr> release, IDisposable? parent = null)
    {
        _handle = handle;
        _release = release;
        _parent = parent;
    }

    internal bool IsDisposed { get { lock (_sync) return _disposeRequested; } }

    internal IntPtr GetHandle()
    {
        lock (_sync)
        {
            if (_disposeRequested) throw new ObjectDisposedException(nameof(HipInteropResource));
            return _handle;
        }
    }

    internal Borrow Acquire()
    {
        lock (_sync)
        {
            if (_disposeRequested) throw new ObjectDisposedException(nameof(HipInteropResource));
            var borrow = new Borrow(this, _handle);
            _borrowers = checked(_borrowers + 1);
            return borrow;
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _disposeRequested = true;
            if (_borrowers == 0) Release();
        }
    }

    private void Release()
    {
        if (_handle != IntPtr.Zero)
        {
            _release(_handle);
            _handle = IntPtr.Zero;
        }
        _parent?.Dispose();
    }

    private void ReleaseBorrow()
    {
        lock (_sync)
        {
            // Keep the last borrow retryable when native release or parent cleanup fails.
            if (_borrowers == 1 && _disposeRequested) Release();
            _borrowers--;
        }
    }

    internal sealed class Borrow : IDisposable
    {
        private readonly HipAsyncLease _lease;
        internal Borrow(HipInteropResource owner, IntPtr handle)
        {
            Handle = handle;
            _lease = new HipAsyncLease(owner.ReleaseBorrow);
        }
        internal IntPtr Handle { get; }
        public void Dispose() => _lease.Dispose();
    }
}
