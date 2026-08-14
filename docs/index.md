# HipSharp API Documentation / API 文档

HipSharp wraps AMD HIP Runtime and HIPRTC Direct C ABIs for .NET. Published Core `0.9.0` has an assembly-identity naming defect and should not be adopted. The `0.9.1` forward-fix candidate aligns the package and assembly identity as `JYPPX.ROCm.HIP.CSharp.API` while keeping public APIs under `JYPPX.ROCm.HipSharp`; it requires fresh exact-package validation and Owner publication authorization. Windows remains static-only and GPU-unvalidated.

HipSharp 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 封装。已发布 Core `0.9.0` 存在程序集 identity 命名缺陷，不应采用。`0.9.1` forward-fix 候选将包与程序集 identity 统一为 `JYPPX.ROCm.HIP.CSharp.API`，公开 API 仍位于 `JYPPX.ROCm.HipSharp`；它需要新的 exact-package 验证和 Owner 发布授权。Windows 仍为 static-only，未经过 GPU 验证。

## Documentation / 文档

- [Framework compatibility / 框架兼容性](compatibility/frameworks.md)
- [Platform compatibility / 平台兼容性](compatibility/platforms.md)
- [0.9.0 release candidate / 0.9.0 发布候选](releases/0.9.0-rc.md)
- [0.9.1 assembly identity forward fix / 0.9.1 程序集 identity 修正](releases/0.9.1-forward-fix.md)
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
