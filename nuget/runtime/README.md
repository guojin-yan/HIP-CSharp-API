# JYPPX.HipSharp ROCm runtime

`JYPPX.HipSharp.Runtime.linux-x64` carries the audited ROCm 7.2.1 user-mode closure used by the managed-only `JYPPX.HIP.CSharp.API` package. It never contains the AMD kernel driver, firmware, GPU device nodes, headers, compilers, static libraries, debug symbols, or a managed assembly.

The host must be Ubuntu 24.04 x64 with the AMD `amdgpu`/`amdkfd` kernel driver, `/dev/kfd`, `/dev/dri`, and the system libraries declared in `runtime-manifest.json`. The native files are resolved from the consuming application's `runtimes/linux-x64/native` directory. A conflicting mix of package-provided and system ROCm user-mode libraries is rejected.

The package includes its deterministic CycloneDX SBOM, per-component license texts, and the exact signed-source/file manifest. ROCm and HIP versions are fixed to 7.2.1/7.2.53211 for this package version.

This candidate is not published and is not enabled until an isolated clean consumer with no system ROCm user-mode installation passes the real GPU gate.
