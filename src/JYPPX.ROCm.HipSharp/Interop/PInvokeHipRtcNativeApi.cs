using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Generated;
using JYPPX.ROCm.HipSharp.Loading;
using JYPPX.ROCm.HipSharp.Rtc;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 将 HIPRTC 语义边界连接到生成的 P/Invoke 声明 / Connects the HIPRTC semantic boundary to generated P/Invoke declarations.
/// </summary>
internal sealed class PInvokeHipRtcNativeApi : IHipRtcNativeApi
{
    internal PInvokeHipRtcNativeApi(string? explicitLibraryPath)
    {
        HipImportResolver.EnsureLoaded(HipNativeLibraryKind.Rtc, explicitLibraryPath);
    }

    public HipRtcResult Version(out int major, out int minor) => HipNativeMethods.RtcVersion(out major, out minor);

    public string GetErrorString(HipRtcResult result)
    {
        IntPtr pointer = HipNativeMethods.RtcGetErrorString(result);
        return pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;
    }

    public HipRtcResult CreateProgram(string source, string name, out IntPtr program)
    {
        using (var sourceString = new Utf8NativeString(source, nameof(source)))
        using (var nameString = new Utf8NativeString(name, nameof(name)))
        {
            return HipNativeMethods.RtcCreateProgram(
                out program,
                sourceString.Pointer,
                nameString.Pointer,
                0,
                IntPtr.Zero,
                IntPtr.Zero);
        }
    }

    public HipRtcResult DestroyProgram(ref IntPtr program) => HipNativeMethods.RtcDestroyProgram(ref program);

    public HipRtcResult CompileProgram(IntPtr program, IReadOnlyList<string> options)
    {
        using (var nativeOptions = new Utf8NativeStringArray(options, nameof(options)))
        {
            return HipNativeMethods.RtcCompileProgram(program, options.Count, nativeOptions.Pointer);
        }
    }

    public HipRtcResult GetProgramLogSize(IntPtr program, out UIntPtr logSize) =>
        HipNativeMethods.RtcGetProgramLogSize(program, out logSize);

    public HipRtcResult GetProgramLog(IntPtr program, IntPtr log) => HipNativeMethods.RtcGetProgramLog(program, log);

    public HipRtcResult GetCodeSize(IntPtr program, out UIntPtr codeSize) => HipNativeMethods.RtcGetCodeSize(program, out codeSize);

    public HipRtcResult GetCode(IntPtr program, IntPtr code) => HipNativeMethods.RtcGetCode(program, code);
}
