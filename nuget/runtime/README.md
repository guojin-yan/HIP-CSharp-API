# JYPPX.ROCm.HipSharp ROCm runtime

`JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` carries the audited ROCm 7.2.1 user-mode closure used by the managed-only `JYPPX.ROCm.HIP.CSharp.API` package. It never contains the AMD kernel driver, firmware, GPU device nodes, headers, compilers, static libraries, debug symbols, or a managed assembly.

The host must be Ubuntu 24.04 x64 with the AMD `amdgpu`/`amdkfd` kernel driver, `/dev/kfd`, `/dev/dri`, and the system libraries declared in `runtime-manifest.json`. The native files are resolved from the consuming application's `runtimes/linux-x64/native` directory. A conflicting mix of package-provided and system ROCm user-mode libraries is rejected.

The package includes its deterministic CycloneDX SBOM, per-component license texts, and the exact signed-source/file manifest. ROCm and HIP versions are fixed to 7.2.1/7.2.53211 for this package version.

The M8.7/M8.8 exact candidate passed official-host and package-only validation under the old Runtime package ID, and that old `7.2.1` package was later published with repository signing. The corrected package-family ID is a new identity and must be rechecked in a fresh public-feed consumer before it is treated as part of a corrected release. Technical verification is not publication permission.
