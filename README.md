# HIP-CSharp-API

HIP-CSharp-API is a .NET binding for the AMD HIP Runtime and HIPRTC Direct C ABIs. The single `JYPPX.HipSharp` assembly keeps native declarations internal while exposing managed runtime, device, memory, runtime compilation, module, and kernel-launch APIs.

## M5 status

The M4 managed-only core candidate remains unchanged. M5 now has a signed-source lock for AMD's ROCm 7.2.1 Noble repository, a six-ELF HIP/HIPRTC/HSA/COMGR/rocprofiler-register closure, file/package SHA-256 values, component licenses, system/driver boundaries, deterministic reports, and a CycloneDX SBOM. The allowlisted payload is 415,070,520 bytes after the aliases required because NuGet does not preserve Debian symlinks; a local preflight compressed it to 162,813,488 bytes, so the current topology decision remains one runtime package.

`JYPPX.HipSharp.Runtime.linux-x64` is still blocked by `HIPSHARP1001`. No runtime nupkg is generated because this exact closure has not yet passed a newly Owner-authorized isolated GPU consumer with no system ROCm user-mode libraries. It is not published and is not a support claim.

| State | Result |
| --- | --- |
| Build | M4 core assembly and XML documentation build for all 15 TFMs |
| Package | The local core candidate, 15 TFM assets, and 4 clean consumer builds pass audit |
| M1 runtime-tested | Passed on Radeon Cloud Ubuntu 24.04.4 with ROCm 7.2.1 and HIP 7.2.53211 |
| M1 GPU-validated | Passed on one `gfx1100` AMD Radeon Graphics instance: enumerate, allocate, H2D/D2D/D2H, synchronize, and free |
| M2 GPU-validated | Passed on one authorized Radeon Cloud `gfx1100` instance: HIPRTC compile/log/code, module/function, five VectorAdd lengths x 20 repeats, synchronization, D2H, CPU comparison, and expected compile failure |
| Local managed gate | Passed generator/manifest checks, 25 unit tests, 7 quality tests, package audit, loader diagnostics, sample build, and DocFX |
| M4 GPU/ABI-validated | Passed on one Owner-authorized Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / gfx1100 session; not a broad support claim |
| M5 signed provenance/closure/licenses/SBOM | Passed locally for the pinned AMD ROCm 7.2.1 Noble index and six canonical ELF files |
| M5 runtime package/isolated GPU | Blocked pending new Owner authorization; pack guard remains enabled |
| Supported | Not claimed for any runtime/OS/GPU combination |

## Target frameworks

The core project directly targets `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. M1 and M2 hardware validation passed on an authorized Radeon Cloud Ubuntu 24.04.4 instance with ROCm 7.2.1, HIP 7.2.53211, and a `gfx1100` GPU. M2 verified 17 Runtime/Module exports, 9 HIPRTC exports, official-header ABI assertions, and VectorAdd lengths `1`, `127`, `256`, `1000`, and `1048576` for 20 repetitions each. Windows HIP SDK compatibility is retained in the design but has not received AMD GPU validation.

The .NET Core 3.1, .NET 5, .NET 6, .NET 7, .NET Framework 4.6, and .NET Framework 4.6.1 targets are end-of-support upstream. They are build/package compatibility targets, not a promise of security updates. .NET 8 and .NET 9 should also be evaluated against their current upstream support status before deployment.

## Packages

The core package is `JYPPX.HIP.CSharp.API`. It contains managed code and documentation only; it does not contain ROCm, a driver, or AMD native binaries. Runtime package IDs are stable: `JYPPX.HipSharp.Runtime.linux-x64` and `JYPPX.HipSharp.Runtime.win-x64`, with versions `7.2.1` and `7.2.0`. Linux provenance, closure, licenses, hashes, SBOM, and preflight size are audited, but its manifest remains disabled until package audit and isolated GPU validation complete. Windows remains an empty disabled skeleton.

## Local verification

On Windows PowerShell, with the .NET 10 SDK installed:

```powershell
dotnet restore HipSharp.sln
./eng/build.ps1 -Configuration Release
./eng/test.ps1 -Configuration Release -NoBuild
./eng/verify-package.ps1 -PackagePath artifacts/packages/JYPPX.HIP.CSharp.API.0.0.0.nupkg
./eng/generate-runtime-metadata.ps1 -Check
./eng/test-runtime-supply-chain.ps1
./eng/prepare-runtime.ps1 -Manifest ./nuget/runtime-manifests/linux-x64.json -Offline
```

The equivalent cross-platform core gate is `bash ./eng/build.sh Release`. Package output and audit results are written below ignored `artifacts/` directories.

`prepare-runtime.ps1` requires `gpg`, `gpgv`, and `tar`; on Windows it also discovers the standard Git for Windows `usr/bin` copies when they are not on `PATH`. It fails closed on a missing tool, unsigned metadata, an offline cache miss, or any package/file/ELF/license/SBOM mismatch. `pack-runtime.ps1` remains blocked until every manifest verification gate is evidenced.

For an Owner-authorized isolated GPU test, `pack-runtime.ps1 -Candidate` can create a non-publishable local-feed package from a clean SHA and a tool-generated attestation bound to the exact manifest, SBOM, and staging digest. Direct `dotnet pack` remains blocked; after the candidate passes, the verified final package is rebuilt and must pass the isolated gate again.

## Architecture boundary

The M2 implementation calls `amdhip64` and `hiprtc` directly. `eng/interop/interop-manifest.json` is the declaration source; `eng/generate-interop.ps1` deterministically emits `LibraryImport` for .NET 7+ and `DllImport` for older targets. The two native libraries retain independent loading identities and diagnostics. Runtime errors become `HipException`; HIPRTC results become `HipRtcException`, including the compiler log when compilation fails. Device allocations, HIPRTC programs, and modules use checked `IDisposable` ownership plus non-throwing `SafeHandle` final-release fallbacks.

`samples/HipRtcVectorAdd` retains the M2 path. `samples/HipStreamEventVectorAdd` adds two explicit streams, events, async H2D/kernel/D2H, CPU comparison for five lengths, and 100 lifecycle repetitions. It requires an explicit GPU architecture and never writes the code object to disk. Managed-only tests use replaceable native boundaries and make no GPU calls.

All public API XML comments use Chinese/English pairs. Run `./eng/docs.ps1` to generate the API reference and DocFX site under `_site`.

## License

Source code is prepared under Apache-2.0, the default proposed by the project plan. Native ROCm components, when evaluated in a later stage, will retain their own component licenses and notices.

See [README.zh-CN.md](README.zh-CN.md), [framework compatibility](docs/compatibility/frameworks.md), [platform compatibility](docs/compatibility/platforms.md), [contributing](CONTRIBUTING.md), and [security policy](SECURITY.md).
