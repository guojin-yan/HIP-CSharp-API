# Integrated Showcases

<p align="center">
  <strong>English</strong> | <a href="README.zh-CN.md">简体中文</a>
</p>

<p align="center">
  <img src="HeatDiffusion/assets/heat-diffusion-result.png" alt="HeatDiffusion result heatmap" width="46%">
  <img src="VisualInspection/assets/visual-fixtures-contact-sheet.png" alt="VisualInspection fixture contact sheet" width="46%">
</p>

These showcases are the fastest way to see HIP CSharp API doing useful application work. Each one
combines HIPRTC, managed resource ownership, GPU execution, CPU/GPU correctness checks, and
machine-readable output.

> Want to run the demos instead of only reading the source?
> Open [AMD Radeon Cloud](https://developer.amd.com.cn/login?source=J2RtJQRgM), start an Ubuntu/ROCm
> GPU instance, clone this repository, and follow the commands below. Both showcases include a runner
> designed for that environment.

## Choose a showcase

<table>
<thead>
<tr>
<th>Showcase</th>
<th>What you will see</th>
<th>Best starting point</th>
</tr>
</thead>
<tbody>
<tr>
<td><a href="HeatDiffusion/README.md"><strong>HeatDiffusion</strong></a></td>
<td>A CPU reference solver and a HIPRTC stencil solver produce the same two-dimensional heat field.
The run also demonstrates streams, events, graph replay, and a generated heatmap.</td>
<td>First GPU demo and HIP fundamentals</td>
</tr>
<tr>
<td><a href="VisualInspection/README.md"><strong>VisualInspection</strong></a></td>
<td>OpenCV builds a CPU defect mask while a HIPRTC kernel builds the GPU mask. Four fixtures are
checked byte-for-byte, then JSON, CSV, and PNG evidence is written.</td>
<td>Application pipeline and computer vision</td>
</tr>
</tbody>
</table>

## Radeon Cloud: five-minute path

Radeon Cloud is the recommended first-run environment because it provides the AMD GPU, HIP Runtime,
and ROCm user-space libraries needed by these demos.

1. Sign in at [developer.amd.com.cn](https://developer.amd.com.cn/login?source=J2RtJQRgM).
2. Start an Ubuntu/ROCm GPU instance and open a terminal in the repository workspace.
3. Clone the repository and enter its root:

   ```bash
   git clone https://github.com/guojin-yan/HIP-CSharp-API.git
   cd HIP-CSharp-API
   ```

4. Run either showcase. The colocated script checks the GPU, selects a compatible .NET SDK, restores
   in locked mode, builds Release, detects the gfxNNNN architecture, and writes artifacts under
   artifacts/.

   ```bash
   bash ./samples/showcases/HeatDiffusion/run-heat-diffusion.sh
   bash ./samples/showcases/VisualInspection/run-visual-inspection.sh
   ```

For a first smoke test, start with HeatDiffusion's tiny profile or VisualInspection's default
fixture set. Read the detailed walkthrough after the first successful run:

- [HeatDiffusion: run on Radeon Cloud](HeatDiffusion/README.md#run-on-radeon-cloud)
- [VisualInspection: run on Radeon Cloud](VisualInspection/README.md#run-on-radeon-cloud)

The scripts are reproducibility helpers, not a substitute for an authorized project validation
session. A personal Cloud run is useful for learning and sharing a result; it should not be described
as a project release gate or a cross-device benchmark.

## What each run produces

| Showcase | Correctness contract | Output |
| --- | --- | --- |
| HeatDiffusion | Every GPU cell stays within the documented tolerance of the CPU reference. | summary.json, optional heatmap.bmp |
| VisualInspection | Every GPU mask matches both the OpenCV reference and the expected fixture mask. | inspection-summary.json, inspection-results.csv, masks/*.png |

The reported timings describe the current process, device, driver, workload, and run count. Treat them
as demonstration data, not as a universal performance claim.

## How the showcases fit the project

Showcases are application-level examples. They are separate from the numbered tutorials and from
release-validation gates, but they exercise the same public managed API used by real consumers.

| CasePlan priority | Case | Status |
| --- | --- | --- |
| P0 Thermal | Heat diffusion / thermal monitoring | Implemented and Radeon Cloud validated |
| P1 Vision | Industrial defect segmentation | Implemented with OpenCV-CSharp-API and Radeon Cloud validated |
| P2 Vibration | Time/frequency health analysis | Deferred until a rocFFT binding is available |
| P3 Inference | Model inference pipeline | Deferred until MIGraphX or ONNX Runtime integration |

## Continue

- Start with [HeatDiffusion](HeatDiffusion/README.md) if you want the shortest path from C# to a
  working HIPRTC kernel.
- Continue with [VisualInspection](VisualInspection/README.md) if you want to see a GPU kernel
  embedded in a CPU/OpenCV application pipeline.
- Return to the [samples index](../README.md) for the numbered tutorial path.
