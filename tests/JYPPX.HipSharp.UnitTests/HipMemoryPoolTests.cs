using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipMemoryPoolTests
{
    [TestMethod]
    public void NativePoolAbiLayoutsAndEnumValuesArePinned()
    {
        Assert.AreEqual(88, Marshal.SizeOf<HipMemoryPoolPropertiesNative>());
        Assert.AreEqual(0, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.AllocationType)).ToInt32());
        Assert.AreEqual(4, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.HandleTypes)).ToInt32());
        Assert.AreEqual(8, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.Location)).ToInt32());
        Assert.AreEqual(16, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.Win32SecurityAttributes)).ToInt32());
        Assert.AreEqual(24, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.MaximumSize)).ToInt32());
        Assert.AreEqual(32, Marshal.OffsetOf<HipMemoryPoolPropertiesNative>(nameof(HipMemoryPoolPropertiesNative.Reserved0)).ToInt32());
        Assert.AreEqual(12, Marshal.SizeOf<HipMemoryPoolAccessDescriptorNative>());
        Assert.AreEqual(8, Marshal.OffsetOf<HipMemoryPoolAccessDescriptorNative>(nameof(HipMemoryPoolAccessDescriptorNative.Access)).ToInt32());
        int[] accessValues = Array.ConvertAll(Enum.GetValues<HipMemoryPoolAccess>(), value => (int)value);
        Assert.HasCount(2, accessValues);
        Assert.AreEqual(0, accessValues[0]);
        Assert.AreEqual(3, accessValues[1]);
        int[] attributeValues = Array.ConvertAll(Enum.GetValues<HipMemoryPoolAttributeNative>(), value => (int)value);
        CollectionAssert.Contains(attributeValues, 1);
        CollectionAssert.Contains(attributeValues, 8);
    }

    [TestMethod]
    public void CustomPoolAppliesTypedOptionsAndExplicitDestroyIsIdempotent()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetCurrentDevice();
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(device)
        {
            ReleaseThresholdBytes = 64,
            MaximumSizeBytes = 4096,
            AllowEventDependencyReuse = false,
            AllowOpportunisticReuse = true,
            AllowInternalDependencyReuse = false,
        });

        Assert.IsTrue(pool.OwnsHandle);
        Assert.AreEqual(device.Ordinal, pool.DeviceOrdinal);
        Assert.AreEqual(64UL, pool.ReleaseThresholdBytes);
        Assert.AreEqual(4096UL, native.LastPoolMaximumSizeBytes);
        Assert.IsFalse(pool.AllowEventDependencyReuse);
        Assert.IsTrue(pool.AllowOpportunisticReuse);
        Assert.IsFalse(pool.AllowInternalDependencyReuse);
        Assert.AreEqual(1, native.MemoryPoolCreateCount);
        Assert.AreEqual(4, native.MemoryPoolSetAttributeCount);

        pool.Dispose();
        pool.Dispose();
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);
    }

    [TestMethod]
    public void DefaultAndCurrentPoolViewsAreBorrowedAndNeverDestroyRuntimeOwnedPools()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetCurrentDevice();

        HipMemoryPool defaultPool = runtime.GetDefaultMemoryPool(device);
        HipMemoryPool currentPool = runtime.GetCurrentMemoryPool(device);
        Assert.IsFalse(defaultPool.OwnsHandle);
        Assert.IsFalse(currentPool.OwnsHandle);
        defaultPool.Dispose();
        currentPool.Dispose();

        Assert.AreEqual(0, native.MemoryPoolDestroyCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() => currentPool.GetStatistics());
    }

    [TestMethod]
    public void CurrentPoolScopeRestoresPreviousPoolAndKeepsCustomPoolAlive()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetCurrentDevice();
        using HipMemoryPool previous = runtime.GetCurrentMemoryPool(device);
        HipMemoryPool custom = runtime.CreateMemoryPool(new HipMemoryPoolOptions(device));

        HipMemoryPoolCurrentScope scope = custom.UseAsCurrent();
        using HipMemoryPool selected = runtime.GetCurrentMemoryPool(device);
        Assert.IsFalse(selected.OwnsHandle);
        Assert.ThrowsExactly<InvalidOperationException>(() => custom.Dispose());

        scope.Dispose();
        scope.Dispose();
        using HipMemoryPool restored = runtime.GetCurrentMemoryPool(device);
        Assert.AreEqual(previous.ReleaseThresholdBytes, restored.ReleaseThresholdBytes);
        custom.Dispose();
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);
    }

    [TestMethod]
    public void CurrentPoolScopesRequireLifoAndNativeRestoreFailureCanRetry()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetCurrentDevice();
        using HipMemoryPool firstPool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(device));
        using HipMemoryPool secondPool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(device));
        HipMemoryPoolCurrentScope first = firstPool.UseAsCurrent();
        HipMemoryPoolCurrentScope second = secondPool.UseAsCurrent();

        Assert.ThrowsExactly<InvalidOperationException>(() => first.Dispose());
        native.DeviceSetMemoryPoolResult = HipError.InvalidValue;
        HipException restore = Assert.ThrowsExactly<HipException>(() => second.Dispose());
        Assert.AreEqual("hipDeviceSetMemPool", restore.Operation);
        Assert.IsFalse(second.IsDisposed);
        native.DeviceSetMemoryPoolResult = HipError.Success;
        second.Dispose();
        first.Dispose();
    }

    [TestMethod]
    public void TypedAttributesUseNativeWidthsAndStatisticsResetHighWatermarks()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));
        HipPooledDeviceMemory memory = pool.AllocateAsync(32, stream);

        HipMemoryPoolStatistics active = pool.GetStatistics();
        Assert.AreEqual(32UL, active.UsedBytes);
        Assert.AreEqual(32UL, active.UsedHighWatermarkBytes);
        pool.ResetUsedHighWatermark();
        pool.ResetReservedHighWatermark();
        pool.TrimTo(0);
        HipMemoryPoolStatistics reset = pool.GetStatistics();
        Assert.AreEqual(0UL, reset.UsedHighWatermarkBytes);
        Assert.AreEqual(0UL, reset.ReservedHighWatermarkBytes);
        Assert.AreEqual(32UL, reset.ReservedBytes);

        memory.Dispose();
        Assert.ThrowsExactly<InvalidOperationException>(() => pool.Dispose());
        stream.Synchronize();
        Assert.AreEqual(0UL, pool.GetStatistics().UsedBytes);
    }

    [TestMethod]
    public void AccessDescriptorsValidateRuntimeAndMapDeviceLocations()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipDevice first = runtime.GetDevice(0);
        HipDevice second = runtime.GetDevice(1);
        using HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(first));

        pool.SetAccess(second, HipMemoryPoolAccess.ReadWrite);
        Assert.AreEqual(HipMemoryPoolAccess.ReadWrite, pool.GetAccess(second));
        pool.SetAccess(new HipMemoryPoolAccessDescriptor(second, HipMemoryPoolAccess.None));
        Assert.AreEqual(HipMemoryPoolAccess.None, pool.GetAccess(second));
        Assert.AreEqual(2, native.MemoryPoolSetAccessCount);
        Assert.AreEqual(2, native.MemoryPoolGetAccessCount);

        using var otherNative = new FakeHipNativeApi();
        using var otherRuntime = new HipRuntime(otherNative);
        HipDevice foreign = otherRuntime.GetCurrentDevice();
        Assert.ThrowsExactly<ArgumentException>(() => pool.SetAccess(foreign, HipMemoryPoolAccess.ReadWrite));
        Assert.ThrowsExactly<ArgumentException>(() => pool.GetAccess(foreign));
        Assert.ThrowsExactly<ArgumentException>(() => pool.SetAccess(Array.Empty<HipMemoryPoolAccessDescriptor>()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new HipMemoryPoolAccessDescriptor(first, (HipMemoryPoolAccess)1));
    }

    [TestMethod]
    public void PoolAllocationUsesExactPoolBytesAndStreamAndCopiesRoundTrip()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));
        HipPooledDeviceMemory memory = pool.AllocateAsync(8, stream);
        byte[] expected = { 1, 2, 3, 4, 5, 6, 7, 8 };
        var actual = new byte[8];

        Assert.AreEqual(1, native.PendingPoolAllocationCount);
        memory.CopyFromAsync(expected);
        memory.CopyToAsync(actual);
        stream.Synchronize();

        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(8UL, native.LastPoolAllocationBytes);
        Assert.AreNotEqual(IntPtr.Zero, native.LastPoolHandle);
        Assert.AreNotEqual(IntPtr.Zero, native.LastPoolAllocationStream);
        Assert.AreSame(pool, memory.Pool);
        Assert.AreSame(stream, memory.AllocationStream);
        Assert.AreEqual(0, native.PendingPoolAllocationCount);
        memory.Dispose();
        stream.Synchronize();
        Assert.AreEqual(1, native.AsyncFreeCount);
        Assert.AreEqual(0, native.PendingPoolAllocationCount);
    }

    [TestMethod]
    public void PooledFreeKeepsPoolUntilStreamCompletionAndStreamCanDrainIt()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipStream stream = runtime.CreateStream();
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));
        HipPooledDeviceMemory memory = pool.AllocateAsync(16, stream);

        Assert.ThrowsExactly<InvalidOperationException>(() => pool.Dispose());
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Dispose());
        memory.Dispose();
        Assert.ThrowsExactly<InvalidOperationException>(() => pool.Dispose());
        Assert.AreEqual(0, native.AsyncFreeCount);

        stream.Dispose();
        Assert.AreEqual(1, native.AsyncFreeCount);
        pool.Dispose();
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);
    }

    [TestMethod]
    public void QueryCompletionDrainsSubmittedFreeBeforePoolDestroy()
    {
        using var native = new FakeHipNativeApi { StreamQueryResult = HipError.NotReady };
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));
        HipPooledDeviceMemory memory = pool.AllocateAsync(16, stream);
        memory.Dispose();

        Assert.IsFalse(stream.Query());
        Assert.ThrowsExactly<InvalidOperationException>(() => pool.Dispose());
        native.StreamQueryResult = HipError.Success;
        Assert.IsTrue(stream.Query());
        pool.Dispose();
    }

    [TestMethod]
    public void OwnershipAndDisposedChecksPrecedeNativeAllocation()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        using var firstRuntime = new HipRuntime(firstNative);
        using var secondRuntime = new HipRuntime(secondNative);
        using HipMemoryPool pool = firstRuntime.CreateMemoryPool(new HipMemoryPoolOptions(firstRuntime.GetDevice(0)));
        using HipStream foreignStream = secondRuntime.CreateStream();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => pool.AllocateAsync(0, foreignStream));
        Assert.ThrowsExactly<ArgumentException>(() => pool.AllocateAsync(1, foreignStream));
        firstRuntime.GetDevice(1).MakeCurrent();
        using HipStream wrongDevice = firstRuntime.CreateStream();
        Assert.ThrowsExactly<ArgumentException>(() => pool.AllocateAsync(1, wrongDevice));
        HipStream disposedStream = firstRuntime.CreateStream();
        disposedStream.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => pool.AllocateAsync(1, disposedStream));
        Assert.AreEqual(0, firstNative.PoolAllocationCount);

        pool.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => pool.TrimTo(0));
        firstRuntime.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => firstRuntime.GetDefaultMemoryPool(firstRuntime.GetDevice(0)));
    }

    [TestMethod]
    public void ExistingPoolOwnerSurvivesLightweightRuntimeFacadeDisposal()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));

        runtime.Dispose();

        pool.ReleaseThresholdBytes = 128;
        Assert.AreEqual(128UL, pool.ReleaseThresholdBytes);
        pool.TrimTo(0);
        pool.Dispose();
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);
    }

    [TestMethod]
    public void PoolNativeFailuresPreserveOperationAndSupportDestroyAndFreeRetry()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));

        native.MemoryPoolAttributeResult = HipError.NotSupported;
        HipException attribute = Assert.ThrowsExactly<HipException>(() => _ = pool.ReleaseThresholdBytes);
        Assert.AreEqual("hipMemPoolGetAttribute", attribute.Operation);
        native.MemoryPoolAttributeResult = HipError.Success;

        native.MemoryPoolTrimResult = HipError.InvalidValue;
        Assert.AreEqual("hipMemPoolTrimTo", Assert.ThrowsExactly<HipException>(() => pool.TrimTo(0)).Operation);
        native.MemoryPoolTrimResult = HipError.Success;

        native.MemoryPoolAccessResult = HipError.InvalidDevice;
        Assert.AreEqual("hipMemPoolSetAccess", Assert.ThrowsExactly<HipException>(() => pool.SetAccess(runtime.GetCurrentDevice(), HipMemoryPoolAccess.ReadWrite)).Operation);
        native.MemoryPoolAccessResult = HipError.Success;

        HipPooledDeviceMemory memory = pool.AllocateAsync(8, stream);
        native.FreeAsyncResult = HipError.InvalidValue;
        Assert.AreEqual("hipFreeAsync", Assert.ThrowsExactly<HipException>(() => memory.Dispose()).Operation);
        native.FreeAsyncResult = HipError.Success;
        memory.Dispose();
        stream.Synchronize();

        native.MemoryPoolDestroyResult = HipError.InvalidValue;
        Assert.AreEqual("hipMemPoolDestroy", Assert.ThrowsExactly<HipException>(() => pool.Dispose()).Operation);
        native.MemoryPoolDestroyResult = HipError.Success;
        pool.Dispose();
    }

    [TestMethod]
    public void CreateAndAllocationFailuresCleanPartialOwnersWithoutFallback()
    {
        using var native = new FakeHipNativeApi
        {
            MemoryPoolCreateResult = HipError.InvalidValue,
            ReturnMemoryPoolOnFailure = true,
        };
        using var runtime = new HipRuntime(native);
        Assert.AreEqual("hipMemPoolCreate", Assert.ThrowsExactly<HipException>(() => runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()))).Operation);
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);

        native.MemoryPoolCreateResult = HipError.Success;
        native.ReturnMemoryPoolOnFailure = false;
        native.ReturnNullMemoryPoolOnSuccess = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice())));
        native.ReturnNullMemoryPoolOnSuccess = false;

        using HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));
        using HipStream stream = runtime.CreateStream();
        native.MallocFromPoolResult = HipError.OutOfMemory;
        native.ReturnPoolPointerOnFailure = true;
        HipException allocation = Assert.ThrowsExactly<HipException>(() => pool.AllocateAsync(8, stream));
        Assert.AreEqual("hipMallocFromPoolAsync", allocation.Operation);
        Assert.AreEqual(0, native.AsyncAllocationCount);
        stream.Synchronize();

        native.MallocFromPoolResult = HipError.Success;
        native.ReturnPoolPointerOnFailure = false;
        native.ReturnNullPoolPointerOnSuccess = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => pool.AllocateAsync(8, stream));
        native.ReturnNullPoolPointerOnSuccess = false;

        native.MallocFromPoolResult = HipError.NotSupported;
        native.ReturnPoolPointerOnFailure = false;
        HipException unavailable = Assert.ThrowsExactly<HipException>(() => pool.AllocateAsync(8, stream));
        Assert.AreEqual(HipError.NotSupported, unavailable.Error);
        Assert.AreEqual("hipMallocFromPoolAsync", unavailable.Operation);
        Assert.AreEqual(0, native.AsyncAllocationCount);
    }

    [TestMethod]
    public void SafeHandleFinalReleaseNeverThrowsAndEventuallyReleasesPool()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        AbandonPool(runtime);
        CollectFinalizers();
        Assert.AreEqual(1, native.MemoryPoolDestroyCount);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonPool(HipRuntime runtime) => _ = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()));

    private static void CollectFinalizers()
    {
        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
    }
}
