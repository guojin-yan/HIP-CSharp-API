using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Streams;

/// <summary>
/// 通过 SafeHandle 释放 HIP stream / Releases a HIP stream through SafeHandle.
/// </summary>
internal sealed class HipStreamHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipStreamHandle(IHipNativeApi nativeApi, IntPtr stream)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(stream);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.StreamDestroy(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.StreamDestroy(handle) == HipError.Success;
}
