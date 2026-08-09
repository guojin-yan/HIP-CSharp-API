# ABI probe preparation / ABI 探针准备

M1 records the evidence contract and provides a C++ compile-time signature probe plus evidence collectors. `abi-evidence.schema.json` defines the environment, header hash, symbol, and type-layout fields produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M1 建立证据格式，并提供 C++ 编译期签名探针与证据收集器。`abi-evidence.schema.json` 定义基于官方 HIP 头文件生成的环境、头文件哈希、导出符号和类型布局字段。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` against a real `libamdhip64.so` or `amdhip64_7.dll` for a local export check. `tools/radeon/cloud-test.sh` performs the Linux symbol check, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
