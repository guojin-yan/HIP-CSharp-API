# HIPRTC Program 与 Linker：从源码到可加载 Kernel

## 前言

HIPRTC 让应用在运行时编译 HIP Kernel。对于简单的 VectorAdd，创建 Program、编译 code object、加载 Module 就够了；一旦涉及模板 Kernel、LLVM bitcode 或多个链接输入，还要处理 name expression、lowered name 和 Linker。

本文以 **HIP CSharp API** `0.10.0` 的 `HipRtcProgramLinker` 教程为入口，说明这一条完整链路。源码仓库为 <https://github.com/guojin-yan/HIP-CSharp-API>，案例目录为 `samples/tutorials/04-Kernel/HipRtcProgramLinker`。本文不把一次 Linux 运行结果扩大为 Windows 或其他 GPU 的支持声明。

## 一、Program 和 Linker 各做什么

| 对象 | 作用 | 输出或状态 |
| --- | --- | --- |
| `HipRtc` | 加载 HIPRTC 并创建编译/链接对象 | `HipRtcProgram`、`HipRtcLinker` |
| `HipRtcProgram` | 保存源码、注册 name expression、编译源码 | code object、LLVM bitcode、编译日志、lowered name |
| `HipRtcLinker` | 接收 bitcode 或文件输入并完成链接 | 最终 code object 的托管 `byte[]` |
| `HipModule` | 把 code object 加载到 HIP Runtime | `HipKernel` |

Program 负责“把源码变成可链接的产物”，Linker 负责“把链接输入变成最终 code object”。两者不是同一个生命周期。

## 二、从普通 Kernel 到模板 Kernel

最小的非模板路径是：

1. `rtc.CreateProgram(source, name)` 创建 Program；
2. `program.Compile(options)` 编译源码；
3. `compilation.GetCodeObject()` 取得托管 code object；
4. `runtime.LoadModule(codeObject)` 加载 Module；
5. `module.GetKernel("VectorAdd")` 取得 Kernel。

模板 Kernel 的入口名称不是编译前的普通字符串，因此需要先注册表达式：

```csharp
const string source = @"
template <typename T>
__global__ void VectorAddTemplate(const T* a, const T* b, T* c, int length)
{
    int index = (int)(blockIdx.x * blockDim.x + threadIdx.x);
    if (index < length) c[index] = a[index] + b[index];
}";

var rtc = new HipRtc();
using HipRtcProgram program = rtc.CreateProgram(source, "templated-vector-add.hip");

const string expression = "VectorAddTemplate<float>";
program.AddNameExpression(expression);
byte[] bitcode = program.CompileToBitcode(new[]
{
    "--offload-arch=gfx1100",
    "-fgpu-rdc",
    "-O2",
});

string loweredName = program.GetLoweredName(expression);
```

这里的顺序不能交换：`AddNameExpression` 必须在编译前调用；`GetLoweredName` 必须在成功编译后调用。

## 三、把 bitcode 交给 Linker

编译得到 bitcode 后，可以通过 `AddData` 或 `AddFile` 加入 Linker：

```csharp
byte[] codeObject;
using (HipRtcLinker linker = rtc.CreateLinker())
{
    linker.AddData(HipRtcJitInputType.LlvmBitcode, bitcode, "vector-add.bc");
    codeObject = linker.Complete();
}

using HipRuntime runtime = new HipRuntime();
runtime.Initialize();
using HipModule module = runtime.LoadModule(codeObject);
HipKernel kernel = module.GetKernel(loweredName);
```

`AddData` 会把输入复制到 Linker 自己管理的非托管内存中，调用方可以在返回后复用或释放原始 `byte[]`。`Complete` 会把 Linker 拥有的输出复制到新的托管数组；这个数组不依赖 Linker 的 native 状态。

## 四、Program 的生命周期和编译日志

`HipRtcProgram` 拥有 `hiprtcProgram`。编译成功时，`Compile` 返回的结果或 `CompileToBitcode` 返回的数组已经完成复制；Program 释放后，这些托管结果仍然可以继续交给 Module 或 Linker。

编译失败时，项目会读取 HIPRTC 的 program log，并通过 `HipRtcException` 暴露结果、操作和日志。可以用错误 Kernel 验证这条路径：

```bash
dotnet run --project samples/tutorials/04-Kernel/HipRtcVectorAdd/HipRtcVectorAdd.csproj \
  -c Release -- --arch gfx1100 --negative-compile
```

实际应用不要只记录一个错误码。编译选项、目标架构和 `CompilationLog` 通常比错误码更能说明 Kernel 为什么没有通过。

## 五、Linker 的生命周期约束

| 阶段 | 入口 | 托管约束 |
| --- | --- | --- |
| 注册 | `AddNameExpression` | 编译前可调用；编译成功后明确拒绝继续注册 |
| 编译 | `Compile` / `CompileToBitcode` | 返回托管结果；失败保留编译日志 |
| 降名 | `GetLoweredName` | 只允许在成功编译后读取，并立即复制字符串 |
| 输入 | `AddData` / `AddFile` | `AddData` 复制输入；Linker 在完成前保留副本 |
| 完成 | `Complete` | 复制 state-owned code object；成功后不再接受新输入 |
| 销毁 | `Dispose` | 释放 link state；显式释放失败可以重试 |

`Complete` 成功并不等于 Linker 可以被遗忘。Linker 仍然是一个需要释放的 native 对象，只是最终 `byte[]` 已经独立出来。

## 六、运行案例

在 Ubuntu 24.04、ROCm 7.2.1、`gfx1100` 的 Radeon Cloud 环境中运行：

```bash
dotnet run --project samples/tutorials/04-Kernel/HipRtcProgramLinker/HipRtcProgramLinker.csproj \
  --configuration Release -- gfx1100
```

教程的关键输出为：

```text
Linked code object bytes: 4088
```

这说明 Program 生成的 bitcode 已经被 Linker 接受并完成，程序复制到了托管数组。它不是性能基准，也不能单独证明模板 Kernel 的完整执行路径；需要执行 Kernel 和 CPU/GPU 对照时，应使用 `HipRtcVectorAdd` 或综合案例。

## 七、常见失败点

- 目标架构写错：把 `gfx1100` 替换成当前 `rocminfo` 输出的架构。
- 编译后再调用 `AddNameExpression`：托管层会在进入 native 之前拒绝。
- 编译失败后读取 lowered name：只有成功编译才有可用的 lowered name。
- Linker 输入文件不存在：`AddFile` 失败时应保留路径和 HIPRTC 错误信息。
- `Complete` 后继续添加或再次完成：这是状态错误，不是可以忽略的返回值。
- 混用不同 Runtime 闭包：HIP Runtime 与 HIPRTC 必须来自同一套用户态库。

## 八、验证边界

本文的命令和输出来自 Linux `gfx1100` GPU 验证。Windows 的构建和 AMD GPU 实机运行仍需单独验证；其他 ROCm 版本、GPU 架构和 Linker 输入类型也不能由这一次教程运行自动推出支持。

更细的 `AddFile`、负测、code object 哈希和完整 CPU/GPU 对照属于后续审核范围；本文先把 Program 到 Linker 的主路径讲清楚。

## 九、文章声明

- **开源协议：** 项目源码采用 Apache License 2.0；ROCm 组件保留各自许可证和通知。
- **AI 辅助开发：** 项目开发、测试和文档编写过程中使用了人工智能辅助，最终内容由维护者复核。
- **测试范围：** 输出来自 Ubuntu 24.04.4、ROCm 7.2.1、`.NET 10` 和 `gfx1100` Radeon Cloud；代码映射基于 `v0.10.0`。
- **平台限制：** Windows GPU 实机验证尚未完成，其他设备和 HIPRTC 版本需要重新验证。
- **供应商依赖：** AMD HIP/ROCm、驱动、HIPRTC 和 .NET 运行时受其自身版本和许可约束。
- **免责声明：** 生产环境使用前请自行完成编译失败、资源释放和输入完整性测试。
- **社区反馈：** 欢迎通过 <https://github.com/guojin-yan/HIP-CSharp-API/issues> 提交可公开的最小复现。

Copyright (c) 2026 Guojin Yan.
