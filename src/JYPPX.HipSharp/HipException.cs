using System;
using System.Globalization;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp;

/// <summary>
/// 表示 HIP Runtime 返回的失败结果 / Represents a failure result returned by the HIP Runtime.
/// </summary>
public sealed class HipException : Exception
{
    internal HipException(HipError error, string operation, string errorName, string nativeMessage)
        : base(string.Format(CultureInfo.InvariantCulture, "HIP operation '{0}' failed with {1} ({2}): {3}", operation, errorName, (int)error, nativeMessage))
    {
        Error = error;
        Operation = operation;
        ErrorName = errorName;
        NativeMessage = nativeMessage;
    }

    /// <summary>获取原始 HIP 错误枚举值 / Gets the original HIP error enum value.</summary>
    public HipError Error { get; }

    /// <summary>获取原始 HIP 数字错误码 / Gets the original numeric HIP error code.</summary>
    public int NativeErrorCode => (int)Error;

    /// <summary>获取失败的托管操作名称 / Gets the managed operation that failed.</summary>
    public string Operation { get; }

    /// <summary>获取 HIP 返回的错误名称 / Gets the error name returned by HIP.</summary>
    public string ErrorName { get; }

    /// <summary>获取 HIP 返回的错误说明 / Gets the error description returned by HIP.</summary>
    public string NativeMessage { get; }

    internal static HipException Create(IHipNativeApi nativeApi, HipError error, string operation)
    {
        string errorName = ReadDiagnostic(nativeApi.GetErrorName, error, "hipError(" + (int)error + ")");
        string nativeMessage = ReadDiagnostic(nativeApi.GetErrorString, error, "No native error description is available.");
        return new HipException(error, operation, errorName, nativeMessage);
    }

    private static string ReadDiagnostic(Func<HipError, string> reader, HipError error, string fallback)
    {
        try
        {
            string value = reader(error);
            return string.IsNullOrWhiteSpace(value) ? fallback : value;
        }
        catch (Exception)
        {
            return fallback;
        }
    }
}
