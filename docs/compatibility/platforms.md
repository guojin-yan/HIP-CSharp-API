# Platform compatibility

| Platform | M1 build/package | Runtime tested | GPU validated |
| --- | --- | --- | --- |
| Windows x64 | Yes | Pending | No local AMD GPU |
| Linux x64 | Yes | Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 | Passed on one `gfx1100` instance |

The first validation baseline passed on Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100`. It covered all 15 managed target frameworks, required symbol and ABI checks, device enumeration, and an H2D/D2D/D2H memory round trip. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
