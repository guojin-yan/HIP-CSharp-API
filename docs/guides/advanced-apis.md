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

P2P state is represented by an explicit ordered device pair. `EnablePeerAccess(accessingDevice, peerDevice)`, `CopyAsync`, and an owned disable all require `accessingDevice` to be current. Device allocations and streams record their creation-device ordinal; `CopyAsync` derives native ordinals from those owners, rejects allocations outside the pair, and requires a stream created on `accessingDevice`. A returned owner distinguishes unsupported, newly enabled, and already-enabled states. Disposal disables only access first enabled by that owner; it never revokes a pre-existing enable. `CopyAsync` retains both memory owners until the stream completes.

P2P 状态由显式、有方向的设备对表示。`EnablePeerAccess(accessingDevice, peerDevice)`、`CopyAsync` 和 owner 执行的 disable 都要求 `accessingDevice` 已成为当前设备。设备分配与 stream 会记录创建时的设备序号；`CopyAsync` 从 owner 读取原生 ordinal，拒绝设备对之外的 allocation，并要求 stream 创建于 `accessingDevice`。返回的 owner 区分 unsupported、newly enabled 和 already enabled；释放时只撤销自身首次启用的访问，不会撤销已有状态。`CopyAsync` 在 stream 完成前同时保留源、目标内存 owner。

## Graph capture / Graph 捕获

`HipRuntime.CaptureGraph` balances begin/end capture and returns an independent `HipGraph` owner. Resources referenced by captured wrapper operations, including device memory, modules, managed arrays, and pinned buffers, are retained automatically. The graph and every executable share those leases; the last owner releases them. `Instantiate` returns an independent `HipGraphExec`, so destroying the source graph does not destroy resources still needed by an executable. `Launch` also retains the executable until the target stream completes. Capture callbacks must only submit operations accepted by HIP capture rules and must not synchronize the captured stream. Native pointers passed outside these wrapper operations remain caller-owned.

`HipRuntime.CaptureGraph` 配对 begin/end capture 并返回独立 `HipGraph` owner。通过封装层操作引用的 device memory、module、托管数组与 pinned buffer 会自动保活；graph 与每个 executable 共享这些 lease，并由最后一个 owner 释放。`Instantiate` 返回独立 `HipGraphExec`，所以销毁源 graph 不会销毁 executable 仍需要的资源。`Launch` 还会在目标 stream 完成前保留 executable。capture callback 只能提交 HIP capture 规则允许的操作，不得同步正在捕获的 stream；绕过封装层直接传入的 native pointer 仍由调用方管理。

## Availability and errors / 可用性与错误

These 15 advanced ABI entries are optional in the managed manifest so older runtimes can still load the core library. A missing native export is normalized to `HipError.NotSupported`; native capability failures retain their original `HipError` and operation in `HipException`. An API being present does not imply that every GPU, device pair, memory mode, or capture operation supports it.

这 15 个高级 ABI 入口在托管 manifest 中标记为 optional，使旧 runtime 仍可加载核心库。缺少原生导出时统一映射为 `HipError.NotSupported`；native capability 失败则在 `HipException` 中保留原始 `HipError` 和 operation。函数存在不代表每种 GPU、设备对、内存模式或 capture 操作都支持它。

See the module-oriented tutorials under `samples/tutorials` for focused examples. The former combined workload now lives at `samples/validation/AdvancedReliabilityStress` and retains CPU/GPU comparison, graph replay, managed-memory validation, repeated owner lifecycles, P2P copy-or-skip, and the optional multi-stream reliability mode. Its output intentionally contains no timing or performance claim.

## Advanced interop ownership / 高级互操作所有权

`HipRuntime.AdvancedInterop` imports external memory/semaphores and owns graphics/IPC resources. Disposal blocks new use immediately; a failed native cleanup retains the handle so `Dispose` can be retried. Mapped buffers/mipmaps retain their parent import. Asynchronous semaphore operations retain every semaphore until stream completion and reject foreign Runtime clients and graph capture. Graphics mappings retain their registered resources and stream; dispose the mapping before its stream. Unmapping retains resources until the stream completes, including cleanup retry after a native failure.

`HipRuntime.AdvancedInterop` 导入外部 memory/semaphore，并管理 graphics/IPC 资源。请求释放后立即禁止新使用；原生清理失败时保留句柄，可重试 `Dispose`。映射 buffer/mipmap 保活父导入。异步 semaphore 操作在 stream 完成前保活全部 semaphore，并拒绝其他 Runtime client 的资源及 graph capture。graphics mapping 保活注册资源和 stream，必须先释放 mapping 再释放 stream；取消映射后仍保活资源至 stream 完成，原生清理失败可重试。

IPC exports borrow the original memory/event owner; keep that owner alive until all importers finish. Imported memory closes with `hipIpcCloseMemHandle`, and an imported event synchronizes and destroys its own event handle. The local fake-native regressions check both ends of the 64-byte opaque value, all import bytes/flags, owner identity, null-success rejection, native error identity, and retry after synchronization/destruction failure. The separate Linux KFD fixture executes the raw IPC bindings across processes; it is not GPU proof of every managed wrapper or every error path. Its reviewed package identities and logs are pinned in `eng/interface-coverage/reviewed-function-evidence.json`.

IPC 导出借用原 memory/event owner；全部导入方完成前须保持原 owner 存活。导入内存使用 `hipIpcCloseMemHandle` 关闭，导入事件同步后销毁自己的事件句柄。本机 fake-native 回归验证 64 字节句柄首尾、全部导入字节与 flags、owner 身份、空成功结果拒绝、原生错误身份及同步/销毁失败重试。Linux KFD 的独立夹具验证 raw IPC 的跨进程执行；托管包装的全部 GPU 行为和错误路径仍须单独举证。

Advanced graph nodes join the same topology, dependency, sealing, node-kind and pending-launch checks as ordinary nodes. Descriptor storage and all resources embedded in `HipNativeDescriptor` remain caller-owned: graph parameter calls do not inspect descriptors or automatically retain embedded handles. Keep those resources alive through graph/executable use and every submitted launch. The managed `RetainUserObject` accepts flags zero only; ownership-moving flags require the low-level API. A `HipUserObject` releases all its initial and additional local references on disposal; graph-owned references follow native graph lifetime.

高级 graph node 与普通 node 共享拓扑、依赖、封存、node 类型及 pending launch 检查。`HipNativeDescriptor` 的存储及其中嵌入的资源仍由调用方拥有：graph 参数接口不解析 descriptor，也不自动保活其中的句柄。必须在 graph/executable 使用期间及所有已提交 launch 完成前保留这些资源。托管 `RetainUserObject` 只接受零 flags，所有权移动需使用底层 API。`HipUserObject.Dispose` 释放全部初始及额外的本地引用；graph 所拥有的引用遵循原生 graph 生命周期。

All managed descriptor consumers reject `default(HipNativeDescriptor)` before calling native HIP, including graph parameter updates and external mappings. This only checks for a null address; the caller remains responsible for ABI layout, buffer length and lifetime. Graphics queries reject native success with a null mapped pointer/array while keeping the mapping available for retry.

所有使用 descriptor 的托管入口均在调用原生 HIP 前拒绝 `default(HipNativeDescriptor)`，包括 graph 参数更新和外部映射。这只检查空地址，ABI 布局、缓冲区长度及生命周期仍由调用方负责。graphics 查询会拒绝“原生成功但返回空 pointer/array”的结果，mapping 仍可重试。

`AddStreamCallback` rejects a disposed Runtime, foreign/disposed streams and graph capture. Registration is serialized with stream disposal/capture; the callback remains rooted until native invocation and user exceptions do not escape into HIP. Do not call HIP APIs from a HIP stream callback. Driver error queries through a retained facade also reject a disposed Runtime.

`AddStreamCallback` 拒绝已释放 Runtime、其他 Runtime 的 stream、已释放 stream 及 graph capture；注册与 stream 释放/capture 通过同一锁串行执行。回调保活至原生调用，用户异常不会逃逸至 HIP。不要在 HIP stream callback 中调用 HIP API。保留的高级门面在 Runtime 释放后也会拒绝 driver error 查询。

Use `eng/test-local-runtime-package.ps1 -InteropBoundaries` for an additional exact-package native callback/driver-diagnostic and managed rejection workload on either platform. Its extended summary is separate from the fixed standard candidate matrix and cannot be used directly by `release-candidate.py seal`. It does not validate external memory/semaphore or graphics resources.

两端均可使用 `eng/test-local-runtime-package.ps1 -InteropBoundaries`，追加精确包的原生 callback/driver 诊断和托管拒绝路径工作负载。其扩展 summary 与固定标准候选矩阵分开，不能直接用于 `release-candidate.py seal`，也不代表完成 external memory/semaphore 或 graphics 有效资源验证。

Failed submission rollback attempts to return every resource borrow, even if one native cleanup fails. It reports the first cleanup failure after attempting the remaining borrows. The owner and a retained completion lease can both retry a failed final cleanup without decrementing the borrower count twice. Graphics map rollback also returns stream ownership when resource unregistration fails. Keep failed owners/streams alive and retry disposal after resolving the native error.

提交失败后的回滚会尝试归还全部资源租约，单个原生清理失败不会阻止其余租约归还；全部尝试后报告首个清理错误。owner 和保留的完成租约均可重试最终清理，不会重复递减借用计数。graphics map 回滚即使注销资源失败，也会归还 stream 所有权。出现清理失败时，应保留 owner/stream，在解决原生错误后重试释放。

For isolated raw API data/query/lifecycle fixtures and observed local Runtime defects, see [native function validation](native-function-validation.md). These package-specific local receipts are separate from reviewed KFD/cloud evidence and preserve native failures.
