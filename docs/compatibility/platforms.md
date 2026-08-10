# Platform compatibility

| Platform | M3 build/package | Prior GPU evidence | M3 stream/event validation |
| --- | --- | --- | --- |
| Windows x64 | Yes | Prior M1/M2 pending locally | Blocked; no local AMD GPU or new cloud authorization |
| Linux x64 | Yes | Prior Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 | Blocked pending a new Owner-authorized session |

Prior M1/M2 validation passed on an authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` session. M3 adds 31 Runtime exports, explicit streams/events, async memory/kernel and lease checks, but has no new GPU result because the required Owner authorization was not provided. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results for one environment, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
