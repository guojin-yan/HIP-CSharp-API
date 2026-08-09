using System;
using System.Linq;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipRuntimeTests
{
    [TestMethod]
    public void RuntimeProvidesVersionsDevicesAndSynchronizationThroughFacade()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);

        runtime.Initialize(0);
        HipRuntimeVersionInfo versions = runtime.GetVersionInfo();
        HipDevice[] devices = runtime.GetDevices().ToArray();
        devices[1].MakeCurrent();
        runtime.Synchronize();

        Assert.AreEqual(0U, native.LastInitFlags);
        Assert.AreEqual("7.2.1", versions.RuntimeVersion.ToString());
        Assert.AreEqual("7.2.0", versions.DriverVersion.ToString());
        Assert.AreEqual(2, devices.Length);
        Assert.AreEqual("Fake Radeon 1", runtime.GetCurrentDevice().Name);
        Assert.AreEqual(1, native.LastSetDevice);
        Assert.AreEqual(1, native.SynchronizeCount);
    }

    [TestMethod]
    public void DeviceMemoryCopiesInBothDirectionsAndReleasesExactlyOnce()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        byte[] source = { 1, 3, 5, 7, 9 };
        var destination = new byte[source.Length];

        using (HipDeviceMemory first = runtime.Allocate((ulong)source.Length))
        using (HipDeviceMemory second = runtime.Allocate((ulong)source.Length))
        {
            first.CopyFrom(source);
            first.CopyTo(second, (ulong)source.Length);
            second.CopyTo(destination);
            CollectionAssert.AreEqual(source, destination);

            Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => first.CopyTo(new byte[source.Length + 1]));
            first.Dispose();
            first.Dispose();
            Assert.ThrowsExactly<ObjectDisposedException>(() => first.DangerousGetHandle());
        }

        Assert.AreEqual(2, native.FreeCount);
    }

    [TestMethod]
    public void HipExceptionPreservesKnownAndUnknownNativeCodes()
    {
        using var native = new FakeHipNativeApi { MallocResult = (HipError)987654 };
        var runtime = new HipRuntime(native);

        HipException exception = Assert.ThrowsExactly<HipException>(() => runtime.Allocate(16));

        Assert.AreEqual(987654, exception.NativeErrorCode);
        Assert.AreEqual((HipError)987654, exception.Error);
        Assert.AreEqual("hipMalloc", exception.Operation);
        Assert.AreEqual("hipErrorUnknown", exception.ErrorName);
        StringAssert.Contains(exception.Message, "987654");
    }
}
