# Runtime package projects

The Linux project is backed by the schema 2 signed-source manifest, dependency reports, licenses, SBOM, staging allowlist, and strict package audit tooling. It still intentionally fails during `Pack` with `HIPSHARP1001` until the exact package content and isolated clean-environment GPU validation are recorded. The Windows project remains an empty disabled skeleton.

Runtime package IDs remain stable (`JYPPX.HipSharp.Runtime.linux-x64` and `JYPPX.HipSharp.Runtime.win-x64`). Their NuGet package versions match the packaged ROCm release, such as `7.2.1` or `7.2.0`.

`eng/prepare-runtime.ps1` stages native assets outside Git under `runtimes/<rid>/native`. `eng/pack-runtime.ps1` invokes the same validator used by direct MSBuild packing; a property cannot bypass it. `eng/verify-runtime-package.ps1` audits the final ZIP allowlist, hashes, RID, licenses, SBOM, forbidden payload, managed assembly absence, and package size.

The current Linux decision is one package: 415,070,520 unpacked bytes and 162,813,488 bytes in the local preflight ZIP, below the 262,144,000-byte gate. Splitting is deferred because the four payload components form one lockstep ROCm 7.2.1 loader closure. This preflight is not a final nupkg size or a publication.
