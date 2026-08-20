# HIP CSharp API 首次发布！在 C# 中直接调用 AMD HIP，让 .NET 项目也能用上 Radeon GPU

![HIP CSharp API 项目标识](https://ygj-images1.oss-cn-hangzhou.aliyuncs.com/images/hero-light.svg)

## 前言

对于很多使用 C# 做桌面软件、工业视觉、数据处理和 AI 应用的开发者来说，真正麻烦的往往不是算法本身，而是怎样把 GPU 能力接进现有工程。官方示例使用 C++ 或 Python 很快就能跑起来，到了 C# 项目中，却经常要重新处理 P/Invoke、动态链接库、指针生命周期、运行库版本和跨平台发布。示例代码可能只有几十行，配置环境和排查依赖却要花上大半天。

我开发 HIP CSharp API，就是想把这条路打通。C# 开发者不需要因为一段 GPU 计算代码就重写整个项目，也不应该每次都从手写原生接口开始。项目保留 HIP 原生 API 的能力和语义，同时使用 C# 开发者熟悉的托管对象、异常和 `IDisposable` 管理设备、显存、Stream、Event、Module 与 Kernel。

简单说，**HIP CSharp API** 是一套面向 C#/.NET 的 AMD HIP Runtime 与 HIPRTC API。这是项目的第一次公开发布，当前已经具备以下内容：

- `JYPPX.ROCm.HIP.CSharp.API` Core 包已经发布到 NuGet.org。
- 可选的 `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64 7.2.1` 已发布到 NuGet.org，并提供对应的 GitHub Release 附件。
- 仓库保留 `v0.10.0` Git tag；Ubuntu Runtime 使用独立的 `runtime-ubuntu.24.04-v7.2.1` annotated tag 和 GitHub Release。
- Core 包不自动安装 AMD 驱动；可以使用系统中的 ROCm/HIP，也可以安装项目提供的 Linux Runtime 包。
- 项目已经在 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2 和 `gfx1100` 环境完成 GPU 测试；Windows AMD GPU 实机测试还需要更多开发者一起参与。





## 一、这个项目解决哪些实际问题

### 1.1 C# 项目不应该被挡在 GPU 门外

现在用 C# 做桌面软件、工业视觉、数据处理、服务端任务和 AI 应用已经非常自然，但一旦需要自己写 GPU Kernel，资料、示例和工具链往往又回到了 C++。很多项目最后只能把 GPU 代码单独编译成动态库，再写一层 P/Invoke；接口稍微调整，C++、导出函数和 C# 声明就要一起修改。

问题并不是 C# 不能做 GPU，而是缺少一条足够自然的入口。开发者希望继续使用熟悉的 .NET 工程、异常处理和 `IDisposable`，同时又能访问设备、显存、Stream、Event、Module、Kernel 和 HIPRTC。

**HIP CSharp API** 就是为这条入口而做。它不重新实现 HIP，而是基于 AMD HIP 的 C ABI，为 .NET 项目提供从底层绑定到托管资源管理的一套连续 API。

### 1.2 能跑 Demo，还要能够查问题

一个库真正有价值，不是把函数名翻译成 C#，而是让开发者在真实项目中少走弯路：

- 动态库加载失败时，能够知道当前加载路径、Runtime 来源和错误上下文；
- GPU 资源进入异步执行后，能够明确谁拥有它、什么时候可以释放；
- HIPRTC 编译失败时，能够拿到编译日志，而不是只看到一个错误码；
- GPU 结果出现偏差时，能够和 CPU 参考结果、输入参数和生成产物一起复核。

这也是项目同时提供托管 owner、异常边界、基础教程和综合案例的原因。先把问题定位的路径做出来，再把更多接口和场景逐步补齐。

## 二、HIP CSharp API 是什么

**HIP CSharp API** 是一个面向 C#/.NET 的 AMD HIP Runtime 与 HIPRTC API 项目。项目没有增加一个覆盖全部接口的 C++ 中间层，而是在 AMD HIP 的 C ABI 之上提供两种可以并存的使用方式：

- **托管 owner 层：** 使用 `HipRuntime`、`HipDeviceMemory`、`HipStream`、`HipEvent`、`HipModule` 和 `HipKernel` 管理资源和生命周期；
- **低层 API：** 在需要时仍能贴近 HIP Runtime 和 HIPRTC 的原始错误码、签名和 ABI 语义。

普通 .NET 项目可以从托管 API 开始；需要排查、扩展或研究 ABI 时，再进入低层。这样既能从托管 API 上手，也保留了需要深入 ABI 时的入口。

如果你有以下需求：

- 想在 ASP.NET Core、Worker、CLI 或桌面程序中直接使用 AMD GPU 的 .NET 开发者；
- 想在 C# 中编译 HIP Kernel、加载 Module、管理 Stream/Event 并验证 CPU/GPU 结果的开发者；

那这个库将是你的首选，不过不同 GPU、驱动和 ROCm 版本仍需要在实际设备上验证，GPU 是否可用必须以目标环境的运行结果为准。

## 三、HIP CSharp API 目前能做什么

### 3.1 从设备到 Kernel

当前版本已经覆盖一条完整的 C# GPU 工作路径：

| 阶段 | 可以做什么 |
| --- | --- |
| 设备与加载 | 查看 Runtime/Driver、枚举设备、读取属性和加载诊断 |
| 内存与执行 | 分配显存、主机/设备复制、使用 Stream 和 Event 管理异步工作 |
| Kernel | 加载 Module、设置参数、启动 Kernel，并通过 HIPRTC 在运行时编译源码 |
| 结果复核 | 使用 CPU 参考实现、误差指标和结构化输出检查 GPU 结果 |

这不是一张“所有接口都已在所有平台验证”的清单。上述主路径都有对应的示例、测试或实际 GPU 运行结果；更细的 API 覆盖和验证边界会在后续文章中单独说明。

### 3.2 从最小教程到综合案例

仓库既有设备、内存、执行和 Kernel 的小型教程，也有两个完整应用案例：

| 案例 | 你会看到什么 | 输出 |
| --- | --- | --- |
| `HeatDiffusion` | CPU 参考解与 HIPRTC GPU 热扩散计算的对照 | 数值误差、性能摘要和热力图 |
| `VisualInspection` | OpenCV CPU 缺陷掩码与 HIPRTC GPU 掩码的对照 | 四组图片校验、JSON、CSV 和 PNG |

这些案例的重点不是展示一段孤立 Kernel，而是展示 C# 应用如何准备输入、管理资源、执行 GPU 工作、复核结果并生成可以继续查看的产物。

## 四、核心架构：从 C# 到 AMD GPU 的完整链路

从应用代码到 GPU，实际会经过几层边界：

![hip-csharp-rocm-architecture-clean](https://ygj-images1.oss-cn-hangzhou.aliyuncs.com/images/hip-csharp-rocm-architecture-clean.png)



这张链路解释了两个常见事实：Core NuGet 包不是驱动安装器；即使应用还原成功，目标主机仍必须有兼容的驱动、设备节点和 GPU。反过来，用户态 Runtime 的来源可以是系统 ROCm，也可以是项目发布的 Ubuntu Runtime 包。

托管层的作用不是改变 HIP 的执行规则，而是让资源所有权和错误边界在 C# 中更清楚。Stream、Pinned Memory 和异步复制仍然需要遵守 GPU 的执行顺序；项目只把这些约束变得更容易表达和诊断。

## 五、NuGet 包与版本说明



| 包 | 版本 | 用途 | 地址 |
| --- | --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | `0.10.0` | HIP Runtime、HIPRTC、托管 owner 和生成式 ABI | https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API/0.10.0 |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` | `7.2.1` | Ubuntu 24.04 x64 用户态 ROCm/HIP Runtime 闭包 | https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64/7.2.1 |

`0.10.0` 是 **HIP CSharp API** 第一次公开发布的预览版本，不是面向旧版本用户的升级包。它把已经完成的托管 API、Runtime 包、教程、综合案例和可复核运行结果整理为一个可安装、可运行、可继续反馈的起点。

## 六、本地快速用起

### 6.1 前置条件

本地运行需要兼容的 AMD GPU、驱动和 HIP/ROCm 用户态环境。先检查设备节点、架构和 .NET：

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --list-runtimes
```

示例中的 `gfx1100` 只是已验证设备的架构，使用其他 GPU 时应替换为实际检测结果。

### 6.2 安装包

在现有 .NET 项目中执行：

```bash
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.10.0
```

如果目标是 Ubuntu 24.04 x64，并希望使用项目提供的用户态 Runtime，再执行：

```bash
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64 --version 7.2.1
```

如果主机已经正确安装系统 ROCm，可以只安装 Core 包。两种方式的区别在于用户态动态库由谁提供，不改变应用对 AMD 驱动和设备节点的要求。

### 6.3 先跑一个最小案例

从仓库根目录运行设备和加载诊断：

```bash
dotnet run --project samples/tutorials/01-RuntimeDevice/EnvironmentAndDevice/EnvironmentAndDevice.csproj -c Release
```

这个案例会输出 Runtime/Driver 版本和设备信息。本文验证环境中的关键输出如下；你的 Runtime 版本和设备名会随环境变化：

```text
HIP Runtime: 7.2.53211
HIP Driver:  7.2.53211
0: AMD Radeon Graphics
```

能够看到真实设备信息，就说明动态库加载和设备枚举已经完成。

### 6.4 再跑一个有结果的 GPU 案例

想快速看到 C# 和 GPU 共同完成一项任务，可以运行：

```bash
dotnet run --project samples/showcases/HeatDiffusion/HeatDiffusion.csproj -c Release -- --arch gfx1100 --profile quick
```

程序会运行 CPU 参考路径和 GPU 路径，最后输出误差和结果摘要。本文在 `gfx1100` 验证环境中的关键输出如下：

```text
Result: PASSED
Execution mode: graph-capture
GPU kernel median: 17.48 ms
GPU end-to-end median: 20.60 ms
Maximum absolute error: 1.14441E-05
RMSE: 9.10307E-08
```

这里的耗时只代表当前设备、驱动、输入规模和运行次数，不应直接当作跨设备基准。

## 七、没有 AMD GPU，如何在 Radeon Cloud 体验

### 7.1 准备云端环境

如果手上暂时没有 AMD GPU，可以使用 [AMD Radeon Cloud](https://developer.amd.com.cn/login?source=J2RtJQRgM)。

启动 Ubuntu/ROCm GPU 实例后，在实例终端执行：

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
```

两个综合案例都提供了面向 Radeon Cloud 的运行脚本：

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
bash ./samples/showcases/VisualInspection/run-visual-inspection.sh
```

脚本会检查 GPU、选择兼容的 .NET SDK、使用 locked mode 还原、构建 Release、检测 `gfxNNNN` 架构，并把摘要和图片产物写入 `artifacts/`。

### 7.2 HeatDiffusion

在 `gfx1100` Radeon Cloud 环境中，HeatDiffusion 的一次实际运行结果为：

```text
Result: PASSED
Execution mode: graph-capture
GPU kernel median: 17.48 ms
GPU end-to-end median: 20.60 ms
Maximum absolute error: 1.14441E-05
RMSE: 9.10307E-08
```

它完成了超过 14 亿次网格更新，并生成 `summary.json` 和热力图。性能会随着设备、驱动、输入规模和运行次数变化，文章只把这次结果作为可复核示例。

![heatdiffusion_whiteboard](https://ygj-images1.oss-cn-hangzhou.aliyuncs.com/images/heatdiffusion_whiteboard.png)

### 7.3 VisualInspection

VisualInspection 使用四组测试图片，把 OpenCV CPU 掩码、GPU 掩码和期望掩码逐项比较：

```text
Result: PASSED
Fixtures passed: 4/4
GPU end-to-end median: 0.07 ms
IoU: 1
Maximum pixel difference: 0
```

程序还会生成 `inspection-summary.json`、`inspection-results.csv` 和 GPU 掩码 PNG。

![visualinspection_whiteboard](https://ygj-images1.oss-cn-hangzhou.aliyuncs.com/images/visualinspection_whiteboard.png)

这两个案例说明，库的价值不止是“Kernel 能启动”，而是能够把 GPU 计算放进一个有输入、有参考结果、有输出文件和有失败诊断的 .NET 应用中。

## 八、这个项目是怎样走到今天的

这个项目的起点并不是“把 HIP 函数翻译成 C#”这么简单。真正开始做以后，很快会遇到几个连续的问题：原生 ABI 如何保持准确，旧 .NET Framework 和现代 .NET 如何共享同一套接口，SafeHandle 和异步 GPU 资源如何协作，HIPRTC 的编译日志和指针生命周期如何安全地进入托管层。

我选择从 AMD HIP 的 C ABI 出发，是因为这条边界足够清晰，也更容易审计和复现。然后在上面逐步建立托管 owner、异常类型、加载诊断和示例程序。每增加一层能力，都需要同时考虑资源所有权、失败路径和不同 Runtime 来源，而不是只让正常路径跑通。

项目也经历过从本地构建到云端真实 GPU 的反复验证：先确认 Core 包能还原和加载，再验证 Runtime 包的依赖闭包，最后在 Ubuntu 24.04、ROCm 7.2.1 和 `gfx1100` 环境中运行完整案例。今天发布的内容，是这条路径已经能够被其他开发者安装、运行和复核后的结果。

这也是我希望通过第一次发布传达的事情：先把一条真实可用的路走通，再和社区一起扩大设备、平台、案例和接口覆盖，而不是用一张很大的 API 清单提前承诺所有场景。

## 九、边界与项目入口

### 9.1 当前边界

- Core 包和 Runtime 包都不会安装 AMD 驱动、内核模块或完整 ROCm SDK。
- Ubuntu Runtime 包面向 Ubuntu 24.04 x64；其他发行版、GPU 和 ROCm 组合需要重新验证。
- Windows x64 已完成部分构建和加载检查，但尚未完成 Windows AMD GPU 实机验证。
- ROCm 7.2.1 中记录的 `hipMemRetainAllocationHandle` 上游引用计数问题，仍应根据目标构建和使用场景谨慎评估。

### 9.2 项目入口

| 资源 | 地址 |
| --- | --- |
| GitHub 仓库 | https://github.com/guojin-yan/HIP-CSharp-API |
| Core NuGet | https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API/0.10.0 |
| Ubuntu 24.04 Runtime NuGet | https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64/7.2.1 |
| Ubuntu Runtime GitHub Release | https://github.com/guojin-yan/HIP-CSharp-API/releases/tag/runtime-ubuntu.24.04-v7.2.1 |
| 综合案例索引 | https://github.com/guojin-yan/HIP-CSharp-API/tree/v0.10.0/samples/showcases |
| Radeon Cloud | https://developer.amd.com.cn/login?source=J2RtJQRgM |
| Issues | https://github.com/guojin-yan/HIP-CSharp-API/issues |
| Discussions | https://github.com/guojin-yan/HIP-CSharp-API/discussions |

## 十、总结

这次首发先把 C# 调用 AMD GPU 的主路径走通：从设备与运行时加载，到 HIPRTC Kernel，再到可复核的应用结果。

本地有 AMD GPU，可以先安装 NuGet 包并运行最小案例；手上暂时没有 AMD GPU，可以在 Radeon Cloud 中体验完整案例。有关架构、异步资源、HIPRTC 和 Runtime 供应链的细节，后续会分别展开。

这个项目仍然需要更多设备、平台和真实用户反馈。欢迎大家先运行一个案例，再把环境信息、输出摘要和最小复现分享回来。

## 十一、文章声明

- **开源协议：** 项目源码采用 Apache License 2.0；打包的 ROCm 组件保留各自的许可证和通知，项目许可证不替代第三方条款。
- **AI 辅助开发：** 项目开发、测试和文档编写过程中使用了人工智能辅助，最终内容仍需由维护者复核。
- **质量与测试：** 本文重点描述安装、运行和可复核结果，不把单次云端运行扩展为全平台支持承诺。
- **平台限制：** 文中结果来自 Ubuntu 24.04、ROCm 7.2.1 和 `gfx1100` 环境，其他平台和设备必须重新验证。
- **供应商依赖：** AMD 驱动、ROCm 组件、GPU 设备和 NuGet.org 服务分别受其自身版本、许可证和服务条款约束。
- **免责声明：** 在商业、工业或关键任务环境使用前，请自行完成完整测试、故障恢复和供应链审计。
- **社区反馈：** 欢迎通过 GitHub Issues、Discussions 或 QQ 群 `945057948` 提交可公开的环境信息、最小复现和改进建议。

![personal-contact-banner-v6-zh](https://ygj-images1.oss-cn-hangzhou.aliyuncs.com/images/personal-contact-banner-v6-zh.png)

Copyright (c) 2026 Guojin Yan.
