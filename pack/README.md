# Runtime package projects

The Linux project is backed by the schema 2 signed-source manifest, dependency reports, licenses, SBOM, staging allowlist, and strict package audit tooling. Candidate packing requires an exact clean-SHA candidate manifest and attestation; incomplete inputs fail with `HIPSHARP1001`. The Windows project remains an empty disabled skeleton.

Runtime package IDs remain stable (`JYPPX.ROCm.HipSharp.Runtime.linux-x64` and `JYPPX.ROCm.HipSharp.Runtime.win-x64`). Their NuGet package versions match the packaged ROCm release, such as `7.2.1` or `7.2.0`.

`eng/prepare-runtime.ps1` stages native assets outside Git under `runtimes/<rid>/native`. `eng/pack-runtime.ps1` invokes the same validator used by direct MSBuild packing; a property cannot bypass it. Before cloud evidence exists, `-Candidate` requires an ignored attestation bound to a clean Git SHA, manifest, SBOM, and complete staging digest and emits a non-publishable package whose embedded manifest remains unverified. `eng/verify-runtime-package.ps1` audits the candidate/final ZIP allowlist, hashes, RID, licenses, SBOM, forbidden payload, managed assembly absence, repository commit, and package size.

The current Linux decision is one package: 415,070,520 unpacked bytes, 162,891,900 bytes in the historical validated candidate, and 162,892,126 bytes in the historical verified final package, below the 262,144,000-byte gate. Splitting is deferred because the payload components form one lockstep ROCm 7.2.1 loader closure. A current candidate's audit records its own exact size and SHA-256; no package is published by these commands.
