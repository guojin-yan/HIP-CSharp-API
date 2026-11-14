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

Remove `-CompilerOnly` to attempt the existing 18 GPU workloads, publish and negative checks using the exact public Runtime. The scope is `published-runtime-regression`, distinct from local 10.1.0 validation. Failure stays failure, and `completed=false` indicates the full matrix was not completed. On the present W7900 WSL2 `/dev/dxg` host, the public 7.2.1 HSA/Runtime closure returns `hipErrorNoDevice (100)` at initialization, despite correctly loading package-local libraries. That result does not establish an application defect or a pass: the historical package targets its reviewed Linux/KFD environment. Current Core plus this Runtime still needs a suitable GPU host for its exact-package gate. The local 10.1.0 WSL closure remains separately validated.
