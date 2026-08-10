# ABI probe preparation / ABI 探针准备

M3 extends the evidence contract and C++ compile-time signature probe through HIPRTC, Runtime/Memory, Stream/Event, and Module/Launch. `abi-evidence.schema.json` defines the environment, both header hashes, per-library symbols, handle sizes, enum/flag values, `size_t` and `dim3` layout fields, and normalized manifest hash produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M3 将证据格式和 C++ 编译期签名探针扩展到 HIPRTC、Runtime/Memory、Stream/Event 和 Module/Launch。`abi-evidence.schema.json` 定义基于官方 HIP 头文件生成的环境、两份头文件哈希、分库导出符号、句柄尺寸、枚举/flags、`size_t`/`dim3` 布局字段和 normalized manifest hash。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` with `-LibraryName amdhip64` or `-LibraryName hiprtc` against a real native library for a local export check. `tools/radeon/cloud-test.sh` performs both Linux symbol checks, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
