# Reviewed Core candidate / Core 候选包发布

The release workflow publishes the exact Core nupkg validated on Windows and Linux. The newly rebuilt package from its managed test step is not uploaded. Native Runtime packages remain local test dependencies; public staging contains their identities and receipts, not their binaries. This procedure does not authorize publication.

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

Keep only the manifest and its listed files in the candidate directory; validation rejects unlisted files, symbolic links and Windows junctions. JSON objects must have unique keys. Bundle and ZIP paths must work on both Windows and Linux: reserved device names, illegal characters, case aliases and file/directory collisions are rejected before extraction. Store additional working notes outside the candidate directory.

For offline verification, set `HIPSHARP_SUPPORT_FEED` to an existing local directory of Microsoft framework/reference packages (and the pinned DocFX package for documentation). Consumer audits use source mapping so the tested HipSharp package can only resolve from its exact candidate feed. This also avoids a global `RestoreSources` override replacing the candidate source during a clean restore. Documentation tool restore uses a generated configuration containing only the offline source.

External build roots are mapped to a stable generated-source/PDB path, and Core packing normalizes NuGet timestamps, relationships and core-properties names before hashing or validation. Compare independent builds made with the same SDK and host; Windows/Linux XML line endings and compiler inputs are not claimed to produce identical package bytes. Never normalize or rebuild a package after its exact-package GPU evidence has been collected.

## Authorized publication / 获授权后发布

First export a Core-only candidate from the fully verified local bundle:

```powershell
python eng/release-candidate.py export-core --manifest <local-candidate/candidate.json> `
  --manifest-sha256 <reviewed-local-manifest-hash> --output <new-public-directory>
```

The exporter verifies the complete local bundle before copying the exact Core,
both platform summaries and all five evidence roles. Schema 2 adds the unchanged
local manifest and Runtime identity records (package ID/version/source/hash/size).
The Runtime nupkg files are omitted. Verification binds retained files to the
original manifest and the Runtime hashes to the deployment summaries. This is
portable evidence of the reviewed local inputs, not a fresh execution or Runtime
redistribution approval. Both publication workflows require `verify --public-only`
and reject a schema 1 bundle containing native dependencies.

Zip the exported contents with `candidate.json` at the archive root. Record the ZIP
and exported manifest hashes. Run **Stage reviewed Core candidate** at the exact
reviewed source commit using either an HTTPS download URL or a release asset ID
in this repository. A draft release can hold the Core-only ZIP; the workflow uses
its repository token to download it, so no public release/tag is needed for staging.
Never attach the original local Runtime bundle. The stage verifies all bindings
without executing bundle content, then uploads `hipsharp-release-candidate`.
Supply that successful same-repository Actions run ID, version, exported
manifest hash and full reviewed `source_commit` to **NuGet release**. The workflow
checks out that exact commit and requires it to belong to main history. Publishing
control changes can therefore retain the already reviewed package and receipts;
they do not substitute a newly built package for the reviewed bytes.

Configure the `nuget-production` GitHub environment with required reviewers and
the `NUGET_USER` Actions variable containing the NuGet profile name (not email).
In that NuGet account, create a [Trusted Publishing policy](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing)
for repository owner `guojin-yan`, repository `HIP-CSharp-API`, workflow file
`nuget-release.yml`, and environment `nuget-production`. The publish job alone
has `id-token: write`; the pinned official `NuGet/login` action exchanges GitHub
OIDC for a temporary API key immediately before upload. No long-lived NuGet API
key secret is used by this Core workflow. NuGet's account-side policy and GitHub
environment protection are external settings; source changes do not establish them.
Tag pushes do not trigger upload. The workflow checks the artifact run commit,
version, every referenced hash and deployment receipts, then repeats the binding
check immediately before push. Existing public versions are errors.

After push, it downloads the public package, runs `dotnet nuget verify --all`, and compares every uncompressed entry except `.signature.p7s`. ZIP timestamps/compression/order may differ. Nuspec, dependency metadata, README, licenses and assemblies must match.

Implementing or testing this procedure does not run publication, create a tag or establish a stable release. A stable candidate and explicit release instruction are still required.
