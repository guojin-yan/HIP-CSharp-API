# Runtime package skeletons

These projects intentionally fail during `Pack`. A runtime package can only be enabled after its manifest identifies official sources, SHA-256 hashes, complete native dependencies, component licenses, supported platforms, and successful clean-environment GPU validation.

Runtime package IDs remain stable (`JYPPX.HipSharp.Runtime.linux-x64` and `JYPPX.HipSharp.Runtime.win-x64`). Their NuGet package versions match the packaged ROCm release, such as `7.2.1` or `7.2.0`.

Native assets will be placed under `runtimes/<rid>/native`. M0 contains and downloads no ROCm binaries.
