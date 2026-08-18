# Radeon Cloud tools

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

These scripts support two separate workflows on an Owner-authorized Radeon Cloud instance.

## Quick product experience

Run the platform-independent [`HeatDiffusion`](../../samples/showcases/HeatDiffusion) showcase from
the repository root. The runner is kept beside the showcase code:

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

The helper detects the GPU architecture, reuses any installed .NET 10 or later SDK and bootstraps only
when no compatible SDK is available, builds the
sample, and writes `summary.json` plus `heatmap.bmp` below `artifacts/heat-diffusion/`. It does not
require a detached checkout and does not create release-validation evidence. See the sample's
[Radeon Cloud walkthrough](../../samples/showcases/HeatDiffusion/README.md#run-on-radeon-cloud) for
the complete source walkthrough, profiles, artifact retrieval, and result interpretation. The previous
`tools/radeon/run-heat-diffusion.sh` path remains as a compatibility wrapper for existing notes.

## Validation gates

The remaining scripts are execution helpers, not stored validation evidence. Use them only after the
Owner authorizes the current Radeon Cloud instance and a clean detached checkout of an exact commit
is available.

From the repository root:

```bash
bash ./tools/radeon/bootstrap.sh
bash ./tools/radeon/env-report.sh
bash ./tools/radeon/cloud-test.sh <40-character-commit>
```

`bootstrap.sh` installs the fixed .NET SDK used by the validated environment from Microsoft's official HTTPS endpoint. It keeps certificate verification enabled and is idempotent. `cloud-test.sh` uses `/persistent` for the NuGet cache when available and otherwise uses `/workspace/.nuget/packages`.

`cloud-test.sh` runs the managed build/test/package gate, the core package audit and clean consumers (PowerShell is required for `eng/verify-package.ps1`), verifies the 109 managed-manifest exports, and verifies the complete pinned-header model against the installed ROCm headers and libraries. ROCm 7.2.1 on Linux exports 458 of 459 Runtime declarations; `hipExternalMemoryGetMappedMipmappedArray` is the single explicit reviewed exception. All 18 HIPRTC declarations must be exported. The gate also compiles the schema 7 owner ABI probe, including the M8.6 `hipModuleGetGlobal` signature, M8.5 module function-attribute/occupancy/cooperative-launch signatures and enums, M8.4 graph layouts, and the 0.10.0 HIPRTC Program/Linker signatures. It executes DeviceInfo, MemoryCopy, HIPRTC VectorAdd/negative compile, the HIPRTC Program/Linker exact-package workload, stream/event, advanced API paths, and the schema-versioned M8.2-M8.6 managed expansion workload. The HIPRTC workload exercises name lowering, bitcode retrieval, both `AddData` and `AddFile`, module execution, CPU/GPU comparison, and fail-closed lifecycle negatives. Only documented capability/export conditions may be skipped. The reliability gate compares every lane with the CPU without emitting timing or a performance claim.

Each `cloud-test.sh` invocation writes to a new `artifacts/radeon-cloud/<commit>/<UTC-run>/` directory and exports that exact path to the stress gate. This prevents evidence from another commit or prior attempt from being silently reused. Copy and review the selected run into the external `Radeon_Cloud/records/<session>/` structure before treating it as evidence. `env-report.sh` suppresses the cloud hostname and identifying GPU fields, and it does not request the GPU unique identifier. `cloud-stress.sh` may also be run after a Release build; its bounded profile can be adjusted with `HIPSHARP_STRESS_ROUNDS`, `HIPSHARP_STRESS_STREAMS`, `HIPSHARP_STRESS_LENGTH`, and `HIPSHARP_STRESS_LIFECYCLES`.

`runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression] [RUNTIME_PACKAGE_COMMIT]` is the isolated package gate. It must run in a newly Owner-authorized isolated container/rootfs with `HIPSHARP_ISOLATED_CONSUMER=1`, `/dev/kfd` and `/dev/dri` exposed, no `/opt/rocm`, no system `libamdhip64`/`libhiprtc`, and only the declared Ubuntu system libraries. It restores the core and Linux runtime packages from a local feed, builds six package consumers without source/staging references, verifies the 91 Runtime and 18 HIPRTC managed-manifest exports, records `readelf`/`ldd` and `/proc/<pid>/maps` paths, and runs DeviceInfo, MemoryCopy, HIPRTC VectorAdd, the HIPRTC Program/Linker exact-package workload, the two-stream/event VectorAdd, the M6 advanced API workload, M8.2-M8.6 managed expansion, and the bounded multi-stream stress profile. It then checks missing-dependency, package-tamper, core-only, and mixed-closure negatives. The package-tamper negative requires a nonzero package-verification exit and accepts only the verifier's hash/size mismatch or its documented NuGet `NU3005` repository-signature rejection; an unrelated failure does not satisfy the gate. A Runtime package containing NuGet's `.signature.p7s` entry is accepted only after `dotnet nuget verify --all` succeeds; the signed package itself remains the consumer input. `candidate` and `final` keep strict current-SHA binding. `regression` requires the immutable runtime package's own historical commit as the fifth argument, verifies that commit is an ancestor of the current checkout, and always records the package as non-publishable. It must not be run against an M4 instance or a host whose user-mode ROCm files are merely hidden by renaming.

If HTTPS access to the source host fails because the platform proxy certificate is not trusted, do not disable certificate verification. Create a Git bundle for the exact named commit/ref on the trusted local machine, verify its SHA-256 after transfer, and use a clean detached checkout on the cloud instance.

The scripts never change TLS verification and never store a cloud address, port, key, token, or password.
