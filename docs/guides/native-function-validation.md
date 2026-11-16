# Native function validation / 原生逐函数验证

`eng/function-validation/run.py` builds a .NET 10 PackageReference-only consumer from two exact local nupkg files. It uses a new local feed/cache, removes SDK library search paths, and runs each named fixture in a separate process with a 90-second timeout. All project files, restored packages, compiler output, diagnostics and receipts go to a new directory **outside this repository**. It does not download dependencies or change the Core package.

`eng/function-validation/run.py` 使用两个精确本地 nupkg 构建 .NET 10 PackageReference-only 消费者，采用独立本地 feed/cache，移除 SDK 搜索路径，并以独立进程运行每组夹具（90 秒超时）。项目、中间文件、诊断和证据全部写入仓库外的新目录；入口不下载依赖、不替换 Core 包。

```text
python -B eng/function-validation/run.py --core <exact-core.nupkg> --runtime <exact-host-runtime.nupkg> --hip-root <installed-HIP-SDK> --output <new-external-directory> --architecture gfx1100
python -B eng/function-validation/verify.py <external-directory>/summary.json
```

Use the pinned .NET 10.0.300 environment. The Runtime must be the local-development package for the current host RID. Each native file is checked against its package manifest before building. Every process runs the shared filename/path/SHA-256 module audit. Package identity, Core source commit, validation commit, tool hashes, host/WSL identity, ordered native-call traces and per-fixture log hashes are recorded. Source changes during execution invalidate stable evidence. `--fixture <name>` selects a subset and cannot claim a full fixture matrix.

使用固定的 .NET 10.0.300 环境和匹配当前 RID 的 local-development Runtime 包。构建前逐项核对包内原生文件清单；每个进程核对实际模块文件名、路径及 SHA-256。摘要绑定 Core/Runtime 包、Core 源提交、工具提交/哈希、主机与 WSL 身份及原始日志。运行中修改源码会使证据不稳定；`--fixture` 子集不会标记完整矩阵。

The host-kernel/global-symbol fixtures compile a test-only helper with the installed HIP compiler, then load it after the exact packaged Runtime. This helper stays outside the Core package. Its source, compiler executable, build log, built/deployed bytes and the actual loaded-helper marker are bound by hashes. Header `static_assert` checks verify the batch-operation descriptor layouts. `--hip-root` is required for these fixtures and the full matrix; subsets without host fixtures do not need a compiler. Module auditing still rejects SDK Runtime loading. The helper does not establish binary provenance or redistribution permission for the installed SDK.

host kernel/global symbol 组通过本机 HIP 编译器生成测试辅助库，加载精确包 Runtime 后才加载该库；辅助库不进入 Core 包。核验绑定源码、编译器文件、编译日志、构建和部署后的二进制及实际加载标记，并通过头文件静态断言检查 batch descriptor 布局。完整矩阵和 host 组必须传入 `--hip-root`；其他子集无需编译器。模块审计仍拒绝从 SDK 加载 Runtime。这些记录不代替 SDK 的原始来源及再分发材料。

The fixtures assert data copies, row padding, device and allocation queries, pinned-host mapping, stream/event ordering, capture frontiers, graph topology/clone/update/enable state, host callbacks, user-object destruction, HIPRTC diagnostics/code/bitcode/name lowering/linking, loaded-kernel execution, pool policies and retained virtual-memory ownership. Windows array fixtures exercise driver descriptors, mip levels, array copies and texture/surface descriptor round trips. They do not establish texture sampling correctness. Raw retained-allocation tests reject HIP older than 7.12 before invoking its unsafe native retain implementation.

夹具检查实际复制数据与行 padding、查询往返、固定 host 映射、stream/event 顺序、capture frontier、graph 拓扑/克隆/更新/开关、回调、user object 析构、HIPRTC 编译/链接/诊断和真实 kernel 结果、pool 策略及虚拟内存独立引用。Windows array 组检查 descriptor、mip level、copy 和 texture/surface 查询，不宣称验证了纹理采样计算。raw retain 夹具在调用前拒绝 HIP 7.12 以下版本。

Only a fixture that completes all its assertions and cleanup, exits zero, and passes module auditing contributes function evidence. Symbol presence, mock calls, successful calls before a failed assertion and missing logs do not count. `NotSupported` is recorded separately and is never positive function evidence. The overall command exits nonzero for unexpected failures **or any native restriction**, while continuing the other fixtures. `verify.py` recomputes the inventory and rejects package/tool/log/hash or derived-result changes; integrity verification can succeed for a report containing genuine test failures. It is not a release approval.

只有全部断言与清理完成、进程成功退出且模块审计通过的夹具才贡献函数证据。失败断言之前的成功调用不计入；符号、mock 和缺失日志均不能代替执行。`NotSupported` 单列，不算正向通过。只要存在失败或原生限制，总入口返回非零，但继续执行其他组。`verify.py` 重核全部绑定与 477 行结果；“证据完整性通过”可以对应一份包含真实失败的报告，不等于发布批准。

## Observed local 10.1.0 native defects / 本地原生缺陷

The Windows and WSL local Runtime libraries were also called directly through `native-control.py`, without loading the C# assembly. The control verifies native input hashes and preserves its observations outside source. It reproduced these failures:

- `hipMemsetD2D16`, `hipMemsetD2D32` and their Async variants return success while filling only `width` bytes per row. Their header contract describes `width` 16/32-bit elements. A 128-byte expected write with a 256-byte pitch stops after 64/32 bytes. Padding and untouched data are checked independently.
- Immediately after `hipInit` and primary-context reset, `hipDevicePrimaryCtxGetState` reports inactive; `hipDevicePrimaryCtxSetFlags` returns 216, and state remains inactive after a successful retain. Context resource cleanup is still attempted.
- Several deprecated context APIs return `NotSupported`, including flags, API version and cache/synchronization controls. Supported context operations are validated separately.
- The driver `hipLinkAddData` and `hipLinkAddFile` reject LLVM bitcode input type 100 with `InvalidValue`, while the separate HIPRTC linker accepts the corresponding compiler output. Both data/file modes are also exercised by the direct-native control; they are not silently rerouted to HIPRTC.
- Windows `hipStreamBatchMemOp` and graph batch-memory operations return success while leaving the requested 64-bit write at zero; the separate scalar stream-write/wait APIs pass. Direct-native controls reproduce the batch failure with header-checked layouts. The same batch operations pass on local WSL/Linux.
- WSL `hipDeviceGetTexture1DLinearMaxWidth` returns success with a zero width for a valid channel descriptor, also observed without the managed assembly. Array-related `NotSupported` remains a separate WSL limitation.

上述结果也通过不加载 C# 程序集的直接原生对照复现。它们是本地 Runtime 的开放边界，不通过修改 raw ABI、乘改调用参数、吞错或伪造成功来掩盖。尤其不能把此 Runtime 视为全部 477 接口均满足原生语义的正式发布依赖。

The new local inventory is separate from the reviewed KFD evidence in `eng/interface-coverage/reviewed-function-evidence.json`. It neither upgrades that historical ledger nor establishes current-Core KFD/PRoot, external-memory/semaphore, graphics or multi-GPU validation. Per-entry positive evidence also does not imply complete negative, concurrency or lifecycle coverage. Existing native redistribution, source/build/license and production publication gates remain applicable.

本机新结果不改写历史 KFD 清单，也不代替当前 Core 的 KFD/PRoot、external/semaphore、graphics、多 GPU、完整负向与并发生命周期验证；原生再分发、完整源码/build/license 和生产发布门禁仍须独立闭合。
