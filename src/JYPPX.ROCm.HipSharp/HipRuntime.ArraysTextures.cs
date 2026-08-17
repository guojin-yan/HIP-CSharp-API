using System;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Textures;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp;

/// <summary>提供数组、纹理与表面资源创建入口 / Provides array, texture, and surface resource creation entry points.</summary>
public sealed partial class HipRuntime
{
    /// <summary>分配该资源 / Allocates a one- or two-dimensional runtime-style HIP array.</summary>
    public unsafe HipArray AllocateArray(HipChannelFormatDescriptor channelFormat, ulong width, ulong height = 0, HipArrayFlags flags = HipArrayFlags.Default)
    {
        ThrowIfDisposed();
        ValidateArrayShape(width, height, 0, flags);
        UIntPtr nativeWidth = HipDeviceMemory.ToUIntPtr(width, nameof(width));
        UIntPtr nativeHeight = HipDeviceMemory.ToUIntPtr(height, nameof(height));
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.MallocArray((IntPtr)(&handle), (IntPtr)(&channelFormat), nativeWidth, nativeHeight, (uint)flags);
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.FreeArray(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocArray");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMallocArray succeeded but returned a null array handle.");
        return new HipArray(_nativeApi, handle, new HipArrayInfo(channelFormat, width, height, 0, flags), HipArrayReleaseKind.FreeArray);
    }

    /// <summary>分配该资源 / Allocates a runtime-style HIP array with up to three dimensions.</summary>
    public unsafe HipArray AllocateArray3D(HipChannelFormatDescriptor channelFormat, ulong width, ulong height, ulong depth, HipArrayFlags flags = HipArrayFlags.Default)
    {
        ThrowIfDisposed();
        ValidateArrayShape(width, height, depth, flags);
        var extent = new HipExtent(
            HipDeviceMemory.ToUIntPtr(width, nameof(width)),
            HipDeviceMemory.ToUIntPtr(height, nameof(height)),
            HipDeviceMemory.ToUIntPtr(depth, nameof(depth)));
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.Malloc3DArray((IntPtr)(&handle), (IntPtr)(&channelFormat), extent, (uint)flags);
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.FreeArray(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMalloc3DArray");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMalloc3DArray succeeded but returned a null array handle.");
        return new HipArray(_nativeApi, handle, new HipArrayInfo(channelFormat, width, height, depth, flags), HipArrayReleaseKind.FreeArray);
    }

    /// <summary>创建该对象 / Creates a driver-style one- or two-dimensional HIP array.</summary>
    public unsafe HipArray CreateArray(HipArrayDescriptor descriptor)
    {
        ThrowIfDisposed();
        HipArrayDescriptorNative nativeDescriptor = descriptor.ToNative();
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.ArrayCreate((IntPtr)(&handle), (IntPtr)(&nativeDescriptor));
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.ArrayDestroy(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipArrayCreate");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipArrayCreate succeeded but returned a null array handle.");
        return new HipArray(
            _nativeApi,
            handle,
            new HipArrayInfo(ToChannelFormat(descriptor.Format, descriptor.ChannelCount), descriptor.Width, descriptor.Height, 0, HipArrayFlags.Default),
            HipArrayReleaseKind.DestroyArray);
    }

    /// <summary>创建该对象 / Creates a driver-style HIP array with up to three dimensions.</summary>
    public unsafe HipArray CreateArray3D(HipArray3DDescriptor descriptor)
    {
        ThrowIfDisposed();
        HipArray3DDescriptorNative nativeDescriptor = descriptor.ToNative();
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.Array3DCreate((IntPtr)(&handle), (IntPtr)(&nativeDescriptor));
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.ArrayDestroy(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipArray3DCreate");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipArray3DCreate succeeded but returned a null array handle.");
        return new HipArray(
            _nativeApi,
            handle,
            new HipArrayInfo(ToChannelFormat(descriptor.Format, descriptor.ChannelCount), descriptor.Width, descriptor.Height, descriptor.Depth, descriptor.Flags),
            HipArrayReleaseKind.DestroyArray);
    }

    /// <summary>分配该资源 / Allocates a runtime-style HIP mipmapped array.</summary>
    public unsafe HipMipmappedArray AllocateMipmappedArray(HipChannelFormatDescriptor channelFormat, ulong width, ulong height, ulong depth, uint levelCount, HipArrayFlags flags = HipArrayFlags.Default)
    {
        ThrowIfDisposed();
        ValidateMipmappedShape(width, height, depth, levelCount, flags);
        var extent = new HipExtent(
            HipDeviceMemory.ToUIntPtr(width, nameof(width)),
            HipDeviceMemory.ToUIntPtr(height, nameof(height)),
            HipDeviceMemory.ToUIntPtr(depth, nameof(depth)));
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.MallocMipmappedArray((IntPtr)(&handle), (IntPtr)(&channelFormat), extent, levelCount, (uint)flags);
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.FreeMipmappedArray(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMallocMipmappedArray");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMallocMipmappedArray succeeded but returned a null mipmapped-array handle.");
        return new HipMipmappedArray(_nativeApi, handle, channelFormat, width, height, depth, levelCount, flags, HipMipmappedArrayReleaseKind.FreeMipmappedArray);
    }

    /// <summary>创建该对象 / Creates a driver-style HIP mipmapped array.</summary>
    public unsafe HipMipmappedArray CreateMipmappedArray(HipArray3DDescriptor descriptor, uint levelCount)
    {
        ThrowIfDisposed();
        ValidateMipmappedShape(descriptor.Width, descriptor.Height, descriptor.Depth, levelCount, descriptor.Flags);
        HipArray3DDescriptorNative nativeDescriptor = descriptor.ToNative();
        IntPtr handle = IntPtr.Zero;
        HipError error = _nativeApi.MipmappedArrayCreate((IntPtr)(&handle), (IntPtr)(&nativeDescriptor), levelCount);
        if (error != HipError.Success && handle != IntPtr.Zero) _ = _nativeApi.MipmappedArrayDestroy(handle);
        HipCall.ThrowIfFailed(_nativeApi, error, "hipMipmappedArrayCreate");
        if (handle == IntPtr.Zero) throw new InvalidOperationException("hipMipmappedArrayCreate succeeded but returned a null mipmapped-array handle.");
        return new HipMipmappedArray(
            _nativeApi,
            handle,
            ToChannelFormat(descriptor.Format, descriptor.ChannelCount),
            descriptor.Width,
            descriptor.Height,
            descriptor.Depth,
            levelCount,
            descriptor.Flags,
            HipMipmappedArrayReleaseKind.DestroyMipmappedArray);
    }

    /// <summary>创建该对象 / Creates a texture object backed by a HIP array.</summary>
    public HipTextureObject CreateTextureObject(HipArray array, HipTextureDescriptor descriptor, HipResourceViewDescriptor? resourceView = null)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        return CreateTextureObjectCore(array, descriptor, resourceView, handle => new HipResourceDescriptorNative(HipTextureResourceKind.Array, handle));
    }

    /// <summary>创建该对象 / Creates a texture object backed by a HIP mipmapped array.</summary>
    public HipTextureObject CreateTextureObject(HipMipmappedArray array, HipTextureDescriptor descriptor, HipResourceViewDescriptor? resourceView = null)
    {
        if (array is null) throw new ArgumentNullException(nameof(array));
        return CreateTextureObjectCore(array, descriptor, resourceView, handle => new HipResourceDescriptorNative(HipTextureResourceKind.MipmappedArray, handle));
    }

    /// <summary>创建该对象 / Creates a texture object backed by linear device memory.</summary>
    public HipTextureObject CreateTextureObject(HipDeviceMemory memory, HipChannelFormatDescriptor channelFormat, HipTextureDescriptor descriptor)
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        var owner = new HipPointerTextureResourceOwner(memory, HipTextureResourceKind.Linear);
        return CreateTextureObjectCore(owner, descriptor, null, handle => HipResourceDescriptorNative.ForLinear(
            handle, channelFormat, HipDeviceMemory.ToUIntPtr(memory.ByteLength, nameof(memory))));
    }

    /// <summary>创建该对象 / Creates a texture object backed by pitched two-dimensional device memory.</summary>
    public HipTextureObject CreateTextureObject<T>(HipPitchedDeviceMemory<T> memory, HipChannelFormatDescriptor channelFormat, HipTextureDescriptor descriptor) where T : unmanaged
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        var owner = new HipPointerTextureResourceOwner(memory, HipTextureResourceKind.Pitch2D);
        return CreateTextureObjectCore(owner, descriptor, null, handle => HipResourceDescriptorNative.ForPitch2D(
            handle,
            channelFormat,
            HipDeviceMemory.ToUIntPtr(memory.Width, nameof(memory)),
            HipDeviceMemory.ToUIntPtr(memory.Height, nameof(memory)),
            HipDeviceMemory.ToUIntPtr(memory.PitchBytes, nameof(memory))));
    }

    /// <summary>创建该对象 / Creates a surface object backed by a HIP array.</summary>
    public unsafe HipSurfaceObject CreateSurfaceObject(HipArray array)
    {
        ThrowIfDisposed();
        if (array is null) throw new ArgumentNullException(nameof(array));
        ValidateResource(array.NativeApi, nameof(array));
        bool reference = false;
        try
        {
            IntPtr arrayHandle = array.AcquireHandle(out reference);
            var resource = new HipResourceDescriptorNative(HipTextureResourceKind.Array, arrayHandle);
            ulong surfaceObject = 0;
            HipError error = _nativeApi.CreateSurfaceObject((IntPtr)(&surfaceObject), (IntPtr)(&resource));
            if (error != HipError.Success && surfaceObject != 0) _ = _nativeApi.DestroySurfaceObject(surfaceObject);
            HipCall.ThrowIfFailed(_nativeApi, error, "hipCreateSurfaceObject");
            if (surfaceObject == 0) throw new InvalidOperationException("hipCreateSurfaceObject succeeded but returned a null surface object.");
            var owner = new HipSurfaceObject(_nativeApi, surfaceObject, array, reference);
            reference = false;
            return owner;
        }
        finally
        {
            if (reference) array.ReleaseHandle();
        }
    }

    /// <summary>获取该值 / Gets a borrowed legacy texture reference for a native texture symbol.</summary>
    public unsafe HipTextureReference GetTextureReference(IntPtr nativeTextureSymbol)
    {
        ThrowIfDisposed();
        if (nativeTextureSymbol == IntPtr.Zero) throw new ArgumentException("A non-null native texture symbol is required.", nameof(nativeTextureSymbol));
        IntPtr textureReference = IntPtr.Zero;
        HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GetTextureReference((IntPtr)(&textureReference), nativeTextureSymbol), "hipGetTextureReference");
        if (textureReference == IntPtr.Zero) throw new InvalidOperationException("hipGetTextureReference succeeded but returned a null texture reference.");
        return new HipTextureReference(_nativeApi, textureReference);
    }

    private unsafe HipTextureObject CreateTextureObjectCore(
        IHipTextureResourceOwner resourceOwner,
        HipTextureDescriptor descriptor,
        HipResourceViewDescriptor? resourceView,
        Func<IntPtr, HipResourceDescriptorNative> resourceFactory)
    {
        ThrowIfDisposed();
        if (descriptor is null) throw new ArgumentNullException(nameof(descriptor));
        ValidateResource(resourceOwner.NativeApi, nameof(resourceOwner));
        HipTextureDescriptorNative nativeTexture = descriptor.ToNative();
        HipResourceViewDescriptorNative nativeView = resourceView is null ? default : resourceView.ToNative();
        bool reference = false;
        try
        {
            IntPtr resourceHandle = resourceOwner.AcquireHandle(out reference);
            HipResourceDescriptorNative nativeResource = resourceFactory(resourceHandle);
            IntPtr viewPointer = resourceView is null ? IntPtr.Zero : (IntPtr)(&nativeView);
            ulong textureObject = 0;
            HipError error = _nativeApi.CreateTextureObject((IntPtr)(&textureObject), (IntPtr)(&nativeResource), (IntPtr)(&nativeTexture), viewPointer);
            if (error != HipError.Success && textureObject != 0) _ = _nativeApi.DestroyTextureObject(textureObject);
            HipCall.ThrowIfFailed(_nativeApi, error, "hipCreateTextureObject");
            if (textureObject == 0) throw new InvalidOperationException("hipCreateTextureObject succeeded but returned a null texture object.");
            var owner = new HipTextureObject(_nativeApi, textureObject, resourceOwner, reference);
            reference = false;
            return owner;
        }
        finally
        {
            if (reference) resourceOwner.ReleaseHandle();
        }
    }

    private void ValidateResource(IHipNativeApi nativeApi, string parameterName)
    {
        if (!ReferenceEquals(_nativeApi, nativeApi)) throw new ArgumentException("Resource belongs to a different HIP Runtime client.", parameterName);
    }

    private static void ValidateArrayShape(ulong width, ulong height, ulong depth, HipArrayFlags flags)
    {
        if (width == 0) throw new ArgumentOutOfRangeException(nameof(width));
        _ = HipDeviceMemory.ToUIntPtr(width, nameof(width));
        _ = HipDeviceMemory.ToUIntPtr(height, nameof(height));
        _ = HipDeviceMemory.ToUIntPtr(depth, nameof(depth));
        HipArray3DDescriptor.ValidateFlags(flags);
    }

    private static void ValidateMipmappedShape(ulong width, ulong height, ulong depth, uint levelCount, HipArrayFlags flags)
    {
        ValidateArrayShape(width, height, depth, flags);
        if (levelCount == 0) throw new ArgumentOutOfRangeException(nameof(levelCount));
    }

    private static HipChannelFormatDescriptor ToChannelFormat(HipArrayFormat format, uint channelCount)
    {
        int bits;
        HipChannelFormatKind kind;
        switch (format)
        {
            case HipArrayFormat.UnsignedInt8: bits = 8; kind = HipChannelFormatKind.UnsignedInteger; break;
            case HipArrayFormat.UnsignedInt16: bits = 16; kind = HipChannelFormatKind.UnsignedInteger; break;
            case HipArrayFormat.UnsignedInt32: bits = 32; kind = HipChannelFormatKind.UnsignedInteger; break;
            case HipArrayFormat.SignedInt8: bits = 8; kind = HipChannelFormatKind.SignedInteger; break;
            case HipArrayFormat.SignedInt16: bits = 16; kind = HipChannelFormatKind.SignedInteger; break;
            case HipArrayFormat.SignedInt32: bits = 32; kind = HipChannelFormatKind.SignedInteger; break;
            case HipArrayFormat.Half: bits = 16; kind = HipChannelFormatKind.FloatingPoint; break;
            case HipArrayFormat.FloatingPoint32: bits = 32; kind = HipChannelFormatKind.FloatingPoint; break;
            default: throw new ArgumentOutOfRangeException(nameof(format));
        }
        if (channelCount != 1 && channelCount != 2 && channelCount != 4) throw new ArgumentOutOfRangeException(nameof(channelCount));
        return new HipChannelFormatDescriptor(bits, channelCount >= 2 ? bits : 0, channelCount >= 4 ? bits : 0, channelCount >= 4 ? bits : 0, kind);
    }
}
