# Contributing / 社区开发与贡献指南

感谢你帮助验证和改进 HIP CSharp API。本文件是面向社区开发者的实际操作指南，尤其适用于拥有
Windows AMD GPU、可以帮助项目完成真实 Runtime/GPU 测试的贡献者。

[English](CONTRIBUTING.md)

## 1. 先了解项目边界 / Know the project boundary

- Git 仓库根目录是本目录 `HIP-CSharp-API/`。上级目录中的 `plan/`、`diary/` 和
  `Radeon_Cloud/` 是项目维护记录，不属于 Git 内容，不能提交。
- 项目提供 `JYPPX.ROCm.HIP.CSharp.API` Core 包和可选 Runtime 包，公开命名空间是
  `JYPPX.ROCm.HipSharp`。
- Core 包不携带 AMD 驱动、ROCm 安装或任意未审计的原生 DLL/SO。不要把从本机 HIP SDK、压缩包
  或云端复制出来的二进制加入提交。
- 基线维护不得顺手加入未经验证的 HIP P/Invoke 声明、下载的 ROCm 二进制、GPU 支持结论或
  Runtime payload。任何未来 native asset 都必须先有官方来源 URL、包/版本、SHA-256、依赖闭包、
  许可证证据和 clean consumer 测试。
- 当前主线仍是 `0.x` 验证阶段。Linux/Radeon Cloud 结果不能替代 Windows AMD GPU 验证；在
  Windows 实机验证完成且 Owner 明确授权之前，不得宣称 `1.0.0` 或“Windows 已支持”。

项目的验证状态使用以下术语，提交报告时请准确区分：

| 状态 | 含义 |
| --- | --- |
| `Build` | 可以还原并编译目标框架，不代表能加载 HIP。 |
| `Managed-tested` | 无 GPU 的单元、包、生成器或静态检查通过。 |
| `Runtime-tested` | 在指定 OS、HIP/ROCm Runtime 和 .NET runtime 上成功加载。 |
| `GPU-validated` | 在真实 AMD GPU 上完成对应的功能或集成测试。 |

## 2. 可以贡献什么 / What to contribute

没有 AMD GPU 也可以贡献：修复托管生命周期、错误处理、文档、样例、生成器、包审计和无 GPU
测试。有 Windows AMD GPU 时，最有价值的是提供可复现的 Runtime/GPU 结果，覆盖项目当前本地
无法验证的 Windows 路径。

优先欢迎以下贡献：

1. 在官方支持的 Windows 11 x64 + AMD GPU + HIP SDK 组合上运行现有测试并报告结果。
2. 发现设备型号、驱动、HIP SDK 或 .NET 版本相关的加载、ABI、内存、Stream/Event、HIPRTC、
   Module、Graph 和生命周期问题。
3. 为失败或 `Skipped` 的能力补充最小复现、诊断信息和文档，而不是只提交一段未经说明的日志。
4. 修复代码或文档，并保持 API、包身份、生成文件和多目标框架约束不变。

## 3. 环境要求 / Prerequisites

### 所有贡献者 / Everyone

- Git。
- `global.json` 指定的 .NET 10 SDK；可用 `dotnet --info` 确认。
- PowerShell 7 (`pwsh`) 用于仓库脚本；Windows PowerShell 5.1 不作为脚本兼容基线。
- 能够访问 NuGet，且本地工作树保持干净。

### Windows AMD GPU 验证者 / Windows GPU validators

- Windows 11 x64。
- AMD 官方 HIP SDK for Windows 兼容的 GPU/APU、驱动和对应 HIP SDK。请先核对
  [AMD HIP SDK 系统要求](https://rocm.docs.amd.com/projects/install-on-windows/en/latest/reference/system-requirements.html)。
  未列入官方兼容表的设备可以提交实验结果，但不能写成项目支持承诺。
- 使用系统安装的 HIP SDK 进行首次验证，不要自行下载或提交未经审计的 Runtime DLL。
- 如果要验证 .NET Framework，必须在 Windows 上实际运行；Linux/Radeon Cloud 结果不能替代它。

### Linux/Radeon Cloud / Linux cloud validation

Radeon Cloud 只由项目 Owner 在明确授权后使用。社区贡献者不要连接历史实例、猜测旧地址，或在
提交中写入 IP、端口、私钥、token 和原始云端日志。云端脚本的说明见
[`tools/radeon/README.zh-CN.md`](tools/radeon/README.zh-CN.md)。

## 4. 获取源码并完成本地基线 / Clone and establish a baseline

在 PowerShell 中执行：

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
Set-Location HIP-CSharp-API
git status --short
dotnet --info
dotnet restore .\HipSharp.sln --locked-mode
```

先运行不需要 GPU 的完整基线：

```powershell
pwsh .\eng\build.ps1 -Configuration Release
pwsh .\eng\test.ps1 -Configuration Release -NoBuild
pwsh .\eng\test-docs.ps1 -Configuration Release
```

`build.ps1` 会检查确定性 interop 输出和全部 15 个目标框架。`test.ps1` 还会打包 Core、运行
单元/包/仓库质量测试、公开 API 检查和 clean consumer 检查。纯托管基线不要求 AMD GPU；如果
基线失败，请先记录失败命令、完整错误摘要、commit SHA 和 `dotnet --info`，再开始 GPU 排查。

修改绑定 manifest 或生成代码后，必须确认生成结果可复现：

```powershell
pwsh .\eng\generate-interop.ps1 -Check
```

## 5. 无 GPU 也能运行的检查 / Checks without a GPU

`HipManagedExpansionValidation` 提供不加载 HIP 的契约自测：

```powershell
dotnet run --project .\samples\validation\HipManagedExpansionValidation\HipManagedExpansionValidation.csproj -c Release --no-restore -- --self-test
```

还可以只构建单个样例，确认项目引用和参数解析没有回归：

```powershell
dotnet build .\samples\tutorials\01-RuntimeDevice\EnvironmentAndDevice\EnvironmentAndDevice.csproj -c Release --no-restore
```

无 GPU 的结果只能标记为 `Build` 或 `Managed-tested`。不要把“样例编译成功”写成 Runtime 或
GPU 验证通过。

## 6. Windows AMD GPU 验证流程 / Windows GPU validation

### 6.1 记录环境 / Record the environment

验证前记录下列信息；可以脱敏后附在 Issue/PR 中：

```powershell
git rev-parse HEAD
git status --short
dotnet --info
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsArchitecture
Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, VideoModeDescription
hipcc --version
```

同时注明 HIP SDK 安装版本、GPU 的 `gfx` 架构、是否使用系统 HIP SDK，以及测试是 Debug 还是
Release。不要上传设备序列号、GUID、完整用户名路径或公司内部主机名。

### 6.2 从低风险到高风险逐层运行 / Run in layers

先运行设备和加载诊断：

```powershell
dotnet run --project .\samples\tutorials\01-RuntimeDevice\EnvironmentAndDevice\EnvironmentAndDevice.csproj -c Release --no-restore
dotnet run --project .\samples\tutorials\01-RuntimeDevice\LoaderDiagnostics\LoaderDiagnostics.csproj -c Release --no-restore
```

再运行内存、Stream/Event 和 HIPRTC VectorAdd：

```powershell
dotnet run --project .\samples\tutorials\02-Memory\LinearMemoryCopy\LinearMemoryCopy.csproj -c Release --no-restore
dotnet run --project .\samples\tutorials\03-Execution\StreamAndEvent\StreamAndEvent.csproj -c Release --no-restore
$env:HIPSHARP_GPU_ARCH = 'gfx1100' # 替换为本机实际架构
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --length 1000 --repeat 20
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --negative-compile
```

最后运行综合可靠性工作负载。它会将 GPU 结果与 CPU 结果比较，并允许能力受限时输出受控
`Skipped`；任何未记录的失败都应视为问题：

```powershell
dotnet run --project .\samples\validation\AdvancedReliabilityStress\AdvancedReliabilityStress.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --graph-launch-repeats 3 --lifecycle-repeats 100 --stress-rounds 10 --stress-streams 4 --stress-length 4194304
```

运行顺序很重要：先确认 loader 和基础内存，再进入 HIPRTC、Module、Graph 和压力场景。某个
样例失败时，保留第一个失败点和其后是否出现连锁错误，不要只报告最后一行退出码。

### 6.3 Windows Runtime 包验证边界 / Runtime package boundary

当前 Windows Runtime 包仍需要来源、SHA-256、Authenticode、依赖闭包、许可证和 SBOM 证据。
静态审计可以使用：

```powershell
pwsh .\eng\test-windows-runtime-skeleton.ps1
pwsh .\eng\verify-windows-runtime.ps1
```

这些脚本不会替代真实 GPU 运行。不要为了让审计通过而改写 manifest、关闭签名检查、复制系统
DLL 到仓库，或把 `gpuValidated` 手工改成 `true`。若要提供 Runtime 包候选，请先在 Issue 中
说明来源和授权，再按 Owner 指定的 staging 目录提交脱敏证据。

## 7. 如何报告测试结果 / Evidence and issue reports

每次 Windows GPU 验证至少包含：

1. 精确的 40 位 Git SHA，以及工作树是否 clean。
2. Windows 版本、GPU 型号与 `gfx` 架构、驱动、HIP SDK、.NET SDK、构建配置。
3. 实际执行的命令、通过项、失败项和受控 `Skipped` 的原因。
4. 第一个错误的完整异常类型、HIP/HIPRTC 错误码、加载器诊断和相关样例输出。
5. 是否验证了系统 SDK、Runtime 包或两者；没有验证的范围要明确写出。
6. 可安全公开的日志或截图。删除路径中的用户名、序列号、GUID、内部地址和凭据。

建议的 Issue 标题：

```text
[Windows GPU][gfx1100][HIP SDK 7.2] HipRtcVectorAdd fails during module load
```

建议的结果格式：

```text
Commit: <40-character-sha>
Host: Windows 11 x64 / <GPU> / <driver> / <HIP SDK>
.NET: <dotnet --info summary>
Runtime source: system HIP SDK or package <id/version>
Passed: EnvironmentAndDevice, LinearMemoryCopy, StreamAndEvent
Skipped: P2P (one device; capability unavailable)
Failed: HipRtcVectorAdd --negative-compile
First error: <exception, HIPRTC code, and short sanitized output>
Reproduction: <exact command>
```

不要提交性能排行榜或跨设备性能承诺。样例输出的耗时只代表当前会话；正确性、资源释放和可
复现的错误信息比单次吞吐数字更重要。

## 8. 分支、commit 和 Pull Request / Branches, commits, and PRs

- 从最新 `main` 创建短生命周期分支，例如 `test/windows-gpu-rx7900xt`、
  `fix/hiprtc-loader-diagnostic` 或 `docs/windows-contributing`。
- 一个 commit 解决一个主题，subject 使用动词开头，例如 `fix: preserve HIPRTC linker ownership`、
  `docs: add Windows GPU validation guide`。
- 修改公开 API、native declaration、owner/disposal 行为、包 identity、Runtime payload 或
  版本号前，先阅读 [`docs/guides/api-freeze.md`](docs/guides/api-freeze.md)，在 PR 中说明兼容性
  影响和证据。
- 生成文件应由仓库脚本产生，提交 manifest/输入变更和生成结果，不要手工编辑 `.g.cs` 来绕过
  生成器检查。
- PR 描述应包含：目的、涉及的路径、运行过的命令、测试层级、Windows GPU 环境（如适用）、
  未测试范围和是否需要 Owner 进行 Radeon Cloud 验证。

提交前建议再次运行：

```powershell
git diff --check
pwsh .\eng\build.ps1 -Configuration Release
pwsh .\eng\test.ps1 -Configuration Release -NoBuild
git status --short
```

提交中不得包含 `bin/`、`obj/`、`artifacts/`、NuGet 缓存、HIP/ROCm 原生二进制、本机绝对路径、
云端地址、证书、SSH key、PAT、token 或临时测试日志。安全问题请按
[`SECURITY.md`](SECURITY.md) 私下报告，不要创建公开 Issue。

## 9. Radeon Cloud 与 Windows 的职责边界 / Cloud versus Windows

Windows AMD 贡献者负责提供 Windows 实机证据；Radeon Cloud 负责 Owner 授权后的 Linux/ROCm
ABI、Runtime、GPU、包闭包和重复验证。两者结果分别记录，不能互相替代：

```text
Windows AMD GPU -> HIP SDK for Windows -> Windows loader / samples / .NET Framework
Radeon Cloud    -> Ubuntu + ROCm       -> Linux ABI / runtime package / GPU gates
```

如果 PR 需要云端验证，请在描述中写明精确 SHA、目标门禁、预计耗时和是否需要重新打包。等待
Owner 授权期间，贡献者只应继续完成本地可验证工作，不要自行访问云端。

## 10. NuGet 发布 / NuGet releases

稳定版本发布由 [`.github/workflows/nuget-release.yml`](.github/workflows/nuget-release.yml) 负责。只有在更新
`eng/Versions.props` 并完成已授权的发布门禁后，才创建并推送 `vMAJOR.MINOR.PATCH` 标注 tag。Action 会检查 tag、
项目版本和包版本一致，然后执行 Release 测试、文档验证、包审计和干净消费者验证，最后发布 NuGet 包。

仓库 Actions Secret 必须命名为 `NUGET_API_KEY`，且拥有发布 `JYPPX.ROCm.HIP.CSharp.API` 的权限。不要把 Key
写入 workflow、提交到仓库的命令、Issue、日志或包文件。发布后 Action 会确认包可下载，并验证 NuGet.org 仓库签名。

## 11. 快速检查清单 / Quick checklist

- [ ] 我在正确的 Git 根目录工作，且没有修改或提交 `plan/`、`diary/`、`Radeon_Cloud/`。
- [ ] 我确认了 `dotnet --info`、commit SHA 和工作树状态。
- [ ] 我运行了与改动相关的 build/test，明确区分了编译、托管、Runtime 和 GPU 结果。
- [ ] Windows GPU 报告包含 GPU、驱动、HIP SDK、架构和可复现命令。
- [ ] 所有 `Skipped` 都有能力或环境原因，未把未知失败标为跳过。
- [ ] 没有提交原生二进制、凭据、内部地址、缓存或未脱敏日志。
- [ ] PR 说明了未测试范围，以及是否需要 Radeon Cloud/Owner 复核。

English contributors can follow the same commands and evidence rules above. The repository's authoritative
compatibility and release decisions remain in the project plan and the checked-in guides referenced here.
