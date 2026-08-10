# Platform compatibility

| Platform | M4 build/package | Prior GPU evidence | M4 Runtime/ABI/GPU validation |
| --- | --- | --- | --- |
| Windows x64 | Yes | Prior M1/M2 pending locally | Blocked; no local AMD GPU, ROCm headers, or new cloud authorization |
| Linux x64 | Yes | Radeon Cloud Ubuntu 24.04.4 / ROCm 7.2.1 / HIP 7.2.53211 / gfx1100 (M4 final SHA) | Passed for one Owner-authorized session; not a support claim |

Prior M1/M2 validation passed on an authorized Radeon Cloud Ubuntu 24.04.4, ROCm 7.2.1, HIP 7.2.53211, and `gfx1100` session. M4 adds 31 Runtime exports, 9 HIPRTC exports, official-header ABI schema 2, explicit streams/events, async memory/kernel and lease checks; the final Owner-authorized session passed the full cloud gate on one `gfx1100` environment. Windows HIP SDK 7.2 loader paths are implemented but have not received AMD GPU execution. These are validation results for one environment, not support claims. The core package does not ship the driver or ROCm user-mode libraries.

Runtime packages must not be enabled until the complete native dependency closure, official source and SHA-256, per-component licenses, package size, and a clean consumer/GPU test have evidence.
