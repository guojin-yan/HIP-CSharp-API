using System;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Memory;

/// <summary>保持 pool 和 stream 至 ordered free 完成的 pooled allocation 句柄 / Pooled allocation handle that retains its pool and stream until ordered free completion.</summary>
internal sealed class HipPooledDeviceMemoryHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipStream _stream;
    private readonly IDisposable _streamOwner;
    private readonly IDisposable _poolChild;
    private readonly object _sync = new();

    internal HipPooledDeviceMemoryHandle(IHipNativeApi nativeApi, IntPtr pointer, HipStream stream, IDisposable streamOwner, IDisposable poolChild)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        _stream = stream;
        _streamOwner = streamOwner;
        _poolChild = poolChild;
        SetHandle(pointer);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipError ReleaseAsyncChecked()
    {
        lock (_sync)
        {
            if (IsClosed || IsInvalid) return HipError.Success;
            HipError error = _nativeApi.FreeAsync(handle, _stream.DangerousGetHandle());
            if (error != HipError.Success) return error;
            QueueCompletionLease();
            SetHandle(IntPtr.Zero);
            SetHandleAsInvalid();
            return HipError.Success;
        }
    }

    protected override bool ReleaseHandle()
    {
        try
        {
            if (_nativeApi.FreeAsync(handle, _stream.DangerousGetHandle()) != HipError.Success) return false;
            QueueCompletionLease();
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void QueueCompletionLease()
    {
        _stream.AddPendingLease(new HipAsyncLease(_poolChild.Dispose));
        _streamOwner.Dispose();
    }
}
