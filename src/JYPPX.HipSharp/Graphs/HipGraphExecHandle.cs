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

    internal HipGraphExecHandle(IHipNativeApi nativeApi, IntPtr handle) : base(IntPtr.Zero, true)
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
            HipError error = _nativeApi.GraphExecDestroy(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.GraphExecDestroy(handle) == HipError.Success;
}
