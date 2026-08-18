# HeatDiffusion

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

`HeatDiffusion` is the first end-to-end showcase in the repository. It solves the same two-dimensional heat equation twice: once with a C# `Parallel.For` reference implementation and once with a HIP C++ stencil compiled at runtime by HIPRTC. The sample proves that the GPU result is correct before it reports timing and writes a heatmap.

This is a practical tutorial. Follow it from top to bottom to get a working run, then use the source walkthrough to connect each step to HIP-CSharp-API concepts.

All commands below assume the repository root, the directory containing `HipSharp.sln`:

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
```

<p align="center"><img src="assets/heat-diffusion-result.png" alt="HeatDiffusion GPU heatmap" width="520" /></p>

## What You Will Build

At the end of this tutorial you will have a repeatable workload that:

1. Creates a deterministic temperature field with fixed boundaries, hot sources, and a cooling source.
2. Computes the final field on the CPU as a correctness oracle.
3. Compiles `Kernels/heat-diffusion.hip` for the target AMD architecture with HIPRTC.
4. Runs hundreds of stencil iterations using device memory, a non-blocking stream, events, and a graph.
5. Compares every CPU and GPU value, writes `summary.json`, and optionally writes `heatmap.bmp`.

This is an application-level example, not a benchmark harness. The reported speedup describes the current process, device, driver, workload, and number of measured runs only.

## Before You Start

### Supported execution environments

The workload requires an AMD GPU with HIP Runtime and HIPRTC. The checked-in cloud runner targets an Ubuntu 24.04 ROCm environment. A local Linux ROCm installation can run the C# project directly.

| Environment | Build | Run GPU workload | Notes |
| --- | --- | --- | --- |
| Radeon Cloud Ubuntu/ROCm | Yes | Yes | Use the colocated shell runner |
| Local Linux/ROCm | Yes | Yes | Pass the architecture reported by `rocminfo` |
| Windows | Yes | With a compatible HIP/ROCm setup | The documented runner is Linux-oriented |

You need a .NET 10 SDK, HIP Runtime, HIPRTC, and a visible AMD device. Direct `dotnet` commands run from the repository root use the SDK selected by `global.json` (currently `10.0.300`). The colocated cloud runner invokes a compatible SDK from the repository parent directory, which is why it also works on cloud images with a different installed feature band. Check the environment before troubleshooting:

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --info
```

The first command prints the architecture string used by HIPRTC, for example `gfx1100`. Use the value from your machine; do not copy `gfx1100` blindly.

## Five-Minute Run

### 1. Run a small smoke test

On a Linux ROCm host, replace `gfx1100` with the value from `rocminfo`:

```bash
dotnet restore ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj --locked-mode
dotnet build ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-restore
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-build --no-restore -- \
  --arch gfx1100 --profile tiny
```

`tiny` uses a 256 x 256 grid and 50 iterations. It is the fastest way to check device discovery, HIPRTC compilation, kernel launch, result validation, and image generation.

### 2. Run the default demonstration

The `quick` profile uses a 1536 x 1536 grid and 600 iterations, which is large enough to make the CPU/GPU execution paths visible while remaining convenient for a cloud tutorial:

```bash
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  --configuration Release --no-build --no-restore -- \
  --arch gfx1100 --profile quick
```

The process exits with code `0` only when the GPU result passes the numerical tolerances. A failed validation is an error even if a heatmap was produced.

<a id="run-on-radeon-cloud"></a>

### 3. Run it on Radeon Cloud

The script beside the project performs the environment checks, selects an installed .NET SDK, detects the GPU architecture, restores in locked mode, builds Release, and runs the sample:

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

The script defaults to `quick`. For a longer demonstration or a diagnostic run:

```bash
HIPSHARP_HEAT_PROFILE=showcase \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh

HIPSHARP_GPU_ARCH=gfx1100 \
HIPSHARP_HEAT_OUTPUT=/persistent/projects/hip-csharp-api/results/heat-diffusion \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh \
  --gpu-runs 5
```

The runner creates a timestamped directory under `artifacts/heat-diffusion/` unless `HIPSHARP_HEAT_OUTPUT` is set. Keep that directory when you want to compare runs or share a result.

## What Happens During a Run

The output is easier to understand if you know the order of operations:

1. `Options` resolves the profile, grid size, iteration count, architecture, and output directory.
2. `HeatProblem` creates `fixedField` and `initialField` with the same deterministic values for both implementations.
3. `CpuHeatSolver` runs the reference calculation with `Parallel.For` and double buffering.
4. `GpuHeatSolver` initializes HIP, selects the first device, and compiles the HIP source with `--offload-arch=<gfxNNNN> -O3`.
5. The GPU path allocates current values, next values, and the immutable fixed field on the device.
6. Initial data is copied over a non-blocking stream, followed by a warm-up launch.
7. Two opposite stencil launches (`current -> next`, then `next -> current`) are captured as a graph when graph capture is supported. An odd final iteration is launched directly.
8. Each measured run records HIP events around the kernel work, copies the final field back, and synchronizes once. The reported GPU time is the median of the requested runs.
9. `ErrorMetrics` compares the CPU and GPU arrays. Only then are the JSON summary and heatmap written.

The two-buffer alternation is important: a stencil step reads the previous field while writing a new field. Capturing two steps lets the same graph be replayed for long even workloads without rebuilding it for every iteration count.

## The Mathematical Workload

For each non-fixed cell, the sample applies a five-point stencil with `alpha = 0.2`:

```text
next[y,x] = current[y,x]
             + alpha * (current[y-1,x] + current[y+1,x]
                      + current[y,x-1] + current[y,x+1]
                      - 4 * current[y,x])
```

Fixed cells are copied unchanged on every iteration:

| Region | Temperature | Meaning |
| --- | ---: | --- |
| Outer boundary | 20 | Ambient boundary condition |
| Source 1 | 100 | Hot source |
| Source 2 | 80 | Second hot source |
| Cooling source | 5 | Cold source |
| Other cells | 20 initially | Values that diffuse over time |

The fixed-field marker is internal to `HeatProblem`; it is not part of the public output. Because the outer boundary is fixed, the kernel can read four neighbors for every updated interior cell safely.

## Read the Output

A successful run produces this layout:

```text
<output>/
├── summary.json
└── heatmap.bmp
```

`summary.json` is the machine-readable source of truth. Start with these fields:

| Field | Why it matters |
| --- | --- |
| `profile`, `width`, `height`, `steps` | Exact workload configuration |
| `architecture`, `deviceName`, `executionMode` | Device and graph/direct execution path |
| `cpuMilliseconds` | CPU reference time |
| `gpuKernelMilliseconds` | Kernel-only median measured by HIP events |
| `gpuEndToEndMilliseconds` | Copy + kernel + synchronization median |
| `maximumAbsoluteError`, `rootMeanSquareError` | CPU/GPU numerical agreement |
| `heatmapPath` | Relative path to the generated image |

The speedup shown by the program is `cpuMilliseconds / gpuEndToEndMilliseconds`. HIPRTC compilation is reported separately and is not included in that ratio. Changing the grid, GPU state, driver, or run count changes the measurement.

## Command-Line Reference

| Option | Default | Description |
| --- | --- | --- |
| `--arch gfxNNNN` | Required unless `HIPSHARP_GPU_ARCH` is set | HIPRTC offload target |
| `--profile tiny\|quick\|showcase` | `quick` | 256 x 256 / 50, 1536 x 1536 / 600, or 2048 x 2048 / 1000 |
| `--width N` | Profile value | Grid width, 64-4096 |
| `--height N` | Profile value | Grid height, 64-4096 |
| `--steps N` | Profile value | Iterations, 1-10000 |
| `--gpu-runs N` | `3` | Measured GPU runs, 1-9; median is reported |
| `--output PATH` | Timestamped artifact directory | Output location |
| `--no-image` | Image enabled | Skip `heatmap.bmp` |
| `--help` | Off | Print usage without requiring a GPU |

For example, keep the profile dimensions but increase only the iteration count:

```bash
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  -c Release --no-build --no-restore -- \
  --arch gfx1100 --profile quick --steps 1200 --gpu-runs 5
```

## Source Walkthrough

| File | Read it when you want to understand... |
| --- | --- |
| `Program.cs` | Complete orchestration, validation contract, and exit status |
| `Options.cs` | Profiles, ranges, environment overrides, and output defaults |
| `HeatProblem.cs` | Deterministic sources, boundaries, and CPU/GPU input agreement |
| `CpuHeatSolver.cs` | Reference stencil and double buffering |
| `GpuHeatSolver.cs` | HIP runtime, memory, stream, events, graph capture, and replay |
| `Kernels/heat-diffusion.hip` | Device-side `__global__` stencil kernel |
| `HeatmapBmpWriter.cs` | Dependency-free 24-bit BMP output |
| `ResultSummary.cs` | Stable JSON evidence schema |
| `run-heat-diffusion.sh` | Cloud checks, SDK selection, restore/build/run, and artifact paths |

The runner belongs beside the sample because it is part of the reproduction path. The older `tools/radeon/run-heat-diffusion.sh` entry point is retained only as a compatibility wrapper.

## Correctness and Troubleshooting

The process requires all GPU values to be finite, maximum absolute error `<= 0.05`, and RMSE `<= 0.01`.

| Symptom | Action |
| --- | --- |
| `Radeon GPU devices are unavailable` | Use a GPU-enabled instance and expose `/dev/kfd` and `/dev/dri`. |
| Architecture cannot be detected | Run `rocminfo`, then set `HIPSHARP_GPU_ARCH=gfxNNNN`. |
| HIPRTC compilation fails | Check that HIP Runtime and HIPRTC come from the same ROCm installation and that the architecture matches the device. |
| Execution mode is `direct-stream` | Graph capture is unsupported on that runtime; correctness is still valid. |
| Memory pressure or timeout | Start with `--profile tiny`, then reduce width, height, or steps. |
| Restore rejects the repository `global.json` | Use the colocated shell runner; it invokes a compatible SDK from outside the repository root. |

## Reproducible Cloud Record

A Radeon Cloud `gfx1100` run with the `quick` profile is retained under [`Radeon_Cloud/records/20260818-101738-8eea3de-heat-diffusion`](../../../../Radeon_Cloud/records/20260818-101738-8eea3de-heat-diffusion). Its summary, console output, heatmap, and image captures show the expected artifact format. Treat those numbers as an example of one session, not a universal benchmark.

## Continue Learning

After the first successful run, make one change at a time:

1. Run `tiny --no-image` and inspect only `summary.json`.
2. Compare an even and an odd `--steps` value to observe buffer parity.
3. Compare `graph-capture` with `direct-stream` on a runtime where graph capture is unavailable.
4. Add a fixed source in `HeatProblem` and confirm the CPU/GPU error remains within tolerance.
5. Change the heatmap color mapping without touching the numerical solver.

These exercises preserve the CPU implementation as a correctness oracle while introducing one HIP concept at a time.
