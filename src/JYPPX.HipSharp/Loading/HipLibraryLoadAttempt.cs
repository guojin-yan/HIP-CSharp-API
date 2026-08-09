using System;

namespace JYPPX.HipSharp.Loading;

/// <summary>
/// 描述一次 HIP 原生库加载尝试 / Describes one HIP native-library load attempt.
/// </summary>
public sealed class HipLibraryLoadAttempt
{
    internal HipLibraryLoadAttempt(string candidate, string source, bool succeeded, string detail)
    {
        Candidate = candidate ?? throw new ArgumentNullException(nameof(candidate));
        Source = source ?? throw new ArgumentNullException(nameof(source));
        Succeeded = succeeded;
        Detail = detail ?? string.Empty;
    }

    /// <summary>获取脱敏后的库名或路径 / Gets the redacted library name or path.</summary>
    public string Candidate { get; }

    /// <summary>获取候选项来源 / Gets the source of the candidate.</summary>
    public string Source { get; }

    /// <summary>获取加载是否成功 / Gets whether loading succeeded.</summary>
    public bool Succeeded { get; }

    /// <summary>获取原生加载器返回的诊断摘要 / Gets the diagnostic summary returned by the native loader.</summary>
    public string Detail { get; }
}
