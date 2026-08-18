# Interface coverage ledger

This directory contains the deterministic 0.x interface ledger for the pinned ROCm 7.2.1 model.

Run from the repository root:

```powershell
pwsh -NoProfile -File ./eng/interface-coverage/generate-interface-coverage.ps1
```

The command reads `eng/interop/complete-api-model.json`, `eng/interop/interop-manifest.json`, `managed-interface-map.json`, and `reviewed-classification.json`, then writes:

- `interface-coverage.jsonl`: exactly 477 JSON objects, one complete-model entry per line;
- `interface-coverage.md`: the human review summary.

The JSONL is ordered by ordinal `library` and `entryPoint`. The project-quality test reruns the generator, checks byte determinism, validates model/manifest cross-references, and fails closed on missing fields, duplicate entries, invalid dispositions, or unsupported evidence statuses.

`managed` rows map 109 ABI-manifest entries plus 82 reviewed high-level promotions to concrete unit-test sources. The original 100 entries retain `passed-historical` cloud status bound to the exact historical 0.x SHA. The nine 0.10.0 HIPRTC additions and all 82 M8.13 promotions remain `not-tested` until their own exact-SHA Radeon Cloud workloads run. `managed-next` is empty; the 238 `raw-only-reviewed` and 48 `deferred-capability` rows remain explicit. No row upgrades a symbol export or historical result into a current function-level pass.

Current state is `implemented-local / cloud-validation-open`; this ledger does not authorize a cloud connection, package publication, or `1.0.0` release.
