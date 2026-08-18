# Radeon Cloud 工具

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

这些脚本支持在项目所有者授权的 Radeon Cloud 实例上执行两类不同工作流。

## 快速体验产品

在仓库根目录运行平台无关的 [`HeatDiffusion`](../../samples/showcases/HeatDiffusion/README.zh-CN.md)
综合案例。运行脚本现在和案例源码放在一起：

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

辅助脚本会检测 GPU 架构，复用任意已安装的 .NET 10 或更高版本 SDK，仅在缺少兼容 SDK 时
执行 bootstrap；随后构建案例，并将 `summary.json` 和 `heatmap.bmp` 写入
`artifacts/heat-diffusion/`。该流程不要求 detached checkout，也不生成发布验证证据。配置选择、
完整源码导读、配置选择、产物取回和结果解读见案例的
[Radeon Cloud 使用教程](../../samples/showcases/HeatDiffusion/README.zh-CN.md#在-radeon-cloud-运行)。
旧的 `tools/radeon/run-heat-diffusion.sh` 路径仍保留为兼容入口，已有记录无需修改。

## 验证门禁

其余脚本是执行辅助工具，不是已经保存的验证证据。只有在项目所有者授权当前 Radeon Cloud
实例，并准备好精确提交的干净 detached checkout 后才能使用。

从仓库根目录执行：

```bash
bash ./tools/radeon/bootstrap.sh
bash ./tools/radeon/env-report.sh
bash ./tools/radeon/cloud-test.sh <40-character-commit>
```

`bootstrap.sh` 从 Microsoft 官方 HTTPS 地址安装已验证环境使用的固定 .NET SDK，始终保持证书
校验，并可重复执行。`cloud-test.sh` 优先使用 `/persistent` 保存 NuGet 缓存，否则使用
`/workspace/.nuget/packages`。

`cloud-test.sh` 会执行托管构建、测试和打包门禁，Core 包审计及干净 Consumer 验证
（`eng/verify-package.ps1` 需要 PowerShell），校验 109 个托管清单导出，并对照已安装的 ROCm
头文件和库验证完整的固定头文件模型。Linux ROCm 7.2.1 导出 459 个 Runtime 声明中的 458 个；
`hipExternalMemoryGetMappedMipmappedArray` 是唯一经过明确审查的例外。18 个 HIPRTC 声明必须
全部导出。

门禁还会编译 schema 7 owner ABI probe，其中包括 M8.6 `hipModuleGetGlobal` 签名、M8.5 Module
函数属性、Occupancy、Cooperative Launch 的签名和枚举、M8.4 Graph 布局，以及 0.10.0 HIPRTC
Program/Linker 签名。它会运行 DeviceInfo、MemoryCopy、HIPRTC VectorAdd 与负向编译、HIPRTC
Program/Linker 精确包工作负载、Stream/Event、高级 API 路径和带 schema 版本的 M8.2-M8.6
托管扩展工作负载。HIPRTC 工作负载覆盖 name lowering、bitcode 获取、`AddData`、`AddFile`、
Module 执行、CPU/GPU 比较和 fail-closed 生命周期负向测试。只有文档明确说明的 capability/export
条件允许跳过。可靠性门禁会将每条路径与 CPU 结果比较，但不输出计时或性能声明。

每次调用 `cloud-test.sh` 都会写入新的
`artifacts/radeon-cloud/<commit>/<UTC-run>/` 目录，并将该精确路径传给 stress 门禁，避免错误复用
其他提交或之前尝试的证据。将选定结果复制并审查到外部
`Radeon_Cloud/records/<session>/` 结构后，才能将其作为证据。`env-report.sh` 会隐藏云端主机名和
具有识别性的 GPU 字段，也不会查询 GPU 唯一标识。Release 构建后还可运行
`cloud-stress.sh`；其有界配置可通过 `HIPSHARP_STRESS_ROUNDS`、`HIPSHARP_STRESS_STREAMS`、
`HIPSHARP_STRESS_LENGTH` 和 `HIPSHARP_STRESS_LIFECYCLES` 调整。

`runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression]
[RUNTIME_PACKAGE_COMMIT]` 是隔离包门禁。它必须在项目所有者新授权的隔离容器或 rootfs 中运行，
要求设置 `HIPSHARP_ISOLATED_CONSUMER=1`，暴露 `/dev/kfd` 和 `/dev/dri`，不存在 `/opt/rocm`、
系统 `libamdhip64` 或 `libhiprtc`，且只安装声明的 Ubuntu 系统库。该门禁从本地源还原 Core 与
Linux Runtime 包，在不引用源码或 staging 的情况下构建六个包 Consumer，校验 91 个 Runtime
和 18 个 HIPRTC 托管清单导出，记录 `readelf`、`ldd` 和 `/proc/<pid>/maps` 路径，并运行
DeviceInfo、MemoryCopy、HIPRTC VectorAdd、HIPRTC Program/Linker 精确包工作负载、双 Stream/Event
VectorAdd、M6 高级 API、M8.2-M8.6 托管扩展和有界多 Stream stress 配置。

随后门禁会检查缺少依赖、包篡改、仅 Core 和混合闭包负向场景。包篡改负向测试要求包验证以
非零代码退出，且只接受校验器的 hash/size 不匹配，或文档明确允许的 NuGet `NU3005` 仓库签名
拒绝；无关失败不能满足门禁。包含 NuGet `.signature.p7s` 的 Runtime 包只有在
`dotnet nuget verify --all` 成功后才能接受，且签名包本身仍是 Consumer 输入。`candidate` 与
`final` 保持严格的当前 SHA 绑定。`regression` 要求第五个参数传入不可变 Runtime 包自身的历史
提交，验证该提交是当前签出的祖先，并始终将该包记录为不可发布。不得在 M4 实例上运行此门禁，
也不得在仅通过重命名隐藏用户态 ROCm 文件的主机上运行。

如果由于平台代理证书不受信任而无法通过 HTTPS 访问源码托管服务，不得关闭证书校验。应在可信
本地机器上为明确的提交或 ref 创建 Git bundle，传输后验证 SHA-256，并在云端使用干净的
detached checkout。

这些脚本从不修改 TLS 校验，也不会保存云端地址、端口、密钥、token 或密码。
