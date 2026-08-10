using System;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipModuleKernelTests
{
    [TestMethod]
    public void ModuleKernelLaunchMarshalsPointersToStableArgumentValues()
    {
        using var native = new FakeHipNativeApi();
        native.ExpectedKernelPointerArguments.Add(true);
        native.ExpectedKernelPointerArguments.Add(true);
        native.ExpectedKernelPointerArguments.Add(true);
        native.ExpectedKernelPointerArguments.Add(false);
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1, 2, 3 });
        HipKernel kernel = module.GetKernel("VectorAdd");
        using HipDeviceMemory a = runtime.Allocate(16);
        using HipDeviceMemory b = runtime.Allocate(16);
        using HipDeviceMemory c = runtime.Allocate(16);

        kernel.Launch(
            new HipLaunchDimensions(2),
            new HipLaunchDimensions(64),
            new[]
            {
                HipKernelArgument.DevicePointer(a),
                HipKernelArgument.DevicePointer(b),
                HipKernelArgument.DevicePointer(c),
                HipKernelArgument.Scalar32(4),
            });

        CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, native.LastModuleCodeObject);
        Assert.AreEqual("VectorAdd", kernel.Name);
        Assert.AreEqual(a.DangerousGetHandle().ToInt64(), native.LastKernelArgumentValues[0]);
        Assert.AreEqual(b.DangerousGetHandle().ToInt64(), native.LastKernelArgumentValues[1]);
        Assert.AreEqual(c.DangerousGetHandle().ToInt64(), native.LastKernelArgumentValues[2]);
        Assert.AreEqual(4L, native.LastKernelArgumentValues[3]);
        Assert.AreEqual(1, native.ModuleLaunchCount);
    }

    [TestMethod]
    public void ModuleUnloadsExactlyOnceAndKernelCannotOutliveIt()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        module.Dispose();
        module.Dispose();

        Assert.AreEqual(1, native.ModuleUnloadCount);
        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            kernel.Launch(new HipLaunchDimensions(1), new HipLaunchDimensions(1), Array.Empty<HipKernelArgument>()));
    }

    [TestMethod]
    public void ModuleUnloadCanRetryAfterNativeFailure()
    {
        using var native = new FakeHipNativeApi { ModuleUnloadResult = HipError.InvalidValue };
        HipModule module = new HipRuntime(native).LoadModule(new byte[] { 1 });

        Assert.ThrowsExactly<HipException>(() => module.Dispose());
        native.ModuleUnloadResult = HipError.Success;
        module.Dispose();
        Assert.AreEqual(1, native.ModuleUnloadCount);
    }

    [TestMethod]
    public void LaunchRejectsDisposedAndCrossRuntimeMemory()
    {
        using var firstNative = new FakeHipNativeApi();
        using var secondNative = new FakeHipNativeApi();
        var firstRuntime = new HipRuntime(firstNative);
        var secondRuntime = new HipRuntime(secondNative);
        using HipModule module = firstRuntime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");
        HipDeviceMemory disposed = firstRuntime.Allocate(4);
        disposed.Dispose();

        Assert.ThrowsExactly<ObjectDisposedException>(() => LaunchWithMemory(kernel, disposed));

        using HipDeviceMemory foreign = secondRuntime.Allocate(4);
        Assert.ThrowsExactly<ArgumentException>(() => LaunchWithMemory(kernel, foreign));
    }

    [TestMethod]
    public void LaunchValidatesDimensionsOverflowAndNativeErrors()
    {
        using var native = new FakeHipNativeApi();
        var runtime = new HipRuntime(native);
        using HipModule module = runtime.LoadModule(new byte[] { 1 });
        HipKernel kernel = module.GetKernel("kernel");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            kernel.Launch(default, new HipLaunchDimensions(1), Array.Empty<HipKernelArgument>()));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            kernel.Launch(
                new HipLaunchDimensions(uint.MaxValue, uint.MaxValue, uint.MaxValue),
                new HipLaunchDimensions(1),
                Array.Empty<HipKernelArgument>()));

        native.ModuleLaunchResult = HipError.InvalidValue;
        Assert.ThrowsExactly<HipException>(() =>
            kernel.Launch(new HipLaunchDimensions(1), new HipLaunchDimensions(1), Array.Empty<HipKernelArgument>()));
    }

    [TestMethod]
    public void SynchronizePropagatesNativeErrors()
    {
        using var native = new FakeHipNativeApi { SynchronizeResult = HipError.InvalidValue };
        var runtime = new HipRuntime(native);

        Assert.ThrowsExactly<HipException>(() => runtime.Synchronize());
    }

    private static void LaunchWithMemory(HipKernel kernel, HipDeviceMemory memory) =>
        kernel.Launch(
            new HipLaunchDimensions(1),
            new HipLaunchDimensions(1),
            new[] { HipKernelArgument.DevicePointer(memory) });
}
