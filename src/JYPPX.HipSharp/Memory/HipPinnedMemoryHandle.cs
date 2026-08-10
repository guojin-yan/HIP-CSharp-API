using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>
/// 通过 SafeHandle 释放 pinned host memory / Releases pinned host memory through SafeHandle.
/// </summary>
internal sealed class HipPinnedMemoryHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipPinnedMemoryHandle(IHipNativeApi nativeApi, IntPtr pointer)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.HostFree(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.HostFree(handle) == HipError.Success;
}
