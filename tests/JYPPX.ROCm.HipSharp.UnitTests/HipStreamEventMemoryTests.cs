using System;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipStreamEventMemoryTests
{
    [TestMethod]
    public void StreamAndEventOwnHandlesAndReleaseExactlyOnce()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipEvent start = runtime.CreateEvent();
        using HipEvent end = runtime.CreateEvent(HipEventFlags.DisableTiming);

        start.Record(stream);
        end.Record(stream);
        Assert.IsTrue(stream.Query());
        Assert.IsTrue(end.Query());
        Assert.AreEqual(1.25f, HipEvent.ElapsedTime(start, end));
        stream.Dispose();
        stream.Dispose();
        start.Dispose();
        end.Dispose();
        Assert.AreEqual(1, native.StreamDestroyCount);
        Assert.AreEqual(2, native.EventDestroyCount);
    }

    [TestMethod]
    public void AsyncMemoryLeaseDefersFreeUntilStreamCompletion()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipDeviceMemory memory = runtime.Allocate(8);
        memory.CopyFromAsync(new byte[8], stream);
        memory.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.AsyncCopyCount);
    }

    [TestMethod]
    public void ExplicitStreamKernelRetainsModuleAndMemoryUntilQuery()
    {
        using var native = new FakeHipNativeApi { StreamQueryResult = HipError.NotReady };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        HipDeviceMemory memory = runtime.Allocate(4);
        kernel.Launch(stream, new HipLaunchDimensions(1), new HipLaunchDimensions(1), new[] { HipKernelArgument.DevicePointer(memory) });
        memory.Dispose();
        module.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        Assert.AreEqual(0, native.ModuleUnloadCount);
        Assert.IsFalse(stream.Query());
        native.StreamQueryResult = HipError.Success;
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void PinnedHostLeaseDefersHostFreeUntilStreamCompletion()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipDeviceMemory device = runtime.Allocate(8);
        HipPinnedMemory pinned = runtime.AllocatePinned(8);
        pinned.CopyFrom(new byte[8]);
        device.CopyFromAsync(pinned, stream);
        pinned.Dispose();
        Assert.AreEqual(1, native.AsyncCopyCount);
        stream.Synchronize();
        Assert.ThrowsExactly<ObjectDisposedException>(() => pinned.DangerousGetHandle());
    }
}
