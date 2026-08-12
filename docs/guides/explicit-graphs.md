# Explicit HIP graphs / 显式 HIP Graph

`HipRuntime.CreateGraph` creates a managed DAG without stream capture. Nodes are graph-owned borrowed identities: users compose them through typed APIs and never handle `hipGraphNode_t`, native parameter structures, dependency-pointer arrays, or graph allocation pointers.

`HipRuntime.CreateGraph` 无需 stream capture 即可创建托管 DAG。Node 是由 graph 拥有的 borrowed identity；用户只通过 typed API 组合节点，不接触 `hipGraphNode_t`、原生参数结构、依赖指针数组或 graph allocation pointer。

```csharp
using HipGraph graph = runtime.CreateGraph();
HipGraphNode clear = graph.AddMemset(output, value: 0);
HipGraphNode copy = graph.AddCopy(input, output, byteCount, new[] { clear });
HipGraphNode launch = graph.AddKernel(
    kernel,
    grid,
    block,
    arguments,
    new[] { copy });

using HipGraphExec executable = graph.Instantiate();
executable.Upload(stream);
executable.Launch(stream);
stream.Synchronize();
```

Dependencies point from a prerequisite to a dependent. `Nodes`, `RootNodes`, `Edges`, `Type`, and `Dependencies` return read-only managed snapshots. Duplicate edges, self edges, cycles, cross-graph identities, and mutation after the first successful `Instantiate` fail before native state changes. Captured graphs keep their existing instantiate/launch behavior but reject these explicit-builder and topology APIs.

Dependency 的方向是 prerequisite 到 dependent。`Nodes`、`RootNodes`、`Edges`、`Type` 和 `Dependencies` 返回只读托管快照。重复边、自依赖、环、跨 graph identity，以及首次成功 `Instantiate` 后的修改，都会在改变原生状态前失败。Captured graph 保持既有 instantiate/launch 行为，但拒绝显式 builder 和 topology API。

## Typed operation nodes / 类型化操作节点

`AddKernel` retains the module, device-memory arguments, scalar storage, and native argument-pointer array for the graph/executable lifetime. `AddCopy` supports checked one-dimensional device-to-device copies. `AddMemset` applies one byte pattern and treats a zero `byteCount` as the full destination allocation. All regular allocations must belong to the same Runtime client and device as the graph; stream-ordered allocations, arbitrary pointers, host pointers, peer copies, arrays, symbols, and 2D/3D graph operations remain low-level.

`AddKernel` 会在 graph/executable 生命周期内保留 module、device-memory 参数、scalar storage 和原生参数指针数组。`AddCopy` 提供经过容量检查的一维 device-to-device copy。`AddMemset` 写入单字节 pattern；`byteCount` 为零时处理整个目标 allocation。所有普通 allocation 必须与 graph 属于同一 Runtime client 和设备；stream-ordered allocation、任意指针、host pointer、peer copy、array、symbol 和 2D/3D graph 操作仍保留在低层 API。

## Graph-local memory / Graph 局部内存

`AddMemoryAllocation` returns `HipGraphMemory`, a graph-scoped reference rather than an independently disposable allocation. It can only be used by later kernel, copy, or memset nodes in the same graph. Every consumer must transitively depend on the allocation node. `AddMemoryFree` creates the one free DAG node, which must depend on every consumer, and each allocation must have a free node before instantiation.

`AddMemoryAllocation` 返回 graph-scoped `HipGraphMemory`，而不是可独立 `Dispose` 的普通 allocation。它只能被同一 graph 中更晚的 kernel、copy 或 memset node 使用。每个 consumer 必须传递依赖 allocation node。`AddMemoryFree` 创建唯一的 free DAG node；该节点必须依赖所有 consumer，而且每个 allocation 在实例化前都必须有对应 free node。

```csharp
HipGraphMemory scratch = graph.AddMemoryAllocation(byteCount, device);
HipGraphNode initialize = graph.AddMemset(
    scratch,
    value: 0,
    dependencies: new[] { scratch.AllocationNode });
HipGraphNode useScratch = graph.AddKernel(
    kernel,
    grid,
    block,
    new[] { HipKernelArgument.DevicePointer(scratch) },
    new[] { initialize });
graph.AddMemoryFree(scratch, new[] { useScratch });
```

The graph-local pointer is never public and must not be used on an arbitrary stream. Each executable launch runs allocation, consumers, and free in DAG order, including repeated launches. Free is a node, not owner disposal.

Graph-local pointer 永不公开，也不能在任意 stream 上使用。每次 executable launch 都按 DAG 顺序执行 allocation、consumer 和 free，重复 launch 亦如此。Free 是 node，不是 owner disposal。

## Upload, launch, and updates / Upload、launch 与更新

`Upload` prepares an executable on a same-device stream. `Launch` retains the executable and all graph resources until stream completion. The source graph may be disposed after instantiation; a live executable remains launchable. Dispose/operation races are unsupported and callers must coordinate them.

`Upload` 在同设备 stream 上准备 executable。`Launch` 会保留 executable 和全部 graph resources，直到 stream 完成。实例化后可以先释放源 graph；仍存活的 executable 依然可以 launch。Dispose 与操作之间的并发竞态不受支持，调用方必须自行协调。

An explicit executable can update its kernel, one-dimensional copy, or memset node with `UpdateKernel`, `UpdateCopy`, and `UpdateMemset`. The node must belong to the graph that created the executable and have the matching type. Updates are rejected while a launch is pending. Native update failure releases the candidate resources and leaves the previous executable parameters usable. Whole-graph `hipGraphExecUpdate`, node removal, native topology queries, and raw node parameter mutation remain low-level because their identity and resource effects cannot be represented safely by this managed DAG.

显式 executable 可通过 `UpdateKernel`、`UpdateCopy` 和 `UpdateMemset` 更新 kernel、一维 copy 或 memset node。Node 必须来自创建该 executable 的 graph，且类型匹配。存在 pending launch 时拒绝更新。原生更新失败会释放候选资源，并保留 executable 的旧参数继续可用。Whole-graph `hipGraphExecUpdate`、node removal、原生 topology query 和 raw node parameter mutation 仍保留在低层 API，因为其 identity 与 resource 影响无法由此托管 DAG 安全表达。

All added graph exports are optional. A missing symbol becomes `HipException` with `HipError.NotSupported`; no capture-based approximation is attempted. Managed and fake-native validation does not prove installed ROCm symbols or GPU execution, which require a separately authorized runtime environment.

所有新增 Graph 导出均为 optional。缺少 symbol 时抛出带 `HipError.NotSupported` 的 `HipException`，不会退化为 capture 近似实现。托管与 fake-native 验证不能证明已安装 ROCm symbol 或真实 GPU 执行；这些证据需要另行授权的 Runtime 环境。
