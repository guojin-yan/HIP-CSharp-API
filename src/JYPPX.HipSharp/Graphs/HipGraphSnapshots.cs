using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Modules;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.Graphs;

/// <summary>Owns an unmanaged graph parameter snapshot / 持有非托管 Graph 参数快照.</summary>
internal sealed class HipGraphStructBuffer<T> : IDisposable where T : struct
{
    internal HipGraphStructBuffer(T value)
    {
        Pointer = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        Marshal.StructureToPtr(value, Pointer, false);
    }

    internal IntPtr Pointer { get; private set; }

    internal T Read() => Marshal.PtrToStructure<T>(Pointer);

    public void Dispose()
    {
        if (Pointer == IntPtr.Zero) return;
        Marshal.FreeHGlobal(Pointer);
        Pointer = IntPtr.Zero;
    }
}

internal sealed class HipGraphKernelSnapshot : IDisposable
{
    private readonly HipModule _module;
    private readonly List<IHipPointerOwner> _owners = new();
    private readonly List<IntPtr> _values = new();
    private IntPtr _parameterArray;
    private bool _moduleReference;
    private bool _disposed;

    internal HipGraphKernelSnapshot(HipGraph graph, HipKernel kernel, HipLaunchDimensions grid, HipLaunchDimensions block, IReadOnlyList<HipKernelArgument> arguments, uint sharedMemoryBytes, HipGraphNode? updateNode = null)
    {
        if (kernel is null) throw new ArgumentNullException(nameof(kernel));
        if (arguments is null) throw new ArgumentNullException(nameof(arguments));
        HipKernel.ValidateGraphDimensions(grid, nameof(grid));
        HipKernel.ValidateGraphDimensions(block, nameof(block));
        if (arguments.Count > int.MaxValue / IntPtr.Size) throw new ArgumentOutOfRangeException(nameof(arguments));
        if (!ReferenceEquals(graph.NativeApi, kernel.Module.NativeApi)) throw new ArgumentException("Kernel belongs to a different HIP Runtime client.", nameof(kernel));
        if (kernel.Module.DeviceOrdinal != graph.DeviceOrdinal) throw new ArgumentException("Kernel module differs from the graph device.", nameof(kernel));

        _module = kernel.Module;
        try
        {
            _module.AcquireAsyncReference();
            _moduleReference = true;
            if (arguments.Count != 0) _parameterArray = Marshal.AllocHGlobal(checked(arguments.Count * IntPtr.Size));
            for (int index = 0; index < arguments.Count; index++)
            {
                HipKernelArgument argument = arguments[index] ?? throw new ArgumentNullException(nameof(arguments), "Kernel arguments cannot contain null elements.");
                IntPtr value = argument.Kind switch
                {
                    HipKernelArgumentKind.DevicePointer => CreateOwnerPointer(graph, argument.PointerOwner!, arguments),
                    HipKernelArgumentKind.GraphMemoryPointer => CreateGraphPointer(graph, argument.GraphMemory!, arguments, updateNode),
                    _ => CreateScalar(argument.Int32Value),
                };
                _values.Add(value);
                Marshal.WriteIntPtr(_parameterArray, index * IntPtr.Size, value);
            }

            Parameters = new HipKernelNodeParameters
            {
                BlockDimensions = new HipDim3(block.X, block.Y, block.Z),
                Extra = IntPtr.Zero,
                Function = kernel.Function,
                GridDimensions = new HipDim3(grid.X, grid.Y, grid.Z),
                KernelParameters = _parameterArray,
                SharedMemoryBytes = sharedMemoryBytes,
            };
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    internal HipKernelNodeParameters Parameters { get; }

    public void Dispose()
    {
        if (_disposed) return;
        while (_owners.Count != 0)
        {
            int index = _owners.Count - 1;
            _owners[index].ReleasePointer();
            _owners.RemoveAt(index);
        }
        while (_values.Count != 0)
        {
            int index = _values.Count - 1;
            Marshal.FreeHGlobal(_values[index]);
            _values.RemoveAt(index);
        }
        if (_parameterArray != IntPtr.Zero)
        {
            Marshal.FreeHGlobal(_parameterArray);
        }
        _parameterArray = IntPtr.Zero;
        if (_moduleReference)
        {
            _module.ReleaseAsyncReference();
            _moduleReference = false;
        }
        _disposed = true;
    }

    private IntPtr CreateOwnerPointer(HipGraph graph, IHipPointerOwner owner, IReadOnlyList<HipKernelArgument> arguments)
    {
        if (!ReferenceEquals(graph.NativeApi, owner.NativeApi)) throw new ArgumentException("Kernel pointer arguments must belong to the graph Runtime client.", nameof(arguments));
        if (owner.RequiredStream is not null) throw new ArgumentException("Stream-ordered memory cannot be used by an explicit graph kernel node.", nameof(arguments));
        IntPtr pointer = owner.AcquirePointer(out bool addedReference);
        if (!addedReference || pointer == IntPtr.Zero)
        {
            if (addedReference) owner.ReleasePointer();
            throw new ObjectDisposedException(nameof(HipKernelArgument));
        }
        if (owner is HipDeviceMemory deviceMemory && deviceMemory.DeviceOrdinal != graph.DeviceOrdinal)
        {
            owner.ReleasePointer();
            throw new ArgumentException("Kernel device-memory arguments must be on the graph device.", nameof(arguments));
        }
        _owners.Add(owner);
        return CreatePointerValue(pointer);
    }

    private static IntPtr CreateGraphPointer(HipGraph graph, HipGraphMemory memory, IReadOnlyList<HipKernelArgument> arguments, HipGraphNode? updateNode)
    {
        if (updateNode is null) graph.ValidateGraphMemoryForConsumer(memory, arguments);
        else graph.ValidateGraphMemoryForExecConsumer(memory, updateNode, arguments);
        return CreatePointerValue(memory.Pointer);
    }

    private static IntPtr CreatePointerValue(IntPtr pointer)
    {
        IntPtr storage = Marshal.AllocHGlobal(IntPtr.Size);
        Marshal.WriteIntPtr(storage, pointer);
        return storage;
    }

    private static IntPtr CreateScalar(int value)
    {
        IntPtr storage = Marshal.AllocHGlobal(sizeof(int));
        Marshal.WriteInt32(storage, value);
        return storage;
    }
}

internal sealed class HipGraphPointerLease : IDisposable
{
    private IHipPointerOwner? _owner;

    internal HipGraphPointerLease(IHipPointerOwner owner)
    {
        _owner = owner;
        Pointer = owner.AcquirePointer(out bool addedReference);
        if (!addedReference || Pointer == IntPtr.Zero)
        {
            if (addedReference) owner.ReleasePointer();
            _owner = null;
            throw new ObjectDisposedException(nameof(IHipPointerOwner));
        }
    }

    internal IntPtr Pointer { get; }

    public void Dispose()
    {
        IHipPointerOwner? owner = _owner;
        if (owner is null) return;
        owner.ReleasePointer();
        _owner = null;
    }
}

internal readonly struct HipGraphMemoryOperand
{
    internal HipGraphMemoryOperand(IntPtr pointer, ulong byteLength, IDisposable? lease, HipGraphMemory? graphMemory)
    {
        Pointer = pointer;
        ByteLength = byteLength;
        Lease = lease;
        GraphMemory = graphMemory;
    }

    internal IntPtr Pointer { get; }
    internal ulong ByteLength { get; }
    internal IDisposable? Lease { get; }
    internal HipGraphMemory? GraphMemory { get; }
}

internal sealed class HipGraphCompositeLease : IDisposable
{
    private IDisposable? _first;
    private IDisposable? _second;

    internal HipGraphCompositeLease(IDisposable? first, IDisposable? second)
    {
        _first = first;
        _second = second;
    }

    public void Dispose()
    {
        if (_second is not null)
        {
            _second.Dispose();
            _second = null;
        }
        if (_first is not null)
        {
            _first.Dispose();
            _first = null;
        }
    }
}

internal sealed class HipGraphExecResources : IDisposable
{
    private readonly object _sync = new();
    private readonly Dictionary<HipGraphNode, IDisposable> _updates = new();
    private readonly List<IDisposable> _retired = new();
    private IDisposable? _graphReference;
    private bool _disposed;

    internal HipGraphExecResources(IDisposable? graphReference) => _graphReference = graphReference;

    internal void Replace(HipGraphNode node, IDisposable resources)
    {
        IDisposable? previous = null;
        lock (_sync)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(HipGraphExecResources));
            if (_updates.TryGetValue(node, out previous)) _updates[node] = resources;
            else _updates.Add(node, resources);
        }
        if (previous is not null)
        {
            lock (_sync) _retired.Add(previous);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed) return;
            if (_updates.Count != 0)
            {
                _retired.AddRange(_updates.Values);
                _updates.Clear();
            }
            while (_retired.Count != 0)
            {
                int index = _retired.Count - 1;
                _retired[index].Dispose();
                _retired.RemoveAt(index);
            }
            _graphReference?.Dispose();
            _graphReference = null;
            _disposed = true;
        }
    }
}
