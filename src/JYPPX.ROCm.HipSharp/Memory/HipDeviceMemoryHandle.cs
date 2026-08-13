using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>
/// 通过 SafeHandle 为 HIP 设备分配提供最终释放保障 / Provides final-release protection for a HIP device allocation through SafeHandle.
/// </summary>
internal sealed class HipDeviceMemoryHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipDeviceMemoryHandle(IHipNativeApi nativeApi, IntPtr pointer)
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
            if (IsClosed || IsInvalid)
            {
                return HipError.Success;
            }

            HipError error = _nativeApi.Free(handle);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }

            return error;
        }
    }

    protected override bool ReleaseHandle() => _nativeApi.Free(handle) == HipError.Success;
}
