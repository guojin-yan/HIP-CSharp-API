# StreamAndEvent / Stream 与 Event

This sample performs asynchronous H2D and D2H copies on one non-blocking stream, records an event,
synchronizes the stream, and verifies the returned bytes on the CPU.

本案例在一个 non-blocking stream 上执行异步 H2D 和 D2H copy，记录 event，同步 stream，并在 CPU
端验证返回数据。它只讲解执行顺序和生命周期，不包含 Kernel 或性能测试。

```powershell
dotnet run --project samples/tutorials/03-Execution/StreamAndEvent/StreamAndEvent.csproj -c Release
```
