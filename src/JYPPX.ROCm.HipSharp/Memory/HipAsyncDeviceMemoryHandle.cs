using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>
/// 绑定分配 stream 的异步设备内存句柄 / Safe handle for device memory allocated on a specific stream.
/// </summary>
internal sealed class HipAsyncDeviceMemoryHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly IntPtr _stream;
    private readonly IDisposable _ownerLease;
    private readonly object _sync = new();

    internal HipAsyncDeviceMemoryHandle(IHipNativeApi nativeApi, IntPtr pointer, IntPtr stream, IDisposable ownerLease)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _stream = stream;
        _ownerLease = ownerLease;
        SetHandle(pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseAsyncChecked()
    {
        lock (_sync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.FreeAsync(handle, _stream);
            if (error == HipError.Success)
            {
                SetHandle(IntPtr.Zero);
                SetHandleAsInvalid();
                _ownerLease.Dispose();
            }
            return error;
        }
    }

    protected override bool ReleaseHandle()
    {
        bool released = _nativeApi.FreeAsync(handle, _stream) == HipError.Success;
        if (released) _ownerLease.Dispose();
        return released;
    }
}
