# Interface coverage ledger

This directory contains the deterministic 0.x interface ledger for the pinned ROCm 7.2.1 model.

Run from the repository root:

```powershell
pwsh -NoProfile -File ./eng/interface-coverage/generate-interface-coverage.ps1
```

The command reads `eng/interop/complete-api-model.json`, `eng/interop/interop-manifest.json`, and `reviewed-classification.json`, then writes:

- `interface-coverage.jsonl`: exactly 477 JSON objects, one complete-model entry per line;
- `interface-coverage.md`: the human review summary.

The JSONL is ordered by ordinal `library` and `entryPoint`. The project-quality test reruns the generator, checks byte determinism, validates model/manifest cross-references, and fails closed on missing fields, duplicate entries, invalid dispositions, or unsupported evidence statuses.

`managed` rows map the existing 100 owner entries to concrete unit-test sources and historical combination workloads. Their `passed-historical` cloud status is bound to the exact 0.x SHA in the record and explicitly says it is not evidence for the current checkout. `managed-next`, `raw-only-reviewed`, and `deferred-capability` rows retain `not-tested` for function and negative coverage until a dedicated batch produces evidence. No row upgrades a symbol export into a function-level pass.

Current state is `implemented-local / cloud-validation-open`; this ledger does not authorize a cloud connection, package publication, or `1.0.0` release.
