using System;
using System.Collections.Generic;
using JYPPX.ROCm.HipSharp.Rtc;

namespace JYPPX.ROCm.HipSharp.Interop;

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

    public HipRtcResult AddNameExpression(IntPtr program, string nameExpression);

    public HipRtcResult GetLoweredName(IntPtr program, string nameExpression, out IntPtr loweredName);

    public HipRtcResult GetProgramLogSize(IntPtr program, out UIntPtr logSize);

    public HipRtcResult GetProgramLog(IntPtr program, IntPtr log);

    public HipRtcResult GetCodeSize(IntPtr program, out UIntPtr codeSize);

    public HipRtcResult GetCode(IntPtr program, IntPtr code);

    public HipRtcResult GetBitcodeSize(IntPtr program, out UIntPtr bitcodeSize);

    public HipRtcResult GetBitcode(IntPtr program, IntPtr bitcode);

    public HipRtcResult LinkCreate(out IntPtr linkState);

    public HipRtcResult LinkAddFile(IntPtr linkState, HipRtcJitInputType inputType, string filePath);

    public HipRtcResult LinkAddData(IntPtr linkState, HipRtcJitInputType inputType, IntPtr image, UIntPtr imageSize, string? name);

    public HipRtcResult LinkComplete(IntPtr linkState, out IntPtr codeObject, out UIntPtr codeObjectSize);

    public HipRtcResult LinkDestroy(IntPtr linkState);
}
