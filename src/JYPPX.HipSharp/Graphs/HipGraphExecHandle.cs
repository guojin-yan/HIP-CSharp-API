using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>
/// HIP graph executable 的安全句柄 / Safe handle for an instantiated HIP graph executable.
/// </summary>
internal sealed class HipGraphExecHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _sync = new();
    private IDisposable? _captureReference;
    private bool _nativeReleased;

    internal HipGraphExecHandle(IHipNativeApi nativeApi, IntPtr handle, IDisposable? captureReference = null) : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _captureReference = captureReference;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_sync)
        {
            if (IsClosed) return HipError.Success;
            if (!_nativeReleased)
            {
                HipError error = _nativeApi.GraphExecDestroy(handle);
                if (error != HipError.Success) return error;
                _nativeReleased = true;
            }

            if (_captureReference is not null)
            {
                _captureReference.Dispose();
                _captureReference = null;
            }

            SetHandle(IntPtr.Zero);
            SetHandleAsInvalid();
            return HipError.Success;
        }
    }

    protected override bool ReleaseHandle()
    {
        lock (_sync)
        {
            bool released = _nativeReleased || IsInvalid || _nativeApi.GraphExecDestroy(handle) == HipError.Success;
            if (!released) return false;
            _nativeReleased = true;
            try
            {
                _captureReference?.Dispose();
                _captureReference = null;
                SetHandle(IntPtr.Zero);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
