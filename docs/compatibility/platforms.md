# Platform compatibility

| Platform | Core build/package | GPU evidence | Runtime package state |
| --- | --- | --- | --- |
| Windows x64 | Core builds all applicable TFMs; loader and PE paths pass static audit | No Windows AMD GPU validation | Runtime 7.2.0 disabled, inventory-empty static skeleton; unsupported for deployment |
| Linux x64 | Core `0.9.1` published; Core `1.0.0` candidate is unpublished | Public Core `0.9.1` + Runtime `7.2.1` passed M8.9 nuget.org-only package GPU/ABI validation on Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2 / single `gfx1100`; exact `1.0.0` bytes are not yet GPU-validated | Runtime `7.2.1` published and validated; optional to a compatible system ROCm installation |

M8.9 validated the repository-signed public `0.9.1`/`7.2.1` pair on an Owner-authorized package-only path: managed symbols `91/91` Runtime and `9/9` HIPRTC, complete symbols `458/459` Runtime with the reviewed `hipExternalMemoryGetMappedMipmappedArray` exception and `18/18` HIPRTC, schema-7 ABI, all five managed expansion stages with 1,127 CPU/GPU comparisons, 10 rounds across 4 streams, loader/maps confinement, and four fail-closed negatives. The one-device instance produced `skipped(device-count<2)` for P2P. No timing or performance claim is made. Those results are regression evidence, not authorization for different `1.0.0` package bytes.

The Linux closure consists of HIP Runtime/HIPRTC/builtins, HSA Runtime, COMGR, and rocprofiler-register user-mode files. Ubuntu supplies glibc, libstdc++, libgcc, zlib/zstd, libelf, libdrm, libnuma, and optional X11/GL interfaces at the declared minimums. The host supplies `amdgpu`/`amdkfd`, `/dev/kfd`, and `/dev/dri`; none are package payload. M5 proved these boundaries in an Ubuntu Base consumer without `/opt/rocm` or another system ROCm user-mode library, including package-local process maps and fail-closed missing/tampered/mixed-closure negatives.

Linux-first decision (2026-08-15): Windows Runtime may remain deferred without blocking Linux Core `1.0.0`, provided the support matrix continues to identify Windows Runtime as disabled/unverified and does not convert Core compile compatibility into a Windows GPU/runtime support claim. The exact `1.0.0` Core nupkg must still pass fresh official-host and isolated package-only GPU validation before it can be called validated.
