# Managed memory pools / 托管 Memory Pool

M8.3 adds a process-local managed owner for HIP memory pools. The normal workflow is:

```csharp
using var runtime = new HipRuntime();
using HipStream stream = runtime.CreateStream();
using HipMemoryPool pool = runtime.CreateMemoryPool(
    new HipMemoryPoolOptions(runtime.GetCurrentDevice())
    {
        ReleaseThresholdBytes = 64UL * 1024 * 1024,
        AllowOpportunisticReuse = true,
    });

pool.SetAccess(runtime.GetCurrentDevice(), HipMemoryPoolAccess.ReadWrite);
using HipPooledDeviceMemory memory = pool.AllocateAsync(16UL * 1024 * 1024, stream);
memory.CopyFromAsync(new byte[16 * 1024 * 1024]);
stream.Synchronize();
memory.Dispose();
stream.Synchronize();
pool.TrimTo(0);
```

M8.3 为 HIP memory pool 增加进程内托管 owner。普通用户只需要 `HipMemoryPoolOptions`、`HipMemoryPoolAccess`、`HipMemoryPoolStatistics`、`HipMemoryPool` 和 `HipPooledDeviceMemory`，不需要 `IntPtr`、`hipMemPoolProps*`、`hipMemAccessDesc*` 或整数 attribute。

`CreateMemoryPool` 返回 owned pool；`GetDefaultMemoryPool` 返回 Runtime-owned borrowed view，`GetCurrentMemoryPool` 返回可能指向 default 或 custom pool 的 borrowed view。borrowed view 的 `Dispose` 不会销毁 HIP pool，也不会延长 custom owner 生命周期。custom pool 可以通过 `UseAsCurrent()` 进入 `HipMemoryPoolCurrentScope`，scope 保活该 custom pool，释放时恢复 previous current pool；scope 必须按 LIFO 顺序释放。

`HipPooledDeviceMemory` 绑定创建 stream。`Dispose` 只提交同一 stream 上的 `hipFreeAsync`，并在 stream completion 前保留 pool child。pool 或 stream 在 allocation/free 完成前都会拒绝提前销毁；`Synchronize`、成功的 `Query` 或 `stream.Dispose` 会推进 pending lease。pool allocation 不会静默 fallback 为 `hipMallocAsync`，缺少 optional export 会转成 `HipException`/`HipError.NotSupported`。

`MaximumSizeBytes` 和 `ReleaseThresholdBytes` 使用 bytes，前者的零值让 HIP 选择系统相关上限；reuse policy 使用 native 32-bit boolean；`GetStatistics()` 返回 reserved/used current 和 high-watermark bytes。两个 high-watermark 只能通过显式 reset 方法归零。`SetAccess`/`GetAccess` 只接受托管 `HipDevice` 和 `None`/`ReadWrite` flags，并检查 runtime、device 和 count 归属。

跨进程 shareable handle/pointer import/export、IPC、Graph memory allocation/free nodes、virtual memory、external memory 和 raw pool handle 注入仍只保留在 low-level API；它们需要独立的 OS handle、安全和生命周期契约。

本轮验证为 managed-only Fake HIP、ABI layout/static source、15 TFM build、public API、DocFX 和 Core package 门禁。没有本轮 Radeon Cloud 授权，因此不能把这些结果表述为真实 symbol、Runtime 或 GPU 执行证据；Windows Runtime 仍为 disabled/unverified/static-only。
