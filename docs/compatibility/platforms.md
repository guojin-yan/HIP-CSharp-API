# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes; M6 loader and PE audit are static-verified | No Windows AMD GPU validation | Disabled, inventory-empty M6 static skeleton |
| Linux x64 | Yes | M4 system ROCm and M5 package-local closure passed on Ubuntu 24.04.4 / ROCm 7.2.1 / gfx1100 sessions; M6 advanced API GPU run pending | M5 verified guarded local package; not published and not a broad support claim |

Prior M1/M2/M4 validation passed on authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` sessions. M6 expands the manifest to 46 Runtime and 9 HIPRTC entries, but its 15 advanced Runtime symbols and execution paths have not yet received a newly authorized GPU run. Windows HIP SDK 7.2 names and loader paths are implemented and statically audited, without a local SDK inventory or AMD GPU execution. These are validation results, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.
