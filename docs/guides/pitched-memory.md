# Pitched memory and 2D/3D copies / Pitched memory 与 2D/3D copy

`HipPitchedDeviceMemory<T>` owns a `hipMallocPitch` or `hipMalloc3D` allocation and releases it with
`hipFree`. Dimensions, offsets, and regions are measured in `T` elements. `PitchBytes`,
`SlicePitchBytes`, and pinned-buffer offsets are measured in bytes. The native pointer and
`hipMemcpy3DParms` remain implementation details.

`HipPitchedDeviceMemory<T>` 独占 `hipMallocPitch` 或 `hipMalloc3D` allocation，并通过 `hipFree`
释放。尺寸、偏移与区域使用 `T` 元素为单位；`PitchBytes`、`SlicePitchBytes` 和 pinned buffer
偏移使用字节为单位。原生指针和 `hipMemcpy3DParms` 均保留为实现细节。

```csharp
using JYPPX.HipSharp;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Streams;

using var runtime = new HipRuntime();
HipMemoryInfo info = runtime.GetMemoryInfo();

using HipPitchedDeviceMemory<float> source = runtime.Allocate2D<float>(1920, 1080);
using HipPitchedDeviceMemory<float> destination = runtime.Allocate2D<float>(1920, 1080);
using HipStream stream = runtime.CreateStream();

source.SetZeroAsync(stream);
destination.CopyFromAsync(source, stream);
stream.Synchronize();
```

`SetZero` and `SetByte` cover the complete logical extent or an explicit `HipMemoryRegion`.
`SetByte` repeats one 8-bit pattern and is not a typed-value fill; zero is valid for all unmanaged
element representations. Copy overloads support tightly packed managed arrays, `HipPinnedMemory`,
and another pitched owner of the same element type. A 3D region uses byte X coordinates internally
as required by HIP, while the public X coordinate remains an element offset.

`SetZero` 与 `SetByte` 可处理完整逻辑范围或显式 `HipMemoryRegion`。`SetByte` 重复一个 8-bit
pattern，并不是 typed-value fill；零适用于所有 unmanaged 元素表示。copy overload 支持紧密排列的
托管数组、`HipPinnedMemory` 和同元素类型的另一个 pitched owner。3D region 在内部按 HIP 要求把 X
转换为 byte 坐标，公开 X 坐标仍是元素偏移。

Synchronous calls borrow buffers only until the native call returns. Asynchronous calls retain every
device allocation, pinned owner, or pinned managed array until `HipStream.Synchronize`, a successful
`Query`, stream disposal, or graph ownership completes the lease. Disposing a referenced memory owner
marks it unusable immediately and defers `hipFree` until the lease ends. Runtime-client, creation-device,
stream-device, disposed-state, region, pitch, and overflow checks run before the memory operation.

同步调用只在 native call 返回前借用 buffer。异步调用会保活每个 device allocation、pinned owner
或 pinned 托管数组，直到 `HipStream.Synchronize`、成功的 `Query`、stream Dispose 或 graph owner
完成 lease。释放仍被引用的 memory owner 会立即禁止继续使用，并把 `hipFree` 延迟到 lease 结束。
Runtime client、创建 device、stream device、disposed 状态、region、pitch 和 overflow 均在内存操作前检查。

HIP arrays, symbols, legacy driver-copy structures, batch copies, peer 3D copies, and arbitrary raw
pointer combinations remain low-level APIs. Their ownership and synchronization models require separate
managed designs.

HIP array、symbol、legacy driver-copy struct、batch copy、peer 3D copy 和任意 raw pointer 组合仍为
低层 API；它们需要单独设计所有权和同步模型。

This M8.2 surface has managed-only fake-native tests and multi-target compilation evidence. It has not
yet been executed against a real HIP Runtime or GPU.

本 M8.2 表面已有 managed-only fake-native 测试和多目标编译证据，尚未针对真实 HIP Runtime 或 GPU 执行。
