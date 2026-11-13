# NuGet dependency and deployment boundary

The HIP user-mode runtime dependencies can be distributed through NuGet. Keep the managed Core package separate from each platform's native Runtime package, so an application can choose packaged native libraries or an existing compatible system installation. A GPU driver and compatible GPU remain host prerequisites.

## Package selection

| Package | Current state | Purpose |
| --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | Published `0.10.0`; source `0.11.0-dev` is unpublished | Managed assembly and XML documentation for all 15 target frameworks; no application NuGet dependencies |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` | Published `7.2.1` | Audited ROCm 7.2.1 native closure for Ubuntu 24.04 x64 |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` | Disabled `7.2.0` project skeleton; unpublished | Requires a complete Windows native inventory and package validation before use |

For the existing Ubuntu 24.04 release, explicitly reference both packages:

```sh
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.10.0
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64 --version 7.2.1
```

This does not install a GPU driver or certify WSL2. See the [Linux runtime guide](linux-runtime-package.md) for the published package's host requirements and exact-package checks. On Windows, use the installed compatible HIP SDK until a Windows Runtime package is ready.

## What belongs in a package

| Dependency | Distribution boundary |
| --- | --- |
| HIP Runtime and HIPRTC/builtins | Native Runtime package, including the libraries and compiler resources required by its tested workloads |
| COMGR, HSA and other transitive user-mode components | Platform-specific closure, pinned to compatible component versions with provenance, licenses and hashes |
| AMD driver, firmware and GPU device access | Host installation; excluded from Runtime packages |
| Windows system DLLs, Linux loader/glibc and declared system libraries | Host prerequisites; excluded from the ROCm payload |
| MSVC runtime on Windows | Requires a supported Redistributable installation or separately reviewed, permitted app-local deployment |
| .NET runtime | Application deployment choice; a self-contained application may include it separately |
| .NET SDK, reference assemblies, MSTest and test SDK | Build/test dependencies restored normally; excluded from application Runtime packages |
| OpenCV packages used by `VisualInspection` | Separate sample dependencies restored through NuGet; not Core dependencies |
| MIGraphX, PyTorch/LibTorch, rocBLAS, MIOpen and the rest of the installed math stack | Not required by Core; applications using them need separate dependencies |

HIPRTC compile options and source includes can introduce additional resources. A native import list alone does not prove that runtime compilation will work after removing the installed SDK.

## Local 10.1.0 inventory and remaining work

The 2026-11-13 development host has a TheRock ROCm SDK labeled `10.1.0`, reporting HIP Runtime `7.16.26385` and HIPRTC `9.0`. These component versions are distinct from the published Runtime `7.2.1` and the old Windows `7.2.0` skeleton. Local development tests against the installed SDK do not validate a new standalone NuGet Runtime package.

| Platform | Preliminary inventory | Limit of this measurement |
| --- | --- | --- |
| Windows x64 | `amdhip64_7.dll`, `hiprtc0716.dll`, `hiprtc-builtins0716.dll`, `amd_comgr.dll`, `rocm_kpack.dll`: 142,187,008 uncompressed bytes | Candidate files only; dynamic dependencies and runtime compiler resources still need an isolated audit |
| Ubuntu 24.04 x64 / WSL2 | 17 unique SDK files reachable through `ldd` from HIP Runtime, HIPRTC and builtins: 289,766,033 uncompressed bytes | Static dependency closure only; excludes dynamic WSL loading, compiler resources, aliases and packaging overhead |

Neither measurement is a final package size. The existing ROCm 7.2.1 Runtime's historical verified final package was 162,892,126 compressed bytes; this does not predict the size of a 10.1.0-based package.

The five Windows candidates report `NotSigned`. The existing Windows manifest requires AMD Authenticode signatures, so it must not be enabled with these files as-is. Completing Windows packaging requires an explicit, verified source/provenance policy, the full license and dependency inventory, an SBOM, and tests against the extracted package without relying on the SDK's search paths. The old manifest's M6 workstation notes describe its historical audit, not the current host.

On Linux, the installed 10.1.0 closure includes shared LLVM/Clang libraries and nested `llvm/lib` and `rocm_sysdeps/lib` paths. Preserve or deliberately rewrite and validate the loader paths; the 7.2.1 flat `$ORIGIN` recipe cannot simply be reused. WSL-provided DXCore libraries and the Windows host driver remain host components. A WSL2 test does not replace a native Linux/KFD package gate.

For either platform, validate the exact extracted package using loading diagnostics, dependency traces, HIPRTC compilation and GPU execution, including missing/mixed-dependency failures. New native bytes require a new version and fresh package evidence; never replace an already published package's contents. The current inventory does not enable packing or publish any new version.
