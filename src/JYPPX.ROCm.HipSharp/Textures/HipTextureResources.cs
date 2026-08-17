using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Textures;

/// <summary>拥有 HIP texture object 并保活 backing resource / Owns a HIP texture object and retains its backing resource.</summary>
public sealed class HipTextureObject : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipTextureObjectHandle _handle;

    internal HipTextureObject(IHipNativeApi nativeApi, ulong handle, IHipTextureResourceOwner resourceOwner, bool resourceReference)
    {
        _nativeApi = nativeApi;
        _handle = new HipTextureObjectHandle(nativeApi, handle, resourceOwner, resourceReference);
    }

    /// <summary>获取该值 / Gets whether the texture object has been destroyed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取该值 / Gets the borrowed native texture-object value; the caller must not destroy it.</summary>
    public ulong DangerousGetHandle()
    {
        ThrowIfDisposed();
        return _handle.DangerousGetValue();
    }

    /// <summary>查询该状态 / Queries the runtime-style texture descriptor.</summary>
    public unsafe HipTextureDescriptor GetTextureDescriptor()
    {
        ThrowIfDisposed();
        HipTextureDescriptorNative descriptor = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetTextureObjectTextureDesc((IntPtr)(&descriptor), DangerousGetHandle()), "hipGetTextureObjectTextureDesc");
        return HipTextureDescriptor.FromNative(descriptor);
    }

    /// <summary>查询该状态 / Queries the legacy driver-style texture descriptor.</summary>
    public unsafe HipDriverTextureDescriptor GetDriverTextureDescriptor()
    {
        ThrowIfDisposed();
        HipDriverTextureDescriptorNative descriptor = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.TexObjectGetTextureDesc((IntPtr)(&descriptor), DangerousGetHandle()), "hipTexObjectGetTextureDesc");
        return new HipDriverTextureDescriptor(descriptor);
    }

    /// <summary>查询该状态 / Queries the borrowed resource handle backing the texture.</summary>
    public unsafe HipTextureResourceInfo GetResourceInfo()
    {
        ThrowIfDisposed();
        HipResourceDescriptorNative descriptor = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetTextureObjectResourceDesc((IntPtr)(&descriptor), DangerousGetHandle()), "hipGetTextureObjectResourceDesc");
        return new HipTextureResourceInfo(descriptor.ResourceType, descriptor.Resource.Handle);
    }

    /// <summary>查询该状态 / Queries the texture resource-view descriptor.</summary>
    public unsafe HipResourceViewDescriptor GetResourceViewDescriptor()
    {
        ThrowIfDisposed();
        HipResourceViewDescriptorNative descriptor = default;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetTextureObjectResourceViewDesc((IntPtr)(&descriptor), DangerousGetHandle()), "hipGetTextureObjectResourceViewDesc");
        return HipResourceViewDescriptor.FromNative(descriptor);
    }

    /// <summary>销毁该资源 / Destroys the texture and releases its backing-resource lease.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipDestroyTextureObject");
    }

    private void ThrowIfDisposed()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipTextureObject));
    }
}

/// <summary>拥有该资源 / Owns a HIP surface object and retains its backing array.</summary>
public sealed class HipSurfaceObject : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipSurfaceObjectHandle _handle;

    internal HipSurfaceObject(IHipNativeApi nativeApi, ulong handle, IHipTextureResourceOwner resourceOwner, bool resourceReference)
    {
        _nativeApi = nativeApi;
        _handle = new HipSurfaceObjectHandle(nativeApi, handle, resourceOwner, resourceReference);
    }

    /// <summary>获取该值 / Gets whether the surface object has been destroyed.</summary>
    public bool IsDisposed => _handle.IsClosed || _handle.IsInvalid;

    /// <summary>获取该值 / Gets the borrowed native surface-object value; the caller must not destroy it.</summary>
    public ulong DangerousGetHandle()
    {
        if (IsDisposed) throw new ObjectDisposedException(nameof(HipSurfaceObject));
        return _handle.DangerousGetValue();
    }

    /// <summary>销毁该资源 / Destroys the surface and releases its backing-array lease.</summary>
    public void Dispose()
    {
        if (IsDisposed) return;
        HipError error = _handle.ReleaseChecked();
        if (error == HipError.Success)
        {
            _handle.Dispose();
            return;
        }
        HipCall.ThrowIfFailed(_nativeApi, error, "hipDestroySurfaceObject");
    }
}

/// <summary>表示该原生概念 / Represents a borrowed legacy texture reference and controls its bound-resource lease.</summary>
public sealed class HipTextureReference : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly IntPtr _handle;
    private readonly object _sync = new();
    private Action? _releaseBinding;
    private bool _disposed;

    internal HipTextureReference(IHipNativeApi nativeApi, IntPtr handle)
    {
        _nativeApi = nativeApi;
        _handle = handle;
    }

    /// <summary>获取该值 / Gets whether this borrowed wrapper has been released.</summary>
    public bool IsDisposed
    {
        get
        {
            lock (_sync) return _disposed;
        }
    }

    /// <summary>获取该值 / Gets the borrowed native texture-reference pointer; the caller must not release it.</summary>
    public IntPtr DangerousGetHandle()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    /// <summary>绑定该资源 / Binds linear device memory and retains the owner until unbound.</summary>
    public unsafe ulong Bind(HipDeviceMemory memory, HipChannelFormatDescriptor descriptor, ulong byteCount = 0)
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        ulong count = byteCount == 0 ? memory.ByteLength : byteCount;
        if (count == 0 || count > memory.ByteLength) throw new ArgumentOutOfRangeException(nameof(byteCount));
        return BindPointer(memory, descriptor, count, false, 0, 0, 0);
    }

    /// <summary>绑定该资源 / Binds two-dimensional pitched device memory and retains the owner until unbound.</summary>
    public unsafe ulong Bind2D<T>(HipPitchedDeviceMemory<T> memory, HipChannelFormatDescriptor descriptor) where T : unmanaged
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        return BindPointer(memory, descriptor, memory.ByteLength, true, memory.Width, memory.Height, memory.PitchBytes);
    }

    /// <summary>绑定该资源 / Binds a HIP array and retains it until unbound.</summary>
    public unsafe void Bind(HipArray array)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        ValidateRuntime(array.NativeApi, nameof(array));
        HipChannelFormatDescriptor descriptor = array.Info.ChannelFormat;
        bool reference = false;
        try
        {
            IntPtr arrayHandle = array.AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.BindTextureToArray(_handle, arrayHandle, (IntPtr)(&descriptor)), "hipBindTextureToArray");
            ReplaceBinding(() => array.ReleaseHandle());
            reference = false;
        }
        finally
        {
            if (reference) array.ReleaseHandle();
        }
    }

    /// <summary>绑定该资源 / Binds a HIP mipmapped array and retains it until unbound.</summary>
    public unsafe void Bind(HipMipmappedArray array)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        ValidateRuntime(((IHipTextureResourceOwner)array).NativeApi, nameof(array));
        HipChannelFormatDescriptor descriptor = array.ChannelFormat;
        bool reference = false;
        try
        {
            IntPtr arrayHandle = array.AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.BindTextureToMipmappedArray(_handle, arrayHandle, (IntPtr)(&descriptor)), "hipBindTextureToMipmappedArray");
            ReplaceBinding(() => array.ReleaseHandle());
            reference = false;
        }
        finally
        {
            if (reference) array.ReleaseHandle();
        }
    }

    /// <summary>使用该原生入口 / Uses the legacy driver setter to bind an array.</summary>
    public void SetArray(HipArray array, bool overrideFormat = false)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        ValidateRuntime(array.NativeApi, nameof(array));
        bool reference = false;
        try
        {
            IntPtr arrayHandle = array.AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.TexRefSetArray(_handle, arrayHandle, overrideFormat ? 1U : 0U), "hipTexRefSetArray");
            ReplaceBinding(() => array.ReleaseHandle());
            reference = false;
        }
        finally
        {
            if (reference) array.ReleaseHandle();
        }
    }

    /// <summary>使用该原生入口 / Uses the legacy driver setter to bind a mipmapped array.</summary>
    public void SetMipmappedArray(HipMipmappedArray array, bool overrideFormat = false)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        ValidateRuntime(((IHipTextureResourceOwner)array).NativeApi, nameof(array));
        bool reference = false;
        try
        {
            IntPtr arrayHandle = array.AcquireHandle(out reference);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.TexRefSetMipmappedArray(_handle, arrayHandle, overrideFormat ? 1U : 0U), "hipTexRefSetMipmappedArray");
            ReplaceBinding(() => array.ReleaseHandle());
            reference = false;
        }
        finally
        {
            if (reference) array.ReleaseHandle();
        }
    }

    /// <summary>获取该值 / Gets the currently bound borrowed array handle without taking ownership.</summary>
    public unsafe IntPtr GetBoundArrayHandle()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            IntPtr array = IntPtr.Zero;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.TexRefGetArray((IntPtr)(&array), _handle), "hipTexRefGetArray");
            return array;
        }
    }

    /// <summary>获取该值 / Gets the currently bound borrowed mipmapped-array handle without taking ownership.</summary>
    public unsafe IntPtr GetBoundMipmappedArrayHandle()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            IntPtr array = IntPtr.Zero;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.TexRefGetMipMappedArray((IntPtr)(&array), _handle), "hipTexRefGetMipMappedArray");
            return array;
        }
    }

    /// <summary>获取该值 / Gets the native alignment offset for the current binding.</summary>
    public unsafe ulong GetAlignmentOffset()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            UIntPtr offset = UIntPtr.Zero;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetTextureAlignmentOffset((IntPtr)(&offset), _handle), "hipGetTextureAlignmentOffset");
            return offset.ToUInt64();
        }
    }

    /// <summary>解除资源绑定 / Unbinds the texture reference and releases the current resource lease.</summary>
    public void Unbind()
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (_releaseBinding is null) return;
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UnbindTexture(_handle), "hipUnbindTexture");
            Action release = _releaseBinding;
            _releaseBinding = null;
            release();
        }
    }

    /// <summary>解除资源绑定 / Unbinds any resource and releases this borrowed wrapper.</summary>
    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_releaseBinding is not null)
            {
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.UnbindTexture(_handle), "hipUnbindTexture");
                Action release = _releaseBinding;
                _releaseBinding = null;
                release();
            }
            _disposed = true;
        }
    }

    private unsafe ulong BindPointer(IHipPointerOwner owner, HipChannelFormatDescriptor descriptor, ulong byteCount, bool twoDimensional, ulong width, ulong height, ulong pitch)
    {
        ValidateRuntime(owner.NativeApi, nameof(owner));
        bool reference = false;
        try
        {
            IntPtr pointer = owner.AcquirePointer(out reference);
            UIntPtr offset = UIntPtr.Zero;
            HipError error = twoDimensional
                ? _nativeApi.BindTexture2D((IntPtr)(&offset), _handle, pointer, (IntPtr)(&descriptor), ToUIntPtr(width), ToUIntPtr(height), ToUIntPtr(pitch))
                : _nativeApi.BindTexture((IntPtr)(&offset), _handle, pointer, (IntPtr)(&descriptor), ToUIntPtr(byteCount));
            HipCall.ThrowIfFailed(_nativeApi, error, twoDimensional ? "hipBindTexture2D" : "hipBindTexture");
            ReplaceBinding(owner.ReleasePointer);
            reference = false;
            return offset.ToUInt64();
        }
        finally
        {
            if (reference) owner.ReleasePointer();
        }
    }

    private void ReplaceBinding(Action release)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            Action? previous = _releaseBinding;
            _releaseBinding = release;
            previous?.Invoke();
        }
    }

    private void ValidateRuntime(IHipNativeApi nativeApi, string parameterName)
    {
        lock (_sync)
        {
            ThrowIfDisposed();
            if (!ReferenceEquals(_nativeApi, nativeApi)) throw new ArgumentException("Texture reference and resource belong to different HIP Runtime clients.", parameterName);
        }
    }

    private static UIntPtr ToUIntPtr(ulong value) => HipDeviceMemory.ToUIntPtr(value, nameof(value));

    private void ThrowIfDisposed()
    {
        if (_disposed) throw new ObjectDisposedException(nameof(HipTextureReference));
    }
}

internal abstract class HipTextureResourceHandle : SafeHandle
{
    private readonly IHipTextureResourceOwner _resourceOwner;
    private bool _resourceReference;

    protected HipTextureResourceHandle(ulong handle, IHipTextureResourceOwner resourceOwner, bool resourceReference)
        : base(IntPtr.Zero, true)
    {
        _resourceOwner = resourceOwner;
        _resourceReference = resourceReference;
        SetHandle(new IntPtr(unchecked((long)handle)));
    }

    public override bool IsInvalid => handle == IntPtr.Zero;

    internal ulong DangerousGetValue() => unchecked((ulong)handle.ToInt64());

    protected void ReleaseResourceReference()
    {
        if (!_resourceReference) return;
        _resourceOwner.ReleaseHandle();
        _resourceReference = false;
    }
}

internal sealed class HipTextureObjectHandle : HipTextureResourceHandle
{
    private readonly IHipNativeApi _nativeApi;

    internal HipTextureObjectHandle(IHipNativeApi nativeApi, ulong handle, IHipTextureResourceOwner resourceOwner, bool resourceReference)
        : base(handle, resourceOwner, resourceReference) => _nativeApi = nativeApi;

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = _nativeApi.DestroyTextureObject(DangerousGetValue());
        if (error == HipError.Success)
        {
            SetHandleAsInvalid();
            ReleaseResourceReference();
        }
        return error;
    }

    protected override bool ReleaseHandle()
    {
        if (_nativeApi.DestroyTextureObject(DangerousGetValue()) != HipError.Success) return false;
        try
        {
            ReleaseResourceReference();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class HipSurfaceObjectHandle : HipTextureResourceHandle
{
    private readonly IHipNativeApi _nativeApi;

    internal HipSurfaceObjectHandle(IHipNativeApi nativeApi, ulong handle, IHipTextureResourceOwner resourceOwner, bool resourceReference)
        : base(handle, resourceOwner, resourceReference) => _nativeApi = nativeApi;

    internal HipError ReleaseChecked()
    {
        if (IsClosed || IsInvalid) return HipError.Success;
        HipError error = _nativeApi.DestroySurfaceObject(DangerousGetValue());
        if (error == HipError.Success)
        {
            SetHandleAsInvalid();
            ReleaseResourceReference();
        }
        return error;
    }

    protected override bool ReleaseHandle()
    {
        if (_nativeApi.DestroySurfaceObject(DangerousGetValue()) != HipError.Success) return false;
        try
        {
            ReleaseResourceReference();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

internal sealed class HipPointerTextureResourceOwner : IHipTextureResourceOwner
{
    private readonly IHipPointerOwner _owner;

    internal HipPointerTextureResourceOwner(IHipPointerOwner owner, HipTextureResourceKind resourceKind)
    {
        _owner = owner;
        ResourceKind = resourceKind;
    }

    public IHipNativeApi NativeApi => _owner.NativeApi;
    public HipTextureResourceKind ResourceKind { get; }
    public IntPtr AcquireHandle(out bool addedReference) => _owner.AcquirePointer(out addedReference);
    public void ReleaseHandle() => _owner.ReleasePointer();
}
