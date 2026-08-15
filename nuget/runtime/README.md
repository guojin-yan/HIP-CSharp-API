# JYPPX.ROCm.HipSharp ROCm runtime

`JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` carries the audited ROCm 7.2.1 user-mode closure used by the managed-only `JYPPX.ROCm.HIP.CSharp.API` package. It never contains the AMD kernel driver, firmware, GPU device nodes, headers, compilers, static libraries, debug symbols, or a managed assembly.

The host must be Ubuntu 24.04 x64 with the AMD `amdgpu`/`amdkfd` kernel driver, `/dev/kfd`, `/dev/dri`, and the system libraries declared in `runtime-manifest.json`. The native files are resolved from the consuming application's `runtimes/linux-x64/native` directory. A conflicting mix of package-provided and system ROCm user-mode libraries is rejected.

The package includes its deterministic CycloneDX SBOM, per-component license texts, and the exact signed-source/file manifest. ROCm and HIP versions are fixed to 7.2.1/7.2.53211 for this package version.

The corrected package-family ID passed fresh official-host, signed Ubuntu Base/PRoot package-only, repository-signature, nuget.org-only static, and public package GPU/ABI validation in M8.9. Its public signed SHA-256 is `21D0A2E511964923DE4BE2C7F1BF02CE19E9ABD9E9BF535CB915C7D7C81B5799`. That evidence applies to these immutable `7.2.1` bytes; a new Core package still needs its own exact-package validation. Technical verification is not publication permission.
