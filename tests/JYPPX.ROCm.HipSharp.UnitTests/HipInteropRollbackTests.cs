using System;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA1861

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipInteropRollbackTests
{
    [TestMethod]
    public void FailedLastBorrowCleanupCanRetryThroughOwnerWithoutReleasingTwice()
    {
        int releases = 0;
        using var resource = new HipInteropResource(new IntPtr(1), _ =>
        {
            if (++releases == 1) throw new InvalidOperationException("Transient cleanup failure.");
        });
        using var borrow = resource.Acquire();
        resource.Dispose();
        Assert.ThrowsExactly<InvalidOperationException>(borrow.Dispose);
        resource.Dispose();
        Assert.AreEqual(2, releases);
        borrow.Dispose();
        resource.Dispose();
        Assert.AreEqual(2, releases);
    }

    [TestMethod]
    public void FailedSemaphoreSubmissionReturnsEveryBorrowEvenWhenEachDestroyFails()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        IntPtr storage = Marshal.AllocHGlobal(64);
        try
        {
            var descriptor = new HipNativeDescriptor(storage);
            using var first = runtime.AdvancedInterop.ImportExternalSemaphore(descriptor);
            using var second = runtime.AdvancedInterop.ImportExternalSemaphore(descriptor);
            native.BeforeAdvancedResult = operation =>
            {
                if (operation != "hipWaitExternalSemaphoresAsync") return;
                // Simulate owner disposal while a native submission holds its borrows.
                first.Dispose();
                second.Dispose();
                native.AdvancedResult = HipError.NotSupported;
            };
            Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.WaitExternalSemaphores(stream, new[] { first, second }, descriptor));
            native.BeforeAdvancedResult = null;
            native.AdvancedResult = HipError.Success;
            Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
            first.Dispose();
            second.Dispose();
            Assert.AreEqual(4, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
            stream.Synchronize();
            Assert.AreEqual(4, native.AdvancedCalls.Count(call => call == "hipDestroyExternalSemaphore"));
        }
        finally
        {
            native.BeforeAdvancedResult = null;
            native.AdvancedResult = HipError.Success;
            Marshal.FreeHGlobal(storage);
        }
    }

    [TestMethod]
    public void FailedGraphicsMapReturnsStreamOwnershipDespiteUnregisterFailure()
    {
        using var native = new FakeHipNativeApi();
        using var runtime = new HipRuntime(native);
        using var stream = runtime.CreateStream();
        using var resource = runtime.AdvancedInterop.OwnGraphicsResource(new IntPtr(1234));
        native.BeforeAdvancedResult = operation =>
        {
            if (operation != "hipGraphicsMapResources") return;
            resource.Dispose();
            native.AdvancedResult = HipError.NotSupported;
        };
        try
        {
            Assert.ThrowsExactly<HipException>(() => runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream));
        }
        finally
        {
            native.BeforeAdvancedResult = null;
            native.AdvancedResult = HipError.Success;
        }
        resource.Dispose();
        Assert.AreEqual(2, native.AdvancedCalls.Count(call => call == "hipGraphicsUnregisterResource"));
        stream.Dispose();
        Assert.IsTrue(stream.IsDisposed);
    }
}
