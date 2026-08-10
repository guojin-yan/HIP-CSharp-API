# Radeon Cloud M1 validation

These scripts are execution helpers, not stored validation evidence. Use them only after the Owner authorizes the current Radeon Cloud instance and a clean detached checkout of an exact commit is available.

From the repository root:

```bash
bash ./tools/radeon/bootstrap.sh
bash ./tools/radeon/env-report.sh
bash ./tools/radeon/cloud-test.sh <40-character-commit>
```

`bootstrap.sh` installs the fixed .NET SDK used by the validated environment from Microsoft's official HTTPS endpoint. It keeps certificate verification enabled and is idempotent. `cloud-test.sh` uses `/persistent` for the NuGet cache when available and otherwise uses `/workspace/.nuget/packages`.

`cloud-test.sh` runs the managed build/test/package gate, verifies the required `libamdhip64.so` exports, compiles the ABI probe against the installed HIP headers, and executes the device-information and H2D/D2D/D2H samples. Raw outputs stay under ignored `artifacts/radeon-cloud/`; copy and review them into the external `Radeon_Cloud/records/<session>/` structure before treating the run as evidence. `env-report.sh` suppresses the cloud hostname and identifying GPU fields, and it does not request the GPU unique identifier.

If HTTPS access to the source host fails because the platform proxy certificate is not trusted, do not disable certificate verification. Create a Git bundle for the exact named commit/ref on the trusted local machine, verify its SHA-256 after transfer, and use a clean detached checkout on the cloud instance.

The scripts never change TLS verification and never store a cloud address, port, key, token, or password.
