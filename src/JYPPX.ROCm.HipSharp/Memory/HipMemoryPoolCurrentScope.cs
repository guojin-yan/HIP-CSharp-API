using System;

namespace JYPPX.ROCm.HipSharp.Memory;

/// <summary>拥有一次 device current memory-pool 切换并在释放时恢复 previous pool / Owns a device current-memory-pool switch and restores the previous pool on disposal.</summary>
public sealed class HipMemoryPoolCurrentScope : IDisposable
{
    private readonly HipRuntime _runtime;
    private bool _disposed;

    internal HipMemoryPoolCurrentScope(HipRuntime runtime, HipMemoryPool pool, IntPtr previousHandle)
    {
        _runtime = runtime;
        Pool = pool;
        PreviousHandle = previousHandle;
    }

    /// <summary>获取 scope 设为 current 的 custom pool / Gets the custom pool made current by this scope.</summary>
    public HipMemoryPool Pool { get; }

    /// <summary>获取 scope 是否已恢复 previous pool / Gets whether the scope has restored the previous pool.</summary>
    public bool IsDisposed => _disposed;

    internal IntPtr PreviousHandle { get; }

    /// <summary>恢复 previous pool；原生失败时可重试 / Restores the previous pool; native failure can be retried.</summary>
    public void Dispose()
    {
        if (_disposed) return;
        _runtime.EndMemoryPoolCurrentScope(this);
        _disposed = true;
    }
}
