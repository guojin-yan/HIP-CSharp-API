# HIP-CSharp-API 源码编译

本模块面向需要参与开发、检查 ABI 或构建本地包的读者。文章必须给出固定 SDK/工具版本、精确命令、失败边界和清理范围；构建成功不等于 GPU 支持或公开发布。

## 规划文章

| ID | 子系列 | 主题 | 状态 |
| --- | --- | --- | --- |
| `BLD-001` | `managed` | 全 TFM 托管层构建、测试和 DocFX | `planned` |
| `BLD-002` | `bindings` | HIP 头文件 Manifest、AST 绑定生成和确定性差异 | `planned` |
| `BLD-003` | `bindings` | ABI probe、sizeof/offsetof/enum 和导出符号审计 | `planned` |
| `BLD-004` | `native` | 可选 shim、CMake 和原生调试边界 | `planned` |
| `BLD-005` | `runtime` | Linux/Windows Runtime 包拆分、许可证和 SBOM | `planned` |
| `BLD-006` | `ci` | CI 分层、云端门禁和可复现构建证据 | `planned` |

