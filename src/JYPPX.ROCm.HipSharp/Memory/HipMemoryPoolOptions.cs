using System;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>配置进程内 custom HIP memory pool / Configures a process-local custom HIP memory pool.</summary>
public sealed class HipMemoryPoolOptions
{
    /// <summary>创建使用指定 backing device 的配置 / Creates options for the specified backing device.</summary>
    /// <param name="device">pool allocation 所在设备 / Device on which pool allocations reside.</param>
    public HipMemoryPoolOptions(HipDevice device)
    {
        Device = device ?? throw new ArgumentNullException(nameof(device));
    }

    /// <summary>获取 backing device / Gets the backing device.</summary>
    public HipDevice Device { get; }

    /// <summary>获取或设置同步时尝试归还 OS 前保留的字节阈值 / Gets or sets the byte threshold retained before synchronization attempts to return memory to the OS.</summary>
    public ulong ReleaseThresholdBytes { get; set; }

    /// <summary>获取或设置 pool 的最大字节数；零表示由 HIP 选择系统相关上限 / Gets or sets the maximum pool size in bytes; zero lets HIP choose a system-dependent limit.</summary>
    public ulong MaximumSizeBytes { get; set; }

    /// <summary>获取或设置是否复用具有 event dependency 的异步释放 / Gets or sets whether asynchronously freed memory with an event dependency may be reused.</summary>
    public bool AllowEventDependencyReuse { get; set; } = true;

    /// <summary>获取或设置是否机会式复用已经完成的释放 / Gets or sets whether completed frees may be reused opportunistically.</summary>
    public bool AllowOpportunisticReuse { get; set; } = true;

    /// <summary>获取或设置是否允许 allocator 插入内部 stream dependency / Gets or sets whether the allocator may insert internal stream dependencies.</summary>
    public bool AllowInternalDependencyReuse { get; set; } = true;
}
