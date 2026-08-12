# Complete Native API / 完整原生 API

The package now contains two low-level entry-point surfaces generated from the pinned ROCm 7.2.1 headers:

该包现在提供两个由固定 ROCm 7.2.1 头文件生成的低层入口面：

- `HipRuntimeNativeApi`: 459 public declarations from `hip/hip_runtime_api.h`, including the 11 HIP memory-pool exports kept low-level for raw ABI callers.
- `HipRtcNativeApi`: 18 public declarations from `hip/hiprtc.h`, including name expressions, linker, and bitcode calls.

The existing `HipRuntime` and `HipRtc` classes remain the preferred managed surface. They validate arguments, translate errors, and own streams, events, linear, pitched, and pooled allocations, explicit and captured graphs, programs, modules, and module kernel occupancy/cooperative launch. The managed manifest currently contains 90 Runtime and 9 HIPRTC entries; 369 Runtime and 9 HIPRTC declarations remain low-level-only. The complete low-level model remains 459 Runtime and 18 HIPRTC declarations.

现有的 `HipRuntime` 与 `HipRtc` 仍是推荐的托管 API；它们负责参数检查、错误转换以及 stream、event、linear/pitched/pooled allocation、显式与捕获 graph、program、module 和 module kernel occupancy/cooperative launch 的所有权。managed manifest 当前包含 90 个 Runtime 和 9 个 HIPRTC 入口；仍有 369 个 Runtime 和 9 个 HIPRTC 声明仅提供低层调用。完整低层 model 仍包含 459 个 Runtime 与 18 个 HIPRTC 声明。

## Raw ABI rules / 原生 ABI 规则

Constructing a low-level client loads its corresponding logical library through the same verified resolver as the managed owners:

创建低层客户端时，会通过与托管 owner 相同的已验证 resolver 加载对应 logical library：

```csharp
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Types;

var runtime = new HipRuntimeNativeApi();
var rtc = new HipRtcNativeApi();
HipError result = runtime.Init(0);
```

Pointer, callback, string-buffer, and pointer-to-structure parameters are exposed as `IntPtr`. The caller owns allocation, pinning, encoding, and release of those buffers. By-value ABI structures have explicit layouts in `JYPPX.HipSharp.Types`: `HipDim3`, `HipExtent`, `HipPitchedPtr`, `HipMemLocation`, `HipIpcMemHandle`, and `HipIpcEventHandle`.

指针、回调、字符串缓冲区和复杂结构体指针参数统一暴露为 `IntPtr`；缓冲区的分配、pin、编码和释放由调用方负责。按值传递的 ABI 结构在 `JYPPX.HipSharp.Types` 中提供了明确布局：`HipDim3`、`HipExtent`、`HipPitchedPtr`、`HipMemLocation`、`HipIpcMemHandle` 和 `HipIpcEventHandle`。

The raw methods do not add synchronization or ownership. A successful native return value only means that HIP accepted the call. A header declaration also does not guarantee that every platform library exports it; an unavailable entry point raises the normal .NET native entry-point exception. Use the managed owners when a lifecycle contract matters.

原生方法不额外执行同步或所有权管理。原生返回成功只表示 HIP 接受了调用；头文件声明也不保证每个平台的动态库都导出该入口，缺少入口时会抛出标准 .NET 原生入口异常。需要生命周期契约时应使用托管 owner。

## Reproducible generation / 可重复生成

`eng/interop/complete-api-model.json` records the two official header hashes and the extracted signatures. The generator never downloads or searches for headers implicitly. To verify the model against an explicitly prepared header root:

`eng/interop/complete-api-model.json` 记录两份官方头文件的哈希和提取后的签名。生成器不会隐式下载或搜索头文件。要使用显式准备的头文件目录复核模型：

```powershell
./eng/generate-interop.ps1 generate -HeaderRoot ./artifacts/hip-headers -Check
```

The gate fails closed unless the headers produce exactly 459 Runtime and 18 HIPRTC public C functions. Cloud GPU execution is intentionally a separate validation step; this source-level gate proves declaration coverage and generation determinism, not device capability.

如果头文件提取结果不是正好 459 个 Runtime 和 18 个 HIPRTC 公开 C 函数，门禁会直接失败。云端 GPU 执行仍是独立验证步骤；这里证明的是声明覆盖和生成确定性，不是设备能力。
