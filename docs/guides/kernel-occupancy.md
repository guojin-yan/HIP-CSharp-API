# Managed kernel occupancy and cooperative launch / 托管 Kernel Occupancy 与 Cooperative Launch

M8.5 adds typed resource attributes, occupancy estimates, launch plans, and single-device cooperative launch to module-owned `HipKernel` instances. The API uses `hipFunction_t` throughout; it does not reinterpret a compiler function address, expose a native attribute enum, or require a `void**` parameter array.

M8.5 为 module-owned `HipKernel` 增加 typed 资源属性、occupancy 估算、launch plan 和单设备 cooperative launch。该 API 始终使用 `hipFunction_t`，不会把编译期函数地址错误转换为 module function，也不公开 native attribute enum 或要求用户构造 `void**` 参数数组。

```csharp
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Modules;

using var runtime = new HipRuntime();
HipDevice device = runtime.GetCurrentDevice();
using var stream = runtime.CreateStream();
using HipModule module = runtime.LoadModule(codeObject);
HipKernel kernel = module.GetKernel("cooperative_reduce");

HipKernelAttributes attributes = kernel.GetAttributes();
HipOccupancyPlan plan = kernel.GetOccupancyPlan();

if (device.SupportsCooperativeLaunch)
{
    kernel.LaunchCooperative(
        stream,
        new HipLaunchDimensions((uint)plan.MinimumGridSize),
        new HipLaunchDimensions((uint)plan.BlockSize),
        arguments);
    stream.Synchronize();
}
```

## Units and results / 单位与结果

`HipKernelAttributes` reports maximum threads per block, static shared-memory bytes per block, constant-memory bytes, local-memory bytes per thread, registers per thread, the binary version, and maximum dynamic shared-memory bytes per block. Backend-specific PTX, cache-mode, and shared-memory carveout policy fields are intentionally absent.

On AMD ROCm 7.2.1, an unused constant-memory region is reported by `hipFuncGetAttribute` as the exact sentinel `-1`; `GetAttributes()` normalizes that value to zero. Other negative resource values still fail closed.

`HipKernelAttributes` 返回每个 block 最大线程数、每个 block 静态共享内存字节数、常量内存字节数、每线程 local memory 字节数、每线程寄存器数、binary version 和每个 block 最大动态共享内存字节数。具有 backend-specific 含义的 PTX、cache mode 和 shared-memory carveout policy 字段不会伪装成通用契约。

在 AMD ROCm 7.2.1 上，未使用的常量内存区域会被 `hipFuncGetAttribute` 以精确 sentinel `-1` 返回；`GetAttributes()` 会将其归一化为零，其他负资源值仍会 fail closed。

`GetOccupancy(blockSize, dynamicSharedMemoryBytes, flags)` takes a one-dimensional thread count and returns active blocks per multiprocessor, the device multiprocessor count, and their checked product. `GetOccupancyPlan` returns a minimum grid size in blocks and a suggested block size in threads. A `blockSizeLimit` of zero means the native kernel maximum. `HipOccupancyFlags.Default` uses the non-flags export; `DisableCachingOverride` uses the flags export. Unknown bits fail before native code.

`GetOccupancy(blockSize, dynamicSharedMemoryBytes, flags)` 的 block size 单位是线程，返回每个 multiprocessor 的 active block 数、设备 multiprocessor 数和 checked 乘积。`GetOccupancyPlan` 的 minimum grid size 单位是 block，建议 block size 单位是线程。`blockSizeLimit=0` 保留原生“kernel 最大值”语义。`HipOccupancyFlags.Default` 调用 non-flags 导出，`DisableCachingOverride` 调用 flags 导出，未知位会在 native 调用前失败。

Occupancy is a resource-residency estimate, not a benchmark and not a guarantee of the fastest configuration. HIP remains the final authority for launch legality.

Occupancy 是资源常驻估算，不是 benchmark，也不保证得到最快配置；HIP Runtime 仍是启动合法性的最终判断者。

## Cooperative launch contract / Cooperative Launch 契约

Both default-stream and explicit-stream overloads validate three-dimensional grid/block products, dynamic shared memory, arguments, the current module device, cooperative capability, and resident capacity. The managed capacity rule is:

default stream 与 explicit stream 重载都会检查三维 grid/block 乘积、动态共享内存、参数、module 当前设备、cooperative capability 和常驻容量。托管容量规则为：

```text
grid.X * grid.Y * grid.Z
    <= activeBlocksPerMultiprocessor * multiprocessorCount
```

There is no fallback to ordinary `hipModuleLaunchKernel`: doing so would silently remove grid-wide synchronization semantics. A missing optional occupancy or cooperative export becomes `HipError.NotSupported` through `HipException`.

API 不会 fallback 到普通 `hipModuleLaunchKernel`，因为这会静默移除 grid-wide synchronization 语义。缺少 optional occupancy 或 cooperative export 时，会通过 `HipException` 稳定报告 `HipError.NotSupported`。

An explicit stream must belong to the same Runtime and device as the module. Stream-ordered memory must use its allocation stream. The module and all pointer owners remain alive until `Query`, `Synchronize`, or stream disposal observes completion. For default-stream submission, callers must synchronize before disposing owners. Graph-local pointers remain graph-only.

explicit stream 必须与 module 属于同一 Runtime 和设备；stream-ordered memory 必须使用其 allocation stream。module 和所有 pointer owner 会保留到 `Query`、`Synchronize` 或 stream dispose 确认完成。default stream 提交后，调用方必须先同步再释放 owner；graph-local pointer 仍只能用于 graph。

## Deliberate low-level boundary / 明确保留低层的边界

`hipFuncGetAttributes`, `hipFuncSetAttribute`, `hipFuncSetCacheConfig`, `hipFuncSetSharedMemConfig`, and Runtime `hipOccupancy*` variants accept compiler function addresses (`const void*`), not a module `hipFunction_t`. Multi-device cooperative launch requires a separate cross-device launch-list and partial-failure ownership design. These functions remain on `HipRuntimeNativeApi`; M8.5 does not weaken their pointer semantics.

`hipFuncGetAttributes`、`hipFuncSetAttribute`、`hipFuncSetCacheConfig`、`hipFuncSetSharedMemConfig` 和 Runtime `hipOccupancy*` variants 接受编译期函数地址（`const void*`），不是 module `hipFunction_t`。multi-device cooperative launch 还需要独立的跨设备 launch list 和 partial-failure owner 设计。这些函数继续保留在 `HipRuntimeNativeApi`，M8.5 不会弱化其指针语义。

Local fake-native tests prove managed validation, routing, marshaling, and lifetime only. Real export, Runtime, occupancy-result, and cooperative GPU execution evidence is pending a newly Owner-authorized Radeon Cloud session.

本地 fake-native 测试只证明托管 validation、路由、封送与生命周期。真实 export、Runtime、occupancy 结果和 cooperative GPU 执行证据仍等待 Owner 新授权的 Radeon Cloud 会话。
