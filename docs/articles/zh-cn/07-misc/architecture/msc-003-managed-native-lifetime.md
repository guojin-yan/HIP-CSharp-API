# Native Interop、托管对象和 SafeHandle 生命周期

## 前言

GPU API 最容易被低估的部分不是函数调用，而是“什么时候可以释放”。CPU 代码里，一个对象离开作用域通常就能回收；GPU 工作提交到 Stream 后，CPU 线程可能已经继续往下走，设备内存、Pinned Memory、Module 或 Kernel 仍然被异步工作引用。

本文基于 **HIP CSharp API** `0.10.0`，解释托管 owner、SafeHandle 和异步引用如何一起工作。源码仓库为 <https://github.com/guojin-yan/HIP-CSharp-API>；示例入口是 `samples/tutorials/04-Kernel/HipRtcVectorAdd` 和 `samples/showcases/HeatDiffusion`。

## 一、`Dispose` 不是把指针置零

一个简单但危险的写法是：提交异步复制后立刻释放源数组、设备内存或 Stream。native 函数可能已经返回，但 GPU 还没有读完这些地址。

项目把这个问题拆成两个层次：

- **所有权：** 谁负责调用 HIP 的 destroy/free 函数。
- **使用期：** 在 Stream 或 Graph 完成前，哪些 owner 必须继续保持有效。

`SafeHandle` 解决的是“原生句柄最终由谁释放”；托管 owner 解决的是“哪些对象要一起存活”和“释放后如何在托管层报错”。两者不是替代关系。

## 二、当前对象的所有权关系

| 托管对象 | 负责的 native 资源 | 借用对象的规则 | 异步工作中的要求 |
| --- | --- | --- | --- |
| `HipRuntime` | Runtime 客户端和设备操作入口 | `HipDevice` 不单独销毁 Runtime | Runtime 必须晚于其创建的资源释放 |
| `HipDeviceMemory` | `hipMalloc` 得到的设备内存 | `DangerousGetHandle` 返回借用指针，调用方不能 free | 异步复制或 Kernel 完成前保持 owner 存活 |
| `HipPinnedMemory` | Pinned host allocation | 指针视图不拥有分配 | Stream 完成前不能释放 host buffer |
| `HipStream` | HIP Stream | Event 记录在 Stream 上，不拥有 Stream | Stream 必须晚于排队的工作完成 |
| `HipEvent` | HIP Event | 只记录和查询事件 | 事件查询不能替代资源同步 |
| `HipModule` | 已加载的 code object | `HipKernel` 借用 Module 中的函数句柄 | Kernel 使用期间 Module 必须有效 |
| `HipRtcProgram` | HIPRTC program | lowered name 从 native 指针复制成 `string` | 编译或读取结果期间不能销毁 Program |
| `HipRtcLinker` | HIPRTC link state 和保留输入 | `Complete` 返回独立 `byte[]` | 完成或失败后仍需释放 link state |

对象各自负责自己的 native 资源，但不会因为一个对象被 `Dispose` 就自动替调用方销毁所有相关对象。调用方仍要按依赖关系安排作用域。

## 三、异步复制为什么需要保留引用

以设备内存和 Stream 为例，调用关系大致是：

```csharp
using var runtime = new HipRuntime();
runtime.Initialize();

using var stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
using var device = runtime.Allocate(1024 * sizeof(float));

byte[] input = new byte[1024 * sizeof(float)];
device.CopyFromAsync(input, stream);

stream.Synchronize();
```

`CopyFromAsync` 返回时，`input` 可能还在被 native 异步复制使用。项目的托管实现会在排队期间记录异步引用，等 Stream 同步或对应工作完成后再释放这些引用。调用方仍然应当保持 `stream` 和 `device` 在同步点之前有效；不要把“库暂时保留引用”理解成可以随意释放 owner。

这条规则同样适用于 Kernel 参数：设备指针、Module 和涉及 Pinned Memory 的参数都必须覆盖 Kernel 的执行期。

## 四、Stream、Event 与释放顺序

Stream 和 Event 的常见使用顺序是：

1. 创建 Runtime、Stream、Event 和设备内存。
2. 把复制或 Kernel 工作排入 Stream。
3. 在同一 Stream 上记录 Event，或调用 `Synchronize`。
4. 确认工作完成后释放内存、Event、Stream，最后释放 Runtime。

Event 只表示一个时间点或完成点，不会自动延长其他 owner 的作用域。下面的输出来自仓库中的 Stream/Event 生命周期案例：

```text
Device clock (kHz): 1760000
Expected invalid-device-ordinal error captured.
stream/event VectorAdd passed; lengths=1,127,256,1000,1048576; lifecycleRepeats=100; repeatedDispose=true; expectedError=True; failureIndex=-1
```

`repeatedDispose=true` 说明重复释放路径符合案例预期；`expectedError=True` 说明预设的无效设备序号错误被捕获，而不是程序无条件忽略异常。

## 五、SafeHandle 能解决什么，不能解决什么

项目中的 `HipDeviceMemoryHandle`、`HipStreamHandle`、`HipEventHandle`、`HipModuleHandle` 和 HIPRTC handle 类型负责把 native destroy/free 操作集中到一个释放入口。这样做有几个直接好处：

- 显式 `Dispose` 和最终器走同一套句柄关闭逻辑；
- 句柄关闭后再次使用会在托管层先失败；
- native 释放失败可以保留为显式释放路径中的 `HipException`；
- 借用指针不会被错误地包装成拥有型句柄。

但 SafeHandle 不知道 GPU 队列的业务依赖。它不会自动判断某个 Kernel 是否还在使用 Module，也不会替调用方协调两个线程之间的提交和释放。项目的公开约束仍然是：不要让一个线程释放 owner 的同时，另一个线程继续通过它提交工作。

## 六、异常边界

不同失败发生在不同层次：

| 失败 | 托管层表现 |
| --- | --- |
| 传入空数组、负尺寸或超出分配范围 | `ArgumentNullException`、`ArgumentOutOfRangeException` 或 `ArgumentException` |
| 已释放对象继续使用 | `ObjectDisposedException` |
| 两个 Runtime 客户端的资源混用 | `ArgumentException` 或无效操作异常 |
| HIP Runtime 返回错误码 | `HipException`，保留错误码、名称和操作上下文 |
| HIPRTC 编译失败 | `HipRtcException`，尽量保留编译日志 |
| native 释放失败 | 显式 `Dispose` 返回可观察错误，最终器不抛出异常 |

这类边界的目的不是把所有 native 错误改写成普通 .NET 异常，而是让调用方知道失败发生在参数、生命周期、Runtime 还是编译阶段。

## 七、建议的作用域写法

对一个有设备、Stream 和 Module 的案例，建议让作用域反映依赖关系：

```csharp
using var runtime = new HipRuntime();
runtime.Initialize();
using var module = runtime.LoadModule(codeObject);
using var stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
byte[] inputBytes = new byte[1024 * sizeof(float)];
byte[] outputBytes = new byte[inputBytes.Length];
using var input = runtime.Allocate((ulong)inputBytes.Length);
using var output = runtime.Allocate((ulong)outputBytes.Length);

input.CopyFromAsync(inputBytes, stream);
HipKernel kernel = module.GetKernel("VectorAdd");
var arguments = new[]
{
    HipKernelArgument.DevicePointer(input),
    HipKernelArgument.DevicePointer(input),
    HipKernelArgument.DevicePointer(output),
    HipKernelArgument.Scalar32(1024),
};
kernel.Launch(stream, new HipLaunchDimensions(4), new HipLaunchDimensions(256), arguments);
output.CopyToAsync(outputBytes, stream);
stream.Synchronize();
```

示例只展示顺序，不代表每个 Kernel 都使用相同的重载。关键点是：`Synchronize` 之前不要释放被排队工作引用的资源，Module 的作用域要覆盖 Kernel 启动，Runtime 要覆盖所有子资源。

## 八、验证与边界

可从仓库根目录运行 Stream/Event 教程或 `HeatDiffusion` 的 `tiny` profile。教程案例适合验证资源契约；综合案例会进一步验证 CPU/GPU 结果和文件产物。

这些案例证明的是指定环境中的生命周期和结果路径，不是任意线程模型下的并发安全保证。当前项目不承诺通用的“操作与 Dispose 可并发竞态”，也不承诺没有同步点就能安全复用托管数组。

## 九、文章声明

- **开源协议：** 项目源码采用 Apache License 2.0；ROCm 组件保留各自许可证和通知。
- **AI 辅助开发：** 项目开发、测试和文档编写过程中使用了人工智能辅助，最终内容由维护者复核。
- **测试范围：** 输出示例来自 Ubuntu 24.04、ROCm 7.2.1、`gfx1100` 的 Radeon Cloud 验证；代码规则来自 `v0.10.0` 源码和测试。
- **平台限制：** Windows AMD GPU 实机验证尚未完成，其他设备和 Runtime 需要重新验证。
- **供应商依赖：** AMD HIP/ROCm、驱动和 .NET 运行时受其自身版本和许可约束。
- **免责声明：** 商业或关键任务环境使用前，请自行完成压力、故障恢复和资源审计。
- **社区反馈：** 欢迎通过 <https://github.com/guojin-yan/HIP-CSharp-API/issues> 提交可公开的最小复现。

Copyright (c) 2026 Guojin Yan.
