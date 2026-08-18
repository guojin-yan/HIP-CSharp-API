# GraphCaptureReplay / Graph 捕获与重放

This sample captures an asynchronous memory round trip, instantiates the captured graph, launches it,
and verifies the result. A missing optional graph capability is reported as a controlled skip.

本案例捕获一次异步内存往返，实例化并启动 captured graph，然后验证结果。Runtime 缺少可选 Graph
能力时会明确输出受控 skip。

```powershell
dotnet run --project samples/tutorials/05-Graph/GraphCaptureReplay/GraphCaptureReplay.csproj -c Release
```
