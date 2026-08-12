using System;
using System.Linq;
using JYPPX.HipSharp.Graphs;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipKernelOccupancyTests
{
    private static readonly int[] ExpectedFunctionAttributeValues = { 0, 1, 2, 3, 4, 6, 8 };
    private static readonly int[] ExpectedOccupancyFlagValues = { 0, 1 };
    private static readonly int[] ExpectedDeviceAttributeValues = { 11, 64, 89 };

    [TestMethod]
    public void PinnedKernelOccupancyAndDeviceEnumValuesAreStable()
    {
        CollectionAssert.AreEqual(
            ExpectedFunctionAttributeValues,
            new[]
            {
                (int)HipFunctionAttributeNative.MaxThreadsPerBlock,
                (int)HipFunctionAttributeNative.SharedSizeBytes,
                (int)HipFunctionAttributeNative.ConstantSizeBytes,
                (int)HipFunctionAttributeNative.LocalSizeBytes,
                (int)HipFunctionAttributeNative.NumberOfRegisters,
                (int)HipFunctionAttributeNative.BinaryVersion,
                (int)HipFunctionAttributeNative.MaxDynamicSharedSizeBytes,
            });
        CollectionAssert.AreEqual(
            ExpectedOccupancyFlagValues,
            new[] { (int)HipOccupancyFlags.Default, (int)HipOccupancyFlags.DisableCachingOverride });
        CollectionAssert.AreEqual(
            ExpectedDeviceAttributeValues,
            new[]
            {
                (int)HipDeviceAttribute.CooperativeLaunch,
                (int)HipDeviceAttribute.MultiprocessorCount,
                (int)HipDeviceAttribute.WarpSize,
            });
    }

    [TestMethod]
    public void DeviceConveniencePropertiesValidateTypedResults()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetDevice(0);

        Assert.IsTrue(device.SupportsCooperativeLaunch);
        Assert.AreEqual(20, device.MultiprocessorCount);
        Assert.AreEqual(64, device.WarpSize);

        native.CooperativeLaunchCapability = 2;
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = device.SupportsCooperativeLaunch);
        native.MultiprocessorCountValue = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = device.MultiprocessorCount);
        native.WarpSizeValue = -1;
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = device.WarpSize);
    }

    [TestMethod]
    public void GetAttributesMapsEverySelectedFunctionAttribute()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        HipKernelAttributes attributes = kernel.GetAttributes();

        Assert.AreEqual(1024, attributes.MaximumThreadsPerBlock);
        Assert.AreEqual(2048UL, attributes.StaticSharedMemoryBytes);
        Assert.AreEqual(128UL, attributes.ConstantMemoryBytes);
        Assert.AreEqual(32UL, attributes.LocalMemoryBytesPerThread);
        Assert.AreEqual(24, attributes.RegistersPerThread);
        Assert.AreEqual(1100, attributes.BinaryVersion);
        Assert.AreEqual(65536UL, attributes.MaximumDynamicSharedMemoryBytes);
        CollectionAssert.AreEqual(
            new[]
            {
                HipFunctionAttributeNative.MaxThreadsPerBlock,
                HipFunctionAttributeNative.SharedSizeBytes,
                HipFunctionAttributeNative.ConstantSizeBytes,
                HipFunctionAttributeNative.LocalSizeBytes,
                HipFunctionAttributeNative.NumberOfRegisters,
                HipFunctionAttributeNative.BinaryVersion,
                HipFunctionAttributeNative.MaxDynamicSharedSizeBytes,
            },
            native.FunctionAttributeCalls.ToArray());
    }

    [TestMethod]
    public void GetAttributesFailsClosedForInvalidOutputFailureAndDisposedModule()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        native.FunctionAttributes[HipFunctionAttributeNative.ConstantSizeBytes] = -1;

        Assert.ThrowsExactly<InvalidOperationException>(() => kernel.GetAttributes());
        Assert.AreEqual(3, native.FunctionAttributeCalls.Count);

        native.FunctionAttributes[HipFunctionAttributeNative.ConstantSizeBytes] = 1;
        native.FunctionAttributeResults[HipFunctionAttributeNative.LocalSizeBytes] = HipError.InvalidValue;
        HipException failure = Assert.ThrowsExactly<HipException>(() => kernel.GetAttributes());
        Assert.AreEqual("hipFuncGetAttribute", failure.Operation);

        module.Dispose();
        int calls = native.FunctionAttributeCalls.Count;
        Assert.ThrowsExactly<ObjectDisposedException>(() => kernel.GetAttributes());
        Assert.AreEqual(calls, native.FunctionAttributeCalls.Count);
    }

    [TestMethod]
    public void OccupancyRoutesDefaultAndFlaggedQueriesWithUnits()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        HipOccupancyInfo first = kernel.GetOccupancy(128, 3072);
        Assert.AreEqual(128, first.BlockSize);
        Assert.AreEqual(3072UL, first.DynamicSharedMemoryBytes);
        Assert.AreEqual(4, first.ActiveBlocksPerMultiprocessor);
        Assert.AreEqual(20, first.MultiprocessorCount);
        Assert.AreEqual(80L, first.MaximumResidentBlocks);
        Assert.AreEqual(1, native.OccupancyNonFlagsCallCount);
        Assert.AreEqual(0, native.OccupancyFlagsCallCount);

        HipOccupancyInfo second = kernel.GetOccupancy(64, 4096, HipOccupancyFlags.DisableCachingOverride);
        Assert.AreEqual(64, native.LastOccupancyBlockSize);
        Assert.AreEqual(4096UL, native.LastOccupancyDynamicSharedMemoryBytes);
        Assert.AreEqual(1U, native.LastOccupancyFlags);
        Assert.AreEqual(1, native.OccupancyFlagsCallCount);
        Assert.AreEqual(80L, second.MaximumResidentBlocks);
    }

    [TestMethod]
    public void OccupancyPlanRoutesLimitAndRejectsInvalidInputsAndOutputs()
    {
        using var native = new FakeHipNativeApi
        {
            PotentialMinimumGridSize = 40,
            PotentialBlockSize = 256,
        };
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        HipOccupancyPlan plan = kernel.GetOccupancyPlan(8192, 512, HipOccupancyFlags.DisableCachingOverride);
        Assert.AreEqual(40, plan.MinimumGridSize);
        Assert.AreEqual(256, plan.BlockSize);
        Assert.AreEqual(80L, plan.Occupancy.MaximumResidentBlocks);
        Assert.AreEqual(512, native.LastPotentialBlockSizeLimit);
        Assert.AreEqual(8192UL, native.LastOccupancyDynamicSharedMemoryBytes);
        Assert.AreEqual(2, native.OccupancyFlagsCallCount);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => kernel.GetOccupancy(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => kernel.GetOccupancy(1, flags: (HipOccupancyFlags)2));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => kernel.GetOccupancyPlan(blockSizeLimit: -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => kernel.GetOccupancyPlan(flags: (HipOccupancyFlags)3));

        native.PotentialMinimumGridSize = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() => kernel.GetOccupancyPlan());
        native.PotentialMinimumGridSize = 1;
        native.PotentialBlockSize = -1;
        Assert.ThrowsExactly<InvalidOperationException>(() => kernel.GetOccupancyPlan());
        native.PotentialBlockSize = 1;
        native.ActiveBlocksPerMultiprocessor = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() => kernel.GetOccupancyPlan());
    }

    [TestMethod]
    public void OccupancyRequiresModuleDeviceAndPropagatesNativeFailures()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        runtime.GetDevice(1).MakeCurrent();

        Assert.ThrowsExactly<InvalidOperationException>(() => kernel.GetOccupancy(1));
        Assert.AreEqual(0, native.OccupancyNonFlagsCallCount);

        runtime.GetDevice(0).MakeCurrent();
        native.OccupancyResult = HipError.NotSupported;
        HipException failure = Assert.ThrowsExactly<HipException>(() => kernel.GetOccupancy(1));
        Assert.AreEqual(HipError.NotSupported, failure.Error);
        Assert.AreEqual("hipModuleOccupancyMaxActiveBlocksPerMultiprocessor", failure.Operation);
    }

    [TestMethod]
    public void CooperativeLaunchMarshalsThreeDimensionalConfigurationAndArguments()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        native.ExpectedKernelPointerArguments.Add(false);
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        using HipDeviceMemory memory = runtime.Allocate(4);
        HipKernel kernel = module.GetKernel("kernel");

        kernel.LaunchCooperative(
            new HipLaunchDimensions(2, 2, 5),
            new HipLaunchDimensions(8, 4, 2),
            new[] { HipKernelArgument.DevicePointer(memory), HipKernelArgument.Scalar32(17) },
            2048);

        Assert.AreEqual(1, native.CooperativeLaunchCount);
        Assert.AreEqual(0, native.ModuleLaunchCount);
        Assert.AreEqual(new IntPtr(0x3000), native.LastLaunchedFunction);
        Assert.AreEqual(2U, native.LastGridX);
        Assert.AreEqual(2U, native.LastGridY);
        Assert.AreEqual(5U, native.LastGridZ);
        Assert.AreEqual(8U, native.LastBlockX);
        Assert.AreEqual(4U, native.LastBlockY);
        Assert.AreEqual(2U, native.LastBlockZ);
        Assert.AreEqual(2048U, native.LastLaunchSharedMemoryBytes);
        Assert.AreEqual(IntPtr.Zero, native.LastLaunchStream);
        Assert.AreEqual(memory.DangerousGetHandle().ToInt64(), native.LastKernelArgumentValues[0]);
        Assert.AreEqual(17L, native.LastKernelArgumentValues[1]);
        Assert.AreEqual(64, native.LastOccupancyBlockSize);
    }

    [TestMethod]
    public void ExplicitCooperativeLaunchRetainsModuleAndMemoryUntilCompletion()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory memory = runtime.Allocate(4);
        HipKernel kernel = module.GetKernel("kernel");

        kernel.LaunchCooperative(
            stream,
            new HipLaunchDimensions(8),
            new HipLaunchDimensions(64),
            new[] { HipKernelArgument.DevicePointer(memory) });
        memory.Dispose();
        module.Dispose();

        Assert.AreNotEqual(IntPtr.Zero, native.LastLaunchStream);
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(0, native.ModuleUnloadCount);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void CooperativeStreamDisposeDrainsPendingOwners()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        var runtime = new HipRuntime(native);
        HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory memory = runtime.Allocate(4);
        HipKernel kernel = module.GetKernel("kernel");

        kernel.LaunchCooperative(
            stream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(memory) });
        memory.Dispose();
        module.Dispose();
        stream.Dispose();

        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.AreEqual(1, native.StreamDestroyCount);
    }

    [TestMethod]
    public void CooperativeLaunchFailureReleasesReferencesWithoutPendingLease()
    {
        using var native = new FakeHipNativeApi { CooperativeLaunchResult = HipError.InvalidValue };
        native.ExpectedKernelPointerArguments.Add(true);
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory memory = runtime.Allocate(4);
        HipKernel kernel = module.GetKernel("kernel");

        HipException failure = Assert.ThrowsExactly<HipException>(() => kernel.LaunchCooperative(
            stream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(memory) }));
        Assert.AreEqual("hipModuleLaunchCooperativeKernel", failure.Operation);

        memory.Dispose();
        module.Dispose();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void CooperativePendingReleaseFailureCanBeRetried()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory memory = runtime.Allocate(4);
        HipKernel kernel = module.GetKernel("kernel");
        kernel.LaunchCooperative(
            stream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(memory) });
        memory.Dispose();
        module.Dispose();
        native.FreeResult = HipError.OutOfMemory;

        HipException failure = Assert.ThrowsExactly<HipException>(() => stream.Synchronize());
        Assert.AreEqual("hipFree", failure.Operation);
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(0, native.ModuleUnloadCount);

        native.FreeResult = HipError.Success;
        stream.Synchronize();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void CooperativeLaunchFailsClosedForCapabilityCapacityAndMissingExports()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        native.CooperativeLaunchCapability = 0;
        Assert.ThrowsExactly<InvalidOperationException>(() => LaunchEmpty(kernel, 1, 1));
        Assert.AreEqual(0, native.CooperativeLaunchCount);

        native.CooperativeLaunchCapability = 1;
        native.ActiveBlocksPerMultiprocessor = 1;
        native.MultiprocessorCountValue = 2;
        Assert.ThrowsExactly<InvalidOperationException>(() => LaunchEmpty(kernel, 3, 1));
        Assert.AreEqual(0, native.CooperativeLaunchCount);

        native.OccupancyResult = HipError.NotSupported;
        HipException missing = Assert.ThrowsExactly<HipException>(() => LaunchEmpty(kernel, 1, 1));
        Assert.AreEqual(HipError.NotSupported, missing.Error);
        Assert.AreEqual(0, native.CooperativeLaunchCount);
        Assert.AreEqual(0, native.ModuleLaunchCount);

        native.OccupancyResult = HipError.Success;
        native.CooperativeLaunchResult = HipError.NotSupported;
        HipException missingLaunch = Assert.ThrowsExactly<HipException>(() => LaunchEmpty(kernel, 1, 1));
        Assert.AreEqual(HipError.NotSupported, missingLaunch.Error);
        Assert.AreEqual(1, native.CooperativeLaunchCount);
        Assert.AreEqual(0, native.ModuleLaunchCount);
    }

    [TestMethod]
    public void CooperativeLaunchRejectsInvalidOwnersStreamsAndBlockOverflow()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        using HipStream allocationStream = runtime.CreateStream();
        using HipStream otherStream = runtime.CreateStream();
        using HipAsyncDeviceMemory asyncMemory = runtime.AllocateAsync(4, allocationStream);
        var asyncArgument = new[] { HipKernelArgument.DevicePointer(asyncMemory) };

        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            otherStream, new HipLaunchDimensions(1), new HipLaunchDimensions(1), asyncArgument));
        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1), new HipLaunchDimensions(1), asyncArgument));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(uint.MaxValue, 2),
            Array.Empty<HipKernelArgument>()));

        using HipGraph graph = runtime.CreateGraph();
        HipGraphMemory graphMemory = graph.AddMemoryAllocation(4, runtime.GetCurrentDevice());
        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(graphMemory) }));
        Assert.ThrowsExactly<ArgumentNullException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new HipKernelArgument[] { null! }));
    }

    [TestMethod]
    public void CooperativeLaunchRejectsCrossDeviceStreamAndMemory()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        runtime.GetDevice(1).MakeCurrent();
        using HipStream wrongDeviceStream = runtime.CreateStream();
        using HipDeviceMemory wrongDeviceMemory = runtime.Allocate(4);
        runtime.GetDevice(0).MakeCurrent();

        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            wrongDeviceStream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            Array.Empty<HipKernelArgument>()));
        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(wrongDeviceMemory) }));
        Assert.AreEqual(0, native.CooperativeLaunchCount);
    }

    [TestMethod]
    public void CooperativeLaunchRejectsCrossRuntimeAndDisposedMemory()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        var firstRuntime = new HipRuntime(firstNative);
        var secondRuntime = new HipRuntime(secondNative);
        using HipModule module = firstRuntime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        using HipStream foreignStream = secondRuntime.CreateStream();
        using HipDeviceMemory foreignMemory = secondRuntime.Allocate(4);
        HipDeviceMemory disposedMemory = firstRuntime.Allocate(4);
        disposedMemory.Dispose();

        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            foreignStream,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            Array.Empty<HipKernelArgument>()));
        Assert.ThrowsExactly<ArgumentException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(foreignMemory) }));
        Assert.ThrowsExactly<ObjectDisposedException>(() => kernel.LaunchCooperative(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(disposedMemory) }));
        Assert.AreEqual(0, firstNative.CooperativeLaunchCount);
    }

    private static void LaunchEmpty(HipKernel kernel, uint gridX, uint blockX) =>
        kernel.LaunchCooperative(
            new HipLaunchDimensions(gridX),
            new HipLaunchDimensions(blockX),
            Array.Empty<HipKernelArgument>());
}
