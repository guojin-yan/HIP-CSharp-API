using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>拥有 custom HIP memory pool 的原生句柄 / Owns the native handle of a custom HIP memory pool.</summary>
internal sealed class HipMemoryPoolHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _sync = new();

    internal HipMemoryPoolHandle(IHipNativeApi nativeApi, IntPtr handle)
        : base(IntPtr.Zero, true)
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
            HipError error = _nativeApi.MemPoolDestroy(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.MemPoolDestroy(handle) == HipError.Success;
}
