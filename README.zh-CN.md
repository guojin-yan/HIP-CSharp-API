<picture>
  <source media="(prefers-color-scheme: dark)" srcset="docs/images/readme/hero-dark.svg">
  <source media="(prefers-color-scheme: light)" srcset="docs/images/readme/hero-light.svg">
  <img alt="HIP CSharp API — 面向 .NET 的 HIP Runtime 与 HIPRTC 直接绑定" src="docs/images/readme/hero-light.svg">
</picture>

<p align="center">
  面向 C# 与 .NET 的 AMD HIP Runtime 和 HIPRTC 直接绑定，同时提供托管资源所有者与完整生成式低层 C ABI。
</p>

<p align="center">
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml"><img src="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/guojin-yan/HIP-CSharp-API.svg" alt="许可证" /></a>
  <a href="https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API"><img src="https://img.shields.io/nuget/v/JYPPX.ROCm.HIP.CSharp.API?label=NuGet" alt="NuGet 版本" /></a>
  <a href="https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API"><img src="https://img.shields.io/nuget/dt/JYPPX.ROCm.HIP.CSharp.API?label=downloads" alt="NuGet 下载量" /></a>
  <a href="docs/compatibility/frameworks.md"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET 目标框架" /></a>
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/HIP-CSharp-API?style=flat&amp;label=Stars" alt="GitHub stars" /></a>
</p>

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

# ⚡ HIP CSharp API

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

Core `0.10.0` 与可选 Linux Runtime `7.2.1` 已在 nuget.org 公开。精确的 Core 包已在 Ubuntu 24.04.4 / ROCm 7.2.1 / `gfx1100` 通过官方宿主和隔离 package-only Linux GPU/ABI 门禁。Core `0.9.0` 因错误的 `JYPPX.ROCm.HipSharp` 程序集 identity 保持不可变且已 unlist，请勿采用。

Core `0.10.0` 包含 HIPRTC Program/Linker 托管扩展，以及更新后的可运行教程与综合案例。固定 ROCm 7.2.1 runtime 仍有已记录的上游 `hipMemRetainAllocationHandle` 引用计数缺陷，详见 [0.10.0 发布说明](docs/releases/0.10.0.md)。未来任何 `1.0.0` 仍必须满足 Windows AMD GPU 实机验证；本次 `0.10.0` 工作不能替代或绕过该条件。

## 🚀 30 秒开始

安装已公开的托管 Core，并选择兼容的 system ROCm 或可选的 Linux Runtime 包：

```powershell
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.10.0
# Ubuntu 24.04 x64 可选：
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64 --version 7.2.1
```

从源码运行时，需要 `global.json` 指定的 .NET 10 SDK，以及已正确安装 AMD 驱动和兼容 HIP/ROCm 用户态运行时的机器：

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet run --project .\samples\tutorials\01-RuntimeDevice\EnvironmentAndDevice\EnvironmentAndDevice.csproj -c Release
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

| 包 | 内容 |
| --- | --- |
| <code>JYPPX.ROCm.HIP.CSharp.API</code> | 面向已声明 .NET 目标框架的托管 HIP Runtime 与 HIPRTC C# API |
| <code>JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64</code> | 可选的、已审计的 ROCm 7.2.1 Linux x64 用户态运行时闭包 |
| <code>JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64</code> | 已禁用的 Windows 运行时骨架，不含原生 inventory，不是可用运行时包 |

Core 包不含依赖，也不会安装 GPU 驱动。Runtime 包属于可选部署资产，采用独立版本并受到更严格的发布门禁。原生文件边界见 [Linux Runtime 包指南](docs/guides/linux-runtime-package.md)。

## 🌐 公开包与 Release 资产

已发布的 Core 与 Linux Runtime 包位于 NuGet.org，下表版本使用实时 NuGet.org 徽章。仓库存在标注的 `v0.10.0` Git tag，未上传 Release 资产。

后续稳定版 Core 包由基于 tag 的 [NuGet 发布 workflow](.github/workflows/nuget-release.yml) 发布；它只在发布时读取 Actions Secret `NUGET_API_KEY`。

| 包 | 版本 | NuGet.org | 用途 |
| --- | --- | --- | --- |
| <code>JYPPX.ROCm.HIP.CSharp.API</code> | [![version](https://img.shields.io/nuget/v/JYPPX.ROCm.HIP.CSharp.API.svg?label=version)](https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API/) | [包页面](https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API/) | Core 托管 HIP Runtime 与 HIPRTC API |
| <code>JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64</code> | [![version](https://img.shields.io/nuget/v/JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64.svg?label=version)](https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64/) | [包页面](https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64/) | 可选的、已审计的 Linux x64 ROCm 用户态运行时闭包 |

| 发布渠道 | 链接 | 资产 |
| --- | --- | --- |
| Git tag | [v0.10.0](https://github.com/guojin-yan/HIP-CSharp-API/tree/v0.10.0) | 未上传 Release 资产 |
| NuGet.org | [包搜索](https://www.nuget.org/packages?q=JYPPX.ROCm.HIP.CSharp.API) | 已发布的 Core 与 Linux Runtime 包 |

### 🧩 Runtime 包矩阵

下表列出全部 Runtime 包项目。已发布包的版本列使用实时 NuGet.org 徽章；Windows 包保持无原生 inventory 的已禁用静态审计骨架，NuGet.org 上不存在该包。

| 包 ID | 版本 | RID | 原生基线 | 发布状态 |
| --- | --- | --- | --- | --- |
| <code>JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64</code> | [![version](https://img.shields.io/nuget/v/JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64.svg?label=version)](https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64/) | <code>linux-x64</code> | ROCm 7.2.1 用户态闭包 | 已发布到 NuGet.org |
| <code>JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64</code> | 未发布 | <code>win-x64</code> | HIP SDK 7.2.0 骨架 | 已禁用；无原生 inventory |

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
| Linux x64 | 是 | Core `0.10.0` + Runtime `7.2.1` 已在 Ubuntu 24.04.4 / ROCm 7.2.1 / `gfx1100` 通过 exact-package 官方宿主和隔离 package-only GPU/ABI 门禁 | 已公开且 receipt-verified 的 Runtime `7.2.1`；也可选择 system ROCm |
| Windows x64 | 是；loader 与 PE 路径已静态审计 | 尚未在 AMD GPU 上验证 | 已禁用的 7.2.0 骨架 |

部分旧 .NET 目标已结束上游支持，本项目仅将其作为包兼容目标保留。构建兼容、历史验证与正式支持之间的完整区别见[框架兼容性](docs/compatibility/frameworks.md)和[平台兼容性](docs/compatibility/platforms.md)。

## 🧪 示例

[案例学习路径](samples/README.zh-CN.md)按 HIP 功能模块组织，而不是按单个 API 调用堆叠。
完整的云端可复现矩阵、运行日志和 Windows 复现说明见[教程验证指南](samples/tutorials/README.zh-CN.md)。

| 模块 | 入口案例 | 内容 |
| --- | --- | --- |
| Runtime/Device | [`EnvironmentAndDevice`](samples/tutorials/01-RuntimeDevice/EnvironmentAndDevice) | Runtime/Driver 版本与设备枚举 |
| Memory | [`LinearMemoryCopy`](samples/tutorials/02-Memory/LinearMemoryCopy) | H2D、D2D 与 D2H 内存往返 |
| Execution | [`StreamAndEvent`](samples/tutorials/03-Execution/StreamAndEvent) | 非阻塞 Stream、Event 与异步顺序 |
| Kernel | [`HipRtcVectorAdd`](samples/tutorials/04-Kernel/HipRtcVectorAdd) | HIPRTC 编译、Module 加载、Kernel 启动与 CPU 校验 |
| Graph | [`GraphCaptureReplay`](samples/tutorials/05-Graph/GraphCaptureReplay) | Stream capture、Graph 实例化与重放 |
| Multi-device | [`PeerToPeerCopy`](samples/tutorials/06-MultiDevice/PeerToPeerCopy) | Capability-gated peer access 与 P2P copy |
| Data objects | [`ArrayTextureSurface`](samples/tutorials/07-DataObjects/ArrayTextureSurface) | Array、Texture 与 Surface 所有权 |
| Low-level | [`NativeAbiInterop`](samples/tutorials/90-LowLevel/NativeAbiInterop) | 面向专家的生成式 C ABI 直接调用 |
| 综合案例 | [`HeatDiffusion`](samples/showcases/HeatDiffusion/README.zh-CN.md) | 使用 HIPRTC、Stream、Event 和 Graph 重放完成 CPU/GPU 热扩散模拟、校验与 BMP 热力图输出 |
| 综合案例 | [`VisualInspection`](samples/showcases/VisualInspection/README.zh-CN.md) | 使用 OpenCV CPU 图像流水线和 AMD GPU HIPRTC 缺陷掩码 Kernel，输出 PNG 以及 JSON/CSV 证据 |

GPU 示例要求显式提供真实目标架构，例如：

```powershell
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --length 1000 --repeat 20
```

教程示例用于验证正确性与所有权行为，不是性能基准，也不作性能承诺。`HeatDiffusion` 和
`VisualInspection` 综合案例输出仅适用于当前进程运行的 CPU/GPU 测量结果，并分别提供
[HeatDiffusion Radeon Cloud 使用教程](samples/showcases/HeatDiffusion/README.zh-CN.md)
和 [VisualInspection Radeon Cloud 使用教程](samples/showcases/VisualInspection/README.zh-CN.md)。

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

欢迎提交 Issue 与 Pull Request。修改公开 API、原生声明、所有权行为、包标识或 Runtime payload 前，请阅读 [中文贡献指南](CONTRIBUTING.zh-CN.md) 与 [API 冻结指南](docs/guides/api-freeze.md)。安全问题请按照 [SECURITY.md](SECURITY.md) 私下报告，不要提交公开 Issue。

## 🙏 致谢

本项目构建于 [AMD HIP](https://github.com/ROCm/HIP) 与 ROCm 生态之上。HIP 行为、平台要求及第三方 Runtime 许可应以 AMD 官方资料为准。

## 📄 许可证

项目源码采用 [Apache License 2.0](LICENSE)。任何打包的 ROCm 组件都保留各自许可证与通知，本项目许可证不会替代其原始条款。

## 📮 联系、社区与赞助

- [GitHub Issues](https://github.com/guojin-yan/HIP-CSharp-API/issues)：错误报告与功能建议。
- [GitHub Discussions](https://github.com/guojin-yan/HIP-CSharp-API/discussions)：使用交流。
- QQ 群 `945057948`：社区讨论。

<p align="center">
  <img src="docs/images/readme/personal-contact-banner-v7-sponsor-zh.png" width="100%" alt="开发者联系渠道、社区入口以及微信和支付宝赞助二维码">
</p>

---

## ⚠️ 软件声明与免责声明

### 📜 1. 开源协议声明

作者所有开源项目代码均遵循 **Apache License 2.0** 开源协议。

*特别说明：本项目集成了若干第三方库。若任何第三方库的许可协议与 Apache 2.0 协议存在冲突或不一致，均以该第三方库的原始许可协议为准。本项目不包含也不代表这些第三方库的授权声明，使用前请务必阅读并遵守第三方库的相关许可。*

### 🤖 2. 代码开发与质量说明

- **AI 辅助开发**：本代码在开发过程中使用了人工智能（AI）辅助生成与优化，并非完全由人工逐行编写。
- **安全性承诺**：**作者郑重声明，本代码中绝无任何有意设置的后门、病毒、木马或旨在破坏用户设备、窃取数据的恶意代码。**
- **技术局限性**：受限于作者个人的技术水平与能力，代码中可能存在因逻辑不严谨、优化不足或经验欠缺导致的低级问题（例如但不限于内存泄漏、偶发崩溃、资源未释放等）。这些问题纯属能力不足所致，并非主观故意。
- **测试范围**：由于作者精力有限，未对本软件进行全方位、覆盖所有边缘场景的完整测试。

### 🚨 3. 免责声明（重要）

**请在将本代码应用于任何实际项目（特别是商业、工业或关键任务环境）之前，务必进行详尽、严格的自行测试与验证。** 鉴于上述可能存在的代码缺陷及测试覆盖不足，**因使用本代码而导致的任何直接或间接损失（包括但不限于设备故障、数据丢失、系统瘫痪或利润损失等），本作者概不负责。** 一旦您开始使用本代码，即表示您已知晓上述风险并同意自行承担一切后果，相关问题与本作者无关。

### 🔓 4. 代码开源范围

本项目承诺核心逻辑代码完全开源，但上述提到的“第三方库”的二进制文件、源代码或相关资源不在本项目的开源义务范围内，请根据其各自的指引获取。

### 🤝 5. 社区与反馈

尽管存在上述不足，我们仍欢迎大家下载使用、提交 Issue 或参与测试，共同完善项目。如果您在使用过程中发现 Bug、内存溢出或有改进建议，欢迎通过项目主页提供的联系方式与作者取得联系，我们将尽力在有限的时间内提供协助。

Copyright (c) 2026 Guojin Yan.
