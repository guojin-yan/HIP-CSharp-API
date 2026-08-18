# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Core builds all applicable TFMs; loader and PE paths pass static audit | No Windows AMD GPU validation | Runtime 7.2.0 disabled, inventory-empty static skeleton; unsupported for deployment |
| Linux x64 | Core `0.10.0` published; the HIPRTC Program/Linker expansion is part of the managed API | Exact Core `0.10.0` + Runtime `7.2.1` validation runs on Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2 / single `gfx1100`; package-only evidence includes the required isolation and negative checks | Runtime `7.2.1` published and validated; optional to a compatible system ROCm installation |

The `0.10.0` Core package is validated against the published `7.2.1` Runtime closure: managed symbols `91/91` Runtime and `18/18` HIPRTC, complete symbols `458/459` Runtime with the reviewed `hipExternalMemoryGetMappedMipmappedArray` exception, schema-7 ABI, HIPRTC Program/Linker lifecycle coverage, all five managed expansion stages with 1,127 CPU/GPU comparisons, 10 rounds across 4 streams, loader/maps confinement, and fail-closed isolated negatives. The one-device instance produces `skipped(device-count<2)` for P2P. No timing or performance claim is made.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.

Owner release boundary (2026-08-15): Windows AMD GPU Runtime/GPU validation and a separate explicit Owner release request are both mandatory before any future `1.0.0`. Linux validation cannot replace either condition. Until then, development and validation remain on `0.x.x`; `publishable=false` and `releaseAuthorized=false`.
