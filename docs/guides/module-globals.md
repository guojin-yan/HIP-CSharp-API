# Managed module globals / 托管 Module 全局符号

`HipModule.GetGlobal` resolves global, device, and constant symbols exported by a loaded code object.
The returned `HipModuleGlobal` is a borrowed byte range owned by the module. It is not an allocation,
has no `Dispose` method, and never calls `hipFree`.

`HipModule.GetGlobal` 可查询 loaded code object 导出的 global、device 与 constant symbol。返回的
`HipModuleGlobal` 是由 module 拥有的 borrowed byte range，不是 allocation、没有 `Dispose`，也绝不会
调用 `hipFree`。

```csharp
using var runtime = new HipRuntime();
using HipStream stream = runtime.CreateStream();
using HipModule module = runtime.LoadModule(codeObject);

HipModuleGlobal counter = module.GetGlobal("counter");
counter.CopyFrom(new byte[] { 1, 0, 0, 0 });

var counterBytes = new byte[4];
counter.CopyTo(counterBytes);

HipModuleGlobal<int> values = module.GetGlobal<int>("values");
values.CopyFrom(new[] { 1, 2, 3, 4 });
var result = new int[4];
values.CopyToAsync(result, stream);
stream.Synchronize();
```

## Units and ranges / 单位与范围

`HipModuleGlobal` measures `ByteLength`, offsets, and counts in bytes. `HipModuleGlobal<T>` requires
the native byte extent to be divisible by `sizeof(T)` and measures `ElementCount`, offsets, and counts
in elements. Managed-array overloads always copy the complete array; pinned and device-memory
overloads take an explicit byte or element count. Every multiplication, addition, and pointer offset
is checked before native copy. A zero-length operation is valid at the end of a range and does not
call `hipMemcpy`.

`HipModuleGlobal` 的 `ByteLength`、offset 与 count 都以 bytes 为单位。`HipModuleGlobal<T>` 要求
native byte extent 可以被 `sizeof(T)` 整除，并以 elements 表示 `ElementCount`、offset 与 count。
托管数组 overload 总是复制整个数组；pinned/device memory overload 要求显式 byte 或 element count。
所有乘法、加法和 pointer offset 都在 native copy 前进行 checked 验证。range 末尾允许零长度操作，且
不会调用 `hipMemcpy`。

## Lifetime and streams / 生命周期与 Stream

Disposing the module immediately invalidates all its views. Reloading the same code object creates a
new module identity; an old same-name view never points to the new symbol. Synchronous copies finish
before returning. Asynchronous overloads require an explicit `HipStream`; the stream retains the
module, managed array pin, pinned owner, or device allocation as applicable until `Query`,
`Synchronize`, or stream disposal observes completion. A requested module or memory disposal is
therefore delayed for already submitted work, while new view operations fail immediately.

释放 module 会立即使其全部 view 失效。重新加载同一 code object 会产生新的 module identity，旧的同名
view 不会指向新 symbol。同步复制在返回前完成。异步 overload 必须接收显式 `HipStream`；stream 会保留
module、managed array pin、pinned owner 或 device allocation，直到 `Query`、`Synchronize` 或 stream
dispose 观察到完成。已提交工作可以延迟 owner 的 native 释放，但任何新的 view 操作会立即失败。

The stream and device allocation must belong to the same Runtime client and device as the module;
the module device must also be current. `HipPinnedMemory` and `HipDeviceMemory` overloads expose
controlled owners only, never arbitrary pointers. Native submission failure creates no pending
lease. Cleanup failure remains retryable and does not double-release owners.

stream 和 device allocation 必须与 module 属于同一 Runtime client 和 device，且 module device 必须
为 current。`HipPinnedMemory`/`HipDeviceMemory` overload 只接受受控 owner，不接受任意 pointer。native
提交失败不会创建伪 pending lease；cleanup failure 可重试且不会 double release。

## Deliberate boundaries / 明确保留边界

The implementation uses only `hipModuleGetGlobal` for identity and ordinary `hipMemcpy` variants for
copy. Runtime compiler-symbol APIs (`hipGetSymbolAddress`, `hipGetSymbolSize`, and
`hipMemcpy*Symbol*`) accept a compile-time `const void* symbol` and remain low-level-only. Module
texture/surface references and linker APIs also remain low-level. Module globals are not accepted as
kernel arguments in M8.6 because the existing pointer-owner contract cannot prove exact same-module
identity.

实现仅使用 `hipModuleGetGlobal` 查询 identity，并使用普通 `hipMemcpy` variants 复制。Runtime
compiler-symbol APIs（`hipGetSymbolAddress`、`hipGetSymbolSize` 和 `hipMemcpy*Symbol*`）接受编译期
`const void* symbol`，继续只保留低层。module texture/surface reference 与 linker API 也继续保留低层。
M8.6 不允许把 module global 作为 kernel argument，因为现有 pointer-owner contract 尚不能证明 exact
same-module identity。

Local tests prove managed validation, data direction, ownership, fake-native behavior, 15-TFM API
parity, documentation, and packaging. Real `hipModuleGetGlobal` export, Runtime behavior, symbol
contents, and GPU execution remain pending a separate Owner-authorized session.

本地测试证明 managed validation、数据方向、ownership、fake-native 行为、15 TFM API 一致性、文档与
打包。真实 `hipModuleGetGlobal` export、Runtime 行为、symbol 内容和 GPU execution 仍等待独立的 Owner
授权会话。
