# VisualInspection

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

`VisualInspection` is a reproducible GPU vision pipeline. It reads four grayscale part images, creates an OpenCV CPU reference mask, runs the equivalent HIPRTC kernel on an AMD GPU, and requires the GPU mask to equal both the OpenCV result and the checked-in expected mask. Successful runs retain JSON, CSV, and PNG evidence instead of only printing a performance number.

The threshold rule is deliberately simple. That makes the full path easy to inspect: image I/O and classical operations belong to OpenCV-CSharp-API, while HIP-CSharp-API owns device memory, execution, timing, and GPU validation.

All commands below assume the repository root, the directory containing `HipSharp.sln`:

```bash
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
```

<p align="center"><img src="assets/visual-fixtures-contact-sheet.png" alt="VisualInspection deterministic fixtures" width="720" /></p>

## What You Will Reproduce

Following this tutorial produces a complete small inspection workload:

1. Load four deterministic PGM fixtures through OpenCV-CSharp-API `5.0.0`.
2. Segment pixels outside an accepted grayscale range with OpenCV on the CPU.
3. Compile `Kernels/visual-inspection.hip` for the target `gfxNNNN` architecture with HIPRTC.
4. Move the image through pinned host memory, device memory, a non-blocking stream, events, and a captured graph when supported.
5. Compare CPU, GPU, and expected masks byte-for-byte, then write a summary, a CSV table, and one PNG mask for each fixture.

This is an integration tutorial, not a production inspection model. The images are synthetic so every expected decision is known and every output can be inspected quickly.

## Before You Start

### Runtime Requirements

The runnable workload is designed for an Ubuntu 24.04 ROCm host with an AMD GPU, HIP Runtime, and HIPRTC. It targets both `net8.0` and `net10.0`; the cloud runner chooses an installed compatible runtime automatically.

The project references these packages through the repository's central package configuration:

| Package | Version | Purpose |
| --- | --- | --- |
| `JYPPX.OpenCV.CSharp.API` | `5.0.0` | Managed OpenCV APIs |
| `JYPPX.OpenCV.runtime.ubuntu.24.04-x64.mini` | `5.0.0` | Ubuntu 24.04 native OpenCV runtime |

The managed namespace is `JYPPX.OpenCvSharp.*`. The project intentionally uses the checked-in Ubuntu-specific RID graph, so it does not require a system-wide OpenCV installation.

Direct `dotnet` commands from the repository root use the SDK selected by `global.json` (currently `10.0.300`). The colocated runner invokes a compatible SDK from the repository parent directory, so it also works on a cloud image whose installed SDK is on a different feature band.

| Environment | Restore and build | Run the full workload | Notes |
| --- | --- | --- | --- |
| Radeon Cloud Ubuntu/ROCm | Yes | Yes | Recommended first experience |
| Local Ubuntu 24.04/ROCm | Yes | Yes | Use the direct commands below |
| Windows | Yes | No with the checked-in Ubuntu runtime | Build and read the source locally; run the GPU workload on Linux |

Before a first Linux run, verify the host:

```bash
rocminfo | grep -Eo 'gfx[0-9]+' | head -n 1
ls -l /dev/kfd /dev/dri
dotnet --list-runtimes
```

Use the architecture reported by `rocminfo`; `gfx1100` in this document is only an example.

<a id="run-on-radeon-cloud"></a>

## Five-Minute Run on Radeon Cloud

From the repository root on an authorized Ubuntu ROCm instance, run:

```bash
bash ./samples/showcases/VisualInspection/run-visual-inspection.sh
```

The runner performs all setup that is specific to Radeon Cloud or an equivalent Ubuntu ROCm host:

1. Checks `/dev/kfd`, `/dev/dri`, and `rocminfo`.
2. Reuses an installed .NET 8+ SDK, bootstrapping only when there is no compatible SDK.
3. Detects the first `gfxNNNN` architecture unless `HIPSHARP_GPU_ARCH` is supplied.
4. Restores in locked mode with `runtime-distro-rid-graph.json`.
5. Builds Release, runs the selected `net10.0` or `net8.0` target, and writes a timestamped artifact directory below `artifacts/visual-inspection/`.

For an explicit architecture, persistent output path, or more timing samples:

```bash
HIPSHARP_GPU_ARCH=gfx1100 HIPSHARP_VISUAL_OUTPUT=/persistent/projects/hip-csharp-api/results/visual-inspection bash ./samples/showcases/VisualInspection/run-visual-inspection.sh --gpu-runs 5
```

Set `HIPSHARP_VISUAL_TFM=net8.0` or `HIPSHARP_VISUAL_TFM=net10.0` only when you need to choose the runtime explicitly. The default selection is based on installed runtimes.

## Run Directly on a Local Linux ROCm Host

Run these commands from the repository root after replacing `gfx1100` with the local architecture:

```bash
dotnet restore ./samples/showcases/VisualInspection/VisualInspection.csproj --locked-mode
dotnet build ./samples/showcases/VisualInspection/VisualInspection.csproj --configuration Release --no-restore
dotnet run --project ./samples/showcases/VisualInspection/VisualInspection.csproj --framework net8.0 --configuration Release --no-build --no-restore -- --arch gfx1100
```

Use `--framework net10.0` instead when that is the installed runtime. Because the sample targets an Ubuntu-native OpenCV runtime, a Windows build is useful for source inspection but cannot load `JYPPX.OpenCV.Native` to execute the workload.

## Understand the Inspection Rule

Each fixture is a 128 x 96 8-bit grayscale PGM image. A pixel is classified as a defect when its intensity is below `100` or above `190`. The GPU writes `255` for a defect and `0` otherwise:

```text
defect = pixel < 100 || pixel > 190
mask   = defect ? 255 : 0
```

The CPU reference creates the same mask with two OpenCV `Threshold` calls followed by `BitwiseOr`. The fixtures, expected masks, and recipe are stored in `assets/`:

| Fixture | Condition | Expected decision | Expected defect pixels |
| --- | --- | --- | ---: |
| `part_000_ok` | No defect | PASS | 0 |
| `part_001_scratch` | Dark scratch | FAIL | 261 |
| `part_002_hole` | Dark hole | FAIL | 529 |
| `part_003_contamination` | Bright contamination | FAIL | 421 |

The input contract is explicit. A custom `--input` directory must contain `visual-fixture-recipe.json` and all image and expected-mask paths named by that recipe. The program rejects dimension mismatches before starting GPU work.

## What Happens During a Run

1. `Options` resolves the architecture, input directory, output directory, and number of measured GPU runs.
2. `VisualRecipe` loads the fixture list. `OpenCvImageTools` reads each image into an OpenCV `Mat`, creates the CPU mask, and converts the pixels for the HIP boundary.
3. HIPRTC compiles `visual-inspection.hip` using `--offload-arch=<gfxNNNN> -O3`.
4. The GPU path allocates input/output device buffers and pinned input/output host buffers once.
5. The kernel launch is captured into a graph when supported; otherwise it uses the non-blocking stream.
6. For every fixture, the program copies pixels to the device, measures the kernel with HIP events, copies the mask back, then compares GPU, OpenCV CPU, and expected masks.
7. OpenCV writes a PNG GPU mask per fixture. The program writes JSON and CSV evidence and returns `0` only when every fixture passes.

The graph contains only the reusable kernel launch. Input and output copies remain per-fixture, so the same captured graph can process every image safely.

## Inspect the Artifacts

Every successful run creates:

```text
<output>/
├── inspection-summary.json
├── inspection-results.csv
└── masks/
    ├── part_000_ok_gpu.png
    ├── part_001_scratch_gpu.png
    ├── part_002_hole_gpu.png
    └── part_003_contamination_gpu.png
```

Read the files in this order:

| File | What to check |
| --- | --- |
| `inspection-summary.json` | `status`, device, architecture, HIPRTC hash, timing, execution mode, and per-fixture results |
| `inspection-results.csv` | One line per fixture; convenient for spreadsheet comparison |
| `masks/*.png` | The exact GPU-produced masks that were validated |

For every fixture, `passed` is true only when the GPU mask equals both the OpenCV reference mask and the expected mask. `intersectionOverUnion` should be `1.0` and `maximumByteDifference` should be `0`.

CPU time includes OpenCV decoding and reference segmentation. GPU kernel time is measured by HIP events. GPU end-to-end time also includes pinned H2D/D2H transfers and synchronization. The images are small by design, so treat the timing as pipeline evidence rather than an industrial throughput claim.

## Command-Line Reference

| Option | Default | Description |
| --- | --- | --- |
| `--arch gfxNNNN` | Required unless `HIPSHARP_GPU_ARCH` is set | HIPRTC offload target |
| `--input PATH` | Bundled `assets/` | Directory containing the recipe, images, and expected masks |
| `--output PATH` | Timestamped artifact directory | JSON, CSV, and PNG destination |
| `--gpu-runs N` | `3` | Measurements per fixture, 1-9; median is reported |
| `--help` | Off | Print usage without starting HIP or OpenCV |

To retain a named local result directory:

```bash
dotnet run --project ./samples/showcases/VisualInspection/VisualInspection.csproj --framework net8.0 -c Release --no-build --no-restore -- --arch gfx1100 --gpu-runs 5 --output ./artifacts/visual-inspection/tutorial-run
```

## Source Walkthrough

| File | Read it when you want to understand... |
| --- | --- |
| `Program.cs` | Fixture orchestration, CPU/GPU/expected comparison, JSON/CSV writing, and exit status |
| `Options.cs` | Runtime arguments and output defaults |
| `VisualRecipe.cs` | Recipe schema and fixture loading |
| `OpenCvImageTools.cs` | `JYPPX.OpenCvSharp.*` image loading, CPU segmentation, and PNG writing |
| `GpuInspectionSolver.cs` | Pinned memory, device memory, stream/event timing, graph capture, and fallback |
| `Kernels/visual-inspection.hip` | One-dimensional HIP defect-mask kernel |
| `PgmImage.cs` | Small pixel container at the OpenCV/HIP boundary |
| `runtime-distro-rid-graph.json` | Ubuntu-specific RID compatibility graph |
| `run-visual-inspection.sh` | ROCm checks, .NET selection, locked restore, build, run, and artifact path |

## Troubleshooting

| Symptom | Action |
| --- | --- |
| `Radeon GPU devices are unavailable` | Use a GPU-enabled Linux instance and expose `/dev/kfd` and `/dev/dri`. |
| `Specify --arch gfxNNNN` | Read the target from `rocminfo`, then pass `--arch` or set `HIPSHARP_GPU_ARCH`. |
| `JYPPX.OpenCV.Native` cannot be loaded on Windows | Expected: this project is pinned to the Ubuntu 24.04 native OpenCV package. Run it on Linux. |
| NuGet reports a package hash mismatch | Remove only the affected OpenCV package version from the local NuGet cache, then restore in locked mode. |
| HIPRTC compilation fails | Confirm that HIP Runtime and HIPRTC are from the same ROCm installation and target the reported architecture. |
| `executionMode=direct-stream` | Graph capture is unavailable; the direct stream path remains correct and fully validated. |
| Restore rejects `global.json` | Use `run-visual-inspection.sh`; it invokes a compatible SDK outside the repository root. |

## Verified Radeon Cloud Run

The stable `5.0.0` package run on Radeon Cloud used an Ubuntu 24.04 OpenCV mini runtime, a `gfx1100` device, and .NET 10. It passed every fixture with `graph-capture`:

| Measurement | Value |
| --- | ---: |
| HIPRTC compile | 57.28 ms |
| OpenCV CPU reference | 32.03 ms |
| GPU kernel median | 0.02 ms |
| GPU end-to-end median | 0.06 ms |
| Fixtures passed | 4/4 |

The retained JSON, CSV, and PNG masks are in [`Radeon_Cloud/records/20260818-145855-visual-inspection-5.0.0`](../../../../Radeon_Cloud/records/20260818-145855-visual-inspection-5.0.0). The values document this fixture set and session; they are not a cross-device performance promise.

## Continue Learning

After reproducing the bundled run, keep the correctness contract and change one thing at a time:

1. Copy `assets/` to a new directory, then run with `--input` to prove the external-fixture workflow.
2. Add a fixture and expected mask to the recipe, then confirm JSON, CSV, and PNG output grow together.
3. Change the accepted grayscale band in the CPU and HIP implementations together, then regenerate the expected masks.
4. Replace the PGM fixtures with a camera-frame export while keeping the OpenCV-to-HIP byte boundary.
5. Increase fixture count or image size only after preserving expected masks and CPU/GPU validation.

That progression mirrors real inspection work: establish a known-good data contract first, then expand the GPU path without losing the ability to prove the result.
