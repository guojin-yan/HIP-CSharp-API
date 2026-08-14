# MIGraphX adapter pending-lease boundary / MIGraphX 适配器 pending 租约边界

The optional `JYPPX.ROCm.MIGraphX.CSharp.API.HIP.Interop` package submits native MIGraphX work to a HipSharp-owned stream and retains managed cleanup until that stream completes. HipSharp core remains independent of MIGraphXSharp and does not publish an unbounded raw-handle API.

可选 `JYPPX.ROCm.MIGraphX.CSharp.API.HIP.Interop` 包把原生 MIGraphX 工作提交到 HipSharp 拥有的 stream，并把托管清理保留到该 stream 完成。HipSharp core 继续独立于 MIGraphXSharp，也不公开无期限裸 handle API。

## Internal contract / 内部契约

`HipStream.EnqueuePending<TResult>` is internal and available only to the exact friend adapter assembly. Under the stream's existing lock it rejects disposed and graph-capturing streams, passes the borrowed stream pointer to a synchronous enqueue callback, registers a completion callback before releasing the lock, and returns only managed enqueue state.

`HipStream.EnqueuePending<TResult>` 是 internal，且只向精确 friend adapter 程序集开放。它在 stream 现有锁内拒绝已释放/capture 状态，把 borrowed stream pointer 交给同步 enqueue 回调，在释放锁前注册 completion callback，并且只返回托管 enqueue 状态。

The pending-list capacity and managed completion holder are allocated before native enqueue. The atomic lock interval then closes the race in which stream disposal or a managed allocation failure could otherwise occur after native enqueue but before completion registration. Existing `Query`, `Synchronize`, and `Dispose` behavior releases the callback after HIP establishes stream completion. Failed enqueue does not register pending work.

pending list 容量与托管 completion holder 均在 native enqueue 前分配。同一锁区间随后消除了“native enqueue 已成功、completion callback 尚未注册时 stream 被释放或发生托管分配失败”的竞态。现有 `Query`、`Synchronize` 与 `Dispose` 在 HIP 建立完成边界后释放 callback；enqueue 失败不会注册 pending work。

## Ownership and public surface / 所有权与公开接口

HipSharp owns and destroys the stream. The adapter owns its MIGraphX state and uses existing `HipDeviceMemory` SafeHandle reference counting for device-pointer lifetime. Neither core references the other. The only cross-repository access is `InternalsVisibleTo("JYPPX.ROCm.MIGraphX.CSharp.API.HIP.Interop")`.

HipSharp 拥有并销毁 stream。adapter 拥有 MIGraphX 状态，并复用 `HipDeviceMemory` 现有 SafeHandle 引用计数保护 device pointer 生命周期。两个 core 不互相引用；唯一跨仓库入口是精确的 `InternalsVisibleTo("JYPPX.ROCm.MIGraphX.CSharp.API.HIP.Interop")`。

No HipSharp public type or member is added. The contract does not expose `IntPtr`, `SafeHandle`, `IHipNativeApi`, `IHipPointerOwner`, or a reusable native lease. Graph capture remains explicitly unsupported by the adapter.

HipSharp 不新增公开类型或成员。该契约不公开 `IntPtr`、`SafeHandle`、`IHipNativeApi`、`IHipPointerOwner` 或可重复使用的 native lease；adapter 明确不支持 graph capture。

## Validation boundary / 验证边界

Unit tests execute NotReady-to-Success callback retention, idempotent Query, capture rejection, and failed-enqueue cleanup with `FakeHipNativeApi`. Cross-repository adapter tests use local fake HIP/MIGraphX libraries for early disposal and pointer leases. These tests do not establish official MIGraphX execution, GPU overlap, performance, or zero-copy behavior.

单元测试通过 `FakeHipNativeApi` 执行 NotReady 到 Success 的 callback 保活、幂等 Query、capture 拒绝和 enqueue 失败清理。跨仓库 adapter 测试使用本地 fake HIP/MIGraphX 库验证 early-dispose 与指针租约；它们不构成官方 MIGraphX、GPU overlap、性能或 zero-copy 证据。
