# Samples / 案例

案例按 HIP 功能模块组织。学习时建议按编号依次进入；`validation` 目录只包含 GPU、Runtime 和包
门禁工作负载，不属于初学者教程主线。

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

所有 GPU 案例都需要兼容的 AMD 驱动和 HIP Runtime。依赖可选 Runtime 或设备能力的案例会输出
受控 `Skipped`，这些程序用于正确性演示，不是性能基准。
