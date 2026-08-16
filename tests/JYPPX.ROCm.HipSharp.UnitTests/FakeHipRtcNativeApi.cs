using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Rtc;

namespace JYPPX.ROCm.HipSharp.UnitTests;

internal sealed class FakeHipRtcNativeApi : IHipRtcNativeApi, IDisposable
{
    private static readonly IntPtr Program = new(0x4000);
    private static readonly IntPtr LinkState = new(0x5000);
    private IntPtr _loweredNamePointer;
    private IntPtr _linkedCodePointer;
    private IntPtr _linkInputPointer;
    private int _linkInputSize;

    internal HipRtcResult CreateResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CompileResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult DestroyResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LogSizeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LogResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CodeSizeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CodeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult AddNameExpressionResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LoweredNameResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult BitcodeSizeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult BitcodeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LinkCreateResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LinkAddFileResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LinkAddDataResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LinkCompleteResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LinkDestroyResult { get; set; } = HipRtcResult.Success;

    internal UIntPtr? LogSizeOverride { get; set; }

    internal UIntPtr? CodeSizeOverride { get; set; }

    internal UIntPtr? BitcodeSizeOverride { get; set; }

    internal UIntPtr? LinkedCodeSizeOverride { get; set; }

    internal string Log { get; set; } = "编译成功 / compiled\n";

    internal byte[] Code { get; set; } = new byte[] { 0x7f, 0x45, 0x4c, 0x46 };

    internal byte[] Bitcode { get; set; } = new byte[] { 0x42, 0x43, 0xc0, 0xde };

    internal byte[] LinkedCode { get; set; } = new byte[] { 0x7f, 0x45, 0x4c, 0x46, 0x4c };

    internal byte[] LinkInputAtComplete { get; private set; } = Array.Empty<byte>();

    internal IList<string> NameExpressions { get; } = new List<string>();

    internal IList<string> LastOptions { get; } = new List<string>();

    internal string LastSource { get; private set; } = string.Empty;

    internal string LastName { get; private set; } = string.Empty;

    internal int DestroyCount { get; private set; }

    internal int LinkDestroyCount { get; private set; }

    internal HipRtcJitInputType LastLinkInputType { get; private set; }

    internal string LastLinkFilePath { get; private set; } = string.Empty;

    internal string? LastLinkInputName { get; private set; }

    internal string LoweredName { get; set; } = "_Z3foov";

    internal bool ReturnNullLinkState { get; set; }

    internal bool ReturnNullLoweredName { get; set; }

    internal bool ReturnNullLinkedCode { get; set; }

    public HipRtcResult Version(out int major, out int minor)
    {
        major = 7;
        minor = 2;
        return HipRtcResult.Success;
    }

    public string GetErrorString(HipRtcResult result) => "fake HIPRTC result " + (int)result;

    public HipRtcResult CreateProgram(string source, string name, out IntPtr program)
    {
        LastSource = source;
        LastName = name;
        program = CreateResult == HipRtcResult.Success ? Program : IntPtr.Zero;
        return CreateResult;
    }

    public HipRtcResult DestroyProgram(ref IntPtr program)
    {
        if (DestroyResult == HipRtcResult.Success)
        {
            DestroyCount++;
            program = IntPtr.Zero;
        }

        return DestroyResult;
    }

    public HipRtcResult CompileProgram(IntPtr program, IReadOnlyList<string> options)
    {
        LastOptions.Clear();
        foreach (string option in options)
        {
            LastOptions.Add(option);
        }

        return CompileResult;
    }

    public HipRtcResult AddNameExpression(IntPtr program, string nameExpression)
    {
        if (AddNameExpressionResult == HipRtcResult.Success)
        {
            NameExpressions.Add(nameExpression);
        }

        return AddNameExpressionResult;
    }

    public HipRtcResult GetLoweredName(IntPtr program, string nameExpression, out IntPtr loweredName)
    {
        FreeLoweredName();
        if (LoweredNameResult == HipRtcResult.Success && !ReturnNullLoweredName)
        {
            _loweredNamePointer = Marshal.StringToHGlobalAnsi(LoweredName);
        }

        loweredName = _loweredNamePointer;
        return LoweredNameResult;
    }

    public HipRtcResult GetProgramLogSize(IntPtr program, out UIntPtr logSize)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Log);
        logSize = LogSizeOverride ?? new UIntPtr((uint)(bytes.Length + 1));
        return LogSizeResult;
    }

    public HipRtcResult GetProgramLog(IntPtr program, IntPtr log)
    {
        if (LogResult != HipRtcResult.Success)
        {
            return LogResult;
        }

        byte[] bytes = Encoding.UTF8.GetBytes(Log + "\0");
        Marshal.Copy(bytes, 0, log, bytes.Length);
        return HipRtcResult.Success;
    }

    public HipRtcResult GetCodeSize(IntPtr program, out UIntPtr codeSize)
    {
        codeSize = CodeSizeOverride ?? new UIntPtr((uint)Code.Length);
        return CodeSizeResult;
    }

    public HipRtcResult GetCode(IntPtr program, IntPtr code)
    {
        if (CodeResult == HipRtcResult.Success)
        {
            Marshal.Copy(Code, 0, code, Code.Length);
        }

        return CodeResult;
    }

    public HipRtcResult GetBitcodeSize(IntPtr program, out UIntPtr bitcodeSize)
    {
        bitcodeSize = BitcodeSizeOverride ?? new UIntPtr((uint)Bitcode.Length);
        return BitcodeSizeResult;
    }

    public HipRtcResult GetBitcode(IntPtr program, IntPtr bitcode)
    {
        if (BitcodeResult == HipRtcResult.Success)
        {
            Marshal.Copy(Bitcode, 0, bitcode, Bitcode.Length);
        }

        return BitcodeResult;
    }

    public HipRtcResult LinkCreate(out IntPtr linkState)
    {
        linkState = LinkCreateResult == HipRtcResult.Success && !ReturnNullLinkState ? LinkState : IntPtr.Zero;
        return LinkCreateResult;
    }

    public HipRtcResult LinkAddFile(IntPtr linkState, HipRtcJitInputType inputType, string filePath)
    {
        LastLinkInputType = inputType;
        LastLinkFilePath = filePath;
        return LinkAddFileResult;
    }

    public HipRtcResult LinkAddData(IntPtr linkState, HipRtcJitInputType inputType, IntPtr image, UIntPtr imageSize, string? name)
    {
        LastLinkInputType = inputType;
        LastLinkInputName = name;
        if (LinkAddDataResult == HipRtcResult.Success)
        {
            _linkInputPointer = image;
            _linkInputSize = checked((int)imageSize.ToUInt64());
        }

        return LinkAddDataResult;
    }

    public HipRtcResult LinkComplete(IntPtr linkState, out IntPtr codeObject, out UIntPtr codeObjectSize)
    {
        if (_linkInputPointer != IntPtr.Zero)
        {
            LinkInputAtComplete = new byte[_linkInputSize];
            Marshal.Copy(_linkInputPointer, LinkInputAtComplete, 0, _linkInputSize);
        }

        FreeLinkedCode();
        if (LinkCompleteResult == HipRtcResult.Success && !ReturnNullLinkedCode && LinkedCode.Length > 0)
        {
            _linkedCodePointer = Marshal.AllocHGlobal(LinkedCode.Length);
            Marshal.Copy(LinkedCode, 0, _linkedCodePointer, LinkedCode.Length);
        }

        codeObject = _linkedCodePointer;
        codeObjectSize = LinkedCodeSizeOverride ?? new UIntPtr((uint)LinkedCode.Length);
        return LinkCompleteResult;
    }

    public HipRtcResult LinkDestroy(IntPtr linkState)
    {
        if (LinkDestroyResult == HipRtcResult.Success)
        {
            LinkDestroyCount++;
            _linkInputPointer = IntPtr.Zero;
            _linkInputSize = 0;
            FreeLinkedCode();
        }

        return LinkDestroyResult;
    }

    public void Dispose()
    {
        FreeLoweredName();
        FreeLinkedCode();
        GC.SuppressFinalize(this);
    }

    ~FakeHipRtcNativeApi()
    {
        FreeLoweredName();
        FreeLinkedCode();
    }

    private void FreeLoweredName()
    {
        if (_loweredNamePointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_loweredNamePointer);
            _loweredNamePointer = IntPtr.Zero;
        }
    }

    private void FreeLinkedCode()
    {
        if (_linkedCodePointer != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_linkedCodePointer);
            _linkedCodePointer = IntPtr.Zero;
        }
    }
}
