using System;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Loading;
using JYPPX.HipSharp.Graphs;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Peer;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Rtc;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class AssemblyBaselineTests
{
    [TestMethod]
    public void CoreAssemblyExposesM6ApiAndMetadata()
    {
        Assembly managed = Assembly.Load("JYPPX.HipSharp");

        Assert.AreEqual("JYPPX.HipSharp", managed.GetName().Name);
        CollectionAssert.IsSubsetOf(
            new[]
            {
                typeof(HipRuntime), typeof(HipDevice), typeof(HipException), typeof(HipDeviceMemory),
                typeof(HipError), typeof(HipVersion), typeof(HipRuntimeVersionInfo), typeof(HipDeviceInfo),
                typeof(HipLibraryLoadException), typeof(HipLibraryLoadDiagnostics), typeof(HipLibraryLoadAttempt),
                typeof(HipRtc), typeof(HipRtcProgram), typeof(HipRtcCompilation), typeof(HipRtcResult),
                typeof(HipRtcException), typeof(HipRtcVersion), typeof(HipModule), typeof(HipKernel),
                typeof(HipKernelArgument), typeof(HipLaunchDimensions), typeof(HipKernelAttributes),
                typeof(HipOccupancyFlags), typeof(HipOccupancyInfo), typeof(HipOccupancyPlan),
                typeof(JYPPX.HipSharp.Streams.HipStream), typeof(JYPPX.HipSharp.Streams.HipEvent),
                typeof(HipStreamFlags), typeof(HipEventFlags), typeof(HipDeviceAttribute),
                typeof(HipPinnedMemory), typeof(HipTypedMemory<>),
                typeof(HipAsyncDeviceMemory), typeof(HipManagedMemory), typeof(HipMemoryAdvise),
                typeof(HipManagedMemoryFlags), typeof(HipStreamCaptureMode), typeof(HipGraph),
                typeof(HipGraphExec), typeof(HipPeerAccess),
                typeof(HipRuntimeNativeApi), typeof(HipRtcNativeApi), typeof(HipDim3), typeof(HipExtent),
                typeof(HipPitchedPtr), typeof(HipMemLocation), typeof(HipIpcMemHandle), typeof(HipIpcEventHandle),
                typeof(HipMemoryPool), typeof(HipMemoryPoolOptions), typeof(HipMemoryPoolAccess),
                typeof(HipMemoryPoolAccessDescriptor), typeof(HipMemoryPoolStatistics),
                typeof(HipMemoryPoolCurrentScope), typeof(HipPooledDeviceMemory),
            },
            managed.GetExportedTypes());
        Assert.AreEqual(459, typeof(HipRuntimeNativeApi).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
        Assert.AreEqual(18, typeof(HipRtcNativeApi).GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly).Length);
        Assert.AreEqual("M6-advanced-api-static-windows", ReadMetadata(managed, "HipSharpStage"));
        Assert.AreEqual("true", ReadMetadata(managed, "HipApiImplemented"));
        Assert.AreEqual("eng/interop/interop-manifest.json", ReadMetadata(managed, "InteropSource"));
        Assert.AreEqual("eng/interop/complete-api-model.json", ReadMetadata(managed, "CompleteInteropSource"));
    }

    [TestMethod]
    public void CompleteNativeApiByValueTypesMatchTheX64Abi()
    {
        Assert.AreEqual(12, Marshal.SizeOf<HipDim3>());
        Assert.AreEqual(24, Marshal.SizeOf<HipExtent>());
        Assert.AreEqual(32, Marshal.SizeOf<HipPitchedPtr>());
        Assert.AreEqual(24, Marshal.SizeOf<HipPos>());
        Assert.AreEqual(160, Marshal.SizeOf<HipMemcpy3DParameters>());
        Assert.AreEqual(8, Marshal.SizeOf<HipMemLocation>());
        Assert.AreEqual(64, Marshal.SizeOf<HipIpcMemHandle>());
        Assert.AreEqual(64, Marshal.SizeOf<HipIpcEventHandle>());
        Assert.AreEqual(88, Marshal.SizeOf<HipMemoryPoolPropertiesNative>());
        Assert.AreEqual(12, Marshal.SizeOf<HipMemoryPoolAccessDescriptorNative>());
    }

    private static string? ReadMetadata(Assembly assembly, string key)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            .Value;
    }
}
