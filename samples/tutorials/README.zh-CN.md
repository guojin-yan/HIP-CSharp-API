# HIP 教程案例

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

本目录是 HIP-CSharp-API 的分步学习路径，按 Runtime/Device、Memory、Execution、Kernel、Graph、
Multi-device、Data objects 和低层 C ABI 组织。每个案例都是一个可执行的小程序，用于说明一个明确
的所有权或执行契约。

本文记录的验证是在 Radeon Cloud 中完成的，环境为 AMD Radeon Graphics（`gfx1100`）、ROCm 7.2.1
和 .NET 10.0.110。Windows 构建和 GPU 运行尚未验证；每个案例中的 Windows 部分仅作为复现指引。

## 从这里开始

在 Linux ROCm 主机或 Radeon Cloud 实例中，先获取源码并运行完整教程验证矩阵：

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
bash ./samples/tutorials/run-cloud-verification.sh
```

该脚本会检测第一个 `gfxNNNN` 架构，以 locked mode 还原，只构建 tutorials 项目，逐个运行所有案例，
并写出一个带时间戳的证据目录。脚本刻意逐项目构建，避免仓库中无关工具项目的分析器错误掩盖教程
案例结果。

需要显式指定架构或证据目录时：

```bash
HIPSHARP_GPU_ARCH=gfx1100 HIPSHARP_TUTORIAL_RECORD=/persistent/projects/hip-csharp-api/results/tutorials bash ./samples/tutorials/run-cloud-verification.sh
```

通过 `HIPSHARP_PRECOMPILED_CODE_OBJECT=/path/to/vector-add.hsaco` 可以为 `PrecompiledModule` 提供
外部 code object。如果未设置该变量，案例只运行入口用法路径，并记录为 `usage-only`，因为仓库中
没有可移植的预编译 code object。

## Radeon Cloud 验证结果

本次保留的运行记录位于
[`Radeon_Cloud/records/20260818-161709-tutorials`](../../../Radeon_Cloud/records/20260818-161709-tutorials)。
目录包含 `environment.md`、`build.log`、`results.csv` 和每个案例的独立日志。
本次矩阵结果为：**18 个通过、1 个跳过、1 个入口用法路径、0 个失败**。

| 模块 | 案例 | 云端结果 | 证据摘要 |
| --- | --- | --- | --- |
| Runtime/Device | `EnvironmentAndDevice` | 通过 | 输出 Runtime/Driver 版本并枚举 AMD 设备 |
| Runtime/Device | `LoaderDiagnostics` | 通过 | HIP 原生 Loader 初始化成功 |
| Memory | `LinearMemoryCopy` | 通过 | H2D、D2D、D2H 往返 |
| Memory | `PinnedHostMemory` | 通过 | 锁页主机内存往返 |
| Memory | `PitchedMemory2D3D` | 通过 | 2D/3D extent 和物理 pitch |
| Memory | `ManagedMemory` | 通过 | Managed Memory 可见性往返 |
| Memory | `AsyncAllocationAndMemoryPool` | 通过 | Stream-ordered allocation 和 memory pool |
| Memory | `VirtualMemory` | 通过 | Reserve/map/access/release 流程 |
| Execution | `StreamAndEvent` | 通过 | 异步 Copy 顺序和 Event 同步 |
| Execution | `AsyncVectorAdd` | 通过 | 五种长度、双 Stream、Event、预期错误和 100 次生命周期重复 |
| Kernel | `HipRtcProgramLinker` | 通过 | LLVM bitcode 链接为 code object |
| Kernel | `HipRtcVectorAdd` | 通过 | HIPRTC 编译、启动、20 次重复和 CPU 校验 |
| Kernel | `KernelOccupancy` | 通过 | Occupancy 与启动计划查询 |
| Kernel | `ModuleGlobals` | 通过 | 类型化 Module Global 往返 |
| Kernel | `PrecompiledModule` | 入口用法 | 需要外部 `.hsaco` 或兼容 code object |
| Graph | `ExplicitGraphDag` | 通过 | 两节点 DAG 拓扑和启动 |
| Graph | `GraphCaptureReplay` | 通过 | 捕获的异步 Memory 往返 |
| Multi-device | `PeerToPeerCopy` | 跳过 | 当前验证实例只有一张 GPU |
| Data objects | `ArrayTextureSurface` | 通过 | Array、Texture、Surface 所有权 |
| Low-level ABI | `NativeAbiInterop` | 通过 | 原始 C ABI 初始化和设备计数 |

`PeerToPeerCopy` 是受控跳过，不是失败。需要在至少有两张 GPU 且设备 0 可以访问设备 1 的实例中运行，
才能验证真正的 P2P Copy。

## 按顺序阅读案例

| 阶段 | 模块 | 教程入口 |
| --- | --- | --- |
| 01 | Runtime 与 Device | [`01-RuntimeDevice`](01-RuntimeDevice/README.zh-CN.md) |
| 02 | Memory | [`02-Memory`](02-Memory/README.zh-CN.md) |
| 03 | Execution | [`03-Execution`](03-Execution/README.zh-CN.md) |
| 04 | Kernel | [`04-Kernel`](04-Kernel/README.zh-CN.md) |
| 05 | Graph | [`05-Graph`](05-Graph/README.zh-CN.md) |
| 06 | Multi-device | [`06-MultiDevice`](06-MultiDevice/README.zh-CN.md) |
| 07 | Data objects | [`07-DataObjects`](07-DataObjects/README.zh-CN.md) |
| 90 | Low-level ABI | [`90-LowLevel`](90-LowLevel/README.zh-CN.md) |

每个案例目录都包含英文 `README.md` 和中文 `README.zh-CN.md`，其中说明直接运行命令、云端状态、
预期输出、源码阅读顺序和 Windows 构建/运行边界。

## Windows 构建与运行说明

本项目尚未在 Windows 上完成运行验证。下面的命令仅用于源码阅读和尽力构建：

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet --version
dotnet restore .\HipSharp.sln --locked-mode
dotnet build .\HipSharp.sln --configuration Release --no-restore
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj --configuration Release -- --arch gfx1100
```

安装的 SDK 必须满足仓库 `global.json`（当前为 .NET SDK `10.0.300`，允许 patch roll-forward）。实际
GPU 运行还需要匹配的 AMD Windows 驱动、HIP Runtime、HIPRTC、原生库搜索路径和受支持的 GPU 架构。
Windows 构建成功不能视为 Windows 运行验证通过。

## 证据目录

```text
Radeon_Cloud/records/<run-id>-tutorials/
├── environment.md
├── build.log
├── results.csv
└── logs/
    ├── EnvironmentAndDevice.log
    ├── HipRtcVectorAdd.log
    └── ... 每个案例一个日志
```

日志是本次云端运行的权威证据，同时保留受控跳过和入口用法路径，方便区分硬件能力限制与实现失败。
