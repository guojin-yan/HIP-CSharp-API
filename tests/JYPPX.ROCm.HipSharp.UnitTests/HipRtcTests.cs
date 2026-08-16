using System;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Rtc;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace JYPPX.ROCm.HipSharp.UnitTests;

[TestClass]
public sealed class HipRtcTests
{
    private static readonly string[] CompileOptions = { "--offload-arch=gfx1100", "-O2" };
    private static readonly string[] NullOption = { null! };
    private static readonly string[] NullCharacterOption = { "-O2\0ignored" };
    private static readonly string[] Utf8Options = { "--define=值", "-O2" };
    private static readonly string[] NameExpressions = { "my_kernel<(int)3, float>" };
    private static readonly string[] BitcodeOptions = { "--gpu-architecture=gfx1100", "-fgpu-rdc" };

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

    [TestMethod]
    public void NameExpressionsAreValidatedAndLoweredNamesAreCopied()
    {
        using var native = new FakeHipRtcNativeApi { LoweredName = "_Z9my_kernelILi3EfEvPT0_" };
        using HipRtcProgram program = new HipRtc(native).CreateProgram("template<int N> __global__ void my_kernel(float*) {}", "names.hip");

        program.AddNameExpression("my_kernel<(int)3, float>");
        program.Compile();
        string loweredName = program.GetLoweredName("my_kernel<(int)3, float>");

        CollectionAssert.AreEqual(NameExpressions, native.NameExpressions.ToArray());
        Assert.AreEqual(native.LoweredName, loweredName);
        program.Dispose();
        Assert.AreEqual("_Z9my_kernelILi3EfEvPT0_", loweredName);
    }

    [TestMethod]
    public void SuccessfulCompileRejectsNewNameExpressionsBeforeNativeCall()
    {
        using var native = new FakeHipRtcNativeApi();
        using HipRtcProgram program = new HipRtc(native).CreateProgram("source");
        program.AddNameExpression("kernel<float>");
        int callsBeforeCompile = native.AddNameExpressionCallCount;

        program.Compile();

        Assert.ThrowsExactly<InvalidOperationException>(() => program.AddNameExpression("kernel<double>"));
        Assert.AreEqual(callsBeforeCompile, native.AddNameExpressionCallCount);
    }

    [TestMethod]
    public void SuccessfulCompileToBitcodeRejectsNewNameExpressionsBeforeNativeCall()
    {
        using var native = new FakeHipRtcNativeApi();
        using HipRtcProgram program = new HipRtc(native).CreateProgram("source");
        int callsBeforeCompile = native.AddNameExpressionCallCount;

        program.CompileToBitcode(BitcodeOptions);

        Assert.ThrowsExactly<InvalidOperationException>(() => program.AddNameExpression("kernel<double>"));
        Assert.AreEqual(callsBeforeCompile, native.AddNameExpressionCallCount);
    }

    [TestMethod]
    public void FailedCompilationDoesNotCloseNameExpressionRegistration()
    {
        using var native = new FakeHipRtcNativeApi { CompileResult = HipRtcResult.Compilation };
        using HipRtcProgram program = new HipRtc(native).CreateProgram("source");

        Assert.ThrowsExactly<HipRtcException>(() => program.Compile());
        int callsBeforeRegistration = native.AddNameExpressionCallCount;

        program.AddNameExpression("kernel<double>");

        Assert.AreEqual(callsBeforeRegistration + 1, native.AddNameExpressionCallCount);
    }

    [TestMethod]
    public void NameExpressionOperationsRejectInvalidDisposedAndNullNativeResults()
    {
        using var native = new FakeHipRtcNativeApi();
        HipRtcProgram program = new HipRtc(native).CreateProgram("source");

        Assert.ThrowsExactly<ArgumentNullException>(() => program.AddNameExpression(null!));
        Assert.ThrowsExactly<ArgumentException>(() => program.AddNameExpression(" "));
        Assert.ThrowsExactly<ArgumentException>(() => program.GetLoweredName("bad\0name"));

        native.ReturnNullLoweredName = true;
        Assert.ThrowsExactly<InvalidOperationException>(() => program.GetLoweredName("kernel<int>"));

        program.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(() => program.AddNameExpression("kernel<int>"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => program.GetLoweredName("kernel<int>"));
    }

    [TestMethod]
    public void CompileToBitcodeCopiesOutputAndPreservesCompilerOptions()
    {
        using var native = new FakeHipRtcNativeApi();
        using HipRtcProgram program = new HipRtc(native).CreateProgram("source");

        byte[] bitcode = program.CompileToBitcode(BitcodeOptions);
        CollectionAssert.AreEqual(native.Bitcode, bitcode);
        CollectionAssert.AreEqual(BitcodeOptions, native.LastOptions.ToArray());

        bitcode[0] = 0;
        Assert.AreEqual(0x42, native.Bitcode[0]);
    }

    [TestMethod]
    public void BitcodeRejectsEmptyOversizedAndNativeReadFailures()
    {
        using var emptyNative = new FakeHipRtcNativeApi { Bitcode = Array.Empty<byte>() };
        using HipRtcProgram emptyProgram = new HipRtc(emptyNative).CreateProgram("source");
        Assert.ThrowsExactly<InvalidOperationException>(() => emptyProgram.CompileToBitcode());

        using var failedNative = new FakeHipRtcNativeApi { BitcodeResult = HipRtcResult.InternalError };
        using HipRtcProgram failedProgram = new HipRtc(failedNative).CreateProgram("source");
        HipRtcException failure = Assert.ThrowsExactly<HipRtcException>(() => failedProgram.CompileToBitcode());
        Assert.AreEqual("hiprtcGetBitcode", failure.Operation);

        if (UIntPtr.Size == 8)
        {
            using var oversizedNative = new FakeHipRtcNativeApi { BitcodeSizeOverride = new UIntPtr(ulong.MaxValue) };
            using HipRtcProgram oversizedProgram = new HipRtc(oversizedNative).CreateProgram("source");
            Assert.ThrowsExactly<InvalidOperationException>(() => oversizedProgram.CompileToBitcode());
        }
    }

    [TestMethod]
    public void LinkerRetainsInputCopyAndReturnsIndependentCodeObject()
    {
        using var native = new FakeHipRtcNativeApi();
        var input = new byte[] { 0x42, 0x43, 0xc0, 0xde };
        byte[] expectedInput = input.ToArray();
        byte[] linked;

        using (HipRtcLinker linker = new HipRtc(native).CreateLinker())
        {
            linker.AddData(HipRtcJitInputType.LlvmBitcode, input, "vector-add.bc");
            input[0] = 0;
            linked = linker.Complete();

            CollectionAssert.AreEqual(expectedInput, native.LinkInputAtComplete);
            CollectionAssert.AreEqual(native.LinkedCode, linked);
            Assert.AreEqual(HipRtcJitInputType.LlvmBitcode, native.LastLinkInputType);
            Assert.AreEqual("vector-add.bc", native.LastLinkInputName);
            Assert.IsTrue(linker.IsCompleted);
            Assert.ThrowsExactly<InvalidOperationException>(() => linker.Complete());
            Assert.ThrowsExactly<InvalidOperationException>(() => linker.AddData(HipRtcJitInputType.LlvmBitcode, expectedInput));
        }

        Assert.AreEqual(1, native.LinkDestroyCount);
        CollectionAssert.AreEqual(new byte[] { 0x7f, 0x45, 0x4c, 0x46, 0x4c }, linked);
    }

    [TestMethod]
    public void LinkerAddFileAndOptionalDataNamePreserveParameters()
    {
        using var native = new FakeHipRtcNativeApi();
        using HipRtcLinker linker = new HipRtc(native).CreateLinker();

        linker.AddFile(HipRtcJitInputType.LlvmBundledBitcode, "inputs/kernel.bc");
        Assert.AreEqual(HipRtcJitInputType.LlvmBundledBitcode, native.LastLinkInputType);
        Assert.AreEqual("inputs/kernel.bc", native.LastLinkFilePath);

        linker.AddData(HipRtcJitInputType.SpirV, new byte[] { 3, 2, 35, 7 });
        Assert.AreEqual(HipRtcJitInputType.SpirV, native.LastLinkInputType);
        Assert.IsNull(native.LastLinkInputName);
    }

    [TestMethod]
    public void LinkerValidatesArgumentsAndDisposedStateBeforeNativeCalls()
    {
        using var native = new FakeHipRtcNativeApi();
        HipRtcLinker linker = new HipRtc(native).CreateLinker();

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => linker.AddData((HipRtcJitInputType)0, new byte[] { 1 }));
        Assert.ThrowsExactly<ArgumentNullException>(() => linker.AddData(HipRtcJitInputType.LlvmBitcode, null!));
        Assert.ThrowsExactly<ArgumentException>(() => linker.AddData(HipRtcJitInputType.LlvmBitcode, Array.Empty<byte>()));
        Assert.ThrowsExactly<ArgumentException>(() => linker.AddData(HipRtcJitInputType.LlvmBitcode, new byte[] { 1 }, " "));
        Assert.ThrowsExactly<ArgumentException>(() => linker.AddFile(HipRtcJitInputType.LlvmBitcode, "bad\0path"));

        linker.Dispose();
        Assert.IsTrue(linker.IsDisposed);
        Assert.ThrowsExactly<ObjectDisposedException>(() => linker.AddFile(HipRtcJitInputType.LlvmBitcode, "input.bc"));
        Assert.ThrowsExactly<ObjectDisposedException>(() => linker.Complete());
    }

    [TestMethod]
    public void LinkerDestroyRunsExactlyOnceAndCanRetryAfterFailure()
    {
        using var native = new FakeHipRtcNativeApi { LinkDestroyResult = HipRtcResult.InternalError };
        HipRtcLinker linker = new HipRtc(native).CreateLinker();
        var retainedInput = new byte[] { 1, 2, 3 };
        linker.AddData(HipRtcJitInputType.LlvmBitcode, retainedInput);

        Assert.ThrowsExactly<HipRtcException>(() => linker.Dispose());
        Assert.AreEqual(0, native.LinkDestroyCount);
        Assert.IsFalse(linker.IsDisposed);
        linker.Complete();
        CollectionAssert.AreEqual(retainedInput, native.LinkInputAtComplete);

        native.LinkDestroyResult = HipRtcResult.Success;
        linker.Dispose();
        linker.Dispose();
        Assert.AreEqual(1, native.LinkDestroyCount);
    }

    [TestMethod]
    public void LinkerRejectsNullHandlesAndInvalidCompleteOutputs()
    {
        using var nullStateNative = new FakeHipRtcNativeApi { ReturnNullLinkState = true };
        Assert.ThrowsExactly<InvalidOperationException>(() => new HipRtc(nullStateNative).CreateLinker());

        using var nullCodeNative = new FakeHipRtcNativeApi { ReturnNullLinkedCode = true };
        using HipRtcLinker nullCodeLinker = new HipRtc(nullCodeNative).CreateLinker();
        Assert.ThrowsExactly<InvalidOperationException>(() => nullCodeLinker.Complete());

        if (UIntPtr.Size == 8)
        {
            using var oversizedNative = new FakeHipRtcNativeApi { LinkedCodeSizeOverride = new UIntPtr(ulong.MaxValue) };
            using HipRtcLinker oversizedLinker = new HipRtc(oversizedNative).CreateLinker();
            Assert.ThrowsExactly<InvalidOperationException>(() => oversizedLinker.Complete());
        }
    }
}
