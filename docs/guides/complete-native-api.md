# Complete Native API / 完整原生 API

The package now contains two low-level entry-point surfaces generated from the pinned ROCm 7.2.1 headers:

该包现在提供两个由固定 ROCm 7.2.1 头文件生成的低层入口面：

- `HipRuntimeNativeApi`: 459 public declarations from `hip/hip_runtime_api.h`, including the 11 HIP memory-pool exports kept low-level for raw ABI callers.
- `HipRtcNativeApi`: 18 public declarations from `hip/hiprtc.h`, including name expressions, linker, and bitcode calls.

The existing `HipRuntime` and `HipRtc` classes remain the preferred managed surface. `HipRuntime.AdvancedInterop` now owns the reviewed external-memory/semaphore, graphics-resource mapping, IPC, callback, profiler, user-object, external-semaphore graph, and driver-compatibility entries. The reviewed managed surface covers 239 declarations: 109 ABI-manifest entries plus 130 high-level promotions. The remaining declarations are explicitly classified as 238 `raw-only-reviewed`; the 48 former `deferred-capability` entries are now exposed through the advanced capability facade. The complete low-level model remains 459 Runtime and 18 HIPRTC declarations.

现有的 `HipRuntime` 与 `HipRtc` 仍是推荐的托管 API；`HipRuntime.AdvancedInterop` 已纳入外部内存/信号量、graphics resource 映射、IPC、callback、profiler、user object、外部信号量 graph 节点和 driver 兼容入口。经审查的 managed surface 覆盖 239 个声明：109 个 ABI manifest 入口加 130 个高层 promotion。其余 238 个声明明确分类为 `raw-only-reviewed`；原先 48 个 `deferred-capability` 入口已通过高级能力 facade 暴露。完整低层 model 仍包含 459 个 Runtime 与 18 个 HIPRTC 声明。

`hipModuleGetGlobal` remains the owning module-global identity path. Compiler-symbol address/size queries and legacy texture references now have explicit borrowed-pointer façades; they never claim ownership of the native symbol. `HipRuntime.AdvancedInterop` is the reviewed ownership path for external graphics/IPC, callback, profiler, user-object, external-semaphore graph, and deprecated driver-overlap calls; descriptor storage remains borrowed and caller-owned.

`hipModuleGetGlobal` 仍是拥有明确 module-global identity 的 owner 路径。compiler-symbol address/size query 与 legacy texture reference 现在提供显式 borrowed-pointer façade，但绝不取得 native symbol 所有权。`HipRuntime.AdvancedInterop` 是 external graphics/IPC、callback、profiler、user object、external-semaphore graph 和 deprecated driver overlap 入口的审查后所有权路径；descriptor 存储仍由调用方借用并负责。

## Raw ABI rules / 原生 ABI 规则

Constructing a low-level client loads its corresponding logical library through the same verified resolver as the managed owners:

创建低层客户端时，会通过与托管 owner 相同的已验证 resolver 加载对应 logical library：

```csharp
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Types;

var runtime = new HipRuntimeNativeApi();
var rtc = new HipRtcNativeApi();
HipError result = runtime.Init(0);
```

Pointer, callback, string-buffer, and pointer-to-structure parameters are exposed as `IntPtr`. The caller owns allocation, pinning, encoding, and release of those buffers. By-value ABI structures have explicit layouts in `JYPPX.ROCm.HipSharp.Types`: `HipDim3`, `HipExtent`, `HipPitchedPtr`, `HipMemLocation`, `HipIpcMemHandle`, and `HipIpcEventHandle`.

指针、回调、字符串缓冲区和复杂结构体指针参数统一暴露为 `IntPtr`；缓冲区的分配、pin、编码和释放由调用方负责。按值传递的 ABI 结构在 `JYPPX.ROCm.HipSharp.Types` 中提供了明确布局：`HipDim3`、`HipExtent`、`HipPitchedPtr`、`HipMemLocation`、`HipIpcMemHandle` 和 `HipIpcEventHandle`。

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
