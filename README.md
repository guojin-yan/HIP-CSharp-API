# HIP-CSharp-API

HIP-CSharp-API is a .NET binding for the AMD HIP Runtime and HIPRTC Direct C ABIs. The single `JYPPX.HipSharp` assembly keeps native declarations internal while exposing managed runtime, device, memory, runtime compilation, module, and kernel-launch APIs.

## M2 status

`0.0.0` is a local engineering candidate, not a nuget.org release. Versions beginning at `0.0.0` represent preview development without a prerelease suffix. M2 extends the single interop manifest with HIPRTC compile/log/code APIs and HIP Runtime module/function/launch APIs. The managed surface owns programs and modules, marshals device pointers and 32-bit scalar kernel arguments through `void**`, and retains M1 initialization, device, synchronous memory, and error behavior.

| State | Result |
| --- | --- |
| Build | M2 core assembly and XML documentation build for all 15 TFMs |
| Package | The local core candidate and clean consumer builds are regression-tested |
| M1 runtime-tested | Passed on Radeon Cloud Ubuntu 24.04.4 with ROCm 7.2.1 and HIP 7.2.53211 |
| M1 GPU-validated | Passed on one `gfx1100` AMD Radeon Graphics instance: enumerate, allocate, H2D/D2D/D2H, synchronize, and free |
| M2 GPU-validated | Pending a newly authorized Radeon Cloud session; HIPRTC compile and VectorAdd are not yet claimed as hardware-validated |
| Supported | Not claimed for any runtime/OS/GPU combination |

## Target frameworks

The core project directly targets `net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`. The M1 hardware validation passed on Ubuntu 24.04.4 with ROCm 7.2.1, HIP 7.2.53211, and a `gfx1100` GPU through Radeon Cloud. M2 HIPRTC and VectorAdd hardware validation requires a fresh authorized session. Windows HIP SDK compatibility is retained in the design but has not received AMD GPU validation.

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

The M2 implementation calls `amdhip64` and `hiprtc` directly. `eng/interop/interop-manifest.json` is the declaration source; `eng/generate-interop.ps1` deterministically emits `LibraryImport` for .NET 7+ and `DllImport` for older targets. The two native libraries retain independent loading identities and diagnostics. Runtime errors become `HipException`; HIPRTC results become `HipRtcException`, including the compiler log when compilation fails. Device allocations, HIPRTC programs, and modules use checked `IDisposable` ownership plus non-throwing `SafeHandle` final-release fallbacks.

`samples/HipRtcVectorAdd` compiles HIP C++ in memory, loads the code object, launches through `kernelParams`, synchronizes, copies the result to the CPU, and checks every element after every repetition. It requires an explicit GPU architecture and never writes the code object to disk. `samples/DeviceInfo` and `samples/MemoryCopy` retain the M1 workflows. Managed-only tests use replaceable native boundaries and make no GPU calls.

All public API XML comments use Chinese/English pairs. Run `./eng/docs.ps1` to generate the API reference and DocFX site under `_site`.

## License

Source code is prepared under Apache-2.0, the default proposed by the project plan. Native ROCm components, when evaluated in a later stage, will retain their own component licenses and notices.

See [README.zh-CN.md](README.zh-CN.md), [framework compatibility](docs/compatibility/frameworks.md), [platform compatibility](docs/compatibility/platforms.md), [contributing](CONTRIBUTING.md), and [security policy](SECURITY.md).
