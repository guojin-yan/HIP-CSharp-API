# HipAdvancedFeatures

This sample runs a CPU/GPU vector-add comparison with stream-ordered allocations and a captured HIP graph. It also exercises managed-memory advice/prefetch, repeats owner lifecycles 100 times, and reports the explicit P2P capability path for devices 0 and 1.

```powershell
$env:HIPSHARP_GPU_ARCH = 'gfx1100'
dotnet run --project samples/HipAdvancedFeatures/HipAdvancedFeatures.csproj -c Release -- --graph-launch-repeats 3 --lifecycle-repeats 100
```

The graph and async-allocation APIs require matching support from the installed HIP Runtime. With fewer than two devices, or with an unsupported pair, P2P is reported as skipped rather than treated as a failure.

The final line reports the tested lengths, repeat counts, each advanced feature path, and `failureIndex=-1` on success.
