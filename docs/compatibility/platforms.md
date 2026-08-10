# Platform compatibility

| Platform | M2 build/package | M1 runtime tested | M2 HIPRTC/VectorAdd validated |
| --- | --- | --- | --- |
| Windows x64 | Yes | Pending | Pending; no local AMD GPU |
| Linux x64 | Yes | Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 | Passed on one `gfx1100` instance |

M1 and M2 validation passed on authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` sessions. M2 independently covered all 15 managed target frameworks, 17 Runtime/Module exports, 9 HIPRTC exports, official-header ABI assertions, HIPRTC compile/log/code, module/function lookup, five VectorAdd lengths for 20 repetitions each, D2H CPU comparison, and an expected compiler failure with a diagnostic log. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results for one environment, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
