# Native asset staging

The M5 pipeline accepts only the pinned AMD ROCm 7.2.1 Ubuntu Noble HTTPS repository and archive key declared by `nuget/runtime-manifests/linux-x64.json`. It verifies the key fingerprint, `InRelease` signature, signed `Packages.gz` hash, exact package name/version/architecture/URL/size/SHA-256, selected package entries, ELF64 SONAME/NEEDED/RPATH, file hashes, license hashes, dependency closure, and SBOM hash before staging.

```powershell
pwsh -NoProfile -File ./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json
pwsh -NoProfile -File ./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json -Offline
pwsh -NoProfile -File ./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json -VerifyOnly
pwsh -NoProfile -File ./eng/generate-runtime-metadata.ps1 -Check
pwsh -NoProfile -File ./eng/test-runtime-supply-chain.ps1
pwsh -NoProfile -File ./eng/test-runtime-source.ps1
```

`gpg`, `gpgv`, and `tar` are required. On Windows, `gpg` and `gpgv` are also discovered in standard Git for Windows installations when they are absent from `PATH`; explicit `-GpgPath`/`-GpgvPath` values remain authoritative. Online mode uses normal platform TLS/certificate validation; offline mode refuses cache misses. Missing tools, unsigned or changed metadata, wrong architecture, hash/size mismatch, path traversal, undeclared SONAME, dependency cycles, forbidden payload, missing license, or SBOM mismatch returns nonzero. No insecure fallback exists.

The `downloads`, `staging`, and `cache` subdirectories are ignored by Git. They contain `.deb`, extracted ELF files, aliases, and local reports and must never be committed. `pack-runtime.ps1` uses the staged allowlist but remains blocked until the manifest contains tool-verifiable package audit and isolated GPU evidence.
