using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>拥有 captured 或 explicit HIP graph / Owns a captured or explicit HIP graph.</summary>
public sealed class HipGraph : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphHandle _handle;
    private readonly HipGraphResources _resources;
    private readonly object _lifetimeSync = new();
    private readonly List<HipGraphNode> _nodes = new();
    private readonly Dictionary<HipGraphNode, List<HipGraphNode>> _dependencies = new();
    private readonly List<HipGraphEdge> _edges = new();
    private readonly Dictionary<HipGraphMemory, GraphMemoryState> _graphMemory = new();
    private bool _sealed;
    private bool _disposeRequested;

    internal HipGraph(IHipNativeApi nativeApi, IntPtr handle, HipGraphResources resources, HipGraphKind kind = HipGraphKind.Captured, int deviceOrdinal = -1)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("A HIP graph handle cannot be null.", nameof(handle));
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _resources = resources ?? throw new ArgumentNullException(nameof(resources));
        _handle = new HipGraphHandle(nativeApi, handle, resources);
        Kind = kind;
        DeviceOrdinal = deviceOrdinal;
    }

    /// <summary>获取 graph 的创建方式 / Gets how the graph was created.</summary>
    public HipGraphKind Kind { get; }

    /// <summary>获取 explicit graph 绑定的设备序号；captured graph 返回其 capture stream 设备 / Gets the graph device ordinal.</summary>
    public int DeviceOrdinal { get; }

    /// <summary>获取 graph 是否已释放 / Gets whether the graph is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>获取 explicit graph 的 node 快照 / Gets a node snapshot for an explicit graph.</summary>
    public IReadOnlyList<HipGraphNode> Nodes { get { lock (_lifetimeSync) { ValidateExplicit(); return Snapshot(_nodes); } } }

    /// <summary>获取 explicit graph 的 root node 快照 / Gets a root-node snapshot for an explicit graph.</summary>
    public IReadOnlyList<HipGraphNode> RootNodes
    {
        get
        {
            lock (_lifetimeSync)
            {
                ValidateExplicit();
                var roots = new List<HipGraphNode>();
                foreach (HipGraphNode node in _nodes) if (_dependencies[node].Count == 0) roots.Add(node);
                return Snapshot(roots);
            }
        }
    }

    /// <summary>获取 explicit graph 的 directed edge 快照 / Gets a directed-edge snapshot for an explicit graph.</summary>
    public IReadOnlyList<HipGraphEdge> Edges { get { lock (_lifetimeSync) { ValidateExplicit(); return Snapshot(_edges); } } }

    /// <summary>添加 empty no-op node / Adds an empty no-op node.</summary>
    public HipGraphNode AddEmpty(IReadOnlyList<HipGraphNode>? dependencies = null)
    {
        lock (_lifetimeSync)
        {
            List<HipGraphNode> validated = ValidateDependencies(dependencies);
            IntPtr node = IntPtr.Zero;
            HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddEmptyNode(out node, Handle, pointer, count));
            return CompleteNodeAdd(error, node, HipGraphNodeType.Empty, validated, "hipGraphAddEmptyNode");
        }
    }

    /// <summary>添加 prerequisite 到 dependent 的 dependency / Adds a dependency from prerequisite to dependent.</summary>
    public void AddDependency(HipGraphNode prerequisite, HipGraphNode dependent)
    {
        lock (_lifetimeSync)
        {
            ValidateMutable();
            ValidateNode(prerequisite, nameof(prerequisite));
            ValidateNode(dependent, nameof(dependent));
            if (ReferenceEquals(prerequisite, dependent)) throw new ArgumentException("A node cannot depend on itself.", nameof(dependent));
            if (_dependencies[dependent].Contains(prerequisite)) throw new ArgumentException("The dependency already exists.", nameof(prerequisite));
            if (DependsOn(prerequisite, dependent)) throw new ArgumentException("The dependency would create a cycle.", nameof(prerequisite));
            InvokeDependencyPair(prerequisite, dependent, add: true);
            _dependencies[dependent].Add(prerequisite);
            _edges.Add(new HipGraphEdge(prerequisite, dependent));
        }
    }

    /// <summary>移除 prerequisite 到 dependent 的 dependency / Removes a dependency from prerequisite to dependent.</summary>
    public void RemoveDependency(HipGraphNode prerequisite, HipGraphNode dependent)
    {
        lock (_lifetimeSync)
        {
            ValidateMutable();
            ValidateNode(prerequisite, nameof(prerequisite));
            ValidateNode(dependent, nameof(dependent));
            if (!_dependencies[dependent].Remove(prerequisite)) throw new ArgumentException("The dependency does not exist.", nameof(prerequisite));
            try { ValidateAllGraphMemoryOrdering(); }
            catch { _dependencies[dependent].Add(prerequisite); throw; }
            try { InvokeDependencyPair(prerequisite, dependent, add: false); }
            catch { _dependencies[dependent].Add(prerequisite); throw; }
            _edges.Remove(new HipGraphEdge(prerequisite, dependent));
        }
    }

    /// <summary>添加 typed kernel node / Adds a typed kernel node.</summary>
    public HipGraphNode AddKernel(HipKernel kernel, HipLaunchDimensions grid, HipLaunchDimensions block, IReadOnlyList<HipKernelArgument> arguments, IReadOnlyList<HipGraphNode>? dependencies = null, uint sharedMemoryBytes = 0)
    {
        lock (_lifetimeSync)
        {
            List<HipGraphNode> validated = ValidateDependencies(dependencies);
            ValidateGraphMemoryArguments(arguments, validated);
            var snapshot = new HipGraphKernelSnapshot(this, kernel, grid, block, arguments, sharedMemoryBytes);
            try
            {
                IntPtr node = IntPtr.Zero;
                using var buffer = new HipGraphStructBuffer<HipKernelNodeParameters>(snapshot.Parameters);
                HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddKernelNode(out node, Handle, pointer, count, buffer.Pointer));
                HipGraphNode result = CompleteNodeAdd(error, node, HipGraphNodeType.Kernel, validated, "hipGraphAddKernelNode");
                _resources.Add(snapshot);
                RegisterGraphMemoryConsumers(arguments, result);
                snapshot = null!;
                return result;
            }
            finally { snapshot?.Dispose(); }
        }
    }

    /// <summary>添加一维 device-to-device copy node / Adds a one-dimensional device-to-device copy node.</summary>
    public HipGraphNode AddCopy(HipDeviceMemory source, HipDeviceMemory destination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddCopyOperands(() => CreateDeviceOperand(source, nameof(source)), () => CreateDeviceOperand(destination, nameof(destination)), byteCount, dependencies);

    /// <summary>从普通 device memory 复制到 graph-local memory / Copies from regular device memory to graph-local memory.</summary>
    public HipGraphNode AddCopy(HipDeviceMemory source, HipGraphMemory destination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddCopyOperands(() => CreateDeviceOperand(source, nameof(source)), () => CreateGraphOperand(destination, nameof(destination)), byteCount, dependencies);

    /// <summary>从 graph-local memory 复制到普通 device memory / Copies from graph-local memory to regular device memory.</summary>
    public HipGraphNode AddCopy(HipGraphMemory source, HipDeviceMemory destination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddCopyOperands(() => CreateGraphOperand(source, nameof(source)), () => CreateDeviceOperand(destination, nameof(destination)), byteCount, dependencies);

    /// <summary>在两个 graph-local memory references 之间复制 / Copies between two graph-local memory references.</summary>
    public HipGraphNode AddCopy(HipGraphMemory source, HipGraphMemory destination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddCopyOperands(() => CreateGraphOperand(source, nameof(source)), () => CreateGraphOperand(destination, nameof(destination)), byteCount, dependencies);

    /// <summary>添加 byte-pattern memset node；byteCount 为零时填充整个 allocation / Adds a byte-pattern memset node; zero byteCount fills the allocation.</summary>
    public HipGraphNode AddMemset(HipDeviceMemory destination, int value, ulong byteCount = 0, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddMemsetCore(CreateDeviceOperand(destination, nameof(destination)), value, byteCount, dependencies);

    /// <summary>添加 graph-local byte-pattern memset node / Adds a graph-local byte-pattern memset node.</summary>
    public HipGraphNode AddMemset(HipGraphMemory destination, int value, ulong byteCount = 0, IReadOnlyList<HipGraphNode>? dependencies = null) =>
        AddMemsetCore(CreateGraphOperand(destination, nameof(destination)), value, byteCount, dependencies);

    /// <summary>添加 graph-local allocation node / Adds a graph-local allocation node.</summary>
    public HipGraphMemory AddMemoryAllocation(ulong byteCount, HipDevice device, IReadOnlyList<HipMemoryPoolAccessDescriptor>? access = null, IReadOnlyList<HipGraphNode>? dependencies = null)
    {
        if (device is null) throw new ArgumentNullException(nameof(device));
        lock (_lifetimeSync)
        {
            ValidateMutable();
            if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
            if (!ReferenceEquals(_nativeApi, device.NativeApi)) throw new ArgumentException("Device belongs to a different HIP Runtime client.", nameof(device));
            if (device.Ordinal != DeviceOrdinal) throw new ArgumentException("Device differs from the graph device.", nameof(device));
            UIntPtr nativeBytes = HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
            List<HipGraphNode> validated = ValidateDependencies(dependencies);
            IReadOnlyList<HipMemoryPoolAccessDescriptor> effectiveAccess = access ?? new[] { new HipMemoryPoolAccessDescriptor(device, HipMemoryPoolAccess.ReadWrite) };
            IntPtr descriptors = MarshalAccessDescriptors(effectiveAccess);
            try
            {
                var parameters = new HipMemoryAllocationNodeParameters
                {
                    PoolProperties = HipMemoryPoolPropertiesNative.ForDevice(device.Ordinal, UIntPtr.Zero),
                    AccessDescriptors = descriptors,
                    AccessDescriptorCount = ToUIntPtr(effectiveAccess.Count),
                    ByteCount = nativeBytes,
                };
                IntPtr buffer = Marshal.AllocHGlobal(Marshal.SizeOf<HipMemoryAllocationNodeParameters>());
                try
                {
                    Marshal.StructureToPtr(parameters, buffer, false);
                    IntPtr node = IntPtr.Zero;
                    HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddMemAllocNode(out node, Handle, pointer, count, buffer));
                    parameters = Marshal.PtrToStructure<HipMemoryAllocationNodeParameters>(buffer);
                    if (error != HipError.Success)
                    {
                        if (node != IntPtr.Zero) _nativeApi.GraphDestroyNode(node);
                        HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphAddMemAllocNode");
                    }
                    if (node == IntPtr.Zero) throw new InvalidOperationException("hipGraphAddMemAllocNode succeeded but returned a null node.");
                    if (parameters.DevicePointer == IntPtr.Zero)
                    {
                        _nativeApi.GraphDestroyNode(node);
                        throw new InvalidOperationException("hipGraphAddMemAllocNode succeeded but returned a null graph-local pointer.");
                    }
                    HipGraphNode allocationNode = CompleteNodeAdd(HipError.Success, node, HipGraphNodeType.MemoryAllocation, validated, "hipGraphAddMemAllocNode");
                    var memory = new HipGraphMemory(this, allocationNode, parameters.DevicePointer, byteCount, device);
                    _graphMemory.Add(memory, new GraphMemoryState(memory));
                    return memory;
                }
                finally { Marshal.FreeHGlobal(buffer); }
            }
            finally { if (descriptors != IntPtr.Zero) Marshal.FreeHGlobal(descriptors); }
        }
    }

    /// <summary>添加 graph-local free node；free 是 DAG node，不是 memory Dispose / Adds a graph-local free node; free is a DAG node, not memory disposal.</summary>
    public HipGraphNode AddMemoryFree(HipGraphMemory memory, IReadOnlyList<HipGraphNode>? dependencies = null)
    {
        lock (_lifetimeSync)
        {
            ValidateMutable();
            GraphMemoryState state = GetGraphMemoryState(memory, nameof(memory));
            if (state.FreeNode is not null) throw new InvalidOperationException("Graph-local memory already has a free node.");
            IReadOnlyList<HipGraphNode> requested = dependencies ?? (state.Consumers.Count == 0 ? new[] { memory.AllocationNode } : state.Consumers);
            List<HipGraphNode> validated = ValidateDependencies(requested);
            foreach (HipGraphNode consumer in state.Consumers)
            {
                if (!ProspectiveNodeDependsOn(validated, consumer)) throw new ArgumentException("The free node must depend on every graph-local memory consumer.", nameof(dependencies));
            }
            if (!ProspectiveNodeDependsOn(validated, memory.AllocationNode)) throw new ArgumentException("The free node must depend on the allocation node.", nameof(dependencies));
            IntPtr node = IntPtr.Zero;
            HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddMemFreeNode(out node, Handle, pointer, count, memory.Pointer));
            HipGraphNode result = CompleteNodeAdd(error, node, HipGraphNodeType.MemoryFree, validated, "hipGraphAddMemFreeNode");
            state.FreeNode = result;
            return result;
        }
    }

    /// <summary>创建 graph executable；graph 与 executable 是独立 owner / Creates a graph executable; graph and executable are independent owners.</summary>
    public HipGraphExec Instantiate(ulong flags = 0)
    {
        if (flags != 0) throw new ArgumentOutOfRangeException(nameof(flags));
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            if (Kind == HipGraphKind.Explicit)
            {
                foreach (GraphMemoryState state in _graphMemory.Values)
                    if (state.FreeNode is null) throw new InvalidOperationException("Every graph-local allocation must have exactly one free node before instantiation.");
                ValidateAllGraphMemoryOrdering();
            }
            IDisposable? resourceReference = _handle.AcquireResourceReference();
            try
            {
                HipError error = _nativeApi.GraphInstantiateWithFlags(out IntPtr executable, Handle, flags);
                if (error != HipError.Success && executable != IntPtr.Zero)
                {
                    var partialHandle = new HipGraphExecHandle(_nativeApi, executable);
                    if (partialHandle.ReleaseChecked() == HipError.Success) partialHandle.Dispose();
                }
                HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphInstantiateWithFlags");
                if (executable == IntPtr.Zero) throw new InvalidOperationException("hipGraphInstantiateWithFlags succeeded but returned a null executable.");
                var result = new HipGraphExec(_nativeApi, executable, resourceReference, Kind == HipGraphKind.Explicit ? this : null, DeviceOrdinal);
                resourceReference = null;
                if (Kind == HipGraphKind.Explicit) _sealed = true;
                return result;
            }
            finally { resourceReference?.Dispose(); }
        }
    }

    /// <summary>释放 graph；重复调用安全 / Disposes the graph; repeated calls are safe.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed) return;
            _disposeRequested = true;
            HipError error = _handle.ReleaseChecked();
            if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphDestroy");
            _handle.Dispose();
        }
    }

    internal IHipNativeApi NativeApi => _nativeApi;

    internal IReadOnlyList<HipGraphNode> GetDependencies(HipGraphNode node)
    {
        lock (_lifetimeSync)
        {
            ValidateExplicit();
            ValidateNode(node, nameof(node));
            return Snapshot(_dependencies[node]);
        }
    }

    internal void ValidateGraphMemoryForConsumer(HipGraphMemory memory, object argumentName)
    {
        if (memory is null) throw new ArgumentNullException(nameof(memory));
        if (!ReferenceEquals(memory.Graph, this)) throw new ArgumentException("Graph-local memory belongs to a different graph.", argumentName.ToString());
        if (!_graphMemory.TryGetValue(memory, out GraphMemoryState? state)) throw new ArgumentException("Graph-local memory is not registered by this graph.", argumentName.ToString());
        if (state.FreeNode is not null) throw new InvalidOperationException("Cannot add a consumer after the graph-local memory free node.");
    }

    internal void ValidateExecNode(HipGraphNode node, HipGraphNodeType type, string parameterName)
    {
        lock (_lifetimeSync)
        {
            ValidateExplicit();
            ValidateNode(node, parameterName);
            if (node.Type != type) throw new ArgumentException("Node type does not match the requested executable update.", parameterName);
            if (!_sealed) throw new InvalidOperationException("The graph has not been instantiated.");
        }
    }

    internal void ValidateGraphMemoryForExecConsumer(HipGraphMemory memory, HipGraphNode node, object argumentName)
    {
        lock (_lifetimeSync)
        {
            ValidateExecNode(node, node.Type, nameof(node));
            GraphMemoryState state = GetGraphMemoryState(memory, argumentName.ToString() ?? nameof(memory));
            if (!DependsOn(node, memory.AllocationNode)) throw new ArgumentException("The updated node does not depend on this graph-local allocation.", argumentName.ToString());
            if (state.FreeNode is null || !DependsOn(state.FreeNode, node)) throw new ArgumentException("The graph-local free node does not depend on the updated node.", argumentName.ToString());
        }
    }

    private HipGraphNode AddCopyCore(HipGraphMemoryOperand source, HipGraphMemoryOperand destination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies)
    {
        lock (_lifetimeSync)
        {
            IDisposable? sourceLease = source.Lease;
            IDisposable? destinationLease = destination.Lease;
            try
            {
                List<HipGraphNode> validated = ValidateDependencies(dependencies);
                ValidateOperationBytes(byteCount, source.ByteLength, destination.ByteLength);
                ValidateOperandOrdering(source, validated);
                ValidateOperandOrdering(destination, validated);
                IntPtr node = IntPtr.Zero;
                HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddMemcpyNode1D(out node, Handle, pointer, count, destination.Pointer, source.Pointer, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), HipMemoryCopyKind.DeviceToDevice));
                HipGraphNode result = CompleteNodeAdd(error, node, HipGraphNodeType.MemoryCopy, validated, "hipGraphAddMemcpyNode1D");
                if (sourceLease is not null) { _resources.Add(sourceLease); sourceLease = null; }
                if (destinationLease is not null) { _resources.Add(destinationLease); destinationLease = null; }
                RegisterOperandConsumer(source, result);
                RegisterOperandConsumer(destination, result);
                return result;
            }
            finally { sourceLease?.Dispose(); destinationLease?.Dispose(); }
        }
    }

    private HipGraphNode AddCopyOperands(Func<HipGraphMemoryOperand> createSource, Func<HipGraphMemoryOperand> createDestination, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies)
    {
        HipGraphMemoryOperand source = createSource();
        try
        {
            HipGraphMemoryOperand destination = createDestination();
            return AddCopyCore(source, destination, byteCount, dependencies);
        }
        catch
        {
            source.Lease?.Dispose();
            throw;
        }
    }

    private HipGraphNode AddMemsetCore(HipGraphMemoryOperand destination, int value, ulong byteCount, IReadOnlyList<HipGraphNode>? dependencies)
    {
        lock (_lifetimeSync)
        {
            IDisposable? lease = destination.Lease;
            try
            {
                if (value < byte.MinValue || value > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
                ulong actualBytes = byteCount == 0 ? destination.ByteLength : byteCount;
                ValidateOperationBytes(actualBytes, destination.ByteLength, destination.ByteLength);
                List<HipGraphNode> validated = ValidateDependencies(dependencies);
                ValidateOperandOrdering(destination, validated);
                var parameters = new HipMemsetNodeParameters
                {
                    Destination = destination.Pointer,
                    ElementSize = 1,
                    Height = new UIntPtr(1),
                    Pitch = UIntPtr.Zero,
                    Value = (uint)value,
                    Width = HipDeviceMemory.ToUIntPtr(actualBytes, nameof(byteCount)),
                };
                IntPtr node = IntPtr.Zero;
                using var buffer = new HipGraphStructBuffer<HipMemsetNodeParameters>(parameters);
                HipError error = WithDependencyHandles(validated, (pointer, count) => _nativeApi.GraphAddMemsetNode(out node, Handle, pointer, count, buffer.Pointer));
                HipGraphNode result = CompleteNodeAdd(error, node, HipGraphNodeType.MemorySet, validated, "hipGraphAddMemsetNode");
                if (lease is not null) { _resources.Add(lease); lease = null; }
                RegisterOperandConsumer(destination, result);
                return result;
            }
            finally { lease?.Dispose(); }
        }
    }

    private HipGraphMemoryOperand CreateDeviceOperand(HipDeviceMemory memory, string parameterName)
    {
        lock (_lifetimeSync)
        {
            if (memory is null) throw new ArgumentNullException(parameterName);
            ValidateMutable();
            if (!ReferenceEquals(_nativeApi, memory.NativeApi)) throw new ArgumentException("Memory belongs to a different HIP Runtime client.", parameterName);
            if (memory.DeviceOrdinal != DeviceOrdinal) throw new ArgumentException("Memory differs from the graph device.", parameterName);
            var lease = new HipGraphPointerLease((IHipPointerOwner)memory);
            return new HipGraphMemoryOperand(lease.Pointer, memory.ByteLength, lease, null);
        }
    }

    private HipGraphMemoryOperand CreateGraphOperand(HipGraphMemory memory, string parameterName)
    {
        lock (_lifetimeSync)
        {
            ValidateMutable();
            ValidateGraphMemoryForConsumer(memory, parameterName);
            return new HipGraphMemoryOperand(memory.Pointer, memory.ByteLength, null, memory);
        }
    }

    private void ValidateOperandOrdering(HipGraphMemoryOperand operand, List<HipGraphNode> dependencies)
    {
        if (operand.GraphMemory is not null && !ProspectiveNodeDependsOn(dependencies, operand.GraphMemory.AllocationNode))
            throw new ArgumentException("A graph-local memory consumer must depend on its allocation node.", nameof(dependencies));
    }

    private void RegisterOperandConsumer(HipGraphMemoryOperand operand, HipGraphNode node)
    {
        if (operand.GraphMemory is not null) _graphMemory[operand.GraphMemory].AddConsumer(node);
    }

    private void ValidateGraphMemoryArguments(IReadOnlyList<HipKernelArgument> arguments, List<HipGraphNode> dependencies)
    {
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        for (int index = 0; index < arguments.Count; index++)
        {
            HipKernelArgument argument = arguments[index] ?? throw new ArgumentNullException(nameof(arguments), "Kernel arguments cannot contain null elements.");
            if (argument.GraphMemory is null) continue;
            ValidateGraphMemoryForConsumer(argument.GraphMemory, nameof(arguments));
            if (!ProspectiveNodeDependsOn(dependencies, argument.GraphMemory.AllocationNode))
                throw new ArgumentException("A kernel using graph-local memory must depend on its allocation node.", nameof(dependencies));
        }
    }

    private void RegisterGraphMemoryConsumers(IReadOnlyList<HipKernelArgument> arguments, HipGraphNode node)
    {
        foreach (HipKernelArgument argument in arguments)
            if (argument.GraphMemory is not null) _graphMemory[argument.GraphMemory].AddConsumer(node);
    }

    private List<HipGraphNode> ValidateDependencies(IReadOnlyList<HipGraphNode>? dependencies)
    {
        ValidateMutable();
        var result = new List<HipGraphNode>(dependencies?.Count ?? 0);
        if (dependencies is null) return result;
        foreach (HipGraphNode? dependency in dependencies)
        {
            if (dependency is null) throw new ArgumentNullException(nameof(dependencies), "Dependencies cannot contain null elements.");
            ValidateNode(dependency, nameof(dependencies));
            if (result.Contains(dependency)) throw new ArgumentException("Dependencies cannot contain duplicates.", nameof(dependencies));
            result.Add(dependency);
        }
        return result;
    }

    private HipGraphNode CompleteNodeAdd(HipError error, IntPtr nativeNode, HipGraphNodeType type, List<HipGraphNode> dependencies, string operation)
    {
        if (error != HipError.Success && nativeNode != IntPtr.Zero) _nativeApi.GraphDestroyNode(nativeNode);
        HipCall.ThrowIfFailed(_nativeApi, error, operation);
        if (nativeNode == IntPtr.Zero) throw new InvalidOperationException(operation + " succeeded but returned a null node.");
        var node = new HipGraphNode(this, nativeNode, type);
        _nodes.Add(node);
        _dependencies.Add(node, new List<HipGraphNode>(dependencies));
        foreach (HipGraphNode dependency in dependencies) _edges.Add(new HipGraphEdge(dependency, node));
        return node;
    }

    private HipError WithDependencyHandles(List<HipGraphNode> dependencies, Func<IntPtr, UIntPtr, HipError> operation)
    {
        IntPtr buffer = IntPtr.Zero;
        try
        {
            if (dependencies.Count != 0)
            {
                buffer = Marshal.AllocHGlobal(checked(dependencies.Count * IntPtr.Size));
                for (int index = 0; index < dependencies.Count; index++) Marshal.WriteIntPtr(buffer, index * IntPtr.Size, dependencies[index].Handle);
            }
            return operation(buffer, ToUIntPtr(dependencies.Count));
        }
        finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
    }

    private void InvokeDependencyPair(HipGraphNode prerequisite, HipGraphNode dependent, bool add)
    {
        IntPtr from = Marshal.AllocHGlobal(IntPtr.Size);
        IntPtr to = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            Marshal.WriteIntPtr(from, prerequisite.Handle);
            Marshal.WriteIntPtr(to, dependent.Handle);
            HipError error = add ? _nativeApi.GraphAddDependencies(Handle, from, to, new UIntPtr(1)) : _nativeApi.GraphRemoveDependencies(Handle, from, to, new UIntPtr(1));
            HipCall.ThrowIfFailed(_nativeApi, error, add ? "hipGraphAddDependencies" : "hipGraphRemoveDependencies");
        }
        finally { Marshal.FreeHGlobal(to); Marshal.FreeHGlobal(from); }
    }

    private IntPtr MarshalAccessDescriptors(IReadOnlyList<HipMemoryPoolAccessDescriptor>? access)
    {
        if (access is null || access.Count == 0) return IntPtr.Zero;
        int size = Marshal.SizeOf<HipMemoryPoolAccessDescriptorNative>();
        IntPtr buffer = Marshal.AllocHGlobal(checked(access.Count * size));
        var devices = new HashSet<int>();
        try
        {
            for (int index = 0; index < access.Count; index++)
            {
                HipMemoryPoolAccessDescriptor descriptor = access[index];
                if (!ReferenceEquals(_nativeApi, descriptor.Device.NativeApi)) throw new ArgumentException("Access device belongs to another Runtime.", nameof(access));
                if (!devices.Add(descriptor.Device.Ordinal)) throw new ArgumentException("Access descriptors cannot contain duplicate devices.", nameof(access));
                Marshal.StructureToPtr(new HipMemoryPoolAccessDescriptorNative(descriptor.Device.Ordinal, descriptor.Access), IntPtr.Add(buffer, index * size), false);
            }
            return buffer;
        }
        catch { Marshal.FreeHGlobal(buffer); throw; }
    }

    private void ValidateAllGraphMemoryOrdering()
    {
        foreach (GraphMemoryState state in _graphMemory.Values)
        {
            foreach (HipGraphNode consumer in state.Consumers)
                if (!DependsOn(consumer, state.Memory.AllocationNode)) throw new InvalidOperationException("A graph-local memory consumer no longer depends on its allocation.");
            if (state.FreeNode is not null)
            {
                if (!DependsOn(state.FreeNode, state.Memory.AllocationNode)) throw new InvalidOperationException("A graph-local memory free node no longer depends on its allocation.");
                foreach (HipGraphNode consumer in state.Consumers)
                    if (!DependsOn(state.FreeNode, consumer)) throw new InvalidOperationException("A graph-local free node no longer depends on every consumer.");
            }
        }
    }

    private bool ProspectiveNodeDependsOn(List<HipGraphNode> directDependencies, HipGraphNode prerequisite)
    {
        foreach (HipGraphNode dependency in directDependencies)
            if (ReferenceEquals(dependency, prerequisite) || DependsOn(dependency, prerequisite)) return true;
        return false;
    }

    private bool DependsOn(HipGraphNode node, HipGraphNode prerequisite)
    {
        var pending = new Stack<HipGraphNode>();
        var visited = new HashSet<HipGraphNode>();
        pending.Push(node);
        while (pending.Count != 0)
        {
            HipGraphNode current = pending.Pop();
            if (!visited.Add(current)) continue;
            foreach (HipGraphNode dependency in _dependencies[current])
            {
                if (ReferenceEquals(dependency, prerequisite)) return true;
                pending.Push(dependency);
            }
        }
        return false;
    }

    private GraphMemoryState GetGraphMemoryState(HipGraphMemory memory, string parameterName)
    {
        if (memory is null) throw new ArgumentNullException(parameterName);
        if (!ReferenceEquals(memory.Graph, this) || !_graphMemory.TryGetValue(memory, out GraphMemoryState? state))
            throw new ArgumentException("Graph-local memory belongs to a different graph.", parameterName);
        return state;
    }

    private void ValidateNode(HipGraphNode node, string parameterName)
    {
        if (node is null) throw new ArgumentNullException(parameterName);
        if (!ReferenceEquals(node.Graph, this) || !_dependencies.ContainsKey(node)) throw new ArgumentException("Node belongs to a different graph.", parameterName);
        ThrowIfDisposed();
    }

    private void ValidateExplicit()
    {
        ThrowIfDisposed();
        if (Kind != HipGraphKind.Explicit) throw new InvalidOperationException("Managed topology is available only for graphs created explicitly by HipRuntime.CreateGraph.");
    }

    private void ValidateMutable()
    {
        ValidateExplicit();
        if (_sealed) throw new InvalidOperationException("An explicit graph cannot be mutated after successful instantiation.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposeRequested || _handle.IsClosed || _handle.IsInvalid) throw new ObjectDisposedException(nameof(HipGraph));
    }

    private IntPtr Handle => _handle.DangerousGetHandle();

    private static UIntPtr ToUIntPtr(int value) => new UIntPtr(unchecked((uint)value));

    private static void ValidateOperationBytes(ulong byteCount, ulong sourceCapacity, ulong destinationCapacity)
    {
        if (byteCount == 0) throw new ArgumentOutOfRangeException(nameof(byteCount));
        if (byteCount > sourceCapacity || byteCount > destinationCapacity) throw new ArgumentOutOfRangeException(nameof(byteCount), "The operation exceeds an operand capacity.");
        HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
    }

    private static ReadOnlyCollection<T> Snapshot<T>(IEnumerable<T> values) => new(new List<T>(values));

    private sealed class GraphMemoryState
    {
        internal GraphMemoryState(HipGraphMemory memory) => Memory = memory;
        internal HipGraphMemory Memory { get; }
        internal List<HipGraphNode> Consumers { get; } = new();
        internal HipGraphNode? FreeNode { get; set; }
        internal void AddConsumer(HipGraphNode node) { if (!Consumers.Contains(node)) Consumers.Add(node); }
    }
}
