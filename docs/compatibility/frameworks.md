# Framework compatibility

The single core project directly targets the following TFMs:

`net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0`.

This matrix is a build and package target, not a runtime or GPU support matrix. M0 checks all assets in a local package and compiles clean consumers for `net46`, `netcoreapp3.1`, `net7.0`, and `net10.0`; it does not run those consumers.

`net46`, `net461`, `netcoreapp3.1`, `net5.0`, `net6.0`, and `net7.0` are end-of-support upstream. .NET Framework support also depends on the Windows lifecycle. Keep these targets only where compatibility with an existing application requires them, and apply the relevant security updates independently.

`net7.0` and later reserve source-generated `LibraryImport`; older targets reserve `DllImport`. Both declarations are generated from one manifest. M0's compile probe is intentionally not a HIP entry point.
