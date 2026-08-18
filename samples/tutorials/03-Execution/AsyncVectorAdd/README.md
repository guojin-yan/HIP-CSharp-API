# AsyncVectorAdd / 异步 VectorAdd

This sample is a real GPU gate for the explicit stream/event path. It compiles VectorAdd with HIPRTC, creates two non-blocking streams and events, queues asynchronous H2D, kernel, and D2H work, synchronizes, and compares every CPU element for lengths `1`, `127`, `256`, `1000`, and `1048576`. It also repeats normal and error-prone lifecycle operations at least 100 times and disposes resources in reverse order.

Run it on an authorized ROCm host with an explicit architecture:

```text
dotnet run --project samples/tutorials/03-Execution/AsyncVectorAdd/AsyncVectorAdd.csproj -c Release -- --arch gfx1100
```

The sample is a correctness gate, not a benchmark; it emits no unsupported performance claim. The cloud gate must capture device attributes, stream/event query and synchronization, async transfers, CPU comparison, and repeated disposal evidence.
