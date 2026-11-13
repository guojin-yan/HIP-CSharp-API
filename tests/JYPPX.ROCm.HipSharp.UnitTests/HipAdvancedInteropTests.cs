using System;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

#pragma warning disable CA1861

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipAdvancedInteropTests
{
    [TestMethod]
    public void RetainedAdvancedFacadeRejectsDisposedRuntimeBeforeNativeCalls()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipAdvancedInterop advanced = runtime.AdvancedInterop;
        using var descriptor = new NativeDescriptorBuffer();
        runtime.Dispose();
        int calls = native.AdvancedCalls.Count;
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.ImportExternalMemory(descriptor.Descriptor));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.ImportExternalSemaphore(descriptor.Descriptor));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.OpenIpcMemory(default));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.OpenIpcEvent(default));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.CreateUserObject(IntPtr.Zero, descriptor.Descriptor));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.CopyDriver3D(descriptor.Descriptor));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.LaunchDriverKernel(descriptor.Descriptor, new IntPtr(1)));
        Assert.AreEqual(calls, native.AdvancedCalls.Count);
    }

    [TestMethod]
    public void ExternalIpcProfilerAndCallbackAreOwnedByManagedWrappers()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        var advanced = runtime.AdvancedInterop;
        using HipProfilerSession profiler = advanced.StartProfiler();
        using (HipStream stream = runtime.CreateStream())
            advanced.AddStreamCallback(stream, _ => { });
        CollectionAssert.Contains(native.AdvancedCalls, "hipProfilerStart");
        CollectionAssert.Contains(native.AdvancedCalls, "hipStreamAddCallback");
    }

    [TestMethod]
    public void AdvancedGraphAndDriverApisRouteThroughTheSameRuntime()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var graph = runtime.CreateGraph();
        using var descriptor = new NativeDescriptorBuffer();
        HipAdvancedInterop advanced = runtime.AdvancedInterop;

        HipAdvancedGraphNode signal = advanced.AddExternalSemaphoreSignalNode(graph, null, descriptor.Descriptor);
        HipAdvancedGraphNode copy = advanced.AddDriverMemcpyNode(graph, new[] { signal }, descriptor.Descriptor);
        advanced.SetExternalSemaphoreSignalNodeParameters(signal, descriptor.Descriptor);
        advanced.GetExternalSemaphoreSignalNodeParameters(signal, descriptor.Descriptor);
        advanced.GetDriverMemcpyNodeParameters(copy, descriptor.Descriptor);
        advanced.SetDriverMemcpyNodeParameters(copy, descriptor.Descriptor);
        Assert.AreEqual("hipErrorFake", advanced.GetDriverErrorName(HipError.Success));
        Assert.AreEqual("fake driver error", advanced.GetDriverErrorString(HipError.Success));
        CollectionAssert.Contains(native.AdvancedCalls, "hipGraphAddExternalSemaphoresSignalNode");
        CollectionAssert.Contains(native.AdvancedCalls, "hipDrvGraphAddMemcpyNode");
    }

    [TestMethod]
    public void ExternalMemoryAndIpcOwnersReleaseTheirNativeResources()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        HipAdvancedInterop advanced = runtime.AdvancedInterop;

        using (HipExternalMemory memory = advanced.ImportExternalMemory(descriptor.Descriptor))
        using (HipExternalMemoryBuffer mapped = memory.MapBuffer(descriptor.Descriptor))
        using (HipExternalSemaphore semaphore = advanced.ImportExternalSemaphore(descriptor.Descriptor))
        using (HipIpcMemory ipc = advanced.OpenIpcMemory(default))
        {
            Assert.IsFalse(memory.IsDisposed);
            Assert.IsFalse(mapped.IsDisposed);
            Assert.IsFalse(semaphore.IsDisposed);
            Assert.IsFalse(ipc.IsDisposed);
        }

        CollectionAssert.Contains(native.AdvancedCalls, "hipDestroyExternalMemory");
        CollectionAssert.Contains(native.AdvancedCalls, "hipDestroyExternalSemaphore");
        CollectionAssert.Contains(native.AdvancedCalls, "hipIpcCloseMemHandle");
    }

    [TestMethod]
    public void NativeDestroyFailuresRetainOwnershipForRetry()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        var advanced = runtime.AdvancedInterop;
        IDisposable[] owners = { advanced.ImportExternalMemory(descriptor.Descriptor), advanced.ImportExternalSemaphore(descriptor.Descriptor), advanced.OpenIpcMemory(default), advanced.OwnGraphicsResource(new IntPtr(1234)) };
        native.AdvancedResult = HipError.InvalidValue;
        foreach (IDisposable owner in owners) Assert.ThrowsExactly<HipException>(owner.Dispose);
        native.AdvancedResult = HipError.Success;
        foreach (IDisposable owner in owners) { owner.Dispose(); owner.Dispose(); }
        foreach (string operation in new[] { "hipDestroyExternalMemory", "hipDestroyExternalSemaphore", "hipIpcCloseMemHandle", "hipGraphicsUnregisterResource" })
            Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == operation));
    }

    [TestMethod]
    public void MappedBufferRetainsItsParentAndRetriesBothFreeAndParentCleanup()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        HipExternalMemory memory = runtime.AdvancedInterop.ImportExternalMemory(descriptor.Descriptor);
        HipExternalMemoryBuffer buffer = memory.MapBuffer(descriptor.Descriptor);
        memory.Dispose();
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalMemory"));
        native.FreeResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(buffer.Dispose);
        Assert.AreEqual(0, native.FreeCount);
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalMemory"));
        native.FreeResult = HipError.Success;
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(buffer.Dispose);
        Assert.AreEqual(1, native.FreeCount);
        native.AdvancedResult = HipError.Success;
        buffer.Dispose();
        buffer.Dispose();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipDestroyExternalMemory"));
    }

    [TestMethod]
    public void SemaphoreBorrowSurvivesPendingWorkAndFailedCompletionCleanup()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        using HipStream stream = runtime.CreateStream();
        HipExternalSemaphore semaphore = runtime.AdvancedInterop.ImportExternalSemaphore(descriptor.Descriptor);
        runtime.AdvancedInterop.SignalExternalSemaphores(stream, new[] { semaphore }, descriptor.Descriptor);
        runtime.AdvancedInterop.WaitExternalSemaphores(stream, new[] { semaphore }, descriptor.Descriptor);
        semaphore.Dispose();
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalSemaphore"));
        native.StreamQueryResult = HipError.NotReady;
        Assert.IsFalse(stream.Query());
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalSemaphore"));
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(stream.Synchronize);
        native.AdvancedResult = HipError.Success;
        stream.Synchronize();
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
        semaphore.Dispose();
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
    }

    [TestMethod]
    public void SemaphoreSubmissionRejectsForeignOwnersCaptureAndNativeFailureWithoutLeakingBorrows()
    {
        using var native = new FakeHipNativeApi();
        using var foreignNative = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var foreign = new HipRuntime(foreignNative);
        using var descriptor = new NativeDescriptorBuffer();
        using HipStream stream = runtime.CreateStream();
        HipExternalSemaphore local = runtime.AdvancedInterop.ImportExternalSemaphore(descriptor.Descriptor);
        using HipExternalSemaphore other = foreign.AdvancedInterop.ImportExternalSemaphore(descriptor.Descriptor);
        Assert.ThrowsExactly<ArgumentException>(() => runtime.AdvancedInterop.SignalExternalSemaphores(stream, new[] { local, other }, descriptor.Descriptor));
        stream.BeginCapture();
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.AdvancedInterop.SignalExternalSemaphores(stream, new[] { local }, descriptor.Descriptor));
        using (stream.EndCapture()) { }
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.WaitExternalSemaphores(stream, new[] { local }, descriptor.Descriptor));
        native.AdvancedResult = HipError.Success;
        local.Dispose();
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
    }

    [TestMethod]
    public void GraphicsMappingRetainsStreamAndResourcesUntilUnmapCompletes()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipGraphicsResource resource = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(1234));
        HipGraphicsMapping mapping = runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream);
        resource.Dispose();
        Assert.IsFalse(native.AdvancedCalls.Contains("hipGraphicsUnregisterResource"));
        Assert.AreEqual(16UL, mapping.GetMappedPointer(resource).ByteLength);
        Assert.ThrowsExactly<InvalidOperationException>(stream.Dispose);
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(mapping.Dispose);
        Assert.ThrowsExactly<InvalidOperationException>(stream.Dispose);
        native.AdvancedResult = HipError.Success;
        mapping.Dispose();
        mapping.Dispose();
        Assert.IsFalse(native.AdvancedCalls.Contains("hipGraphicsUnregisterResource"));
        stream.Synchronize();
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipGraphicsUnregisterResource"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => mapping.GetMappedPointer(resource));
    }

    [TestMethod]
    public void GraphicsMapFailureReleasesStreamAndResourceBorrows()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipGraphicsResource resource = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(1234));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.AdvancedInterop.MapGraphicsResources(new[] { resource, resource }, stream));
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream));
        native.AdvancedResult = HipError.Success;
        resource.Dispose();
        stream.Dispose();
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipGraphicsUnregisterResource"));
    }

    [TestMethod]
    public void UserObjectDisposalBalancesInitialAndRetainedReferencesAndCanRetry()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        HipUserObject owner = runtime.AdvancedInterop.CreateUserObject(IntPtr.Zero, descriptor.Descriptor, 2);
        owner.Retain(3);
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() => owner.Retain(4));
        Assert.ThrowsExactly<HipException>(owner.Dispose);
        Assert.ThrowsExactly<ObjectDisposedException>(() => owner.Retain());
        native.AdvancedResult = HipError.Success;
        owner.Dispose();
        owner.Dispose();
        CollectionAssert.AreEqual(new uint[] { 5, 5 }, native.UserObjectReleaseCounts);
        using HipUserObject maximum = runtime.AdvancedInterop.CreateUserObject(IntPtr.Zero, descriptor.Descriptor, uint.MaxValue);
        int retainCalls = native.UserObjectRetainCounts.Count;
        Assert.ThrowsExactly<OverflowException>(() => maximum.Retain());
        Assert.AreEqual(retainCalls, native.UserObjectRetainCounts.Count);
    }

    [TestMethod]
    public void ManagedGraphUserObjectRejectsReferenceMoves()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        using HipUserObject owner = runtime.AdvancedInterop.CreateUserObject(IntPtr.Zero, descriptor.Descriptor);
        using HipGraph graph = runtime.CreateGraph();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.AdvancedInterop.RetainUserObject(graph, owner, 1, 1));
        Assert.IsFalse(native.AdvancedCalls.Contains("hipGraphRetainUserObject"));
    }

    [TestMethod]
    public void ProfilerStopFailureCanBeRetried()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        HipProfilerSession session = runtime.AdvancedInterop.StartProfiler();
        native.AdvancedResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(session.Dispose);
        Assert.IsFalse(session.IsDisposed);
        native.AdvancedResult = HipError.Success;
        session.Dispose();
        session.Dispose();
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipProfilerStop"));
    }

    [TestMethod]
    public void AdvancedNodesParticipateInTopologyAndRespectSealingAndPendingLaunches()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new NativeDescriptorBuffer();
        using HipGraph graph = runtime.CreateGraph();
        HipAdvancedInterop advanced = runtime.AdvancedInterop;
        HipAdvancedGraphNode signal = advanced.AddExternalSemaphoreSignalNode(graph, null, descriptor.Descriptor);
        HipAdvancedGraphNode copy = advanced.AddDriverMemcpyNode(graph, new[] { signal }, descriptor.Descriptor);
        Assert.HasCount(2, graph.Nodes);
        Assert.AreEqual(HipGraphNodeType.ExternalSemaphoreSignal, graph.Nodes[0].Type);
        Assert.AreSame(graph.Nodes[0], graph.Nodes[1].Dependencies[0]);
        Assert.HasCount(1, graph.Edges);
        Assert.ThrowsExactly<ArgumentException>(() => advanced.AddDriverMemcpyNode(graph, new[] { signal, signal }, descriptor.Descriptor));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.SetExternalSemaphoreWaitNodeParameters(signal, descriptor.Descriptor));
        using HipGraphExec executable = graph.Instantiate();
        Assert.ThrowsExactly<InvalidOperationException>(() => advanced.AddExternalSemaphoreSignalNode(graph, null, descriptor.Descriptor));
        Assert.ThrowsExactly<InvalidOperationException>(() => advanced.SetDriverMemcpyNodeParameters(copy, descriptor.Descriptor));
        using HipStream stream = runtime.CreateStream();
        executable.Launch(stream);
        Assert.ThrowsExactly<InvalidOperationException>(() => advanced.UpdateExternalSemaphoreSignalNode(executable, signal, descriptor.Descriptor));
        Assert.ThrowsExactly<InvalidOperationException>(() => advanced.UpdateDriverMemcpyNode(executable, copy, descriptor.Descriptor));
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDrvGraphExecMemcpyNodeSetParams"));
        stream.Synchronize();
        advanced.UpdateDriverMemcpyNode(executable, copy, descriptor.Descriptor);
        advanced.UpdateExternalSemaphoreSignalNode(executable, signal, descriptor.Descriptor);
        Assert.IsTrue(native.AdvancedCalls.Contains("hipDrvGraphExecMemcpyNodeSetParams"));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.UpdateExternalSemaphoreWaitNode(executable, signal, descriptor.Descriptor));
    }

    private sealed class NativeDescriptorBuffer : IDisposable
    {
        private readonly IntPtr _pointer = Marshal.AllocHGlobal(64);
        internal HipNativeDescriptor Descriptor => new(_pointer);
        public void Dispose() => Marshal.FreeHGlobal(_pointer);
    }
}
