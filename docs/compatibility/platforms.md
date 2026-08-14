# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Yes; loader and PE audit are static-verified | No Windows AMD GPU validation | Runtime 7.2.0 disabled, inventory-empty static skeleton |
| Linux x64 | Corrected Core pending fresh gate | M8.7/M8.8 validated the immutable old Core identity on Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2 / single `gfx1100`; `0.9.1` assembly-identity forward fix requires a new public-feed recheck | Runtime 7.2.1 is public; corrected Core/Runtime pairing is not yet release-validated and is not a broad support claim |

M8.7 validated the exact renamed candidate on separately authorized Radeon Cloud paths: managed symbols `91/91` Runtime and `9/9` HIPRTC, complete symbols `458/459` Runtime with one reviewed exception and `18/18` HIPRTC, schema-7 ABI, all five managed expansion stages with 1,127 CPU/GPU comparisons, and the bounded reliability run. The one-device instance produced `skipped(device-count<2)` for P2P rather than a multi-GPU claim. The result has no timing or performance claim. Windows HIP SDK 7.2 names and loader paths are implemented and statically audited, without a local SDK inventory or AMD GPU execution. These are validation results, not support claims. The Core package does not ship the driver or ROCm user-mode libraries.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.

The M8.7 exact candidate closed the real Linux GPU gap for M8.2-M8.6 only for the old immutable Core identity. The `0.9.1` forward fix changes the assembly and package bytes, so its newly built nupkg is not covered until a fresh exact-package gate passes. Windows support for these families remains static-only and unverified.
