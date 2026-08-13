using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 汇总 HIP 原生库加载环境和尝试结果 / Summarizes the environment and attempts used to load the HIP native library.
/// </summary>
public sealed class HipLibraryLoadDiagnostics
{
    internal HipLibraryLoadDiagnostics(
        string operatingSystem,
        string processArchitecture,
        string targetFramework,
        string runtimeIdentifier,
        string libraryName,
        IList<HipLibraryLoadAttempt> attempts)
    {
        OperatingSystem = operatingSystem;
        ProcessArchitecture = processArchitecture;
        TargetFramework = targetFramework;
        RuntimeIdentifier = runtimeIdentifier;
        LibraryName = libraryName;
        Attempts = new ReadOnlyCollection<HipLibraryLoadAttempt>(attempts ?? throw new ArgumentNullException(nameof(attempts)));
    }

    /// <summary>获取操作系统说明 / Gets the operating-system description.</summary>
    public string OperatingSystem { get; }

    /// <summary>获取当前进程架构 / Gets the current process architecture.</summary>
    public string ProcessArchitecture { get; }

    /// <summary>获取当前目标框架名称 / Gets the current target-framework name.</summary>
    public string TargetFramework { get; }

    /// <summary>获取用于探测 runtime 资源的 RID / Gets the RID used to probe runtime assets.</summary>
    public string RuntimeIdentifier { get; }

    /// <summary>获取本次加载的逻辑库名 / Gets the logical library name being loaded.</summary>
    public string LibraryName { get; }

    /// <summary>获取按顺序执行的加载尝试 / Gets the load attempts in execution order.</summary>
    public IReadOnlyList<HipLibraryLoadAttempt> Attempts { get; }

    /// <summary>获取单行加载环境摘要 / Gets a single-line summary of the load environment.</summary>
    /// <returns>加载环境摘要 / Load-environment summary.</returns>
    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "Library={0}; OS={1}; Architecture={2}; TFM={3}; RID={4}; Attempts={5}", LibraryName, OperatingSystem, ProcessArchitecture, TargetFramework, RuntimeIdentifier, Attempts.Count);
}
