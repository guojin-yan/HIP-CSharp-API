using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

#pragma warning disable CA1861

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipAdvancedInteropTests
{
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

    private sealed class NativeDescriptorBuffer : IDisposable
    {
        private readonly IntPtr _pointer = Marshal.AllocHGlobal(64);
        internal HipNativeDescriptor Descriptor => new(_pointer);
        public void Dispose() => Marshal.FreeHGlobal(_pointer);
    }
}
