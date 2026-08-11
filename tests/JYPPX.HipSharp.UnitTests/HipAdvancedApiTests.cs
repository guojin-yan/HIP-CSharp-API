using System;
using System.Runtime.CompilerServices;
using JYPPX.HipSharp.Graphs;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Peer;
using JYPPX.HipSharp.Streams;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipAdvancedApiTests
{
    [TestMethod]
    public void AsyncAllocationIsFreedOnAllocationStreamAndBlocksPrematureStreamDispose()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipAsyncDeviceMemory memory = runtime.AllocateAsync(8, stream);
        memory.CopyFromAsync(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });

        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Dispose());
        memory.Dispose();
        Assert.AreEqual(0, native.AsyncFreeCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.AsyncFreeCount);
        Assert.IsTrue(memory.IsDisposed);
    }

    [TestMethod]
    public void AdvancedAllocationAndPeerInputsFailBeforeNativeMutation()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.AllocateAsync(0, stream));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.AllocateManaged(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.AllocateManaged(4, (HipManagedMemoryFlags)99));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.CanAccessPeer(-1, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.EnablePeerAccess(0, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => stream.BeginCapture((HipStreamCaptureMode)99));

        runtime.GetDevice(1).MakeCurrent();
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.EnablePeerAccess(0, 1));
        Assert.AreEqual(0, native.AsyncAllocationCount);
        Assert.AreEqual(0, native.ManagedAllocationCount);
        Assert.AreEqual(0, native.PeerEnableCount);
    }

    [TestMethod]
    public void AsyncAllocationFailureReleasesStreamOwnerAndRepeatedDisposeQueuesOneFree()
    {
        using var native = new FakeHipNativeApi { MallocAsyncResult = HipError.OutOfMemory };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();

        Assert.ThrowsExactly<HipException>(() => runtime.AllocateAsync(4, stream));
        native.MallocAsyncResult = HipError.Success;
        HipAsyncDeviceMemory memory = runtime.AllocateAsync(4, stream);
        memory.Dispose();
        memory.Dispose();
        Assert.AreEqual(1, native.FreeAsyncCallCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.AsyncFreeCount);
    }

    [TestMethod]
    public void AsyncAllocationKernelArgumentRejectsDefaultAndDifferentStreams()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        using HipStream allocationStream = runtime.CreateStream();
        using HipStream otherStream = runtime.CreateStream();
        using HipAsyncDeviceMemory memory = runtime.AllocateAsync(4, allocationStream);
        var arguments = new[] { HipKernelArgument.DevicePointer(memory) };

        Assert.ThrowsExactly<ArgumentException>(() => kernel.Launch(
            new HipLaunchDimensions(1), new HipLaunchDimensions(1), arguments));
        Assert.ThrowsExactly<ArgumentException>(() => kernel.Launch(
            otherStream, new HipLaunchDimensions(1), new HipLaunchDimensions(1), arguments));

        kernel.Launch(allocationStream, new HipLaunchDimensions(1), new HipLaunchDimensions(1), arguments);
        allocationStream.Synchronize();
        Assert.AreEqual(1, native.ModuleLaunchCount);
    }

    [TestMethod]
    public void ManagedMemorySupportsHostRoundTripAndStreamOrderedHints()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipManagedMemory memory = runtime.AllocateManaged(4, HipManagedMemoryFlags.Global);
        byte[] source = { 9, 8, 7, 6 };
        byte[] destination = new byte[4];

        memory.CopyFromHost(source);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => memory.Advise((HipMemoryAdvise)99, 0));
        memory.Advise(HipMemoryAdvise.SetReadMostly, 0);
        memory.PrefetchAsync(0, stream);
        stream.Synchronize();
        memory.CopyToHost(destination);

        CollectionAssert.AreEqual(source, destination);
        Assert.AreEqual(1, native.MemAdviseCount);
        Assert.AreEqual(1, native.MemPrefetchCount);
        Assert.AreEqual(1, native.ManagedAllocationCount);
    }

    [TestMethod]
    public void ManagedDisposeDefersCheckedFreeAndSynchronizationCanRetryFailure()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipManagedMemory memory = runtime.AllocateManaged(4);
        memory.PrefetchAsync(0, stream);
        memory.Dispose();
        native.FreeResult = HipError.OutOfMemory;

        HipException exception = Assert.ThrowsExactly<HipException>(() => stream.Synchronize());
        Assert.AreEqual("hipFree(managed)", exception.Operation);
        Assert.AreEqual(0, native.FreeCount);

        native.FreeResult = HipError.Success;
        stream.Synchronize();
        Assert.AreEqual(1, native.FreeCount);
    }

    [TestMethod]
    public void ManagedMemoryPropagatesUnsupportedNativeAllocation()
    {
        using var native = new FakeHipNativeApi { ManagedMallocResult = HipError.NotSupported };
        var runtime = new HipRuntime(native);

        HipException exception = Assert.ThrowsExactly<HipException>(() => runtime.AllocateManaged(4));

        Assert.AreEqual(HipError.NotSupported, exception.Error);
        Assert.AreEqual("hipMallocManaged", exception.Operation);
    }

    [TestMethod]
    public void PartialConstructionFailuresReleaseReturnedNativeResources()
    {
        using var native = new FakeHipNativeApi
        {
            ManagedMallocResult = HipError.OutOfMemory,
            ReturnManagedPointerOnFailure = true,
            MallocAsyncResult = HipError.OutOfMemory,
            ReturnAsyncPointerOnFailure = true,
        };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();

        Assert.ThrowsExactly<HipException>(() => runtime.AllocateManaged(4));
        Assert.ThrowsExactly<HipException>(() => runtime.AllocateAsync(4, stream));
        stream.Synchronize();
        Assert.AreEqual(2, native.FreeCount);
        Assert.AreEqual(1, native.FreeAsyncCallCount);

        native.EndCaptureResult = HipError.StreamCaptureInvalidated;
        native.ReturnGraphOnEndCaptureFailure = true;
        stream.BeginCapture();
        Assert.ThrowsExactly<HipException>(() => stream.EndCapture());
        Assert.AreEqual(1, native.GraphDestroyCount);

        native.EndCaptureResult = HipError.Success;
        using HipGraph graph = runtime.CaptureGraph(stream, _ => { });
        native.GraphInstantiateResult = HipError.InvalidValue;
        native.ReturnGraphExecOnInstantiateFailure = true;
        Assert.ThrowsExactly<HipException>(() => graph.Instantiate());
        Assert.AreEqual(1, native.GraphExecDestroyCount);
    }

    [TestMethod]
    public void FailedCheckedDisposeClosesManagedAndGraphOwnersLogicallyAndAllowsRetry()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipManagedMemory memory = runtime.AllocateManaged(4);
        native.FreeResult = HipError.OutOfMemory;

        Assert.ThrowsExactly<HipException>(() => memory.Dispose());
        Assert.IsTrue(memory.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.Advise(HipMemoryAdvise.SetReadMostly, 0));
        native.FreeResult = HipError.Success;
        memory.Dispose();

        using HipStream stream = runtime.CreateStream();
        using HipGraph graph = runtime.CaptureGraph(stream, _ => { });
        HipGraphExec executable = graph.Instantiate();
        native.GraphExecDestroyResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => executable.Dispose());
        Assert.IsTrue(executable.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => executable.Launch(stream));
        native.GraphExecDestroyResult = HipError.Success;
        executable.Dispose();
    }

    [TestMethod]
    public void PeerOwnerOnlyDisablesAccessItEnabled()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);
        Assert.IsTrue(peer.IsSupported);
        Assert.IsTrue(peer.IsEnabled);
        Assert.IsFalse(peer.WasAlreadyEnabled);
        Assert.AreEqual(1, native.PeerEnableCount);

        peer.Dispose();
        peer.Dispose();
        Assert.AreEqual(1, native.PeerDisableCount);
    }

    [TestMethod]
    public void PeerAlreadyEnabledIsNotRevokedByNewOwner()
    {
        using var native = new FakeHipNativeApi { PeerEnableResult = HipError.PeerAccessAlreadyEnabled };
        var runtime = new HipRuntime(native);
        using HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);

        Assert.IsTrue(peer.IsEnabled);
        Assert.IsTrue(peer.WasAlreadyEnabled);
        peer.Dispose();
        Assert.AreEqual(0, native.PeerDisableCount);
    }

    [TestMethod]
    public void PeerOperationsRequireTheAccessingDeviceToRemainCurrent()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipDeviceMemory source = runtime.Allocate(1);
        using HipDeviceMemory destination = runtime.Allocate(1);
        HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);
        runtime.GetDevice(1).MakeCurrent();

        Assert.ThrowsExactly<InvalidOperationException>(() => peer.CopyAsync(destination, 0, source, 1, 1, stream));
        Assert.ThrowsExactly<InvalidOperationException>(() => peer.Dispose());
        Assert.AreEqual(0, native.PeerCopyCount);
        Assert.AreEqual(0, native.PeerDisableCount);

        runtime.GetDevice(0).MakeCurrent();
        peer.Dispose();
        Assert.AreEqual(1, native.PeerDisableCount);
    }

    [TestMethod]
    public void PeerCopyRejectsDeviceOrdinalsOutsideTheOwnedPair()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipDeviceMemory source = runtime.Allocate(1);
        using HipDeviceMemory destination = runtime.Allocate(1);
        using HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);

        Assert.ThrowsExactly<ArgumentException>(() => peer.CopyAsync(destination, 0, source, 0, 1, stream));
        Assert.AreEqual(0, native.PeerCopyCount);
    }

    [TestMethod]
    public void PeerCopyCompletesOnTheOwnedPairAndPreservesData()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipDeviceMemory source = runtime.Allocate(4);
        using HipDeviceMemory destination = runtime.Allocate(4);
        using HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);
        byte[] expected = { 1, 3, 5, 7 };
        source.CopyFrom(expected);

        peer.CopyAsync(destination, 0, source, 1, 4, stream);
        stream.Synchronize();
        byte[] actual = new byte[4];
        destination.CopyTo(actual);

        CollectionAssert.AreEqual(expected, actual);
        Assert.AreEqual(1, native.PeerCopyCount);
    }

    [TestMethod]
    public void UnsupportedPeerPairIsAnExplicitNoOpOwner()
    {
        using var native = new FakeHipNativeApi { PeerCapability = false };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipDeviceMemory source = runtime.Allocate(1);
        using HipDeviceMemory destination = runtime.Allocate(1);
        using HipPeerAccess peer = runtime.EnablePeerAccess(0, 1);

        Assert.IsFalse(peer.IsSupported);
        Assert.IsFalse(peer.IsEnabled);
        Assert.ThrowsExactly<InvalidOperationException>(() => peer.CopyAsync(destination, 1, source, 0, 1, stream));
    }

    [TestMethod]
    public void CaptureProducesIndependentGraphAndExecutableOwners()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipGraph graph = runtime.CaptureGraph(stream, _ => { });
        using HipGraphExec executable = graph.Instantiate();

        executable.Launch(stream);
        executable.Dispose();
        Assert.AreEqual(0, native.GraphExecDestroyCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.GraphLaunchCount);
        Assert.AreEqual(1, native.GraphExecDestroyCount);
        Assert.IsTrue(graph.IsDisposed == false);
    }

    [TestMethod]
    public void GraphExecutableDeferredDestroyFailureIsReportedAndRetriedBySynchronization()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipGraph graph = runtime.CaptureGraph(stream, _ => { });
        HipGraphExec executable = graph.Instantiate();
        executable.Launch(stream);
        executable.Dispose();
        native.GraphExecDestroyResult = HipError.InvalidValue;

        HipException exception = Assert.ThrowsExactly<HipException>(() => stream.Synchronize());
        Assert.AreEqual("hipGraphExecDestroy", exception.Operation);
        Assert.AreEqual(0, native.GraphExecDestroyCount);

        native.GraphExecDestroyResult = HipError.Success;
        stream.Synchronize();
        Assert.AreEqual(1, native.GraphExecDestroyCount);
    }

    [TestMethod]
    public void CapturingStreamMustEndCaptureBeforeDispose()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        stream.BeginCapture();

        Assert.ThrowsExactly<InvalidOperationException>(() => stream.Dispose());
        using HipGraph graph = stream.EndCapture();
        stream.Dispose();
        Assert.AreEqual(1, native.StreamDestroyCount);
    }

    [TestMethod]
    public void CaptureAndGraphFailuresPreserveOwningHandlesForRetryOrDispose()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        stream.BeginCapture();
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.BeginCapture());
        using HipGraph graph = stream.EndCapture();
        Assert.ThrowsExactly<InvalidOperationException>(() => stream.EndCapture());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => graph.Instantiate(1));

        native.GraphInstantiateResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => graph.Instantiate());
        native.GraphInstantiateResult = HipError.Success;
        using HipGraphExec executable = graph.Instantiate();
        native.GraphLaunchResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => executable.Launch(stream));
        native.GraphLaunchResult = HipError.Success;
        executable.Launch(stream);
        stream.Synchronize();

        Assert.AreEqual(1, native.GraphLaunchCount);
    }

    [TestMethod]
    public void SafeHandleFinalizersReleaseAbandonedAdvancedOwners()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        AbandonAdvancedOwners(runtime);

        for (int attempt = 0; attempt < 3; attempt++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }

        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.FreeAsyncCallCount);
        Assert.AreEqual(1, native.GraphDestroyCount);
        Assert.AreEqual(1, native.GraphExecDestroyCount);
    }

    [TestMethod]
    public void CaptureFailureLeavesStreamOutOfCapture()
    {
        using var native = new FakeHipNativeApi { EndCaptureResult = HipError.StreamCaptureInvalidated };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();

        Assert.ThrowsExactly<HipException>(() => runtime.CaptureGraph(stream, _ => { }));
        Assert.IsFalse(stream.IsCapturing);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static void AbandonAdvancedOwners(HipRuntime runtime)
    {
        _ = runtime.AllocateManaged(4);
        HipStream allocationStream = runtime.CreateStream();
        _ = runtime.AllocateAsync(4, allocationStream);
        HipStream captureStream = runtime.CreateStream();
        HipGraph graph = runtime.CaptureGraph(captureStream, _ => { });
        _ = graph.Instantiate();
    }
}
