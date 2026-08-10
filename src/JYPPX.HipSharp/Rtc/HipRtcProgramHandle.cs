using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;

namespace JYPPX.HipSharp.Rtc;

/// <summary>
/// 通过 SafeHandle 为 HIPRTC program 提供最终释放保障 / Provides final-release protection for a HIPRTC program through SafeHandle.
/// </summary>
internal sealed class HipRtcProgramHandle : SafeHandle
{
    private readonly IHipRtcNativeApi _nativeApi;
    private readonly object _releaseSync = new();

    internal HipRtcProgramHandle(IHipRtcNativeApi nativeApi, IntPtr program)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(program);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipRtcResult ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid)
            {
                return HipRtcResult.Success;
            }

            IntPtr program = handle;
            HipRtcResult result = _nativeApi.DestroyProgram(ref program);
            if (result == HipRtcResult.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }

            return result;
        }
    }

    protected override bool ReleaseHandle()
    {
        IntPtr program = handle;
        return _nativeApi.DestroyProgram(ref program) == HipRtcResult.Success;
    }
}
