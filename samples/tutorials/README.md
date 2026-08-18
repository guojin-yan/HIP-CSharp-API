# HIP Tutorial Samples

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

This directory is the step-by-step learning path for HIP-CSharp-API. The cases are grouped by capability:
runtime/device, memory, execution, kernels, graphs, multi-device, data objects, and the low-level C ABI.
Each case is a small executable that demonstrates one ownership or execution contract.

The verification described in this document was performed on Radeon Cloud with an AMD Radeon Graphics
device (`gfx1100`), ROCm 7.2.1, and .NET 10.0.110. Windows build and GPU execution have not been
validated for these tutorials; the Windows sections in each case are a reproduction guide only.

## Start Here

From a Linux ROCm host or a Radeon Cloud instance, clone the repository and run the complete tutorial
verification matrix:

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
bash ./samples/tutorials/run-cloud-verification.sh
```

The runner detects the first `gfxNNNN` target, restores in locked mode, builds only the tutorial projects,
runs every case, and writes one timestamped evidence directory. It deliberately builds the tutorial
projects individually so analyzer failures in unrelated repository tools do not hide tutorial results.

To select an architecture or evidence location explicitly:

```bash
HIPSHARP_GPU_ARCH=gfx1100 HIPSHARP_TUTORIAL_RECORD=/persistent/projects/hip-csharp-api/results/tutorials bash ./samples/tutorials/run-cloud-verification.sh
```

The same script accepts `HIPSHARP_PRECOMPILED_CODE_OBJECT=/path/to/vector-add.hsaco` for the
`PrecompiledModule` case. Without that variable, the case runs its usage path and is recorded as
`usage-only`, because the repository does not contain a portable precompiled code object.

## Radeon Cloud Result

The retained run is available at
[`Radeon_Cloud/records/20260818-161709-tutorials`](../../../Radeon_Cloud/records/20260818-161709-tutorials).
It contains `environment.md`, `build.log`, `results.csv`, and one log for every case.
The matrix result is **18 passed, 1 skipped, 1 usage-only, and 0 failed**.

| Module | Case | Cloud result | Evidence summary |
| --- | --- | --- | --- |
| Runtime/Device | `EnvironmentAndDevice` | Passed | Runtime/driver versions and one AMD device enumerated |
| Runtime/Device | `LoaderDiagnostics` | Passed | HIP native loader initialized |
| Memory | `LinearMemoryCopy` | Passed | H2D, D2D, and D2H round trip |
| Memory | `PinnedHostMemory` | Passed | Pinned host round trip |
| Memory | `PitchedMemory2D3D` | Passed | 2D/3D extents and physical pitch verified |
| Memory | `ManagedMemory` | Passed | Managed memory visibility round trip |
| Memory | `AsyncAllocationAndMemoryPool` | Passed | Stream-ordered allocation and pool round trip |
| Memory | `VirtualMemory` | Passed | Reserve/map/access/release path |
| Execution | `StreamAndEvent` | Passed | Asynchronous copy ordering and event synchronization |
| Execution | `AsyncVectorAdd` | Passed | Five lengths, two streams, events, expected error, 100 lifecycle repeats |
| Kernel | `HipRtcProgramLinker` | Passed | LLVM bitcode linked to a code object |
| Kernel | `HipRtcVectorAdd` | Passed | HIPRTC compile, launch, 20 repetitions, CPU verification |
| Kernel | `KernelOccupancy` | Passed | Occupancy and launch-plan query |
| Kernel | `ModuleGlobals` | Passed | Typed module-global round trip |
| Kernel | `PrecompiledModule` | Usage-only | Requires an external `.hsaco` or compatible code object |
| Graph | `ExplicitGraphDag` | Passed | Two-node DAG topology and launch |
| Graph | `GraphCaptureReplay` | Passed | Captured asynchronous memory round trip |
| Multi-device | `PeerToPeerCopy` | Skipped | The validation instance exposes one GPU |
| Data objects | `ArrayTextureSurface` | Passed | Array, texture, and surface ownership |
| Low-level ABI | `NativeAbiInterop` | Passed | Raw C ABI initialization and device count |

`PeerToPeerCopy` is a correct controlled skip, not a failure. Run it on an instance with at least two
devices and peer access between device 0 and device 1 to exercise the copy itself.

## Read the Cases in Order

| Stage | Module | Guide |
| --- | --- | --- |
| 01 | Runtime and device | [`01-RuntimeDevice`](01-RuntimeDevice/README.md) |
| 02 | Memory | [`02-Memory`](02-Memory/README.md) |
| 03 | Execution | [`03-Execution`](03-Execution/README.md) |
| 04 | Kernel | [`04-Kernel`](04-Kernel/README.md) |
| 05 | Graph | [`05-Graph`](05-Graph/README.md) |
| 06 | Multi-device | [`06-MultiDevice`](06-MultiDevice/README.md) |
| 07 | Data objects | [`07-DataObjects`](07-DataObjects/README.md) |
| 90 | Low-level ABI | [`90-LowLevel`](90-LowLevel/README.md) |

Each case directory contains an English `README.md` and a Chinese `README.zh-CN.md`. The case guide
contains the direct command, the cloud status, the expected output, a source walkthrough, and a Windows
build/run section.

## Build and Run on Windows

Windows execution has not been validated by this project. The following commands are provided for
source inspection and a best-effort local build only:

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet --version
dotnet restore .\HipSharp.sln --locked-mode
dotnet build .\HipSharp.sln --configuration Release --no-restore
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj --configuration Release -- --arch gfx1100
```

The installed SDK must satisfy the repository `global.json` (currently .NET SDK `10.0.300` with patch
roll-forward). Actual GPU execution additionally requires a matching AMD Windows driver, HIP Runtime,
HIPRTC, native library search paths, and a supported GPU architecture. A successful Windows build must
not be interpreted as a Windows runtime validation.

## Evidence Layout

```text
Radeon_Cloud/records/<run-id>-tutorials/
├── environment.md
├── build.log
├── results.csv
└── logs/
    ├── EnvironmentAndDevice.log
    ├── HipRtcVectorAdd.log
    └── ... one log per case
```

The logs are the authoritative evidence for the cloud run. They intentionally retain controlled skips and
usage-only paths so a reader can distinguish unsupported hardware from a failed implementation.
