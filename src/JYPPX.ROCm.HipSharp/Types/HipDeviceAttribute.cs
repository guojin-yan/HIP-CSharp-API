namespace JYPPX.ROCm.HipSharp.Types;

/// <summary>
/// HIP device attributes exposed by the M3 ABI contract / M3 ABI 契约公开的 HIP 设备属性。
/// </summary>
public enum HipDeviceAttribute
{
    /// <summary>最大线程数 / Maximum resident threads per block.</summary>
    MaxThreadsPerBlock = 56,

    /// <summary>每个 block 的共享内存字节数 / Shared-memory bytes per block.</summary>
    MaxSharedMemoryPerBlock = 74,

    /// <summary>是否支持 CPU/GPU 并发访问托管内存 / Whether concurrent CPU/GPU managed-memory access is supported.</summary>
    ConcurrentManagedAccess = 9,

    /// <summary>是否支持本系统的托管内存分配 / Whether managed-memory allocation is supported on this system.</summary>
    ManagedMemory = 24,

    /// <summary>是否支持虚拟内存管理 / Whether virtual-memory management is supported.</summary>
    VirtualMemoryManagementSupported = 89,

    /// <summary>是否支持单设备 cooperative launch / Whether single-device cooperative launch is supported.</summary>
    CooperativeLaunch = 10,

    /// <summary>设备主时钟频率（kHz） / Device clock rate in kHz.</summary>
    ClockRate = 5,

    /// <summary>设备计算能力主版本 / Compute capability major version.</summary>
    ComputeCapabilityMajor = 23,

    /// <summary>设备计算能力次版本 / Compute capability minor version.</summary>
    ComputeCapabilityMinor = 61,

    /// <summary>设备的 multiprocessor 数量 / Number of multiprocessors on the device.</summary>
    MultiprocessorCount = 63,

    /// <summary>warp 宽度（线程数） / Warp width, in threads.</summary>
    WarpSize = 87,
}
