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

    internal HipGraphHandle(IHipNativeApi nativeApi, IntPtr handle) : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_sync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.GraphDestroy(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.GraphDestroy(handle) == HipError.Success;
}
