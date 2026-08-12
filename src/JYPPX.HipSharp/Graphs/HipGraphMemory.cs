using System;

namespace JYPPX.HipSharp.Graphs;

/// <summary>表示只能由同一 graph 的 nodes 使用的 graph-local memory reference / Represents graph-local memory usable only by nodes in the same graph.</summary>
public sealed class HipGraphMemory
{
    private readonly HipGraph _graph;
    private readonly IntPtr _pointer;

    internal HipGraphMemory(HipGraph graph, HipGraphNode allocationNode, IntPtr pointer, ulong byteLength, HipDevice device)
    {
        _graph = graph;
        AllocationNode = allocationNode;
        _pointer = pointer;
        ByteLength = byteLength;
        Device = device;
    }

    /// <summary>获取 allocation node / Gets the allocation node.</summary>
    public HipGraphNode AllocationNode { get; }

    /// <summary>获取请求的字节数 / Gets the requested byte count.</summary>
    public ulong ByteLength { get; }

    /// <summary>获取 allocation 所在设备 / Gets the device where the allocation resides.</summary>
    public HipDevice Device { get; }

    /// <summary>获取 graph-local reference 是否仍有效 / Gets whether the graph-local reference remains valid.</summary>
    public bool IsValid => !_graph.IsDisposed;

    internal HipGraph Graph => _graph;
    internal IntPtr Pointer => _pointer;
}
