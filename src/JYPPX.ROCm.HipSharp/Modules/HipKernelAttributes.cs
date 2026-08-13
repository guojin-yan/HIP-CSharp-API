using System;

namespace JYPPX.ROCm.HipSharp.Modules;

/// <summary>
/// 表示 module kernel 的不可变资源属性 / Represents immutable resource attributes of a module kernel.
/// </summary>
public readonly struct HipKernelAttributes : IEquatable<HipKernelAttributes>
{
    /// <summary>创建 kernel 资源属性 / Creates kernel resource attributes.</summary>
    /// <param name="maximumThreadsPerBlock">每个 block 的最大线程数 / Maximum threads per block.</param>
    /// <param name="staticSharedMemoryBytes">每个 block 的静态共享内存字节数 / Static shared-memory bytes per block.</param>
    /// <param name="constantMemoryBytes">常量内存字节数 / Constant-memory bytes.</param>
    /// <param name="localMemoryBytesPerThread">每个线程的 local memory 字节数 / Local-memory bytes per thread.</param>
    /// <param name="registersPerThread">每个线程的寄存器数 / Registers per thread.</param>
    /// <param name="binaryVersion">kernel binary 版本 / Kernel binary version.</param>
    /// <param name="maximumDynamicSharedMemoryBytes">每个 block 的最大动态共享内存字节数 / Maximum dynamic shared-memory bytes per block.</param>
    /// <exception cref="ArgumentOutOfRangeException">线程数不是正数 / The thread count is not positive.</exception>
    public HipKernelAttributes(
        int maximumThreadsPerBlock,
        ulong staticSharedMemoryBytes,
        ulong constantMemoryBytes,
        ulong localMemoryBytesPerThread,
        int registersPerThread,
        int binaryVersion,
        ulong maximumDynamicSharedMemoryBytes)
    {
        if (maximumThreadsPerBlock <= 0) throw new ArgumentOutOfRangeException(nameof(maximumThreadsPerBlock));
        if (registersPerThread < 0) throw new ArgumentOutOfRangeException(nameof(registersPerThread));
        if (binaryVersion < 0) throw new ArgumentOutOfRangeException(nameof(binaryVersion));
        MaximumThreadsPerBlock = maximumThreadsPerBlock;
        StaticSharedMemoryBytes = staticSharedMemoryBytes;
        ConstantMemoryBytes = constantMemoryBytes;
        LocalMemoryBytesPerThread = localMemoryBytesPerThread;
        RegistersPerThread = registersPerThread;
        BinaryVersion = binaryVersion;
        MaximumDynamicSharedMemoryBytes = maximumDynamicSharedMemoryBytes;
    }

    /// <summary>获取每个 block 的最大线程数 / Gets maximum threads per block.</summary>
    public int MaximumThreadsPerBlock { get; }

    /// <summary>获取每个 block 的静态共享内存字节数 / Gets static shared-memory bytes per block.</summary>
    public ulong StaticSharedMemoryBytes { get; }

    /// <summary>获取常量内存字节数 / Gets constant-memory bytes.</summary>
    public ulong ConstantMemoryBytes { get; }

    /// <summary>获取每个线程的 local memory 字节数 / Gets local-memory bytes per thread.</summary>
    public ulong LocalMemoryBytesPerThread { get; }

    /// <summary>获取每个线程的寄存器数 / Gets registers per thread.</summary>
    public int RegistersPerThread { get; }

    /// <summary>获取 kernel binary 版本 / Gets the kernel binary version.</summary>
    public int BinaryVersion { get; }

    /// <summary>获取每个 block 的最大动态共享内存字节数 / Gets maximum dynamic shared-memory bytes per block.</summary>
    public ulong MaximumDynamicSharedMemoryBytes { get; }

    /// <summary>判断两个属性集合是否相等 / Determines whether two attribute sets are equal.</summary>
    public bool Equals(HipKernelAttributes other) =>
        MaximumThreadsPerBlock == other.MaximumThreadsPerBlock &&
        StaticSharedMemoryBytes == other.StaticSharedMemoryBytes &&
        ConstantMemoryBytes == other.ConstantMemoryBytes &&
        LocalMemoryBytesPerThread == other.LocalMemoryBytesPerThread &&
        RegistersPerThread == other.RegistersPerThread &&
        BinaryVersion == other.BinaryVersion &&
        MaximumDynamicSharedMemoryBytes == other.MaximumDynamicSharedMemoryBytes;

    /// <summary>判断对象是否表示相同属性 / Determines whether an object represents the same attributes.</summary>
    public override bool Equals(object? obj) => obj is HipKernelAttributes other && Equals(other);

    /// <summary>获取哈希码 / Gets the hash code.</summary>
    public override int GetHashCode()
    {
        int hash = MaximumThreadsPerBlock;
        hash = (hash * 397) ^ StaticSharedMemoryBytes.GetHashCode();
        hash = (hash * 397) ^ ConstantMemoryBytes.GetHashCode();
        hash = (hash * 397) ^ LocalMemoryBytesPerThread.GetHashCode();
        hash = (hash * 397) ^ RegistersPerThread;
        hash = (hash * 397) ^ BinaryVersion;
        return (hash * 397) ^ MaximumDynamicSharedMemoryBytes.GetHashCode();
    }

    /// <summary>判断两个属性集合是否相等 / Determines whether two attribute sets are equal.</summary>
    public static bool operator ==(HipKernelAttributes left, HipKernelAttributes right) => left.Equals(right);

    /// <summary>判断两个属性集合是否不相等 / Determines whether two attribute sets differ.</summary>
    public static bool operator !=(HipKernelAttributes left, HipKernelAttributes right) => !left.Equals(right);
}
