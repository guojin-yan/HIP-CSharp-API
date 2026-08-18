# 1.0 public API freeze review / 1.0 公开 API 冻结审查

The pre-release JYPPX ROCm family rename invalidated the former namespace snapshot. The post-migration `JYPPX.ROCm.HipSharp` surface described below is the new freeze baseline; no legacy namespace or forwarding assembly is part of the contract. See the [naming migration decision](../design/jyppx-rocm-naming-migration.md).

发布前 JYPPX ROCm 家族重命名使原 namespace snapshot 作废。下述迁移后的 `JYPPX.ROCm.HipSharp` surface 是新的冻结基线；旧 namespace 和类型转发程序集都不属于契约。参见[命名迁移决策](../design/jyppx-rocm-naming-migration.md)。

The `1.0.0` candidate freezes the exported surface recorded in `eng/public-api/JYPPX.ROCm.HipSharp.1.0.0.txt`. The automated gate compares that baseline with every one of the 15 target-framework assemblies and checks bilingual Chinese/English XML summaries. The `1.0.0` and historical `0.9.1` snapshots are byte-identical, providing an explicit semantic difference result of zero types and zero members. Package validation provides a second compatibility check during packing. Its only suppression category covers the BCL-provided `ISpanFormattable` enum interface introduced in .NET 8, not a HipSharp-declared contract.

`1.0.0` 候选冻结 `eng/public-api/JYPPX.ROCm.HipSharp.1.0.0.txt` 中记录的导出面。自动门禁会比较该基线与全部 15 个目标框架程序集，并检查中英文双语 XML summary；`1.0.0` 与历史 `0.9.1` 快照逐字节相同，语义差异为 0 types/0 members；打包时的 package validation 提供第二层兼容性检查。

## Surface categories / API 分类

| Category | Contract |
| --- | --- |
| Formal | Public runtime, device, memory, stream/event, module/kernel, HIPRTC, peer, graph, value, enum, and exception types under `JYPPX.ROCm.HipSharp` |
| Diagnostic | Public loader attempts, diagnostics, and load exception under `JYPPX.ROCm.HipSharp.Loading`; stable for 0.9 but not the primary compute API |
| Sample-only | Types compiled only from projects under `samples/tutorials`, `samples/consumers`, or `samples/validation`; they are not core package API. The M8.7 validation result model is sample-only and does not change the frozen Core surface. |
| Internal | All non-exported implementation types, including generated interop, native handles, leases, and native boundaries |

The frozen `1.0.0` namespace surface remains 68 exported types and 1,002 members across all 15 TFMs. M8.9 exercised the immutable public `0.9.1` bytes. That evidence is a regression baseline only; the version and package bytes change for `1.0.0`, so the exact candidate requires fresh validation even though the namespace surface is unchanged.

## Ownership and disposal / 所有权与释放

| Area | Frozen contract |
| --- | --- |
| Device, pinned, and typed memory | The returned owner releases its allocation. Async managed-array copies retain their buffers until the stream completes. `Dispose` is idempotent; using a disposed owner fails in managed code. |
| Pitched 2D/3D memory | `HipPitchedDeviceMemory<T>` exclusively owns one allocation. Public shapes and regions use element units; pitch and host offsets use bytes. Async copy/memset retains all owners until stream or graph completion. |
| Stream-ordered memory | `HipAsyncDeviceMemory` belongs to its allocation stream. Copies and kernel arguments remain on that stream. Disposal enqueues `hipFreeAsync`; the stream must outlive the allocation owner and synchronizes pending leases before native destruction. |
| Memory pools | `HipMemoryPool` distinguishes owned custom pools from Runtime-owned borrowed default/current views. `HipPooledDeviceMemory` retains its pool child and allocation stream through `hipFreeAsync` completion; current-pool scopes restore the previous pool in LIFO order. |
| Managed memory | `HipManagedMemory` owns the allocation. `Advise` is synchronous metadata only; `PrefetchAsync` retains the allocation through the supplied stream. Host access requires relevant GPU work to have completed. |
| Stream and event | Stream work retains referenced owners until synchronization. Event recording follows stream ordering. General operation/disposal races are unsupported. |
| Module, globals, and kernel | `HipModule` owns the native module; kernels and borrowed global views are valid only while that exact module remains alive. Global views never free their symbol pointer; byte/typed copies validate ranges and explicit-stream submissions retain the module plus host/device owners until completion. Attributes and occupancy are typed value queries on module-owned `hipFunction_t`. Explicit-stream ordinary and cooperative launches retain the module and pointer owners until completion; cooperative launch validates device capability and resident grid capacity without an ordinary-launch fallback. |
| HIPRTC | `HipRtcProgram` owns the compiler program. A successful compilation copies the code object into managed memory; the compilation result no longer depends on program lifetime. Disposal and failure paths release the native program. |
| Peer access | `HipPeerAccess` represents one ordered device pair. It disables access only when that owner enabled it; pre-existing enablement is never revoked. Copies retain both allocations through the accessing-device stream. |
| Graphs | Capture retains wrapper-owned resources. Explicit graphs expose graph-owned node identities, a sealed managed DAG, typed kernel/copy/memset nodes, and graph-local allocation/free ordering. Each executable has an independent resource lease; launch retains it until stream completion, and destroying the source graph does not invalidate a live executable. Per-node executable updates are rejected during pending launch and preserve old parameters on native failure. |

Owners serialize their own mutable bookkeeping, but they are not advertised as generally thread-safe. Concurrent read-only inspection is not a guarantee that operation and disposal may race. Callers must coordinate lifecycle transitions and must not dispose an owner while another thread is submitting work through it.

各 owner 会串行化自身的可变 bookkeeping，但不承诺通用线程安全。调用方必须协调生命周期变化，不能让一个线程释放 owner 的同时由另一线程继续提交工作。

## Errors / 错误

HIP runtime failures become `HipException`, preserving the `HipError`, numeric code, operation, native error name, and native message. HIPRTC failures become `HipRtcException`, preserving result, operation, native description, and compilation log when available. Missing optional advanced exports normalize to `HipError.NotSupported`. Invalid arguments, invalid ownership relationships, cross-stream misuse, and disposed-object use fail as managed argument, invalid-operation, or disposed-object exceptions before unsafe native work where possible. Cleanup finalizers are non-throwing; explicit disposal remains the observable error path.

## Freeze rule / 冻结规则

For the `1.0.0` freeze, no public API change is permitted inside M8.10. Removing, adding, or renaming types or members; changing signatures, enum values, default values, ownership, disposal ordering, error normalization, ABI, or target-framework parity requires a separate design review and a new candidate. Documentation clarifications that do not change contract semantics remain allowed. No public API change may land as an unreviewed generated-file refresh.
