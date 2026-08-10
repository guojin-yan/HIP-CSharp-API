using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Rtc;

namespace JYPPX.HipSharp.UnitTests;

internal sealed class FakeHipRtcNativeApi : IHipRtcNativeApi
{
    private static readonly IntPtr Program = new(0x4000);

    internal HipRtcResult CreateResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CompileResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult DestroyResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LogSizeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult LogResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CodeSizeResult { get; set; } = HipRtcResult.Success;

    internal HipRtcResult CodeResult { get; set; } = HipRtcResult.Success;

    internal UIntPtr? LogSizeOverride { get; set; }

    internal UIntPtr? CodeSizeOverride { get; set; }

    internal string Log { get; set; } = "编译成功 / compiled\n";

    internal byte[] Code { get; set; } = new byte[] { 0x7f, 0x45, 0x4c, 0x46 };

    internal IList<string> LastOptions { get; } = new List<string>();

    internal string LastSource { get; private set; } = string.Empty;

    internal string LastName { get; private set; } = string.Empty;

    internal int DestroyCount { get; private set; }

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
}
