# NuGetQuickstart / NuGet 消费者起步

This application references Core `0.9.1` through `PackageReference` and does not reference the repository
source project. Provide a compatible system HIP Runtime or the matching optional Runtime package before
running it.

本应用通过 `PackageReference` 引用 Core `0.9.1`，不引用仓库源码。运行前请提供兼容的系统 HIP
Runtime，或安装匹配的可选 Runtime 包。

```powershell
dotnet run --project samples/consumers/NuGetQuickstart/NuGetQuickstart.csproj -c Release
```
