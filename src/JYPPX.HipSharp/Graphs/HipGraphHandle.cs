using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>
/// HIP graph 的安全句柄 / Safe handle for a HIP graph.
/// </summary>
internal sealed class HipGraphHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _sync = new();
    private HipGraphCaptureResources? _captureResources;
    private bool _nativeReleased;

    internal HipGraphHandle(IHipNativeApi nativeApi, IntPtr handle, HipGraphCaptureResources? captureResources = null) : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _captureResources = captureResources;
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
                HipError error = _nativeApi.GraphDestroy(handle);
                if (error != HipError.Success) return error;
                _nativeReleased = true;
            }

            if (_captureResources is not null)
            {
                _captureResources.ReleaseInitialReference();
                _captureResources = null;
            }

            SetHandle(IntPtr.Zero);
            SetHandleAsInvalid();
            return HipError.Success;
        }
    }

    internal IDisposable? AcquireCaptureReference()
    {
        lock (_sync)
        {
            if (IsClosed || IsInvalid || _nativeReleased) throw new ObjectDisposedException(nameof(HipGraphHandle));
            return _captureResources?.AcquireReference();
        }
    }

    protected override bool ReleaseHandle()
    {
        lock (_sync)
        {
            bool released = _nativeReleased || IsInvalid || _nativeApi.GraphDestroy(handle) == HipError.Success;
            if (!released) return false;
            _nativeReleased = true;
            try
            {
                _captureResources?.ReleaseInitialReference();
                _captureResources = null;
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
