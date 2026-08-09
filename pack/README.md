# Runtime package skeletons

These projects intentionally fail during `Pack`. A runtime package can only be enabled after its manifest identifies official sources, SHA-256 hashes, complete native dependencies, component licenses, supported platforms, and successful clean-environment GPU validation.

Native assets will be placed under `runtimes/<rid>/native`. M0 contains and downloads no ROCm binaries.
