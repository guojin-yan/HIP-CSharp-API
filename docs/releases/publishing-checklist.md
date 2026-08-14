# Manual publishing checklist / 人工发布清单

This checklist is a handoff, not publication authorization. Complete it only after the M8.8 release envelope reports both final exact-package gates passed and an Owner separately authorizes push, tag, NuGet publication, and GitHub Release actions.

本清单只用于交接，不构成发布授权。仅当 M8.8 release envelope 记录两条 final 精确包门禁均通过，且 Owner 分别授权 push、tag、NuGet 发布与 GitHub Release 后执行。

- Confirm the NuGet account, organization ownership, package ID availability and 2FA recovery path without recording credentials.
- Recompute the final Git SHA, Core/Runtime nupkg sizes and SHA-256 values; require exact equality with the immutable release envelope.
- Review rendered English/Chinese README, release notes, icon, Apache-2.0 project license, component licenses/notices and CycloneDX SBOM.
- Confirm Core `0.9.1`, Runtime `7.2.1`, repository metadata, dependency policy, and the package signing strategy. Record the Owner decision for unlisting defective Core `0.9.0` separately.
- Require fresh official-host and package-only PRoot results for the exact forward-fix bytes; the M8.7/M8.8 `0.9.0` result cannot be promoted across the assembly-identity change. Check loader origin, symbols, ABI, 1,127 comparisons, reliability and all four negatives.
- Confirm Windows remains disabled/unverified/static-only, P2P is a single-device skip, and no timing or performance claim appears.
- Create the immutable tag only after explicit authorization; never move or overwrite a published version or tag.
- Publish one time only. On partial failure, stop and follow the documented rollback/forward-fix decision; never replace an existing NuGet version.
- Create the GitHub Release only after package publication is confirmed, with the same hashes and release notes.
- Download both packages back from `nuget.org`, recompute hashes as distribution evidence, and run a clean restore/build/smoke test from the public feed.
- Keep the release envelope, redacted validation record and post-publication smoke result; do not retain credentials, endpoints, raw cloud logs or unique device identifiers.
