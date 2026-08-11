# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes; M6 loader and PE audit are static-verified | No Windows AMD GPU validation | Disabled, inventory-empty M6 static skeleton |
| Linux x64 | Yes | M4 system ROCm, M5 package-local closure, and M6 advanced API/ABI passed on Ubuntu 24.04.4 / ROCm 7.2.1 / gfx1100 sessions | M5 verified guarded local package; M6 historical package regression passed; not published and not a broad support claim |

M1/M2/M4/M5/M6 validation passed on separately authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` sessions. M6 verified all 46 Runtime and 9 HIPRTC entries plus the 15 advanced Runtime paths; the one-device instance produced an explicit P2P skip rather than a multi-GPU claim. Windows HIP SDK 7.2 names and loader paths are implemented and statically audited, without a local SDK inventory or AMD GPU execution. These are validation results, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.
