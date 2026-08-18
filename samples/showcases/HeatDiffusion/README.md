# HeatDiffusion

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

`HeatDiffusion` is a complete C# to AMD GPU workload. It solves a two-dimensional heat-diffusion
problem twice: first with a C# `Parallel.For` reference implementation, then with a HIP C++ stencil
kernel compiled at runtime by HIPRTC. The sample compares both final fields, measures the GPU path,
and writes a heatmap that makes the result easy to inspect.

This is an application-level showcase rather than a single-API test. It deliberately brings together
the capabilities learned in the numbered samples: device initialization, HIPRTC, module and kernel
launch, device memory, asynchronous copies, streams, events, graph capture, resource ownership, and
CPU/GPU result validation.

<p align="center"><img src="assets/heat-diffusion-result.png" alt="HeatDiffusion GPU heatmap" width="520" /></p>

## What You Learn

After completing this sample, you should be able to:

1. Start with a deterministic CPU implementation that serves as a correctness oracle.
2. Represent a two-dimensional field as a flat row-major `float[]` buffer.
3. Compile a HIP kernel for the actual AMD architecture reported by the machine.
4. Move immutable and changing fields to device memory and alternate two output buffers.
5. Use a non-blocking HIP stream to order copies, kernel launches, and synchronization.
6. Use HIP events for kernel-only timing and a stopwatch for end-to-end timing.
7. Capture two alternating stencil steps into a graph and replay that graph for long workloads.
8. Fall back to direct stream launches when graph capture is not supported.
9. Compare numerical results before presenting any timing number.
10. Keep cloud-specific environment setup outside the C# workload.

## Workload Model

The field contains one temperature value per grid cell. At every step, a non-fixed cell uses the
five-point stencil below, where `alpha = 0.2`:

```text
next[y,x] = current[y,x]
             + alpha * (current[y-1,x] + current[y+1,x]
                      + current[y,x-1] + current[y,x+1]
                      - 4 * current[y,x])
```

The boundary and source cells are fixed in `fixedField` and are copied unchanged at every step:

| Region | Temperature | Purpose |
| --- | ---: | --- |
| Outer boundary | 20 | Ambient boundary condition |
| Source 1 | 100 | Hot source |
| Source 2 | 80 | Second hot source |
| Cooling source | 5 | Cold source |
| Other cells | Initial 20 | Values that diffuse over time |

`HeatProblem` generates the same deterministic fields for both implementations. The fixed field uses
`-1` as the internal marker for a cell that should be updated. Because the outer boundary is fixed, the
kernel can safely read the four neighbors for every non-fixed interior cell.

## Execution Pipeline

The complete run follows this order:

1. `Program.Main` parses the profile and creates the output directory.
2. `HeatProblem` builds `fixedField` and the ambient-temperature `initialField`.
3. `CpuHeatSolver` runs the reference implementation with `Parallel.For`.
4. `GpuHeatSolver` initializes `HipRuntime`, enumerates a device, and makes it current.
5. HIPRTC compiles `Kernels/heat-diffusion.hip` with `--offload-arch=<gfxNNNN>` and `-O3`.
6. The code object is loaded as a `HipModule`, and `HeatStep` is looked up as a `HipKernel`.
7. Three device allocations are created: current values, next values, and the immutable fixed field.
8. Initial data is copied to the device, followed by a warm-up launch.
9. When at least two steps are requested, two opposite-direction launches are captured into a graph:
   `current -> next` and `next -> current`.
10. Each measured run copies the initial field to the device, records an event, queues step pairs by
    graph replay or direct launches, copies the final buffer back, and synchronizes once.
11. The median of the requested GPU runs is reported. The CPU and GPU fields are then compared and the
    JSON summary and optional BMP heatmap are written.

The graph contains two steps because the kernel arguments alternate between the two device buffers.
An odd final step is launched directly after the graph pairs. This keeps the buffer parity explicit and
avoids rebuilding a separate graph for every possible iteration count.

## Source Walkthrough

| File | Responsibility | HIP or .NET concept |
| --- | --- | --- |
| `Program.cs` | Orchestrates one complete run and exit status | Explicit `Program.Main`, validation, JSON output |
| `Options.cs` | Parses profiles and overrides | `--arch`, dimensions, steps, output, image switch |
| `HeatProblem.cs` | Creates deterministic sources and compares fields | CPU/GPU correctness contract |
| `CpuHeatSolver.cs` | Reference stencil | `Parallel.For`, double buffering |
| `GpuHeatSolver.cs` | HIP execution path | Runtime, HIPRTC, memory, stream, event, graph, module, kernel |
| `Kernels/heat-diffusion.hip` | Device stencil | HIP C++ `__global__` kernel |
| `HeatmapBmpWriter.cs` | Writes a dependency-free image | 24-bit BMP, no image package required |
| `ResultSummary.cs` | Serializes run evidence | Stable camel-case `summary.json` schema |
| `run-heat-diffusion.sh` | Adapts an authorized ROCm cloud environment | `rocminfo`, SDK selection, restore/build/run |

The shell runner lives beside this project because it is part of this showcase's learning path. The
generic `tools/radeon` directory still contains the shared validation helpers. Its old
`run-heat-diffusion.sh` path is now only a compatibility wrapper that delegates here.

## Requirements

- A .NET 10 or later SDK. The project currently targets `net10.0` to match the repository's supported
  development baseline. The cloud runner reuses an installed compatible SDK and only bootstraps when
  the machine has none.
- An AMD GPU supported by the installed HIP/ROCm runtime.
- HIP Runtime and HIPRTC native libraries visible to the process.
- The actual GPU target, such as `gfx1100`, passed with `--arch` or `HIPSHARP_GPU_ARCH`.

The C# workload has no Radeon Cloud reference and can be moved to another Linux ROCm environment. Only
the shell runner assumes `/dev/kfd`, `/dev/dri`, `rocminfo`, and the repository's cloud bootstrap helper.

## Run Locally on Linux ROCm

From the repository root:

```bash
dotnet restore ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj --locked-mode
dotnet build ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj -c Release --no-restore
dotnet run --project ./samples/showcases/HeatDiffusion/HeatDiffusion.csproj \
  -c Release --no-build --no-restore -- \
  --arch gfx1100 \
  --profile quick
```

Replace `gfx1100` with the architecture reported by the target machine. Run the following before the
sample when diagnosing a new ROCm installation:

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --info
```

The C# command deliberately does not guess the GPU architecture. A wrong `--arch` can produce a valid
code object for the wrong device, so the target is an explicit input.

## Run on Radeon Cloud

The sample itself is cloud-independent. On an authorized Ubuntu ROCm instance, from the repository
root run the script next to the project:

```bash
bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

The runner performs these environment steps before it invokes the C# program:

1. Verifies `/dev/kfd`, `/dev/dri`, and `rocminfo`.
2. Reuses an installed .NET 10 or later SDK. It runs `tools/radeon/bootstrap.sh` only when no compatible
   SDK is available.
3. Detects the first `gfxNNNN` architecture from `rocminfo`, unless `HIPSHARP_GPU_ARCH` is set.
4. Uses a persistent NuGet cache when `/persistent` is available.
5. Restores with the lock file, builds Release, and runs the sample.
6. Writes all output below one timestamped artifact directory.

The runner invokes `dotnet` from the repository parent directory and passes the project by absolute path.
This allows a cloud machine with a compatible SDK feature band to use the `net10.0` sample without
being rejected by the repository's development-only `global.json` pin.

Use a larger workload or explicit overrides:

```bash
HIPSHARP_HEAT_PROFILE=showcase \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh

bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh \
  --width 2048 \
  --height 2048 \
  --steps 800 \
  --gpu-runs 5
```

If a previous document uses `bash ./tools/radeon/run-heat-diffusion.sh`, that compatibility command
still works. New tutorials should use the colocated path above.

When the template does not contain the source, use the configured repository checkout mechanism. If a
GitHub proxy certificate is not trusted, do not disable TLS verification. Transfer a Git bundle from a
trusted machine, verify its SHA-256, and check out the intended commit as described in
[`tools/radeon/README.md`](../../../tools/radeon/README.md).

## Profiles and Command-Line Options

| Option | Default | Meaning |
| --- | --- | --- |
| `--arch gfxNNNN` | Required unless `HIPSHARP_GPU_ARCH` is set | HIPRTC offload target |
| `--profile tiny` | `quick` | 256 x 256, 50 steps, smoke test |
| `--profile quick` | `quick` | 1536 x 1536, 600 steps, default experience |
| `--profile showcase` | `quick` | 2048 x 2048, 1000 steps, longer demonstration |
| `--width N` | Profile value | Grid width, 64 to 4096 |
| `--height N` | Profile value | Grid height, 64 to 4096 |
| `--steps N` | Profile value | Iteration count, 1 to 10000 |
| `--gpu-runs N` | `3` | Measured runs, 1 to 9; median is reported |
| `--output PATH` | Timestamped artifact directory | Where JSON and image files are written |
| `--no-image` | Image enabled | Skip `heatmap.bmp` when only numeric output is needed |
| `--help` | Off | Print usage without requiring a GPU |

The shell runner accepts the same arguments after its own environment setup. `HIPSHARP_HEAT_PROFILE`
controls the default profile used by the runner, `HIPSHARP_GPU_ARCH` overrides architecture detection,
and `HIPSHARP_HEAT_OUTPUT` selects the artifact directory.

## Artifacts

Every successful run writes:

```text
<output>/
├── summary.json
└── heatmap.bmp                 # omitted with --no-image
```

`summary.json` is intended for scripts and tutorials. Important field groups are:

| Group | Fields | Interpretation |
| --- | --- | --- |
| Workload | `profile`, `width`, `height`, `steps`, `cellUpdates` | Exact problem size |
| CPU baseline | `cpuWorkers`, `cpuMilliseconds` | C# reference cost |
| GPU timing | `gpuCompileMilliseconds`, `gpuKernelMilliseconds`, `gpuEndToEndMilliseconds`, `gpuRuns` | Compile, kernel-only, and copy-inclusive measurements |
| Execution | `executionMode`, `architecture`, `deviceName` | Graph/direct path and target device |
| Runtime | HIP and HIPRTC version fields, code object size and SHA-256 | Native execution evidence |
| Correctness | `maximumAbsoluteError`, `rootMeanSquareError`, `nonFiniteValues`, tolerances | CPU/GPU comparison |
| Image | `heatmapPath` | Relative image name when enabled |

For cloud tutorials, retrieve the whole timestamped directory before destroying the instance. Do not
commit endpoint, port, private key, token, hostname, GPU UUID, or other instance-unique identifiers.

## Verified Cloud Run

The retained Radeon Cloud run used ROCm 7.2.1, a `gfx1100` device, the `quick` profile, and the
installed .NET SDK `10.0.110`. It completed 1,415,577,600 cell updates on a 1536 x 1536 grid over
600 steps with `executionMode=graph-capture`:

| Measurement | Value |
| --- | ---: |
| CPU reference, 16 workers | 2064.9364 ms |
| GPU kernel median | 17.4798 ms |
| GPU end-to-end median | 20.5979 ms |
| Observed current-session ratio | 100.2499x |
| Maximum absolute error | 1.1444e-05 |
| RMSE | 9.1031e-08 |

The run passed with zero non-finite values and zero build warnings or errors. These numbers are kept
as a reproducible example of the output format, not as a promise for every AMD GPU. The original
`summary.json`, console log, BMP, and PNG are retained in the external `Radeon_Cloud/records/` record.

## Correctness and Performance

The process exits with code `0` only when all GPU values are finite, maximum absolute error is at most
`0.05`, and RMSE is at most `0.01`. A nonzero exit code means the run is not a successful showcase,
even if the kernel produced an image.

The reported acceleration is intentionally split into three numbers:

- HIPRTC compilation: startup cost, reported separately and excluded from speedup.
- GPU kernel median: device execution measured by HIP events.
- GPU end-to-end median: per-run H2D copy, all steps, D2H copy, and synchronization.

The displayed speedup is `cpuMilliseconds / gpuEndToEndMilliseconds` for this process only. It is not a
cross-device benchmark, does not include power measurement, and should not be presented as an energy
efficiency result. Change the grid, step count, GPU state, driver, SDK, or number of runs and the number
can change substantially.

## Troubleshooting

### `Radeon GPU devices are unavailable`

The runner expects `/dev/kfd` and `/dev/dri`. Start a GPU-enabled instance and expose both device
nodes. For a local run, invoke the C# project directly after confirming the HIP loader can see the GPU.

### `Unable to determine a gfxNNNN target`

Run `rocminfo` and set the architecture explicitly:

```bash
HIPSHARP_GPU_ARCH=gfx1100 \
  bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
```

### HIPRTC compilation fails

Confirm that `libhiprtc` and the HIP Runtime come from the same ROCm installation, and that the selected
architecture is supported by the installed device. Keep the compiler log from the exception output with
the artifact directory when reporting the problem.

### The run reports `direct-stream`

This is a supported fallback. The runtime returned `HipError.NotSupported` while capturing the graph, so
the sample launched the same two-step pairs directly on the stream. Correctness and timing still run;
only graph replay is unavailable.

### Restore fails with a `global.json` feature-band error

Use the colocated cloud runner. It resolves and invokes the SDK from the repository parent directory,
then passes the project path explicitly. Do not weaken the repository-wide `global.json` just to run this
showcase.

### Memory or runtime timeouts

Start with `--profile tiny`, then `quick`. Reduce `--width`, `--height`, or `--steps` and keep the output
directory so the failed console log and partial artifacts can be inspected.

## Suggested Learning Exercises

1. Run `tiny` with `--no-image` and inspect only `summary.json`.
2. Change `--steps` between even and odd values and observe the final-buffer parity in the GPU solver.
3. Temporarily disable graph capture and compare `direct-stream` with `graph-capture` execution.
4. Add a third fixed source in `HeatProblem` and verify that the CPU/GPU error remains within tolerance.
5. Replace the thermal color map with a domain-specific visualization while keeping the numerical solver
   unchanged.
6. Add a second GPU device selection step after the device-enumeration tutorial.

These exercises keep the sample useful after the first cloud run: each one changes one concept while
leaving the CPU reference as a stable correctness check.
