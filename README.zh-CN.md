# HIP-CSharp-API

HIP-CSharp-API 计划为 AMD HIP 的 Direct C ABI 提供 .NET 绑定。项目只发布 `JYPPX.HipSharp` 一个程序集；原生声明作为 internal 代码保留在 `Interop`、`Types`、`Loading` 和 `Generated` 目录，托管 API 按功能领域组织。

## M0 状态

`0.0.0` 只是本地工程候选包，不代表已发布到 nuget.org。从 `0.0.0` 开始的版本表示预览开发，不再追加 prerelease 后缀。M0 完成仓库、15 个目标框架构建、互操作声明分流、包审计、干净消费者检查和 CI 基线；没有实现 `hipInit`、`hipMalloc`、HIPRTC、动态加载器或任何 GPU 操作。

- Build：核心程序集及 XML 文档可构建全部 15 个 TFM。
- Package：本地核心包每个 TFM 携带一个程序集及对应 XML 文档。
- Runtime-tested：未执行，没有加载 HIP 库。
- GPU-validated：未执行，本机没有 AMD GPU。
- Supported：尚未对任何 runtime、操作系统或 GPU 组合作支持承诺。

首个计划验证基线是 Radeon Cloud 的 Ubuntu 24.04、ROCm 7.2.1、HIP 7.2 和 `gfx1100`。Windows HIP SDK 兼容设计已保留，但尚无 AMD GPU 实测。`.NET Core 3.1`、`.NET 5/6/7`、`.NET Framework 4.6/4.6.1` 已经 EOL，仅作为构建和包兼容目标。

核心包 `JYPPX.HIP.CSharp.API` 默认不包含 ROCm 或 AMD 原生二进制。runtime 包 ID 固定为 `JYPPX.HipSharp.Runtime.linux-x64` 和 `JYPPX.HipSharp.Runtime.win-x64`，包版本使用对应 ROCm 版本。两个候选 manifest 仍明确禁用；依赖闭包、逐组件许可证、官方来源与哈希、体积和干净 GPU 验证完成前，不会生成伪 runtime 包。

所有 API XML 注释采用中文/英文双语格式。运行 `./eng/docs.ps1` 可在 `_site` 生成 DocFX 文档站点。

本地验证入口见英文 [README](README.md)。源码按总体方案的默认建议准备为 Apache-2.0；ROCm 组件未来仍须保留各自许可证和 NOTICE。
