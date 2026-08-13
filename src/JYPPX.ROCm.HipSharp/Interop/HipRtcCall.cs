using JYPPX.ROCm.HipSharp.Rtc;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 将 HIPRTC 返回码统一转换为托管异常 / Converts HIPRTC result codes to managed exceptions consistently.
/// </summary>
internal static class HipRtcCall
{
    internal static void ThrowIfFailed(IHipRtcNativeApi nativeApi, HipRtcResult result, string operation, string compilationLog = "")
    {
        if (result != HipRtcResult.Success)
        {
            throw new HipRtcException(result, operation, nativeApi.GetErrorString(result), compilationLog);
        }
    }
}
