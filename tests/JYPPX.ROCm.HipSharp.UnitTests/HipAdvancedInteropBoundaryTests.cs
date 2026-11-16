using System;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA1861

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipAdvancedInteropBoundaryTests
{
    [TestMethod]
    public void DefaultDescriptorsAreRejectedBeforeImportOrDriverCalls()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        var advanced = runtime.AdvancedInterop;
        Action[] calls =
        {
            () => advanced.ImportExternalMemory(default),
            () => advanced.ImportExternalSemaphore(default),
            () => advanced.CreateUserObject(IntPtr.Zero, default),
            () => advanced.LaunchDriverKernel(default, new IntPtr(1)),
            () => advanced.CopyDriver2DUnaligned(default),
            () => advanced.CopyDriver3D(default),
            () => advanced.CopyDriver3DAsync(default, stream),
            () => advanced.GetDriverPointerAttributes(new[] { 1 }, default, new IntPtr(1))
        };
        foreach (Action call in calls) Assert.ThrowsExactly<ArgumentException>(call);
        Assert.HasCount(0, native.AdvancedCalls);
    }

    [TestMethod]
    public void DefaultMappingAndSemaphoreDescriptorsDoNotAcquirePersistentBorrows()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var stream = runtime.CreateStream();
        using var memory = runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value);
        using var semaphore = runtime.AdvancedInterop.ImportExternalSemaphore(descriptor.Value);
        int before = native.AdvancedCalls.Count;
        Assert.ThrowsExactly<ArgumentException>(() => memory.MapBuffer(default));
        Assert.ThrowsExactly<ArgumentException>(() => memory.MapMipmappedArray(default));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.AdvancedInterop.SignalExternalSemaphores(stream, new[] { semaphore }, default));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.AdvancedInterop.WaitExternalSemaphores(stream, new[] { semaphore }, default));
        Assert.HasCount(before, native.AdvancedCalls);
        memory.Dispose();
        semaphore.Dispose();
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipDestroyExternalMemory"));
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
    }

    [TestMethod]
    public void DefaultGraphDescriptorsCannotAddOrMutateNodes()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var graph = runtime.CreateGraph();
        var advanced = runtime.AdvancedInterop;
        var signal = advanced.AddExternalSemaphoreSignalNode(graph, null, descriptor.Value);
        var wait = advanced.AddExternalSemaphoreWaitNode(graph, null, descriptor.Value);
        var copy = advanced.AddDriverMemcpyNode(graph, null, descriptor.Value);
        int before = native.AdvancedCalls.Count;
        Action[] calls =
        {
            () => advanced.AddExternalSemaphoreSignalNode(graph, null, default),
            () => advanced.AddExternalSemaphoreWaitNode(graph, null, default),
            () => advanced.AddDriverMemcpyNode(graph, null, default),
            () => advanced.AddDriverMemsetNode(graph, null, default),
            () => advanced.SetExternalSemaphoreSignalNodeParameters(signal, default),
            () => advanced.GetExternalSemaphoreSignalNodeParameters(signal, default),
            () => advanced.SetExternalSemaphoreWaitNodeParameters(wait, default),
            () => advanced.GetExternalSemaphoreWaitNodeParameters(wait, default),
            () => advanced.SetDriverMemcpyNodeParameters(copy, default),
            () => advanced.GetDriverMemcpyNodeParameters(copy, default)
        };
        foreach (Action call in calls) Assert.ThrowsExactly<ArgumentException>(call);
        Assert.HasCount(before, native.AdvancedCalls);
        Assert.HasCount(3, graph.Nodes);
        // Memory-free nodes take a pointer, so their internal default descriptor is valid.
        Assert.IsTrue(advanced.AddDriverMemFreeNode(graph, null, new IntPtr(123)).IsValid);
    }

    [TestMethod]
    public void DefaultExecutableDescriptorsAreRejectedWithoutNativeUpdates()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var graph = runtime.CreateGraph();
        var advanced = runtime.AdvancedInterop;
        var signal = advanced.AddExternalSemaphoreSignalNode(graph, null, descriptor.Value);
        var wait = advanced.AddExternalSemaphoreWaitNode(graph, null, descriptor.Value);
        var copy = advanced.AddDriverMemcpyNode(graph, null, descriptor.Value);
        var memset = advanced.AddDriverMemsetNode(graph, null, descriptor.Value);
        using var executable = graph.Instantiate();
        int before = native.AdvancedCalls.Count;
        Assert.ThrowsExactly<ArgumentException>(() => advanced.UpdateExternalSemaphoreSignalNode(executable, signal, default));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.UpdateExternalSemaphoreWaitNode(executable, wait, default));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.UpdateDriverMemcpyNode(executable, copy, default));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.UpdateDriverMemsetNode(executable, memset, default));
        Assert.HasCount(before, native.AdvancedCalls);
    }

    [TestMethod]
    public void RetainedFacadeCannotRegisterCallbacksOrQueryDriverAfterRuntimeDisposal()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        var advanced = runtime.AdvancedInterop;
        runtime.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.AddStreamCallback(stream, _ => { }));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.GetDriverErrorName(HipError.Success));
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.GetDriverErrorString(HipError.Success));
        Assert.HasCount(0, native.AdvancedCalls);
    }

    [TestMethod]
    public void CallbacksRejectCaptureForeignAndDisposedStreamsWithoutNativeRegistration()
    {
        using var native = new FakeHipNativeApi();
        using var foreignNative = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var foreign = new HipRuntime(foreignNative);
        using var stream = runtime.CreateStream();
        using var otherStream = foreign.CreateStream();
        var advanced = runtime.AdvancedInterop;
        Assert.ThrowsExactly<ArgumentNullException>(() => advanced.AddStreamCallback(null!, _ => { }));
        Assert.ThrowsExactly<ArgumentNullException>(() => advanced.AddStreamCallback(stream, null!));
        Assert.ThrowsExactly<ArgumentException>(() => advanced.AddStreamCallback(otherStream, _ => { }));
        stream.BeginCapture();
        try { Assert.ThrowsExactly<InvalidOperationException>(() => advanced.AddStreamCallback(stream, _ => { })); }
        finally { using var captured = stream.EndCapture(); }
        stream.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => advanced.AddStreamCallback(stream, _ => { }));
        Assert.HasCount(0, native.AdvancedCalls);
    }

    [TestMethod]
    public void CallbackFailurePreservesErrorAndSuccessfulCallbackRunsOnceWithNativeStatus()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        int callbacks = 0;
        HipError observedStatus = HipError.Success;
        native.AdvancedResult = HipError.NotSupported;
        HipException error = Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.AddStreamCallback(stream, _ => callbacks++));
        Assert.AreEqual("hipStreamAddCallback", error.Operation);
        Assert.AreEqual(HipError.NotSupported, error.Error);
        native.AdvancedResult = HipError.Success;
        native.CallbackStatus = HipError.InvalidValue;
        runtime.AdvancedInterop.AddStreamCallback(stream, status =>
        {
            callbacks++;
            observedStatus = status;
            throw new InvalidOperationException("User exceptions must not cross the native callback boundary.");
        });
        Assert.AreEqual(0, callbacks);
        stream.Synchronize();
        stream.Synchronize();
        Assert.AreEqual(1, callbacks);
        Assert.AreEqual(HipError.InvalidValue, observedStatus);
    }

    [TestMethod]
    public void GraphicsNullSuccessIsRejectedAndMappingRemainsUsableForRetry()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        using var resource = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(1234));
        using var mapping = runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream);
        native.ReturnNullAdvancedHandle = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => mapping.GetMappedPointer(resource));
        Assert.ThrowsExactly<InvalidOperationException>(() => mapping.GetMappedArray(resource, 0, 0));
        native.ReturnNullAdvancedHandle = false;
        Assert.AreNotEqual(IntPtr.Zero, mapping.GetMappedPointer(resource).Pointer);
        Assert.AreNotEqual(IntPtr.Zero, mapping.GetMappedArray(resource, 1, 2));
    }

    [TestMethod]
    public void ExternalMappingFailureAndNullSuccessReleaseTheParentBorrows()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var memory = runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value);
        native.AdvancedResult = HipError.NotSupported;
        Assert.AreEqual("hipExternalMemoryGetMappedBuffer", Assert.ThrowsExactly<HipException>(() => memory.MapBuffer(descriptor.Value)).Operation);
        Assert.AreEqual("hipExternalMemoryGetMappedMipmappedArray", Assert.ThrowsExactly<HipException>(() => memory.MapMipmappedArray(descriptor.Value)).Operation);
        native.AdvancedResult = HipError.Success;
        native.ReturnNullAdvancedHandle = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => memory.MapBuffer(descriptor.Value));
        Assert.ThrowsExactly<InvalidOperationException>(() => memory.MapMipmappedArray(descriptor.Value));
        memory.Dispose();
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipDestroyExternalMemory"));
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(0, native.MipmappedArrayFreeCallCount);
    }

    [TestMethod]
    public void ExternalImportsRejectNullSuccessAndPreserveNativeErrorIdentity()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        native.ReturnNullAdvancedHandle = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value));
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.AdvancedInterop.ImportExternalSemaphore(descriptor.Value));
        Assert.ThrowsExactly<InvalidOperationException>(() => runtime.AdvancedInterop.CreateUserObject(IntPtr.Zero, descriptor.Value));
        native.AdvancedResult = HipError.NotSupported;
        Assert.AreEqual("hipImportExternalMemory", Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value)).Operation);
        Assert.AreEqual("hipImportExternalSemaphore", Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.ImportExternalSemaphore(descriptor.Value)).Operation);
        Assert.AreEqual("hipUserObjectCreate", Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.CreateUserObject(IntPtr.Zero, descriptor.Value)).Operation);
        Assert.IsFalse(native.AdvancedCalls.Any(call => call.StartsWith("hipDestroy", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ExternalMipmapRetainsParentAndRetriesFreeAndParentDestructionSeparately()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var memory = runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value);
        using var mipmap = memory.MapMipmappedArray(descriptor.Value);
        memory.Dispose();
        native.ArrayTextureReleaseResult = HipError.InvalidValue;
        Assert.AreEqual("hipFreeMipmappedArray(external-memory)", Assert.ThrowsExactly<HipException>(mipmap.Dispose).Operation);
        Assert.AreEqual(0, native.MipmappedArrayFreeCallCount);
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalMemory"));
        native.ArrayTextureReleaseResult = HipError.Success;
        native.AdvancedResult = HipError.NotSupported;
        Assert.AreEqual("hipDestroyExternalMemory", Assert.ThrowsExactly<HipException>(mipmap.Dispose).Operation);
        Assert.AreEqual(1, native.MipmappedArrayFreeCallCount);
        native.AdvancedResult = HipError.Success;
        mipmap.Dispose();
        mipmap.Dispose();
        Assert.AreEqual(1, native.MipmappedArrayFreeCallCount);
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipDestroyExternalMemory"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => mipmap.DangerousGetHandle());
    }

    [TestMethod]
    public void ExternalBufferAndMipmapReleaseTheirCommonParentOnlyAfterTheLastMapping()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var descriptor = new DescriptorStorage();
        using var memory = runtime.AdvancedInterop.ImportExternalMemory(descriptor.Value);
        using var buffer = memory.MapBuffer(descriptor.Value);
        using var mipmap = memory.MapMipmappedArray(descriptor.Value);
        memory.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.MapBuffer(descriptor.Value));
        Assert.ThrowsExactly<ObjectDisposedException>(() => memory.MapMipmappedArray(descriptor.Value));
        buffer.Dispose();
        Assert.AreEqual(1, native.FreeCount);
        Assert.IsFalse(native.AdvancedCalls.Contains("hipDestroyExternalMemory"));
        Assert.AreNotEqual(IntPtr.Zero, mipmap.DangerousGetHandle());
        mipmap.Dispose();
        Assert.AreEqual(1, native.MipmappedArrayFreeCallCount);
        Assert.AreEqual(1, native.AdvancedCalls.Count(call => call == "hipDestroyExternalMemory"));
    }

    [TestMethod]
    public void GraphicsQueryErrorsKeepTheirIdentityAndCannotEscapeTheMappingMembership()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        using var resource = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(1234));
        using var other = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(2345));
        using var mapping = runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream);
        int before = native.AdvancedCalls.Count;
        Assert.ThrowsExactly<ArgumentNullException>(() => mapping.GetMappedPointer(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() => mapping.GetMappedArray(null!, 0, 0));
        Assert.ThrowsExactly<ArgumentException>(() => mapping.GetMappedPointer(other));
        Assert.ThrowsExactly<ArgumentException>(() => mapping.GetMappedArray(other, 0, 0));
        Assert.HasCount(before, native.AdvancedCalls);
        native.AdvancedResult = HipError.NotSupported;
        try
        {
            HipException pointerError = Assert.ThrowsExactly<HipException>(() => mapping.GetMappedPointer(resource));
            HipException arrayError = Assert.ThrowsExactly<HipException>(() => mapping.GetMappedArray(resource, 0, 0));
            Assert.AreEqual("hipGraphicsResourceGetMappedPointer", pointerError.Operation);
            Assert.AreEqual("hipGraphicsSubResourceGetMappedArray", arrayError.Operation);
            Assert.AreEqual(HipError.NotSupported, pointerError.Error);
            Assert.AreEqual(HipError.NotSupported, arrayError.Error);
        }
        finally { native.AdvancedResult = HipError.Success; }
        mapping.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => mapping.GetMappedArray(resource, 0, 0));
    }

    private sealed class DescriptorStorage : IDisposable
    {
        private readonly IntPtr _pointer = Marshal.AllocHGlobal(64);
        internal HipNativeDescriptor Value => new(_pointer);
        public void Dispose() => Marshal.FreeHGlobal(_pointer);
    }
}
