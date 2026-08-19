# JYPPX.ROCm.HipSharp ROCm runtime

`JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` carries the audited ROCm 7.2.1 user-mode closure used by the managed-only `JYPPX.ROCm.HIP.CSharp.API` package. It never contains the AMD kernel driver, firmware, GPU device nodes, headers, compilers, static libraries, debug symbols, or a managed assembly.

The host must be Ubuntu 24.04 x64 with the AMD `amdgpu`/`amdkfd` kernel driver, `/dev/kfd`, `/dev/dri`, and the system libraries declared in `runtime-manifest.json`. The native files are resolved from the consuming application's `runtimes/linux-x64/native` directory. A conflicting mix of package-provided and system ROCm user-mode libraries is rejected.

The package includes its deterministic CycloneDX SBOM, per-component license texts, and the exact signed-source/file manifest. ROCm and HIP versions are fixed to 7.2.1/7.2.53211 for this package version.

The payload source, dependency closure, licenses, and SBOM derive from the audited Ubuntu 24.04 baseline. Its manifest intentionally blocks final packing and publication until a fresh exact-package content/size audit and GPU validation produce a promotion receipt for this exact distribution-specific identity.
