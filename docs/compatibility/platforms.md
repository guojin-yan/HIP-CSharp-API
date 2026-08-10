# Platform compatibility

| Platform | M2 build/package | M1 runtime tested | M2 HIPRTC/VectorAdd validated |
| --- | --- | --- | --- |
| Windows x64 | Yes | Pending | Pending; no local AMD GPU |
| Linux x64 | Yes | Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 | Pending a new authorized session |

The M1 validation baseline passed on Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100`. It covered all 15 managed target frameworks, required Runtime symbol and ABI checks, device enumeration, and an H2D/D2D/D2H memory round trip. M2 adds HIPRTC and Module/Launch but has not reused that earlier session as evidence. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
