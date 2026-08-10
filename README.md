# HIP-CSharp-API

HIP-CSharp-API is a .NET binding for the foundational AMD HIP Runtime Direct C ABI. The single `JYPPX.HipSharp` assembly keeps native declarations internal under `Interop`, `Types`, `Loading`, and `Generated`, while exposing managed runtime, device, error, and device-memory APIs.

## M1 status

`0.0.0` is a local engineering candidate, not a nuget.org release. Versions beginning at `0.0.0` represent preview development without a prerelease suffix. M1 implements the initial `amdhip64` Direct P/Invoke vertical slice from one manifest: initialization, runtime and driver versions, device enumeration and selection, device names, synchronous memory allocation/copy/free, synchronization, and native error diagnostics. HIPRTC, modules, kernels, streams, and extended device properties remain out of scope.

| State | M1 result |
| --- | --- |
| Build | The core assembly and XML documentation build for all 15 TFMs |
| Package | The local core candidate and clean consumer builds are regression-tested |
| Runtime-tested | Passed on Radeon Cloud Ubuntu 24.04.4 with ROCm 7.2.1 and HIP 7.2.53211 |
| GPU-validated | Passed on one `gfx1100` AMD Radeon Graphics instance: enumerate, allocate, H2D/D2D/D2H, synchronize, and free |
| Supported | Not claimed for any runtime/OS/GPU combination |

## Target frameworks

The core project directly targets `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. The first hardware validation passed on Ubuntu 24.04.4 with ROCm 7.2.1, HIP 7.2.53211, and a `gfx1100` GPU through Radeon Cloud. Windows HIP SDK compatibility is retained in the design but has not received AMD GPU validation.

The .NET Core 3.1, .NET 5, .NET 6, .NET 7, .NET Framework 4.6, and .NET Framework 4.6.1 targets are end-of-support upstream. They are build/package compatibility targets, not a promise of security updates. .NET 8 and .NET 9 should also be evaluated against their current upstream support status before deployment.

## Packages

The core package is `JYPPX.HIP.CSharp.API`. It contains managed code and documentation only; it does not contain ROCm, a driver, or AMD native binaries. Future runtime package IDs are stable: `JYPPX.HipSharp.Runtime.linux-x64` and `JYPPX.HipSharp.Runtime.win-x64`. Their NuGet package versions match ROCm (`7.2.1` and `7.2.0` for the current skeletons). Their manifests are deliberately disabled until official provenance, dependency closure, component licenses, hashes, package size, and clean GPU validation are complete.

## Local verification

On Windows PowerShell, with the .NET 10 SDK installed:

```powershell
dotnet restore HipSharp.sln
./eng/build.ps1 -Configuration Release
./eng/test.ps1 -Configuration Release -NoBuild
./eng/verify-package.ps1 -PackagePath artifacts/packages/JYPPX.HIP.CSharp.API.0.0.0.nupkg
```

The equivalent cross-platform core gate is `bash ./eng/build.sh Release`. Package output and audit results are written below ignored `artifacts/` directories.

## Architecture boundary

The M1 implementation calls the foundational `amdhip64` C ABI directly. `eng/interop/interop-manifest.json` is the declaration source; `eng/generate-interop.ps1` deterministically emits `LibraryImport` for .NET 7+ and `DllImport` for older targets. The public layer converts native errors to `HipException`, records every native-library load attempt, and owns `hipMalloc` allocations through `IDisposable` plus a `SafeHandle` fallback.

The `samples/DeviceInfo` and `samples/MemoryCopy` projects demonstrate the two M1 workflows. They require a working HIP Runtime and AMD GPU. Radeon Cloud validation executes both samples; managed-only local CI compiles them without making GPU calls.

All public API XML comments use Chinese/English pairs. Run `./eng/docs.ps1` to generate the API reference and DocFX site under `_site`.

## License

Source code is prepared under Apache-2.0, the default proposed by the project plan. Native ROCm components, when evaluated in a later stage, will retain their own component licenses and notices.

See [README.zh-CN.md](README.zh-CN.md), [framework compatibility](docs/compatibility/frameworks.md), [platform compatibility](docs/compatibility/platforms.md), [contributing](CONTRIBUTING.md), and [security policy](SECURITY.md).
