# HIP-CSharp-API 完整应用工作流

本模块描述跨越环境、包、托管 API、GPU 执行和结果校验的完整工作流。它不把单个 API 调用或本地 build-only 结果写成应用成功，也不把源码样例自动描述成已发布应用。

## 规划文章

| ID | 子系列 | 主题 | 状态 |
| --- | --- | --- | --- |
| `APP-001` | `workflows` | 从 C# HIP 源码到 HIPRTC VectorAdd 的完整闭环 | `planned` |
| `APP-002` | `workflows` | DeviceInfo、MemoryCopy、Stream/Event 的组合验证 | `planned` |
| `APP-003` | `package-consumer` | 从 NuGet clean consumer 到 GPU Smoke 的包消费流程 | `planned` |
| `APP-004` | `package-consumer` | Runtime 包依赖闭包、加载诊断和结果复核 | `planned` |

