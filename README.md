# HIP-CSharp-API

HIP-CSharp-API is a .NET binding for the AMD HIP Runtime and HIPRTC Direct C ABIs. The single `JYPPX.HipSharp` assembly keeps native declarations internal while exposing managed runtime, device, memory, runtime compilation, module, and kernel-launch APIs.

## 0.9.0 release-candidate status

Core version `0.9.0` is the local M8.1 release candidate for API-freeze review. A committed API snapshot is compared with all 15 target frameworks, while NuGet package validation checks compatibility during packing. The selected stream-ordered allocation, managed-memory, P2P, and graph APIs retain the M6 ownership and error contracts described in the [freeze review](docs/guides/api-freeze.md).

The optional Linux Runtime remains version `7.2.1`. Its signed AMD Noble provenance, six-ELF closure, licenses, CycloneDX SBOM, 415,070,520-byte allowlist, and historical verified package remain regression inputs. A current candidate is bound to the exact clean Git SHA, embeds an explicitly unverified candidate manifest, and is always `publishable=false` until that exact Core/Runtime pair passes newly Owner-authorized isolated host and PRoot GPU gates.

| State | Result |
| --- | --- |
| Core candidate | `JYPPX.HIP.CSharp.API` `0.9.0`; local only, unpublished, and not yet declared stable |
| Public API | Frozen snapshot plus identical-surface comparison across all 15 TFMs; formal and diagnostic API are distinguished from sample-only and internal code |
| Interop ABI | One normalized manifest drives 55 declarations across `LibraryImport` and `DllImport` branches |
| Linux Runtime candidate | `JYPPX.HipSharp.Runtime.linux-x64` `7.2.1`; guarded, exact-SHA, non-publishable candidate |
| Historical Linux evidence | M4-M6 passed on separately authorized Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / `gfx1100` sessions; historical evidence does not validate the current packages |
| Current Linux cloud gate | Pending fresh Owner authorization for the exact candidate packages |
| Windows | Runtime `7.2.0` skeleton remains disabled, inventory-empty, static-audit-only, and GPU-unvalidated |
| Supported | Not claimed for any runtime/OS/GPU combination; no performance claim |

## Target frameworks

The core project directly targets `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. M1 and M2 hardware validation passed on an authorized Radeon Cloud Ubuntu 24.04.4 instance with ROCm 7.2.1, HIP 7.2.53211, and a `gfx1100` GPU. M2 verified 17 Runtime/Module exports, 9 HIPRTC exports, official-header ABI assertions, and VectorAdd lengths `1`, `127`, `256`, `1000`, and `1048576` for 20 repetitions each. Windows HIP SDK compatibility is retained in the design but has not received AMD GPU validation.

The .NET Core 3.1, .NET 5, .NET 6, .NET 7, .NET Framework 4.6, and .NET Framework 4.6.1 targets are end-of-support upstream. They are build/package compatibility targets, not a promise of security updates. .NET 8 and .NET 9 should also be evaluated against their current upstream support status before deployment.

## Packages

The core package is `JYPPX.HIP.CSharp.API`. It contains managed code and documentation only; it does not contain ROCm, a driver, or AMD native binaries. Runtime package IDs are stable: `JYPPX.HipSharp.Runtime.linux-x64` and `JYPPX.HipSharp.Runtime.win-x64`, with versions `7.2.1` and `7.2.0`. Linux provenance, closure, licenses, hashes, SBOM, package content, and one isolated `gfx1100` GPU environment are audited. Windows remains a disabled, inventory-empty M6 static skeleton; it is not a redistribution or support claim.

## Local verification

On Windows PowerShell, with the .NET 10 SDK installed:

```powershell
dotnet restore HipSharp.sln
./eng/generate-interop.ps1 generate -Check
./eng/build.ps1 -Configuration Release
./eng/test.ps1 -Configuration Release -NoBuild
./eng/verify-public-api.ps1 -Configuration Release
./eng/verify-package.ps1 -PackagePath artifacts/packages/JYPPX.HIP.CSharp.API.0.9.0.nupkg -ExpectedVersion 0.9.0
./eng/generate-runtime-metadata.ps1 -Check
./eng/test-runtime-supply-chain.ps1
./eng/test-windows-runtime-skeleton.ps1
./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json -Offline
```

The equivalent cross-platform core gate is `bash ./eng/build.sh Release`. Package output and audit results are written below ignored `artifacts/` directories.

`prepare-runtime.ps1` requires `gpg`, `gpgv`, and `tar`; on Windows it also discovers the standard Git for Windows `usr/bin` copies when they are not on `PATH`. It fails closed on a missing tool, unsigned metadata, an offline cache miss, or any package/file/ELF/license/SBOM mismatch. `pack-runtime.ps1` is the guarded entry point for the verified Linux package; the Windows runtime and incomplete manifests remain blocked.

For an Owner-authorized isolated GPU test, `pack-runtime.ps1 -Candidate` creates a non-publishable local-feed package from a clean SHA and a tool-generated attestation bound to the exact manifest, SBOM, and staging digest. Direct `dotnet pack` without an accepted manifest remains fail-closed. Historical package validation cannot promote a newly built candidate; the exact package must pass both isolated gates before any release decision.

## Architecture boundary

The implementation calls `amdhip64` and `hiprtc` directly. `eng/interop/interop-manifest.json` is the declaration source; the binding generator deterministically emits `LibraryImport` for .NET 7+ and `DllImport` for older targets. The two native libraries retain independent loading identities and diagnostics. Runtime errors become `HipException`; HIPRTC results become `HipRtcException`, including the compiler log when compilation fails. Device allocations, stream-ordered allocations, managed memory, graphs, graph executables, HIPRTC programs, and modules use explicit `IDisposable` ownership plus non-throwing `SafeHandle` final-release fallbacks.

`samples/HipRtcVectorAdd` retains the M2 path. `samples/HipStreamEventVectorAdd` retains the M4 stream/event path. `samples/HipAdvancedFeatures` adds stream-ordered allocations, graph replay, managed-memory hints, CPU/GPU comparison for five lengths, 100 owner lifecycles, and a verified P2P copy-or-skip path. Its optional stress mode submits large vector operations to multiple streams before synchronization, validates every lane against the CPU, and repeats allocation/release without reporting performance figures. These GPU paths require an explicit architecture and never write the code object to disk. Managed-only tests use replaceable native boundaries and make no GPU calls.

All public API XML comments use Chinese/English pairs. Run `./eng/docs.ps1` to generate the API reference and DocFX site under `_site`.

## License

Source code is prepared under Apache-2.0, the default proposed by the project plan. Packaged ROCm components retain their own component licenses and notices.

See [README.zh-CN.md](README.zh-CN.md), [framework compatibility](docs/compatibility/frameworks.md), [platform compatibility](docs/compatibility/platforms.md), [contributing](CONTRIBUTING.md), and [security policy](SECURITY.md).
