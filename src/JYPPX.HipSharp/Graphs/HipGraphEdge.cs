using System;

namespace JYPPX.HipSharp.Graphs;

/// <summary>表示 prerequisite 到 dependent 的 directed graph edge / Represents a directed graph edge from prerequisite to dependent.</summary>
public readonly struct HipGraphEdge : IEquatable<HipGraphEdge>
{
    /// <summary>创建 directed edge / Creates a directed edge.</summary>
    public HipGraphEdge(HipGraphNode prerequisite, HipGraphNode dependent)
    {
        Prerequisite = prerequisite ?? throw new ArgumentNullException(nameof(prerequisite));
        Dependent = dependent ?? throw new ArgumentNullException(nameof(dependent));
    }

    /// <summary>获取 prerequisite node / Gets the prerequisite node.</summary>
    public HipGraphNode Prerequisite { get; }

    /// <summary>获取 dependent node / Gets the dependent node.</summary>
    public HipGraphNode Dependent { get; }

    /// <summary>比较此 edge 与另一个 edge / Compares this edge with another edge.</summary>
    public bool Equals(HipGraphEdge other) => ReferenceEquals(Prerequisite, other.Prerequisite) && ReferenceEquals(Dependent, other.Dependent);

    /// <summary>比较此 edge 与另一个对象 / Compares this edge with another object.</summary>
    public override bool Equals(object? obj) => obj is HipGraphEdge other && Equals(other);

    /// <summary>获取 edge 的哈希码 / Gets the edge hash code.</summary>
    public override int GetHashCode() => ((Prerequisite?.GetHashCode() ?? 0) * 397) ^ (Dependent?.GetHashCode() ?? 0);

    /// <summary>比较两个 edge / Compares two edges.</summary>
    public static bool operator ==(HipGraphEdge left, HipGraphEdge right) => left.Equals(right);

    /// <summary>比较两个 edge / Compares two edges.</summary>
    public static bool operator !=(HipGraphEdge left, HipGraphEdge right) => !left.Equals(right);
}
