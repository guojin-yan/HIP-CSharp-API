# HeatDiffusion

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

`HeatDiffusion` 是一个完整的 C# 到 AMD GPU 工作负载。它会对二维热扩散问题执行两次求解：
第一次使用 C# `Parallel.For` 参考实现，第二次使用 HIPRTC 在运行时编译的 HIP C++ 五点差分
Kernel。案例会比较两次计算的最终温度场，测量 GPU 路径，并生成便于检查结果的热力图。

这不是一个只演示单个 API 的小例子，而是一个应用级综合案例。它有意识地组合编号教程中学到的
能力：设备初始化、HIPRTC、Module 与 Kernel 启动、设备内存、异步 Copy、Stream、Event、Graph
捕获、资源所有权和 CPU/GPU 结果校验。

<p align="center"><img src="assets/heat-diffusion-result.png" alt="HeatDiffusion GPU 热力图" width="520" /></p>

## 学习目标

完成本案例后，应能够：

1. 先建立确定性的 CPU 实现，把它作为 GPU 正确性基准。
2. 使用扁平、按行存储的 `float[]` 表示二维网格。
3. 针对机器实际报告的 AMD GPU 架构编译 HIP Kernel。
4. 将不变数据和迭代数据放入设备内存，并交替使用两个输出缓冲区。
5. 使用非阻塞 HIP Stream 排列 Copy、Kernel 启动和同步操作。
6. 使用 HIP Event 测量 Kernel 时间，使用 Stopwatch 测量端到端时间。
7. 将两个交替的差分步骤捕获为 Graph，并在长工作负载中重放。
8. 当 Graph 捕获不受支持时，退回普通 Stream 启动。
9. 先完成数值校验，再展示任何性能数字。
10. 把云端环境适配与 C# 工作负载分离。

## 工作负载模型

网格中的每个单元保存一个温度值。对于非固定单元，每一步使用五点差分公式，`alpha = 0.2`：

```text
next[y,x] = current[y,x]
             + alpha * (current[y-1,x] + current[y+1,x]
                      + current[y,x-1] + current[y,x+1]
                      - 4 * current[y,x])
```

边界和热源单元保存在 `fixedField` 中，每一步都原样写回：

| 区域 | 温度 | 作用 |
| --- | ---: | --- |
| 外边界 | 20 | 环境边界条件 |
| 热源 1 | 100 | 高温源 |
| 热源 2 | 80 | 第二个高温源 |
| 冷却源 | 5 | 低温源 |
| 其他单元 | 初始为 20 | 随时间扩散的温度 |

`HeatProblem` 为两条实现生成完全相同的确定性输入。固定场使用 `-1` 作为内部标记，表示该
单元需要参与更新。由于外边界始终固定，Kernel 对每个非固定内部单元读取四个邻居时不会越界。

## 完整执行流程

一次运行按以下顺序进行：

1. `Program.Main` 解析配置并创建产物目录。
2. `HeatProblem` 构造 `fixedField` 和全为环境温度的 `initialField`。
3. `CpuHeatSolver` 使用 `Parallel.For` 执行 CPU 参考实现。
4. `GpuHeatSolver` 初始化 `HipRuntime`、枚举设备并将目标设备设为当前设备。
5. HIPRTC 使用 `--offload-arch=<gfxNNNN>` 和 `-O3` 编译 `Kernels/heat-diffusion.hip`。
6. 将 code object 加载为 `HipModule`，并从中取得 `HeatStep` 对应的 `HipKernel`。
7. 分配三个设备缓冲区：当前值、下一步值和不变的固定场。
8. 把初始数据复制到设备，并执行一次预热启动。
9. 当迭代次数至少为 2 时，将两个相反方向的启动捕获为一个 Graph：`current -> next` 和
   `next -> current`。
10. 每次正式测量先把初始场复制到设备，记录 Event，使用 Graph 重放或普通启动排队所有步骤，
    再把最终缓冲区复制回主机并只执行一次同步。
11. 计算指定次数 GPU 运行的中位数，比较 CPU/GPU 温度场，最后写入 JSON 摘要和可选 BMP 热力图。

Graph 包含两个步骤，是因为 Kernel 参数需要在两个设备缓冲区之间交替。若总步数为奇数，最后
一个步骤会在 Graph 成对步骤之后直接启动。这样可以明确处理缓冲区奇偶性，也不需要为每个步数
重新构建一张 Graph。

## 源码导读

| 文件 | 职责 | HIP 或 .NET 概念 |
| --- | --- | --- |
| `Program.cs` | 编排一次完整运行并返回退出码 | 显式 `Program.Main`、校验、JSON 输出 |
| `Options.cs` | 解析配置和覆盖参数 | `--arch`、尺寸、步数、输出目录、图片开关 |
| `HeatProblem.cs` | 创建确定性热源并比较结果 | CPU/GPU 正确性契约 |
| `CpuHeatSolver.cs` | CPU 参考差分 | `Parallel.For`、双缓冲 |
| `GpuHeatSolver.cs` | HIP 执行路径 | Runtime、HIPRTC、内存、Stream、Event、Graph、Module、Kernel |
| `Kernels/heat-diffusion.hip` | GPU 差分计算 | HIP C++ `__global__` Kernel |
| `HeatmapBmpWriter.cs` | 写出不依赖第三方库的图片 | 24 位 BMP |
| `ResultSummary.cs` | 序列化运行证据 | 稳定的 camel-case `summary.json` 结构 |
| `run-heat-diffusion.sh` | 适配授权的 ROCm 云端环境 | `rocminfo`、SDK 选择、还原、构建、运行 |

Shell 运行脚本现在和案例项目放在一起，因为它属于本案例的学习路径。通用的 `tools/radeon`
目录仍然保存共享验证工具；其中原来的 `run-heat-diffusion.sh` 只保留为兼容入口，会转调这里
的真实实现。

## 环境要求

- .NET 10 或更高版本 SDK。项目当前目标框架为 `net10.0`，与仓库的开发基线保持一致。云端
  脚本会复用机器中已有的兼容 SDK，只有完全没有兼容 SDK 时才执行 bootstrap。
- 已安装 HIP/ROCm Runtime 所支持的 AMD GPU。
- 进程可以加载 HIP Runtime 和 HIPRTC 原生库。
- 通过 `--arch` 或 `HIPSHARP_GPU_ARCH` 传入真实 GPU 架构，例如 `gfx1100`。

C# 工作负载本身不引用 Radeon Cloud，可以迁移到其他 Linux ROCm 环境。只有 Shell 运行脚本假设
存在 `/dev/kfd`、`/dev/dri`、`rocminfo` 和仓库中的云端 bootstrap 工具。

## 在 Linux ROCm 工作站运行

从仓库根目录执行：

```bash
dotnet restore ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj --locked-mode
dotnet build ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj -c Release --no-restore
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  -c Release --no-build --no-restore -- \
  --arch gfx1100 \
  --profile quick
```

请将 `gfx1100` 替换为目标机器实际报告的架构。诊断新的 ROCm 环境时，可以先运行：

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --info
```

C# 命令不会擅自猜测 GPU 架构。错误的 `--arch` 可能仍然生成一个有效的 code object，但它并不
一定适用于当前设备，因此架构必须作为明确输入。

## 在 Radeon Cloud 运行

案例本身不依赖云平台。在已授权的 Ubuntu ROCm 实例上，从仓库根目录运行和项目放在一起的
脚本：

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

脚本在调用 C# 程序前会完成以下环境工作：

1. 检查 `/dev/kfd`、`/dev/dri` 和 `rocminfo`。
2. 复用已有的 .NET 10 或更高版本 SDK；只有缺少兼容 SDK 时才运行 `tools/radeon/bootstrap.sh`。
3. 从 `rocminfo` 识别首个 `gfxNNNN` 架构，除非设置了 `HIPSHARP_GPU_ARCH`。
4. 在存在 `/persistent` 时使用持久化 NuGet 缓存。
5. 使用锁文件还原，以 Release 配置构建并运行案例。
6. 将所有输出写入一个带时间戳的产物目录。

脚本从仓库父目录调用 `dotnet`，同时使用绝对项目路径。这样云端已有兼容 SDK 时，不会因为仓库
开发专用的 `global.json` feature band 固定值而提前失败。

使用更大的配置或覆盖参数：

```bash
HIPSHARP_HEAT_PROFILE=showcase \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh

bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh \
  --width 2048 \
  --height 2048 \
  --steps 800 \
  --gpu-runs 5
```

如果旧文档中仍然出现 `bash ./tools/radeon/run-heat-diffusion.sh`，该兼容命令仍可工作。新的
教程应使用上面的案例内脚本路径。

如果模板没有源码，应使用平台配置好的仓库签出机制。若 GitHub 代理证书不受信任，不得关闭
TLS 校验。应在可信机器上创建 Git bundle，传输后验证 SHA-256，并按
[`tools/radeon/README.zh-CN.md`](../../../tools/radeon/README.zh-CN.md) 中的流程签出目标提交。

## 配置与命令行参数

| 参数 | 默认值 | 含义 |
| --- | --- | --- |
| `--arch gfxNNNN` | 未设置环境变量时必填 | HIPRTC 编译目标 |
| `--profile tiny` | `quick` | 256 x 256、50 步，冒烟测试 |
| `--profile quick` | `quick` | 1536 x 1536、600 步，默认体验 |
| `--profile showcase` | `quick` | 2048 x 2048、1000 步，较长演示 |
| `--width N` | 由配置决定 | 网格宽度，64 到 4096 |
| `--height N` | 由配置决定 | 网格高度，64 到 4096 |
| `--steps N` | 由配置决定 | 迭代次数，1 到 10000 |
| `--gpu-runs N` | `3` | 正式 GPU 测量次数，1 到 9，输出中位数 |
| `--output PATH` | 带时间戳的产物目录 | JSON 和图片的输出位置 |
| `--no-image` | 默认生成图片 | 只需要数值结果时跳过 `heatmap.bmp` |
| `--help` | 关闭 | 打印用法，不要求 GPU |

Shell 脚本完成环境适配后，会把相同参数传给 C# 程序。`HIPSHARP_HEAT_PROFILE` 控制脚本默认
配置，`HIPSHARP_GPU_ARCH` 覆盖架构检测，`HIPSHARP_HEAT_OUTPUT` 指定产物目录。

## 产物说明

每次成功运行会生成：

```text
<output>/
├── summary.json
└── heatmap.bmp                 # 使用 --no-image 时不生成
```

`summary.json` 面向脚本和教程使用，主要字段按以下类别组织：

| 类别 | 字段 | 解释 |
| --- | --- | --- |
| 工作负载 | `profile`、`width`、`height`、`steps`、`cellUpdates` | 问题规模 |
| CPU 基线 | `cpuWorkers`、`cpuMilliseconds` | C# 参考实现开销 |
| GPU 计时 | `gpuCompileMilliseconds`、`gpuKernelMilliseconds`、`gpuEndToEndMilliseconds`、`gpuRuns` | 编译、Kernel 和包含 Copy 的端到端测量 |
| 执行方式 | `executionMode`、`architecture`、`deviceName` | Graph 或普通 Stream，以及目标设备 |
| Runtime | HIP/HIPRTC 版本、code object 大小和 SHA-256 | 原生执行证据 |
| 正确性 | `maximumAbsoluteError`、`rootMeanSquareError`、`nonFiniteValues`、容差 | CPU/GPU 比较结果 |
| 图片 | `heatmapPath` | 启用图片时的相对文件名 |

用于云端教程时，在销毁实例前应取回完整的时间戳目录。不要提交云端地址、端口、私钥、token、
主机名、GPU UUID 或其他实例唯一标识。

## 已验证的云端运行

已保留的 Radeon Cloud 运行使用 ROCm 7.2.1、`gfx1100` 设备、`quick` 配置和实例已有的
.NET SDK `10.0.110`。案例在 1536 x 1536 网格上迭代 600 步，共完成 1,415,577,600 次单元
更新，并使用 `executionMode=graph-capture`：

| 测量项 | 数值 |
| --- | ---: |
| CPU 参考实现，16 workers | 2064.9364 ms |
| GPU Kernel 中位时间 | 17.4798 ms |
| GPU 端到端中位时间 | 20.5979 ms |
| 当前会话观测加速比 | 100.2499x |
| 最大绝对误差 | 1.1444e-05 |
| RMSE | 9.1031e-08 |

本次运行没有非有限值，构建没有警告或错误，并通过了最终结果校验。这些数字用于展示输出
格式和完整流程，不是对所有 AMD GPU 的性能承诺。原始 `summary.json`、控制台日志、BMP 和
PNG 已保存在外部 `Radeon_Cloud/records/` 记录中。

## 正确性与性能

只有在所有 GPU 数值都是有限值、最大绝对误差不超过 `0.05` 且 RMSE 不超过 `0.01` 时，进程才
返回退出码 `0`。即使 Kernel 生成了图片，只要退出码非零，就不能视为成功的综合案例运行。

输出的加速信息明确分为三类：

- HIPRTC 编译时间：启动成本，单独输出，不计入加速比。
- GPU Kernel 中位时间：由 HIP Event 测得的设备执行时间。
- GPU 端到端中位时间：每次运行的 H2D Copy、全部步骤、D2H Copy 和同步。

显示的加速比是当前进程中的 `cpuMilliseconds / gpuEndToEndMilliseconds`。它不是跨设备基准，
不包含功耗测量，也不能作为能源效率结果。网格、步数、GPU 状态、驱动、SDK 或运行次数变化后，
该数字可能明显变化。

## 常见问题排查

### `Radeon GPU devices are unavailable`

脚本要求 `/dev/kfd` 和 `/dev/dri`。请启动 GPU 实例并暴露这两个设备节点。本地运行时，可以在
确认 HIP Loader 能看到 GPU 后直接调用 C# 项目。

### `Unable to determine a gfxNNNN target`

执行 `rocminfo` 并显式设置架构：

```bash
HIPSHARP_GPU_ARCH=gfx1100 \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

### HIPRTC 编译失败

确认 `libhiprtc` 与 HIP Runtime 来自同一套 ROCm，并确认所选架构由当前设备支持。报告问题时，
应将异常中的编译日志和产物目录一起保留。

### 输出 `direct-stream`

这是受支持的降级路径。Runtime 在捕获 Graph 时返回了 `HipError.NotSupported`，因此案例使用
普通 Stream 直接启动相同的两步成对 Kernel。正确性和计时仍会执行，只是没有 Graph 重放。

### Restore 因 `global.json` feature band 失败

使用案例目录内的云端脚本。它从仓库父目录解析并调用 SDK，再显式传入项目路径。不要为了运行
该案例而削弱仓库级 `global.json`。

### 内存不足或运行超时

先使用 `--profile tiny`，再逐步切换到 `quick`。降低 `--width`、`--height` 或 `--steps`，并保留
输出目录，以便检查失败时的控制台日志和部分产物。

## 建议练习

1. 使用 `tiny` 和 `--no-image`，只查看 `summary.json`。
2. 在偶数和奇数 `--steps` 之间切换，观察 GPU Solver 的最终缓冲区奇偶性。
3. 临时关闭 Graph 捕获，对比 `direct-stream` 与 `graph-capture`。
4. 在 `HeatProblem` 中增加第三个固定热源，确认 CPU/GPU 误差仍在容差内。
5. 更换热力图颜色映射，但保持数值 Solver 不变。
6. 在完成设备枚举教程后增加第二 GPU 的选择逻辑。

这些练习让案例在第一次云端运行后仍然有学习价值：每次只改变一个概念，同时保留 CPU 参考
实现作为稳定的正确性检查。
