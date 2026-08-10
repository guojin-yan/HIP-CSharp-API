using System;
using System.Linq;
using System.Reflection;
using JYPPX.HipSharp.Loading;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Rtc;
using JYPPX.HipSharp.Types;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class AssemblyBaselineTests
{
    [TestMethod]
    public void CoreAssemblyExposesM4ApiAndMetadata()
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
                typeof(HipKernelArgument), typeof(HipLaunchDimensions),
                typeof(JYPPX.HipSharp.Streams.HipStream), typeof(JYPPX.HipSharp.Streams.HipEvent),
                typeof(HipStreamFlags), typeof(HipEventFlags), typeof(HipDeviceAttribute),
                typeof(HipPinnedMemory), typeof(HipTypedMemory<>),
            },
            managed.GetExportedTypes());
        Assert.AreEqual("M4-release-candidate", ReadMetadata(managed, "HipSharpStage"));
        Assert.AreEqual("true", ReadMetadata(managed, "HipApiImplemented"));
        Assert.AreEqual("eng/interop/interop-manifest.json", ReadMetadata(managed, "InteropSource"));
    }

    private static string? ReadMetadata(Assembly assembly, string key)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            .Value;
    }
}
