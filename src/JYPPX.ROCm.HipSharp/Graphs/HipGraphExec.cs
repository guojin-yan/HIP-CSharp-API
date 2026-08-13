using System;
using System.Collections.Generic;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Graphs;

/// <summary>拥有可 upload、更新和启动的 HIP graph executable / Owns a HIP graph executable that can be uploaded, updated, and launched.</summary>
public sealed class HipGraphExec : IDisposable
{
    private readonly IHipNativeApi _nativeApi;
    private readonly HipGraphExecHandle _handle;
    private readonly HipGraphExecResources _resources;
    private readonly HipGraph? _explicitGraph;
    private readonly int _deviceOrdinal;
    private readonly object _lifetimeSync = new();
    private int _asyncReferences;
    private bool _disposeRequested;

    internal HipGraphExec(IHipNativeApi nativeApi, IntPtr handle, IDisposable? resourceReference = null, HipGraph? explicitGraph = null, int deviceOrdinal = -1)
    {
        if (handle == IntPtr.Zero) throw new ArgumentException("A HIP graph executable handle cannot be null.", nameof(handle));
        _nativeApi = nativeApi ?? throw new ArgumentNullException(nameof(nativeApi));
        _resources = new HipGraphExecResources(resourceReference);
        _handle = new HipGraphExecHandle(nativeApi, handle, _resources);
        _explicitGraph = explicitGraph;
        _deviceOrdinal = deviceOrdinal;
    }

    /// <summary>获取 executable 是否已释放 / Gets whether the executable is disposed.</summary>
    public bool IsDisposed { get { lock (_lifetimeSync) return _disposeRequested || _handle.IsClosed || _handle.IsInvalid; } }

    /// <summary>将 executable upload 到指定 stream / Uploads the executable to a stream.</summary>
    public void Upload(HipStream stream)
    {
        ValidateStream(stream);
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            ThrowIfLaunchPending("Upload");
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphUpload(_handle.DangerousGetHandle(), stream.DangerousGetHandle()), "hipGraphUpload");
        }
    }

    /// <summary>在指定 stream 上启动 executable；stream 完成前 executable 保持有效 / Launches on a stream while retaining the executable until completion.</summary>
    public void Launch(HipStream stream)
    {
        ValidateStream(stream);
        bool addedReference = false;
        try
        {
            lock (_lifetimeSync)
            {
                ThrowIfDisposed();
                _handle.DangerousAddRef(ref addedReference);
                if (!addedReference) throw new ObjectDisposedException(nameof(HipGraphExec));
                _asyncReferences++;
            }
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphLaunch(_handle.DangerousGetHandle(), stream.DangerousGetHandle()), "hipGraphLaunch");
            stream.AddPendingLease(new HipAsyncLease(ReleaseAsyncReference));
            addedReference = false;
        }
        finally { if (addedReference) ReleaseAsyncReference(); }
    }

    /// <summary>更新 executable kernel node 参数；pending launch 时拒绝 / Updates executable kernel-node parameters and rejects pending launches.</summary>
    public void UpdateKernel(HipGraphNode node, HipKernel kernel, HipLaunchDimensions grid, HipLaunchDimensions block, IReadOnlyList<HipKernelArgument> arguments, uint sharedMemoryBytes = 0)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            ThrowIfLaunchPending("Kernel update");
            HipGraph graph = ValidateUpdateNode(node, HipGraphNodeType.Kernel);
            var snapshot = new HipGraphKernelSnapshot(graph, kernel, grid, block, arguments, sharedMemoryBytes, node);
            try
            {
                using var buffer = new HipGraphStructBuffer<HipKernelNodeParameters>(snapshot.Parameters);
                HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecKernelNodeSetParams(_handle.DangerousGetHandle(), node.Handle, buffer.Pointer), "hipGraphExecKernelNodeSetParams");
                _resources.Replace(node, snapshot);
                snapshot = null!;
            }
            finally { snapshot?.Dispose(); }
        }
    }

    /// <summary>更新普通 device memory 之间的 memcpy node / Updates a memcpy node between regular device allocations.</summary>
    public void UpdateCopy(HipGraphNode node, HipDeviceMemory source, HipDeviceMemory destination, ulong byteCount) =>
        UpdateCopyOperands(node, () => CreateDeviceOperand(source, nameof(source)), () => CreateDeviceOperand(destination, nameof(destination)), byteCount);

    /// <summary>更新普通到 graph-local memory 的 memcpy node / Updates a regular-to-graph-local memcpy node.</summary>
    public void UpdateCopy(HipGraphNode node, HipDeviceMemory source, HipGraphMemory destination, ulong byteCount) =>
        UpdateCopyOperands(node, () => CreateDeviceOperand(source, nameof(source)), () => CreateGraphOperand(node, destination, nameof(destination)), byteCount);

    /// <summary>更新 graph-local 到普通 memory 的 memcpy node / Updates a graph-local-to-regular memcpy node.</summary>
    public void UpdateCopy(HipGraphNode node, HipGraphMemory source, HipDeviceMemory destination, ulong byteCount) =>
        UpdateCopyOperands(node, () => CreateGraphOperand(node, source, nameof(source)), () => CreateDeviceOperand(destination, nameof(destination)), byteCount);

    /// <summary>更新两个 graph-local references 之间的 memcpy node / Updates a memcpy node between graph-local references.</summary>
    public void UpdateCopy(HipGraphNode node, HipGraphMemory source, HipGraphMemory destination, ulong byteCount) =>
        UpdateCopyOperands(node, () => CreateGraphOperand(node, source, nameof(source)), () => CreateGraphOperand(node, destination, nameof(destination)), byteCount);

    /// <summary>更新普通 device memory memset node / Updates a regular device-memory memset node.</summary>
    public void UpdateMemset(HipGraphNode node, HipDeviceMemory destination, int value, ulong byteCount = 0) =>
        UpdateMemsetOperand(node, () => CreateDeviceOperand(destination, nameof(destination)), value, byteCount);

    /// <summary>更新 graph-local memory memset node / Updates a graph-local memory memset node.</summary>
    public void UpdateMemset(HipGraphNode node, HipGraphMemory destination, int value, ulong byteCount = 0) =>
        UpdateMemsetOperand(node, () => CreateGraphOperand(node, destination, nameof(destination)), value, byteCount);

    /// <summary>释放 executable；pending launch 完成后再销毁 / Disposes the executable after pending launches complete.</summary>
    public void Dispose()
    {
        lock (_lifetimeSync)
        {
            if (_handle.IsClosed) return;
            _disposeRequested = true;
            if (_asyncReferences != 0) return;
        }
        ReleaseChecked();
    }

    private void UpdateCopyCore(HipGraphNode node, HipGraphMemoryOperand source, HipGraphMemoryOperand destination, ulong byteCount)
    {
        HipGraphCompositeLease? leases = new(source.Lease, destination.Lease);
        try
        {
            ValidateBytes(byteCount, source.ByteLength, destination.ByteLength);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecMemcpyNodeSetParams1D(_handle.DangerousGetHandle(), node.Handle, destination.Pointer, source.Pointer, HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount)), HipMemoryCopyKind.DeviceToDevice), "hipGraphExecMemcpyNodeSetParams1D");
            _resources.Replace(node, leases);
            leases = null;
        }
        finally { leases?.Dispose(); }
    }

    private void UpdateCopyOperands(HipGraphNode node, Func<HipGraphMemoryOperand> createSource, Func<HipGraphMemoryOperand> createDestination, ulong byteCount)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            ThrowIfLaunchPending("Memcpy update");
            ValidateUpdateNode(node, HipGraphNodeType.MemoryCopy);
            HipGraphMemoryOperand source = createSource();
            try
            {
                HipGraphMemoryOperand destination = createDestination();
                UpdateCopyCore(node, source, destination, byteCount);
            }
            catch
            {
                source.Lease?.Dispose();
                throw;
            }
        }
    }

    private void UpdateMemsetCore(HipGraphNode node, HipGraphMemoryOperand destination, int value, ulong byteCount)
    {
        IDisposable? lease = destination.Lease;
        try
        {
            if (value < byte.MinValue || value > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(value));
            ulong actualBytes = byteCount == 0 ? destination.ByteLength : byteCount;
            ValidateBytes(actualBytes, destination.ByteLength, destination.ByteLength);
            var parameters = new HipMemsetNodeParameters
            {
                Destination = destination.Pointer,
                ElementSize = 1,
                Height = new UIntPtr(1),
                Pitch = UIntPtr.Zero,
                Value = (uint)value,
                Width = HipDeviceMemory.ToUIntPtr(actualBytes, nameof(byteCount)),
            };
            using var buffer = new HipGraphStructBuffer<HipMemsetNodeParameters>(parameters);
            HipCall.ThrowIfFailed(_nativeApi, _nativeApi.GraphExecMemsetNodeSetParams(_handle.DangerousGetHandle(), node.Handle, buffer.Pointer), "hipGraphExecMemsetNodeSetParams");
            _resources.Replace(node, lease ?? new HipGraphCompositeLease(null, null));
            lease = null;
        }
        finally { lease?.Dispose(); }
    }

    private void UpdateMemsetOperand(HipGraphNode node, Func<HipGraphMemoryOperand> createDestination, int value, ulong byteCount)
    {
        lock (_lifetimeSync)
        {
            ThrowIfDisposed();
            ThrowIfLaunchPending("Memset update");
            ValidateUpdateNode(node, HipGraphNodeType.MemorySet);
            UpdateMemsetCore(node, createDestination(), value, byteCount);
        }
    }

    private HipGraphMemoryOperand CreateDeviceOperand(HipDeviceMemory memory, string parameterName)
    {
        if (memory is null) throw new ArgumentNullException(parameterName);
        if (!ReferenceEquals(_nativeApi, memory.NativeApi)) throw new ArgumentException("Memory belongs to another HIP Runtime client.", parameterName);
        if (_deviceOrdinal >= 0 && memory.DeviceOrdinal != _deviceOrdinal) throw new ArgumentException("Memory differs from the executable device.", parameterName);
        var lease = new HipGraphPointerLease((IHipPointerOwner)memory);
        return new HipGraphMemoryOperand(lease.Pointer, memory.ByteLength, lease, null);
    }

    private HipGraphMemoryOperand CreateGraphOperand(HipGraphNode node, HipGraphMemory memory, string parameterName)
    {
        HipGraph graph = ValidateUpdateNode(node, node.Type);
        graph.ValidateGraphMemoryForExecConsumer(memory, node, parameterName);
        return new HipGraphMemoryOperand(memory.Pointer, memory.ByteLength, null, memory);
    }

    private HipGraph ValidateUpdateNode(HipGraphNode node, HipGraphNodeType type)
    {
        if (node is null) throw new ArgumentNullException(nameof(node));
        HipGraph graph = _explicitGraph ?? throw new InvalidOperationException("Captured graph executables do not expose managed node updates.");
        if (!ReferenceEquals(node.Graph, graph)) throw new ArgumentException("Node belongs to a different graph executable.", nameof(node));
        graph.ValidateExecNode(node, type, nameof(node));
        return graph;
    }

    private void ValidateStream(HipStream stream)
    {
        if (stream is null) throw new ArgumentNullException(nameof(stream));
        if (!ReferenceEquals(_nativeApi, stream.NativeApi)) throw new ArgumentException("Graph executable and stream belong to different HIP Runtime clients.", nameof(stream));
        if (_deviceOrdinal >= 0 && stream.DeviceOrdinal != _deviceOrdinal) throw new ArgumentException("Stream differs from the graph executable device.", nameof(stream));
    }

    private void ReleaseAsyncReference()
    {
        bool releaseChecked;
        lock (_lifetimeSync)
        {
            if (_asyncReferences > 0)
            {
                _handle.DangerousRelease();
                _asyncReferences--;
            }
            releaseChecked = _disposeRequested && _asyncReferences == 0;
        }
        if (releaseChecked) ReleaseChecked();
    }

    private void ReleaseChecked()
    {
        HipError error = _handle.ReleaseChecked();
        if (error != HipError.Success) HipCall.ThrowIfFailed(_nativeApi, error, "hipGraphExecDestroy");
        _handle.Dispose();
    }

    private void ThrowIfLaunchPending(string operation)
    {
        if (_asyncReferences != 0) throw new InvalidOperationException(operation + " is not allowed while a graph launch is pending.");
    }

    private void ThrowIfDisposed()
    {
        if (_disposeRequested || _handle.IsClosed || _handle.IsInvalid) throw new ObjectDisposedException(nameof(HipGraphExec));
    }

    private static void ValidateBytes(ulong byteCount, ulong sourceCapacity, ulong destinationCapacity)
    {
        if (byteCount == 0 || byteCount > sourceCapacity || byteCount > destinationCapacity) throw new ArgumentOutOfRangeException(nameof(byteCount));
        HipDeviceMemory.ToUIntPtr(byteCount, nameof(byteCount));
    }
}
