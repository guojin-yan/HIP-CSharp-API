# Linux ROCm 7.2.1 Runtime 包的职责与安装边界

> 文章 ID：`REL-003`  
> 对应版本：`7.2.1`  
> 文章记录日期：2026-08-19  
> 内容状态：`ready`，尚未发布到外部平台

## 前言

`JYPPX.ROCm.HIP.CSharp.API` 是 HIP-CSharp-API 的托管 Core 包。它不包含 AMD 驱动、内核模块或完整 ROCm SDK；Linux 上的应用可以使用系统安装的 ROCm，也可以安装项目发布的 Ubuntu 24.04 用户态 Runtime 包：

```text
https://www.nuget.org/packages/JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64/7.2.1
```

本文说明该包的适用环境、安装方式、验证证据和不包含的内容。项目源码位于 `https://github.com/guojin-yan/HIP-CSharp-API`。

## 一、包身份和适用范围

包 ID 为 `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64`，版本为 `7.2.1`。它是 Ubuntu 24.04 x64 专用的 ROCm/HIP 用户态依赖闭包；包内原生资产采用 NuGet 可识别的 `runtimes/linux-x64/native/` 路径，但这不把它扩展为所有 Linux 发行版的通用 Runtime。

应用仍须由宿主提供兼容的 AMD 驱动、内核模块、`/dev/kfd`、`/dev/dri`、GPU 硬件和基础系统库。该包不重新分发这些组件，也不替代完整 ROCm SDK。

历史通用包 `JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` 与本包不是同一发行物。新的消费和部署应使用 Ubuntu 24.04 专用包；旧包的取消列出需要其 NuGet owner 凭据，不能据此推断新包不可用。

## 二、安装和加载边界

在 Ubuntu 24.04 x64 的 .NET 项目中，先安装 Core 包，再按需安装 Runtime 包：

```bash
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.10.0
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64 --version 7.2.1
```

若主机已经有兼容的系统 ROCm 用户态库，可以只安装 Core 包。无论选择哪种用户态库来源，首次运行前都应在目标设备检查 GPU、设备节点和实际架构；示例中的 `gfx1100` 只是已验证设备的架构，不能替代目标设备检测。

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
```

包还原成功只证明 NuGet 资产可用，不能单独证明动态库加载、GPU 运算或其他 ROCm/GPU 组合已经得到支持。

## 三、发布和验证证据

该包对应提交 `3d10727814823e2ab584e1664fda97fd748a9966` 和 annotated tag `runtime-ubuntu.24.04-v7.2.1`。上传 NuGet.org 前的精确构建包为 `162894000` bytes，SHA-256 为 `eb344fc68859754dbf721d89c6a99525e9b1b90f4828282277cda4988c2bc9c3`。

NuGet.org 为公开包附加 repository signature，因此公开下载包的大小为 `162907087` bytes，SHA-256 为 `1b8463fccbc364b9e0b7a1bc58cfc76c7ecfc8f73f1e3eaac1bdfadb197dc23d`。GitHub Release 附件与公开 NuGet 包一致：

```text
https://github.com/guojin-yan/HIP-CSharp-API/releases/tag/runtime-ubuntu.24.04-v7.2.1
```

Radeon Cloud Ubuntu 24.04 PRoot 隔离 runtime gate（M8.8）已针对该精确提交和上传前包通过。验证覆盖 package-only consumer、运行时依赖闭包混装拒绝、篡改拒绝和重复压力运行。该结果只适用于所记录的 Ubuntu 24.04、ROCm 7.2.1、`gfx1100` 环境和测试批次，不构成其他发行版、Windows、其他 GPU 或性能承诺。

公开包可使用 NuGet CLI 验证 repository signature：

```bash
dotnet nuget verify JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64.7.2.1.nupkg --all
```

成功结果应显示 `Repository` 签名和 `NuGet.org Repository by Microsoft`；仍应在目标环境执行应用自己的加载和 GPU 功能测试。

## 四、已知限制

- 包仅面向 Ubuntu 24.04 x64；系统库、驱动和 GPU 架构仍由部署环境决定。
- Core 包和 Runtime 包都不会安装 AMD 驱动，也不能使没有兼容 AMD GPU 的主机获得 GPU 功能。
- Windows AMD GPU 的实际验证不属于本包的结论，也不构成任何 `1.0.0` 发布条件的完成。
- ROCm 7.2.1 中已记录的 `hipMemRetainAllocationHandle` 上游引用计数问题仍需依据目标 ROCm 构建和上游修复状态评估。

## 五、文章声明

- **开源协议：** 项目源码采用 Apache License 2.0；打包的 ROCm 组件保留各自的许可证和通知，项目许可证不替代第三方条款。
- **AI 辅助开发：** 项目开发、测试和文档编写过程中使用了人工智能辅助，最终内容仍需由维护者复核。
- **质量与测试：** 本文区分包还原、签名验证、隔离 Runtime gate 和真实 GPU 功能验证；没有将其中任一项单独扩展为全平台支持声明。
- **平台限制：** 本文记录 Ubuntu 24.04 x64、ROCm 7.2.1 和 `gfx1100` 的证据边界，其他平台和设备必须重新验证。
- **供应商依赖：** AMD 驱动、ROCm 组件、GPU 设备和 NuGet.org repository signing 分别受其自身版本、许可证和服务条款约束。
- **免责声明：** 在商业、工业或关键任务环境使用前，请自行完成完整测试、故障恢复和供应链审计。
- **社区反馈：** 欢迎通过 GitHub Issues 或 Discussions 提交可公开的环境信息、最小复现和改进建议；请勿提交凭据、私有日志或受限制二进制。
