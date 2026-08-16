using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;

namespace JYPPX.ROCm.HipSharp.Rtc;

/// <summary>拥有 HIPRTC link state 及其调用期输入副本 / Owns a HIPRTC link state and its call-lifetime input copies.</summary>
internal sealed class HipRtcLinkStateHandle : SafeHandle
{
    private readonly IHipRtcNativeApi _nativeApi;
    private readonly List<IntPtr> _inputBuffers = new();
    private readonly object _releaseSync = new();

    internal HipRtcLinkStateHandle(IHipRtcNativeApi nativeApi, IntPtr linkState)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        SetHandle(linkState);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal void TrackInputBuffer(IntPtr buffer)
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid)
            {
                throw new ObjectDisposedException(nameof(HipRtcLinker));
            }

            _inputBuffers.Add(buffer);
        }
    }

    internal HipRtcResult ReleaseChecked()
    {
        lock (_releaseSync)
        {
            if (IsClosed || IsInvalid)
            {
                return HipRtcResult.Success;
            }

            HipRtcResult result = _nativeApi.LinkDestroy(handle);
            if (result == HipRtcResult.Success)
            {
                FreeInputBuffers();
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
            }

            return result;
        }
    }

    protected override bool ReleaseHandle()
    {
        HipRtcResult result = _nativeApi.LinkDestroy(handle);
        FreeInputBuffers();
        return result == HipRtcResult.Success;
    }

    private void FreeInputBuffers()
    {
        foreach (IntPtr buffer in _inputBuffers)
        {
            Marshal.FreeHGlobal(buffer);
        }

        _inputBuffers.Clear();
    }
}
