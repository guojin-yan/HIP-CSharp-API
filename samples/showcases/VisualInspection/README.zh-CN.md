# VisualInspection

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

`VisualInspection` 是一个可复现的 GPU 视觉处理流水线。它读取四张灰度零件图，使用 OpenCV生成 CPU 参考掩码，再使用 HIPRTC Kernel 在 AMD GPU 上  生成等价掩码。GPU 掩码必须同时等于OpenCV 结果和仓库内保存的期望掩码；成功运行会保存 JSON、CSV 和 PNG 证据，而不只是打印性能数字。阈值规则故  意保持简单，这样可以清楚观察完整路径：OpenCV-CSharp-API 负责图像 I/O 和经典图像操作，HIP-CSharp-API 负责设备内存、执行、计时和 GPU 结果校验。

下文所有命令都假设当前目录是包含 `HipSharp.sln` 的仓库根目录：

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
```

<p align="center"><img src="assets/visual-fixtures-contact-sheet.png" alt="VisualInspection 确定性测试图" width="720" /></p>

## 你将复现什么

完成本教程后，你会得到一个完整的小型视觉检测工作负载：

1. 使用 OpenCV-CSharp-API `5.0.0` 读取四组确定性 PGM 测试图。
2. 使用 CPU OpenCV 找出超出灰度合格范围的像素。
3. 使用 HIPRTC 针对目标 `gfxNNNN` 架构编译 `Kernels/visual-inspection.hip`。
4. 经由锁页主机内存、设备内存、非阻塞 Stream、Event 和可用时的 Graph 处理图像。
5. 逐字节比较 CPU、GPU 和期望掩码，写出摘要、CSV 表和每组测试图对应的 PNG 掩码。

这是集成教学案例，不是生产级检测模型。图片是合成的，因此每个期望判定都已知，所有输出都能快速 人工检查。

## 开始前准备

### 运行环境

可执行的工作负载面向 Ubuntu 24.04 ROCm 主机，需要 AMD GPU、HIP Runtime 和 HIPRTC。项目同时 目标 `net8.0` 和 `net10.0`，云端脚本会自动选择已安装的兼容 runtime。

项目通过仓库的中央包版本配置引用以下包：

| 包 | 版本 | 用途 |
| --- | --- | --- |
| `JYPPX.OpenCV.CSharp.API` | `5.0.0` | 托管 OpenCV API |
| `JYPPX.OpenCV.runtime.ubuntu.24.04-x64.mini` | `5.0.0` | Ubuntu 24.04 原生 OpenCV runtime |

托管命名空间是 `JYPPX.OpenCvSharp.*`。项目有意使用随案例保存的 Ubuntu 专用 RID 图，因此不要求 在系统层面预装 OpenCV。

从仓库根目录直接调用 `dotnet` 时，会使用 `global.json` 指定的 SDK（当前为 `10.0.300`）。案例 目录中的脚本会在仓库父目录调用兼容 SDK，因此也能适配安装了不同 feature band 的云端镜像。

| 环境 | 还原和构建 | 运行完整工作负载 | 说明 |
| --- | --- | --- | --- |
| Radeon Cloud Ubuntu/ROCm | 支持 | 支持 | 建议的首次体验环境 |
| 本地 Ubuntu 24.04/ROCm | 支持 | 支持 | 使用下方直接运行命令 |
| Windows | 支持 | 不支持当前 Ubuntu runtime | 可以本地阅读和构建源码；完整 GPU 运行应放在 Linux |

第一次在 Linux 中运行前，先检查主机：

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --list-runtimes
```

请使用 `rocminfo` 输出的实际架构；文中的 `gfx1100` 只是示例。

<a id="run-on-radeon-cloud"></a>

## 在 Radeon Cloud 中五分钟完成运行

在已授权的 Ubuntu ROCm 实例中，进入仓库根目录后执行：

```bash
bash ./samples/showcases/VisualInspection/run-visual-inspection.sh
```

这个脚本完成 Radeon Cloud 或等价 Ubuntu ROCm 环境特有的准备工作：

1. 检查 `/dev/kfd`、`/dev/dri` 和 `rocminfo`。
2. 复用已有的 .NET 8+ SDK；只有没有兼容 SDK 时才 bootstrap。
3. 除非设置了 `HIPSHARP_GPU_ARCH`，否则自动检测第一个 `gfxNNNN` 架构。
4. 使用 `runtime-distro-rid-graph.json` 以 locked mode 还原。
5. 构建 Release，运行自动选择的 `net10.0` 或 `net8.0` 目标，并在 `artifacts/visual-inspection/` 下生成时间戳产物目录。

需要显式指定架构、持久化输出路径或增加计时样本时：

```bash
HIPSHARP_GPU_ARCH=gfx1100 HIPSHARP_VISUAL_OUTPUT=/persistent/projects/hip-csharp-api/results/visual-inspection bash ./samples/showcases/VisualInspection/run-visual-inspection.sh --gpu-runs 5
```

仅在需要强制选择 runtime 时设置 `HIPSHARP_VISUAL_TFM=net8.0` 或 `HIPSHARP_VISUAL_TFM=net10.0`。默认选择会依据已安装 runtime 进行。

## 在本地 Linux ROCm 主机直接运行

在仓库根目录执行。请把 `gfx1100` 替换成当前机器的实际架构：

```bash
dotnet restore ./samples/showcases/VisualInspection/VisualInspection.csproj --locked-mode
dotnet build ./samples/showcases/VisualInspection/VisualInspection.csproj --configuration Release --no-restore
dotnet run --project ./samples/showcases/VisualInspection/VisualInspection.csproj --framework net8.0 --configuration Release --no-build --no-restore -- --arch gfx1100
```

如果机器安装的是 .NET 10 runtime，则把 `--framework net8.0` 改为 `--framework net10.0`。案例固定使用 Ubuntu 原生 OpenCV runtime，因此 Windows 本地构建适合阅读源码和验证依赖，但无法加载 `JYPPX.OpenCV.Native` 来执行完整工作负载。

## 理解检测规则

每张测试图都是 128 x 96、8-bit 灰度 PGM 图。像素值小于 `100` 或大于 `190` 时被视为缺陷。GPU 对 缺陷写入 `255`，其余位置写入 `0`：

```text
defect = pixel < 100 || pixel > 190
mask   = defect ? 255 : 0
```

CPU 参考路径用两次 OpenCV `Threshold` 和一次 `BitwiseOr` 构造完全相同的掩码。测试图、期望掩码 和配方都位于 `assets/`：

| 测试图 | 条件 | 期望判定 | 期望缺陷像素数 |
| --- | --- | --- | ---: |
| `part_000_ok` | 无缺陷 | PASS | 0 |
| `part_001_scratch` | 深色划痕 | FAIL | 261 |
| `part_002_hole` | 深色孔洞 | FAIL | 529 |
| `part_003_contamination` | 高亮污染 | FAIL | 421 |

输入契约是明确的。自定义 `--input` 目录必须包含 `visual-fixture-recipe.json`，并包含配方中引用的 全部图像和期望掩码。程序会在启动 GPU 工作前拒绝尺寸不一致的输入。

## 一次运行内部发生了什么

1. `Options` 解析架构、输入目录、输出目录和 GPU 测量次数。
2. `VisualRecipe` 加载测试图清单。`OpenCvImageTools` 把每张图读成 OpenCV `Mat`，生成 CPU 掩码， 并把像素转换为 HIP 边界使用的数据。
3. HIPRTC 使用 `--offload-arch=<gfxNNNN> -O3` 编译 `visual-inspection.hip`。
4. GPU 路径一次性分配输入/输出设备缓冲区和输入/输出锁页主机缓冲区。
5. 支持时把 Kernel 启动捕获为 Graph；不支持时使用非阻塞 Stream 直接启动。
6. 对每张测试图，程序复制像素到设备、使用 HIP Event 测量 Kernel、复制掩码回主机，然后比较 GPU、OpenCV CPU 和期望掩码。
7. OpenCV 为每张测试图写出 GPU PNG 掩码；程序写出 JSON 和 CSV，并且仅在全部测试图通过时返回 `0`。

Graph 只包含可复用的 Kernel 启动。输入和输出 Copy 仍然按测试图执行，因此同一个捕获 Graph 能够安全 处理每张图。

## 查看产物

成功运行会生成：

```text
<output>/
├── inspection-summary.json
├── inspection-results.csv
└── masks/
    ├── part_000_ok_gpu.png
    ├── part_001_scratch_gpu.png
    ├── part_002_hole_gpu.png
    └── part_003_contamination_gpu.png
```

建议按下列顺序检查：

| 文件 | 检查内容 |
| --- | --- |
| `inspection-summary.json` | `status`、设备、架构、HIPRTC 哈希、计时、执行模式和各测试图结果 |
| `inspection-results.csv` | 每张测试图一行，适合导入电子表格比较 |
| `masks/*.png` | 已经参与校验的 GPU 实际输出掩码 |

每张测试图的 `passed` 只有在 GPU 掩码同时等于 OpenCV 参考掩码和期望掩码时才为 true。 `intersectionOverUnion` 应为 `1.0`，`maximumByteDifference` 应为 `0`。

CPU 时间包含 OpenCV 解码和参考分割；GPU Kernel 时间由 HIP Event 测量；GPU 端到端时间还包含锁页 H2D/D2H Copy 和同步。输入图特意保持很小，因此这些时间用于证明流水线工作正常，不应用来宣称工业 吞吐量。

## 命令行参数

| 参数 | 默认值 | 说明 |
| --- | --- | --- |
| `--arch gfxNNNN` | 未设置 `HIPSHARP_GPU_ARCH` 时必填 | HIPRTC 编译目标 |
| `--input PATH` | 内置 `assets/` | 包含配方、图像和期望掩码的目录 |
| `--output PATH` | 时间戳产物目录 | JSON、CSV 和 PNG 输出位置 |
| `--gpu-runs N` | `3` | 每张图的测量次数，1-9，报告中位数 |
| `--help` | 关闭 | 打印帮助，不启动 HIP 或 OpenCV |

例如保存一个名字固定的本地结果目录：

```bash
dotnet run --project ./samples/showcases/VisualInspection/VisualInspection.csproj --framework net8.0 -c Release --no-build --no-restore -- --arch gfx1100 --gpu-runs 5 --output ./artifacts/visual-inspection/tutorial-run
```

## 源码阅读顺序

| 文件 | 适合在什么时候阅读 |
| --- | --- |
| `Program.cs` | 了解测试图编排、CPU/GPU/期望结果比较、JSON/CSV 写入和退出码 |
| `Options.cs` | 了解运行参数和输出默认值 |
| `VisualRecipe.cs` | 了解配方结构和测试图加载 |
| `OpenCvImageTools.cs` | 了解 `JYPPX.OpenCvSharp.*` 图像读取、CPU 分割和 PNG 输出 |
| `GpuInspectionSolver.cs` | 了解锁页内存、设备内存、Stream/Event 计时、Graph 捕获和回退 |
| `Kernels/visual-inspection.hip` | 了解一维 HIP 缺陷掩码 Kernel |
| `PgmImage.cs` | 了解 OpenCV/HIP 边界处的小型像素容器 |
| `runtime-distro-rid-graph.json` | 了解 Ubuntu 专用 RID 兼容图 |
| `run-visual-inspection.sh` | 了解 ROCm 检查、.NET 选择、锁定还原、构建、运行和产物路径 |

## 故障排查

| 现象 | 处理方式 |
| --- | --- |
| `Radeon GPU devices are unavailable` | 使用带 GPU 的 Linux 实例，并确认暴露了 `/dev/kfd` 和 `/dev/dri`。 |
| `Specify --arch gfxNNNN` | 从 `rocminfo` 读取架构，再传入 `--arch` 或设置 `HIPSHARP_GPU_ARCH`。 |
| Windows 无法加载 `JYPPX.OpenCV.Native` | 这是预期行为：项目固定使用 Ubuntu 24.04 原生 OpenCV 包，应在 Linux 中运行。 |
| NuGet 报告包哈希不一致 | 只删除本地 NuGet 缓存中受影响的 OpenCV 包版本，再以 locked mode 还原。 |
| HIPRTC 编译失败 | 确认 HIP Runtime 和 HIPRTC 来自同一 ROCm 安装，且目标架构与设备一致。 |
| `executionMode=direct-stream` | Graph Capture 不可用；普通 Stream 路径仍然会完成完整校验。 |
| 还原被 `global.json` 拒绝 | 使用 `run-visual-inspection.sh`；它会在仓库根目录之外调用兼容 SDK。 |

## 已验证的 Radeon Cloud 运行

Radeon Cloud 上使用稳定版 `5.0.0` 包、Ubuntu 24.04 OpenCV mini runtime、`gfx1100` 和 .NET 10 的 运行已通过全部测试图，执行模式为 `graph-capture`：

| 测量项 | 数值 |
| --- | ---: |
| HIPRTC 编译 | 57.28 ms |
| OpenCV CPU 参考 | 32.03 ms |
| GPU Kernel 中位时间 | 0.02 ms |
| GPU 端到端中位时间 | 0.06 ms |
| 通过测试图 | 4/4 |

保留的 JSON、CSV 和 PNG 掩码位于 [`Radeon_Cloud/records/20260818-145855-visual-inspection-5.0.0`](../../../../Radeon_Cloud/records/20260818-145855-visual-inspection-5.0.0)。 这些数值描述当前测试图集和当前会话，不代表跨设备性能承诺。

## 继续学习

复现内置案例后，请保持正确性契约不变，每次只改变一个因素：

1. 复制 `assets/` 到新目录，并通过 `--input` 运行，验证外部测试图流程。
2. 在配方中新增测试图和期望掩码，确认 JSON、CSV 和 PNG 同步增加。
3. 同时修改 CPU 与 HIP 实现中的灰度合格范围，再生成期望掩码。
4. 使用相机帧导出替换 PGM 图，同时保持 OpenCV 到 HIP 的字节边界。
5. 只有在保留期望掩码和 CPU/GPU 校验后，才增加测试图数量或图像尺寸。

这正是实际视觉检测工作的推进方式：先建立可证明正确的数据契约，再扩展 GPU 路径，而不失去验证 结果的能力。
