# Platform compatibility

| Platform | Core build/package | Prior GPU evidence | M5 runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes | No Windows AMD GPU validation | Disabled empty runtime skeleton; outside M5 |
| Linux x64 | Yes | M4 system ROCm and M5 package-local closure passed on Ubuntu 24.04.4 / ROCm 7.2.1 / gfx1100 sessions | Verified guarded local package; not published and not a broad support claim |

Prior M1/M2 validation passed on an authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` session. M4 adds 31 Runtime exports, 9 HIPRTC exports, official-header ABI schema 2, explicit streams/events, async memory/kernel and lease checks; the final Owner-authorized session passed the full cloud gate on one `gfx1100` environment. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results for one environment, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.
