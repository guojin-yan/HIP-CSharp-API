using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Textures;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public class HipArrayTextureTests
{
    private static readonly HipChannelFormatDescriptor ByteChannel = new(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);

    [TestMethod]
    public void NativeDescriptorLayoutsMatchRocm721X64Abi()
    {
        Assert.AreEqual(8, IntPtr.Size, "The reviewed HIP package ABI is x64-only.");
        Assert.AreEqual(20, Marshal.SizeOf<HipChannelFormatDescriptor>());
        Assert.AreEqual(24, Marshal.SizeOf<HipArrayDescriptorNative>());
        Assert.AreEqual(40, Marshal.SizeOf<HipArray3DDescriptorNative>());
        Assert.AreEqual(64, Marshal.SizeOf<HipTextureDescriptorNative>());
        Assert.AreEqual(64, Marshal.SizeOf<HipResourceDescriptorNative>());
        Assert.AreEqual(48, Marshal.SizeOf<HipResourceViewDescriptorNative>());
        Assert.AreEqual(104, Marshal.SizeOf<HipDriverTextureDescriptorNative>());
    }

    [TestMethod]
    public void RuntimeArraysQueryCopyAndDeferReleaseForAsyncWork()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipArray array = runtime.AllocateArray(ByteChannel, 8, 2);
        using HipArray destination = runtime.AllocateArray(ByteChannel, 8, 2);

        HipArrayInfo info = array.GetInfo();
        Assert.AreEqual(8UL, info.Width);
        Assert.AreEqual(2UL, info.Height);
        Assert.AreEqual(HipChannelFormatKind.UnsignedInteger, info.ChannelFormat.Kind);
        Assert.AreEqual(8UL, array.GetDriverDescriptor().Width);

        array.CopyFrom(new byte[4], 2);
        array.CopyTo(new byte[4], 2);
        Assert.AreEqual(1, native.Memcpy2DToArrayCallCount);
        Assert.AreEqual(0, native.MemcpyToArrayCallCount);
        Assert.AreEqual(1, native.Memcpy2DFromArrayCallCount);
        Assert.AreEqual(0, native.MemcpyFromArrayCallCount);
        array.Copy2DFrom(new byte[16], 8, 2);
        array.Copy2DTo(new byte[16], 8, 2);
        array.Copy2DTo(destination, 8, 2);
        Assert.AreEqual(5, native.ArrayCopyCallCount);

        using HipStream stream = runtime.CreateStream();
        array.Copy2DFromAsync(new byte[16], 8, 2, stream);
        array.Dispose();
        Assert.IsTrue(array.IsDisposed);
        Assert.AreEqual(0, native.ArrayFreeCallCount);

        stream.Synchronize();
        Assert.AreEqual(1, native.ArrayFreeCallCount);
    }

    [TestMethod]
    public void DriverArrayOwnersUseDriverCreateQueryAndDestroyPaths()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        using (HipArray array = runtime.CreateArray(new HipArrayDescriptor(16, 4, HipArrayFormat.UnsignedInt8, 1)))
        {
            HipArrayDescriptor descriptor = array.GetDriverDescriptor();
            Assert.AreEqual(16UL, descriptor.Width);
            Assert.AreEqual(4UL, descriptor.Height);
        }

        using (HipArray array = runtime.CreateArray3D(new HipArray3DDescriptor(8, 4, 2, HipArrayFormat.FloatingPoint32, 2, HipArrayFlags.SurfaceLoadStore)))
        {
            HipArray3DDescriptor descriptor = array.GetDriver3DDescriptor();
            Assert.AreEqual(2UL, descriptor.Depth);
            Assert.AreEqual(2U, descriptor.ChannelCount);
        }

        Assert.AreEqual(2, native.ArrayFreeCallCount);
    }

    [TestMethod]
    public void PartialArrayHandlesAreCleanedWhenCreationFails()
    {
        using var native = new FakeHipNativeApi
        {
            ArrayTextureResult = HipError.OutOfMemory,
            ReturnArrayHandleOnFailure = true,
        };
        var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<HipException>(() => runtime.AllocateArray(ByteChannel, 8));
        Assert.ThrowsExactly<HipException>(() => runtime.CreateArray(new HipArrayDescriptor(8, 0, HipArrayFormat.UnsignedInt8, 1)));
        Assert.AreEqual(2, native.ArrayFreeCallCount);
    }

    [TestMethod]
    public void MipmappedLevelsAreBorrowedViewsThatLeaseTheirParent()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        HipMipmappedArray mipmapped = runtime.AllocateMipmappedArray(ByteChannel, 16, 8, 0, 4);
        HipArray level = mipmapped.GetLevel(2);
        Assert.AreEqual(4UL, level.Info.Width);
        Assert.AreEqual(2UL, level.Info.Height);

        mipmapped.Dispose();
        Assert.AreEqual(0, native.MipmappedArrayFreeCallCount);
        Assert.AreEqual(4UL, level.GetInfo().Width);
        level.Dispose();
        Assert.AreEqual(1, native.MipmappedArrayFreeCallCount);

        HipMipmappedArray driver = runtime.CreateMipmappedArray(
            new HipArray3DDescriptor(8, 4, 0, HipArrayFormat.UnsignedInt8, 1), 3);
        using (HipArray driverLevel = driver.GetLevel(1))
        {
            Assert.AreEqual(4UL, driverLevel.Info.Width);
        }
        driver.Dispose();
        Assert.AreEqual(2, native.MipmappedArrayFreeCallCount);
    }

    [TestMethod]
    public void TextureAndSurfaceObjectsRetainBackingArrays()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipArray array = runtime.AllocateArray(ByteChannel, 16, 4, HipArrayFlags.SurfaceLoadStore);
        var textureDescriptor = new HipTextureDescriptor
        {
            AddressModeX = HipTextureAddressMode.Clamp,
            FilterMode = HipTextureFilterMode.Linear,
            NormalizedCoordinates = true,
            MaximumMipmapLevelClamp = 3,
        };
        var view = new HipResourceViewDescriptor { Width = 16, Height = 4, Format = HipResourceViewFormat.UnsignedChar1 };
        HipTextureObject texture = runtime.CreateTextureObject(array, textureDescriptor, view);

        IntPtr arrayHandle = array.DangerousGetHandle();
        array.Dispose();
        Assert.AreEqual(0, native.ArrayFreeCallCount);
        Assert.AreEqual(arrayHandle, texture.GetResourceInfo().BorrowedHandle);
        Assert.AreEqual(HipTextureFilterMode.Linear, texture.GetTextureDescriptor().FilterMode);
        Assert.AreEqual(HipTextureFilterMode.Linear, texture.GetDriverTextureDescriptor().FilterMode);
        Assert.AreEqual(16UL, texture.GetResourceViewDescriptor().Width);

        texture.Dispose();
        Assert.AreEqual(1, native.TextureObjectDestroyCount);
        Assert.AreEqual(1, native.ArrayFreeCallCount);

        HipArray surfaceArray = runtime.AllocateArray(ByteChannel, 8, 8, HipArrayFlags.SurfaceLoadStore);
        HipSurfaceObject surface = runtime.CreateSurfaceObject(surfaceArray);
        surfaceArray.Dispose();
        Assert.AreEqual(1, native.ArrayFreeCallCount);
        surface.Dispose();
        Assert.AreEqual(1, native.SurfaceObjectDestroyCount);
        Assert.AreEqual(2, native.ArrayFreeCallCount);
    }

    [TestMethod]
    public void LinearTextureRetainsDeviceMemoryUntilDestroyed()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipDeviceMemory memory = runtime.Allocate(4096);
        HipTextureObject texture = runtime.CreateTextureObject(memory, ByteChannel, new HipTextureDescriptor());

        memory.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(HipTextureResourceKind.Linear, texture.GetResourceInfo().Kind);

        texture.Dispose();
        Assert.AreEqual(1, native.FreeCount);
    }

    [TestMethod]
    public void LegacyTextureReferenceKeepsBindingsAliveAndReturnsBorrowedHandles()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipArray array = runtime.AllocateArray(ByteChannel, 16, 2);
        HipTextureReference textureReference = runtime.GetTextureReference(new IntPtr(0x1234));
        IntPtr arrayHandle = array.DangerousGetHandle();

        textureReference.Bind(array);
        array.Dispose();
        Assert.AreEqual(0, native.ArrayFreeCallCount);
        Assert.AreEqual(arrayHandle, textureReference.GetBoundArrayHandle());
        Assert.AreEqual(0UL, textureReference.GetAlignmentOffset());

        textureReference.Unbind();
        Assert.AreEqual(1, native.TextureUnbindCount);
        Assert.AreEqual(1, native.ArrayFreeCallCount);
        textureReference.Dispose();

        HipMipmappedArray mipmapped = runtime.AllocateMipmappedArray(ByteChannel, 8, 8, 0, 3);
        using HipTextureReference mipReference = runtime.GetTextureReference(new IntPtr(0x5678));
        IntPtr mipHandle = mipmapped.DangerousGetHandle();
        mipReference.SetMipmappedArray(mipmapped, true);
        mipmapped.Dispose();
        Assert.AreEqual(mipHandle, mipReference.GetBoundMipmappedArrayHandle());
        mipReference.Dispose();
        Assert.AreEqual(1, native.MipmappedArrayFreeCallCount);
    }

    [TestMethod]
    public void ArrayTextureValidationAndCapabilityFailuresAreFailClosed()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new HipChannelFormatDescriptor(0, 0, 0, 0, HipChannelFormatKind.UnsignedInteger));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HipArrayDescriptor(1, 0, HipArrayFormat.UnsignedInt8, 3));

        using var native = new FakeHipNativeApi();
        using var foreignNative = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        var foreignRuntime = new HipRuntime(foreignNative);
        using HipArray array = runtime.AllocateArray(ByteChannel, 8);
        using HipArray foreignArray = foreignRuntime.AllocateArray(ByteChannel, 8);
        Assert.ThrowsExactly<ArgumentException>(() => runtime.CreateTextureObject(foreignArray, new HipTextureDescriptor()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.AllocateArray(ByteChannel, 0));

        native.ArrayTextureResult = HipError.NotSupported;
        Assert.ThrowsExactly<HipException>(() => runtime.CreateSurfaceObject(array));
    }

    [TestMethod]
    public void TextureReleaseCanBeRetriedAndPartialObjectIsCleaned()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipArray array = runtime.AllocateArray(ByteChannel, 8);
        HipTextureObject texture = runtime.CreateTextureObject(array, new HipTextureDescriptor());
        array.Dispose();

        native.ArrayTextureReleaseResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => texture.Dispose());
        Assert.IsFalse(texture.IsDisposed);
        Assert.AreEqual(0, native.ArrayFreeCallCount);

        native.ArrayTextureReleaseResult = HipError.Success;
        texture.Dispose();
        Assert.AreEqual(1, native.ArrayFreeCallCount);

        using HipArray second = runtime.AllocateArray(ByteChannel, 8);
        native.ArrayTextureResult = HipError.OutOfMemory;
        native.ReturnTextureObjectOnFailure = true;
        Assert.ThrowsExactly<HipException>(() => runtime.CreateTextureObject(second, new HipTextureDescriptor()));
        Assert.AreEqual(2, native.TextureObjectDestroyCount);
    }

    [TestMethod]
    public void DeviceReportsLinearTextureWidthThroughTypedDescriptor()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        Assert.AreEqual(1UL << 20, runtime.GetDevice(0).GetTexture1DLinearMaximumWidth(ByteChannel));
    }
}
