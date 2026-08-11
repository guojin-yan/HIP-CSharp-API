using JYPPX.HipSharp.Loading;

namespace JYPPX.HipSharp.Interop;

/// <summary>
/// 提供 HIP Runtime 7.2.1 完整公开 C ABI 的低层绑定 / Provides low-level bindings for the complete public HIP Runtime 7.2.1 C ABI.
/// </summary>
/// <remarks>
/// 此类型保留原生所有权规则。指针、回调和复杂结构指针以 <see cref="System.IntPtr"/> 暴露，调用方必须遵守官方 HIP 生命周期和缓冲区规则 /
/// This type preserves native ownership rules. Pointers, callbacks, and pointers to complex structures are exposed as <see cref="System.IntPtr"/>;
/// callers must follow the official HIP lifetime and buffer rules.
/// </remarks>
public sealed partial class HipRuntimeNativeApi
{
    /// <summary>
    /// 加载 HIP Runtime 原生库并创建低层 API 客户端 / Loads the HIP Runtime native library and creates a low-level API client.
    /// </summary>
    /// <param name="nativeLibraryPath">可选的绝对原生库路径 / Optional absolute path to the native library.</param>
    public HipRuntimeNativeApi(string? nativeLibraryPath = null)
    {
        HipImportResolver.EnsureLoaded(HipNativeLibraryKind.Runtime, nativeLibraryPath);
    }
}
