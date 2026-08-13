namespace JYPPX.ROCm.HipSharp.Graphs;

/// <summary>描述 graph 的创建方式 / Describes how a graph was created.</summary>
public enum HipGraphKind
{
    /// <summary>由 stream capture 创建 / Created by stream capture.</summary>
    Captured = 0,
    /// <summary>由高层 builder 显式创建 / Created explicitly by the high-level builder.</summary>
    Explicit = 1,
}
