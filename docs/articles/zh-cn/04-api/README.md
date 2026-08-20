# HIP-CSharp-API 接口使用

本模块按公开对象职责、native 边界、资源所有权和验证输出解释 API。它不是对 HIP 头文件的逐项翻译；每篇文章必须指出实际 EntryPoint、最低 HIP/ROCm 版本、同步语义、错误处理和释放者。

## 接口分组

| 子目录 | 主要主题 |
| --- | --- |
| `runtime` | `HipRuntime`、加载器、错误码、设备枚举和诊断 |
| `memory` | Device、Pinned、Managed、Pitched、Pool、Virtual Memory |
| `execution` | Stream、Event、Graph、Kernel、Occupancy |
| `rtc` | HIPRTC Program、编译结果、Linker 和 code object |
| `advanced` | Module globals、Array/Texture/Surface、P2P 和扩展能力 |

## 规划文章

| ID | 主题 | 状态 |
| --- | --- | --- |
| `API-001` | Runtime 初始化、动态库加载和错误边界 | `planned` |
| `API-002` | Device 枚举、属性、版本和能力查询 | `planned` |
| `API-003` | Device/Pinned/Managed Memory 所有权 | `planned` |
| `API-004` | 同步内存复制、Pitched Memory 与 2D/3D Copy | `planned` |
| `API-005` | Stream、Event 和异步生命周期 | `planned` |
| `API-006` | Module、Kernel 参数和 Launch Dimensions | `planned` |
| `API-007` | HIPRTC Program 与 Linker：从源码到可加载 Kernel | `draft` |
| `API-008` | HIPRTC Linker 与 code object 加载 | `planned` |
| `API-009` | Memory Pool、异步分配和 P2P | `planned` |
| `API-010` | Explicit Graph、Capture 和 GraphExec | `planned` |
| `API-011` | Occupancy、Cooperative Launch 和能力门控 | `planned` |
| `API-012` | Module globals、Array、Texture 和 Surface | `planned` |
