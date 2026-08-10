# HipRtcVectorAdd

Compiles a HIP C++ VectorAdd kernel in memory with HIPRTC, loads the resulting code object, launches it on the default stream, synchronizes, copies the result to the CPU, and checks every element after every repetition. The code object is never written to disk.

Pass the actual GPU architecture explicitly:

```bash
dotnet run --project samples/HipRtcVectorAdd/HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --length 1000 --repeat 20
```

Run the expected compiler-failure check, which succeeds only when `HipRtcException` contains a compilation log:

```bash
dotnet run --project samples/HipRtcVectorAdd/HipRtcVectorAdd.csproj -c Release -- --arch gfx1100 --negative-compile
```

`HIPSHARP_GPU_ARCH` can replace `--arch`. The sample deliberately does not guess an architecture or make performance claims.
