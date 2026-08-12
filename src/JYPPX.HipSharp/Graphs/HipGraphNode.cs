using System;
using System.Collections.Generic;

namespace JYPPX.HipSharp.Graphs;

/// <summary>表示由一个 explicit graph 拥有的 borrowed node identity / Represents a borrowed node identity owned by an explicit graph.</summary>
public sealed class HipGraphNode
{
    private readonly HipGraph _graph;
    private readonly IntPtr _handle;

    internal HipGraphNode(HipGraph graph, IntPtr handle, HipGraphNodeType type)
    {
        _graph = graph;
        _handle = handle;
        Type = type;
    }

    /// <summary>获取 node 类型 / Gets the node type.</summary>
    public HipGraphNodeType Type { get; }

    /// <summary>获取拥有此 node 的 graph / Gets the graph that owns this node.</summary>
    public HipGraph Graph => _graph;

    /// <summary>获取 node 当前是否仍有效 / Gets whether the node is currently valid.</summary>
    public bool IsValid => !_graph.IsDisposed;

    /// <summary>获取此 node 的直接依赖快照 / Gets a snapshot of this node's direct dependencies.</summary>
    public IReadOnlyList<HipGraphNode> Dependencies => _graph.GetDependencies(this);

    internal IntPtr Handle => _handle;
}
