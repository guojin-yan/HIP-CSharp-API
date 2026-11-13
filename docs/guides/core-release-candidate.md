# Reviewed Core candidate / Core 候选包发布

The release workflow publishes the exact Core nupkg validated on Windows and Linux. The newly rebuilt package from its managed test step is not uploaded. Native Runtime packages in the bundle are test dependencies only. This procedure does not authorize publication.

## Preparation and review / 准备与审查

1. Choose a stable version in `eng/Versions.props`, create its reviewed API snapshot, and commit. Build from that clean commit with `HIPSHARP_OUTPUT_ROOT` outside source. Use the same Core nupkg for both platform tests.
2. Run `eng/test-local-runtime-package.ps1` on both hosts. Summaries must record matching source/validation commits, `sourceTreeDirty=false`, package hashes, and all 23 Windows / 22 Linux cases. RID/apphost/net48 cases cannot be skipped. Windows needs cached Framework reference assemblies 1.0.3 or `-ReferenceAssembliesFeed` pointing to a reference-only feed.
3. Retain managed tests, all-TFM API checks, native ABI checks, DocFX clean-generation results and a support/limitations review. These are the evidence roles `managed`, `api`, `abi`, `docs`, `support`. Include source and package identities and retain workload logs alongside the review. Reviewers must inspect the report contents: the gate checks file hashes, not the truth of arbitrary report text.
4. Assemble a new directory outside source:

```powershell
python eng/release-candidate.py seal --core <core.nupkg> `
  --windows-runtime <windows.nupkg> --windows-summary <windows-summary.json> `
  --linux-runtime <ubuntu.nupkg> --linux-summary <linux-summary.json> `
  --managed <test-report> --api <api-report> --abi <abi-report> `
  --docs <docfx-report> --support <support-review> --output <new-directory>
```

For local rehearsal only, `seal`/`verify` accept `--allow-prerelease`. Neither publication workflow passes this switch. Older summaries missing clean-tree/deployment fields are rejected.

The resulting `candidate.json` binds the packages, summaries and five evidence files. Review the manifest AND its evidence, especially exact-package ABI, isolation, supported hosts and capability skips, then record its SHA-256 independently. A self-generated hash is not GPU proof or approval. `create-core-candidate-attestation.ps1` remains an optional package/API inventory with `publishable=false`; it does not replace review.

## Authorized publication / 获授权后发布

Zip the candidate contents with `candidate.json` at the archive root. Record the ZIP hash and stage it at an HTTPS download URL. Run **Stage reviewed Core candidate** at the exact reviewed source commit with the URL, ZIP hash and reviewed manifest hash. It verifies all bindings without executing bundle content, then uploads `hipsharp-release-candidate`. Supply that successful same-repository Actions run ID, version and manifest hash to **NuGet release**.

Configure the `nuget-production` GitHub environment with required reviewers and its `NUGET_API_KEY` secret. Environment protection is a repository setting; source changes cannot establish it. Tag pushes no longer trigger upload. The workflow checks the artifact run commit, version, every referenced hash and deployment receipts, then repeats the binding check immediately before push. Existing public versions are errors.

After push, it downloads the public package, runs `dotnet nuget verify --all`, and compares every uncompressed entry except `.signature.p7s`. ZIP timestamps/compression/order may differ. Nuspec, dependency metadata, README, licenses and assemblies must match.

Implementing or testing this procedure does not run publication, create a tag or establish a stable release. A stable candidate and explicit release instruction are still required.
