using System;
using System.Linq;
using System.Reflection;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipModuleGlobalTests
{
    private static readonly int[] OffsetValues = { 10, 20 };
    private static readonly int[] ExpectedOffsetValues = { 0, 10, 20, 0 };
    private static readonly int[] FullValues = { 11, 22, 33, 44 };

    [TestMethod]
    public void GetGlobalUsesModuleAndUtf8NameAndSeparatesSymbolContents()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });

        HipModuleGlobal counter = module.GetGlobal("counter");
        HipModuleGlobal values = module.GetGlobal("values");
        counter.CopyFrom(new byte[] { 1, 2, 3, 4 });
        values.CopyFrom(new byte[] { 9, 8, 7, 6 });
        var counterBytes = new byte[4];
        var valueBytes = new byte[4];
        counter.CopyTo(counterBytes);
        values.CopyTo(valueBytes);

        Assert.AreEqual("values", native.LastModuleGlobalName);
        Assert.AreEqual(new IntPtr(0x2000), native.LastModuleGlobalModule);
        Assert.AreEqual(2, native.ModuleGetGlobalCallCount);
        Assert.AreEqual(4UL, counter.ByteLength);
        Assert.AreEqual(16UL, values.ByteLength);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 4 }, counterBytes);
        CollectionAssert.AreEqual(new byte[] { 9, 8, 7, 6 }, valueBytes);
        Assert.AreEqual(0, native.FreeCallCount);
    }

    [TestMethod]
    public void GetGlobalValidatesNamesBeforeNativeDispatch()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });

        Assert.ThrowsExactly<ArgumentNullException>(() => module.GetGlobal(null!));
        Assert.ThrowsExactly<ArgumentException>(() => module.GetGlobal(string.Empty));
        Assert.ThrowsExactly<ArgumentException>(() => module.GetGlobal("  "));
        Assert.ThrowsExactly<ArgumentException>(() => module.GetGlobal("bad\0name"));
        Assert.AreEqual(0, native.ModuleGetGlobalCallCount);
    }

    [TestMethod]
    public void GetGlobalFailsClosedForNullZeroOverflowMissingAndOptionalExport()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });

        native.ReturnNullModuleGlobal = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => module.GetGlobal("counter"));
        native.ReturnNullModuleGlobal = false;
        native.ReturnZeroModuleGlobalSize = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => module.GetGlobal("counter"));
        native.ReturnZeroModuleGlobalSize = false;
        native.ReturnOverflowModuleGlobalRange = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => module.GetGlobal("counter"));
        native.ReturnOverflowModuleGlobalRange = false;

        HipException missing = Assert.ThrowsExactly<HipException>(() => module.GetGlobal("missing"));
        Assert.AreEqual(HipError.InvalidValue, missing.Error);
        Assert.AreEqual("hipModuleGetGlobal", missing.Operation);
        native.ModuleGetGlobalResult = HipError.NotSupported;
        HipException optional = Assert.ThrowsExactly<HipException>(() => module.GetGlobal("counter"));
        Assert.AreEqual(HipError.NotSupported, optional.Error);
        Assert.AreEqual("hipModuleGetGlobal", optional.Operation);
    }

    [TestMethod]
    public void TypedViewUsesElementUnitsAndRejectsNonDivisibleExtent()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipModuleGlobal<int> values = module.GetGlobal<int>("values");

        values.CopyFrom(OffsetValues, 1);
        var result = new int[4];
        values.CopyTo(result);

        Assert.AreEqual(4UL, values.ElementCount);
        Assert.AreEqual(16UL, values.ByteLength);
        CollectionAssert.AreEqual(ExpectedOffsetValues, result);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => values.CopyFrom(OffsetValues, 3));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => values.CopyTo(new int[1], ulong.MaxValue));
        Assert.ThrowsExactly<ArgumentException>(() => module.GetGlobal<long>("counter"));
    }

    [TestMethod]
    public void ByteViewSupportsOffsetsZeroLengthAndCheckedOverflow()
    {
        using var native = new FakeHipNativeApi();
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("values");

        global.CopyFrom(new byte[] { 4, 5, 6 }, 5);
        var destination = new byte[3];
        global.CopyTo(destination, 5);
        CollectionAssert.AreEqual(new byte[] { 4, 5, 6 }, destination);

        int copies = native.MemcpyCallCount;
        global.CopyFrom(Array.Empty<byte>(), global.ByteLength);
        global.CopyTo(Array.Empty<byte>(), global.ByteLength);
        Assert.AreEqual(copies, native.MemcpyCallCount);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => global.CopyFrom(new byte[1], global.ByteLength));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => global.CopyTo(new byte[1], ulong.MaxValue));
        Assert.IsTrue(typeof(HipModuleGlobal).GetMethods().Where(method => method.Name.StartsWith("Copy", StringComparison.Ordinal))
            .SelectMany(method => method.GetParameters()).Where(parameter => parameter.Name!.Contains("Offset", StringComparison.Ordinal))
            .All(parameter => parameter.ParameterType == typeof(ulong)));
    }

    [TestMethod]
    public void SyncHostAndDeviceCopiesUseCorrectDirectionsAndRanges()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("values");
        using HipPinnedMemory pinned = runtime.AllocatePinned(8);
        using HipDeviceMemory device = runtime.Allocate(8);

        pinned.CopyFrom(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 });
        device.CopyFrom(new byte[8]);
        global.CopyFrom(pinned, 4, 2, 1);
        Assert.AreEqual(HipMemoryCopyKind.HostToDevice, native.LastMemcpyKind);
        global.CopyTo(device, 4, 2, 1);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToDevice, native.LastMemcpyKind);
        var deviceBytes = new byte[8];
        device.CopyTo(deviceBytes);
        CollectionAssert.AreEqual(new byte[] { 0, 2, 3, 4, 5, 0, 0, 0 }, deviceBytes);
        global.CopyTo(pinned, 4, 2, 3);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToHost, native.LastMemcpyKind);
        var pinnedBytes = new byte[8];
        pinned.CopyTo(pinnedBytes);
        CollectionAssert.AreEqual(new byte[] { 1, 2, 3, 2, 3, 4, 5, 8 }, pinnedBytes);

        device.CopyFrom(new byte[] { 8, 7, 6, 5 });
        global.CopyFrom(device, 3, 8, 1);
        var result = new byte[3];
        global.CopyTo(result, 8);
        CollectionAssert.AreEqual(new byte[] { 7, 6, 5 }, result);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => global.CopyFrom(pinned, 2, 15));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => global.CopyTo(device, 9));
    }

    [TestMethod]
    public void AsyncArrayCopyRetainsModuleUntilQueryCompletion()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("counter");

        global.CopyFromAsync(new byte[] { 3, 2, 1, 0 }, stream);
        module.Dispose();
        Assert.AreEqual(HipMemoryCopyKind.HostToDevice, native.LastMemcpyKind);
        Assert.AreEqual(stream.DangerousGetHandle(), native.LastMemcpyStream);
        Assert.AreEqual(0, native.ModuleUnloadCount);
        Assert.IsFalse(global.IsValid);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void AsyncTypedDestinationCompletesWithoutInternalSynchronization()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal<int> values = module.GetGlobal<int>("values");
        values.CopyFrom(FullValues);
        var destination = new int[4];

        values.CopyToAsync(destination, stream);

        Assert.AreEqual(1, native.MemcpyAsyncCallCount);
        Assert.AreEqual(0, native.SynchronizeCount);
        Assert.AreEqual(HipMemoryCopyKind.DeviceToHost, native.LastMemcpyKind);
        stream.Synchronize();
        CollectionAssert.AreEqual(FullValues, destination);
    }

    [TestMethod]
    public void AsyncPinnedAndDeviceOwnersStayAliveUntilStreamCompletion()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("values");
        HipPinnedMemory pinned = runtime.AllocatePinned(4);
        HipDeviceMemory device = runtime.Allocate(4);

        pinned.CopyFrom(new byte[] { 1, 2, 3, 4 });
        global.CopyFromAsync(pinned, stream, 4);
        global.CopyToAsync(device, stream, 4);
        pinned.Dispose();
        device.Dispose();
        Assert.AreEqual(0, native.FreeCount);
        stream.Synchronize();
        Assert.AreEqual(2, native.FreeCount);
        Assert.AreEqual(2, native.MemcpyAsyncCallCount);
    }

    [TestMethod]
    public void StreamDisposeDrainsPendingModuleAndOwners()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipPinnedMemory pinned = runtime.AllocatePinned(4);
        HipModuleGlobal global = module.GetGlobal("counter");
        global.CopyToAsync(pinned, stream, 4);
        pinned.Dispose();
        module.Dispose();

        stream.Dispose();

        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.AreEqual(1, native.StreamDestroyCount);
    }

    [TestMethod]
    public void AsyncCopyFailureRegistersNoPendingLeaseAndReleasesOwners()
    {
        using var native = new FakeHipNativeApi { MemcpyAsyncResult = HipError.InvalidValue };
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory device = runtime.Allocate(4);
        HipModuleGlobal global = module.GetGlobal("counter");

        HipException failure = Assert.ThrowsExactly<HipException>(() => global.CopyToAsync(device, stream, 4));
        Assert.AreEqual("hipMemcpyAsync(device-to-device module global)", failure.Operation);
        device.Dispose();
        module.Dispose();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
        stream.Synchronize();
        Assert.AreEqual(1, native.FreeCount);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void SynchronousCopyFailurePropagatesWithoutCompilerSymbolFallback()
    {
        using var native = new FakeHipNativeApi { MemcpyResult = HipError.InvalidMemcpyDirection };
        using HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("counter");

        HipException failure = Assert.ThrowsExactly<HipException>(() => global.CopyFrom(new byte[] { 1, 2, 3, 4 }));

        Assert.AreEqual(HipError.InvalidMemcpyDirection, failure.Error);
        Assert.AreEqual("hipMemcpy(module global)", failure.Operation);
        Assert.AreEqual(1, native.MemcpyCallCount);
        Assert.AreEqual(0, native.MemcpyAsyncCallCount);
        Assert.AreEqual(1, native.ModuleGetGlobalCallCount);
    }

    [TestMethod]
    public void PendingCleanupFailureCanRetryWithoutDoubleRelease()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipDeviceMemory device = runtime.Allocate(4);
        HipModuleGlobal global = module.GetGlobal("counter");
        global.CopyToAsync(device, stream, 4);
        device.Dispose();
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
    public void PendingModuleUnloadFailureCanRetryWithoutDoubleRelease()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("counter");
        global.CopyFromAsync(new byte[] { 1, 2, 3, 4 }, stream);
        module.Dispose();
        native.ModuleUnloadResult = HipError.InvalidValue;

        HipException failure = Assert.ThrowsExactly<HipException>(() => stream.Query());
        Assert.AreEqual("hipModuleUnload", failure.Operation);
        Assert.AreEqual(0, native.ModuleUnloadCount);
        native.ModuleUnloadResult = HipError.Success;
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.IsTrue(stream.Query());
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void DisposedModuleAndOwnersAreRejectedBeforeNativeCopy()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("counter");
        module.Dispose();
        int copies = native.MemcpyCallCount;
        Assert.ThrowsExactly<ObjectDisposedException>(() => global.CopyFrom(new byte[4]));
        Assert.AreEqual(copies, native.MemcpyCallCount);

        using HipModule liveModule = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal liveGlobal = liveModule.GetGlobal("counter");
        HipPinnedMemory pinned = runtime.AllocatePinned(4);
        pinned.Dispose();
        HipDeviceMemory device = runtime.Allocate(4);
        device.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => liveGlobal.CopyFrom(pinned, 4));
        Assert.ThrowsExactly<ObjectDisposedException>(() => liveGlobal.CopyTo(device, 4));
    }

    [TestMethod]
    public void ReloadedModuleHasIndependentSameNameSymbolIdentity()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipModule first = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal oldCounter = first.GetGlobal("counter");
        oldCounter.CopyFrom(new byte[] { 1, 2, 3, 4 });
        first.Dispose();

        using HipModule second = runtime.LoadModule(new byte[] { 1 });
        HipModuleGlobal newCounter = second.GetGlobal("counter");
        var newBytes = new byte[4];
        newCounter.CopyTo(newBytes);

        CollectionAssert.AreEqual(new byte[4], newBytes);
        Assert.IsFalse(oldCounter.IsValid);
        Assert.ThrowsExactly<ObjectDisposedException>(() => oldCounter.CopyTo(new byte[4]));
        Assert.IsTrue(newCounter.IsValid);
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void CrossRuntimeDeviceStreamAndCurrentDeviceMismatchFailBeforeCopy()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        var first = new HipRuntime(firstNative);
        var second = new HipRuntime(secondNative);
        using HipModule module = first.LoadModule(new byte[] { 1 });
        HipModuleGlobal global = module.GetGlobal("counter");
        using HipStream foreignStream = second.CreateStream();
        using HipDeviceMemory foreignMemory = second.Allocate(4);
        using HipPinnedMemory foreignPinned = second.AllocatePinned(4);

        Assert.ThrowsExactly<ArgumentException>(() => global.CopyFromAsync(new byte[4], foreignStream));
        Assert.ThrowsExactly<ArgumentException>(() => global.CopyFromAsync(Array.Empty<byte>(), foreignStream, global.ByteLength));
        Assert.ThrowsExactly<ArgumentException>(() => global.CopyFrom(foreignPinned, 4));
        Assert.ThrowsExactly<ArgumentException>(() => global.CopyTo(foreignMemory, 4));

        first.GetDevice(1).MakeCurrent();
        using HipStream wrongDeviceStream = first.CreateStream();
        using HipDeviceMemory wrongDeviceMemory = first.Allocate(4);
        first.GetDevice(0).MakeCurrent();
        Assert.ThrowsExactly<ArgumentException>(() => global.CopyToAsync(new byte[4], wrongDeviceStream));
        Assert.ThrowsExactly<ArgumentException>(() => global.CopyTo(wrongDeviceMemory, 4));
        first.GetDevice(1).MakeCurrent();
        Assert.ThrowsExactly<InvalidOperationException>(() => global.CopyFrom(new byte[4]));
        Assert.AreEqual(0, firstNative.MemcpyCallCount);
    }

    [TestMethod]
    public void RuntimeCompilerSymbolApisAreNotPartOfManagedNativeBoundary()
    {
        string[] forbidden =
        {
            "GetSymbolAddress", "GetSymbolSize", "MemcpyFromSymbol", "MemcpyFromSymbolAsync",
            "MemcpyToSymbol", "MemcpyToSymbolAsync",
        };
        string[] managedBoundary = typeof(JYPPX.ROCm.HipSharp.Interop.IHipNativeApi)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.Name).ToArray();
        foreach (string name in forbidden) CollectionAssert.DoesNotContain(managedBoundary, name);
    }
}
