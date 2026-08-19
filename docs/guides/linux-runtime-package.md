# Linux runtime package audit / Linux runtime 包审计

`JYPPX.ROCm.HIP.CSharp.API` remains managed-only and can use a system ROCm installation. The optional `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` package is a separate ROCm 7.2.1 user-mode closure for Ubuntu 24.04 x64. This distribution-specific package is not published and remains blocked until its exact package bytes pass content, size, provenance, and GPU validation.

The source lock accepts only AMD's signed Ubuntu Noble repository. `prepare-runtime.ps1` checks the pinned archive-key fingerprint, `InRelease`, `Packages.gz`, six Debian packages, canonical ELF metadata, file hashes, component licenses, closure boundaries, and CycloneDX hash. It stages only HIP Runtime, HIPRTC/builtins, HSA Runtime, COMGR, rocprofiler-register, loader/SONAME aliases, licenses, manifest, and SBOM. Headers, compilers, static libraries, debug symbols, rocminfo executables, rocm-core scripts, kernel drivers, and device nodes are forbidden.

NuGet ZIP extraction does not retain Debian symlinks. The allowlist therefore stores the versioned payload and only the generic/SONAME names needed by the managed loader or ELF/dynamic lookup as identical hashed files. All packaged ROCm libraries use `$ORIGIN` RPATH. The managed loader rejects package/system or two-directory Runtime/HIPRTC mixing.

The audited payload baseline is 415,070,520 unpacked bytes. The distribution-specific manifest deliberately records no final nupkg size or SHA-256 yet; those values must be generated from the exact Ubuntu 24.04 package and remain below the 262,144,000-byte gate before promotion.

Earlier Ubuntu 24.04 payload validation remains useful source and closure evidence, but it does not promote the current package identity. The exact distribution-specific nupkg must repeat loader traces, `readelf`, `ldd`, process maps, symbol coverage, DeviceInfo, MemoryCopy, HIPRTC VectorAdd, stream/event workloads, and missing-HSA/core-only/tampered/mixed-directory negatives.

The promotion verifier hashes every explicit input before parsing it, rejects partial or sensitive evidence, and binds the candidate package/audit/attestation/manifest/SBOM identities. No promotion lock or receipt is currently tracked for the Ubuntu 24.04 package. Direct `dotnet pack` remains blocked until `pack-runtime.ps1` can supply newly generated exact-package evidence.

Candidate-to-final comparison protects every native library, license, SBOM, Core XML document and managed assembly path. NuGet ZIP metadata, README, nuspec repository commit, promoted runtime manifest and embedded receipt are the only reviewed metadata changes. A separate final exact-package gate then checks the new bytes without modifying the packages or manifest, avoiding recursive package identity changes.
