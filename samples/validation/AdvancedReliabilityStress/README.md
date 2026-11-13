# AdvancedReliabilityStress

This sample runs a CPU/GPU vector-add comparison with stream-ordered allocations and a captured HIP graph. It also exercises managed-memory advice/prefetch, repeats owner lifecycles 100 times, and performs a byte-verified P2P copy for devices 1 to 0 when the pair is available.

```powershell
$env:HIPSHARP_GPU_ARCH = 'gfx1100'
dotnet run --project samples/validation/AdvancedReliabilityStress/AdvancedReliabilityStress.csproj -c Release -- --graph-launch-repeats 3 --lifecycle-repeats 100
```

The graph and async-allocation APIs require matching support from the installed HIP Runtime. With fewer than two devices, or with an unsupported pair, P2P is reported as skipped rather than treated as a failure; a capable pair must complete and verify the copy.

Managed-memory advice/prefetch requires the device to report both managed-memory and concurrent-managed-access support. Otherwise only that subtest is reported as skipped, while graph, owner lifecycle and multi-stream stress checks continue. Unexpected native errors remain failures. The summary records `managedMemory=skipped(device-capability)` instead of claiming it ran.

The optional cloud reliability mode submits every lane before synchronization, validates all results against the CPU, and repeats stream-ordered allocation/release. The defaults below keep at most 192 MiB of device buffers in flight. It reports conditions and pass/fail only, not timing or a performance claim.

```powershell
dotnet run --project samples/validation/AdvancedReliabilityStress/AdvancedReliabilityStress.csproj -c Release -- `
  --arch gfx1100 --graph-launch-repeats 3 --lifecycle-repeats 250 `
  --stress-rounds 10 --stress-streams 4 --stress-length 4194304
```

The final lines report the tested lengths, repeat counts, each advanced feature path, stress conditions, and `failureIndex=-1` on success.
