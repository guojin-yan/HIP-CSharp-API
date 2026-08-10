# HIP-CSharp-API

HIP-CSharp-API 为 AMD HIP Runtime 的基础 Direct C ABI 提供 .NET 绑定。项目只发布 `JYPPX.HipSharp` 一个程序集；原生声明作为 internal 代码保留在 `Interop`、`Types`、`Loading` 和 `Generated` 目录，公开托管 API 覆盖 runtime、设备、错误和设备内存。

## M1 状态

`0.0.0` 只是本地工程候选包，不代表已发布到 nuget.org。从 `0.0.0` 开始的版本表示预览开发，不再追加 prerelease 后缀。M1 已从单一 manifest 实现 `amdhip64` 的第一条纵向链路：初始化、runtime/驱动版本、设备枚举与切换、设备名称、同步内存分配/复制/释放、同步和原生错误诊断。HIPRTC、Module、Kernel、Stream 和复杂设备属性不在本阶段范围内。

- Build：核心程序集、公开 API 及 XML 文档可构建全部 15 个 TFM。
- Package：本地核心包和代表性干净消费者会执行回归验证。
- Runtime-tested：已在 Radeon Cloud 的 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 环境通过。
- GPU-validated：已在一个 `gfx1100` AMD Radeon Graphics 实例完成设备枚举、分配、H2D/D2D/D2H、同步和释放。
- Supported：尚未对任何 runtime、操作系统或 GPU 组合作支持承诺。

首个硬件验证基线已在 Radeon Cloud 的 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 和 `gfx1100` 上通过。该结果是单一环境的验证证据，不等同于广泛支持声明。Windows HIP SDK 兼容设计已保留，但尚无 AMD GPU 实测。`.NET Core 3.1`、`.NET 5/6/7`、`.NET Framework 4.6/4.6.1` 已经 EOL，仅作为构建和包兼容目标。

核心包 `JYPPX.HIP.CSharp.API` 默认不包含 ROCm 或 AMD 原生二进制。runtime 包 ID 固定为 `JYPPX.HipSharp.Runtime.linux-x64` 和 `JYPPX.HipSharp.Runtime.win-x64`，包版本使用对应 ROCm 版本。两个候选 manifest 仍明确禁用；依赖闭包、逐组件许可证、官方来源与哈希、体积和干净 GPU 验证完成前，不会生成伪 runtime 包。

`samples/DeviceInfo` 与 `samples/MemoryCopy` 展示 M1 的两条 GPU 工作流。所有公开 API XML 注释采用中文/英文双语格式。运行 `./eng/docs.ps1` 可在 `_site` 生成 DocFX API 文档站点。

本地验证入口见英文 [README](README.md)。源码按总体方案的默认建议准备为 Apache-2.0；ROCm 组件未来仍须保留各自许可证和 NOTICE。
