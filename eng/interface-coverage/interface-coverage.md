# Interface coverage ledger review

Generated from `eng/interop/complete-api-model.json`, `eng/interop/interop-manifest.json`, and `reviewed-classification.json`.
The ledger records evidence boundaries; a symbol export is not a function-level pass. Historical cloud evidence is bound to exact 0.x bytes and is not evidence for the current SHA.

## Inventory

- Total entries: 477 (Runtime 459, HIPRTC 18).
- Complete model: 477; managed owner manifest: 100.
- Disposition: managed 100, managed-next 91, raw-only-reviewed 238, deferred-capability 48.
- Cloud function evidence: historical pass 100, not-tested 377; export scan is tracked separately.

## Managed workload mapping

| Workload | Purpose | Unit source | Historical cloud scope |
| --- | --- | --- | --- |
| `device-info` (8) | device discovery and diagnostics | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipRuntimeTests.cs` | `official-host-device-info` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `m8.2-pitched-memory` (13) | pitched memory and copy ownership | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipPitchedMemoryTests.cs` | `m8.2-pitched-memory` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `m8.3-memory-pool` (11) | pool ownership and stream ordering | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipMemoryPoolTests.cs` | `m8.3-memory-pool` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `m8.4-explicit-graph` (20) | graph node ownership and dependency order | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipExplicitGraphTests.cs` | `m8.4-explicit-graph` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `stream-event` (10) | stream and event lifecycle | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipStreamEventMemoryTests.cs` | `stream-event-vector-add` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `memory-copy` (7) | basic allocation, copy, and synchronization | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipStreamEventMemoryTests.cs` | `memory-copy` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `advanced-features` (9) | managed memory and peer capability | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipAdvancedApiTests.cs` | `advanced-features` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `hiprtc-vector-add` (13) | HIPRTC code-object and module lifetime | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipRtcTests.cs` | `hiprtc-vector-add-and-negative-compile` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `m8.5-kernel-occupancy` (6) | kernel metadata and cooperative launch | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipKernelOccupancyTests.cs` | `m8.5-kernel-occupancy` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `module-global` (1) | borrowed module-global views | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipModuleGlobalTests.cs` | `m8.6-module-globals` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |
| `errors` (2) | error identity and diagnostic ownership | `tests/JYPPX.ROCm.HipSharp.UnitTests/HipRuntimeTests.cs` | `negative-compile-and-error-diagnostics` in `Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix` |

## Review boundaries

- `managed-next` is a planned ownership batch, not an implementation or test result.
- `raw-only-reviewed` retains the generated low-level declaration because no current managed contract is justified.
- `deferred-capability` requires a capability-specific cloud workload before promotion.
- All missing unit, function, or negative evidence is represented as `not-tested`; no status is inferred from an entry-point name.
- Current state: `implemented-local / cloud-validation-open`; `publishable=false`; `releaseAuthorized=false`.
