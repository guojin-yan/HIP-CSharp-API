using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>拥有 HIP mipmapped array 并租借其各级 array / Owns a HIP mipmapped array and leases its individual array levels.</summary>
public sealed class HipMipmappedArray : IDisposable, IHipTextureResourceOwner
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipMipmappedArrayHandle _handle;
    private readonly object _lifetimeSync = new();
    private int _references;
    private bool _disposeRequested;

    internal HipMipmappedArray(
        IHipNativeApi nativeApi,
        IntPtr handle,
        HipChannelFormatDescriptor channelFormat,
        ulong width,
        ulong height,
        ulong depth,
        uint levelCount,
        HipArrayFlags flags,
        HipMipmappedArrayReleaseKind releaseKind)
    {
        _nativeApi = nativeApi;
        _handle = new HipMipmappedArrayHandle(nativeApi, handle, releaseKind);
        ChannelFormat = channelFormat;
        Width = width;
        Height = height;
        Depth = depth;
        LevelCount = levelCount;
        Flags = flags;
    }

    /// <summary>获取该值 / Gets the base-level channel format.</summary>
    public HipChannelFormatDescriptor ChannelFormat { get; }
    /// <summary>获取该值 / Gets the base-level width in elements.</summary>
    public ulong Width { get; }
    /// <summary>获取该值 / Gets the base-level height in elements.</summary>
    public ulong Height { get; }
    /// <summary>获取该值 / Gets the base-level depth in elements.</summary>
    public ulong Depth { get; }
    /// <summary>获取该值 / Gets the number of mipmap levels.</summary>
    public uint LevelCount { get; }
    /// <summary>获取该值 / Gets the allocation flags.</summary>
    public HipArrayFlags Flags { get; }

    /// <summary>获取该值 / Gets whether release has been requested or completed.</summary>
    public bool IsDisposed
    {
        get
        {
            lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid;
        }
    }

    /// <summary>获取该值 / Gets the borrowed native mipmapped-array handle; the caller must not release it.</summary>
    public IntPtr DangerousGetHandle()
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            return _handle.DangerousGetHandle();
        }
    }

    /// <summary>获取该值 / Gets a leased, non-owning array view for one mipmap level.</summary>
    public unsafe HipArray GetLevel(uint level)
    {
        if (level >= LevelCount) throw new ArgumentOutOfRangeException(nameof(level));
        bool reference = false;
        try
        {
            IntPtr mipmappedArray = AcquireHandle(out reference);
            IntPtr levelArray = IntPtr.Zero;
            HipError error = _handle.ReleaseKind == HipMipmappedArrayReleaseKind.DestroyMipmappedArray
                ? _nativeApi.MipmappedArrayGetLevel((IntPtr)(&levelArray), mipmappedArray, level)
                : _nativeApi.GetMipmappedArrayLevel((IntPtr)(&levelArray), mipmappedArray, level);
            string operation = _handle.ReleaseKind == HipMipmappedArrayReleaseKind.DestroyMipmappedArray
                ? "hipMipmappedArrayGetLevel"
                : "hipGetMipmappedArrayLevel";
            HipCall.ThrowIfFailed(_nativeApi, error, operation);
            if (levelArray == IntPtr.Zero) throw new InvalidOperationException(operation + " succeeded but returned a null array handle.");

            var lease = new HipMipmappedArrayLease(this);
            reference = false;
            return new HipArray(
                _nativeApi,
                levelArray,
                new HipArrayInfo(ChannelFormat, Scale(Width, level), ScaleOptional(Height, level), ScaleOptional(Depth, level), Flags),
                HipArrayReleaseKind.Borrowed,
                lease);
        }
        finally
        {
            if (reference) ReleaseHandle();
        }
    }

    /// <summary>释放该资源 / Releases the mipmapped array after all level and texture leases complete.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed || _handle.IsInvalid) return;
            _disposeRequested = true;
            if (_references != 0) return;
        }
        ReleaseOwner();
    }

    IHipNativeApi IHipTextureResourceOwner.NativeApi => _nativeApi;
    HipTextureResourceKind IHipTextureResourceOwner.ResourceKind => HipTextureResourceKind.MipmappedArray;
    IntPtr IHipTextureResourceOwner.AcquireHandle(out bool addedReference) => AcquireHandle(out addedReference);
    void IHipTextureResourceOwner.ReleaseHandle() => ReleaseHandle();

    internal IntPtr AcquireHandle(out bool addedReference)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            addedReference = false;
            _handle.DangerousAddRef(ref addedReference);
            if (addedReference) _references++;
            return _handle.DangerousGetHandle();
        }
    }

    internal void ReleaseHandle()
    {
        bool releaseOwner;
        lock (_lifetimeSync)
        {
            if (_references > 0)
            {
                _handle.DangerousRelease();
                _references--;
            }
            releaseOwner = _disposeRequested && _references == 0 && !_handle.IsClosed && !_handle.IsInvalid;
        }
        if (releaseOwner) ReleaseOwner();
    }

    private void ReleaseOwner()
    {
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }
        HipCall.ThrowIfFailed(_nativeApi, error, _handle.ReleaseOperation);
    }

    private void ThrowIfDisposed()
    {
        if (_disposeRequested || _handle.IsClosed || _handle.IsInvalid) throw new ObjectDisposedException(nameof(HipMipmappedArray));
    }

    private static ulong Scale(ulong value, uint level) => level >= 64 ? 1 : Math.Max(1UL, value >> (int)level);
    private static ulong ScaleOptional(ulong value, uint level) => value == 0 ? 0 : Scale(value, level);
}

internal enum HipMipmappedArrayReleaseKind
{
    FreeMipmappedArray,
    DestroyMipmappedArray,
}

internal sealed class HipMipmappedArrayHandle : SafeHandle
{
    private readonly IHipNativeApi _nativeApi;

    internal HipMipmappedArrayHandle(IHipNativeApi nativeApi, IntPtr handle, HipMipmappedArrayReleaseKind releaseKind)
        : base(IntPtr.Zero, true)
    {
        _nativeApi = nativeApi;
        ReleaseKind = releaseKind;
        SetHandle(handle);
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal HipMipmappedArrayReleaseKind ReleaseKind { get; }
    internal string ReleaseOperation => ReleaseKind == HipMipmappedArrayReleaseKind.DestroyMipmappedArray
        ? "hipMipmappedArrayDestroy"
        : "hipFreeMipmappedArray";

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = ReleaseNative();
        if (error == HipError.Success) SetHandleAsInvalid();
        return error;
    }

    protected override bool ReleaseHandle() => ReleaseNative() == HipError.Success;

    private HipError ReleaseNative() => ReleaseKind == HipMipmappedArrayReleaseKind.DestroyMipmappedArray
        ? _nativeApi.MipmappedArrayDestroy(handle)
        : _nativeApi.FreeMipmappedArray(handle);
}

internal sealed class HipMipmappedArrayLease : IDisposable
{
    private HipMipmappedArray? _array;

    internal HipMipmappedArrayLease(HipMipmappedArray array) => _array = array;

    public void Dispose()
    {
        HipMipmappedArray? array = _array;
        _array = null;
        array?.ReleaseHandle();
    }
}
