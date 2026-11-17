# NuGet dependency and deployment boundary

Keep managed Core separate from optional native Runtime packages. Applications choose the packaged user-mode closure or a compatible system HIP installation. AMD hardware and its compatible host GPU driver remain required.

| Package | Published version | Purpose |
| --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | `1.0.0` | Managed assembly/XML across 15 TFMs, no application dependencies |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` | `10.1.0` | AMD-signed Windows x64 HIP/HIPRTC closure |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64` | `10.1.0` | Ubuntu 24.04 closure; native asset RID `linux-x64` |

```sh
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 1.0.0
# Choose one matching the host:
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64 --version 10.1.0
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64 --version 10.1.0
```

The new [10.1.0 distribution guide](runtime-distribution-10.1.0.md) records exact AMD sources, dependencies, signatures, complete source/notices and package-only validation. These packages need no installed HIP SDK PATH. Windows still needs MSVC/UCRT; Linux needs the declared system C/C++ libraries and device access. Current Linux GPU validation is WSL2/gfx1100, not a new current-Core KFD certification. Historical Ubuntu 7.2.1 remains immutable; old `10.1.0-local.*` packages keep their local-only policy.

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

## Historical installed-wheel audit (2026-11-13)

The following historical wheel audit predates the new signed distribution path and records its then-open tasks. It does not describe the published 10.1.0 packages.

The 2026-11-13 development host has a TheRock ROCm SDK labeled `10.1.0`, reporting HIP Runtime `7.16.26385` and HIPRTC `9.0`. These component versions are distinct from the published Runtime `7.2.1` and the old Windows `7.2.0` skeleton. Local development tests against the installed SDK do not validate a new standalone NuGet Runtime package.

| Platform | Preliminary inventory | Limit of this measurement |
| --- | --- | --- |
| Windows x64 | `amdhip64_7.dll`, `hiprtc0716.dll`, `hiprtc-builtins0716.dll`, `amd_comgr.dll`, `rocm_kpack.dll`: 142,187,008 uncompressed bytes | Candidate files only; dynamic dependencies and runtime compiler resources still need an isolated audit |
| Ubuntu 24.04 x64 / WSL2 | 17 unique SDK files reachable through `ldd` from HIP Runtime, HIPRTC and builtins: 289,766,033 uncompressed bytes | Static dependency closure only; excludes dynamic WSL loading, compiler resources, aliases and packaging overhead |

Neither measurement is a final package size. The existing ROCm 7.2.1 Runtime's historical verified final package was 162,892,126 compressed bytes; this does not predict the size of a 10.1.0-based package.

The five Windows candidates report `NotSigned`. The existing Windows manifest requires AMD Authenticode signatures, so it must not be enabled with these files as-is. Completing Windows packaging requires an explicit, verified source/provenance policy, the full license and dependency inventory, an SBOM, and tests against the extracted package without relying on the SDK's search paths. The old manifest's M6 workstation notes describe its historical audit, not the current host.

On Linux, the installed 10.1.0 closure includes shared LLVM/Clang libraries and nested `llvm/lib` and `rocm_sysdeps/lib` paths. Preserve or deliberately rewrite and validate the loader paths; the 7.2.1 flat `$ORIGIN` recipe cannot simply be reused. WSL-provided DXCore libraries and the Windows host driver remain host components. A WSL2 test does not replace a native Linux/KFD package gate.

For either platform, validate the exact extracted package using loading diagnostics, dependency traces, HIPRTC compilation and GPU execution, including missing/mixed-dependency failures. New native bytes require a new version and fresh package evidence; never replace an already published package's contents. The current inventory does not enable packing or publish any new version.
