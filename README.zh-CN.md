# HIP-CSharp-API

HIP-CSharp-API 为 AMD HIP Runtime 与 HIPRTC Direct C ABI 提供 .NET 绑定。项目只发布 `JYPPX.HipSharp` 一个程序集；原生声明保持 internal，公开托管 API 覆盖 runtime、设备、内存、运行时编译、module 与 kernel launch。

## M6 状态

M6 新增经过选择的 stream-ordered allocation/free、managed-memory advice/prefetch、显式 P2P 状态/复制和 graph capture/instantiate/launch API。单一 normalized manifest 现在驱动 `LibraryImport` 与 `DllImport` 两个分支的 55 个声明；托管 owner 会在 pending stream 工作完成前保留原生资源，缺少 optional export 时统一映射为 `HipError.NotSupported`。

M5 继续作为已验证的供应链/runtime package 回归基线：固定 AMD 官方 ROCm 7.2.1 Noble 签名仓库，得到 HIP/HIPRTC/HSA/COMGR/rocprofiler-register 六个真实 ELF 的最小闭包，并记录包级/文件级 SHA-256、组件许可证、system/driver 边界、确定性报告和 CycloneDX SBOM。由于 NuGet 不保留 Debian symlink，必要 loader/SONAME alias 以同哈希文件保存；allowlist 解包体积为 415,070,520 bytes，经验证候选包为 162,891,900 bytes，因此保留单包方案。

`JYPPX.HipSharp.Runtime.linux-x64` 的精确候选已在本阶段新授权、无 system ROCm 用户态库的隔离 GPU consumer 中通过，现可通过受控脚本在本地打包。包尚未发布，直接 `dotnet pack` 仍受门禁约束，单一环境验证也不构成广泛支持声明。

- Build：核心程序集、公开 API 及 XML 文档可构建全部 15 个 TFM。
- Package：本地核心包和代表性干净消费者会执行回归验证。
- M1 Runtime-tested：已在 Radeon Cloud 的 Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 环境通过。
- M1 GPU-validated：已在一个 `gfx1100` 实例完成设备枚举、分配、H2D/D2D/D2H、同步和释放。
- M2 GPU-validated：已在一个授权的 Radeon Cloud `gfx1100` 实例通过 HIPRTC 编译/日志/code、module/function、五种长度各 20 次 VectorAdd、同步、D2H、CPU 对比和预期编译失败。
- Supported：尚未对任何 runtime、操作系统或 GPU 组合作支持承诺。
- M4 本地 managed gate：generator/manifest、25 个 unit tests、7 个 quality tests、package audit、loader diagnostics、sample build 和 DocFX 已通过。
- M4 GPU/ABI-validated：已在一个 Owner 授权的 Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / `gfx1100` 会话通过；不构成广泛支持声明。
- M5 本地供应链：AMD 签名来源、六个 ELF 闭包、许可证、SBOM 与体积预检已通过。
- M5 runtime package/GPU：候选已通过 package-local loader/maps、31 个 Runtime exports、9 个 HIPRTC exports、四个 GPU workload 和四类 fail-closed 负例；最终包将重建并再次隔离验证。
- M6 本地高级 API：55-function generated ABI、所有权/错误测试、高级样例构建和 Windows 静态审计 fixture 已通过。
- M6 真实 GPU/ABI：等待 Owner 新授权的 Radeon Cloud 会话；不会把既有 M4/M5 证据冒充 M6 证据。

M1 与 M2 已在授权的 Radeon Cloud Ubuntu 24.04.4、ROCm 7.2.1、HIP 7.2.53211 和 `gfx1100` 实例通过。M2 验证了 17 个 Runtime/Module exports、9 个 HIPRTC exports、官方头文件 ABI，以及长度 `1`、`127`、`256`、`1000`、`1048576` 各 20 次 VectorAdd。该结果不等于广泛支持声明。Windows HIP SDK 兼容设计已保留，但尚无 AMD GPU 实测。`.NET Core 3.1`、`.NET 5/6/7`、`.NET Framework 4.6/4.6.1` 已经 EOL，仅作为构建和包兼容目标。

核心包 `JYPPX.HIP.CSharp.API` 不包含 ROCm 或 AMD 原生二进制，也不自动依赖 runtime 包。runtime 包 ID 固定为 `JYPPX.HipSharp.Runtime.linux-x64` 和 `JYPPX.HipSharp.Runtime.win-x64`，包版本使用对应 ROCm 版本。Linux manifest 的 schema 2 来源、闭包、许可证、SBOM、包内容和一次隔离 `gfx1100` GPU 环境均已审计；Windows 仍是禁用且无 inventory 的 M6 静态骨架，不构成再分发或支持声明。

`samples/HipRtcVectorAdd` 保留 M2 路径；`samples/HipStreamEventVectorAdd` 保留 M4 stream/event 路径；`samples/HipAdvancedFeatures` 增加 stream-ordered allocation、graph replay、managed-memory hint、五种长度 CPU/GPU 对比、100 次 owner 生命周期和 P2P enable-or-skip 路径。所有公开 API XML 注释采用中文/英文双语格式。运行 `./eng/docs.ps1` 可在 `_site` 生成 DocFX API 文档站点。

本地验证入口见英文 [README](README.md)。源码按总体方案的默认建议准备为 Apache-2.0；ROCm 组件未来仍须保留各自许可证和 NOTICE。
