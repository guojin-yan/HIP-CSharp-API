# Public Runtime regression / 公开 Runtime 回归

`eng/published-runtime-7.2.1.lock.json` pins the NuGet.org repository-signed Ubuntu Runtime, including exact ID/version, 162,907,087-byte size and SHA-256. The unsigned candidate hash is different. Verify the downloaded package with `dotnet nuget verify <package> --all` as well as the locked hash; do not replace the public lock with an arbitrary locally repacked archive.

## Build pairing / 配对构建

Set `HIPSHARP_OUTPUT_ROOT` to an external build directory, then run on Windows or Linux:

```powershell
./eng/test-core-runtime-pairing.ps1 -CorePackagePath <current-core.nupkg> `
  -RuntimePackagePath <public-ubuntu-runtime-7.2.1.nupkg>
```

The audit uses an independent cache and source mapping: exact HipSharp packages come from the local feed; only Microsoft framework packs may come from NuGet.org. It checks 14 native assets and their hashes in the package consumer, and zero native assets in the Core-only consumer. This is a restore/build check, with `execution=not-run-build-audit-only`; a Windows build of a Linux consumer does not execute the Linux native libraries.

## Compiler and GPU checks / 编译器与 GPU 检查

On Ubuntu 24.04 with PowerShell 7 and .NET 10:

```powershell
./eng/test-local-runtime-package.ps1 -PublishedRuntime -CompilerOnly `
  -RuntimePackage <public-ubuntu-runtime-7.2.1.nupkg> -CorePackage <current-core.nupkg> `
  -OutputRoot <new-external-directory> -Architecture gfx1100
```

This mode compiles a real HIP kernel to an ELF code object and checks expected compile-error diagnostics. It audits actual native module paths and hashes, clears SDK search paths and uses a fresh package cache. It does **not** initialize a GPU or execute the kernel. The summary scope `published-runtime-compiler-only` cannot satisfy the release candidate's GPU receipt requirement.

The current exact public package passed both compiler cases under WSL2; the compile produced a 4,064-byte ELF object. The managed-manifest export check also found all 91 Runtime and 18 HIPRTC exports. These are compiler/symbol results, not proof of native call correctness for every interface.

Remove `-CompilerOnly` to run the 18 GPU workloads, publish and negative checks using the exact public Runtime (22 cases total). The scope is `published-runtime-regression`, distinct from local 10.1.0 validation. Failure stays failure, and `completed=false` indicates the full matrix was not completed. On the W7900 WSL2 `/dev/dxg` host, the public 7.2.1 HSA/Runtime closure returns `hipErrorNoDevice (100)` at initialization, despite correctly loading package-local libraries. That result does not establish an application defect or a pass: the historical package targets its reviewed Linux/KFD environment.

The 1.0.0 Core candidate built at `3d626ddf643e1bfef3a358ea3e26c3e5fedcdf38` passed all 24 enhanced cases with the exact public Runtime on Ubuntu 24.04 / Linux KFD / gfx1100. The same consumer deployments passed in a PRoot Ubuntu guest without exposing the installed HIP SDK; SDK builds ran outside the guest. This evidence applies to Core nupkg SHA-256 `0283698aec534b2d8442f3280468d3f65998ad8dca7938e940f54e82844aaae4`, not later rebuilds. It does not prove all 477 native functions or authorize publication. The local 10.1.0 closure has separate 24-case package and isolation receipts using the same Core bytes. The earlier 54abde4 candidate's 22-case results remain historical.

For additional Linux KFD fixtures, append `-ExtendedLinux` to run 24 cases, including physical-memory POSIX sharing and cross-process memory/event IPC. Preserve that extended summary as supplementary evidence; the release candidate schema continues to require the standard 22-case Linux receipt.
