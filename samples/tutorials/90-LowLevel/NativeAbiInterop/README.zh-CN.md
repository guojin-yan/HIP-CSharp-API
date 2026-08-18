# NativeAbiInterop / 低层原生 ABI

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

`NativeAbiInterop` 是 90-LowLevel 模块中的教程案例。直接调用生成式 HIP Runtime C ABI，检查原始 HipError，并读取 Runtime 版本和设备数量。

## 本案例学习什么

本案例重点演示：HipRuntimeNativeApi、生成式互操作签名、unsafe 指针参数和原始错误处理。

案例有意保持小而明确：通过一个可观察的所有权或执行契约，返回明确的进程退出码。它用于学习和
正确性验证，不是性能基准。

## 环境与验证范围

本次云端验证在 Radeon Cloud 中完成，环境为 AMD Radeon Graphics（`gfx1100`）、ROCm 7.2.1 和
.NET 10.0.110。项目尚未验证 Windows 构建和 GPU 运行，下面的 Windows 部分只是尽力构建/运行
说明，不能视为 Windows 验证结果。

仓库使用 `global.json`，当前要求 .NET SDK `10.0.300` 并允许 patch roll-forward。云端脚本会在
仓库父目录调用 SDK，因此可以使用云端镜像中兼容的 feature band，而不修改仓库版本约束。

## 在 Radeon Cloud 中复现

在仓库根目录运行完整矩阵：

```bash
bash ./samples/tutorials/run-cloud-verification.sh
```

脚本会检测第一个 `gfxNNNN` 架构，以 locked mode 还原，逐个构建 tutorials 项目，运行每个可执行程序，
并保存证据目录。在 SDK 匹配的主机上只运行本案例时，可以执行：

```bash
dotnet run --project samples/tutorials/90-LowLevel/NativeAbiInterop/NativeAbiInterop.csproj --configuration Release
```

需要架构的案例请把 `gfx1100` 替换成 `rocminfo` 输出的实际值。矩阵中的 `NativeAbiInterop` 状态是本案例的
权威云端结果。

## Windows 复现说明（尚未验证）

下面的命令仅用于源码阅读和尽力构建：

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet restore .\samples\tutorials\90-LowLevel\NativeAbiInterop\NativeAbiInterop.csproj --locked-mode
dotnet build .\samples\tutorials\90-LowLevel\NativeAbiInterop\NativeAbiInterop.csproj --configuration Release --no-restore
dotnet run --project .\samples\tutorials\90-LowLevel\NativeAbiInterop\NativeAbiInterop.csproj --configuration Release --no-build --
```

实际 GPU 运行还需要匹配的 AMD Windows 驱动、HIP Runtime、HIPRTC、原生库搜索路径和受支持的架构。
本项目尚未验证这些 Windows runtime 前提；构建成功不代表 GPU 运行验证通过。

## 执行流程

1. `Program.Main` 解析可选参数，并创建本案例所需的 HIP/Runtime 对象。
2. 案例执行下方源码导读中的能力检查和 HIP 操作。
3. 主机端将返回值或状态与确定性期望值比较。
4. 通过或受控跳过返回 `0`，未预期失败返回 `1`。

本次云端运行的关键输出为：

```text
Raw Runtime version: 70253211; Raw device count: 1
```

## 云端证据

云端状态：**Passed**。

云端日志报告原始 Runtime 版本和一张可见设备。

完整证据目录位于
[`Radeon_Cloud/records/20260818-161709-tutorials`](../../../../../Radeon_Cloud/records/20260818-161709-tutorials)。
本案例日志为 `logs/NativeAbiInterop.log`；`results.csv` 保存 20 个教程的状态和退出码。

## 源码阅读顺序

| 文件 | 作用 |
| --- | --- |
| `Program.cs` | 显式入口、HIP 操作、校验和退出码 |
| `NativeAbiInterop.csproj` | .NET 目标框架和本地 HIPSharp 项目引用 |
| `packages.lock.json` | 锁定的依赖图 |
| `../../run-cloud-verification.sh` | 云端架构检测、构建矩阵、运行和证据保存 |

建议先阅读所在模块说明：[`90-LowLevel`](../README.zh-CN.md)。

## 故障排查

- 找不到 HIP 时，检查 `/dev/kfd`、`/dev/dri`、`rocminfo` 和 ROCm 安装。
- HIPRTC 报告目标架构错误时，传入 `rocminfo` 输出的准确 `gfxNNNN`。
- 如果输出 `Skipped`，先查看案例日志；能力门控跳过在部分设备上是预期行为。
- 运行 `PrecompiledModule` 时，提供 `HIPSHARP_PRECOMPILED_CODE_OBJECT` 或直接传入 `.hsaco` 路径。
- Windows 无法加载原生库时，应视为尚未验证的平台限制，并改在 Radeon Cloud 复现。

## 下一步

案例通过后继续学习本模块的下一个案例。保持确定性校验，每次只改变一个概念，不要把教程案例改成
计时基准。
