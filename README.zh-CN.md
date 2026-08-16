<h1 align="center">HIP CSharp API</h1>

<p align="center">
  面向 C# 与 .NET 的 AMD HIP Runtime 和 HIPRTC 直接绑定，同时提供托管资源所有者与完整生成式低层 C ABI。
</p>

<p align="center">
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml"><img src="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/guojin-yan/HIP-CSharp-API.svg" alt="许可证" /></a>
  <a href="#-当前状态已发布预览版--10-候选"><img src="https://img.shields.io/badge/status-1.0%20candidate-2563eb" alt="1.0 候选" /></a>
  <a href="docs/compatibility/frameworks.md"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET 目标框架" /></a>
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/HIP-CSharp-API?style=flat&amp;label=Stars" alt="GitHub stars" /></a>
</p>

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

# HIP CSharp API

HIP CSharp API 为 AMD HIP Runtime 与 HIPRTC C API 提供直接 .NET 绑定。单一 `JYPPX.ROCm.HIP.CSharp.API` 程序集公开 `JYPPX.ROCm.HipSharp` 命名空间，既包含适合常见 GPU 工作流的托管所有者 API，也包含面向原生 ABI 直接调用场景的生成式低层入口。

## 📖 项目简介

项目围绕三条清晰边界设计：

- **托管所有权：** 设备内存、锁页内存、托管内存、流、事件、模块、内核、图以及 HIPRTC 程序都使用显式 `IDisposable` 生命周期。
- **直接原生访问：** `HipRuntimeNativeApi` 与 `HipRtcNativeApi` 暴露生成式 HIP C ABI，不引入项目自有的 C++ 桥接层。
- **显式原生运行时：** 托管包不包含 AMD 驱动、ROCm 安装或 HIP 原生库；原生库加载过程及诊断信息对调用方保持可见。

所有公开 API 都包含中英文双语 XML 文档，并在全部声明的目标框架中保持相同接口面。

## ✨ 主要功能

- 提供设备发现、内存分配与复制、同步、流、事件、模块、内核以及 HIPRTC 编译的托管 API。
- 提供 stream-ordered allocation、托管内存 advice/prefetch、P2P 访问和 HIP graph capture/replay 的高级所有者 API。
- 根据固定 HIP 7.2.1 头文件生成完整低层接口：459 个 HIP Runtime 声明与 18 个 HIPRTC 声明。
- .NET 7 及以上使用源生成 `LibraryImport`，旧目标框架保留 `DllImport` 兼容路径。
- 包含确定性 interop 生成、冻结的公开 API 快照、包审计以及无需 GPU 的托管测试。
- 为独立打包的 MIGraphX adapter 提供 friend-only 原子 stream enqueue/pending-callback 边界，不新增公开裸 handle API，也不让 core 依赖 MIGraphXSharp。
- 提供内存复制、HIPRTC VectorAdd、stream/event 顺序、graph、托管内存及 P2P copy-or-skip 等可运行正确性样例。

## 📢 当前状态：已发布预览版 / 0.x 验证

Core `0.9.1` 与可选 Linux Runtime `7.2.1` 已在 nuget.org 公开。其 repository-signed 公开字节已于 2026-08-14 通过 nuget.org-only 静态消费者和 fresh package-only Linux GPU/ABI 门禁。Core `0.9.0` 因错误的 `JYPPX.ROCm.HipSharp` 程序集 identity 保持不可变且已 unlist，请勿采用。

当前源码正在验证未发布的 Core `0.9.3` HIPRTC Program/Linker 扩展批次。它把固定模型中的 9 个 HIPRTC 声明提升为托管 owner；在取得新 exact-SHA Radeon Cloud 证据前，这 9 项的云端功能状态仍为 `not-tested`。未来任何 `1.0.0` 都必须同时满足 Windows AMD GPU 实机验证和 Owner 明确发布指令；本次 `0.9.3` 工作不能替代或绕过任一条件。

## 🚀 30 秒开始

安装已公开的托管 Core，并选择兼容的 system ROCm 或可选的 Linux Runtime 包：

```powershell
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.9.1
# Ubuntu 24.04 x64 可选：
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64 --version 7.2.1
```

从源码运行时，需要 `global.json` 指定的 .NET 10 SDK，以及已正确安装 AMD 驱动和兼容 HIP/ROCm 用户态运行时的机器：

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet run --project .\samples\DeviceInfo\DeviceInfo.csproj -c Release
```

核心托管 API 保持简洁：

```csharp
using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Types;

var runtime = new HipRuntime();
runtime.Initialize();

HipRuntimeVersionInfo versions = runtime.GetVersionInfo();
Console.WriteLine($"HIP Runtime: {versions.RuntimeVersion}");
Console.WriteLine($"HIP Driver:  {versions.DriverVersion}");

foreach (HipDevice device in runtime.GetDevices())
{
    Console.WriteLine(device);
}
```

使用托管包的应用仍需提供兼容的原生 `amdhip64`，使用运行时编译时还需提供 `hiprtc`。确定部署组合前请先阅读[平台兼容性](docs/compatibility/platforms.md)。

## 📦 包结构

| 包 | 原生基线 | 内容 | 状态 |
| --- | --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | 不适用 | 托管 `JYPPX.ROCm.HIP.CSharp.API` 程序集并公开 `JYPPX.ROCm.HipSharp` 命名空间，另含 XML 文档、包 README、logo 与许可证 | `0.9.1` 已公开；`0.9.3` HIPRTC 扩展批次尚未发布且未获发布授权 |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` | ROCm `7.2.1` | 已审计的 Linux x64 ROCm 用户态闭包、许可证、来源记录、SBOM 与 promotion receipt | `7.2.1` 已公开并通过 public-feed package-only 验证；版本与 Core 独立 |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` | HIP SDK `7.2.0` | 无原生 inventory | 已禁用的静态审计骨架，不是可用运行时包 |

Core 包不含依赖，也不会安装 GPU 驱动。Runtime 包属于可选部署资产，采用独立版本并受到更严格的发布门禁。原生文件边界见 [Linux Runtime 包指南](docs/guides/linux-runtime-package.md)。

## 🧩 API 范围

| 领域 | 主要托管类型 |
| --- | --- |
| Runtime 与设备 | `HipRuntime`、`HipDevice`、`HipRuntimeVersionInfo` |
| 内存 | `HipDeviceMemory`、`HipTypedMemory<T>`、`HipPinnedMemory`、`HipManagedMemory`、`HipAsyncDeviceMemory` |
| 流与事件 | `HipStream`、`HipEvent`、`HipAsyncLease` |
| 运行时编译 | `HipRtc`、`HipRtcProgram`、`HipRtcCompilation`、`HipRtcException` |
| 模块与内核 | `HipModule`、`HipKernel`、`HipLaunchDimensions`、`HipKernelArgument` |
| Graph 与 P2P | `HipGraph`、`HipGraphExec`、`HipPeerAccess` |
| 加载与诊断 | `HipLibraryLocator`、`HipNativeLibraryLoader`、`HipLibraryLoadDiagnostics` |
| 完整原生 ABI | `HipRuntimeNativeApi`、`HipRtcNativeApi` |

普通应用代码建议使用托管所有者；需要精确原生签名与原生所有权语义时再使用低层 API。

## 🖥️ 平台与框架

Core 项目直接面向 15 个目标框架：

`net46`、`net461`、`net462`、`net47`、`net471`、`net472`、`net48`、`net481`、`netcoreapp3.1`、`net5.0`、`net6.0`、`net7.0`、`net8.0`、`net9.0` 与 `net10.0`。

| 平台 | Core 构建/打包 | GPU 验证 | Runtime 包 |
| --- | --- | --- | --- |
| Linux x64 | 是 | 公开 Core `0.9.1` + Runtime `7.2.1` 已在 Ubuntu 24.04.4 / ROCm 7.2.1 / `gfx1100` 通过 M8.9 nuget.org-only package GPU/ABI 门禁；新的 `0.9.3` bytes 需要自己的 exact-SHA 证据 | 已公开且 receipt-verified 的 Runtime `7.2.1`；也可选择 system ROCm |
| Windows x64 | 是；loader 与 PE 路径已静态审计 | 尚未在 AMD GPU 上验证 | 已禁用的 7.2.0 骨架 |

部分旧 .NET 目标已结束上游支持，本项目仅将其作为包兼容目标保留。构建兼容、历史验证与正式支持之间的完整区别见[框架兼容性](docs/compatibility/frameworks.md)和[平台兼容性](docs/compatibility/platforms.md)。

## 🧪 示例

| 示例 | 内容 |
| --- | --- |
| [`DeviceInfo`](samples/DeviceInfo) | Runtime/驱动版本与设备枚举 |
| [`MemoryCopy`](samples/MemoryCopy) | H2D、D2D 与 D2H 内存往返 |
| [`HipRtcVectorAdd`](samples/HipRtcVectorAdd) | 内存中 HIPRTC 编译、模块加载、内核启动与 CPU 校验 |
| [`HipStreamEventVectorAdd`](samples/HipStreamEventVectorAdd) | 异步复制、非阻塞 stream、event 与执行顺序 |
| [`HipAdvancedFeatures`](samples/HipAdvancedFeatures) | stream-ordered allocation、graph replay、托管内存、生命周期压力测试与 P2P copy-or-skip |

GPU 示例要求显式提供真实目标架构，例如：

```powershell
dotnet run --project .\samples\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --length 1000 --repeat 20
```

这些示例用于验证正确性与所有权行为，不是性能基准，也不作性能承诺。

## 📚 文档入口

| 资源 | 内容 |
| --- | --- |
| [文档首页](docs/index.md) | DocFX 入口与双语指南索引 |
| [完整原生 API](docs/guides/complete-native-api.md) | 生成式 Runtime 与 HIPRTC 低层接口 |
| [HIPRTC VectorAdd](docs/guides/hiprtc-vectoradd.md) | 编译、加载、启动和校验内核 |
| [Stream 与 Event](docs/guides/hip-stream-event-vectoradd.md) | 异步顺序与生命周期指南 |
| [高级 API](docs/guides/advanced-apis.md) | Graph、托管内存、stream-ordered allocation 与 P2P |
| [Linux Runtime 包](docs/guides/linux-runtime-package.md) | 来源、依赖闭包与打包边界 |
| [Windows Runtime 审计](docs/guides/windows-runtime-static-audit.md) | 当前仅静态验证的 Windows 状态 |
| [API 参考](docs/api/toc.yml) | 生成式公开 API 参考 |
| [MIGraphX adapter 租约设计](docs/design/migraphx-adapter-pending-lease.md) | internal stream callback 边界；不新增 HipSharp 公开 API |

运行 `./eng/docs.ps1` 可在 `_site` 目录生成 DocFX 文档站点。

## 🔨 源码构建

仓库通过 `global.json` 固定使用 .NET `10.0.300` SDK。

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet restore .\HipSharp.sln --locked-mode
.\eng\build.ps1 -Configuration Release -NoRestore
.\eng\test.ps1 -Configuration Release -NoBuild
.\eng\verify-public-api.ps1 -Configuration Release
```

Linux 上等价的 Core 门禁为：

```bash
bash ./eng/build.sh Release
```

构建会检查确定性 interop 输出、全部 15 个目标框架、测试、包内容以及公开 API 一致性。纯托管测试不需要 AMD GPU。

## 🗂️ 项目结构

```text
HIP-CSharp-API/
|-- src/JYPPX.ROCm.HipSharp/                 托管所有者与生成式原生 API
|-- samples/                            设备、内存、HIPRTC 与高级功能示例
|-- tests/                              单元、包、API 基线及仓库质量测试
|-- docs/                               DocFX 参考、兼容性说明、指南与版本记录
|-- eng/                                构建、生成、审计、打包与发布门禁
|-- nuget/                              Core/Runtime 包内容及 Runtime manifest
|-- pack/                               可选 Runtime 包项目
|-- native/abi-probe/                   原生 ABI 验证探针
`-- .github/workflows/                  持续集成
```

## 🤝 参与贡献

欢迎提交 Issue 与 Pull Request。修改公开 API、原生声明、所有权行为、包标识或 Runtime payload 前，请阅读 [CONTRIBUTING.md](CONTRIBUTING.md) 与 [API 冻结指南](docs/guides/api-freeze.md)。安全问题请按照 [SECURITY.md](SECURITY.md) 私下报告，不要提交公开 Issue。

## 🙏 致谢

本项目构建于 [AMD HIP](https://github.com/ROCm/HIP) 与 ROCm 生态之上。HIP 行为、平台要求及第三方 Runtime 许可应以 AMD 官方资料为准。

## 📄 许可证

项目源码采用 [Apache License 2.0](LICENSE)。任何打包的 ROCm 组件都保留各自许可证与通知，本项目许可证不会替代其原始条款。

## 📮 支持与联系

- [GitHub Issues](https://github.com/guojin-yan/HIP-CSharp-API/issues)：错误报告与功能建议。
- [GitHub Discussions](https://github.com/guojin-yan/HIP-CSharp-API/discussions)：使用交流。
- QQ 群 `945057948`：社区讨论。

## 📢 软件声明

- **AI 辅助开发：** 开发过程中使用了 AI 工具辅助生成、审查和优化部分代码与文档。
- **安全意图：** 作者声明项目未故意嵌入后门、病毒、凭据窃取或其他恶意行为。
- **测试边界：** 项目尚未覆盖所有操作系统、驱动、ROCm 版本、GPU 架构与工作负载；历史门禁通过不等于通用支持保证。
- **用户责任：** 在生产、商业、工业、安全关键或任务关键场景使用前，应自行完成代码审查与代表性测试，并自行评估适用性、可靠性、许可证及部署风险。

第三方二进制、源码与资源仍由其各自所有者及许可证约束。

Copyright (c) 2026 Guojin Yan.
