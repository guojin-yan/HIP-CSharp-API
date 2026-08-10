using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Modules;

/// <summary>
/// 通过 SafeHandle 为 HIP module 提供最终释放保障 / Provides final-release protection for a HIP module through SafeHandle.
/// </summary>
internal sealed class HipModuleHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipModuleHandle(IHipNativeApi nativeApi, IntPtr module)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(module);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid)
            {
                return HipError.Success;
            }

            HipError error = _nativeApi.ModuleUnload(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }

            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.ModuleUnload(handle) == HipError.Success;
}
