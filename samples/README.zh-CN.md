# 案例

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

教程案例按 HIP 功能模块组织，学习时建议按编号依次进入；`showcases` 将多个模块组合成完整任务；
`validation` 只包含 GPU、Runtime 和包门禁工作负载，不属于初学者教程主线。

完整的云端可复现教程矩阵请从 [`tutorials/README.zh-CN.md`](tutorials/README.zh-CN.md) 开始。

所有案例均使用显式 `Program.Main(string[] args)` 入口；参数解析、受控 `Skipped` 结果和进程退出码
均由该方法负责。

## 推荐学习路径

| 模块 | 内容 | 入口 |
| --- | --- | --- |
| 01 Runtime/Device | 初始化、版本、设备和加载错误 | [`EnvironmentAndDevice`](tutorials/01-RuntimeDevice/EnvironmentAndDevice) |
| 02 Memory | 线性、锁页、Pitched、Managed、内存池和虚拟内存 | [`LinearMemoryCopy`](tutorials/02-Memory/LinearMemoryCopy) |
| 03 Execution | Stream、Event 和异步顺序 | [`StreamAndEvent`](tutorials/03-Execution/StreamAndEvent) |
| 04 Kernel | HIPRTC、Module 和 Kernel 启动 | [`HipRtcVectorAdd`](tutorials/04-Kernel/HipRtcVectorAdd) |
| 05 Graph | Graph 捕获与重放 | [`GraphCaptureReplay`](tutorials/05-Graph/GraphCaptureReplay) |
| 06 Multi-device | 有方向的 peer access 和 P2P copy | [`PeerToPeerCopy`](tutorials/06-MultiDevice/PeerToPeerCopy) |
| 07 Data objects | Array、Texture 和 Surface | [`ArrayTextureSurface`](tutorials/07-DataObjects/ArrayTextureSurface) |
| 90 Low-level | 面向专家的生成式 C ABI | [`NativeAbiInterop`](tutorials/90-LowLevel/NativeAbiInterop) |

## 综合案例

| 案例 | 工作负载 | 入口 |
| --- | --- | --- |
| HeatDiffusion | CPU/GPU 二维热扩散模拟、当前会话性能测量和 BMP 热力图 | [`HeatDiffusion`](showcases/HeatDiffusion/README.zh-CN.md) |
| VisualInspection | OpenCV CPU 参考与 HIPRTC AMD GPU 缺陷掩码流水线，输出 PNG/JSON/CSV 证据 | [`VisualInspection`](showcases/VisualInspection/README.zh-CN.md) |

所有 GPU 案例都需要兼容的 AMD 驱动和 HIP Runtime。依赖可选 Runtime 或设备能力的案例会输出
受控 `Skipped`。教程案例用于正确性演示，不是性能基准；`HeatDiffusion` 单独输出仅适用于当前
会话的 CPU/GPU 测量结果。
