# Platform compatibility

Core 1.0.0 and optional Runtime 10.1.0 packages are published. Core includes no GPU driver or native payload. Use compatible AMD hardware and a host driver; choose only the Runtime matching the host. The current package tests use one W7900/gfx1100 GPU.

| Platform | Exact package validation | Deployment requirements |
| --- | --- | --- |
| Windows x64 | Public Core + AMD-signed Runtime 10.1.0: 23 passed / 1 managed-memory capability skip, including net48, RID dotnet/apphost, published VectorAdd, missing-HIPRTC and module hash checks | Compatible AMD driver, MSVC x64 Redistributable/UCRT; optional packaged Runtime or system HIP |
| Ubuntu 24.04 x64 / WSL2 | Public Core + Runtime 10.1.0: 21 passed / 2 capability skips (managed memory, arrays), including interop and RID deployment | Compatible host driver, WSL DXCore/device access, system C/C++ libraries; optional packaged Runtime or system HIP |
| Native Linux/KFD | Earlier packages have historical KFD/PRoot evidence below; current 1.0.0 bytes have no new KFD run | Compatible amdgpu/amdkfd, /dev/kfd and /dev/dri; independently qualify host and GPU |

All 15 declared Core TFMs pass API/package checks; older frameworks may be out of upstream support. Two public Runtime packages pass signature/payload checks and preserve source/notices. See [Runtime distribution](../guides/runtime-distribution-10.1.0.md), [Core release notes](../releases/1.0.0.md), and [upstream native exclusions](../guides/upstream-runtime-issues.md). Full raw-function, multi-GPU and every resource combination are not established. Windows and Linux capability restrictions stay explicit; NotSupported is not a positive pass.

## Historical public 0.10.0 / 7.2.1 evidence

The remainder preserves the original host, package and release constraints of earlier validation. It does not replace the current scope above.

The `0.10.0` Core package is validated against the published `7.2.1` Runtime closure: managed symbols `91/91` Runtime and `18/18` HIPRTC, complete symbols `458/459` Runtime with the reviewed `hipExternalMemoryGetMappedMipmappedArray` exception, schema-7 ABI, HIPRTC Program/Linker lifecycle coverage, all five managed expansion stages with 1,127 CPU/GPU comparisons, 10 rounds across 4 streams, loader/maps confinement, and fail-closed isolated negatives. The one-device instance produces `skipped(device-count<2)` for P2P. No timing or performance claim is made.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.

Owner release boundary (2026-08-15): Windows AMD GPU Runtime/GPU validation and a separate explicit Owner release request are both mandatory before any future `1.0.0`. Linux validation cannot replace either condition. Until then, development and validation remain on `0.x.x`; `publishable=false` and `releaseAuthorized=false`.

The local Windows and WSL2 results above are development evidence, with a cooperative-launch subtest also skipped by device capability. They do not validate every raw ABI entry, external-resource descriptor, cross-device P2P path, or exact release package. Bare Linux and the pinned Runtime package still need separate regression. See [cross-platform validation](../guides/local-cross-platform-validation.md) for commands and boundaries.
