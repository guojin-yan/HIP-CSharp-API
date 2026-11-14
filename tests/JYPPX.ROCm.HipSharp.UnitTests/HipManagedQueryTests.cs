using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public class HipManagedQueryTests
{
    [TestMethod]
    public void RetainRejectsUnsafeNativeVersionsBeforeCreatingAnOwner()
    {
        using var native = new FakeHipNativeApi { RuntimeVersion = 70253211 };
        using var runtime = new HipRuntime(native);
        using var reservation = runtime.ReserveVirtualMemory(4096);
        using var allocation = runtime.CreatePhysicalMemory(4096, new HipVirtualMemoryAllocationOptions(0));
        using var mapping = reservation.Map(allocation, 4096);
        Assert.ThrowsExactly<PlatformNotSupportedException>(() => runtime.RetainPhysicalMemory(reservation.DangerousGetAddress(), 4096));
        Assert.AreEqual(0, native.RetainAllocationCallCount);
        Assert.IsFalse(allocation.IsDisposed);
    }

    [TestMethod]
    public void RetainOnFixedNativeVersionOwnsItsIndependentReference()
    {
        using var native = new FakeHipNativeApi { RuntimeVersion = 71200000 };
        using var runtime = new HipRuntime(native);
        using var reservation = runtime.ReserveVirtualMemory(4096);
        using var allocation = runtime.CreatePhysicalMemory(4096, new HipVirtualMemoryAllocationOptions(0));
        using var mapping = reservation.Map(allocation, 4096);
        using var retained = runtime.RetainPhysicalMemory(reservation.DangerousGetAddress(), 4096);
        retained.Dispose();
        Assert.AreEqual(1, native.RetainAllocationCallCount);
        Assert.IsFalse(allocation.IsDisposed);
    }

    [TestMethod]
    public void RetainPropagatesVersionQueryFailureWithoutCallingNativeRetain()
    {
        using var native = new FakeHipNativeApi { RuntimeVersionResult = HipError.InvalidValue };
        using var runtime = new HipRuntime(native);
        Assert.ThrowsExactly<HipException>(() => runtime.RetainPhysicalMemory(new IntPtr(1), 4096));
        Assert.AreEqual(0, native.RetainAllocationCallCount);
    }

    [TestMethod]
    public void DeviceQueriesReturnTypedValues()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipDevice device = runtime.GetDevice(0);

        Assert.AreEqual(1, native.DeviceGetCallCount);
        Assert.AreEqual(0, device.Ordinal);

        HipComputeCapability capability = device.GetComputeCapability();
        Assert.AreEqual(9, capability.Major);
        Assert.AreEqual(0, capability.Minor);
        Assert.AreEqual("0000:00:00.0", device.GetPciBusId());
        Assert.AreEqual(256, native.LastDeviceGetPciBusIdLength);
        Assert.AreEqual(device.Ordinal, native.LastDeviceGetPciBusIdDevice);
        Assert.AreEqual(new string('0', 32), device.GetUuid().ToString());
        Assert.AreEqual(8UL * 1024UL * 1024UL, device.GetTotalMemory());
        Assert.AreNotEqual(IntPtr.Zero, native.LastDeviceTotalMemOutput);
        Assert.AreEqual(device.Ordinal, native.LastDeviceTotalMemDevice);
        Assert.AreEqual(HipDeviceCacheConfig.Default, runtime.GetDeviceCacheConfig());
        Assert.AreEqual(HipSharedMemoryConfig.DefaultBankSize, runtime.GetDeviceSharedMemoryConfig());
        Assert.AreEqual(4096UL, runtime.GetDeviceLimit(0));
        Assert.AreEqual(1, runtime.GetP2PAttribute(0, 0, 1));
        Assert.AreEqual(0UL, runtime.GetDeviceGraphMemoryAttribute(0, 0));
    }

    [TestMethod]
    public void PciLookupAndPriorityRangeUseManagedBoundary()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        HipDevice device = runtime.GetDeviceByPciBusId("0000:00:00.0");
        HipStreamPriorityRange priorities = runtime.GetStreamPriorityRange();

        Assert.AreEqual(0, device.Ordinal);
        Assert.AreEqual(0, priorities.LeastPriority);
        Assert.AreEqual(-1, priorities.GreatestPriority);
        Assert.ThrowsExactly<ArgumentException>(() => runtime.GetDeviceByPciBusId(" "));
    }

    [TestMethod]
    public void PointerAndSymbolQueriesRejectNullAddresses()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<ArgumentException>(() => runtime.GetSymbolAddress(IntPtr.Zero));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.GetSymbolSize(IntPtr.Zero));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.GetPointerAttribute<int>(IntPtr.Zero, 0));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.GetPointerAttributes(IntPtr.Zero));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.SetPointerAttribute(IntPtr.Zero, 0, 1));
    }

    [TestMethod]
    public void PointerAndSymbolQueriesReturnNativeValues()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        Assert.AreEqual(new IntPtr(0x1234), runtime.GetSymbolAddress(new IntPtr(1)));
        Assert.AreEqual(128UL, runtime.GetSymbolSize(new IntPtr(1)));
        Assert.AreEqual(0L, runtime.GetPointerAttribute<long>(new IntPtr(1), 0));
        HipPointerAttributes attributes = runtime.GetPointerAttributes(new IntPtr(1));
        Assert.AreEqual(0, attributes.MemoryType);
        runtime.SetPointerAttribute(new IntPtr(1), 0, 42);
    }

    [TestMethod]
    public void StreamQueriesExposeBorrowedCaptureDataAndWaitValidation()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();
        using HipEvent eventToWaitFor = runtime.CreateEvent();

        CollectionAssert.AreEqual(new uint[] { 1, 0 }, stream.GetComputeUnitMask(2));
        Assert.AreEqual(HipStreamFlags.Default, stream.GetNativeFlags());
        Assert.AreEqual(0, stream.GetNativeDeviceOrdinal());
        Assert.AreEqual(0, stream.GetNativePriority());
        Assert.AreNotEqual(0UL, stream.GetNativeIdentifier());
        HipStreamCaptureInfo captureInfo = stream.GetCaptureInfo();
        Assert.AreEqual(HipStreamCaptureStatus.None, captureInfo.Status);
        HipStreamCaptureInfoV2 captureInfoV2 = stream.GetCaptureInfoV2();
        Assert.AreEqual(IntPtr.Zero, captureInfoV2.GraphHandle);
        Assert.AreEqual(0UL, captureInfoV2.DependencyCount);
        Assert.AreEqual(0, stream.GetAttribute(0).IntegerValue);

        stream.Wait(eventToWaitFor);
        IntPtr signalAddress = new(0x1234);
        stream.WaitValue32(signalAddress, 0x10203040, HipStreamWaitValueFlags.GreaterOrEqual, 0x00FFFFFF);
        stream.WaitValue64(signalAddress, 0x0102030405060708UL, HipStreamWaitValueFlags.And, 0x00FFFFFFFFFFFFFFUL);
        Assert.AreEqual(1, native.StreamWaitValue32CallCount);
        Assert.AreEqual(signalAddress, native.LastStreamWaitValue32Pointer);
        Assert.AreEqual(0x10203040U, native.LastStreamWaitValue32Value);
        Assert.AreEqual(0U, native.LastStreamWaitValue32Flags);
        Assert.AreEqual(0x00FFFFFFU, native.LastStreamWaitValue32Mask);
        Assert.AreEqual(1, native.StreamWaitValue64CallCount);
        Assert.AreEqual(signalAddress, native.LastStreamWaitValue64Pointer);
        Assert.AreEqual(0x0102030405060708UL, native.LastStreamWaitValue64Value);
        Assert.AreEqual(2U, native.LastStreamWaitValue64Flags);
        Assert.AreEqual(0x00FFFFFFFFFFFFFFUL, native.LastStreamWaitValue64Mask);
        stream.WaitValue32(signalAddress, 0, HipStreamWaitValueFlags.Equal);
        stream.WaitValue64(signalAddress, 0, HipStreamWaitValueFlags.Nor);
        Assert.AreEqual(2, native.StreamWaitValue32CallCount);
        Assert.AreEqual(1U, native.LastStreamWaitValue32Flags);
        Assert.AreEqual(2, native.StreamWaitValue64CallCount);
        Assert.AreEqual(3U, native.LastStreamWaitValue64Flags);
        Assert.ThrowsExactly<ArgumentException>(() => stream.WaitValue32(IntPtr.Zero, 0));
        Assert.ThrowsExactly<ArgumentException>(() => stream.WaitValue64(IntPtr.Zero, 0));
    }

    [TestMethod]
    public void QueryFailuresUseHipException()
    {
        using var native = new FakeHipNativeApi { ManagedNextResult = HipError.InvalidValue };
        var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<HipException>(() => runtime.GetDevice(0).GetComputeCapability());
        Assert.ThrowsExactly<HipException>(() => runtime.GetDeviceCacheConfig());
    }

    [TestMethod]
    public void StreamWaitRejectsCrossRuntimeEvent()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        var first = new HipRuntime(firstNative);
        var second = new HipRuntime(secondNative);
        using HipStream stream = first.CreateStream();
        using HipEvent foreignEvent = second.CreateEvent();

        Assert.ThrowsExactly<ArgumentException>(() => stream.Wait(foreignEvent));
    }

    [TestMethod]
    public void VirtualMemoryMappingKeepsReservationAndAllocationAlive()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        var reservation = runtime.ReserveVirtualMemory(4096);
        var allocation = runtime.CreatePhysicalMemory(4096, new HipVirtualMemoryAllocationOptions(0));
        HipVirtualMemoryMapping mapping = reservation.Map(allocation, 4096);

        reservation.SetAccess(4096, new HipVirtualMemoryAccessDescriptor(new HipMemLocation(1, 0), HipMemoryAccessFlags.ReadWrite));
        Assert.AreEqual(HipMemoryAccessFlags.ReadWrite, reservation.GetAccess(new HipMemLocation(1, 0)));
        Assert.ThrowsExactly<InvalidOperationException>(() => reservation.Dispose());
        Assert.ThrowsExactly<InvalidOperationException>(() => allocation.Dispose());

        mapping.Dispose();
        allocation.Dispose();
        reservation.Dispose();
        Assert.IsTrue(mapping.IsDisposed);
        Assert.IsTrue(allocation.IsDisposed);
        Assert.IsTrue(reservation.IsDisposed);
    }

    [TestMethod]
    public void VirtualMemoryValidatesCapabilityInputsAndNativeFailures()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipStream stream = runtime.CreateStream();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.ReserveVirtualMemory(0));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.ImportPhysicalMemory(IntPtr.Zero, HipMemoryAllocationHandleType.Win32, 4096));
        Assert.ThrowsExactly<ArgumentException>(() => runtime.MapArrayAsync(IntPtr.Zero, 1, stream));

        native.ManagedNextResult = HipError.NotSupported;
        Assert.ThrowsExactly<HipException>(() => runtime.ReserveVirtualMemory(4096));
    }

    [TestMethod]
    public void PosixImportAcceptsZeroAndRejectsValuesOutsideIntRange()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var allocation = runtime.ImportPhysicalMemory(IntPtr.Zero, HipMemoryAllocationHandleType.PosixFileDescriptor, 4096);
        Assert.AreEqual(IntPtr.Zero, native.LastImportedShareableHandle);
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.ImportPhysicalMemory(new IntPtr(-1), HipMemoryAllocationHandleType.PosixFileDescriptor, 4096));
        if (IntPtr.Size == 8)
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.ImportPhysicalMemory(new IntPtr((long)int.MaxValue + 1), HipMemoryAllocationHandleType.PosixFileDescriptor, 4096));
        Assert.AreEqual(1, native.ImportShareableHandleCallCount);
    }

    [TestMethod]
    public void PosixImportPreservesNativeRejectionOfZero()
    {
        using var native = new FakeHipNativeApi { ManagedNextResult = HipError.InvalidValue };
        using var runtime = new HipRuntime(native);
        Assert.ThrowsExactly<HipException>(() => runtime.ImportPhysicalMemory(IntPtr.Zero, HipMemoryAllocationHandleType.PosixFileDescriptor, 4096));
        Assert.AreEqual(1, native.ImportShareableHandleCallCount);
    }

    [TestMethod]
    public void ShareableExportUsesSignedIntForPosixAndPointerForWin32()
    {
        using var native = new FakeHipNativeApi { ExportedShareableHandle = IntPtr.Zero };
        using var runtime = new HipRuntime(native);
        using var allocation = runtime.CreatePhysicalMemory(4096, new HipVirtualMemoryAllocationOptions(0));
        Assert.AreEqual(IntPtr.Zero, allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor));
        native.ExportedShareableHandle = new IntPtr(-1);
        Assert.ThrowsExactly<InvalidOperationException>(() => allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor));
        native.ExportedShareableHandle = new IntPtr(int.MaxValue);
        Assert.AreEqual(new IntPtr(int.MaxValue), allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor));
        native.ExportedShareableHandle = IntPtr.Size == 8 ? new IntPtr(0x12345678ABCDEF) : new IntPtr(0x12345678);
        Assert.AreEqual(native.ExportedShareableHandle, allocation.ExportShareableHandle(HipMemoryAllocationHandleType.Win32));
        native.ExportedShareableHandle = IntPtr.Zero;
        Assert.ThrowsExactly<InvalidOperationException>(() => allocation.ExportShareableHandle(HipMemoryAllocationHandleType.Win32));
    }

    [TestMethod]
    public void ShareableHandlesRejectAmbiguousTypesAndPropagateExportErrors()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var allocation = runtime.CreatePhysicalMemory(4096, new HipVirtualMemoryAllocationOptions(0));
        foreach (var type in new[] { HipMemoryAllocationHandleType.None, HipMemoryAllocationHandleType.PosixFileDescriptor | HipMemoryAllocationHandleType.Win32, (HipMemoryAllocationHandleType)8 })
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => allocation.ExportShareableHandle(type));
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => runtime.ImportPhysicalMemory(new IntPtr(5), type, 4096));
        }
        native.ManagedNextResult = HipError.NotSupported;
        try
        {
            Assert.ThrowsExactly<HipException>(() => allocation.ExportShareableHandle(HipMemoryAllocationHandleType.PosixFileDescriptor));
            Assert.ThrowsExactly<HipException>(() => allocation.ExportShareableHandle(HipMemoryAllocationHandleType.Win32));
        }
        finally { native.ManagedNextResult = HipError.Success; }
    }
}
