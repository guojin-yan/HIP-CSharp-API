# Contributing

Thank you for helping validate and improve HIP CSharp API. This guide is intended for community
developers, especially contributors with a Windows AMD GPU who can provide real Runtime and GPU
evidence that is not currently available in the project's local environment.

[简体中文](CONTRIBUTING.zh-CN.md)

## 1. Project scope and boundaries

- The Git repository root is this directory, `HIP-CSharp-API/`. The sibling `plan/`, `diary/`, and
  `Radeon_Cloud/` directories are project records outside the Git repository and must not be committed.
- The project provides the `JYPPX.ROCm.HIP.CSharp.API` Core package and optional Runtime packages.
  The public namespace is `JYPPX.ROCm.HipSharp`.
- The Core package does not contain AMD drivers, a ROCm installation, or unreviewed native DLL/SO
  files. Do not copy binaries from a local HIP SDK, archive, or cloud host into a pull request.
- The mainline is still in the `0.x` validation phase. Linux/Radeon Cloud results do not replace
  Windows AMD GPU validation. Do not claim `1.0.0` or “Windows supported” until Windows hardware
  validation is complete and the Owner has explicitly authorized that statement.

Use these terms precisely in reports:

| Status | Meaning |
| --- | --- |
| `Build` | The target framework restores and compiles; HIP loading is not implied. |
| `Managed-tested` | GPU-independent unit, package, generator, or static checks passed. |
| `Runtime-tested` | The specified OS, HIP/ROCm Runtime, and .NET runtime loaded successfully. |
| `GPU-validated` | The corresponding workload passed on a real AMD GPU. |

Baseline maintenance must not silently add unverified HIP P/Invoke declarations, downloaded ROCm
binaries, GPU claims, or Runtime payloads. Any future native asset requires an official source URL,
package/version, SHA-256, dependency closure, license evidence, and a clean-consumer test.

## 2. What you can contribute

You can contribute without an AMD GPU by improving managed lifetimes, error handling, documentation,
samples, generators, package audits, and GPU-independent tests. With a Windows AMD GPU, the most
valuable contribution is a reproducible Runtime/GPU result for a Windows path that the project cannot
currently validate locally.

Useful contributions include:

1. Running the existing workloads on an officially supported Windows 11 x64 + AMD GPU + HIP SDK
   combination and reporting the results.
2. Finding device, driver, HIP SDK, or .NET-specific issues in loading, ABI, memory, Stream/Event,
   HIPRTC, Module, Graph, or lifecycle behavior.
3. Adding a minimal reproduction, diagnostics, and documentation for a failure or controlled `Skipped`
   result instead of submitting unexplained logs.
4. Fixing code or documentation while preserving the API, package identity, generated files, and
   multi-target framework constraints.

## 3. Prerequisites

### Everyone

- Git.
- The .NET 10 SDK selected by `global.json`; confirm it with `dotnet --info`.
- PowerShell 7 (`pwsh`) for repository scripts. Windows PowerShell 5.1 is not the script baseline.
- NuGet access and a clean working tree.

### Windows GPU validators

- Windows 11 x64.
- An AMD GPU/APU, driver, and HIP SDK supported by the official AMD compatibility list. Review the
  [AMD HIP SDK system requirements](https://rocm.docs.amd.com/projects/install-on-windows/en/latest/reference/system-requirements.html)
  first. Results from an unlisted device may be useful experiments, but are not project support claims.
- Use the system-installed HIP SDK for the first validation. Do not download or commit unreviewed
  Runtime DLLs.
- .NET Framework validation must run on Windows. Linux/Radeon Cloud results cannot substitute for it.

### Linux/Radeon Cloud

Radeon Cloud is used only after explicit Owner authorization. Do not connect to historical instances,
guess old addresses, or put IPs, ports, private keys, tokens, or raw cloud logs in a commit. See
[`tools/radeon/README.md`](tools/radeon/README.md) for the cloud scripts.

## 4. Clone and establish the local baseline

Run this from PowerShell:

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
Set-Location HIP-CSharp-API
git status --short
dotnet --info
dotnet restore .\HipSharp.sln --locked-mode
```

Run the GPU-independent baseline:

```powershell
pwsh .\eng\build.ps1 -Configuration Release
pwsh .\eng\test.ps1 -Configuration Release -NoBuild
pwsh .\eng\test-docs.ps1 -Configuration Release
```

`build.ps1` checks deterministic interop output and all 15 target frameworks. `test.ps1` also packs
the Core package, runs unit/package/repository-quality tests, checks the public API, and builds a clean
consumer. This baseline does not require an AMD GPU. If it fails, record the command, full error
summary, commit SHA, and `dotnet --info` before investigating GPU behavior.

After changing an interop manifest or generated input, verify deterministic output:

```powershell
pwsh .\eng\generate-interop.ps1 -Check
```

## 5. Checks that do not need a GPU

`HipManagedExpansionValidation` provides a contract self-test without loading HIP:

```powershell
dotnet run --project .\samples\validation\HipManagedExpansionValidation\HipManagedExpansionValidation.csproj -c Release --no-restore -- --self-test
```

You can also build an individual sample to check project references and argument parsing:

```powershell
dotnet build .\samples\tutorials\01-RuntimeDevice\EnvironmentAndDevice\EnvironmentAndDevice.csproj -c Release --no-restore
```

A no-GPU result may only be reported as `Build` or `Managed-tested`. A successful sample compilation
is not Runtime or GPU validation.

## 6. Windows AMD GPU validation

### 6.1 Record the environment

Record the following before validation. Sanitize it before attaching it to an Issue or PR:

```powershell
git rev-parse HEAD
git status --short
dotnet --info
Get-ComputerInfo | Select-Object WindowsProductName, WindowsVersion, OsArchitecture
Get-CimInstance Win32_VideoController | Select-Object Name, DriverVersion, VideoModeDescription
hipcc --version
```

Also record the HIP SDK version, the GPU `gfx` architecture, whether the system SDK or a package was
used, and whether the build was Debug or Release. Do not upload device serial numbers, GUIDs, full
user paths, or internal host names.

### 6.2 Run workloads from low risk to high risk

Start with device and loader diagnostics:

```powershell
dotnet run --project .\samples\tutorials\01-RuntimeDevice\EnvironmentAndDevice\EnvironmentAndDevice.csproj -c Release --no-restore
dotnet run --project .\samples\tutorials\01-RuntimeDevice\LoaderDiagnostics\LoaderDiagnostics.csproj -c Release --no-restore
```

Then run memory, Stream/Event, and HIPRTC VectorAdd:

```powershell
dotnet run --project .\samples\tutorials\02-Memory\LinearMemoryCopy\LinearMemoryCopy.csproj -c Release --no-restore
dotnet run --project .\samples\tutorials\03-Execution\StreamAndEvent\StreamAndEvent.csproj -c Release --no-restore
$env:HIPSHARP_GPU_ARCH = 'gfx1100' # replace with the actual architecture
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --length 1000 --repeat 20
dotnet run --project .\samples\tutorials\04-Kernel\HipRtcVectorAdd\HipRtcVectorAdd.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --negative-compile
```

Finally run the reliability workload. It compares GPU results with the CPU and allows documented
capability-limited `Skipped` results; any undocumented failure should be treated as an issue:

```powershell
dotnet run --project .\samples\validation\AdvancedReliabilityStress\AdvancedReliabilityStress.csproj -c Release --no-restore -- --arch $env:HIPSHARP_GPU_ARCH --graph-launch-repeats 3 --lifecycle-repeats 100 --stress-rounds 10 --stress-streams 4 --stress-length 4194304
```

The order matters: confirm the loader and basic memory operations before entering HIPRTC, Module,
Graph, and stress scenarios. When a sample fails, preserve the first failure and any subsequent
cascade instead of reporting only the final exit code.

### 6.3 Windows Runtime package boundary

A Windows Runtime package still requires provenance, SHA-256, Authenticode, dependency-closure,
license, and SBOM evidence. Static audits can be run with:

```powershell
pwsh .\eng\test-windows-runtime-skeleton.ps1
pwsh .\eng\verify-windows-runtime.ps1
```

These scripts do not replace real GPU execution. Do not edit a manifest to make an audit pass, disable
signature checks, copy system DLLs into the repository, or manually set `gpuValidated` to `true`. If
you want to provide a Runtime package candidate, first describe its source and authorization in an
Issue, then provide sanitized evidence in the staging directory specified by the Owner.

## 7. Evidence and issue reports

Every Windows GPU validation report should include:

1. The exact 40-character Git SHA and whether the working tree was clean.
2. Windows version, GPU model and `gfx` architecture, driver, HIP SDK, .NET SDK, and build configuration.
3. The commands executed, passed items, failed items, and reasons for controlled `Skipped` results.
4. The first error's exception type, HIP/HIPRTC code, loader diagnostics, and relevant sample output.
5. Whether the system SDK, a Runtime package, or both were tested; state untested scope explicitly.
6. Safe-to-publish logs or screenshots with user paths, serials, GUIDs, internal addresses, and credentials removed.

Suggested Issue title:

```text
[Windows GPU][gfx1100][HIP SDK 7.2] HipRtcVectorAdd fails during module load
```

Suggested result format:

```text
Commit: <40-character-sha>
Host: Windows 11 x64 / <GPU> / <driver> / <HIP SDK>
.NET: <dotnet --info summary>
Runtime source: system HIP SDK or package <id/version>
Passed: EnvironmentAndDevice, LinearMemoryCopy, StreamAndEvent
Skipped: P2P (one device; capability unavailable)
Failed: HipRtcVectorAdd --negative-compile
First error: <exception, HIPRTC code, and short sanitized output>
Reproduction: <exact command>
```

Do not submit performance rankings or cross-device performance claims. Sample timings describe one
session only; correctness, resource release, and reproducible diagnostics matter more than one throughput
number.

## 8. Branches, commits, and pull requests

- Create a short-lived branch from the latest `main`, such as `test/windows-gpu-rx7900xt`,
  `fix/hiprtc-loader-diagnostic`, or `docs/windows-contributing`.
- Keep one topic per commit. Use a verb-first subject, for example `fix: preserve HIPRTC linker ownership`
  or `docs: add Windows GPU validation guide`.
- Before changing public APIs, native declarations, owner/disposal behavior, package identity, Runtime
  payloads, or versions, read [`docs/guides/api-freeze.md`](docs/guides/api-freeze.md) and describe the
  compatibility impact and evidence in the PR.
- Generate files with repository scripts. Commit the manifest/input change and generated output; do not
  hand-edit `.g.cs` files to bypass generator checks.
- A PR description should include purpose, affected paths, commands run, test level, Windows GPU
  environment when applicable, untested scope, and whether Owner-authorized Radeon Cloud validation
  is required.

Before committing, run:

```powershell
git diff --check
pwsh .\eng\build.ps1 -Configuration Release
pwsh .\eng\test.ps1 -Configuration Release -NoBuild
git status --short
```

Do not commit `bin/`, `obj/`, `artifacts/`, NuGet caches, HIP/ROCm native binaries, machine-specific
absolute paths, cloud addresses, certificates, SSH keys, PATs, tokens, or temporary test logs.
Report security issues privately according to [`SECURITY.md`](SECURITY.md), not in a public Issue.

## 9. Radeon Cloud versus Windows

Windows AMD contributors provide Windows hardware evidence. Radeon Cloud provides Owner-authorized
Linux/ROCm ABI, Runtime, GPU, package-closure, and repeat-validation evidence. The results are recorded
separately and cannot substitute for each other:

```text
Windows AMD GPU -> HIP SDK for Windows -> Windows loader / samples / .NET Framework
Radeon Cloud    -> Ubuntu + ROCm       -> Linux ABI / runtime package / GPU gates
```

If a PR needs cloud validation, state the exact SHA, target gates, expected duration, and whether a new
package is required. While waiting for authorization, continue only with locally verifiable work.

## 10. NuGet releases

Stable package publication is handled by [`.github/workflows/nuget-release.yml`](.github/workflows/nuget-release.yml).
Create and push an annotated `vMAJOR.MINOR.PATCH` tag only after updating `eng/Versions.props` and completing
the authorized release gates. The workflow checks that the tag, project version, and package version match,
then runs the Release tests, documentation validation, package audit, and clean consumers before publishing.

The repository must contain an Actions secret named `NUGET_API_KEY` with permission to push
`JYPPX.ROCm.HIP.CSharp.API`. Never put the key in a workflow file, command committed to the repository,
issue, log, or package. The workflow verifies that the published package is downloadable and carries a valid
NuGet.org repository signature.

## 11. Quick checklist

- [ ] I am working in the correct Git root and did not modify or commit `plan/`, `diary/`, or `Radeon_Cloud/`.
- [ ] I recorded `dotnet --info`, the commit SHA, and working-tree status.
- [ ] I ran the relevant build/test commands and separated Build, Managed-tested, Runtime-tested, and GPU-validated claims.
- [ ] My Windows GPU report contains GPU, driver, HIP SDK, architecture, and reproducible commands.
- [ ] Every `Skipped` result has a documented capability or environment reason; unknown failures are not skipped.
- [ ] I did not commit native binaries, credentials, internal addresses, caches, or unsanitized logs.
- [ ] The PR states untested scope and whether Radeon Cloud/Owner review is needed.
