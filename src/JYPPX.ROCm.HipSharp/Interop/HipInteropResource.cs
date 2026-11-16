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

    private void ReleaseBorrow(ref bool returned)
    {
        lock (_sync)
        {
            // Returning a borrow and destroying native state are separate steps.
            // Failed rollback can lose the borrow object; the owner must still be
            // able to retry cleanup. A retained borrow can also retry without
            // decrementing the borrower count twice.
            if (!returned)
            {
                _borrowers--;
                returned = true;
            }
            if (_borrowers == 0 && _disposeRequested) Release();
        }
    }

    internal sealed class Borrow : IDisposable
    {
        private readonly HipInteropResource _owner;
        private readonly HipAsyncLease _lease;
        private bool _returned;
        internal Borrow(HipInteropResource owner, IntPtr handle)
        {
            _owner = owner;
            Handle = handle;
            _lease = new HipAsyncLease(Return);
        }
        internal IntPtr Handle { get; }
        public void Dispose() => _lease.Dispose();
        private void Return() => _owner.ReleaseBorrow(ref _returned);
    }
}
