# HipRtcProgramLinker

Compiles LLVM bitcode, adds an in-memory linker input, and copies the completed code object before disposing the linker.

```powershell
dotnet run --project samples/tutorials/04-Kernel/HipRtcProgramLinker/HipRtcProgramLinker.csproj -c Release -- gfx1100
```
