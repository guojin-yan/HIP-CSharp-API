# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes; loader and PE audit are static-verified | No Windows AMD GPU validation | Runtime 7.2.0 disabled, inventory-empty static skeleton |
| Linux x64 | Yes | M4-M6 passed historically on Ubuntu 24.04.4 / ROCm 7.2.1 / gfx1100 sessions; current exact packages are pending fresh authorization | Runtime 7.2.1 guarded exact-SHA candidate; non-publishable, unpublished, and not a broad support claim |

M1/M2/M4/M5/M6 validation passed on separately authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` sessions. M6 verified all 46 Runtime and 9 HIPRTC entries plus the 15 advanced Runtime paths; the one-device instance produced an explicit P2P skip rather than a multi-GPU claim. Those results do not validate packages built from a later commit. Windows HIP SDK 7.2 names and loader paths are implemented and statically audited, without a local SDK inventory or AMD GPU execution. These are validation results, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.

M8.2-M8.6 APIs are currently managed-only validated. M8.7 adds one public-API-only integration workload, an offline result-contract verifier, and official-host/package-only gate wiring for all five families. The workload and both shell gates compile or pass syntax/structural checks locally, but no new Owner-authorized Linux symbol, schema-7 ABI executable, Runtime, loader-map, real module-symbol content, occupancy result, or GPU execution was performed. Windows support for these families remains static-only and unverified.
