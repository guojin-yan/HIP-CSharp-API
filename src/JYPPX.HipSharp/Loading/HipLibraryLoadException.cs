using System;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 表示 HIP 原生库无法加载并携带完整探测诊断 / Represents failure to load the HIP native library and carries complete probe diagnostics.
/// </summary>
public sealed class HipLibraryLoadException : Exception
{
    internal HipLibraryLoadException(HipLibraryLoadDiagnostics diagnostics)
        : base("Unable to load the HIP Runtime native library. " + diagnostics)
    {
        Diagnostics = diagnostics;
    }

    /// <summary>获取加载诊断 / Gets the load diagnostics.</summary>
    public HipLibraryLoadDiagnostics Diagnostics { get; }
}
