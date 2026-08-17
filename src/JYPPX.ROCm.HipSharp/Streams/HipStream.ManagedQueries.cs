using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Streams;

/// <summary>提供 stream 查询与等待操作 / Provides stream query and wait operations.</summary>
public sealed partial class HipStream
{
    /// <summary>获取该值 / Gets the stream compute-unit mask as native 32-bit words.</summary>
    public unsafe uint[] GetComputeUnitMask(uint wordCount)
    {
        if (wordCount == 0) throw new ArgumentOutOfRangeException(nameof(wordCount));
        if (wordCount > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(wordCount));
        int count = checked((int)wordCount);
        lock (_sync)
        {
            ThrowIfDisposed();
            var result = new uint[count];
            fixed (uint* pointer = result)
            {
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.ExtStreamGetCUMask(_handle.DangerousGetHandle(), wordCount, (IntPtr)pointer), "hipExtStreamGetCUMask");
            }
            return result;
        }
    }

    /// <summary>获取该值 / Gets a raw hipStreamAttrValue union for a native attribute identifier.</summary>
    public unsafe HipStreamAttributeValue GetAttribute(int nativeAttribute)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            HipStreamAttributeValue value = default;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetAttribute(_handle.DangerousGetHandle(), nativeAttribute, (IntPtr)(&value)), "hipStreamGetAttribute");
            return value;
        }
    }

    /// <summary>获取该值 / Gets the current stream-capture status and identifier.</summary>
    public unsafe HipStreamCaptureInfo GetCaptureInfo()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            int status = 0;
            ulong identifier = 0;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetCaptureInfo(_handle.DangerousGetHandle(), (IntPtr)(&status), (IntPtr)(&identifier)), "hipStreamGetCaptureInfo");
            return new HipStreamCaptureInfo((HipStreamCaptureStatus)status, identifier);
        }
    }

    /// <summary>获取该值 / Gets extended capture information. Returned graph and dependency handles are borrowed.</summary>
    public unsafe HipStreamCaptureInfoV2 GetCaptureInfoV2()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            int status = 0;
            ulong identifier = 0;
            IntPtr graph = IntPtr.Zero;
            IntPtr dependencies = IntPtr.Zero;
            UIntPtr dependencyCount = UIntPtr.Zero;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetCaptureInfoV2(_handle.DangerousGetHandle(), (IntPtr)(&status), (IntPtr)(&identifier), (IntPtr)(&graph), (IntPtr)(&dependencies), (IntPtr)(&dependencyCount)), "hipStreamGetCaptureInfo_v2");
            return new HipStreamCaptureInfoV2((HipStreamCaptureStatus)status, identifier, graph, dependencies, dependencyCount.ToUInt64());
        }
    }

    /// <summary>获取该值 / Gets the native device ordinal associated with this stream.</summary>
    public unsafe int GetNativeDeviceOrdinal()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            int device = -1;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetDevice(_handle.DangerousGetHandle(), (IntPtr)(&device)), "hipStreamGetDevice");
            if (device < 0) throw new InvalidOperationException("hipStreamGetDevice succeeded but returned a negative ordinal.");
            return device;
        }
    }

    /// <summary>获取该值 / Gets the native flags currently reported by HIP for this stream.</summary>
    public unsafe HipStreamFlags GetNativeFlags()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            uint flags = 0;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetFlags(_handle.DangerousGetHandle(), (IntPtr)(&flags)), "hipStreamGetFlags");
            return (HipStreamFlags)flags;
        }
    }

    /// <summary>获取该值 / Gets the native stream identifier.</summary>
    public unsafe ulong GetNativeIdentifier()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            ulong identifier = 0;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetId(_handle.DangerousGetHandle(), (IntPtr)(&identifier)), "hipStreamGetId");
            return identifier;
        }
    }

    /// <summary>获取该值 / Gets the native stream priority.</summary>
    public unsafe int GetNativePriority()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            int priority = 0;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamGetPriority(_handle.DangerousGetHandle(), (IntPtr)(&priority)), "hipStreamGetPriority");
            return priority;
        }
    }

    /// <summary>说明该托管接口 / Enqueues a wait for an event owned by the same runtime client.</summary>
    public void Wait(HipEvent eventToWaitFor)
    {
        if (eventToWaitFor is null) throw new ArgumentNullException(nameof(eventToWaitFor));
        if (!ReferenceEquals(_nativeApi, eventToWaitFor.NativeApi)) throw new ArgumentException("Event and stream belong to different HIP Runtime clients.", nameof(eventToWaitFor));
        IntPtr eventHandle = eventToWaitFor.DangerousGetHandle();
        lock (_sync)
        {
            ThrowIfDisposed();
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamWaitEvent(_handle.DangerousGetHandle(), eventHandle, 0), "hipStreamWaitEvent");
        }
    }

    /// <summary>说明该托管接口 / Enqueues a 32-bit memory-value wait. The caller retains the native pointer lifetime.</summary>
    public void WaitValue32(IntPtr nativeAddress, uint value, HipStreamWaitValueFlags flags = HipStreamWaitValueFlags.Equal, uint mask = uint.MaxValue)
    {
        if (nativeAddress == IntPtr.Zero) throw new ArgumentException("A non-null native pointer is required.", nameof(nativeAddress));
        lock (_sync)
        {
            ThrowIfDisposed();
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamWaitValue32(_handle.DangerousGetHandle(), nativeAddress, value, (uint)flags, mask), "hipStreamWaitValue32");
        }
    }

    /// <summary>说明该托管接口 / Enqueues a 64-bit memory-value wait. The caller retains the native pointer lifetime.</summary>
    public void WaitValue64(IntPtr nativeAddress, ulong value, HipStreamWaitValueFlags flags = HipStreamWaitValueFlags.Equal, ulong mask = ulong.MaxValue)
    {
        if (nativeAddress == IntPtr.Zero) throw new ArgumentException("A non-null native pointer is required.", nameof(nativeAddress));
        lock (_sync)
        {
            ThrowIfDisposed();
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.StreamWaitValue64(_handle.DangerousGetHandle(), nativeAddress, value, (uint)flags, mask), "hipStreamWaitValue64");
        }
    }
}
