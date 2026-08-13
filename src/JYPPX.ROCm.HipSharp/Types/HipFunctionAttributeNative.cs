namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// 标识受托管 kernel 属性聚合使用的原生 function 属性 / Identifies native function attributes used by the managed kernel aggregate.
/// </summary>
internal enum HipFunctionAttributeNative
{
    /// <summary>每个 block 最大线程数 / Maximum threads per block.</summary>
    MaxThreadsPerBlock = 0,

    /// <summary>静态共享内存字节数 / Static shared-memory bytes.</summary>
    SharedSizeBytes = 1,

    /// <summary>常量内存字节数 / Constant-memory bytes.</summary>
    ConstantSizeBytes = 2,

    /// <summary>每线程 local memory 字节数 / Local-memory bytes per thread.</summary>
    LocalSizeBytes = 3,

    /// <summary>每线程寄存器数 / Registers per thread.</summary>
    NumberOfRegisters = 4,

    /// <summary>kernel binary 版本 / Kernel binary version.</summary>
    BinaryVersion = 6,

    /// <summary>最大动态共享内存字节数 / Maximum dynamic shared-memory bytes.</summary>
    MaxDynamicSharedSizeBytes = 8,
}
