# Interface coverage ledger

This directory contains the deterministic interface ledger for the pinned ROCm 7.2.1 model, with historical 0.x results and reviewed exact-package resource receipts.

Run from the repository root:

```powershell
pwsh -NoProfile -File ./eng/interface-coverage/generate-interface-coverage.ps1
```

The command reads `eng/interop/complete-api-model.json`, `eng/interop/interop-manifest.json`, `managed-interface-map.json`, `reviewed-classification.json`, and `reviewed-function-evidence.json`, then writes:

- `interface-coverage.jsonl`: exactly 477 JSON objects, one complete-model entry per line;
- `interface-coverage.md`: the human review summary.

The JSONL is ordered by ordinal `library` and `entryPoint`. The project-quality test reruns the generator, checks byte determinism, validates model/manifest cross-references, and fails closed on missing fields, duplicate entries, invalid dispositions, or unsupported evidence statuses.

`managed` rows map 109 ABI-manifest entries plus 130 reviewed high-level promotions to concrete unit-test sources. The original 100 entries retain `passed-historical` cloud status bound to the historical 0.x SHA. Seven resource interfaces now have `passed-exact-package` coverage, pinned to Core source `3d626ddf643e1bfef3a358ea3e26c3e5fedcdf38` and nupkg SHA-256 `0283698aec534b2d8442f3280468d3f65998ad8dca7938e940f54e82844aaae4`. The remaining 370 interfaces have no reviewed function-level cloud evidence. The 238 `raw-only-reviewed` dispositions remain explicit.

Five IPC entries were executed through the raw bindings in separate processes; two physical-memory sharing entries were executed through managed owners. Both public 7.2.1 and local .3 native closures passed their resource/lifetime checks. These receipts do not prove the current checkout, all managed IPC wrappers on a GPU, complete negative native coverage, external-memory/semaphore imports, graphics, or multi-GPU correctness. Managed fake-native IPC error/ownership regressions are recorded separately from GPU execution.

`reviewed-function-evidence.json` contains only reviewed identities and relative file/hash pins. Raw logs, temporary probes and packages stay outside source. The generated rows bind the receipt file's own SHA-256. Updating a receipt requires checking its source, logs and package identities; generating the ledger does not run the GPU or establish that the report contents are true.

Run the offline checks from the repository root (use `python3` on Linux):

```sh
python -B eng/interface-coverage/verify-function-evidence.py
python -B eng/interface-coverage/test-function-evidence.py
python -B eng/interface-coverage/verify-function-evidence.py --workspace <external-workspace> --output <external-report.json>
```

The first command checks the inventory only and explicitly reports no execution proof. CI also tests forged hashes, wrong package/source/scope, dirty or incomplete summaries, missing calls, same-process IPC, missing module audits and invalid paths. The full `--workspace` command rehashes the recovered evidence and exact packages, reads the fixture at its tested Git commit, checks package metadata, clean 24-case KFD receipts, native call/process markers and byte/lifetime assertions. It never downloads missing inputs. A missing historical Git object or missing external evidence is a failure. Keep the referenced record files byte-exact, including line endings, when copying them between hosts. The tracked receipt inventory uses LF line endings under `.gitattributes`; regenerate the ledger after editing it so its file hash also matches a clean checkout.

Current state is `implemented-local / partial-reviewed-exact-package-evidence`; this ledger does not authorize a cloud connection, publication, or a full-interface release. A reviewer must inspect the pinned evidence; hashes bind records and cannot independently prove GPU behavior.
