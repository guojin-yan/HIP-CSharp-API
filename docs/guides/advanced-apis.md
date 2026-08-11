# Advanced HIP APIs / HIP 高级 API

M6 adds a selected advanced surface without exposing raw native ownership. The declarations come from `eng/interop/interop-manifest.json`; the public owners are `HipAsyncDeviceMemory`, `HipManagedMemory`, `HipPeerAccess`, `HipGraph`, and `HipGraphExec`.

M6 增加经过选择的高级 API，同时不把原生所有权直接暴露给调用方。声明均来自 `eng/interop/interop-manifest.json`，公开 owner 为 `HipAsyncDeviceMemory`、`HipManagedMemory`、`HipPeerAccess`、`HipGraph` 和 `HipGraphExec`。

## Stream-ordered allocation / Stream 顺序分配

`HipRuntime.AllocateAsync` binds an allocation to one `HipStream`. Copies and kernel arguments using that owner are restricted to the allocation stream because this selected API does not expose cross-stream event dependencies. `HipAsyncDeviceMemory.Dispose` enqueues `hipFreeAsync` on that same stream; it does not imply immediate reclamation. The allocation owner must be disposed before its stream. `HipStream.Dispose` rejects a premature call while stream-ordered allocation owners remain alive, then synchronizes pending work before destroying the stream.

`HipRuntime.AllocateAsync` 将 allocation 绑定到一个 `HipStream`。由于本阶段选择的 API 没有暴露跨 stream event dependency，使用该 owner 的 copy 和 kernel argument 均限制在 allocation stream。`HipAsyncDeviceMemory.Dispose` 只在同一 stream 排入 `hipFreeAsync`，不代表内存立即回收。allocation owner 必须先于 stream 释放；仍有 owner 时调用 `HipStream.Dispose` 会被拒绝，之后 stream 会先同步 pending work 再销毁。

## Managed memory / 托管统一内存

`HipRuntime.AllocateManaged` creates CPU/GPU-visible memory. Host reads or writes are only valid after related GPU work has completed. `Advise` records a usage hint and neither synchronizes nor promises migration or performance. `PrefetchAsync` is ordered on the supplied stream and retains the allocation until that stream completes. Device `-1` denotes the CPU for advice/prefetch; other negative values are rejected.

`HipRuntime.AllocateManaged` 创建 CPU/GPU 可见内存。只有相关 GPU 工作完成后，host 读写才有效。`Advise` 只是使用提示，不执行同步，也不保证迁移或性能。`PrefetchAsync` 在指定 stream 上排序，并在 stream 完成前保留 allocation。advice/prefetch 中 device `-1` 表示 CPU，其他负值会被拒绝。

## Peer access / 设备间访问

P2P state is represented by an explicit ordered device pair. `EnablePeerAccess(accessingDevice, peerDevice)`, `CopyAsync`, and an owned disable all require `accessingDevice` to be current. A returned owner distinguishes unsupported, newly enabled, and already-enabled states. Disposal disables only access first enabled by that owner; it never revokes a pre-existing enable. `CopyAsync` retains both memory owners until the stream completes.

P2P 状态由显式、有方向的设备对表示。`EnablePeerAccess(accessingDevice, peerDevice)`、`CopyAsync` 和 owner 执行的 disable 都要求 `accessingDevice` 已成为当前设备。返回的 owner 区分 unsupported、newly enabled 和 already enabled；释放时只撤销自身首次启用的访问，不会撤销已有状态。`CopyAsync` 在 stream 完成前同时保留源、目标内存 owner。

## Graph capture / Graph 捕获

`HipRuntime.CaptureGraph` balances begin/end capture and returns an independent `HipGraph` owner. `Instantiate` returns an independent `HipGraphExec`; destroying the source graph does not transfer or destroy executable ownership. `Launch` retains the executable until the target stream completes. Graph handles do not own external memory, modules, or host buffers referenced by captured operations, so callers must keep those owners alive through every executable launch and its stream completion. Capture callbacks must only submit operations accepted by HIP capture rules and must not synchronize the captured stream.

`HipRuntime.CaptureGraph` 配对 begin/end capture 并返回独立 `HipGraph` owner。`Instantiate` 返回独立 `HipGraphExec`；销毁源 graph 不会转移或销毁 executable。`Launch` 在目标 stream 完成前保留 executable。graph handle 不拥有 captured operation 引用的外部 memory、module 或 host buffer，因此调用方必须让这些 owner 至少存活到每次 executable launch 及其 stream 完成。capture callback 只能提交 HIP capture 规则允许的操作，不得同步正在捕获的 stream。

## Availability and errors / 可用性与错误

These 15 advanced ABI entries are optional in the managed manifest so older runtimes can still load the core library. A missing native export is normalized to `HipError.NotSupported`; native capability failures retain their original `HipError` and operation in `HipException`. An API being present does not imply that every GPU, device pair, memory mode, or capture operation supports it.

这 15 个高级 ABI 入口在托管 manifest 中标记为 optional，使旧 runtime 仍可加载核心库。缺少原生导出时统一映射为 `HipError.NotSupported`；native capability 失败则在 `HipException` 中保留原始 `HipError` 和 operation。函数存在不代表每种 GPU、设备对、内存模式或 capture 操作都支持它。

See `samples/HipAdvancedFeatures` for CPU/GPU comparison, graph replay, managed-memory validation, 100 owner lifecycles, and a byte-verified P2P copy-or-skip path.
