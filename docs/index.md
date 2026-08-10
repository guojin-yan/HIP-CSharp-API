# HipSharp API Documentation / API 文档

HipSharp wraps AMD HIP Runtime and HIPRTC Direct C ABIs for .NET. The managed-only `0.0.0` candidate covers runtime, device, memory, stream/event, runtime compilation, module, and kernel APIs. M5 adds an audited, guarded local Linux ROCm 7.2.1 runtime package that passed one isolated `gfx1100` environment; it is not published.

HipSharp 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 封装。managed-only `0.0.0` 候选覆盖 runtime、设备、内存、stream/event、运行时编译、module 与 kernel API；M5 新增已审计并在一个隔离 `gfx1100` 环境通过的 Linux ROCm 7.2.1 受控本地 runtime package，尚未发布。

## Documentation / 文档

- [Framework compatibility / 框架兼容性](compatibility/frameworks.md)
- [Platform compatibility / 平台兼容性](compatibility/platforms.md)
- [0.0.0 engineering baseline / 0.0.0 工程基线](releases/0.0.0.md)
- [HIPRTC VectorAdd guide / HIPRTC VectorAdd 指南](guides/hiprtc-vectoradd.md)
- [Linux runtime package audit / Linux runtime 包审计](guides/linux-runtime-package.md)
- [API reference / API 参考](xref:JYPPX.HipSharp)
