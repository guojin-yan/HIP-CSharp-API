<h1 align="center">HIP CSharp API</h1>

<p align="center">
  Direct AMD HIP Runtime and HIPRTC bindings for C# and .NET, with managed resource owners and a complete generated low-level C ABI.
</p>

<p align="center">
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml"><img src="https://github.com/guojin-yan/HIP-CSharp-API/actions/workflows/ci.yml/badge.svg?branch=main" alt="CI" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/github/license/guojin-yan/HIP-CSharp-API.svg" alt="License" /></a>
  <a href="#-current-status-published-preview--10-candidate"><img src="https://img.shields.io/badge/status-1.0%20candidate-2563eb" alt="1.0 candidate" /></a>
  <a href="docs/compatibility/frameworks.md"><img src="https://img.shields.io/badge/.NET-net46%20to%20net10.0-512BD4" alt=".NET target frameworks" /></a>
  <a href="https://github.com/guojin-yan/HIP-CSharp-API/stargazers"><img src="https://img.shields.io/github/stars/guojin-yan/HIP-CSharp-API?style=flat&amp;label=Stars" alt="GitHub stars" /></a>
</p>

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

# HIP CSharp API

HIP CSharp API provides direct .NET bindings for the AMD HIP Runtime and HIPRTC C APIs. The single `JYPPX.ROCm.HIP.CSharp.API` assembly exposes the `JYPPX.ROCm.HipSharp` namespace, combining ergonomic managed owners for common GPU workflows with generated low-level entry points for applications that need the native ABI directly.

## 📖 Introduction

The project is designed around three boundaries:

- **Managed ownership:** device memory, pinned and managed memory, streams, events, modules, kernels, graphs, and HIPRTC programs use explicit `IDisposable` ownership.
- **Direct native access:** `HipRuntimeNativeApi` and `HipRtcNativeApi` expose the generated HIP C ABI without introducing a project-owned C++ bridge.
- **Explicit native runtime:** the managed package does not contain an AMD driver, ROCm installation, or native HIP libraries. Native loading and diagnostics remain visible to the caller.

The public API includes bilingual Chinese/English XML documentation and is kept identical across all declared target frameworks.

## ✨ Key Features

- Managed APIs for device discovery, allocation, copies, synchronization, streams, events, modules, kernels, and HIPRTC compilation.
- Advanced owners for stream-ordered allocation, managed-memory advice and prefetch, peer access, and HIP graph capture/replay.
- A complete generated low-level surface based on pinned HIP 7.2.1 headers: 459 HIP Runtime declarations and 18 HIPRTC declarations.
- Source-generated `LibraryImport` on .NET 7 and later, with `DllImport` compatibility on older targets.
- Deterministic interop generation, a frozen public API snapshot, package audits, and managed tests that do not require a GPU.
- A friend-only atomic stream enqueue/pending-callback boundary for the separately packaged MIGraphX adapter, without adding a public raw-handle API or a core dependency on MIGraphXSharp.
- Runnable correctness samples for memory copies, HIPRTC VectorAdd, stream/event ordering, graphs, managed memory, and P2P copy-or-skip behavior.

## 📢 Current Status: Published Preview / 0.x Validation

Core `0.9.1` and the optional Linux Runtime `7.2.1` are published on nuget.org. Their repository-signed public bytes passed nuget.org-only static consumers and a fresh package-only Linux GPU/ABI gate on 2026-08-14. Core `0.9.0` remains immutable and unlisted with its known unintended `JYPPX.ROCm.HipSharp` assembly identity; do not adopt it.

The source tree is validating an unpublished Core `0.9.2` interface-ledger batch with the same 68-type/1,002-member public surface as `0.9.1`. It tracks all 477 pinned HIP declarations without treating symbol scans as function tests. Windows AMD GPU validation and an explicit Owner release request are both required before any future `1.0.0`; this `0.9.2` work does not satisfy or bypass either condition.

## 🚀 Get Started In 30 Seconds

Install the published managed Core and choose either a compatible system ROCm installation or the optional published Linux Runtime package:

```powershell
dotnet add package JYPPX.ROCm.HIP.CSharp.API --version 0.9.1
# Optional on Ubuntu 24.04 x64:
dotnet add package JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64 --version 7.2.1
```

To run from source, use the .NET 10 SDK selected by `global.json` and a machine with a working AMD driver plus compatible HIP/ROCm user-mode runtime:

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet run --project .\samples\DeviceInfo\DeviceInfo.csproj -c Release
```

The essential managed API is intentionally small:

```csharp
using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Types;

var runtime = new HipRuntime();
runtime.Initialize();

HipRuntimeVersionInfo versions = runtime.GetVersionInfo();
Console.WriteLine($"HIP Runtime: {versions.RuntimeVersion}");
Console.WriteLine($"HIP Driver:  {versions.DriverVersion}");

foreach (HipDevice device in runtime.GetDevices())
{
    Console.WriteLine(device);
}
```

Applications using the managed package must still provide compatible native `amdhip64` and, when using runtime compilation, `hiprtc` libraries. See [platform compatibility](docs/compatibility/platforms.md) before choosing a deployment configuration.

## 📦 Package Layout

| Package | Native baseline | Contents | State |
| --- | --- | --- | --- |
| `JYPPX.ROCm.HIP.CSharp.API` | N/A | Managed `JYPPX.ROCm.HIP.CSharp.API` assembly exposing `JYPPX.ROCm.HipSharp`, XML docs, package README, logo, and license | `0.9.1` published; `0.9.2` interface-ledger validation batch is unpublished and not release-authorized |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64` | ROCm `7.2.1` | Audited Linux x64 ROCm user-mode closure, licenses, provenance, SBOM, and promotion receipt | `7.2.1` published and public-feed package-only validated; independently versioned from Core |
| `JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64` | HIP SDK `7.2.0` | No native inventory | Disabled static-audit skeleton; not a usable runtime package |

The Core package is dependency-free and never installs a GPU driver. Runtime packages are optional deployment artifacts with independent versioning and stricter publication gates. Read the [Linux runtime package guide](docs/guides/linux-runtime-package.md) for the exact native boundary.

## 🧩 API Surface

| Area | Main managed types |
| --- | --- |
| Runtime and devices | `HipRuntime`, `HipDevice`, `HipRuntimeVersionInfo` |
| Memory | `HipDeviceMemory`, `HipTypedMemory<T>`, `HipPinnedMemory`, `HipManagedMemory`, `HipAsyncDeviceMemory` |
| Streams and events | `HipStream`, `HipEvent`, `HipAsyncLease` |
| Runtime compilation | `HipRtc`, `HipRtcProgram`, `HipRtcCompilation`, `HipRtcException` |
| Modules and kernels | `HipModule`, `HipKernel`, `HipLaunchDimensions`, `HipKernelArgument` |
| Graphs and peer access | `HipGraph`, `HipGraphExec`, `HipPeerAccess` |
| Loading and diagnostics | `HipLibraryLocator`, `HipNativeLibraryLoader`, `HipLibraryLoadDiagnostics` |
| Complete native ABI | `HipRuntimeNativeApi`, `HipRtcNativeApi` |

Use managed owners for normal application code. Use the low-level API when exact native signatures and native ownership semantics are required.

## 🖥️ Platforms And Frameworks

The Core project directly targets 15 frameworks:

`net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, and `net10.0`.

| Platform | Core build/package | GPU validation | Runtime package |
| --- | --- | --- | --- |
| Linux x64 | Yes | Public Core `0.9.1` + Runtime `7.2.1` passed the M8.9 nuget.org-only package GPU/ABI gate on Ubuntu 24.04.4 / ROCm 7.2.1 / `gfx1100`; different `0.9.2` bytes require exact-SHA evidence | Published, receipt-verified Runtime `7.2.1`; optional to system ROCm |
| Windows x64 | Yes; loader and PE paths are statically audited | Not yet validated on an AMD GPU | Disabled 7.2.0 skeleton |

Several older .NET targets are end-of-support upstream and exist only for package compatibility. The full distinctions between build compatibility, historical validation, and supported deployment are documented in [framework compatibility](docs/compatibility/frameworks.md) and [platform compatibility](docs/compatibility/platforms.md).

## 🧪 Examples

| Sample | What it demonstrates |
| --- | --- |
| [`DeviceInfo`](samples/DeviceInfo) | Runtime/driver versions and device enumeration |
| [`MemoryCopy`](samples/MemoryCopy) | H2D, D2D, and D2H memory round trip |
| [`HipRtcVectorAdd`](samples/HipRtcVectorAdd) | In-memory HIPRTC compilation, module loading, kernel launch, and CPU verification |
| [`HipStreamEventVectorAdd`](samples/HipStreamEventVectorAdd) | Asynchronous copies, non-blocking streams, events, and ordering |
| [`HipAdvancedFeatures`](samples/HipAdvancedFeatures) | Stream-ordered allocation, graph replay, managed memory, lifecycle stress, and P2P copy-or-skip |

GPU samples require the actual target architecture. For example:

```powershell
dotnet run --project .\samples\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --length 1000 --repeat 20
```

These samples validate correctness and ownership behavior; they are not benchmarks and make no performance claim.

## 📚 Documentation

| Resource | Description |
| --- | --- |
| [Documentation index](docs/index.md) | DocFX entry point and bilingual guide index |
| [Complete native API](docs/guides/complete-native-api.md) | Generated low-level Runtime and HIPRTC surface |
| [HIPRTC VectorAdd](docs/guides/hiprtc-vectoradd.md) | Compile, load, launch, and verify a kernel |
| [Streams and events](docs/guides/hip-stream-event-vectoradd.md) | Asynchronous ordering and lifecycle guide |
| [Advanced APIs](docs/guides/advanced-apis.md) | Graphs, managed memory, stream-ordered allocation, and P2P |
| [Linux runtime package](docs/guides/linux-runtime-package.md) | Provenance, dependency closure, and packaging boundary |
| [Windows runtime audit](docs/guides/windows-runtime-static-audit.md) | Current static-only Windows state |
| [API reference](docs/api/toc.yml) | Generated public API reference |
| [MIGraphX adapter lease design](docs/design/migraphx-adapter-pending-lease.md) | Internal stream callback boundary; no public HipSharp API addition |

Run `./eng/docs.ps1` to build the DocFX site under `_site`.

## 🔨 Build From Source

The repository pins the .NET `10.0.300` SDK through `global.json`.

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet restore .\HipSharp.sln --locked-mode
.\eng\build.ps1 -Configuration Release -NoRestore
.\eng\test.ps1 -Configuration Release -NoBuild
.\eng\verify-public-api.ps1 -Configuration Release
```

On Linux, the equivalent Core gate is:

```bash
bash ./eng/build.sh Release
```

The build verifies deterministic interop output, all 15 target frameworks, tests, package contents, and public API consistency. Managed-only tests do not require an AMD GPU.

## 🗂️ Project Structure

```text
HIP-CSharp-API/
|-- src/JYPPX.ROCm.HipSharp/                 Managed owners and generated native APIs
|-- samples/                            Runnable device, memory, HIPRTC, and advanced samples
|-- tests/                              Unit, package, API-baseline, and repository-quality tests
|-- docs/                               DocFX reference, compatibility notes, guides, and releases
|-- eng/                                Build, generation, audit, packaging, and release gates
|-- nuget/                              Core/runtime package content and runtime manifests
|-- pack/                               Optional runtime package projects
|-- native/abi-probe/                   Native ABI verification probe
`-- .github/workflows/                  Continuous integration
```

## 🤝 Contributing

Issues and pull requests are welcome. Before changing the public API, native declarations, ownership behavior, package identity, or runtime payload, read [CONTRIBUTING.md](CONTRIBUTING.md) and the [API-freeze guide](docs/guides/api-freeze.md). Security reports should follow [SECURITY.md](SECURITY.md) rather than a public issue.

## 🙏 Acknowledgments

This project builds on [AMD HIP](https://github.com/ROCm/HIP) and the wider ROCm ecosystem. AMD remains the authoritative source for HIP behavior, platform requirements, and third-party runtime licensing.

## 📄 License

The project source is licensed under the [Apache License 2.0](LICENSE). Any packaged ROCm components retain their own licenses and notices; the project license does not replace those terms.

## 📮 Support And Contact

- [GitHub Issues](https://github.com/guojin-yan/HIP-CSharp-API/issues) for bugs and feature requests.
- [GitHub Discussions](https://github.com/guojin-yan/HIP-CSharp-API/discussions) for usage questions.
- QQ group `945057948` for community discussion.

## 📢 Software Notice

- **AI-assisted development:** AI tools were used to help generate, review, and optimize parts of the code and documentation.
- **Security intent:** the author states that the project contains no intentionally embedded backdoors, viruses, credential theft, or other malicious behavior.
- **Testing limits:** the project has not been validated on every operating system, driver, ROCm version, GPU architecture, or workload. A passing historical gate is not a universal support guarantee.
- **User responsibility:** perform independent review and representative testing before production, commercial, industrial, safety-critical, or mission-critical use. Users are responsible for evaluating fitness, reliability, licensing, and deployment risk.

Third-party binaries, source, and resources remain governed by their respective owners and licenses.

Copyright (c) 2026 Guojin Yan.
