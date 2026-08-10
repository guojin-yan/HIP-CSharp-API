using System;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Rtc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.HipSharp.UnitTests;

[TestClass]
public sealed class HipRtcTests
{
    private static readonly string[] CompileOptions = { "--offload-arch=gfx1100", "-O2" };
    private static readonly string[] NullOption = { null! };
    private static readonly string[] NullCharacterOption = { "-O2\0ignored" };
    private static readonly string[] Utf8Options = { "--define=值", "-O2" };

    [TestMethod]
    public void VersionAndSuccessfulCompilationPreserveManagedSnapshots()
    {
        var native = new FakeHipRtcNativeApi();
        var rtc = new HipRtc(native);

        Assert.AreEqual(new HipRtcVersion(7, 2), rtc.GetVersion());
        using HipRtcProgram program = rtc.CreateProgram("extern \"C\" __global__ void add() {}", "vector-add.hip");
        HipRtcCompilation compilation = program.Compile(CompileOptions);

        Assert.AreEqual("vector-add.hip", native.LastName);
        CollectionAssert.AreEqual(CompileOptions, native.LastOptions.ToArray());
        Assert.AreEqual(native.Log, compilation.Log);
        Assert.AreEqual((ulong)native.Code.Length, compilation.CodeSize);
        Assert.AreEqual(64, compilation.CodeSha256.Length);
        byte[] firstCopy = compilation.GetCodeObject();
        firstCopy[0] = 0;
        CollectionAssert.AreEqual(native.Code, compilation.GetCodeObject());
    }

    [TestMethod]
    public void CompilationFailureIncludesUtf8CompilerLogAndUnknownResult()
    {
        var native = new FakeHipRtcNativeApi
        {
            CompileResult = (HipRtcResult)999,
            Log = "错误：expected expression / expected expression",
        };
        using HipRtcProgram program = new HipRtc(native).CreateProgram("broken", "broken.hip");

        HipRtcException exception = Assert.ThrowsExactly<HipRtcException>(() => program.Compile());

        Assert.AreEqual((HipRtcResult)999, exception.Result);
        Assert.AreEqual(native.Log, exception.CompilationLog);
        StringAssert.Contains(exception.Message, "999");
        StringAssert.Contains(exception.Message, "expected expression");
    }

    [TestMethod]
    public void ProgramDestroyRunsExactlyOnceAndCanRetryAfterFailure()
    {
        var native = new FakeHipRtcNativeApi { DestroyResult = HipRtcResult.InternalError };
        HipRtcProgram program = new HipRtc(native).CreateProgram("source");

        Assert.ThrowsExactly<HipRtcException>(() => program.Dispose());
        Assert.AreEqual(0, native.DestroyCount);

        native.DestroyResult = HipRtcResult.Success;
        program.Dispose();
        program.Dispose();
        Assert.AreEqual(1, native.DestroyCount);
    }

    [TestMethod]
    public void EmptyAndOversizedCodeObjectsAreRejected()
    {
        var emptyNative = new FakeHipRtcNativeApi { Code = Array.Empty<byte>() };
        using HipRtcProgram emptyProgram = new HipRtc(emptyNative).CreateProgram("source");
        Assert.ThrowsExactly<InvalidOperationException>(() => emptyProgram.Compile());

        if (UIntPtr.Size == 8)
        {
            var oversizedNative = new FakeHipRtcNativeApi { CodeSizeOverride = new UIntPtr(ulong.MaxValue) };
            using HipRtcProgram oversizedProgram = new HipRtc(oversizedNative).CreateProgram("source");
            Assert.ThrowsExactly<InvalidOperationException>(() => oversizedProgram.Compile());
        }
    }

    [TestMethod]
    public void EmptyLogIsAllowedAndLogRetrievalErrorsAreReported()
    {
        var emptyLogNative = new FakeHipRtcNativeApi { LogSizeOverride = UIntPtr.Zero };
        using HipRtcProgram emptyLogProgram = new HipRtc(emptyLogNative).CreateProgram("source");
        Assert.AreEqual(string.Empty, emptyLogProgram.Compile().Log);

        var failedLogNative = new FakeHipRtcNativeApi { LogResult = HipRtcResult.InternalError };
        using HipRtcProgram failedLogProgram = new HipRtc(failedLogNative).CreateProgram("source");
        HipRtcException exception = Assert.ThrowsExactly<HipRtcException>(() => failedLogProgram.Compile());
        Assert.AreEqual("hiprtcGetProgramLog", exception.Operation);
    }

    [TestMethod]
    public void CompileRejectsNullOptionsAndDisposedPrograms()
    {
        var native = new FakeHipRtcNativeApi();
        HipRtcProgram program = new HipRtc(native).CreateProgram("source");

        Assert.ThrowsExactly<ArgumentException>(() => program.Compile(NullOption));
        Assert.ThrowsExactly<ArgumentException>(() => program.Compile(NullCharacterOption));
        program.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => program.Compile());
    }

    [TestMethod]
    public void Utf8OptionArrayUsesPointersToNullTerminatedUtf8Strings()
    {
        using var native = new Utf8NativeStringArray(Utf8Options, "options");

        IntPtr first = Marshal.ReadIntPtr(native.Pointer, 0);
        IntPtr second = Marshal.ReadIntPtr(native.Pointer, IntPtr.Size);
        Assert.AreEqual("--define=值", Marshal.PtrToStringUTF8(first));
        Assert.AreEqual("-O2", Marshal.PtrToStringUTF8(second));
    }
}
