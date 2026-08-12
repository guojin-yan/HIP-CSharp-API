# ABI probe preparation / ABI 探针准备

M8.2 extends the C++ compile-time signature probe through managed memory queries, pitched 2D/3D allocation, memset and copy, in addition to the M6 stream-ordered allocation, peer, graph, HIPRTC, and Runtime/Module surface. `abi-evidence.schema.json` defines the environment, both header hashes, all 68 per-library symbols, handle sizes, enum/flag values, `size_t`, `dim3`, `hipPos`, `hipPitchedPtr`, and `hipMemcpy3DParms` layout fields, and the normalized manifest hash produced from official HIP headers. Results are only created when the cloud script runs against a real HIP installation.

M8.2 将 C++ 编译期签名探针扩展到 managed memory 查询、pitched 2D/3D allocation、memset 和 copy，并保留 M6 的 stream-ordered allocation、peer、graph、HIPRTC 与 Runtime/Module 表面。`abi-evidence.schema.json` 定义基于官方 HIP 头文件生成的环境、两份头文件哈希、全部 68 个分库导出符号、句柄尺寸、枚举/flags、`size_t`、`dim3`、`hipPos`、`hipPitchedPtr`、`hipMemcpy3DParms` 布局字段和 normalized manifest hash。只有云端脚本针对真实 HIP 安装运行时才会产生结果。

Use `eng/verify-symbols.ps1` with `-LibraryName amdhip64` or `-LibraryName hiprtc` against a real native library for a local export check. `tools/radeon/cloud-test.sh` performs both Linux symbol checks, compiles `hip_abi_probe.cpp`, and writes the schema-shaped evidence. Generated reports belong under ignored `artifacts/`, not Git.
