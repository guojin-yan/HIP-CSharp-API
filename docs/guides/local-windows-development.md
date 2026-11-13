# Local Windows development / Windows 本机开发

For the shared Windows/Linux runner and WSL2 results, see [cross-platform validation](local-cross-platform-validation.md). Windows `HIP_PATH` accepts either slash style; discovered HIPRTC paths are normalized before directory comparison.

共用 Windows/Linux 入口与 WSL2 结果见[双平台验证](local-cross-platform-validation.md)。Windows `HIP_PATH` 支持两种斜杠，HIPRTC 发现前会规范化目录以进行比较。

Use the .NET SDK selected by `global.json`, an installed AMD driver, and a compatible HIP SDK. Native libraries remain supplied by the local SDK; the managed Core package does not install drivers. The Windows Runtime packaging skeleton remains disabled.

使用 `global.json` 指定的 .NET SDK、已安装的 AMD 驱动和兼容 HIP SDK。原生库由本机 SDK 提供，托管 Core 不安装驱动；Windows Runtime 打包骨架仍未启用。

From the source checkout in PowerShell 7 / 在源码目录使用 PowerShell 7：

```powershell
# If using this workspace's portable SDK / 使用本工作区便携 SDK 时：
$env:DOTNET_CLI_HOME = 'E:\GitSpace\HIP-CSharp-API\dependencies\dotnet-home'
$env:PATH = 'E:\GitSpace\HIP-CSharp-API\dependencies\dotnet;' + $env:PATH
$env:HIPSHARP_OUTPUT_ROOT = 'E:\GitSpace\HIP-CSharp-API\build\local-windows'
$env:HIP_PATH = 'D:\AMD\ROCm\10.1.0\HIP'
$env:ROCM_PATH = $env:HIP_PATH
$env:PATH = (Join-Path $env:HIP_PATH 'bin') + ';' + $env:PATH
./eng/build.ps1 -Configuration Release
./eng/test.ps1 -Configuration Release -NoBuild
./eng/test-local-windows.ps1 -HipRoot $env:HIP_PATH -Architecture gfx1100 -OutputRoot $env:HIPSHARP_OUTPUT_ROOT -NoBuild
```

Choose an output folder outside the checkout. `HIPSHARP_OUTPUT_ROOT` redirects project `bin`/`obj`, generated verification files, test results, packages, and package-consumer audit work. `HIPSHARP_REPOSITORY_ROOT`, set by the engineering entry points, lets tools and tests locate source independently of assembly location. Keep downloaded SDKs, logs, probes, and development records in sibling workspace folders. Documentation publishing and specialized release scripts have separate output options; inspect those paths before running them.

输出路径应位于源码之外。`HIPSHARP_OUTPUT_ROOT` 重定向项目 `bin`/`obj`、生成校验文件、测试结果、包和消费者审计工作目录。工程入口会设置 `HIPSHARP_REPOSITORY_ROOT`，使工具和测试无需从程序集位置反推源码根。下载的 SDK、日志、探针和开发记录也放在外层目录。文档发布及专项发布脚本具有各自的输出选项，执行前需检查其路径。

The GPU entry point runs 18 correctness workloads, writes a log per workload, and aggregates exit codes in `artifacts/local-windows/summary.json`. It records the base Git commit and labels the result as local development worktree validation. It is not a clean release attestation or performance measurement. A failure remains a failure; explicit capability skips are recorded separately.

The local run passed 17 workloads and skipped the managed-memory tutorial by capability. The managed-expansion workload completed 1,063 comparisons; its cooperative-launch subtest skipped because the device reported no support. A separate .NET Framework 4.8 smoke compiled and executed a GPU increment kernel through the default HIPRTC loader.

GPU 入口运行 18 项正确性工作负载，为每项保存日志，并在 `artifacts/local-windows/summary.json` 汇总退出码。结果记录基础 Git 提交并标识为本地开发工作区验证，不作为干净发布凭据或性能测量。失败按失败记录，明确的能力跳过单独记录。

本机执行通过 17 项，managed-memory 教程按能力跳过。综合扩展验证完成 1,063 次比较，其中 cooperative launch 子项因设备报告不支持而跳过。另以 .NET Framework 4.8 冒烟验证默认 HIPRTC 加载、编译及 GPU 自增内核执行。

## SDK loading / SDK 加载

Within each configured search directory, the loader discovers versioned `hiprtcNNNN.dll` files (for example, `hiprtc0716.dll`) in descending numeric order before the legacy fallback. Directory precedence and the Runtime/HIPRTC same-directory closure rule still apply. `hiprtc-builtins` is a dependency, not an entry library. On .NET Framework, HIPRTC calls resolve delegates from the actual loaded module because legacy CLR import names cannot be redirected with `SetDllImportResolver`.

加载器在每个搜索目录中按版本降序发现 `hiprtcNNNN.dll`（例如 `hiprtc0716.dll`），然后使用旧版回退名；原有目录优先级和 Runtime/HIPRTC 同目录闭包规则继续有效。`hiprtc-builtins` 是依赖库，不是入口库。.NET Framework 从实际加载模块解析 HIPRTC 委托，以适配旧 CLR 无法使用 `SetDllImportResolver` 重定向导入名的限制。

## Device capabilities / 设备能力

On the local W7900/gfx1100 setup with platform SDK 10.1.0, HIP Runtime reports 7.16.26385 and HIPRTC reports 9.0. These are separate component versions. Device queries report managed-memory support and concurrent managed access as zero, so the managed-memory advice/prefetch tutorial skips before allocation. Direct native probing confirmed that successful `hipMallocManaged` alone does not establish advice support.

本机 W7900/gfx1100、平台 SDK 10.1.0 下，HIP Runtime 报告 7.16.26385，HIPRTC 报告 9.0；这些是不同组件的版本。设备查询的 managed-memory 和 concurrent managed access 均为零，因此 advice/prefetch 教程在分配前跳过。直接原生探针确认：`hipMallocManaged` 成功本身不代表支持 advice。

The virtual-memory tutorial checks device support and queries `hipMemGetAllocationGranularity` through the generated low-level API. The local minimum is 65,536 bytes; a fixed 4,096-byte reservation fails with `InvalidValue`. Other native errors remain visible. A local SDK passing selected workloads does not establish the pinned ROCm 7.2.1 full ABI contract, Linux regression status, cross-device P2P, or future release readiness.

虚拟内存教程先检查设备支持，再通过生成式底层 API 查询 `hipMemGetAllocationGranularity`。本机最小粒度为 65,536 字节；固定 4,096 字节的预留会返回 `InvalidValue`。其他原生错误继续向外报告。本机 SDK 的部分工作负载通过，不能代表固定 ROCm 7.2.1 的全部 ABI、Linux 回归、多设备 P2P 或未来发布条件均已完成。
