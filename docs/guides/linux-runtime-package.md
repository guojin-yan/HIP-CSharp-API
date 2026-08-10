# Linux runtime package audit / Linux runtime 包审计

`JYPPX.HIP.CSharp.API` remains managed-only and can use a system ROCm installation. The optional `JYPPX.HipSharp.Runtime.linux-x64` package is a separate ROCm 7.2.1 user-mode closure; it is not enabled or published during the blocked part of M5.

The source lock accepts only AMD's signed Ubuntu Noble repository. `prepare-runtime.ps1` checks the pinned archive-key fingerprint, `InRelease`, `Packages.gz`, six Debian packages, canonical ELF metadata, file hashes, component licenses, closure boundaries, and CycloneDX hash. It stages only HIP Runtime, HIPRTC/builtins, HSA Runtime, COMGR, rocprofiler-register, loader/SONAME aliases, licenses, manifest, and SBOM. Headers, compilers, static libraries, debug symbols, rocminfo executables, rocm-core scripts, kernel drivers, and device nodes are forbidden.

NuGet ZIP extraction does not retain Debian symlinks. The allowlist therefore stores the versioned payload and only the generic/SONAME names needed by the managed loader or ELF/dynamic lookup as identical hashed files. All packaged ROCm libraries use `$ORIGIN` RPATH. The managed loader rejects package/system or two-directory Runtime/HIPRTC mixing.

The payload is 415,070,520 unpacked bytes and 162,813,488 bytes in the local preflight ZIP. This supports a single-package topology below the 262,144,000-byte gate. The final nupkg size remains unset until the pack guard is opened by evidence.

The remaining gate must run on a newly authorized isolated GPU consumer with the AMD kernel driver and `/dev/kfd`/`/dev/dri`, but no `/opt/rocm`, system `libamdhip64`, system `libhiprtc`, source checkout, or staging path. Loader traces, `readelf`, `ldd`, process maps, symbols, DeviceInfo, MemoryCopy, HIPRTC VectorAdd, and the two-stream/event VectorAdd must all bind to the NuGet extraction directory.
