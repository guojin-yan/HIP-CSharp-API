using System;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class AssemblyBaselineTests
{
    [TestMethod]
    public void CoreAssembliesExposeM0MetadataWithoutPrematurePublicApi()
    {
        Assembly managed = Assembly.Load("JYPPX.HipSharp");
        Assembly native = Assembly.Load("JYPPX.HipSharp.Native");

        Assert.AreEqual("JYPPX.HipSharp", managed.GetName().Name);
        Assert.AreEqual("JYPPX.HipSharp.Native", native.GetName().Name);
        Assert.AreEqual(0, managed.GetExportedTypes().Length, "M0 must not freeze a public managed HIP API.");
        Assert.AreEqual(0, native.GetExportedTypes().Length, "M0 must not expose unverified native signatures.");
        Assert.AreEqual("M0-engineering-baseline", ReadMetadata(managed, "HipSharpStage"));
        Assert.AreEqual("false", ReadMetadata(managed, "HipApiImplemented"));
        Assert.AreEqual("eng/interop/interop-manifest.json", ReadMetadata(native, "InteropSource"));
    }

    private static string? ReadMetadata(Assembly assembly, string key)
    {
        return assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            .Single(attribute => string.Equals(attribute.Key, key, StringComparison.Ordinal))
            .Value;
    }
}
