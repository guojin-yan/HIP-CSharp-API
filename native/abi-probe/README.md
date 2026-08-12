# ABI probe preparation / ABI 探针准备

M8.6 extends the C++ compile-time signature probe with the exact `hipModuleGetGlobal(hipDeviceptr_t*, size_t*, hipModule_t, const char*)` declaration while retaining the M8.5 function-attribute, occupancy, and cooperative-launch checks. `abi-evidence.schema.json` schema 7 defines the environment, both header hashes, all 100 manifest symbols, handle sizes, enum/flag values, graph parameter layouts, and the normalized manifest hash produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M8.6 为 C++ 编译期签名探针新增精确的 `hipModuleGetGlobal(hipDeviceptr_t*, size_t*, hipModule_t, const char*)` 声明，并保留 M8.5 的 function attribute、occupancy 与 cooperative launch 检查。`abi-evidence.schema.json` schema 7 定义基于官方 HIP 头文件生成的环境、两份头文件哈希、全部 100 个 manifest 导出符号、句柄尺寸、枚举/flags、Graph 参数布局字段和 normalized manifest hash。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` with `-LibraryName amdhip64` or `-LibraryName hiprtc` against a real native library for a local export check. `tools/radeon/cloud-test.sh` performs both Linux symbol checks, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
