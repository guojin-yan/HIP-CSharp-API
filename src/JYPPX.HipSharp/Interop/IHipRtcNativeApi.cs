using System;
using System.Collections.Generic;
using JYPPX.HipSharp.Rtc;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 定义托管层使用的可替换 HIPRTC 边界 / Defines the replaceable HIPRTC boundary used by the managed layer.
/// </summary>
internal interface IHipRtcNativeApi
{
    public HipRtcResult Version(out int major, out int minor);

    public string GetErrorString(HipRtcResult result);

    public HipRtcResult CreateProgram(string source, string name, out IntPtr program);

    public HipRtcResult DestroyProgram(ref IntPtr program);

    public HipRtcResult CompileProgram(IntPtr program, IReadOnlyList<string> options);

    public HipRtcResult GetProgramLogSize(IntPtr program, out UIntPtr logSize);

    public HipRtcResult GetProgramLog(IntPtr program, IntPtr log);

    public HipRtcResult GetCodeSize(IntPtr program, out UIntPtr codeSize);

    public HipRtcResult GetCode(IntPtr program, IntPtr code);
}
