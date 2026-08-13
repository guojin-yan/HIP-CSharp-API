namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 锁定 ROCm 7.2 Windows SDK 的原生文件布局 / Pins the native file layout of the ROCm 7.2 Windows SDK.
/// </summary>
internal static class HipWindowsSdkLayout
{
    internal const string RuntimeFileName = "amdhip64_7.dll";
    internal const string RtcFileName = "hiprtc0702.dll";

    internal static string FileName(HipNativeLibraryKind kind) =>
        kind == HipNativeLibraryKind.Runtime ? RuntimeFileName : RtcFileName;
}
