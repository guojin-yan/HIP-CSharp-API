namespace JYPPX.ROCm.HipSharp.Graphs;

/// <summary>描述高层 graph node 类型 / Describes a high-level graph-node type.</summary>
public enum HipGraphNodeType
{
    /// <summary>Kernel 执行节点 / Kernel execution node.</summary>
    Kernel = 0,
    /// <summary>内存复制节点 / Memory-copy node.</summary>
    MemoryCopy = 1,
    /// <summary>内存设置节点 / Memory-set node.</summary>
    MemorySet = 2,
    /// <summary>空操作节点 / Empty no-op node.</summary>
    Empty = 5,
    /// <summary>Graph-local 内存分配节点 / Graph-local memory-allocation node.</summary>
    MemoryAllocation = 10,
    /// <summary>Graph-local 内存释放节点 / Graph-local memory-free node.</summary>
    MemoryFree = 11,
}
