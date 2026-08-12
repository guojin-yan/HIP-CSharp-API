# ABI probe preparation / ABI 探针准备

M8.4 extends the C++ compile-time signature probe through 14 explicit-graph exports and the x64 layouts of `hipKernelNodeParams`, `hipMemsetParams`, and `hipMemAllocNodeParams`, in addition to the M8.3 memory-pool and earlier managed surface. `abi-evidence.schema.json` defines the environment, both header hashes, all 93 manifest symbols, handle sizes, enum/flag values, graph node types, the graph parameter layouts, and the normalized manifest hash produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M8.4 将 C++ 编译期签名探针扩展到 14 个显式 Graph 导出，以及 `hipKernelNodeParams`、`hipMemsetParams`、`hipMemAllocNodeParams` 的 x64 布局，并保留 M8.3 memory-pool 与更早的托管表面。`abi-evidence.schema.json` 定义基于官方 HIP 头文件生成的环境、两份头文件哈希、全部 93 个 manifest 导出符号、句柄尺寸、枚举/flags、Graph 节点类型、Graph 参数布局字段和 normalized manifest hash。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` with `-LibraryName amdhip64` or `-LibraryName hiprtc` against a real native library for a local export check. `tools/radeon/cloud-test.sh` performs both Linux symbol checks, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
