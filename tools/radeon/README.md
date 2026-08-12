# Radeon Cloud validation

These scripts are execution helpers, not stored validation evidence. Use them only after the Owner authorizes the current Radeon Cloud instance and a clean detached checkout of an exact commit is available.

From the repository root:

```bash
bash ./tools/radeon/bootstrap.sh
bash ./tools/radeon/env-report.sh
bash ./tools/radeon/cloud-test.sh <40-character-commit>
```

`bootstrap.sh` installs the fixed .NET SDK used by the validated environment from Microsoft's official HTTPS endpoint. It keeps certificate verification enabled and is idempotent. `cloud-test.sh` uses `/persistent` for the NuGet cache when available and otherwise uses `/workspace/.nuget/packages`.

`cloud-test.sh` runs the managed build/test/package gate, the core package audit and clean consumers (PowerShell is required for `eng/verify-package.ps1`), verifies the 100 managed-manifest exports, and verifies the complete pinned-header model against the installed ROCm headers and libraries. ROCm 7.2.1 on Linux exports 458 of 459 Runtime declarations; `hipExternalMemoryGetMappedMipmappedArray` is the single explicit reviewed exception. All 18 HIPRTC declarations must be exported. The gate also compiles the schema 7 owner ABI probe, including the M8.6 `hipModuleGetGlobal` signature, M8.5 module function-attribute/occupancy/cooperative-launch signatures and enums, and M8.4 graph layouts. It executes DeviceInfo, MemoryCopy, HIPRTC VectorAdd/negative compile, stream/event, advanced API paths, and the schema-versioned M8.2-M8.6 managed expansion workload. The workload reports pitched memory, pool, explicit graph, occupancy/cooperative launch, and module-global results independently; only documented capability/export conditions may be skipped. The reliability gate compares every lane with the CPU without emitting timing or a performance claim.

Each `cloud-test.sh` invocation writes to a new `artifacts/radeon-cloud/<commit>/<UTC-run>/` directory and exports that exact path to the stress gate. This prevents evidence from another commit or prior attempt from being silently reused. Copy and review the selected run into the external `Radeon_Cloud/records/<session>/` structure before treating it as evidence. `env-report.sh` suppresses the cloud hostname and identifying GPU fields, and it does not request the GPU unique identifier. `cloud-stress.sh` may also be run after a Release build; its bounded profile can be adjusted with `HIPSHARP_STRESS_ROUNDS`, `HIPSHARP_STRESS_STREAMS`, `HIPSHARP_STRESS_LENGTH`, and `HIPSHARP_STRESS_LIFECYCLES`.

`runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression] [RUNTIME_PACKAGE_COMMIT]` is the isolated package gate. It must run in a newly Owner-authorized isolated container/rootfs with `HIPSHARP_ISOLATED_CONSUMER=1`, `/dev/kfd` and `/dev/dri` exposed, no `/opt/rocm`, no system `libamdhip64`/`libhiprtc`, and only the declared Ubuntu system libraries. It restores the core and Linux runtime packages from a local feed, builds five package consumers without source/staging references, verifies 46 Runtime and 9 HIPRTC exports, records `readelf`/`ldd` and `/proc/<pid>/maps` paths, runs DeviceInfo, MemoryCopy, HIPRTC VectorAdd, the two-stream/event VectorAdd, the M6 advanced API workload, and the bounded multi-stream stress profile, then checks missing-dependency, package-tamper, core-only, and mixed-closure negatives. `candidate` and `final` keep strict current-SHA binding. `regression` requires the immutable runtime package's own historical commit as the fifth argument, verifies that commit is an ancestor of the current checkout, and always records the package as non-publishable. It must not be run against an M4 instance or a host whose user-mode ROCm files are merely hidden by renaming.

If HTTPS access to the source host fails because the platform proxy certificate is not trusted, do not disable certificate verification. Create a Git bundle for the exact named commit/ref on the trusted local machine, verify its SHA-256 after transfer, and use a clean detached checkout on the cloud instance.

The scripts never change TLS verification and never store a cloud address, port, key, token, or password.
