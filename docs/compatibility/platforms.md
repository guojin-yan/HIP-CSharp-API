# Platform compatibility

| Platform | M0 build/package | Runtime tested | GPU validated |
| --- | --- | --- | --- |
| Windows x64 | Yes | No HIP runtime loaded | No local AMD GPU |
| Linux x64 | Yes | No HIP runtime loaded | No |

The first planned validation baseline is Radeon Cloud Ubuntu 24.04, ROCm 7.2.1, HIP 7.2, and `gfx1100`. Windows HIP SDK 7.2 paths are retained for future loader and package work but are not a support claim. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
