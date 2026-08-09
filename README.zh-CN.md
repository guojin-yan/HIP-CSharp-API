# HIP-CSharp-API

HIP-CSharp-API 计划为 AMD HIP 的 Direct C ABI 提供 .NET 绑定。托管层 `JYPPX.HipSharp` 与原生声明层 `JYPPX.HipSharp.Native` 分离，便于分别验证框架兼容、加载诊断、资源所有权和包职责。

## M0 状态

`0.0.0-preview.1` 只是本地工程候选包，不代表已发布到 nuget.org。M0 完成仓库、15 个目标框架构建、互操作声明分流、包审计、干净消费者检查和 CI 基线；没有实现 `hipInit`、`hipMalloc`、HIPRTC、动态加载器或任何 GPU 操作。

- Build：两个核心程序集均可构建全部 15 个 TFM。
- Package：本地核心包每个 TFM 同时携带两个程序集及 XML 文档。
- Runtime-tested：未执行，没有加载 HIP 库。
- GPU-validated：未执行，本机没有 AMD GPU。
- Supported：尚未对任何 runtime、操作系统或 GPU 组合作支持承诺。

首个计划验证基线是 Radeon Cloud 的 Ubuntu 24.04、ROCm 7.2.1、HIP 7.2 和 `gfx1100`。Windows HIP SDK 兼容设计已保留，但尚无 AMD GPU 实测。`.NET Core 3.1`、`.NET 5/6/7`、`.NET Framework 4.6/4.6.1` 已经 EOL，仅作为构建和包兼容目标。

核心包 `JYPPX.HIP.CSharp.API` 默认不包含 ROCm 或 AMD 原生二进制。两个 runtime 包候选的 manifest 明确禁用；依赖闭包、逐组件许可证、官方来源与哈希、体积和干净 GPU 验证完成前，不会生成伪 runtime 包。

本地验证入口见英文 [README](README.md)。源码按总体方案的默认建议准备为 Apache-2.0；ROCm 组件未来仍须保留各自许可证和 NOTICE。
