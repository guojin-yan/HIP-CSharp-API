namespace JYPPX.HipSharp.Types;

/// <summary>
/// 定义 HIP 原生库的逻辑名称 / Defines logical names for HIP native libraries.
/// </summary>
internal static class HipNativeLibraryNames
{
    /// <summary>
    /// HIP Runtime 逻辑库名 / Logical library name for the HIP Runtime.
    /// </summary>
    internal const string Runtime = "amdhip64";

    /// <summary>
    /// P/Invoke 使用的 HIP Runtime 库名 / HIP Runtime library name used by P/Invoke.
    /// </summary>
#if NETFRAMEWORK
    internal const string RuntimeImportName = "amdhip64_7.dll";
#else
    internal const string RuntimeImportName = Runtime;
#endif

    /// <summary>
    /// HIPRTC 逻辑库名 / Logical library name for HIPRTC.
    /// </summary>
    internal const string Rtc = "hiprtc";
}
