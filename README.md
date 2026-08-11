# HIP-CSharp-API

HIP-CSharp-API is a .NET binding for the AMD HIP Runtime and HIPRTC Direct C ABIs. The single `JYPPX.HipSharp` assembly keeps native declarations internal while exposing managed runtime, device, memory, runtime compilation, module, and kernel-launch APIs.

## M6 status

M6 adds selected stream-ordered allocation/free, managed-memory advice/prefetch, explicit P2P state/copy, and graph capture/instantiate/launch APIs. One normalized manifest now drives 55 declarations across the `LibraryImport` and `DllImport` branches. Managed owners retain native resources through pending stream work and captured graphs; P2P copies derive device ordinals from allocation and stream ownership. Missing optional exports normalize to `HipError.NotSupported`.

M5 remains the signed-source/runtime-package regression baseline: AMD's ROCm 7.2.1 Noble repository, a six-ELF HIP/HIPRTC/HSA/COMGR/rocprofiler-register closure, file/package SHA-256 values, component licenses, system/driver boundaries, deterministic reports, and a CycloneDX SBOM. The allowlisted payload is 415,070,520 bytes after the aliases required because NuGet does not preserve Debian symlinks; the verified final package is 162,892,126 bytes, so the topology remains one runtime package.

`JYPPX.HipSharp.Runtime.linux-x64` is enabled for guarded local packaging after its candidate and final package passed newly Owner-authorized isolated GPU consumers with no system ROCm user-mode libraries. M6 repeated the immutable final package as a non-publishable historical regression. The package is not published, direct `dotnet pack` remains guarded, and one validated environment is not a broad support claim.

| State | Result |
| --- | --- |
| Build | M4 core assembly and XML documentation build for all 15 TFMs |
| Package | The local core candidate, 15 TFM assets, and 4 clean consumer builds pass audit |
| M1 runtime-tested | Passed on Radeon Cloud Ubuntu 24.04.4 with ROCm 7.2.1 and HIP 7.2.53211 |
| M1 GPU-validated | Passed on one `gfx1100` AMD Radeon Graphics instance: enumerate, allocate, H2D/D2D/D2H, synchronize, and free |
| M2 GPU-validated | Passed on one authorized Radeon Cloud `gfx1100` instance: HIPRTC compile/log/code, module/function, five VectorAdd lengths x 20 repeats, synchronization, D2H, CPU comparison, and expected compile failure |
| Local managed gate | Passed 15 TFM builds, 56 unit tests, 9 quality tests, 1 package test, 4 clean consumers, loader diagnostics, sample builds, and DocFX |
| M4 GPU/ABI-validated | Passed on one Owner-authorized Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / gfx1100 session; not a broad support claim |
| M5 signed provenance/closure/licenses/SBOM | Passed locally for the pinned AMD ROCm 7.2.1 Noble index and six canonical ELF files |
| M5 runtime package/isolated GPU | Final package passed package-local loader/maps; M6 regression found 46 Runtime and 9 HIPRTC exports, ran five GPU workloads, and retained four fail-closed negatives |
| M6 local advanced API | 55-function generated ABI, 56+9+1 tests, ownership/error tests, advanced sample build, and Windows static-audit fixtures pass locally |
| M6 real GPU/ABI | Passed on an Owner-authorized ROCm 7.2.1 / HIP 7.2.53211 / gfx1100 session, including package-only regression; one visible GPU caused an explicit P2P skip |
| Supported | Not claimed for any runtime/OS/GPU combination |

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
./eng/verify-package.ps1 -PackagePath artifacts/packages/JYPPX.HIP.CSharp.API.0.0.0.nupkg
./eng/generate-runtime-metadata.ps1 -Check
./eng/test-runtime-supply-chain.ps1
./eng/test-windows-runtime-skeleton.ps1
./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json -Offline
```

The equivalent cross-platform core gate is `bash ./eng/build.sh Release`. Package output and audit results are written below ignored `artifacts/` directories.

`prepare-runtime.ps1` requires `gpg`, `gpgv`, and `tar`; on Windows it also discovers the standard Git for Windows `usr/bin` copies when they are not on `PATH`. It fails closed on a missing tool, unsigned metadata, an offline cache miss, or any package/file/ELF/license/SBOM mismatch. `pack-runtime.ps1` is the guarded entry point for the verified Linux package; the Windows runtime and incomplete manifests remain blocked.

For an Owner-authorized isolated GPU test, `pack-runtime.ps1 -Candidate` creates a non-publishable local-feed package from a clean SHA and a tool-generated attestation bound to the exact manifest, SBOM, and staging digest. Direct `dotnet pack` without the verified manifest remains fail-closed. After the candidate passes, `pack-runtime.ps1` rebuilds the verified final package, which must pass the isolated gate again.

## Architecture boundary

The implementation calls `amdhip64` and `hiprtc` directly. `eng/interop/interop-manifest.json` is the declaration source; the binding generator deterministically emits `LibraryImport` for .NET 7+ and `DllImport` for older targets. The two native libraries retain independent loading identities and diagnostics. Runtime errors become `HipException`; HIPRTC results become `HipRtcException`, including the compiler log when compilation fails. Device allocations, stream-ordered allocations, managed memory, graphs, graph executables, HIPRTC programs, and modules use explicit `IDisposable` ownership plus non-throwing `SafeHandle` final-release fallbacks.

`samples/HipRtcVectorAdd` retains the M2 path. `samples/HipStreamEventVectorAdd` retains the M4 stream/event path. `samples/HipAdvancedFeatures` adds stream-ordered allocations, graph replay, managed-memory hints, CPU/GPU comparison for five lengths, 100 owner lifecycles, and a verified P2P copy-or-skip path. Its optional stress mode submits large vector operations to multiple streams before synchronization, validates every lane against the CPU, and repeats allocation/release without reporting performance figures. These GPU paths require an explicit architecture and never write the code object to disk. Managed-only tests use replaceable native boundaries and make no GPU calls.

All public API XML comments use Chinese/English pairs. Run `./eng/docs.ps1` to generate the API reference and DocFX site under `_site`.

## License

Source code is prepared under Apache-2.0, the default proposed by the project plan. Packaged ROCm components retain their own component licenses and notices.

See [README.zh-CN.md](README.zh-CN.md), [framework compatibility](docs/compatibility/frameworks.md), [platform compatibility](docs/compatibility/platforms.md), [contributing](CONTRIBUTING.md), and [security policy](SECURITY.md).
