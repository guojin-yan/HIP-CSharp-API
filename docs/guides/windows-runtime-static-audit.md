# Windows runtime static audit / Windows Runtime 静态审计

M6 locks the ROCm 7.2 Windows SDK names `amdhip64_7.dll` and `hiprtc0702.dll` from AMD's build metadata. Loader candidates are deterministic: explicit path, application base, `runtimes/win-x64/native`, `ROCM_PATH`, `HIP_PATH`, installed version directories discovered under the official Program Files SDK root in stable order, then the operating-system search path.

Each loaded candidate must expose `hipInit` for Runtime or `hiprtcVersion` for HIPRTC before it is accepted. Modern targets use `NativeLibrary.Load/TryGetExport/Free`; .NET Framework uses `LoadLibraryExW/GetProcAddress/FreeLibrary`. A loaded candidate with the wrong identity is released and recorded as a failed diagnostic attempt.

M6 根据 AMD build metadata 固定 ROCm 7.2 Windows SDK 文件名 `amdhip64_7.dll` 和 `hiprtc0702.dll`。loader 候选顺序固定为：显式路径、application base、`runtimes/win-x64/native`、`ROCM_PATH`、`HIP_PATH`、在官方 Program Files SDK 根目录下实际发现并稳定排序的版本目录、操作系统搜索路径。

`eng/verify-windows-runtime.ps1` parses PE headers plus import/export directories and fails closed for malformed provenance, missing SDK archive/inventory hashes, invalid AMD Authenticode identity, missing licenses or SBOM evidence, undeclared/unsafe/non-DLL payloads, hash/size drift or oversize payloads, non-x64 PE files, missing identity exports, incomplete imported-DLL closure, driver-boundary imports, and incomplete verification flags. `eng/test-windows-runtime-skeleton.ps1` runs one positive structured-PE fixture and twelve rejection cases.

`eng/verify-windows-runtime.ps1` 会解析 PE header 及 import/export directory，并对 provenance 不完整、SDK archive/inventory hash 缺失、AMD Authenticode 身份无效、license/SBOM 缺失、未声明/路径逃逸/非 DLL payload、hash/size 漂移或超限、非 x64 PE、身份 export 缺失、imported DLL 闭包不完整、driver-boundary import 和 verification flags 不完整全部 fail closed。`eng/test-windows-runtime-skeleton.ps1` 执行一个结构化 PE 合成正例和十二个拒绝用例。

The current workstation has no installed AMD HIP SDK. Consequently `nuget/runtime-manifests/win-x64.json` remains `packEnabled=false`, `verified=false`, and has no file inventory. `-RequirePackable` and direct package creation remain blocked with `HIPSHARP1001`. This is a static skeleton, not Windows GPU evidence, a redistribution decision, or a supported-platform claim.

当前工作站未安装 AMD HIP SDK，因此 `nuget/runtime-manifests/win-x64.json` 继续保持 `packEnabled=false`、`verified=false`，且没有文件 inventory。`-RequirePackable` 和直接打包仍由 `HIPSHARP1001` 阻止。这只是静态骨架，不是 Windows GPU 证据、再分发结论或支持声明。
