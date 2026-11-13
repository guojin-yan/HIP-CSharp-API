# Current public API baseline / 当前公开 API 基线

Current source version is `0.11.0-dev`. The active snapshot is `eng/public-api/JYPPX.ROCm.HipSharp.0.11.0-dev.txt`: **122 exported types and 1,372 members**. The gate checks all 15 TFM assemblies and bilingual XML summaries against the version-selected snapshot. Packing also validates package compatibility. The BCL `ISpanFormattable` enum suppression is not a HipSharp contract change.

当前开发版的基线为 **122 个公开类型、1,372 个成员**；相对已发布 `0.10.0` 的 109 类型、1,295 成员，新增 90 条快照记录，没有删除记录。集合比较不能替代所有权、ABI 或 GPU 行为验证。历史 `1.0.0`/`0.9.1` 的 68 类型、1,002 成员快照仅是历史记录，不能作为当前冻结凭据。

A stable `0.11.0` package has not been declared. Selecting a stable version requires updating `eng/Versions.props`, creating its reviewed snapshot and validating its exact clean-commit package. See [release preparation](../releases/0.11.0.md) and [candidate publication](core-release-candidate.md).

## Surface categories / API 分类

| Category | Contract |
| --- | --- |
| Formal | Public runtime, device, memory, stream/event, module/kernel, HIPRTC, peer, graph, value, enum, and exception types under `JYPPX.ROCm.HipSharp` |
| Diagnostic | Public loader attempts, diagnostics, and load exception under `JYPPX.ROCm.HipSharp.Loading`; included in the version-selected snapshot |
| Sample-only | Types compiled only from projects under `samples/tutorials`, `samples/consumers`, or `samples/validation`; they are not core package API. The M8.7 validation result model is sample-only and does not change the frozen Core surface. |
| Internal | All non-exported implementation types, including generated interop, native handles, leases, and native boundaries |

Historical M8.9/M8.10 evidence applies only to the recorded package bytes. It does not validate the current development surface.

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

Changes to the current baseline require review. Removing, adding, or renaming types or members; changing signatures, enum values, default values, ownership, disposal ordering, error normalization, ABI, or target-framework parity requires a separate design review and a new candidate. Documentation clarifications that do not change contract semantics remain allowed. No public API change may land as an unreviewed generated-file refresh.
