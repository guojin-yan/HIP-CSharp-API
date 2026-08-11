# HipSharp API Documentation / API 文档

HipSharp wraps AMD HIP Runtime and HIPRTC Direct C ABIs for .NET. The managed-only `0.0.0` candidate includes selected M6 stream-ordered allocation, managed-memory, P2P, and graph APIs. M5's audited Linux ROCm 7.2.1 runtime package remains guarded and unpublished; M6 official-header ABI, advanced GPU, and package-only regression passed on an Owner-authorized Radeon Cloud session. Windows remains static-only and GPU-unvalidated.

HipSharp 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 封装。managed-only `0.0.0` 候选包含 M6 选择的 stream-ordered allocation、managed memory、P2P 和 graph API。M5 已审计的 Linux ROCm 7.2.1 runtime package 继续受控且未发布；M6 官方 header ABI、高级 GPU 和 package-only regression 已在 Owner 授权的 Radeon Cloud 会话通过。Windows 仍为 static-only，未经过 GPU 验证。

## Documentation / 文档

- [Framework compatibility / 框架兼容性](compatibility/frameworks.md)
- [Platform compatibility / 平台兼容性](compatibility/platforms.md)
- [0.0.0 engineering baseline / 0.0.0 工程基线](releases/0.0.0.md)
- [HIPRTC VectorAdd guide / HIPRTC VectorAdd 指南](guides/hiprtc-vectoradd.md)
- [Linux runtime package audit / Linux runtime 包审计](guides/linux-runtime-package.md)
- [Advanced HIP APIs / HIP 高级 API](guides/advanced-apis.md)
- [Windows runtime static audit / Windows Runtime 静态审计](guides/windows-runtime-static-audit.md)
- [API reference / API 参考](xref:JYPPX.HipSharp)
