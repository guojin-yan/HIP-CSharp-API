# Arrays, textures, and surfaces / 数组、纹理与表面

`HipRuntime` exposes owned `HipArray`, `HipMipmappedArray`, `HipTextureObject`, and `HipSurfaceObject` resources. Runtime-style and driver-style creation paths keep their matching native release functions. A mipmap level is a borrowed `HipArray` view that leases its parent; disposing the view never frees the level independently.

`HipRuntime` 提供拥有型 `HipArray`、`HipMipmappedArray`、`HipTextureObject` 和 `HipSurfaceObject`。runtime-style 与 driver-style 创建路径分别使用匹配的 native release。mipmap level 是持有 parent lease 的 borrowed `HipArray` view；释放该 view 不会单独释放 level。

```csharp
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

var runtime = new HipRuntime();
var rgba8 = new HipChannelFormatDescriptor(8, 8, 8, 8, HipChannelFormatKind.UnsignedInteger);
byte[] hostBytes = new byte[256 * 256 * 4];

using HipArray pixels = runtime.AllocateArray(
    rgba8,
    width: 256,
    height: 256,
    flags: HipArrayFlags.SurfaceLoadStore);

pixels.Copy2DFrom(hostBytes, widthBytes: 256 * 4, height: 256);

using var texture = runtime.CreateTextureObject(
    pixels,
    new HipTextureDescriptor
    {
        FilterMode = HipTextureFilterMode.Linear,
        NormalizedCoordinates = true,
    });

using var surface = runtime.CreateSurfaceObject(pixels);
```

Texture and surface objects retain their backing array, mipmapped array, or pointer owner. Calling `Dispose` on the backing owner makes that wrapper unusable immediately, while the actual native free is deferred until the dependent texture or surface is destroyed. Asynchronous two-dimensional copies similarly retain the array and pinned managed buffer until the stream completes.

texture 与 surface object 会保活 backing array、mipmapped array 或 pointer owner。对 backing owner 调用 `Dispose` 后，该 wrapper 立即不可再使用，但实际 native free 会延迟到依赖它的 texture 或 surface 被销毁。异步二维复制同样会保活 array 和 pinned managed buffer，直至 stream 完成。

`HipTextureReference` is a borrowed façade for deprecated native texture references. It requires an explicit native texture-symbol pointer, retains a bound managed resource until `Unbind` or `Dispose`, and returns bound array values only as borrowed handles. It never destroys the native texture symbol.

`HipTextureReference` 是 deprecated native texture reference 的 borrowed façade。它要求显式传入 native texture-symbol pointer，在 `Unbind` 或 `Dispose` 前保活绑定的 managed resource，并且仅以 borrowed handle 返回绑定的 array 值；它绝不会销毁 native texture symbol。

ROCm can report `hipErrorNotSupported` for platform- or device-dependent array, texture, surface, virtual-memory, and legacy-reference operations. The managed layer translates that result to `HipException` without manufacturing a fallback. This surface has local fake-native lifecycle and ABI-layout coverage, but no current exact-SHA GPU or cloud evidence.

ROCm 可对依赖平台或设备能力的 array、texture、surface、virtual-memory 和 legacy-reference 操作返回 `hipErrorNotSupported`。managed 层会将其转换为 `HipException`，不会伪造 fallback。本接口已有本地 fake-native 生命周期与 ABI layout 覆盖，但尚无当前精确 SHA 的 GPU 或云端证据。
