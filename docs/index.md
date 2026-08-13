# HipSharp API Documentation / API 文档

HipSharp wraps AMD HIP Runtime and HIPRTC Direct C ABIs for .NET. Managed-only Core `0.9.0` is a local API-freeze release candidate, not a published stable release. The M8.7 exact Core/Runtime candidate passed official-host and package-only validation; M8.8 imports that result through a deterministic receipt and payload-equivalence gate. Newly built final package bytes still require a newly Owner-authorized final-mode recheck. Windows remains static-only and GPU-unvalidated.

HipSharp 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 封装。managed-only Core `0.9.0` 是本地 API 冻结发布候选，不是已发布稳定版。M8.7 精确 Core/Runtime 候选已通过 official-host 与 package-only 验证；M8.8 通过确定性 receipt 与 payload 等价门禁导入该结论。新生成的 final 包字节仍需 Owner 新授权的 final-mode 复核。Windows 仍为 static-only，未经过 GPU 验证。

## Documentation / 文档

- [Framework compatibility / 框架兼容性](compatibility/frameworks.md)
- [Platform compatibility / 平台兼容性](compatibility/platforms.md)
- [0.9.0 release candidate / 0.9.0 发布候选](releases/0.9.0-rc.md)
- [Manual publishing checklist / 人工发布清单](releases/publishing-checklist.md)
- [0.9 API freeze review / 0.9 API 冻结审查](guides/api-freeze.md)
- [JYPPX ROCm naming migration / JYPPX ROCm 命名迁移](design/jyppx-rocm-naming-migration.md)
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
