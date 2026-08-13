namespace JYPPX.ROCm.HipSharp.Loading;

/// <summary>
/// 表示带来源的原生库候选项 / Represents a native-library candidate with its source.
/// </summary>
internal sealed class HipLibraryCandidate
{
    internal HipLibraryCandidate(string value, string source)
    {
        Value = value;
        Source = source;
    }

    internal string Value { get; }

    internal string Source { get; }
}
