# PrecompiledModule

Loads a precompiled `.hsaco` or compatible code object and resolves a kernel without HIPRTC.

```powershell
dotnet run --project samples/tutorials/04-Kernel/PrecompiledModule/PrecompiledModule.csproj -c Release -- .\vector-add.hsaco VectorAdd
```
