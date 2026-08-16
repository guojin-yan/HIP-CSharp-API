# HipSharp API Documentation / API 文档

HipSharp wraps AMD HIP Runtime and HIPRTC Direct C ABIs for .NET. Core `0.9.1` and optional Linux Runtime `7.2.1` are published and passed fresh nuget.org-only static plus package-only GPU/ABI validation. The source tree validates an unpublished Core `0.9.2` interface-ledger batch with the same frozen public surface. Windows AMD GPU validation and an explicit Owner request remain mandatory before any future `1.0.0` release.

HipSharp 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 封装。Core `0.9.1` 与可选 Linux Runtime `7.2.1` 已公开，并通过 fresh nuget.org-only 静态及 package-only GPU/ABI 验证。当前源码验证公开 surface 不变的 Core `0.9.2` 逐接口账本批次。未来任何 `1.0.0` 仍必须同时取得 Windows AMD GPU 实机验证和 Owner 明确发布指令。

## Documentation / 文档

- [Framework compatibility / 框架兼容性](compatibility/frameworks.md)
- [Platform compatibility / 平台兼容性](compatibility/platforms.md)
- [Historical 0.9.0 assembly incident / 0.9.0 程序集事故历史](releases/0.9.0-rc.md)
- [0.9.1 assembly identity forward fix / 0.9.1 程序集 identity 修正](releases/0.9.1-forward-fix.md)
- [0.9.2 interface ledger validation / 0.9.2 逐接口账本验证](releases/0.9.2-interface-ledger.md)
- [1.0.0 candidate notes / 1.0.0 候选说明](releases/1.0.0.md)
- [1.0.0 readiness matrix / 1.0.0 就绪矩阵](releases/1.0.0-readiness.md)
- [M8.10 controlled cleanup / M8.10 受控清理](releases/1.0.0-cleanup.md)
- [1.0.0 manual publishing checklist / 1.0.0 人工发布清单](releases/publishing-checklist.md)
- [1.0 API freeze review / 1.0 API 冻结审查](guides/api-freeze.md)
- [JYPPX ROCm naming migration / JYPPX ROCm 命名迁移](design/jyppx-rocm-naming-migration.md)
- [MIGraphX adapter pending-lease boundary / MIGraphX 适配器 pending 租约边界](design/migraphx-adapter-pending-lease.md)
- [HIPRTC VectorAdd guide / HIPRTC VectorAdd 指南](guides/hiprtc-vectoradd.md)
- [Linux runtime package audit / Linux runtime 包审计](guides/linux-runtime-package.md)
- [Advanced HIP APIs / HIP 高级 API](guides/advanced-apis.md)
- [Explicit HIP graphs / 显式 HIP Graph](guides/explicit-graphs.md)
- [Kernel occupancy and cooperative launch / Kernel Occupancy 与 Cooperative Launch](guides/kernel-occupancy.md)
- [Managed module globals / 托管 Module 全局符号](guides/module-globals.md)
- [Managed expansion validation / 高层托管扩展验证](guides/managed-expansion-validation.md)
- [Pitched memory and 2D/3D copy / Pitched memory 与 2D/3D copy](guides/pitched-memory.md)
- [Managed memory pools / 托管 Memory Pool](guides/memory-pools.md)
- [Windows runtime static audit / Windows Runtime 静态审计](guides/windows-runtime-static-audit.md)
- [API reference / API 参考](xref:JYPPX.ROCm.HipSharp)
