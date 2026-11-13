# Windows and Linux validation / Windows 与 Linux 验证

The local development matrix uses the same source on Windows x64 and Ubuntu 24.04.5 under WSL2, with the W7900/gfx1100 and installed platform SDK 10.1.0. These are development worktree results, separate from historical published-package evidence and a clean release attestation.

本机开发验证使用同一源码，分别在 Windows x64 和 WSL2 的 Ubuntu 24.04.5 执行，GPU 为 W7900/gfx1100，平台 SDK 为 10.1.0。结果对应开发工作区，与历史公开包证据及干净发布凭据分开记录。

## Run / 执行

Keep separate Windows and Linux output roots; native executables, restore assets and intermediate files must not be shared. Install the SDK selected by `global.json` and PowerShell 7 for each OS. The Linux shell entry delegates to the same PowerShell build/test/API/package gates as Windows and propagates failures.

两端必须使用不同的输出根目录，不共用原生可执行文件、restore 资产或中间文件。各自准备 `global.json` 规定的 SDK 与 PowerShell 7。Linux shell 入口委托给与 Windows 相同的构建、测试、API 和包门禁，失败会继续向外传播。

Windows PowerShell, from source / Windows PowerShell，在源码目录：

```powershell
$env:HIPSHARP_OUTPUT_ROOT = 'E:\GitSpace\HIP-CSharp-API\build\fixes'
./eng/build.ps1 -Configuration Release
./eng/test.ps1 -Configuration Release -NoBuild
./eng/test-local-gpu.ps1 -HipRoot 'D:\AMD\ROCm\10.1.0\HIP' -Architecture gfx1100 -OutputRoot $env:HIPSHARP_OUTPUT_ROOT -NoBuild
```

The existing `eng/test-local-windows.ps1` remains a Windows compatibility entry. See [local Windows development](local-windows-development.md) for portable SDK activation.

原有 `eng/test-local-windows.ps1` 保留为 Windows 兼容入口。便携 SDK 的激活方式见 [Windows 本机开发](local-windows-development.md)。

Linux bash, from source / Linux bash，在源码目录：

```bash
export DOTNET_ROOT=/mnt/e/GitSpace/HIP-CSharp-API/dependencies/dotnet-linux
export DOTNET_CLI_HOME=/mnt/e/GitSpace/HIP-CSharp-API/dependencies/dotnet-home-linux
export NUGET_PACKAGES=/mnt/e/GitSpace/HIP-CSharp-API/dependencies/nuget-linux
export PATH="$DOTNET_ROOT:/mnt/e/GitSpace/HIP-CSharp-API/dependencies/pwsh-linux:$PATH"
export HIPSHARP_OUTPUT_ROOT=/mnt/e/GitSpace/HIP-CSharp-API/build/linux
source /opt/AMD/ROCm/10.1.0/activate.sh
bash eng/build.sh Release
pwsh -NoProfile -File eng/test-local-gpu.ps1 -HipRoot "$HIP_PATH" -Architecture gfx1100 -OutputRoot "$HIPSHARP_OUTPUT_ROOT" -NoBuild
```

`test-local-gpu.ps1` records 18 workloads in `artifacts/local-windows` or `artifacts/local-linux`. Its top-level status preserves capability skips; inspect individual logs and managed-expansion JSON for skipped subtests. The base commit identifies the starting revision, not a clean snapshot of uncommitted changes.

`test-local-gpu.ps1` 在 `artifacts/local-windows` 或 `artifacts/local-linux` 记录 18 项工作负载。顶层状态保留能力跳过；子项跳过需查看独立日志和综合扩展 JSON。基础 commit 表示起点版本，不代表未提交修改已形成干净快照。

## Local platform differences / 本机平台差异

| Check / 检查 | Windows | Linux / WSL2 |
| --- | --- | --- |
| Main GPU workloads / 主 GPU 工作负载 | 17 pass, 1 capability skip | 16 pass, 2 capability skips |
| Managed-memory advice/prefetch | Device capability skip / 设备能力跳过 | Device capability skip / 设备能力跳过 |
| Array/texture/surface | Passed | `hipMallocArray` returned `NotSupported`; incomplete / 未完成 |
| Cooperative launch | Capability subtest skipped / 子项能力跳过 | Capability subtest skipped / 子项能力跳过 |
| Cross-GPU P2P | Single-device skip / 单卡跳过 | Single-device skip / 单卡跳过 |
| Complete-model Runtime exports | 457/459 | 459/459 |
| HIPRTC exports | 18/18 | 18/18 |

Windows lacks `hipMemGetHandleForAddressRange` and `hipPointerSetAttribute` in the installed DLL. The managed pointer setter reports `HipError.NotSupported`; the raw API still follows native export availability. Presence of an export does not establish operation support, as the Linux `hipMallocArray` result demonstrates. The ABI probe's compile-time assertions passed against each installed SDK, and all 142 emitted fields matched across OSes; this is the probe's selected scope, not every native descriptor layout.

本机 Windows DLL 缺少 `hipMemGetHandleForAddressRange` 和 `hipPointerSetAttribute`。托管 pointer setter 返回 `HipError.NotSupported`；raw API 仍受原生导出可用性约束。函数存在不代表功能可用，Linux `hipMallocArray` 的结果即为例证。ABI 探针在两端 SDK 下的编译期断言均通过，142 个输出字段一致；该结论只覆盖探针所选范围，不能扩展到所有原生 descriptor 布局。

`AdvancedReliabilityStress` checks managed-memory capabilities before that subtest, retains unexpected native errors, and continues graph, lifetime and multi-stream correctness checks when the subtest is unavailable. Local validation also exercises 10 rounds, 4 streams, 1,048,576 elements and 100 owner lifecycles, plus the tiny HeatDiffusion CPU/GPU comparison. Timing output from a showcase is not a cross-OS performance comparison.

`AdvancedReliabilityStress` 在托管内存子项前检查设备能力，保留非预期 native 错误；子项不可用时继续执行 graph、生命周期和多流正确性验证。本机还执行 10 轮、4 stream、1,048,576 元素、100 次 owner 生命周期，以及 tiny HeatDiffusion CPU/GPU 对比。案例输出的计时不能用于跨系统性能比较。

WSL2 shares the Windows driver and GPU through `/dev/dxg`. These results do not replace a bare Linux `/dev/kfd` deployment, the pinned ROCm 7.2.1 isolated Runtime package gate, external-resource/IPC integration, or future release approval.

WSL2 经 `/dev/dxg` 共享 Windows 驱动与 GPU；本轮结果不能替代独立 Linux `/dev/kfd` 部署、固定 ROCm 7.2.1 的隔离 Runtime 包门禁、外部资源/IPC 集成及后续发布批准。
