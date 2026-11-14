# 1.0.0 manual publishing checklist / 1.0.0 人工发布清单

This checklist is a handoff only. It never authorizes push, tag creation, NuGet publication, GitHub Release, Actions, Pages, articles, or announcements. The completed `0.9.1` event and its exact hashes are historical evidence and must not be substituted for the `1.0.0` candidate.

本清单仅用于交接，绝不构成 push、tag、NuGet、GitHub Release、Actions、Pages、文章或公告授权。已完成的 `0.9.1` 事件及其精确 hash 是历史证据，不能替代 `1.0.0` 候选。

## State gates / 状态门禁

- `candidate-built`: exact clean-SHA Core `1.0.0` package, audit, manifest, attestation, API equivalence and local consumers exist; `publishable=false` and `releaseAuthorized=false`.
- `candidate-validated`: the same exact Core hash passed newly Owner-authorized official-host and isolated package-only GPU/ABI gates with public Runtime `7.2.1`; still unpublished.
- `release-authorized`: Owner separately approves the precise Git SHA, Core hash, tag, NuGet upload and GitHub Release actions. Technical readiness alone is insufficient.
- `published`: only after immutable tag/package publication and signature-aware public-feed smoke are complete.

## Pre-authorization review / 授权前复核

- Require a clean detached checkout at the exact 40-character candidate SHA and no source/package/protected-entry drift since validation.
- Recompute Core `1.0.0` nupkg size/SHA-256, normalized content digest, API snapshot hash, assembly/XML hashes for all 15 TFMs, nuspec `RepositoryCommit`, package entry manifest and attestation; require exact equality with the reviewed envelope.
- Require the fixed repository-signed public `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` `7.2.1` identity from `eng/published-runtime-7.2.1.lock.json` (SHA-256 `1b8463fccbc364b9e0b7a1bc58cfc76c7ecfc8f73f1e3eaac1bdfadb197dc23d`). Do not substitute an unsigned candidate or the historical generic Runtime.
- Confirm both system-native Core-only and package-only Core + Runtime modes were tested without source `bin`, staging, private cache or accidental `/opt/rocm` fallback.
- Require fresh exact-candidate loader/maps, owner symbols `91/91 + 18/18`, complete symbols with every absence recorded, schema-7 ABI, actual comparison counts, bounded reliability, and missing/tampered/mixed/wrong-package negatives. Run retained-allocation version protection as well as ordinary VMM; ordinary samples missed the native double-release defect.
- Record P2P as pass only with capability evidence; otherwise keep the honest skip. Do not add a performance claim.
- Review rendered English/Chinese README, `1.0.0` notes, compatibility matrix, EOL TFM warnings, icon, Apache-2.0 license, repository metadata and managed-only package boundary.
- Require Windows GPU validation of the exact Core and declared native closure, including RID/apphost and net48. The historical public Windows Runtime `7.2.0` skeleton remains disabled/static-only; the separately tested local `10.1.0-local.*` packages do not grant public redistribution approval.
- Run secret, endpoint, absolute-path, native payload, stale PackageId/assembly name and generated-output scans.

## Authorized release only / 仅获授权后执行

- Create `v1.0.0` only after explicit authorization; never move `v0.9.0`, `v0.9.1`, or any published tag.
- Upload Core `1.0.0` once. Never use `--skip-duplicate` and never replace an existing version.
- Create a GitHub Release only after package indexing and exact public-feed checks; attach no binary unless separately authorized.
- Download the repository-signed public package, verify its signature, compare all protected entries excluding `.signature.p7s`, and repeat isolated clean consumers plus the authorized public-feed GPU gate.
- Retain only redacted hashes, structured results and the release envelope. Never retain credentials, endpoints, raw cloud logs, native binaries or unique device identifiers.
