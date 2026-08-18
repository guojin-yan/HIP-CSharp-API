# Samples / 案例

The samples are organized by HIP capability family. Follow the numbered modules in order when
learning the API. The `validation` directory contains GPU and package gate workloads and is not part
of the beginner tutorial path.

案例按 HIP 功能模块组织。学习时建议按编号依次进入；`validation` 目录只包含 GPU、Runtime 和包
门禁工作负载，不属于初学者教程主线。

## Recommended path / 推荐路径

| Module | Focus | Entry point |
| --- | --- | --- |
| 01 Runtime/Device | Initialization, versions, devices, loader errors | [`EnvironmentAndDevice`](tutorials/01-RuntimeDevice/EnvironmentAndDevice) |
| 02 Memory | Linear, pinned, pitched, managed, pooled, and virtual memory | [`LinearMemoryCopy`](tutorials/02-Memory/LinearMemoryCopy) |
| 03 Execution | Streams, events, and asynchronous order | [`StreamAndEvent`](tutorials/03-Execution/StreamAndEvent) |
| 04 Kernel | HIPRTC, modules, and kernel launch | [`HipRtcVectorAdd`](tutorials/04-Kernel/HipRtcVectorAdd) |
| 05 Graph | Capture and replay | [`GraphCaptureReplay`](tutorials/05-Graph/GraphCaptureReplay) |
| 06 Multi-device | Directed peer access and P2P copy | [`PeerToPeerCopy`](tutorials/06-MultiDevice/PeerToPeerCopy) |
| 07 Data objects | Arrays, textures, and surfaces | [`ArrayTextureSurface`](tutorials/07-DataObjects/ArrayTextureSurface) |
| 90 Low-level | Generated C ABI for expert use | [`NativeAbiInterop`](tutorials/90-LowLevel/NativeAbiInterop) |

All GPU cases require a compatible AMD driver and HIP Runtime. Cases that depend on optional runtime
or device capabilities report a controlled `Skipped` result. These programs are correctness examples,
not performance benchmarks.
