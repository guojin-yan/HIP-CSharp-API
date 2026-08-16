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

    public HipRtcResult AddNameExpression(IntPtr program, string nameExpression)
    {
        using (var expression = new Utf8NativeString(nameExpression, nameof(nameExpression)))
        {
            return HipNativeMethods.RtcAddNameExpression(program, expression.Pointer);
        }
    }

    public HipRtcResult GetLoweredName(IntPtr program, string nameExpression, out IntPtr loweredName)
    {
        using (var expression = new Utf8NativeString(nameExpression, nameof(nameExpression)))
        {
            return HipNativeMethods.RtcGetLoweredName(program, expression.Pointer, out loweredName);
        }
    }

    public HipRtcResult GetProgramLogSize(IntPtr program, out UIntPtr logSize) =>
        HipNativeMethods.RtcGetProgramLogSize(program, out logSize);

    public HipRtcResult GetProgramLog(IntPtr program, IntPtr log) => HipNativeMethods.RtcGetProgramLog(program, log);

    public HipRtcResult GetCodeSize(IntPtr program, out UIntPtr codeSize) => HipNativeMethods.RtcGetCodeSize(program, out codeSize);

    public HipRtcResult GetCode(IntPtr program, IntPtr code) => HipNativeMethods.RtcGetCode(program, code);

    public HipRtcResult GetBitcodeSize(IntPtr program, out UIntPtr bitcodeSize) =>
        HipNativeMethods.RtcGetBitcodeSize(program, out bitcodeSize);

    public HipRtcResult GetBitcode(IntPtr program, IntPtr bitcode) => HipNativeMethods.RtcGetBitcode(program, bitcode);

    public HipRtcResult LinkCreate(out IntPtr linkState) =>
        HipNativeMethods.RtcLinkCreate(0, IntPtr.Zero, IntPtr.Zero, out linkState);

    public HipRtcResult LinkAddFile(IntPtr linkState, HipRtcJitInputType inputType, string filePath)
    {
        using (var path = new Utf8NativeString(filePath, nameof(filePath)))
        {
            return HipNativeMethods.RtcLinkAddFile(linkState, (int)inputType, path.Pointer, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public HipRtcResult LinkAddData(IntPtr linkState, HipRtcJitInputType inputType, IntPtr image, UIntPtr imageSize, string? name)
    {
        if (name is null)
        {
            return HipNativeMethods.RtcLinkAddData(linkState, (int)inputType, image, imageSize, IntPtr.Zero, 0, IntPtr.Zero, IntPtr.Zero);
        }

        using (var nativeName = new Utf8NativeString(name, nameof(name)))
        {
            return HipNativeMethods.RtcLinkAddData(linkState, (int)inputType, image, imageSize, nativeName.Pointer, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }

    public HipRtcResult LinkComplete(IntPtr linkState, out IntPtr codeObject, out UIntPtr codeObjectSize) =>
        HipNativeMethods.RtcLinkComplete(linkState, out codeObject, out codeObjectSize);

    public HipRtcResult LinkDestroy(IntPtr linkState) => HipNativeMethods.RtcLinkDestroy(linkState);
}
