using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Streams;

/// <summary>
/// 通过 SafeHandle 释放 HIP event / Releases a HIP event through SafeHandle.
/// </summary>
internal sealed class HipEventHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipEventHandle(IHipNativeApi nativeApi, IntPtr handle)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.EventDestroy(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.EventDestroy(handle) == HipError.Success;
}
