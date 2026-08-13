using System;
using System.Runtime.CompilerServices;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipPitchedMemoryTests
{
    [TestMethod]
    public void MemoryInfoAndPitchedAllocationsConvertNativeValues()
    {
        using var native = new FakeHipNativeApi
        {
            FreeMemoryBytes = 600,
            TotalMemoryBytes = 1000,
        };
        using var runtime = new HipRuntime(native);

        HipMemoryInfo info = runtime.GetMemoryInfo();
        using HipPitchedDeviceMemory<int> twoDimensional = runtime.Allocate2D<int>(3, 5);

        Assert.AreEqual(600UL, info.FreeBytes);
        Assert.AreEqual(1000UL, info.TotalBytes);
        Assert.AreEqual(400UL, info.UsedBytes);
        Assert.AreEqual(12UL, native.LastAllocationWidthBytes);
        Assert.AreEqual(5UL, native.LastAllocationHeight);
        Assert.AreEqual(1UL, native.LastAllocationDepth);
        Assert.AreEqual(16UL, twoDimensional.PitchBytes);
        Assert.AreEqual(80UL, twoDimensional.ByteLength);
        Assert.AreEqual(new HipMemoryExtent(3, 5), twoDimensional.Extent);

        using HipPitchedDeviceMemory<short> threeDimensional = runtime.Allocate3D<short>(5, 3, 2);
        Assert.AreEqual(10UL, native.LastAllocationWidthBytes);
        Assert.AreEqual(3UL, native.LastAllocationHeight);
        Assert.AreEqual(2UL, native.LastAllocationDepth);
        Assert.AreEqual(16UL, threeDimensional.PitchBytes);
        Assert.AreEqual(48UL, threeDimensional.SlicePitchBytes);
        Assert.AreEqual(96UL, threeDimensional.ByteLength);
    }

    [TestMethod]
    public void TwoDimensionalManagedAndDeviceCopiesPreserveRowsAndKinds()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipPitchedDeviceMemory<int> source = runtime.Allocate2D<int>(3, 2);
        using HipPitchedDeviceMemory<int> destination = runtime.Allocate2D<int>(3, 2);
        int[] expected = { 1, 2, 3, 4, 5, 6 };
        var actual = new int[expected.Length];

        source.CopyFrom(expected);
        Assert.AreEqual(HipMemoryCopyKind.HostToDevice, native.LastPitchedCopyKind);
        Assert.AreEqual(12UL, native.LastCopyWidthBytes);
        Assert.AreEqual(2UL, native.LastCopyHeight);
        Assert.AreEqual(12UL, native.LastSourcePitch);
        Assert.AreEqual(source.PitchBytes, native.LastDestinationPitch);

        destination.CopyFrom(source);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToDevice, native.LastPitchedCopyKind);
        Assert.AreEqual(source.PitchBytes, native.LastSourcePitch);
        Assert.AreEqual(destination.PitchBytes, native.LastDestinationPitch);

        destination.CopyTo(actual);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToHost, native.LastPitchedCopyKind);
        CollectionAssert.AreEqual(expected, actual);
    }

    [TestMethod]
    public void ThreeDimensionalSubregionCopiesCarryByteXAndElementYZ()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipPitchedDeviceMemory<int> source = runtime.Allocate3D<int>(4, 3, 2);
        using HipPitchedDeviceMemory<int> destination = runtime.Allocate3D<int>(5, 4, 3);
        int[] values =
        {
            1, 2, 3, 4,
            5, 6, 7, 8,
            9, 10, 11, 12,
            13, 14, 15, 16,
            17, 18, 19, 20,
            21, 22, 23, 24,
        };
        source.CopyFrom(values);
        var region = new HipMemoryRegion(new HipMemoryOffset(1, 1, 0), new HipMemoryExtent(2, 2, 2));

        destination.CopyFrom(source, region, new HipMemoryOffset(2, 1, 1));

        HipMemcpy3DParameters parameters = native.LastMemcpy3DParameters;
        Assert.AreEqual(4UL, parameters.SourcePosition.X.ToUInt64());
        Assert.AreEqual(1UL, parameters.SourcePosition.Y.ToUInt64());
        Assert.AreEqual(0UL, parameters.SourcePosition.Z.ToUInt64());
        Assert.AreEqual(8UL, parameters.DestinationPosition.X.ToUInt64());
        Assert.AreEqual(1UL, parameters.DestinationPosition.Y.ToUInt64());
        Assert.AreEqual(1UL, parameters.DestinationPosition.Z.ToUInt64());
        Assert.AreEqual(8UL, parameters.Extent.Width.ToUInt64());
        Assert.AreEqual(2UL, parameters.Extent.Height.ToUInt64());
        Assert.AreEqual(2UL, parameters.Extent.Depth.ToUInt64());
        Assert.AreEqual(HipMemoryCopyKind.DeviceToDevice, parameters.Kind);
    }

    [TestMethod]
    public void MemsetSelectsOneTwoAndThreeDimensionalNativeCalls()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipPitchedDeviceMemory<int> memory = runtime.Allocate3D<int>(4, 3, 2);

        memory.SetByte(0x7f, new HipMemoryRegion(new HipMemoryOffset(1, 0, 0), new HipMemoryExtent(2, 1, 1)));
        Assert.AreEqual(8UL, native.LastMemsetExtent.Width.ToUInt64());
        Assert.AreEqual(1UL, native.LastMemsetExtent.Height.ToUInt64());
        Assert.AreEqual(1UL, native.LastMemsetExtent.Depth.ToUInt64());

        memory.SetZero(new HipMemoryRegion(default, new HipMemoryExtent(4, 3, 1)));
        Assert.AreEqual(16UL, native.LastMemsetExtent.Width.ToUInt64());
        Assert.AreEqual(3UL, native.LastMemsetExtent.Height.ToUInt64());
        Assert.AreEqual(1UL, native.LastMemsetExtent.Depth.ToUInt64());

        memory.SetZero();
        Assert.AreEqual(16UL, native.LastMemsetExtent.Width.ToUInt64());
        Assert.AreEqual(3UL, native.LastMemsetExtent.Height.ToUInt64());
        Assert.AreEqual(2UL, native.LastMemsetExtent.Depth.ToUInt64());
        Assert.AreEqual(0, native.LastMemsetValue);
    }

    [TestMethod]
    public void PinnedHostCopiesSupportByteOffsetsAndAsyncLifetime()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipPitchedDeviceMemory<int> memory = runtime.Allocate3D<int>(2, 2, 2);
        HipPinnedMemory pinned = runtime.AllocatePinned(48);
        byte[] storage = new byte[48];
        for (int index = 8; index < 40; index++) storage[index] = (byte)index;
        pinned.CopyFrom(storage);

        memory.CopyFromAsync(pinned, stream, 8);
        Assert.AreNotEqual(IntPtr.Zero, native.LastPitchedStream);
        memory.Dispose();
        pinned.Dispose();
        Assert.AreEqual(0, native.FreeCount);

        stream.Synchronize();
        Assert.AreEqual(2, native.FreeCount);
        Assert.IsTrue(memory.IsDisposed);
        Assert.IsTrue(pinned.IsDisposed);
    }

    [TestMethod]
    public void AsyncDeviceCopyAndMemsetRetainOwnersUntilStreamCompletion()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipPitchedDeviceMemory<float> source = runtime.Allocate2D<float>(4, 2);
        HipPitchedDeviceMemory<float> destination = runtime.Allocate2D<float>(4, 2);

        source.SetZeroAsync(stream);
        destination.CopyFromAsync(source, stream);
        source.Dispose();
        destination.Dispose();
        Assert.AreEqual(0, native.FreeCount);

        stream.Synchronize();
        Assert.AreEqual(2, native.FreeCount);
        Assert.IsTrue(source.IsDisposed);
        Assert.IsTrue(destination.IsDisposed);
    }

    [TestMethod]
    public void RuntimeDeviceAndStreamOwnershipFailuresPrecedeNativeCalls()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        using var firstRuntime = new HipRuntime(firstNative);
        using var secondRuntime = new HipRuntime(secondNative);
        using HipPitchedDeviceMemory<int> first = firstRuntime.Allocate2D<int>(2, 2);
        using HipPitchedDeviceMemory<int> second = secondRuntime.Allocate2D<int>(2, 2);
        using HipStream otherStream = secondRuntime.CreateStream();

        Assert.ThrowsExactly<ArgumentException>(() => first.CopyFrom(second));
        Assert.ThrowsExactly<ArgumentException>(() => first.SetZeroAsync(otherStream));
        Assert.AreEqual(0UL, firstNative.LastCopyWidthBytes);

        firstNative.SetDevice(1);
        Assert.ThrowsExactly<InvalidOperationException>(() => first.SetZero());
        firstNative.SetDevice(0);
        firstRuntime.GetDevice(1).MakeCurrent();
        using HipStream wrongDeviceStream = firstRuntime.CreateStream();
        firstNative.SetDevice(0);
        Assert.ThrowsExactly<ArgumentException>(() => first.SetZeroAsync(wrongDeviceStream));

        firstRuntime.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => firstRuntime.GetMemoryInfo());
        Assert.ThrowsExactly<ObjectDisposedException>(() => firstRuntime.Allocate2D<int>(1, 1));
    }

    [TestMethod]
    public void DisposedMemoryAndStreamAreRejectedBeforePitchedCalls()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipPitchedDeviceMemory<int> memory = runtime.Allocate2D<int>(2, 2);
        HipStream stream = runtime.CreateStream();
        stream.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.SetZeroAsync(stream));
        memory.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.SetZero());
        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.CopyFrom(new int[4]));
    }

    [TestMethod]
    public void InvalidDimensionsRegionsAndHostCapacityFailBeforeNativeCalls()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.Allocate2D<int>(0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.Allocate3D<int>(1, 0, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.Allocate2D<long>(ulong.MaxValue, 1));
        using HipPitchedDeviceMemory<int> memory = runtime.Allocate2D<int>(3, 2);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => memory.SetZero(new HipMemoryRegion(default, new HipMemoryExtent(4, 1))));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => memory.CopyFrom(new int[5]));
        using HipPinnedMemory pinned = runtime.AllocatePinned(8);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => memory.CopyFrom(pinned));
        Assert.AreEqual(0UL, native.LastCopyWidthBytes);
    }

    [TestMethod]
    public void AllocationRejectsNativePitchSmallerThanRowWidthAndReleasesPointer()
    {
        using var native = new FakeHipNativeApi { ForcedAllocationPitch = 7 };
        using var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.Allocate2D<int>(2, 2));

        Assert.AreEqual(1, native.FreeCount);
    }

    [TestMethod]
    public void AllocationMetadataOverflowReleasesNativePointerExactlyOnce()
    {
        using var native = new FakeHipNativeApi { ForcedAllocationYSize = ulong.MaxValue };
        using var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.Allocate3D<int>(2, 2, 2));

        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.FreeCallCount);
    }

    [TestMethod]
    public void NativeErrorsBecomeHipExceptionsAndDisposeCanRetry()
    {
        using var native = new FakeHipNativeApi { MemoryInfoResult = HipError.NotSupported };
        using var runtime = new HipRuntime(native);
        HipException unavailable = Assert.ThrowsExactly<HipException>(() => runtime.GetMemoryInfo());
        Assert.AreEqual(HipError.NotSupported, unavailable.Error);
        Assert.AreEqual("hipMemGetInfo", unavailable.Operation);

        native.MemoryInfoResult = HipError.Success;
        using HipPitchedDeviceMemory<int> memory = runtime.Allocate2D<int>(2, 2);
        native.MemsetResult = HipError.InvalidValue;
        HipException memset = Assert.ThrowsExactly<HipException>(() => memory.SetZero());
        Assert.AreEqual("hipMemset2D", memset.Operation);

        native.MemsetResult = HipError.Success;
        native.FreeResult = HipError.InvalidValue;
        HipException free = Assert.ThrowsExactly<HipException>(() => memory.Dispose());
        Assert.AreEqual("hipFree", free.Operation);
        native.FreeResult = HipError.Success;
        memory.Dispose();
        memory.Dispose();
        Assert.AreEqual(1, native.FreeCount);
    }

    [TestMethod]
    public void FailedAsyncSubmissionReleasesAllBorrowsImmediately()
    {
        using var native = new FakeHipNativeApi { PitchedCopyResult = HipError.InvalidValue };
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipPitchedDeviceMemory<int> source = runtime.Allocate2D<int>(2, 2);
        HipPitchedDeviceMemory<int> destination = runtime.Allocate2D<int>(2, 2);

        Assert.ThrowsExactly<HipException>(() => destination.CopyFromAsync(source, stream));
        source.Dispose();
        destination.Dispose();
        Assert.AreEqual(2, native.FreeCount);

        native.PitchedCopyResult = HipError.Success;
        using HipPitchedDeviceMemory<int> memset = runtime.Allocate2D<int>(2, 2);
        native.MemsetResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => memset.SetZeroAsync(stream));
        memset.Dispose();
        Assert.AreEqual(3, native.FreeCount);
    }

    [TestMethod]
    public void AsyncLeaseIsRetainedUntilSuccessfulQueryAndReleasedByStreamDispose()
    {
        using var native = new FakeHipNativeApi { StreamQueryResult = HipError.NotReady };
        using var runtime = new HipRuntime(native);
        HipStream queryStream = runtime.CreateStream();
        HipPitchedDeviceMemory<int> queryMemory = runtime.Allocate2D<int>(2, 2);

        queryMemory.SetZeroAsync(queryStream);
        queryMemory.Dispose();
        Assert.IsFalse(queryStream.Query());
        Assert.AreEqual(0, native.FreeCount);

        native.StreamQueryResult = HipError.Success;
        Assert.IsTrue(queryStream.Query());
        Assert.AreEqual(1, native.FreeCount);
        queryStream.Dispose();

        HipStream disposeStream = runtime.CreateStream();
        HipPitchedDeviceMemory<int> disposeMemory = runtime.Allocate2D<int>(2, 2);
        disposeMemory.SetZeroAsync(disposeStream);
        disposeMemory.Dispose();

        disposeStream.Dispose();

        Assert.AreEqual(2, native.FreeCount);
        Assert.IsTrue(disposeMemory.IsDisposed);
    }

    [TestMethod]
    public void SafeHandleFinalizerReleasesAbandonedPitchedOwnerWithoutThrowing()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        AbandonPitchedOwner(runtime);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.AreEqual(1, native.FreeCount);
    }

    [TestMethod]
    public void ValueTypesValidateAndCompareDeterministically()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HipMemoryInfo(2, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HipMemoryExtent(1, 1, 0));
        var extent = new HipMemoryExtent(4, 3, 2);
        var offset = new HipMemoryOffset(1, 2, 3);
        var region = new HipMemoryRegion(offset, extent);

        Assert.AreEqual(24UL, extent.ElementCount);
        Assert.AreEqual("4x3x2", extent.ToString());
        Assert.AreEqual("1,2,3", offset.ToString());
        Assert.AreEqual(new HipMemoryRegion(new HipMemoryOffset(1, 2, 3), new HipMemoryExtent(4, 3, 2)), region);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonPitchedOwner(HipRuntime runtime) => _ = runtime.Allocate3D<int>(2, 2, 2);
}
