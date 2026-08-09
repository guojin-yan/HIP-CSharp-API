using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 将 HIP 返回码统一转换为托管异常 / Converts HIP return codes to managed exceptions consistently.
/// </summary>
internal static class HipCall
{
    internal static void ThrowIfFailed(IHipNativeApi nativeApi, HipError error, string operation)
    {
        if (error != HipError.Success)
        {
            throw HipException.Create(nativeApi, error, operation);
        }
    }
}
