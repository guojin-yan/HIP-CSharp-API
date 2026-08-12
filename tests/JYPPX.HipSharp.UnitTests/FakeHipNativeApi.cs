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
    private readonly HashSet<IntPtr> _memoryPools = new();
    private readonly Dictionary<int, IntPtr> _defaultMemoryPools = new();
    private readonly Dictionary<int, IntPtr> _currentMemoryPools = new();
    private readonly Dictionary<IntPtr, Dictionary<int, ulong>> _poolAttributes = new();
    private readonly Dictionary<(IntPtr Pool, int Device), HipMemoryPoolAccess> _poolAccess = new();
    private readonly Dictionary<IntPtr, IntPtr> _allocationPools = new();
    private int _nextPool = 0x9000;

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

    internal HipError MemoryPoolCreateResult { get; set; } = HipError.Success;

    internal HipError MemoryPoolDestroyResult { get; set; } = HipError.Success;

    internal HipError MemoryPoolTrimResult { get; set; } = HipError.Success;

    internal HipError MemoryPoolAttributeResult { get; set; } = HipError.Success;

    internal HipError MemoryPoolAccessResult { get; set; } = HipError.Success;

    internal HipError DeviceSetMemoryPoolResult { get; set; } = HipError.Success;

    internal HipError MallocFromPoolResult { get; set; } = HipError.Success;

    internal HipError FreeResult { get; set; } = HipError.Success;

    internal HipError MemoryInfoResult { get; set; } = HipError.Success;

    internal HipError PitchedAllocationResult { get; set; } = HipError.Success;

    internal HipError MemsetResult { get; set; } = HipError.Success;

    internal HipError PitchedCopyResult { get; set; } = HipError.Success;

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

    internal bool ReturnMemoryPoolOnFailure { get; set; }

    internal bool ReturnNullMemoryPoolOnSuccess { get; set; }

    internal bool ReturnPoolPointerOnFailure { get; set; }

    internal bool ReturnNullPoolPointerOnSuccess { get; set; }

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

    internal int FreeCallCount { get; private set; }

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

    internal int MemoryPoolCreateCount { get; private set; }

    internal int MemoryPoolDestroyCount { get; private set; }

    internal int MemoryPoolTrimCount { get; private set; }

    internal int MemoryPoolSetAttributeCount { get; private set; }

    internal int MemoryPoolGetAttributeCount { get; private set; }

    internal int MemoryPoolSetAccessCount { get; private set; }

    internal int MemoryPoolGetAccessCount { get; private set; }

    internal int PoolAllocationCount { get; private set; }

    internal int PendingPoolAllocationCount { get; private set; }

    internal IntPtr LastPoolHandle { get; private set; }

    internal IntPtr LastPoolAllocationStream { get; private set; }

    internal ulong LastPoolAllocationBytes { get; private set; }

    internal ulong LastPoolMaximumSizeBytes { get; private set; }

    internal int MemAdviseCount { get; private set; }

    internal int MemPrefetchCount { get; private set; }

    internal int PeerEnableCount { get; private set; }

    internal int PeerDisableCount { get; private set; }

    internal int PeerCopyCount { get; private set; }

    internal int GraphDestroyCount { get; private set; }

    internal int GraphExecDestroyCount { get; private set; }

    internal int GraphLaunchCount { get; private set; }

    internal ulong FreeMemoryBytes { get; set; } = 768UL * 1024 * 1024;

    internal ulong TotalMemoryBytes { get; set; } = 1024UL * 1024 * 1024;

    internal ulong LastAllocationWidthBytes { get; private set; }

    internal ulong LastAllocationHeight { get; private set; }

    internal ulong LastAllocationDepth { get; private set; }

    internal ulong LastAllocationPitch { get; private set; }

    internal ulong? ForcedAllocationPitch { get; set; }

    internal ulong? ForcedAllocationYSize { get; set; }

    internal int LastMemsetValue { get; private set; }

    internal HipExtent LastMemsetExtent { get; private set; }

    internal HipMemoryCopyKind LastPitchedCopyKind { get; private set; }

    internal ulong LastCopyWidthBytes { get; private set; }

    internal ulong LastCopyHeight { get; private set; }

    internal ulong LastCopyDepth { get; private set; }

    internal ulong LastSourcePitch { get; private set; }

    internal ulong LastDestinationPitch { get; private set; }

    internal IntPtr LastPitchedStream { get; private set; }

    internal HipMemcpy3DParameters LastMemcpy3DParameters { get; private set; }

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

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes)
    {
        freeBytes = ToUIntPtr(FreeMemoryBytes);
        totalBytes = ToUIntPtr(TotalMemoryBytes);
        return MemoryInfoResult;
    }

    public HipError MallocPitch(out IntPtr pointer, out UIntPtr pitch, UIntPtr widthBytes, UIntPtr height)
    {
        LastAllocationWidthBytes = widthBytes.ToUInt64();
        LastAllocationHeight = height.ToUInt64();
        LastAllocationDepth = 1;
        LastAllocationPitch = ForcedAllocationPitch ?? Align(LastAllocationWidthBytes, 16);
        pitch = ToUIntPtr(LastAllocationPitch);
        if (PitchedAllocationResult != HipError.Success)
        {
            pointer = IntPtr.Zero;
            return PitchedAllocationResult;
        }
        return Malloc(out pointer, ToUIntPtr(checked(LastAllocationPitch * LastAllocationHeight)));
    }

    public HipError Malloc3D(out HipPitchedPtr pitchedPointer, HipExtent extent)
    {
        LastAllocationWidthBytes = extent.Width.ToUInt64();
        LastAllocationHeight = extent.Height.ToUInt64();
        LastAllocationDepth = extent.Depth.ToUInt64();
        LastAllocationPitch = ForcedAllocationPitch ?? Align(LastAllocationWidthBytes, 16);
        if (PitchedAllocationResult != HipError.Success)
        {
            pitchedPointer = default;
            return PitchedAllocationResult;
        }
        HipError result = Malloc(out IntPtr pointer, ToUIntPtr(checked(checked(LastAllocationPitch * LastAllocationHeight) * LastAllocationDepth)));
        pitchedPointer = result == HipError.Success
            ? new HipPitchedPtr(pointer, ToUIntPtr(LastAllocationPitch), extent.Width, ToUIntPtr(ForcedAllocationYSize ?? LastAllocationHeight))
            : default;
        return result;
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
                if (_allocationPools.TryGetValue(pointer, out IntPtr pool))
                {
                    _poolAttributes[pool][(int)HipMemoryPoolAttributeNative.UsedMemCurrent] -= (ulong)_allocations[pointer];
                    _allocationPools.Remove(pointer);
                }
                Free(pointer);
                AsyncFreeCount++;
            }
        });
        return HipError.Success;
    }

    public HipError DeviceGetDefaultMemPool(out IntPtr memoryPool, int deviceOrdinal)
    {
        if (deviceOrdinal < 0 || deviceOrdinal >= 2)
        {
            memoryPool = IntPtr.Zero;
            return HipError.InvalidDevice;
        }
        if (!_defaultMemoryPools.TryGetValue(deviceOrdinal, out memoryPool))
        {
            memoryPool = new IntPtr(0xA000 + deviceOrdinal);
            _defaultMemoryPools.Add(deviceOrdinal, memoryPool);
            _memoryPools.Add(memoryPool);
            _poolAttributes[memoryPool] = DefaultPoolAttributes();
            _currentMemoryPools[deviceOrdinal] = memoryPool;
        }
        return HipError.Success;
    }

    public HipError DeviceGetMemPool(out IntPtr memoryPool, int deviceOrdinal)
    {
        HipError error = DeviceGetDefaultMemPool(out memoryPool, deviceOrdinal);
        if (error != HipError.Success) return error;
        if (_currentMemoryPools.TryGetValue(deviceOrdinal, out IntPtr current)) memoryPool = current;
        return HipError.Success;
    }

    public HipError DeviceSetMemPool(int deviceOrdinal, IntPtr memoryPool)
    {
        if (deviceOrdinal < 0 || deviceOrdinal >= 2 || !_memoryPools.Contains(memoryPool)) return HipError.InvalidValue;
        if (DeviceSetMemoryPoolResult != HipError.Success) return DeviceSetMemoryPoolResult;
        _currentMemoryPools[deviceOrdinal] = memoryPool;
        return HipError.Success;
    }

    public HipError MemPoolCreate(out IntPtr memoryPool, ref HipMemoryPoolPropertiesNative properties)
    {
        memoryPool = IntPtr.Zero;
        if (properties.AllocationType != 1 || properties.HandleTypes != 0 || properties.Location.Type != 1 || properties.Location.Id < 0 || properties.Location.Id >= 2)
            return HipError.InvalidValue;
        LastPoolMaximumSizeBytes = properties.MaximumSize.ToUInt64();
        memoryPool = new IntPtr(_nextPool++);
        _memoryPools.Add(memoryPool);
        _poolAttributes[memoryPool] = DefaultPoolAttributes();
        _poolAccess[(memoryPool, properties.Location.Id)] = HipMemoryPoolAccess.ReadWrite;
        if (MemoryPoolCreateResult != HipError.Success)
        {
            if (!ReturnMemoryPoolOnFailure)
            {
                _memoryPools.Remove(memoryPool);
                _poolAttributes.Remove(memoryPool);
                memoryPool = IntPtr.Zero;
            }
            return MemoryPoolCreateResult;
        }
        if (ReturnNullMemoryPoolOnSuccess)
        {
            _memoryPools.Remove(memoryPool);
            _poolAttributes.Remove(memoryPool);
            memoryPool = IntPtr.Zero;
            return HipError.Success;
        }
        MemoryPoolCreateCount++;
        return HipError.Success;
    }

    public HipError MemPoolDestroy(IntPtr memoryPool)
    {
        if (MemoryPoolDestroyResult != HipError.Success) return MemoryPoolDestroyResult;
        if (!_memoryPools.Contains(memoryPool)) return HipError.InvalidValue;
        foreach (KeyValuePair<int, IntPtr> pair in _currentMemoryPools)
        {
            if (pair.Value == memoryPool) return HipError.InvalidValue;
        }
        _memoryPools.Remove(memoryPool);
        _poolAttributes.Remove(memoryPool);
        MemoryPoolDestroyCount++;
        return HipError.Success;
    }

    public HipError MemPoolTrimTo(IntPtr memoryPool, UIntPtr minimumBytesToKeep)
    {
        if (!_memoryPools.Contains(memoryPool)) return HipError.InvalidValue;
        if (MemoryPoolTrimResult != HipError.Success) return MemoryPoolTrimResult;
        ulong minimum = minimumBytesToKeep.ToUInt64();
        Dictionary<int, ulong> attributes = _poolAttributes[memoryPool];
        ulong floor = Math.Max(minimum, attributes[(int)HipMemoryPoolAttributeNative.UsedMemCurrent]);
        if (attributes[(int)HipMemoryPoolAttributeNative.ReservedMemCurrent] > floor)
            attributes[(int)HipMemoryPoolAttributeNative.ReservedMemCurrent] = floor;
        MemoryPoolTrimCount++;
        return HipError.Success;
    }

    public unsafe HipError MemPoolGetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value)
    {
        MemoryPoolGetAttributeCount++;
        if (!_memoryPools.Contains(memoryPool) || value == IntPtr.Zero) return HipError.InvalidValue;
        if (MemoryPoolAttributeResult != HipError.Success) return MemoryPoolAttributeResult;
        ulong stored = _poolAttributes[memoryPool][(int)attribute];
        if ((int)attribute <= 3) Marshal.WriteInt32(value, stored == 0 ? 0 : 1);
        else Marshal.WriteInt64(value, unchecked((long)stored));
        return HipError.Success;
    }

    public unsafe HipError MemPoolSetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value)
    {
        MemoryPoolSetAttributeCount++;
        if (!_memoryPools.Contains(memoryPool) || value == IntPtr.Zero) return HipError.InvalidValue;
        if (MemoryPoolAttributeResult != HipError.Success) return MemoryPoolAttributeResult;
        ulong next = (int)attribute <= 3 ? (uint)Marshal.ReadInt32(value) : unchecked((ulong)Marshal.ReadInt64(value));
        if ((int)attribute <= 3 && next > 1) return HipError.InvalidValue;
        if (((int)attribute == 6 || (int)attribute == 8) && next != 0) return HipError.InvalidValue;
        _poolAttributes[memoryPool][(int)attribute] = next;
        return HipError.Success;
    }

    public unsafe HipError MemPoolSetAccess(IntPtr memoryPool, HipMemoryPoolAccessDescriptorNative[] descriptors)
    {
        MemoryPoolSetAccessCount++;
        if (!_memoryPools.Contains(memoryPool) || descriptors.Length == 0) return HipError.InvalidValue;
        if (MemoryPoolAccessResult != HipError.Success) return MemoryPoolAccessResult;
        foreach (HipMemoryPoolAccessDescriptorNative descriptor in descriptors)
        {
            if (descriptor.Location.Type != 1 || descriptor.Location.Id < 0 || descriptor.Location.Id >= 2) return HipError.InvalidDevice;
            if (descriptor.Access != HipMemoryPoolAccess.None && descriptor.Access != HipMemoryPoolAccess.ReadWrite) return HipError.InvalidValue;
            _poolAccess[(memoryPool, descriptor.Location.Id)] = descriptor.Access;
        }
        return HipError.Success;
    }

    public HipError MemPoolGetAccess(out HipMemoryPoolAccess access, IntPtr memoryPool, ref HipMemLocation location)
    {
        MemoryPoolGetAccessCount++;
        access = HipMemoryPoolAccess.None;
        if (!_memoryPools.Contains(memoryPool) || location.Type != 1 || location.Id < 0 || location.Id >= 2) return HipError.InvalidValue;
        if (MemoryPoolAccessResult != HipError.Success) return MemoryPoolAccessResult;
        _poolAccess.TryGetValue((memoryPool, location.Id), out access);
        return HipError.Success;
    }

    public HipError MallocFromPoolAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr memoryPool, IntPtr stream)
    {
        LastPoolHandle = memoryPool;
        LastPoolAllocationStream = stream;
        LastPoolAllocationBytes = byteCount.ToUInt64();
        pointer = IntPtr.Zero;
        if (!_memoryPools.Contains(memoryPool) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (MallocFromPoolResult != HipError.Success)
        {
            if (ReturnPoolPointerOnFailure) pointer = AllocateRaw(byteCount);
            return MallocFromPoolResult;
        }
        if (ReturnNullPoolPointerOnSuccess) return HipError.Success;
        HipError error = Malloc(out pointer, byteCount);
        if (error == HipError.Success)
        {
            _allocationPools[pointer] = memoryPool;
            _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.UsedMemCurrent] += byteCount.ToUInt64();
            _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.UsedMemHigh] = Math.Max(
                _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.UsedMemHigh],
                _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.UsedMemCurrent]);
            _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.ReservedMemCurrent] += byteCount.ToUInt64();
            _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.ReservedMemHigh] = Math.Max(
                _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.ReservedMemHigh],
                _poolAttributes[memoryPool][(int)HipMemoryPoolAttributeNative.ReservedMemCurrent]);
            PoolAllocationCount++;
            PendingPoolAllocationCount++;
            QueueStreamAction(stream, () => PendingPoolAllocationCount--);
        }
        return error;
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
        FreeCallCount++;
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

    public HipError Memset(IntPtr destination, int value, UIntPtr byteCount)
    {
        LastMemsetValue = value;
        LastMemsetExtent = new HipExtent(byteCount, new UIntPtr(1), new UIntPtr(1));
        if (MemsetResult != HipError.Success) return MemsetResult;
        Fill(destination, checked((int)byteCount.ToUInt64()), unchecked((byte)value));
        return HipError.Success;
    }

    public HipError MemsetAsync(IntPtr destination, int value, UIntPtr byteCount, IntPtr stream)
    {
        LastPitchedStream = stream;
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return Memset(destination, value, byteCount);
    }

    public HipError Memset2D(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height)
    {
        LastMemsetValue = value;
        LastMemsetExtent = new HipExtent(widthBytes, height, new UIntPtr(1));
        if (MemsetResult != HipError.Success) return MemsetResult;
        int rowPitch = checked((int)pitch.ToUInt64());
        int rowWidth = checked((int)widthBytes.ToUInt64());
        int rows = checked((int)height.ToUInt64());
        for (int y = 0; y < rows; y++) Fill(Add(destination, checked(y * rowPitch)), rowWidth, unchecked((byte)value));
        return HipError.Success;
    }

    public HipError Memset2DAsync(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height, IntPtr stream)
    {
        LastPitchedStream = stream;
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return Memset2D(destination, pitch, value, widthBytes, height);
    }

    public HipError Memset3D(HipPitchedPtr destination, int value, HipExtent extent)
    {
        LastMemsetValue = value;
        LastMemsetExtent = extent;
        if (MemsetResult != HipError.Success) return MemsetResult;
        int pitch = checked((int)destination.Pitch.ToUInt64());
        int slicePitch = checked(pitch * checked((int)destination.YSize.ToUInt64()));
        int width = checked((int)extent.Width.ToUInt64());
        int height = checked((int)extent.Height.ToUInt64());
        int depth = checked((int)extent.Depth.ToUInt64());
        for (int z = 0; z < depth; z++)
        for (int y = 0; y < height; y++)
            Fill(Add(destination.Address, checked(z * slicePitch + y * pitch)), width, unchecked((byte)value));
        return HipError.Success;
    }

    public HipError Memset3DAsync(HipPitchedPtr destination, int value, HipExtent extent, IntPtr stream)
    {
        LastPitchedStream = stream;
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return Memset3D(destination, value, extent);
    }

    public HipError Memcpy2D(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind)
    {
        LastPitchedCopyKind = kind;
        LastCopyWidthBytes = widthBytes.ToUInt64();
        LastCopyHeight = height.ToUInt64();
        LastCopyDepth = 1;
        LastSourcePitch = sourcePitch.ToUInt64();
        LastDestinationPitch = destinationPitch.ToUInt64();
        if (PitchedCopyResult != HipError.Success) return PitchedCopyResult;
        int width = checked((int)LastCopyWidthBytes);
        int rows = checked((int)LastCopyHeight);
        int sourceRowPitch = checked((int)LastSourcePitch);
        int destinationRowPitch = checked((int)LastDestinationPitch);
        for (int y = 0; y < rows; y++)
            CopyBytes(Add(destination, checked(y * destinationRowPitch)), Add(source, checked(y * sourceRowPitch)), width);
        return HipError.Success;
    }

    public HipError Memcpy2DAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind, IntPtr stream)
    {
        LastPitchedStream = stream;
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return Memcpy2D(destination, destinationPitch, source, sourcePitch, widthBytes, height, kind);
    }

    public HipError Memcpy3D(ref HipMemcpy3DParameters parameters)
    {
        LastMemcpy3DParameters = parameters;
        LastPitchedCopyKind = parameters.Kind;
        LastCopyWidthBytes = parameters.Extent.Width.ToUInt64();
        LastCopyHeight = parameters.Extent.Height.ToUInt64();
        LastCopyDepth = parameters.Extent.Depth.ToUInt64();
        LastSourcePitch = parameters.SourcePointer.Pitch.ToUInt64();
        LastDestinationPitch = parameters.DestinationPointer.Pitch.ToUInt64();
        if (PitchedCopyResult != HipError.Success) return PitchedCopyResult;

        int width = checked((int)LastCopyWidthBytes);
        int height = checked((int)LastCopyHeight);
        int depth = checked((int)LastCopyDepth);
        int sourcePitch = checked((int)LastSourcePitch);
        int destinationPitch = checked((int)LastDestinationPitch);
        int sourceSlicePitch = checked(sourcePitch * checked((int)parameters.SourcePointer.YSize.ToUInt64()));
        int destinationSlicePitch = checked(destinationPitch * checked((int)parameters.DestinationPointer.YSize.ToUInt64()));
        int sourceBase = PositionOffset(parameters.SourcePosition, sourcePitch, sourceSlicePitch);
        int destinationBase = PositionOffset(parameters.DestinationPosition, destinationPitch, destinationSlicePitch);
        for (int z = 0; z < depth; z++)
        for (int y = 0; y < height; y++)
            CopyBytes(
                Add(parameters.DestinationPointer.Address, checked(destinationBase + z * destinationSlicePitch + y * destinationPitch)),
                Add(parameters.SourcePointer.Address, checked(sourceBase + z * sourceSlicePitch + y * sourcePitch)),
                width);
        return HipError.Success;
    }

    public HipError Memcpy3DAsync(ref HipMemcpy3DParameters parameters, IntPtr stream)
    {
        LastPitchedStream = stream;
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return Memcpy3D(ref parameters);
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

    private static ulong Align(ulong value, ulong alignment) => checked((value + alignment - 1) / alignment * alignment);

    private static Dictionary<int, ulong> DefaultPoolAttributes() => new()
    {
        [(int)HipMemoryPoolAttributeNative.ReuseFollowEventDependencies] = 1,
        [(int)HipMemoryPoolAttributeNative.ReuseAllowOpportunistic] = 1,
        [(int)HipMemoryPoolAttributeNative.ReuseAllowInternalDependencies] = 1,
        [(int)HipMemoryPoolAttributeNative.ReleaseThreshold] = 0,
        [(int)HipMemoryPoolAttributeNative.ReservedMemCurrent] = 0,
        [(int)HipMemoryPoolAttributeNative.ReservedMemHigh] = 0,
        [(int)HipMemoryPoolAttributeNative.UsedMemCurrent] = 0,
        [(int)HipMemoryPoolAttributeNative.UsedMemHigh] = 0,
    };

    private static UIntPtr ToUIntPtr(ulong value) => UIntPtr.Size == 4 ? new UIntPtr(checked((uint)value)) : new UIntPtr(value);

    private static IntPtr Add(IntPtr pointer, int offset) => new(pointer.ToInt64() + offset);

    private static void Fill(IntPtr destination, int count, byte value)
    {
        for (int index = 0; index < count; index++) Marshal.WriteByte(destination, index, value);
    }

    private static void CopyBytes(IntPtr destination, IntPtr source, int count)
    {
        var bytes = new byte[count];
        Marshal.Copy(source, bytes, 0, count);
        Marshal.Copy(bytes, 0, destination, count);
    }

    private static int PositionOffset(HipPos position, int pitch, int slicePitch) => checked(
        checked((int)position.X.ToUInt64()) +
        checked((int)position.Y.ToUInt64()) * pitch +
        checked((int)position.Z.ToUInt64()) * slicePitch);
}
