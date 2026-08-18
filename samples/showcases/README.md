# Integrated showcases

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

Showcases combine several HIP capability modules into complete workloads. They are application-level
examples rather than steps in the numbered tutorial path or release-validation gates.

| Showcase | Workload | Main capabilities |
| --- | --- | --- |
| [`HeatDiffusion`](HeatDiffusion) | Two-dimensional heat-equation simulation with a CPU reference | HIPRTC, device memory, streams, events, graph replay, result validation, and a BMP heatmap |
| [`VisualInspection`](VisualInspection) | OpenCV CPU reference plus an AMD GPU defect-mask pipeline | OpenCV image I/O and thresholding, HIPRTC, pinned/device memory, streams, events, graph replay, PNG masks, and CSV/JSON evidence |

`HeatDiffusion` is the P0 thermal flagship and is validated independently of OpenCV. `VisualInspection`
is the P1 vision case from `CasePlan`; it uses the published
[`OpenCV-CSharp-API`](https://github.com/guojin-yan/OpenCV-CSharp-API) packages and includes a
Radeon Cloud runner beside the code.

| CasePlan priority | Case | Showcase status |
| --- | --- | --- |
| P0 Thermal | Heat diffusion / thermal monitoring | Implemented and cloud-validated |
| P1 Vision | Industrial defect segmentation | Implemented with OpenCV-CSharp-API and cloud-validated |
| P2 Vibration | Time/frequency health analysis | Deferred until a rocFFT binding is available |
| P3 Inference | Model inference pipeline | Deferred until MIGraphX or ONNX Runtime integration |
