# Platform compatibility

| Platform | Core build/package | Prior GPU evidence | M5 runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes | No Windows AMD GPU validation | Disabled empty runtime skeleton; outside M5 |
| Linux x64 | Yes | M4 system ROCm passed on one Ubuntu 24.04.4 / ROCm 7.2.1 / gfx1100 session | Signed source/closure/licenses/SBOM pass locally; package and isolated GPU consumer blocked pending new authorization |

Prior M1/M2 validation passed on an authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` session. M4 adds 31 Runtime exports, 9 HIPRTC exports, official-header ABI schema 2, explicit streams/events, async memory/kernel and lease checks; the final Owner-authorized session passed the full cloud gate on one `gfx1100` environment. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results for one environment, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. The runtime remains disabled until a clean consumer proves these boundaries without `/opt/rocm` or another system ROCm user-mode library.
