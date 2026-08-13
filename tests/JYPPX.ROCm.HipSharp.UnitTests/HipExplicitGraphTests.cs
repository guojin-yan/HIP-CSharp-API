using System;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipExplicitGraphTests
{
    private static readonly int[] ExpectedGraphNodeTypes = { 0, 1, 2, 5, 10, 11 };
    private static readonly int[] ExpectedGraphUpdateResults = { 0, 1, 2, 3, 4, 5, 6, 7 };

    [TestMethod]
    public void GraphNativeLayoutsAndEnumsMatchPinnedX64Contract()
    {
        Assert.AreEqual(64, Marshal.SizeOf<HipKernelNodeParameters>());
        Assert.AreEqual(0, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.BlockDimensions)));
        Assert.AreEqual(16, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.Extra)));
        Assert.AreEqual(24, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.Function)));
        Assert.AreEqual(32, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.GridDimensions)));
        Assert.AreEqual(48, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.KernelParameters)));
        Assert.AreEqual(56, Offset<HipKernelNodeParameters>(nameof(HipKernelNodeParameters.SharedMemoryBytes)));

        Assert.AreEqual(48, Marshal.SizeOf<HipMemsetNodeParameters>());
        Assert.AreEqual(0, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.Destination)));
        Assert.AreEqual(8, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.ElementSize)));
        Assert.AreEqual(16, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.Height)));
        Assert.AreEqual(24, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.Pitch)));
        Assert.AreEqual(32, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.Value)));
        Assert.AreEqual(40, Offset<HipMemsetNodeParameters>(nameof(HipMemsetNodeParameters.Width)));

        Assert.AreEqual(120, Marshal.SizeOf<HipMemoryAllocationNodeParameters>());
        Assert.AreEqual(0, Offset<HipMemoryAllocationNodeParameters>(nameof(HipMemoryAllocationNodeParameters.PoolProperties)));
        Assert.AreEqual(88, Offset<HipMemoryAllocationNodeParameters>(nameof(HipMemoryAllocationNodeParameters.AccessDescriptors)));
        Assert.AreEqual(96, Offset<HipMemoryAllocationNodeParameters>(nameof(HipMemoryAllocationNodeParameters.AccessDescriptorCount)));
        Assert.AreEqual(104, Offset<HipMemoryAllocationNodeParameters>(nameof(HipMemoryAllocationNodeParameters.ByteCount)));
        Assert.AreEqual(112, Offset<HipMemoryAllocationNodeParameters>(nameof(HipMemoryAllocationNodeParameters.DevicePointer)));

        CollectionAssert.AreEqual(
            ExpectedGraphNodeTypes,
            Enum.GetValues<HipGraphNodeType>().Select(value => (int)value).ToArray());
        CollectionAssert.AreEqual(
            ExpectedGraphUpdateResults,
            Enum.GetValues<HipGraphExecUpdateResultNative>().Select(value => (int)value).ToArray());
    }

    [TestMethod]
    public void CreateGraphHandlesFlagsNullAndPartialFailure()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.CreateGraph(1));
        Assert.AreEqual(0, native.GraphCreateCount);

        native.GraphCreateResult = HipError.OutOfMemory;
        native.ReturnGraphOnCreateFailure = true;
        HipException failure = Assert.ThrowsExactly<HipException>(() => runtime.CreateGraph());
        Assert.AreEqual("hipGraphCreate", failure.Operation);
        Assert.AreEqual(1, native.GraphDestroyCount);

        native.GraphCreateResult = HipError.Success;
        native.ReturnGraphOnCreateFailure = false;
        native.ReturnNullGraphOnCreateSuccess = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.CreateGraph());

        native.ReturnNullGraphOnCreateSuccess = false;
        native.GraphCreateResult = HipError.NotSupported;
        HipException unavailable = Assert.ThrowsExactly<HipException>(() => runtime.CreateGraph());
        Assert.AreEqual(HipError.NotSupported, unavailable.Error);
        Assert.AreEqual("hipGraphCreate", unavailable.Operation);
    }

    [TestMethod]
    public void ExplicitGraphDestroyFailureIsLogicalDisposeAndAllowsRetry()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipGraph graph = runtime.CreateGraph();
        HipGraphNode node = graph.AddEmpty();
        native.GraphDestroyResult = HipError.InvalidValue;

        HipException failure = Assert.ThrowsExactly<HipException>(() => graph.Dispose());
        Assert.AreEqual("hipGraphDestroy", failure.Operation);
        Assert.IsTrue(graph.IsDisposed);
        Assert.IsFalse(node.IsValid);
        Assert.ThrowsExactly<ObjectDisposedException>(() => graph.AddEmpty());
        Assert.AreEqual(0, native.GraphDestroyCount);

        native.GraphDestroyResult = HipError.Success;
        graph.Dispose();
        Assert.AreEqual(1, native.GraphDestroyCount);
    }

    [TestMethod]
    public void ExplicitGraphResourceCleanupFailureRetainsAllLeasesForRetry()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipDeviceMemory source = runtime.Allocate(4);
        HipDeviceMemory destination = runtime.Allocate(4);
        HipGraph graph = runtime.CreateGraph();
        graph.AddCopy(source, destination, 4);
        source.Dispose();
        destination.Dispose();
        native.FreeResult = HipError.InvalidValue;

        HipException failure = Assert.ThrowsExactly<HipException>(() => graph.Dispose());
        Assert.AreEqual("hipFree", failure.Operation);
        Assert.AreEqual(1, native.GraphDestroyCount);
        Assert.AreEqual(0, native.FreeCount);

        native.FreeResult = HipError.Success;
        graph.Dispose();
        Assert.AreEqual(1, native.GraphDestroyCount);
        Assert.AreEqual(2, native.FreeCount);
    }

    [TestMethod]
    public void ExplicitTopologyIsTypedAndManagedPrechecksDoNotMutateNativeState()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipGraph first = runtime.CreateGraph();
        using HipGraph second = runtime.CreateGraph();
        HipGraphNode root = first.AddEmpty();
        HipGraphNode child = first.AddEmpty(new[] { root });
        HipGraphNode foreign = second.AddEmpty();

        Assert.AreEqual(HipGraphKind.Explicit, first.Kind);
        CollectionAssert.AreEqual(new[] { root, child }, first.Nodes.ToArray());
        CollectionAssert.AreEqual(new[] { root }, first.RootNodes.ToArray());
        Assert.AreEqual(1, first.Edges.Count);
        Assert.AreSame(root, first.Edges[0].Prerequisite);
        Assert.AreSame(child, first.Edges[0].Dependent);
        CollectionAssert.AreEqual(new[] { root }, child.Dependencies.ToArray());
        Assert.AreEqual(2, native.MaximumGraphNodeCount);
        Assert.AreEqual(1, native.MaximumGraphEdgeCount);

        Assert.ThrowsExactly<ArgumentException>(() => first.AddEmpty(new[] { root, root }));
        Assert.ThrowsExactly<ArgumentException>(() => first.AddEmpty(new[] { foreign }));
        Assert.ThrowsExactly<ArgumentException>(() => first.AddDependency(child, root));
        Assert.ThrowsExactly<ArgumentException>(() => first.AddDependency(root, root));
        Assert.ThrowsExactly<ArgumentException>(() => first.AddDependency(root, child));
        Assert.AreEqual(2, native.MaximumGraphNodeCount);
        Assert.AreEqual(1, native.MaximumGraphEdgeCount);

        first.RemoveDependency(root, child);
        Assert.AreEqual(0, first.Edges.Count);
        Assert.AreEqual(0, child.Dependencies.Count);
        first.AddDependency(root, child);
        Assert.AreEqual(1, first.Edges.Count);
        Assert.AreEqual(1, native.MaximumGraphEdgeCount);
        Assert.AreEqual(0, default(HipGraphEdge).GetHashCode());
    }

    [TestMethod]
    public void CapturedGraphRejectsBuilderAndManagedIntrospection()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        using HipGraph captured = runtime.CaptureGraph(stream, _ => { });

        Assert.AreEqual(HipGraphKind.Captured, captured.Kind);
        Assert.ThrowsExactly<InvalidOperationException>(() => captured.AddEmpty());
        Assert.ThrowsExactly<InvalidOperationException>(() => _ = captured.Nodes);
        using HipGraphExec executable = captured.Instantiate();
        Assert.ThrowsExactly<ArgumentNullException>(() => executable.UpdateMemset(null!, (HipDeviceMemory)null!, 0));
    }

    [TestMethod]
    public void FailedNodeAddCleansPartialNodeAndDoesNotPublishIdentity()
    {
        using var native = new FakeHipNativeApi
        {
            GraphNodeAddResult = HipError.OutOfMemory,
            ReturnNodeOnAddFailure = true,
        };
        var runtime = new HipRuntime(native);
        using HipGraph graph = runtime.CreateGraph();

        HipException failure = Assert.ThrowsExactly<HipException>(() => graph.AddEmpty());

        Assert.AreEqual("hipGraphAddEmptyNode", failure.Operation);
        Assert.AreEqual(0, graph.Nodes.Count);
        Assert.AreEqual(1, native.GraphNodeDestroyCount);

        native.GraphNodeAddResult = HipError.Success;
        native.ReturnNodeOnAddFailure = false;
        native.ReturnNullNodeOnAddSuccess = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.AddEmpty());
        Assert.AreEqual(0, graph.Nodes.Count);
    }

    [TestMethod]
    public void TypedNodesCaptureKernelCopyAndMemsetParametersAndRetainOwners()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        native.ExpectedKernelPointerArguments.Add(false);
        var runtime = new HipRuntime(native);
        HipDeviceMemory source = runtime.Allocate(8);
        HipDeviceMemory destination = runtime.Allocate(8);
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        using HipGraph graph = runtime.CreateGraph();

        HipGraphNode clear = graph.AddMemset(destination, 0x5A, 8);
        HipGraphNode copy = graph.AddCopy(source, destination, 8, new[] { clear });
        HipGraphNode launch = graph.AddKernel(
            kernel,
            new HipLaunchDimensions(3, 2),
            new HipLaunchDimensions(8),
            new[] { HipKernelArgument.DevicePointer(destination), HipKernelArgument.Scalar32(17) },
            new[] { copy },
            32);

        Assert.AreEqual(HipGraphNodeType.MemorySet, clear.Type);
        Assert.AreEqual(HipGraphNodeType.MemoryCopy, copy.Type);
        Assert.AreEqual(HipGraphNodeType.Kernel, launch.Type);
        Assert.AreEqual(8UL, native.LastGraphCopyBytes);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToDevice, native.LastGraphCopyKind);
        Assert.AreEqual(1U, native.LastGraphMemsetParameters.ElementSize);
        Assert.AreEqual(1UL, native.LastGraphMemsetParameters.Height.ToUInt64());
        Assert.AreEqual(8UL, native.LastGraphMemsetParameters.Width.ToUInt64());
        Assert.AreEqual(0x5AU, native.LastGraphMemsetParameters.Value);
        Assert.AreEqual(3U, native.LastGraphKernelParameters.GridDimensions.X);
        Assert.AreEqual(2U, native.LastGraphKernelParameters.GridDimensions.Y);
        Assert.AreEqual(8U, native.LastGraphKernelParameters.BlockDimensions.X);
        Assert.AreEqual(32U, native.LastGraphKernelParameters.SharedMemoryBytes);
        Assert.AreEqual(destination.DangerousGetHandle().ToInt64(), native.LastGraphKernelArgumentValues[0]);
        Assert.AreEqual(17L, native.LastGraphKernelArgumentValues[1]);

        using HipGraphExec executable = graph.Instantiate();
        source.Dispose();
        destination.Dispose();
        module.Dispose();
        graph.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(0, native.ModuleUnloadCount);
        executable.Dispose();
        Assert.AreEqual(2, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void GraphLocalAllocationConsumerFreeExecutesOnEveryLaunchWithoutLeakingPointer()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipDeviceMemory input = runtime.Allocate(8);
        input.CopyFrom(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        using HipGraph graph = runtime.CreateGraph();
        HipGraphMemory local = graph.AddMemoryAllocation(8, runtime.GetCurrentDevice());

        Assert.AreEqual(8UL, local.ByteLength);
        Assert.AreEqual(1, native.LastGraphAllocationAccessCount);
        Assert.AreEqual(0, native.LastGraphAllocationDevice);
        Assert.IsFalse(typeof(HipGraphMemory).GetMethods().Any(method => method.Name == "DangerousGetHandle"));
        Assert.ThrowsExactly<ArgumentException>(() => graph.AddMemset(local, 7));

        HipGraphNode clear = graph.AddMemset(local, 7, dependencies: new[] { local.AllocationNode });
        HipGraphNode copy = graph.AddCopy(input, local, 8, new[] { clear });
        HipGraphNode free = graph.AddMemoryFree(local, new[] { copy });
        Assert.AreEqual(HipGraphNodeType.MemoryFree, free.Type);
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.AddMemset(local, 0, dependencies: new[] { copy }));
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.AddMemoryFree(local));

        using HipGraphExec executable = graph.Instantiate();
        using var stream = runtime.CreateStream();
        executable.Upload(stream);
        executable.Launch(stream);
        Assert.AreEqual(0, native.GraphAllocationExecutionCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.GraphAllocationExecutionCount);
        Assert.AreEqual(1, native.GraphFreeExecutionCount);
        Assert.AreEqual(0, native.ActiveGraphAllocationCount);
        CollectionAssert.AreEqual(
            new[] { HipGraphNodeType.MemoryAllocation, HipGraphNodeType.MemorySet, HipGraphNodeType.MemoryCopy, HipGraphNodeType.MemoryFree },
            native.LastGraphExecutionTrace.Select(value => Enum.Parse<HipGraphNodeType>(value.Split(':')[0])).ToArray());

        executable.Launch(stream);
        stream.Synchronize();
        Assert.AreEqual(2, native.GraphAllocationExecutionCount);
        Assert.AreEqual(2, native.GraphFreeExecutionCount);
        Assert.AreEqual(0, native.ActiveGraphAllocationCount);
        Assert.AreEqual(1, native.GraphUploadCount);
    }

    [TestMethod]
    public void GraphLocalAllocationMustBeFreedAndOrderingCannotBeBroken()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipGraph graph = runtime.CreateGraph();
        HipGraphMemory local = graph.AddMemoryAllocation(4, runtime.GetCurrentDevice());
        HipGraphNode consumer = graph.AddMemset(local, 0, dependencies: new[] { local.AllocationNode });

        Assert.ThrowsExactly<InvalidOperationException>(() => graph.Instantiate());
        HipGraphNode free = graph.AddMemoryFree(local, new[] { consumer });
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.RemoveDependency(local.AllocationNode, consumer));
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.RemoveDependency(consumer, free));

        using HipGraphExec executable = graph.Instantiate();
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.AddEmpty());
        Assert.ThrowsExactly<InvalidOperationException>(() => graph.AddDependency(local.AllocationNode, free));
    }

    [TestMethod]
    public void GraphLocalFreeWithoutConsumersMustRemainAfterAllocation()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipGraph graph = runtime.CreateGraph();
        HipGraphMemory local = graph.AddMemoryAllocation(4, runtime.GetCurrentDevice());
        HipGraphNode free = graph.AddMemoryFree(local);

        Assert.ThrowsExactly<InvalidOperationException>(() => graph.RemoveDependency(local.AllocationNode, free));
        Assert.AreEqual(1, graph.Edges.Count);
        CollectionAssert.AreEqual(new[] { local.AllocationNode }, free.Dependencies.ToArray());
    }

    [TestMethod]
    public void UploadLaunchUpdateAndFailureRollbackHaveStableState()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipDeviceMemory source = runtime.Allocate(8);
        HipDeviceMemory firstDestination = runtime.Allocate(8);
        HipDeviceMemory secondDestination = runtime.Allocate(8);
        using HipGraph graph = runtime.CreateGraph();
        HipGraphNode copy = graph.AddCopy(source, firstDestination, 4);
        using HipGraphExec executable = graph.Instantiate();
        using var stream = runtime.CreateStream();

        executable.UpdateCopy(copy, source, secondDestination, 8);
        Assert.AreEqual(1, native.GraphNodeUpdateCount);
        native.GraphNodeUpdateResult = HipError.InvalidValue;
        HipException failed = Assert.ThrowsExactly<HipException>(() => executable.UpdateCopy(copy, source, firstDestination, 4));
        Assert.AreEqual("hipGraphExecMemcpyNodeSetParams1D", failed.Operation);
        Assert.AreEqual(1, native.GraphNodeUpdateCount);

        firstDestination.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        secondDestination.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        native.GraphNodeUpdateResult = HipError.Success;
        executable.Launch(stream);
        Assert.ThrowsExactly<InvalidOperationException>(() => executable.Upload(stream));
        Assert.ThrowsExactly<InvalidOperationException>(() => executable.UpdateCopy(copy, source, firstDestination, 4));
        stream.Synchronize();
        executable.Upload(stream);
        executable.Dispose();
        Assert.AreEqual(1, native.FreeCount);
        graph.Dispose();
        Assert.AreEqual(2, native.FreeCount);
    }

    [TestMethod]
    public void KernelAndMemsetExecutableUpdatesCommitTypedParameters()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        var runtime = new HipRuntime(native);
        using HipDeviceMemory memory = runtime.Allocate(16);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        using HipGraph graph = runtime.CreateGraph();
        HipGraphNode memset = graph.AddMemset(memory, 1, 8);
        HipGraphNode launch = graph.AddKernel(
            kernel,
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(4),
            new[] { HipKernelArgument.DevicePointer(memory) },
            new[] { memset });
        using HipGraphExec executable = graph.Instantiate();

        executable.UpdateMemset(memset, memory, 0xAB, 16);
        executable.UpdateKernel(
            launch,
            kernel,
            new HipLaunchDimensions(2, 3),
            new HipLaunchDimensions(8),
            new[] { HipKernelArgument.DevicePointer(memory) },
            sharedMemoryBytes: 64);

        Assert.AreEqual(2, native.GraphNodeUpdateCount);
        Assert.AreEqual(0xABU, native.LastGraphMemsetParameters.Value);
        Assert.AreEqual(16UL, native.LastGraphMemsetParameters.Width.ToUInt64());
        Assert.AreEqual(2U, native.LastGraphKernelParameters.GridDimensions.X);
        Assert.AreEqual(3U, native.LastGraphKernelParameters.GridDimensions.Y);
        Assert.AreEqual(8U, native.LastGraphKernelParameters.BlockDimensions.X);
        Assert.AreEqual(64U, native.LastGraphKernelParameters.SharedMemoryBytes);
    }

    [TestMethod]
    public void ExplicitGraphAndExecutableValidateRuntimeDeviceAndNodeType()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        var firstRuntime = new HipRuntime(firstNative);
        var secondRuntime = new HipRuntime(secondNative);
        using HipDeviceMemory firstMemory = firstRuntime.Allocate(4);
        using HipDeviceMemory foreignMemory = secondRuntime.Allocate(4);
        using HipGraph graph = firstRuntime.CreateGraph();

        Assert.ThrowsExactly<ArgumentException>(() => graph.AddMemset(foreignMemory, 0));
        HipGraphNode empty = graph.AddEmpty();
        HipGraphNode memset = graph.AddMemset(firstMemory, 0, dependencies: new[] { empty });
        using HipGraphExec executable = graph.Instantiate();
        using var foreignStream = secondRuntime.CreateStream();
        Assert.ThrowsExactly<ArgumentException>(() => executable.Upload(foreignStream));
        Assert.ThrowsExactly<ArgumentException>(() => executable.UpdateMemset(empty, firstMemory, 0));
        Assert.ThrowsExactly<ArgumentException>(() => executable.UpdateCopy(memset, firstMemory, firstMemory, 4));
    }

    private static int Offset<T>(string field) where T : struct => Marshal.OffsetOf<T>(field).ToInt32();
}
