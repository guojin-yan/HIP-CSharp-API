# HIP-CSharp-API

HIP-CSharp-API 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 绑定。项目只发布 `JYPPX.HipSharp` 一个程序集；原生声明保持 internal，公开托管 API 覆盖 runtime、设备、内存、运行时编译、module 与 kernel launch。

## M2 状态

`0.0.0` 只是本地工程候选包，不代表已发布到 nuget.org。从 `0.0.0` 开始的版本表示预览开发，不再追加 prerelease 后缀。M2 在同一 manifest 中加入 HIPRTC 编译/日志/code object 与 HIP Runtime module/function/launch 声明，并实现 program、module 所有权及设备指针/32 位标量的 `void**` 参数封送。

- Build：核心程序集、公开 API 及 XML 文档可构建全部 15 个 TFM。
- Package：本地核心包和代表性干净消费者会执行回归验证。
- M1 Runtime-tested：已在 Radeon Cloud 的 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 环境通过。
- M1 GPU-validated：已在一个 `gfx1100` 实例完成设备枚举、分配、H2D/D2D/D2H、同步和释放。
- M2 GPU-validated：等待新授权的 Radeon Cloud 会话；目前不声称 HIPRTC 编译与 VectorAdd 已通过真实硬件验证。
- Supported：尚未对任何 runtime、操作系统或 GPU 组合作支持承诺。

M1 硬件验证基线已在 Radeon Cloud 的 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 和 `gfx1100` 上通过；它不能替代 M2 的新授权验证。Windows HIP SDK 兼容设计已保留，但尚无 AMD GPU 实测。`.NET Core 3.1`、`.NET 5/6/7`、`.NET Framework 4.6/4.6.1` 已经 EOL，仅作为构建和包兼容目标。

核心包 `JYPPX.HIP.CSharp.API` 默认不包含 ROCm 或 AMD 原生二进制。runtime 包 ID 固定为 `JYPPX.HipSharp.Runtime.linux-x64` 和 `JYPPX.HipSharp.Runtime.win-x64`，包版本使用对应 ROCm 版本。两个候选 manifest 仍明确禁用；依赖闭包、逐组件许可证、官方来源与哈希、体积和干净 GPU 验证完成前，不会生成伪 runtime 包。

`samples/HipRtcVectorAdd` 在内存中编译 HIP C++、加载 code object、经 `kernelParams` 启动、同步、复制回 CPU 并逐项验证；它要求显式提供 GPU 架构，不会把 code object 写入磁盘。`samples/DeviceInfo` 与 `samples/MemoryCopy` 保留 M1 工作流。所有公开 API XML 注释采用中文/英文双语格式。运行 `./eng/docs.ps1` 可在 `_site` 生成 DocFX API 文档站点。

本地验证入口见英文 [README](README.md)。源码按总体方案的默认建议准备为 Apache-2.0；ROCm 组件未来仍须保留各自许可证和 NOTICE。
