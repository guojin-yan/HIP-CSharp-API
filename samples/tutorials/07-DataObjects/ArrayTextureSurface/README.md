# ArrayTextureSurface / Array、Texture 与 Surface

This sample creates a two-dimensional HIP array, verifies a host round trip, and creates texture and
surface owners over the same array. It demonstrates ownership only; it is not an image-processing
or texture-performance benchmark.

本案例创建二维 HIP array，验证 host 往返，并在同一 array 上创建 texture 和 surface owner。案例只
演示资源类型和生命周期，不是图像处理或纹理性能测试。

```powershell
dotnet run --project samples/tutorials/07-DataObjects/ArrayTextureSurface/ArrayTextureSurface.csproj -c Release
```
