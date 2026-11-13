# 本机 ROCm 10.1.0 NuGet 包

`eng/pack-local-runtime.py` 将当前已安装的 TheRock SDK 用户态文件制作成供本机使用的 NuGet Runtime 包。版本必须为 `10.1.0-local.<唯一构建标识>`。配套 Core 使用当前源码的 `0.11.0-dev`；这套本地包不覆盖 NuGet.org 上的 Core `0.10.0` 或 Ubuntu Runtime `7.2.1`。

## 包和宿主要求

| 包 | 内容 | 宿主仍需提供 |
| --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | 15 个 TFM 的托管程序集和 XML 文档 | 对应 .NET 运行时 |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` | HIP Runtime、HIPRTC/builtins、COMGR、kpack，共 5 个 DLL | Windows x64 AMD GPU 驱动、MSVC x64 Redistributable/UCRT |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` | HIP/HIPRTC、HSA、COMGR、LLVM/Clang、rocprofiler-register、kpack、rocdxg、私有系统依赖及装载别名，共 21 个文件 | Ubuntu 24.04 x64 系统库和 GPU 设备访问；WSL2 还需 Windows 驱动及宿主 DXCore |

SDK 标识为 `10.1.0`，其中 HIP Runtime 为 `7.16.26385`、HIPRTC 为 `9.0`。原生文件放入标准的 `runtimes/<rid>/native`；Linux 文件的 RPATH 在暂存副本中改为 `$ORIGIN`，原 SDK 不会修改。Linux 包同时包含 WSL 使用的用户态 rocdxg，但不包含 `/usr/lib/wsl/lib` 宿主文件或 Windows 驱动。裸 Linux/KFD 与其他 GPU 的适用性需另行验证。

## 本机生成

要求 Python 3.11+、.NET 10 SDK；Linux 另需 `patchelf` 和 `ldd`。在 Windows 源码根执行，所有输出目录均指定在源码外：

```powershell
$env:HIPSHARP_OUTPUT_ROOT = 'E:/GitSpace/HIP-CSharp-API/build/fixes'
$env:DOTNET_CLI_HOME = 'E:/GitSpace/HIP-CSharp-API/dependencies/dotnet-home'
$env:PATH = 'E:/GitSpace/HIP-CSharp-API/dependencies/dotnet;' + $env:PATH
dotnet pack src/JYPPX.ROCm.HipSharp/JYPPX.ROCm.HipSharp.csproj -c Release `
  -o E:/GitSpace/HIP-CSharp-API/build/local-nuget/feed
python eng/pack-local-runtime.py --hip-root D:/AMD/ROCm/10.1.0/HIP `
  --output-root E:/GitSpace/HIP-CSharp-API/build/local-nuget --version 10.1.0-local.20261113.3
```

Linux 在已启用 .NET 10 的 shell 中执行：

```sh
python3 eng/pack-local-runtime.py --hip-root /opt/AMD/ROCm/10.1.0/HIP \
  --output-root /mnt/e/GitSpace/HIP-CSharp-API/build/local-nuget --version 10.1.0-local.20261113.3
```

每次重新打包使用新的版本后缀和空暂存目录，避免 NuGet 缓存继续使用同版本的旧字节。打包器先校验 SDK 元数据、原生文件和现有许可证的固定 SHA-256；下载的许可证和源归档同样固定哈希。源归档和下载缓存保留在输出目录，包内含组件许可证、文件清单、源 URL、CycloneDX 清单以及打包审计记录。二进制清单依据本机当前安装确定，SDK 更新后必须重新审查清单，不能只修改版本号。

## 消费本地包

在应用目录中执行：

```powershell
dotnet nuget add source E:/GitSpace/HIP-CSharp-API/build/local-nuget/feed --name hipsharp-local
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.11.0-dev
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64 --version 10.1.0-local.20261113.3
```

Ubuntu 将最后一个包名换为 `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64`，本地源路径换为对应 Linux 路径。只添加目标平台的 Runtime 包，不要同时引用两个平台或混用不同 ROCm 闭包。

HIPRTC 的链接器在未显式指定链接目标时查询当前设备。使用 `HipRtcLinker` 前，先通过 `new HipRuntime().Initialize()` 定位并初始化同目录的 HIP Runtime；可参考 `HipRtcProgramLinker` 示例。编译简单内核不需要 SDK 的编译器可执行文件；自行包含外部头文件的源码仍需应用提供对应头文件。

## 验证入口与范围

```powershell
./eng/test-local-runtime-package.ps1 `
  -RuntimePackage E:/GitSpace/HIP-CSharp-API/build/local-nuget/feed/JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64.10.1.0-local.20261113.3.nupkg `
  -CorePackage E:/GitSpace/HIP-CSharp-API/build/local-nuget/feed/JYPPX.ROCm.HIP.CSharp.API.0.11.0-dev.nupkg `
  -OutputRoot E:/GitSpace/HIP-CSharp-API/build/local-nuget/validation/windows-final `
  -Architecture gfx1100
```

该入口创建独立 NuGet 缓存、仅使用本地源、以 PackageReference 构建 18 个工作负载；清除 HIP/ROCm SDK 搜索环境，并校验进程实际加载的原生模块来自消费者目录且哈希与包一致。Linux 上通过 PowerShell 7 使用同一入口，替换为 Linux 包和路径。能力不足的 managed memory 或 array/texture 子项应明确跳过；打包不会增加驱动本身不支持的能力。

验证范围是本机 Windows 和 Ubuntu 24.04/WSL2、W7900 `gfx1100`、.NET 10 的包消费运行。Core 的 15 TFM 构建能力与各 TFM 的原生包部署验证是不同范围。这些本地包没有经过原有公开发布 promotion 流程；Windows 本机 DLL 没有 Authenticode 签名，采用固定安装文件和来源元数据哈希记录，不修改旧 Windows `7.2.0` 清单的签名策略。公开再分发还需完成独立的来源、许可证/源码提供义务及目标机器验证。
