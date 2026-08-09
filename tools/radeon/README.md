# Radeon Cloud M1 validation

These scripts are execution helpers, not stored validation evidence. Use them only after the Owner authorizes the current Radeon Cloud instance and a clean detached checkout of an exact commit is available.

From the repository root:

```bash
bash ./tools/radeon/env-report.sh
bash ./tools/radeon/cloud-test.sh <40-character-commit>
```

`cloud-test.sh` runs the managed build/test/package gate, verifies the required `libamdhip64.so` exports, compiles the ABI probe against the installed HIP headers, and executes the device-information and H2D/D2D/D2H samples. Raw outputs stay under ignored `artifacts/radeon-cloud/`; copy and redact them into the external `Radeon_Cloud/records/<session>/` structure before treating the run as evidence.

The scripts never change TLS verification and never store a cloud address, port, key, token, or password.
