# Framework compatibility

The single core project directly targets the following TFMs:

`net46`, `net461`, `net462`, `net47`, `net471`, `net472`, `net48`, `net481`, `netcoreapp3.1`, `net5.0`, `net6.0`, `net7.0`, `net8.0`, `net9.0`, `net10.0`.

This matrix is a build and package target, not a runtime or GPU support matrix. The package gate checks all assets in a local package and compiles clean consumers for `net46`, `netcoreapp3.1`, `net7.0`, and `net10.0`; it does not run those consumers.

For the `1.0.0` candidate, `eng/verify-public-api.ps1` compares the committed versioned public API snapshot with every target assembly. All 15 TFMs must remain surface-identical, and the `1.0.0` snapshot must be semantically identical to the published `0.9.1` snapshot. The pack step also runs NuGet package validation.

`net46`, `net461`, `netcoreapp3.1`, `net5.0`, `net6.0`, and `net7.0` are end-of-support upstream. .NET Framework support also depends on the Windows lifecycle. Keep these targets only where compatibility with an existing application requires them, and apply the relevant security updates independently.

`net7.0` and later use source-generated `LibraryImport`; older targets use `DllImport`. Runtime, stream-ordered and pool-backed allocation, managed memory, peer, graph, Module/Launch, and HIPRTC declarations are generated from one manifest and compile through both branches.
