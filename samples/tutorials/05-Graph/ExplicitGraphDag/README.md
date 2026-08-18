# ExplicitGraphDag

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

`ExplicitGraphDag` is the 05-Graph tutorial case. Builds a two-node directed acyclic graph, inspects its topology, instantiates it, and launches it on a stream.

## What This Teaches

This case focuses on HipGraph.AddEmpty, dependency edges, HipGraph.Instantiate, HipGraphExec.Launch.

It is intentionally small: the program makes one ownership or execution contract visible and returns
an explicit exit code. It is a learning and correctness example, not a performance benchmark.

## Environment and Validation Scope

The cloud validation was performed on Radeon Cloud with AMD Radeon Graphics (`gfx1100`), ROCm 7.2.1,
and .NET 10.0.110. Windows build and GPU execution have not been validated by this project. The Windows
section below is a best-effort build/run guide and must not be read as Windows validation.

The repository uses `global.json` and currently expects .NET SDK `10.0.300` with patch roll-forward.
The cloud runner invokes the SDK from the repository parent so a compatible cloud SDK feature band can be
used without changing the repository pin.

## Reproduce on Radeon Cloud

From the repository root, run the complete matrix:

```bash
bash ./samples/tutorials/run-cloud-verification.sh
```

The runner detects the first `gfxNNNN` architecture, restores locked dependencies, builds this and the
other tutorial projects individually, runs each executable, and saves the evidence directory. To run
only this case on a host with a matching SDK:

```bash
dotnet run --project samples/tutorials/05-Graph/ExplicitGraphDag/ExplicitGraphDag.csproj --configuration Release
```

For cases that need an architecture, replace `gfx1100` with the value reported by `rocminfo`. The
`ExplicitGraphDag` entry in the matrix is the authoritative cloud status for this tutorial.

## Reproduce on Windows (Not Validated)

The following commands are provided for source inspection and a best-effort local build only:

```powershell
git clone https://github.com/guojin-yan/HIP-CSharp-API.git
cd HIP-CSharp-API
dotnet restore .\samples\tutorials\05-Graph\ExplicitGraphDag\ExplicitGraphDag.csproj --locked-mode
dotnet build .\samples\tutorials\05-Graph\ExplicitGraphDag\ExplicitGraphDag.csproj --configuration Release --no-restore
dotnet run --project .\samples\tutorials\05-Graph\ExplicitGraphDag\ExplicitGraphDag.csproj --configuration Release --no-build --
```

Actual GPU execution additionally requires a matching AMD Windows driver, HIP Runtime, HIPRTC, native
library search paths, and a supported architecture. This project has not validated those Windows
runtime prerequisites. A successful build is not a GPU verification result.

## Execution Walkthrough

1. `Program.Main` parses the optional arguments and establishes the HIP/runtime objects used by the case.
2. The case performs the capability checks and operations described in the source walkthrough below.
3. Host-side validation compares the returned values or state against a deterministic expectation.
4. The process returns `0` for pass or controlled skip, and `1` for an unexpected failure.

The key expected output for this cloud run was:

```text
Explicit graph DAG passed.
```

## Cloud Evidence

Cloud status: **Passed**.

The cloud log reports a passing two-node DAG topology and launch.

The complete evidence directory is
[`Radeon_Cloud/records/20260818-161709-tutorials`](../../../../../Radeon_Cloud/records/20260818-161709-tutorials).
The case log is `logs/ExplicitGraphDag.log`; `results.csv` records the status and exit code for all 20 tutorials.

## Source Walkthrough

| File | Role |
| --- | --- |
| `Program.cs` | Explicit entry point, HIP operations, validation, and exit status |
| `ExplicitGraphDag.csproj` | .NET target and local HIPSharp project reference |
| `packages.lock.json` | Locked package/project dependency graph |
| `../../run-cloud-verification.sh` | Cloud architecture detection, build matrix, execution, and evidence retention |

Read the surrounding module guide for the prerequisite concepts:
[`05-Graph`](../README.md).

## Troubleshooting

- If the program cannot find HIP, verify `/dev/kfd`, `/dev/dri`, `rocminfo`, and the ROCm installation.
- If HIPRTC reports a target error, pass the exact `gfxNNNN` value reported by `rocminfo`.
- If the result is `Skipped`, read the case log: capability-gated skips are expected on some devices.
- If you are running `PrecompiledModule`, provide `HIPSHARP_PRECOMPILED_CODE_OBJECT` or a direct `.hsaco` path.
- If a Windows native library cannot be loaded, treat it as an unvalidated platform limitation and reproduce on Radeon Cloud.

## Next Step

After this case passes, continue to the next case in the module. Keep the deterministic validation in
place while changing one concept at a time; do not turn these tutorial cases into timing benchmarks.
