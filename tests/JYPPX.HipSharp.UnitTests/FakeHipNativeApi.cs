using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using JYPPX.HipSharp.Interop;
using JYPPX.HipSharp.Memory;
using JYPPX.HipSharp.Types;

namespace JYPPX.HipSharp.UnitTests;

internal sealed class FakeHipNativeApi : IHipNativeApi, IDisposable
{
    private readonly Dictionary<IntPtr, int> _allocations = new();
    private readonly HashSet<IntPtr> _streams = new();
    private readonly HashSet<IntPtr> _events = new();
    private readonly HashSet<IntPtr> _graphs = new();
    private readonly HashSet<IntPtr> _graphExecs = new();
    private readonly HashSet<IntPtr> _capturingStreams = new();
    private readonly Dictionary<IntPtr, List<Action>> _pendingStreamActions = new();

    internal HipError MallocResult { get; set; } = HipError.Success;

    internal HipError ModuleLoadResult { get; set; } = HipError.Success;

    internal HipError ModuleUnloadResult { get; set; } = HipError.Success;

    internal HipError ModuleGetFunctionResult { get; set; } = HipError.Success;

    internal HipError ModuleLaunchResult { get; set; } = HipError.Success;

    internal HipError SynchronizeResult { get; set; } = HipError.Success;

    internal HipError StreamSynchronizeResult { get; set; } = HipError.Success;

    internal HipError StreamQueryResult { get; set; } = HipError.Success;

    internal HipError ManagedMallocResult { get; set; } = HipError.Success;

    internal HipError MemAdviseResult { get; set; } = HipError.Success;

    internal HipError MemPrefetchResult { get; set; } = HipError.Success;

    internal HipError MallocAsyncResult { get; set; } = HipError.Success;

    internal HipError FreeAsyncResult { get; set; } = HipError.Success;

    internal HipError FreeResult { get; set; } = HipError.Success;

    internal HipError PeerEnableResult { get; set; } = HipError.Success;

    internal HipError PeerDisableResult { get; set; } = HipError.Success;

    internal HipError PeerCopyResult { get; set; } = HipError.Success;

    internal HipError BeginCaptureResult { get; set; } = HipError.Success;

    internal HipError EndCaptureResult { get; set; } = HipError.Success;

    internal HipError GraphInstantiateResult { get; set; } = HipError.Success;

    internal HipError GraphLaunchResult { get; set; } = HipError.Success;

    internal HipError GraphExecDestroyResult { get; set; } = HipError.Success;

    internal bool PeerCapability { get; set; } = true;

    internal bool ReturnManagedPointerOnFailure { get; set; }

    internal bool ReturnAsyncPointerOnFailure { get; set; }

    internal bool ReturnGraphOnEndCaptureFailure { get; set; }

    internal bool ReturnGraphExecOnInstantiateFailure { get; set; }

    internal HipError EventSynchronizeResult { get; set; } = HipError.Success;

    internal HipError EventQueryResult { get; set; } = HipError.Success;

    internal IList<bool> ExpectedKernelPointerArguments { get; } = new List<bool>();

    internal IList<long> LastKernelArgumentValues { get; } = new List<long>();

    internal byte[] LastModuleCodeObject { get; private set; } = Array.Empty<byte>();

    internal string LastKernelName { get; private set; } = string.Empty;

    internal uint LastInitFlags { get; private set; }

    internal int LastSetDevice { get; private set; }

    internal int FreeCount { get; private set; }

    internal int SynchronizeCount { get; private set; }

    internal int ModuleUnloadCount { get; private set; }

    internal int ModuleLaunchCount { get; private set; }

    internal int AsyncCopyCount { get; private set; }

    internal int StreamDestroyCount { get; private set; }

    internal int EventDestroyCount { get; private set; }

    internal int ManagedAllocationCount { get; private set; }

    internal int AsyncAllocationCount { get; private set; }

    internal int AsyncFreeCount { get; private set; }

    internal int FreeAsyncCallCount { get; private set; }

    internal int MemAdviseCount { get; private set; }

    internal int MemPrefetchCount { get; private set; }

    internal int PeerEnableCount { get; private set; }

    internal int PeerDisableCount { get; private set; }

    internal int PeerCopyCount { get; private set; }

    internal int GraphDestroyCount { get; private set; }

    internal int GraphExecDestroyCount { get; private set; }

    internal int GraphLaunchCount { get; private set; }

    public HipError Init(uint flags)
    {
        LastInitFlags = flags;
        return HipError.Success;
    }

    public HipError RuntimeGetVersion(out int runtimeVersion)
    {
        runtimeVersion = 70200001;
        return HipError.Success;
    }

    public HipError DriverGetVersion(out int driverVersion)
    {
        driverVersion = 70200000;
        return HipError.Success;
    }

    public HipError GetDeviceCount(out int count)
    {
        count = 2;
        return HipError.Success;
    }

    public HipError GetDevice(out int deviceId)
    {
        deviceId = LastSetDevice;
        return HipError.Success;
    }

    public HipError SetDevice(int deviceId)
    {
        if (deviceId < 0 || deviceId >= 2)
        {
            return HipError.InvalidDevice;
        }

        LastSetDevice = deviceId;
        return HipError.Success;
    }

    public HipError DeviceGetName(int deviceId, out string name)
    {
        name = deviceId switch
        {
            0 => "Fake Radeon 0",
            1 => "Fake Radeon 1",
            _ => string.Empty,
        };
        return deviceId >= 0 && deviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId)
    {
        value = attribute == HipDeviceAttribute.MaxThreadsPerBlock ? 1024 : 0;
        return deviceId >= 0 && deviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError MallocManaged(out IntPtr pointer, UIntPtr byteCount, uint flags)
    {
        if (ManagedMallocResult != HipError.Success)
        {
            pointer = ReturnManagedPointerOnFailure ? AllocateRaw(byteCount) : IntPtr.Zero;
            return ManagedMallocResult;
        }
        HipError result = Malloc(out pointer, byteCount);
        if (result == HipError.Success) ManagedAllocationCount++;
        return result;
    }

    public HipError MemPrefetchAsync(IntPtr pointer, UIntPtr byteCount, int device, IntPtr stream)
    {
        if (!_allocations.ContainsKey(pointer) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (MemPrefetchResult == HipError.Success) MemPrefetchCount++;
        return MemPrefetchResult;
    }

    public HipError MemAdvise(IntPtr pointer, UIntPtr byteCount, HipMemoryAdvise advice, int device)
    {
        if (!_allocations.ContainsKey(pointer)) return HipError.InvalidValue;
        if (MemAdviseResult == HipError.Success) MemAdviseCount++;
        return MemAdviseResult;
    }

    public HipError MallocAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr stream)
    {
        if (!_streams.Contains(stream))
        {
            pointer = IntPtr.Zero;
            return HipError.InvalidValue;
        }
        if (MallocAsyncResult != HipError.Success)
        {
            pointer = ReturnAsyncPointerOnFailure ? AllocateRaw(byteCount) : IntPtr.Zero;
            return MallocAsyncResult;
        }
        HipError result = Malloc(out pointer, byteCount);
        if (result == HipError.Success) AsyncAllocationCount++;
        return result;
    }

    public HipError FreeAsync(IntPtr pointer, IntPtr stream)
    {
        FreeAsyncCallCount++;
        if (!_allocations.ContainsKey(pointer) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (FreeAsyncResult != HipError.Success) return FreeAsyncResult;
        QueueStreamAction(stream, () =>
        {
            if (_allocations.ContainsKey(pointer))
            {
                Free(pointer);
                AsyncFreeCount++;
            }
        });
        return HipError.Success;
    }

    public HipError DeviceCanAccessPeer(out int canAccessPeer, int deviceId, int peerDeviceId)
    {
        canAccessPeer = PeerCapability && deviceId != peerDeviceId && deviceId >= 0 && deviceId < 2 && peerDeviceId >= 0 && peerDeviceId < 2 ? 1 : 0;
        return deviceId >= 0 && deviceId < 2 && peerDeviceId >= 0 && peerDeviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError DeviceEnablePeerAccess(int peerDeviceId, uint flags)
    {
        if (peerDeviceId < 0 || peerDeviceId >= 2 || flags != 0) return HipError.InvalidValue;
        if (PeerEnableResult == HipError.Success) PeerEnableCount++;
        return PeerEnableResult;
    }

    public HipError DeviceDisablePeerAccess(int peerDeviceId)
    {
        if (peerDeviceId < 0 || peerDeviceId >= 2) return HipError.InvalidValue;
        if (PeerDisableResult == HipError.Success) PeerDisableCount++;
        return PeerDisableResult;
    }

    public HipError MemcpyPeerAsync(IntPtr destination, int destinationDevice, IntPtr source, int sourceDevice, UIntPtr byteCount, IntPtr stream)
    {
        if (PeerCopyResult != HipError.Success) return PeerCopyResult;
        if (!_streams.Contains(stream) || !_allocations.ContainsKey(destination) || !_allocations.ContainsKey(source)) return HipError.InvalidValue;
        PeerCopyCount++;
        return Memcpy(destination, source, byteCount, HipMemoryCopyKind.DeviceToDevice);
    }

    public HipError StreamBeginCapture(IntPtr stream, HipStreamCaptureMode mode)
    {
        if (!_streams.Contains(stream) || _capturingStreams.Contains(stream)) return HipError.InvalidValue;
        if (BeginCaptureResult == HipError.Success) _capturingStreams.Add(stream);
        return BeginCaptureResult;
    }

    public HipError StreamEndCapture(IntPtr stream, out IntPtr graph)
    {
        graph = IntPtr.Zero;
        if (!_capturingStreams.Remove(stream)) return HipError.StreamCaptureUnmatched;
        if (EndCaptureResult != HipError.Success)
        {
            if (ReturnGraphOnEndCaptureFailure)
            {
                graph = new IntPtr(0x7000 + _graphs.Count + 1);
                _graphs.Add(graph);
            }
            return EndCaptureResult;
        }
        graph = new IntPtr(0x7000 + _graphs.Count + 1);
        _graphs.Add(graph);
        return HipError.Success;
    }

    public HipError GraphDestroy(IntPtr graph)
    {
        if (!_graphs.Remove(graph)) return HipError.InvalidValue;
        GraphDestroyCount++;
        return HipError.Success;
    }

    public HipError GraphInstantiateWithFlags(out IntPtr graphExec, IntPtr graph, ulong flags)
    {
        graphExec = IntPtr.Zero;
        if (!_graphs.Contains(graph) || flags != 0) return HipError.InvalidValue;
        if (GraphInstantiateResult != HipError.Success)
        {
            if (ReturnGraphExecOnInstantiateFailure)
            {
                graphExec = new IntPtr(0x8000 + _graphExecs.Count + 1);
                _graphExecs.Add(graphExec);
            }
            return GraphInstantiateResult;
        }
        graphExec = new IntPtr(0x8000 + _graphExecs.Count + 1);
        _graphExecs.Add(graphExec);
        return HipError.Success;
    }

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream)
    {
        if (!_graphExecs.Contains(graphExec) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (GraphLaunchResult == HipError.Success) GraphLaunchCount++;
        return GraphLaunchResult;
    }

    public HipError GraphExecDestroy(IntPtr graphExec)
    {
        if (GraphExecDestroyResult != HipError.Success) return GraphExecDestroyResult;
        if (!_graphExecs.Remove(graphExec)) return HipError.InvalidValue;
        GraphExecDestroyCount++;
        return HipError.Success;
    }

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount)
    {
        if (MallocResult != HipError.Success)
        {
            pointer = IntPtr.Zero;
            return MallocResult;
        }

        ulong size = byteCount.ToUInt64();
        if (size > int.MaxValue)
        {
            pointer = IntPtr.Zero;
            return HipError.OutOfMemory;
        }

        pointer = Marshal.AllocHGlobal((int)size);
        _allocations.Add(pointer, (int)size);
        return HipError.Success;
    }

    private IntPtr AllocateRaw(UIntPtr byteCount)
    {
        int size = checked((int)byteCount.ToUInt64());
        IntPtr pointer = Marshal.AllocHGlobal(size);
        _allocations.Add(pointer, size);
        return pointer;
    }

    public HipError Free(IntPtr pointer)
    {
        if (FreeResult != HipError.Success) return FreeResult;
        if (!_allocations.Remove(pointer))
        {
            return HipError.InvalidValue;
        }

        Marshal.FreeHGlobal(pointer);
        FreeCount++;
        return HipError.Success;
    }

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind)
    {
        int count = checked((int)byteCount.ToUInt64());
        var buffer = new byte[count];
        Marshal.Copy(source, buffer, 0, count);
        Marshal.Copy(buffer, 0, destination, count);
        return HipError.Success;
    }

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream)
    {
        AsyncCopyCount++;
        return Memcpy(destination, source, byteCount, kind);
    }

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags) => Malloc(out pointer, byteCount);

    public HipError HostFree(IntPtr pointer) => Free(pointer);

    public HipError DeviceSynchronize()
    {
        SynchronizeCount++;
        return SynchronizeResult;
    }

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags)
    {
        stream = new IntPtr(0x5000 + _streams.Count + 1);
        _streams.Add(stream);
        return HipError.Success;
    }

    public HipError StreamDestroy(IntPtr stream)
    {
        if (!_streams.Remove(stream)) return HipError.InvalidValue;
        StreamDestroyCount++;
        return HipError.Success;
    }

    public HipError StreamSynchronize(IntPtr stream)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (StreamSynchronizeResult == HipError.Success) CompleteStream(stream);
        return StreamSynchronizeResult;
    }

    public HipError StreamQuery(IntPtr stream)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (StreamQueryResult == HipError.Success) CompleteStream(stream);
        return StreamQueryResult;
    }

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags)
    {
        eventHandle = new IntPtr(0x6000 + _events.Count + 1);
        _events.Add(eventHandle);
        return HipError.Success;
    }

    public HipError EventDestroy(IntPtr eventHandle)
    {
        if (!_events.Remove(eventHandle)) return HipError.InvalidValue;
        EventDestroyCount++;
        return HipError.Success;
    }

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream) => _events.Contains(eventHandle) && _streams.Contains(stream) ? HipError.Success : HipError.InvalidValue;

    public HipError EventSynchronize(IntPtr eventHandle) => _events.Contains(eventHandle) ? EventSynchronizeResult : HipError.InvalidValue;

    public HipError EventQuery(IntPtr eventHandle) => _events.Contains(eventHandle) ? EventQueryResult : HipError.InvalidValue;

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end)
    {
        milliseconds = 1.25f;
        return _events.Contains(start) && _events.Contains(end) ? HipError.Success : HipError.InvalidValue;
    }

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module)
    {
        LastModuleCodeObject = (byte[])codeObject.Clone();
        module = ModuleLoadResult == HipError.Success ? new IntPtr(0x2000) : IntPtr.Zero;
        return ModuleLoadResult;
    }

    public HipError ModuleUnload(IntPtr module)
    {
        if (ModuleUnloadResult == HipError.Success)
        {
            ModuleUnloadCount++;
        }

        return ModuleUnloadResult;
    }

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function)
    {
        LastKernelName = kernelName;
        function = ModuleGetFunctionResult == HipError.Success ? new IntPtr(0x3000) : IntPtr.Zero;
        return ModuleGetFunctionResult;
    }

    public HipError ModuleLaunchKernel(
        IntPtr function,
        uint gridX,
        uint gridY,
        uint gridZ,
        uint blockX,
        uint blockY,
        uint blockZ,
        uint sharedMemoryBytes,
        IntPtr stream,
        IntPtr kernelParameters)
    {
        ModuleLaunchCount++;
        LastKernelArgumentValues.Clear();
        for (int index = 0; index < ExpectedKernelPointerArguments.Count; index++)
        {
            IntPtr valueStorage = Marshal.ReadIntPtr(kernelParameters, index * IntPtr.Size);
            long value = ExpectedKernelPointerArguments[index]
                ? Marshal.ReadIntPtr(valueStorage).ToInt64()
                : Marshal.ReadInt32(valueStorage);
            LastKernelArgumentValues.Add(value);
        }

        return ModuleLaunchResult;
    }

    public string GetErrorName(HipError error) => error == HipError.OutOfMemory ? "hipErrorOutOfMemory" : "hipErrorUnknown";

    public string GetErrorString(HipError error) => error == HipError.OutOfMemory ? "out of memory" : "unknown HIP error";

    public void Dispose()
    {
        foreach (IntPtr pointer in _allocations.Keys)
        {
            Marshal.FreeHGlobal(pointer);
        }

        _allocations.Clear();
    }

    private void QueueStreamAction(IntPtr stream, Action action)
    {
        if (!_pendingStreamActions.TryGetValue(stream, out List<Action>? actions))
        {
            actions = new List<Action>();
            _pendingStreamActions.Add(stream, actions);
        }
        actions.Add(action);
    }

    private void CompleteStream(IntPtr stream)
    {
        if (!_pendingStreamActions.TryGetValue(stream, out List<Action>? actions)) return;
        _pendingStreamActions.Remove(stream);
        foreach (Action action in actions) action();
    }
}
