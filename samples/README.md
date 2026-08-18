# Samples

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

The tutorial samples are organized by HIP capability family. Follow the numbered modules in order
when learning the API. `showcases` combines multiple modules into complete workloads, while
`validation` contains GPU and package gates that are not part of the beginner tutorial path.

For a complete cloud-reproducible tutorial matrix, start with [`tutorials/README.md`](tutorials/README.md).

Every sample uses an explicit `Program.Main(string[] args)` entry point. Argument parsing, controlled
`Skipped` results, and process exit codes are kept in that method.

## Recommended path

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

## Integrated showcase

| Showcase | Workload | Entry point |
| --- | --- | --- |
| HeatDiffusion | CPU/GPU two-dimensional heat simulation with per-run measurements and a BMP heatmap | [`HeatDiffusion`](showcases/HeatDiffusion) |
| VisualInspection | OpenCV CPU reference and HIPRTC AMD GPU defect-mask pipeline with PNG/JSON/CSV evidence | [`VisualInspection`](showcases/VisualInspection) |

All GPU cases require a compatible AMD driver and HIP Runtime. Cases that depend on optional runtime
or device capabilities report a controlled `Skipped` result. Tutorial cases are correctness examples,
not performance benchmarks. `HeatDiffusion` separately reports measurements scoped to the current run.
