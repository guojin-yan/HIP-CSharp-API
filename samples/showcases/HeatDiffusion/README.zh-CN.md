# HeatDiffusion

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

`HeatDiffusion` 是仓库中的第一个端到端综合案例。它对同一个二维热扩散问题执行两次求解： 第一次使用 C# `Parallel.For` 作为参考实现，第二次使用 HIPRTC 在运行时将 HIP C++ 五点差分 Kernel 编译到 AMD GPU 上执行。案例先证明 GPU 结果正确，再报告耗时并生成热力图。

这是一篇可以从头复现的教程。建议先按顺序完成运行，再通过源码导读把每一步和 HIP-CSharp-API 的功能对应起来。

下文所有命令都假设当前目录是包含 `HipSharp.sln` 的仓库根目录：

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
```

<p align="center"><img src="assets/heat-diffusion-result.png" alt="HeatDiffusion GPU 热力图" width="520" /></p>

## 你将完成什么

完成本教程后，你会得到一个可重复运行的工作负载，它能够：

1. 创建包含固定边界、热源和冷却源的确定性温度场。
2. 使用 CPU 计算最终温度场，作为 GPU 的正确性基准。
3. 使用 HIPRTC 针对目标 AMD 架构编译 `Kernels/heat-diffusion.hip`。
4. 结合设备内存、非阻塞 Stream、Event 和 Graph 执行数百次差分迭代。
5. 逐项比较 CPU/GPU 结果，写出 `summary.json`，并可选生成 `heatmap.bmp`。

这是应用级案例，不是严格的跨设备基准测试。速度比只描述本次进程、设备、驱动、工作负载和 测量次数下的结果。

## 开始前准备

### 支持的运行环境

案例需要带 HIP Runtime 和 HIPRTC 的 AMD GPU。项目内置脚本面向 Ubuntu 24.04 ROCm 环境； 本地 Linux ROCm 环境也可以直接运行 C# 项目。

| 环境 | 构建 | 运行 GPU 工作负载 | 说明 |
| --- | --- | --- | --- |
| Radeon Cloud Ubuntu/ROCm | 支持 | 支持 | 使用案例目录中的 Shell 脚本 |
| 本地 Linux/ROCm | 支持 | 支持 | 传入 `rocminfo` 报告的架构 |
| Windows | 支持 | 取决于本机 HIP/ROCm 配置 | 本教程的脚本以 Linux 为目标 |

需要 .NET 10 SDK、HIP Runtime、HIPRTC，并且进程能够看到 AMD 设备。从仓库根目录直接调用 `dotnet` 时，会使用 `global.json` 指定的 SDK（当前为 `10.0.300`）。案例目录中的云端脚本会在 仓库父目录调用兼容 SDK，因此也能适配安装了不同 feature band 的云端镜像。先检查环境：

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --info
```

第一条命令会输出 HIPRTC 使用的架构字符串，例如 `gfx1100`。运行时请使用当前机器输出的值， 不要直接照抄示例中的 `gfx1100`。

## 五分钟完成第一次运行

### 1. 先运行小规模冒烟测试

在 Linux ROCm 主机上，把下面的 `gfx1100` 替换为 `rocminfo` 输出的架构：

```bash
dotnet restore ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj --locked-mode
dotnet build ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-restore
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-build --no-restore -- \
  --arch gfx1100 --profile tiny
```

`tiny` 使用 256 x 256 网格和 50 次迭代，是检查设备发现、HIPRTC 编译、Kernel 启动、结果校验 和图片生成最快的方式。

### 2. 运行默认演示

`quick` 配置使用 1536 x 1536 网格和 600 次迭代，既能体现 CPU/GPU 路径差异，又适合云端快速 体验：

```bash
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-build --no-restore -- \
  --arch gfx1100 --profile quick
```

只有 GPU 结果通过数值容差时，进程才会返回退出码 `0`。即使已经生成热力图，校验失败仍然代表 本次运行失败。

<a id="run-on-radeon-cloud"></a>

### 3. 在 Radeon Cloud 中运行

案例目录中的脚本会自动完成环境检查、选择已安装的 .NET SDK、检测 GPU 架构、锁定还原、Release 构建和运行：

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

脚本默认运行 `quick`。需要更长的演示或诊断时可以显式设置：

```bash
HIPSHARP_HEAT_PROFILE=showcase \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh

HIPSHARP_GPU_ARCH=gfx1100 \
HIPSHARP_HEAT_OUTPUT=/persistent/projects/hip-csharp-api/results/heat-diffusion \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh \
  --gpu-runs 5
```

未设置 `HIPSHARP_HEAT_OUTPUT` 时，产物会写入 `artifacts/heat-diffusion/时间戳/`。如果需要对比 多次运行或分享结果，请保留整个时间戳目录。

## 一次运行内部发生了什么

了解执行顺序后，控制台输出和源码会更容易理解：

1. `Options` 解析 profile、网格尺寸、迭代次数、架构和输出目录。
2. `HeatProblem` 为两个实现创建完全相同的 `fixedField` 和 `initialField`。
3. `CpuHeatSolver` 使用 `Parallel.For` 和双缓冲执行 CPU 参考计算。
4. `GpuHeatSolver` 初始化 HIP、选择第一个设备，并使用 `--offload-arch=<gfxNNNN> -O3` 编译 HIP 源码。
5. GPU 路径在设备端分配当前值、下一步值和不变的固定场。
6. 通过非阻塞 Stream 将初始数据复制到设备，并执行一次预热启动。
7. 如果运行时支持 Graph Capture，就把两个相反方向的差分启动（`current -> next`、 `next -> current`）捕获为 Graph；奇数次迭代的最后一步直接启动。
8. 每次正式测量用 HIP Event 包围 Kernel 工作，复制最终结果并同步一次，最后取指定次数的中位数。
9. `ErrorMetrics` 比较 CPU/GPU 数组，只有校验完成后才写出 JSON 摘要和热力图。

差分计算必须在两个缓冲区之间交替：一步读取旧场，同时写入新场。捕获两个步骤后，长时间的偶数 迭代可以重复使用同一个 Graph，不需要为每个步数重新构建。

## 数学模型

对每个非固定单元，案例使用 `alpha = 0.2` 的五点差分公式：

```text
next[y,x] = current[y,x]
             + alpha * (current[y-1,x] + current[y+1,x]
                      + current[y,x-1] + current[y,x+1]
                      - 4 * current[y,x])
```

固定单元在每一步保持不变：

| 区域 | 温度 | 含义 |
| --- | ---: | --- |
| 外边界 | 20 | 环境边界条件 |
| 热源 1 | 100 | 高温源 |
| 热源 2 | 80 | 第二个高温源 |
| 冷却源 | 5 | 低温源 |
| 其他单元 | 初始为 20 | 随时间扩散的温度 |

固定场内部标记只在 `HeatProblem` 中使用，不会出现在输出中。由于外边界始终固定，Kernel 可以 安全读取每个内部更新单元的四个邻居。

## 查看运行产物

成功运行后，目录结构如下：

```text
<output>/
├── summary.json
└── heatmap.bmp
```

`summary.json` 是机器可读的结果来源，建议按以下顺序查看：

| 字段 | 作用 |
| --- | --- |
| `profile`、`width`、`height`、`steps` | 本次工作负载的精确配置 |
| `architecture`、`deviceName`、`executionMode` | 设备和 Graph/普通 Stream 路径 |
| `cpuMilliseconds` | CPU 参考实现耗时 |
| `gpuKernelMilliseconds` | HIP Event 测得的 Kernel 中位时间 |
| `gpuEndToEndMilliseconds` | 包含 Copy、Kernel 和同步的端到端中位时间 |
| `maximumAbsoluteError`、`rootMeanSquareError` | CPU/GPU 数值一致性 |
| `heatmapPath` | 生成图片的相对路径 |

程序显示的速度比为 `cpuMilliseconds / gpuEndToEndMilliseconds`。HIPRTC 编译时间单独报告，不 计入速度比。改变网格、驱动、GPU 状态或测量次数都会改变结果。

## 命令行参数

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `--arch gfxNNNN` | 未设置环境变量时必填 | HIPRTC 编译目标 |
| `--profile tiny\|quick\|showcase` | `quick` | 256 x 256 / 50、1536 x 1536 / 600 或 2048 x 2048 / 1000 |
| `--width N` | profile 值 | 网格宽度，64-4096 |
| `--height N` | profile 值 | 网格高度，64-4096 |
| `--steps N` | profile 值 | 迭代次数，1-10000 |
| `--gpu-runs N` | `3` | GPU 测量次数，1-9，报告中位数 |
| `--output PATH` | 时间戳目录 | 产物位置 |
| `--no-image` | 生成图片 | 跳过 `heatmap.bmp` |
| `--help` | 关闭 | 打印帮助，不需要 GPU |

例如保留 `quick` 的网格规模，只增加迭代次数和测量次数：

```bash
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  -c Release --no-build --no-restore -- \
  --arch gfx1100 --profile quick --steps 1200 --gpu-runs 5
```

## 源码阅读顺序

| 文件 | 适合在什么时候阅读 |
| --- | --- |
| `Program.cs` | 了解完整编排、校验契约和退出码 |
| `Options.cs` | 了解 profile、参数范围、环境变量和输出默认值 |
| `HeatProblem.cs` | 了解确定性热源、边界和 CPU/GPU 输入一致性 |
| `CpuHeatSolver.cs` | 了解 CPU 参考差分和双缓冲 |
| `GpuHeatSolver.cs` | 了解 HIP Runtime、内存、Stream、Event、Graph 捕获与重放 |
| `Kernels/heat-diffusion.hip` | 了解设备端 `__global__` 差分 Kernel |
| `HeatmapBmpWriter.cs` | 了解无第三方图片依赖的 BMP 输出 |
| `ResultSummary.cs` | 了解稳定的 JSON 结果结构 |
| `run-heat-diffusion.sh` | 了解云端检查、SDK 选择、还原/构建/运行和产物路径 |

运行脚本放在案例目录中，是因为它本身就是复现流程的一部分。旧的 `tools/radeon/run-heat-diffusion.sh` 入口仅作为兼容包装保留。

## 正确性与故障排查

所有 GPU 值必须是有限值，最大绝对误差必须 `<= 0.05`，RMSE 必须 `<= 0.01`。

| 现象 | 处理方式 |
| --- | --- |
| `Radeon GPU devices are unavailable` | 使用带 GPU 的实例，并确认暴露了 `/dev/kfd` 和 `/dev/dri`。 |
| 无法检测架构 | 运行 `rocminfo`，然后设置 `HIPSHARP_GPU_ARCH=gfxNNNN`。 |
| HIPRTC 编译失败 | 确认 HIP Runtime 和 HIPRTC 来自同一 ROCm 安装，并确认架构与设备匹配。 |
| `executionMode=direct-stream` | 当前运行时不支持 Graph Capture；结果校验仍然有效。 |
| 内存不足或超时 | 从 `--profile tiny` 开始，再降低 width、height 或 steps。 |
| 还原被 `global.json` 拒绝 | 使用案例目录中的脚本；脚本会从仓库外部调用兼容 SDK。 |

## 可复现的云端记录

[`Radeon_Cloud/records/20260818-101738-8eea3de-heat-diffusion`](../../../../Radeon_Cloud/records/20260818-101738-8eea3de-heat-diffusion) 中保留了一次 `gfx1100`、`quick` 配置的 Radeon Cloud 运行结果。记录中的摘要、控制台输出、 热力图和截图展示了完整产物格式。记录中的性能数字只代表一次会话，不代表所有 AMD GPU 都能 达到相同结果。

## 继续学习

第一次运行成功后，每次只改变一个因素：

1. 运行 `tiny --no-image`，只检查 `summary.json`。
2. 对比偶数和奇数 `--steps`，观察双缓冲的最终位置。
3. 在不支持 Graph Capture 的运行时比较 `graph-capture` 和 `direct-stream`。
4. 在 `HeatProblem` 中增加固定热源，确认 CPU/GPU 误差仍在容差内。
5. 修改热力图颜色映射，但不要改变数值求解器。

这些练习保留 CPU 实现这个稳定的正确性基准，同时逐步引入 HIP 概念。
