# NativeAbiInterop / 低层原生 ABI

This expert sample calls the generated HIP Runtime C ABI directly and checks raw `HipError` values.
Normal applications should prefer `HipRuntime` and the managed owner types.

本专家案例直接调用生成式 HIP Runtime C ABI，并手工检查原始 `HipError`。普通应用应优先使用
`HipRuntime` 和托管 owner 类型。

```powershell
dotnet run --project samples/tutorials/90-LowLevel/NativeAbiInterop/NativeAbiInterop.csproj -c Release
```
