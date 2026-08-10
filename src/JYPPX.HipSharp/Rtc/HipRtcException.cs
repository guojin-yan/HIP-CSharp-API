using System;

namespace JYPPX.HipSharp.Rtc;

/// <summary>
/// 表示 HIPRTC 调用失败 / Represents a failed HIPRTC call.
/// </summary>
public sealed class HipRtcException : Exception
{
    internal HipRtcException(HipRtcResult result, string operation, string nativeDescription, string compilationLog)
        : base(BuildMessage(result, operation, nativeDescription, compilationLog))
    {
        Result = result;
        Operation = operation;
        NativeDescription = nativeDescription;
        CompilationLog = compilationLog;
    }

    /// <summary>获取 HIPRTC 返回码 / Gets the HIPRTC result code.</summary>
    public HipRtcResult Result { get; }

    /// <summary>获取失败操作名 / Gets the failed operation name.</summary>
    public string Operation { get; }

    /// <summary>获取 HIPRTC 对返回码的说明 / Gets HIPRTC's description of the result.</summary>
    public string NativeDescription { get; }

    /// <summary>获取编译日志；非编译错误时为空 / Gets the compilation log; empty for non-compilation errors.</summary>
    public string CompilationLog { get; }

    private static string BuildMessage(HipRtcResult result, string operation, string nativeDescription, string compilationLog)
    {
        string message = operation + " failed with " + result + " (" + (int)result + "): " + nativeDescription;
        return string.IsNullOrWhiteSpace(compilationLog) ? message : message + Environment.NewLine + compilationLog;
    }
}
