# ABI probe preparation / ABI 探针准备

M8.5 extends the C++ compile-time signature probe through the six module-owned function-attribute, occupancy, and cooperative-launch exports. It also records `hipFunction_attribute`, occupancy flags, and the cooperative-launch device attributes, while retaining the M8.4 graph layouts and earlier managed surface. `abi-evidence.schema.json` defines the environment, both header hashes, all 99 manifest symbols, handle sizes, enum/flag values, graph parameter layouts, and the normalized manifest hash produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M8.5 将 C++ 编译期签名探针扩展到 6 个 module-owned function attribute、occupancy 与 cooperative launch 导出，并记录 `hipFunction_attribute`、occupancy flags 和 cooperative launch 设备属性，同时保留 M8.4 Graph 布局与更早的托管表面。`abi-evidence.schema.json` 定义基于官方 HIP 头文件生成的环境、两份头文件哈希、全部 99 个 manifest 导出符号、句柄尺寸、枚举/flags、Graph 参数布局字段和 normalized manifest hash。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` with `-LibraryName amdhip64` or `-LibraryName hiprtc` against a real native library for a local export check. `tools/radeon/cloud-test.sh` performs both Linux symbol checks, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
