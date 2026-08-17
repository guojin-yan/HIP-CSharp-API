using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Interop;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.UnitTests;

internal sealed class FakeHipNativeApi : IHipNativeApi, IDisposable
{
    private readonly Dictionary<IntPtr, int> _allocations = new();
    private readonly HashSet<IntPtr> _streams = new();
    private readonly HashSet<IntPtr> _events = new();
    private readonly HashSet<IntPtr> _graphs = new();
    private readonly HashSet<IntPtr> _graphExecs = new();
    private readonly Dictionary<IntPtr, FakeGraphState> _graphStates = new();
    private readonly Dictionary<IntPtr, FakeGraphState> _graphExecStates = new();
    private readonly Dictionary<IntPtr, int> _graphAllocationPointers = new();
    private readonly HashSet<IntPtr> _activeGraphAllocations = new();
    private readonly HashSet<IntPtr> _capturingStreams = new();
    private readonly Dictionary<IntPtr, List<Action>> _pendingStreamActions = new();
    private readonly HashSet<IntPtr> _memoryPools = new();
    private readonly Dictionary<int, IntPtr> _defaultMemoryPools = new();
    private readonly Dictionary<int, IntPtr> _currentMemoryPools = new();
    private readonly Dictionary<IntPtr, Dictionary<int, ulong>> _poolAttributes = new();
    private readonly Dictionary<(IntPtr Pool, int Device), HipMemoryPoolAccess> _poolAccess = new();
    private readonly Dictionary<IntPtr, IntPtr> _allocationPools = new();
    private readonly Dictionary<(IntPtr Module, string Name), (IntPtr Pointer, int Length)> _moduleGlobals = new();
    private readonly HashSet<IntPtr> _virtualAddresses = new();
    private readonly HashSet<IntPtr> _virtualAllocationHandles = new();
    private readonly Dictionary<IntPtr, FakeArrayState> _arrays = new();
    private readonly Dictionary<IntPtr, FakeMipmappedArrayState> _mipmappedArrays = new();
    private readonly Dictionary<ulong, FakeTextureState> _textureObjects = new();
    private readonly Dictionary<ulong, IntPtr> _surfaceObjects = new();
    private readonly Dictionary<IntPtr, FakeTextureReferenceState> _textureReferences = new();
    private int _nextPool = 0x9000;
    private int _nextGraph = 0x7000;
    private int _nextGraphExec = 0x8000;
    private int _nextGraphNode = 0xB000;
    private int _nextModule = 0x2000;
    private int _nextVirtualAddress = 0xC000;
    private int _nextVirtualAllocationHandle = 0xD000;
    private int _nextArray = 0xE100;
    private int _nextMipmappedArray = 0xF100;
    private ulong _nextTextureObject = 0x10100;
    private ulong _nextSurfaceObject = 0x11100;
    private int _nextTextureReference = 0x12100;

    internal HipError MallocResult { get; set; } = HipError.Success;

    internal HipError ModuleLoadResult { get; set; } = HipError.Success;

    internal HipError ModuleUnloadResult { get; set; } = HipError.Success;

    internal HipError ModuleGetFunctionResult { get; set; } = HipError.Success;

    internal HipError ModuleGetGlobalResult { get; set; } = HipError.Success;

    internal HipError MemcpyResult { get; set; } = HipError.Success;

    internal HipError MemcpyAsyncResult { get; set; } = HipError.Success;

    internal bool ReturnNullModuleGlobal { get; set; }

    internal bool ReturnZeroModuleGlobalSize { get; set; }

    internal bool ReturnOverflowModuleGlobalRange { get; set; }

    internal HipError ModuleLaunchResult { get; set; } = HipError.Success;

    internal HipError FunctionAttributeResult { get; set; } = HipError.Success;

    internal HipError OccupancyResult { get; set; } = HipError.Success;

    internal HipError CooperativeLaunchResult { get; set; } = HipError.Success;

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

    internal HipError ManagedNextResult { get; set; } = HipError.Success;

    internal HipError ArrayTextureResult { get; set; } = HipError.Success;

    internal HipError ArrayTextureReleaseResult { get; set; } = HipError.Success;

    internal bool ReturnArrayHandleOnFailure { get; set; }

    internal bool ReturnTextureObjectOnFailure { get; set; }

    internal HipError PitchedAllocationResult { get; set; } = HipError.Success;

    internal HipError MemsetResult { get; set; } = HipError.Success;

    internal HipError PitchedCopyResult { get; set; } = HipError.Success;

    internal HipError PeerEnableResult { get; set; } = HipError.Success;

    internal HipError PeerDisableResult { get; set; } = HipError.Success;

    internal HipError PeerCopyResult { get; set; } = HipError.Success;

    internal HipError BeginCaptureResult { get; set; } = HipError.Success;

    internal HipError EndCaptureResult { get; set; } = HipError.Success;

    internal HipError GraphInstantiateResult { get; set; } = HipError.Success;

    internal HipError GraphCreateResult { get; set; } = HipError.Success;

    internal HipError GraphNodeAddResult { get; set; } = HipError.Success;

    internal HipError GraphDependencyResult { get; set; } = HipError.Success;

    internal HipError GraphUploadResult { get; set; } = HipError.Success;

    internal HipError GraphNodeUpdateResult { get; set; } = HipError.Success;

    internal HipError GraphLaunchResult { get; set; } = HipError.Success;

    internal HipError GraphDestroyResult { get; set; } = HipError.Success;

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

    internal bool ReturnGraphOnCreateFailure { get; set; }

    internal bool ReturnNodeOnAddFailure { get; set; }

    internal bool ReturnNullGraphOnCreateSuccess { get; set; }

    internal bool ReturnNullNodeOnAddSuccess { get; set; }

    internal HipError EventSynchronizeResult { get; set; } = HipError.Success;

    internal HipError EventQueryResult { get; set; } = HipError.Success;

    internal IList<bool> ExpectedKernelPointerArguments { get; } = new List<bool>();

    internal IList<long> LastKernelArgumentValues { get; } = new List<long>();

    internal IDictionary<HipFunctionAttributeNative, int> FunctionAttributes { get; } =
        new Dictionary<HipFunctionAttributeNative, int>
        {
            [HipFunctionAttributeNative.MaxThreadsPerBlock] = 1024,
            [HipFunctionAttributeNative.SharedSizeBytes] = 2048,
            [HipFunctionAttributeNative.ConstantSizeBytes] = 128,
            [HipFunctionAttributeNative.LocalSizeBytes] = 32,
            [HipFunctionAttributeNative.NumberOfRegisters] = 24,
            [HipFunctionAttributeNative.BinaryVersion] = 1100,
            [HipFunctionAttributeNative.MaxDynamicSharedSizeBytes] = 65536,
        };

    internal IDictionary<HipFunctionAttributeNative, HipError> FunctionAttributeResults { get; } =
        new Dictionary<HipFunctionAttributeNative, HipError>();

    internal IList<HipFunctionAttributeNative> FunctionAttributeCalls { get; } = new List<HipFunctionAttributeNative>();

    internal int ActiveBlocksPerMultiprocessor { get; set; } = 4;

    internal int PotentialMinimumGridSize { get; set; } = 80;

    internal int PotentialBlockSize { get; set; } = 256;

    internal int MultiprocessorCountValue { get; set; } = 20;

    internal int WarpSizeValue { get; set; } = 64;

    internal int CooperativeLaunchCapability { get; set; } = 1;

    internal int LastOccupancyBlockSize { get; private set; }

    internal ulong LastOccupancyDynamicSharedMemoryBytes { get; private set; }

    internal int LastOccupancyBlockSizeLimit { get; private set; }

    internal int LastPotentialBlockSizeLimit { get; private set; }

    internal uint LastOccupancyFlags { get; private set; }

    internal int OccupancyNonFlagsCallCount { get; private set; }

    internal int OccupancyFlagsCallCount { get; private set; }

    internal byte[] LastModuleCodeObject { get; private set; } = Array.Empty<byte>();

    internal string LastKernelName { get; private set; } = string.Empty;

    internal string LastModuleGlobalName { get; private set; } = string.Empty;

    internal IntPtr LastModuleGlobalModule { get; private set; }

    internal int ModuleGetGlobalCallCount { get; private set; }

    internal uint LastInitFlags { get; private set; }

    internal int LastSetDevice { get; private set; }

    internal int FreeCount { get; private set; }

    internal int FreeCallCount { get; private set; }

    internal int ArrayFreeCallCount { get; private set; }

    internal int MipmappedArrayFreeCallCount { get; private set; }

    internal int ArrayCopyCallCount { get; private set; }

    internal int TextureObjectDestroyCount { get; private set; }

    internal int SurfaceObjectDestroyCount { get; private set; }

    internal int TextureUnbindCount { get; private set; }

    internal int SynchronizeCount { get; private set; }

    internal int ModuleUnloadCount { get; private set; }

    internal int ModuleLaunchCount { get; private set; }

    internal int CooperativeLaunchCount { get; private set; }

    internal IntPtr LastLaunchedFunction { get; private set; }

    internal uint LastGridX { get; private set; }

    internal uint LastGridY { get; private set; }

    internal uint LastGridZ { get; private set; }

    internal uint LastBlockX { get; private set; }

    internal uint LastBlockY { get; private set; }

    internal uint LastBlockZ { get; private set; }

    internal uint LastLaunchSharedMemoryBytes { get; private set; }

    internal IntPtr LastLaunchStream { get; private set; }

    internal int AsyncCopyCount { get; private set; }

    internal int MemcpyCallCount { get; private set; }

    internal int MemcpyAsyncCallCount { get; private set; }

    internal HipMemoryCopyKind LastMemcpyKind { get; private set; }

    internal ulong LastMemcpyByteCount { get; private set; }

    internal IntPtr LastMemcpyStream { get; private set; }

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

    internal int GraphCreateCount { get; private set; }

    internal int GraphNodeCreateCount { get; private set; }

    internal int GraphNodeDestroyCount { get; private set; }

    internal int GraphUploadCount { get; private set; }

    internal int GraphNodeUpdateCount { get; private set; }

    internal int GraphAllocationExecutionCount { get; private set; }

    internal int GraphFreeExecutionCount { get; private set; }

    internal IList<string> LastGraphExecutionTrace { get; } = new List<string>();

    internal int ActiveGraphAllocationCount => _activeGraphAllocations.Count;

    internal int LastGraphNodeCount => _graphStates.Count == 0 ? 0 : _graphStates.Values.Last().Nodes.Count;

    internal int LastGraphEdgeCount => _graphStates.Count == 0 ? 0 : _graphStates.Values.Last().EdgeCount;

    internal int MaximumGraphNodeCount => _graphStates.Count == 0 ? 0 : _graphStates.Values.Max(state => state.Nodes.Count);

    internal int MaximumGraphEdgeCount => _graphStates.Count == 0 ? 0 : _graphStates.Values.Max(state => state.EdgeCount);

    internal HipKernelNodeParameters LastGraphKernelParameters { get; private set; }

    internal IList<long> LastGraphKernelArgumentValues { get; } = new List<long>();

    internal ulong LastGraphCopyBytes { get; private set; }

    internal HipMemoryCopyKind LastGraphCopyKind { get; private set; }

    internal HipMemsetNodeParameters LastGraphMemsetParameters { get; private set; }

    internal ulong LastGraphAllocationBytes { get; private set; }

    internal int LastGraphAllocationAccessCount { get; private set; }

    internal int LastGraphAllocationDevice { get; private set; }

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
        value = attribute switch
        {
            HipDeviceAttribute.MaxThreadsPerBlock => 1024,
            HipDeviceAttribute.CooperativeLaunch => CooperativeLaunchCapability,
            HipDeviceAttribute.MultiprocessorCount => MultiprocessorCountValue,
            HipDeviceAttribute.WarpSize => WarpSizeValue,
            _ => 0,
        };
        return deviceId >= 0 && deviceId < 2 ? HipError.Success : HipError.InvalidDevice;
    }

    public HipError DeviceComputeCapability(IntPtr major, IntPtr minor, int device)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success)
        {
            Marshal.WriteInt32(major, 9);
            Marshal.WriteInt32(minor, 0);
        }
        return ManagedNextResult;
    }

    public HipError DeviceGet(IntPtr device, int ordinal)
    {
        if (ordinal < 0 || ordinal >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(device, ordinal);
        return ManagedNextResult;
    }

    public HipError DeviceGetByPCIBusId(IntPtr device, IntPtr pciBusId)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(device, 0);
        return ManagedNextResult;
    }

    public HipError DeviceGetCacheConfig(IntPtr cacheConfig)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(cacheConfig, 0);
        return ManagedNextResult;
    }

    public HipError DeviceGetGraphMemAttribute(int device, int attribute, IntPtr value)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt64(value, 0);
        return ManagedNextResult;
    }

    public HipError DeviceGetLimit(IntPtr value, int limit)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(value, new IntPtr(4096));
        return ManagedNextResult;
    }

    public HipError DeviceGetP2PAttribute(IntPtr value, int attribute, int sourceDevice, int destinationDevice)
    {
        if (sourceDevice < 0 || sourceDevice >= 2 || destinationDevice < 0 || destinationDevice >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(value, 1);
        return ManagedNextResult;
    }

    public HipError DeviceGetPCIBusId(IntPtr pciBusId, int length, int device)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (length <= 0) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success)
        {
            byte[] value = System.Text.Encoding.ASCII.GetBytes("0000:00:00.0\0");
            Marshal.Copy(value, 0, pciBusId, Math.Min(length, value.Length));
        }
        return ManagedNextResult;
    }

    public HipError DeviceGetSharedMemConfig(IntPtr config)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(config, 0);
        return ManagedNextResult;
    }

    public HipError DeviceGetStreamPriorityRange(IntPtr leastPriority, IntPtr greatestPriority)
    {
        if (ManagedNextResult == HipError.Success)
        {
            Marshal.WriteInt32(leastPriority, 0);
            Marshal.WriteInt32(greatestPriority, -1);
        }
        return ManagedNextResult;
    }

    public HipError DeviceGetUuid(IntPtr uuid, int device)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success) Marshal.Copy(new byte[16], 0, uuid, 16);
        return ManagedNextResult;
    }

    public HipError DeviceTotalMem(IntPtr bytes, int device)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(bytes, new IntPtr(8 * 1024 * 1024));
        return ManagedNextResult;
    }

    public HipError GetSymbolAddress(IntPtr devicePointer, IntPtr symbol)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(devicePointer, new IntPtr(0x1234));
        return ManagedNextResult;
    }

    public HipError GetSymbolSize(IntPtr size, IntPtr symbol)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(size, new IntPtr(128));
        return ManagedNextResult;
    }

    public HipError PointerGetAttribute(IntPtr data, int attribute, IntPtr pointer)
    {
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt64(data, 0);
        return ManagedNextResult;
    }

    public HipError PointerGetAttributes(IntPtr attributes, IntPtr pointer)
    {
        if (ManagedNextResult == HipError.Success) Marshal.Copy(new byte[32], 0, attributes, 32);
        return ManagedNextResult;
    }

    public HipError PointerSetAttribute(IntPtr value, int attribute, IntPtr pointer) => ManagedNextResult;

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes)
    {
        freeBytes = ToUIntPtr(FreeMemoryBytes);
        totalBytes = ToUIntPtr(TotalMemoryBytes);
        return MemoryInfoResult;
    }

    public HipError MemAddressFree(IntPtr address, UIntPtr size)
    {
        if (!_virtualAddresses.Contains(address)) return HipError.InvalidValue;
        if (ManagedNextResult != HipError.Success) return ManagedNextResult;
        _virtualAddresses.Remove(address);
        return HipError.Success;
    }

    public HipError MemAddressReserve(IntPtr address, UIntPtr size, UIntPtr alignment, IntPtr requestedAddress, ulong flags)
    {
        if (size == UIntPtr.Zero) return HipError.InvalidValue;
        if (ManagedNextResult != HipError.Success) return ManagedNextResult;
        IntPtr reserved = requestedAddress == IntPtr.Zero ? new IntPtr(_nextVirtualAddress++) : requestedAddress;
        if (!_virtualAddresses.Add(reserved)) return HipError.InvalidValue;
        Marshal.WriteIntPtr(address, reserved);
        return HipError.Success;
    }

    public HipError MemCreate(IntPtr handle, UIntPtr size, IntPtr properties, ulong flags)
    {
        if (size == UIntPtr.Zero) return HipError.InvalidValue;
        if (ManagedNextResult != HipError.Success) return ManagedNextResult;
        IntPtr allocation = new IntPtr(_nextVirtualAllocationHandle++);
        _virtualAllocationHandles.Add(allocation);
        Marshal.WriteIntPtr(handle, allocation);
        return HipError.Success;
    }

    public HipError MemExportToShareableHandle(IntPtr shareableHandle, IntPtr handle, int handleType, ulong flags)
    {
        if (!_virtualAllocationHandles.Contains(handle)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(shareableHandle, new IntPtr(0xE000));
        return ManagedNextResult;
    }

    public HipError MemGetAccess(IntPtr flags, IntPtr location, IntPtr address)
    {
        if (!_virtualAddresses.Contains(address)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt64(flags, 3);
        return ManagedNextResult;
    }

    public HipError MemImportFromShareableHandle(IntPtr handle, IntPtr operatingSystemHandle, int handleType)
    {
        if (operatingSystemHandle == IntPtr.Zero) return HipError.InvalidValue;
        IntPtr allocation = new IntPtr(_nextVirtualAllocationHandle++);
        _virtualAllocationHandles.Add(allocation);
        if (ManagedNextResult == HipError.Success) Marshal.WriteIntPtr(handle, allocation);
        return ManagedNextResult;
    }

    public HipError MemMap(IntPtr address, UIntPtr size, UIntPtr offset, IntPtr handle, ulong flags)
    {
        if (!_virtualAddresses.Contains(address) || !_virtualAllocationHandles.Contains(handle)) return HipError.InvalidValue;
        return ManagedNextResult;
    }

    public HipError MemMapArrayAsync(IntPtr mapInformation, uint count, IntPtr stream)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return ManagedNextResult;
    }

    public HipError MemRelease(IntPtr handle)
    {
        if (!_virtualAllocationHandles.Contains(handle)) return HipError.InvalidValue;
        if (ManagedNextResult != HipError.Success) return ManagedNextResult;
        _virtualAllocationHandles.Remove(handle);
        return HipError.Success;
    }

    public HipError MemRetainAllocationHandle(IntPtr handle, IntPtr address)
    {
        if (!_virtualAddresses.Contains(address)) return HipError.InvalidValue;
        if (ManagedNextResult != HipError.Success) return ManagedNextResult;
        IntPtr allocation = new IntPtr(_nextVirtualAllocationHandle++);
        _virtualAllocationHandles.Add(allocation);
        Marshal.WriteIntPtr(handle, allocation);
        return HipError.Success;
    }

    public HipError MemSetAccess(IntPtr address, UIntPtr size, IntPtr descriptors, UIntPtr count) =>
        _virtualAddresses.Contains(address) ? ManagedNextResult : HipError.InvalidValue;

    public HipError MemUnmap(IntPtr address, UIntPtr size) =>
        _virtualAddresses.Contains(address) ? ManagedNextResult : HipError.InvalidValue;

    public HipError Array3DCreate(IntPtr array, IntPtr descriptor)
    {
        HipArray3DDescriptorNative value = Marshal.PtrToStructure<HipArray3DDescriptorNative>(descriptor);
        return CreateArray(array, FakeArrayState.FromDriver(value), true);
    }

    public HipError Array3DGetDescriptor(IntPtr descriptor, IntPtr array)
    {
        if (!_arrays.TryGetValue(array, out FakeArrayState? state)) return HipError.InvalidValue;
        Marshal.StructureToPtr(state.Driver3DDescriptor, descriptor, false);
        return ArrayTextureResult;
    }

    public HipError ArrayCreate(IntPtr array, IntPtr descriptor)
    {
        HipArrayDescriptorNative value = Marshal.PtrToStructure<HipArrayDescriptorNative>(descriptor);
        return CreateArray(array, FakeArrayState.FromDriver(value), true);
    }

    public HipError ArrayDestroy(IntPtr array) => ReleaseArray(array);

    public HipError ArrayGetDescriptor(IntPtr descriptor, IntPtr array)
    {
        if (!_arrays.TryGetValue(array, out FakeArrayState? state)) return HipError.InvalidValue;
        Marshal.StructureToPtr(state.DriverDescriptor, descriptor, false);
        return ArrayTextureResult;
    }

    public HipError ArrayGetInfo(IntPtr descriptor, IntPtr extent, IntPtr flags, IntPtr array)
    {
        if (!_arrays.TryGetValue(array, out FakeArrayState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            Marshal.StructureToPtr(state.ChannelFormat, descriptor, false);
            Marshal.StructureToPtr(new HipExtent(ToUIntPtr(state.Width), ToUIntPtr(state.Height), ToUIntPtr(state.Depth)), extent, false);
            Marshal.WriteInt32(flags, unchecked((int)state.Flags));
        }
        return ArrayTextureResult;
    }

    public HipError BindTexture(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr size)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state) || !_allocations.ContainsKey(devicePointer)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            Marshal.WriteIntPtr(offset, IntPtr.Zero);
            state.BoundPointer = devicePointer;
            state.BoundArray = IntPtr.Zero;
            state.BoundMipmappedArray = IntPtr.Zero;
        }
        return ArrayTextureResult;
    }

    public HipError BindTexture2D(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr width, UIntPtr height, UIntPtr pitch) =>
        BindTexture(offset, textureReference, devicePointer, descriptor, ToUIntPtr(checked(pitch.ToUInt64() * height.ToUInt64())));

    public HipError BindTextureToArray(IntPtr textureReference, IntPtr array, IntPtr descriptor)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state) || !_arrays.ContainsKey(array)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            state.BoundArray = array;
            state.BoundMipmappedArray = IntPtr.Zero;
            state.BoundPointer = IntPtr.Zero;
        }
        return ArrayTextureResult;
    }

    public HipError BindTextureToMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, IntPtr descriptor)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state) || !_mipmappedArrays.ContainsKey(mipmappedArray)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            state.BoundMipmappedArray = mipmappedArray;
            state.BoundArray = IntPtr.Zero;
            state.BoundPointer = IntPtr.Zero;
        }
        return ArrayTextureResult;
    }

    public HipError CreateSurfaceObject(IntPtr surfaceObject, IntPtr resourceDescriptor)
    {
        HipResourceDescriptorNative resource = Marshal.PtrToStructure<HipResourceDescriptorNative>(resourceDescriptor);
        if (resource.ResourceType != HipTextureResourceKind.Array || !_arrays.ContainsKey(resource.Resource.Handle)) return HipError.InvalidValue;
        if (ArrayTextureResult != HipError.Success && !ReturnTextureObjectOnFailure) return ArrayTextureResult;
        ulong handle = _nextSurfaceObject++;
        _surfaceObjects.Add(handle, resource.Resource.Handle);
        Marshal.WriteInt64(surfaceObject, unchecked((long)handle));
        return ArrayTextureResult;
    }

    public HipError CreateTextureObject(IntPtr textureObject, IntPtr resourceDescriptor, IntPtr textureDescriptor, IntPtr resourceViewDescriptor)
    {
        HipResourceDescriptorNative resource = Marshal.PtrToStructure<HipResourceDescriptorNative>(resourceDescriptor);
        if (!IsKnownTextureResource(resource)) return HipError.InvalidValue;
        if (ArrayTextureResult != HipError.Success && !ReturnTextureObjectOnFailure) return ArrayTextureResult;
        ulong handle = _nextTextureObject++;
        var state = new FakeTextureState(
            resource,
            Marshal.PtrToStructure<HipTextureDescriptorNative>(textureDescriptor),
            resourceViewDescriptor == IntPtr.Zero ? default : Marshal.PtrToStructure<HipResourceViewDescriptorNative>(resourceViewDescriptor));
        _textureObjects.Add(handle, state);
        Marshal.WriteInt64(textureObject, unchecked((long)handle));
        return ArrayTextureResult;
    }

    public HipError DestroySurfaceObject(ulong surfaceObject)
    {
        if (!_surfaceObjects.ContainsKey(surfaceObject)) return HipError.InvalidValue;
        if (ArrayTextureReleaseResult != HipError.Success) return ArrayTextureReleaseResult;
        _surfaceObjects.Remove(surfaceObject);
        SurfaceObjectDestroyCount++;
        return HipError.Success;
    }

    public HipError DestroyTextureObject(ulong textureObject)
    {
        if (!_textureObjects.ContainsKey(textureObject)) return HipError.InvalidValue;
        if (ArrayTextureReleaseResult != HipError.Success) return ArrayTextureReleaseResult;
        _textureObjects.Remove(textureObject);
        TextureObjectDestroyCount++;
        return HipError.Success;
    }

    public HipError DeviceGetTexture1DLinearMaxWidth(IntPtr maxWidth, IntPtr descriptor, int device)
    {
        if (device < 0 || device >= 2) return HipError.InvalidDevice;
        if (ArrayTextureResult == HipError.Success) Marshal.WriteIntPtr(maxWidth, new IntPtr(1 << 20));
        return ArrayTextureResult;
    }

    public HipError FreeArray(IntPtr array) => ReleaseArray(array);

    public HipError FreeMipmappedArray(IntPtr mipmappedArray) => ReleaseMipmappedArray(mipmappedArray);

    public HipError GetMipmappedArrayLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level) =>
        GetMipmappedLevel(levelArray, mipmappedArray, level);

    public HipError GetTextureAlignmentOffset(IntPtr offset, IntPtr textureReference)
    {
        if (!_textureReferences.ContainsKey(textureReference)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.WriteIntPtr(offset, IntPtr.Zero);
        return ArrayTextureResult;
    }

    public HipError GetTextureObjectResourceDesc(IntPtr resourceDescriptor, ulong textureObject)
    {
        if (!_textureObjects.TryGetValue(textureObject, out FakeTextureState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.StructureToPtr(state.Resource, resourceDescriptor, false);
        return ArrayTextureResult;
    }

    public HipError GetTextureObjectResourceViewDesc(IntPtr resourceViewDescriptor, ulong textureObject)
    {
        if (!_textureObjects.TryGetValue(textureObject, out FakeTextureState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.StructureToPtr(state.ResourceView, resourceViewDescriptor, false);
        return ArrayTextureResult;
    }

    public HipError GetTextureObjectTextureDesc(IntPtr textureDescriptor, ulong textureObject)
    {
        if (!_textureObjects.TryGetValue(textureObject, out FakeTextureState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.StructureToPtr(state.Texture, textureDescriptor, false);
        return ArrayTextureResult;
    }

    public HipError GetTextureReference(IntPtr textureReference, IntPtr symbol)
    {
        if (symbol == IntPtr.Zero) return HipError.InvalidValue;
        if (ArrayTextureResult != HipError.Success) return ArrayTextureResult;
        IntPtr handle = new(_nextTextureReference++);
        _textureReferences.Add(handle, new FakeTextureReferenceState());
        Marshal.WriteIntPtr(textureReference, handle);
        return HipError.Success;
    }

    public HipError Malloc3DArray(IntPtr array, IntPtr descriptor, HipExtent extent, uint flags)
    {
        HipChannelFormatDescriptor format = Marshal.PtrToStructure<HipChannelFormatDescriptor>(descriptor);
        return CreateArray(array, FakeArrayState.FromRuntime(format, extent.Width.ToUInt64(), extent.Height.ToUInt64(), extent.Depth.ToUInt64(), (HipArrayFlags)flags), false);
    }

    public HipError MallocArray(IntPtr array, IntPtr descriptor, UIntPtr width, UIntPtr height, uint flags)
    {
        HipChannelFormatDescriptor format = Marshal.PtrToStructure<HipChannelFormatDescriptor>(descriptor);
        return CreateArray(array, FakeArrayState.FromRuntime(format, width.ToUInt64(), height.ToUInt64(), 0, (HipArrayFlags)flags), false);
    }

    public HipError MallocMipmappedArray(IntPtr mipmappedArray, IntPtr descriptor, HipExtent extent, uint levels, uint flags)
    {
        HipChannelFormatDescriptor format = Marshal.PtrToStructure<HipChannelFormatDescriptor>(descriptor);
        return CreateMipmappedArray(mipmappedArray, new FakeMipmappedArrayState(format, extent.Width.ToUInt64(), extent.Height.ToUInt64(), extent.Depth.ToUInt64(), levels, (HipArrayFlags)flags), false);
    }

    public HipError Memcpy2DArrayToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind) =>
        RecordArrayCopy(_arrays.ContainsKey(destination) && _arrays.ContainsKey(source));

    public HipError Memcpy2DFromArray(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind) =>
        RecordArrayCopy(destination != IntPtr.Zero && _arrays.ContainsKey(source));

    public HipError Memcpy2DFromArrayAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind, IntPtr stream) =>
        RecordArrayCopy(destination != IntPtr.Zero && _arrays.ContainsKey(source) && _streams.Contains(stream));

    public HipError Memcpy2DToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind) =>
        RecordArrayCopy(_arrays.ContainsKey(destination) && source != IntPtr.Zero);

    public HipError Memcpy2DToArrayAsync(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind, IntPtr stream) =>
        RecordArrayCopy(_arrays.ContainsKey(destination) && source != IntPtr.Zero && _streams.Contains(stream));

    public HipError MemcpyFromArray(IntPtr destination, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr count, int kind) =>
        RecordArrayCopy(destination != IntPtr.Zero && _arrays.ContainsKey(source));

    public HipError MemcpyToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr count, int kind) =>
        RecordArrayCopy(_arrays.ContainsKey(destination) && source != IntPtr.Zero);

    public HipError MipmappedArrayCreate(IntPtr mipmappedArray, IntPtr descriptor, uint levels)
    {
        HipArray3DDescriptorNative value = Marshal.PtrToStructure<HipArray3DDescriptorNative>(descriptor);
        return CreateMipmappedArray(mipmappedArray, FakeMipmappedArrayState.FromDriver(value, levels), true);
    }

    public HipError MipmappedArrayDestroy(IntPtr mipmappedArray) => ReleaseMipmappedArray(mipmappedArray);

    public HipError MipmappedArrayGetLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level) =>
        GetMipmappedLevel(levelArray, mipmappedArray, level);

    public HipError TexObjectGetTextureDesc(IntPtr textureDescriptor, ulong textureObject)
    {
        if (!_textureObjects.TryGetValue(textureObject, out FakeTextureState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            var value = new HipDriverTextureDescriptorNative
            {
                AddressModeX = state.Texture.AddressModeX,
                AddressModeY = state.Texture.AddressModeY,
                AddressModeZ = state.Texture.AddressModeZ,
                FilterMode = state.Texture.FilterMode,
                MaximumAnisotropy = state.Texture.MaximumAnisotropy,
                MipmapFilterMode = state.Texture.MipmapFilterMode,
                MipmapLevelBias = state.Texture.MipmapLevelBias,
                MinimumMipmapLevelClamp = state.Texture.MinimumMipmapLevelClamp,
                MaximumMipmapLevelClamp = state.Texture.MaximumMipmapLevelClamp,
            };
            Marshal.StructureToPtr(value, textureDescriptor, false);
        }
        return ArrayTextureResult;
    }

    public HipError TexRefGetArray(IntPtr array, IntPtr textureReference)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state) || state.BoundArray == IntPtr.Zero) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.WriteIntPtr(array, state.BoundArray);
        return ArrayTextureResult;
    }

    public HipError TexRefGetMipMappedArray(IntPtr mipmappedArray, IntPtr textureReference)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state) || state.BoundMipmappedArray == IntPtr.Zero) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) Marshal.WriteIntPtr(mipmappedArray, state.BoundMipmappedArray);
        return ArrayTextureResult;
    }

    public HipError TexRefSetArray(IntPtr textureReference, IntPtr array, uint flags) =>
        BindTextureToArray(textureReference, array, IntPtr.Zero);

    public HipError TexRefSetMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, uint flags) =>
        BindTextureToMipmappedArray(textureReference, mipmappedArray, IntPtr.Zero);

    public HipError UnbindTexture(IntPtr textureReference)
    {
        if (!_textureReferences.TryGetValue(textureReference, out FakeTextureReferenceState? state)) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success)
        {
            state.BoundArray = IntPtr.Zero;
            state.BoundMipmappedArray = IntPtr.Zero;
            state.BoundPointer = IntPtr.Zero;
            TextureUnbindCount++;
        }
        return ArrayTextureResult;
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
                graph = new IntPtr(_nextGraph++);
                _graphs.Add(graph);
                _graphStates[graph] = new FakeGraphState();
            }
            return EndCaptureResult;
        }
        graph = new IntPtr(_nextGraph++);
        _graphs.Add(graph);
        _graphStates[graph] = new FakeGraphState();
        return HipError.Success;
    }

    public HipError GraphCreate(out IntPtr graph, uint flags)
    {
        graph = IntPtr.Zero;
        if (flags != 0) return HipError.InvalidValue;
        if (GraphCreateResult != HipError.Success)
        {
            if (ReturnGraphOnCreateFailure)
            {
                graph = new IntPtr(_nextGraph++);
                _graphs.Add(graph);
                _graphStates[graph] = new FakeGraphState();
            }
            return GraphCreateResult;
        }
        if (ReturnNullGraphOnCreateSuccess) return HipError.Success;
        graph = new IntPtr(_nextGraph++);
        _graphs.Add(graph);
        _graphStates[graph] = new FakeGraphState();
        GraphCreateCount++;
        return HipError.Success;
    }

    public HipError GraphAddEmptyNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount) =>
        AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.Empty));

    public HipError GraphAddDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount)
    {
        if (!_graphStates.TryGetValue(graph, out FakeGraphState? state)) return HipError.InvalidValue;
        if (GraphDependencyResult != HipError.Success) return GraphDependencyResult;
        int count = checked((int)dependencyCount.ToUInt64());
        for (int index = 0; index < count; index++)
        {
            IntPtr prerequisite = Marshal.ReadIntPtr(from, index * IntPtr.Size);
            IntPtr dependent = Marshal.ReadIntPtr(to, index * IntPtr.Size);
            if (!state.Nodes.ContainsKey(prerequisite) || !state.Nodes.ContainsKey(dependent) || prerequisite == dependent || state.Dependencies[dependent].Contains(prerequisite)) return HipError.InvalidValue;
            if (state.DependsOn(prerequisite, dependent)) return HipError.InvalidValue;
        }
        for (int index = 0; index < count; index++) state.Dependencies[Marshal.ReadIntPtr(to, index * IntPtr.Size)].Add(Marshal.ReadIntPtr(from, index * IntPtr.Size));
        return HipError.Success;
    }

    public HipError GraphRemoveDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount)
    {
        if (!_graphStates.TryGetValue(graph, out FakeGraphState? state)) return HipError.InvalidValue;
        if (GraphDependencyResult != HipError.Success) return GraphDependencyResult;
        int count = checked((int)dependencyCount.ToUInt64());
        for (int index = 0; index < count; index++)
        {
            IntPtr prerequisite = Marshal.ReadIntPtr(from, index * IntPtr.Size);
            IntPtr dependent = Marshal.ReadIntPtr(to, index * IntPtr.Size);
            if (!state.Dependencies.TryGetValue(dependent, out HashSet<IntPtr>? values) || !values.Contains(prerequisite)) return HipError.InvalidValue;
        }
        for (int index = 0; index < count; index++) state.Dependencies[Marshal.ReadIntPtr(to, index * IntPtr.Size)].Remove(Marshal.ReadIntPtr(from, index * IntPtr.Size));
        return HipError.Success;
    }

    public HipError GraphAddKernelNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        HipKernelNodeParameters snapshot = Marshal.PtrToStructure<HipKernelNodeParameters>(parameters);
        LastGraphKernelParameters = snapshot;
        ReadGraphKernelArguments(snapshot);
        return AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.Kernel) { Kernel = snapshot, KernelArguments = LastGraphKernelArgumentValues.ToArray() });
    }

    public HipError GraphExecKernelNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters)
    {
        if (!TryGetExecNode(graphExec, node, HipGraphNodeType.Kernel, out FakeGraphNode? target)) return HipError.InvalidValue;
        if (GraphNodeUpdateResult != HipError.Success) return GraphNodeUpdateResult;
        HipKernelNodeParameters snapshot = Marshal.PtrToStructure<HipKernelNodeParameters>(parameters);
        LastGraphKernelParameters = snapshot;
        ReadGraphKernelArguments(snapshot);
        target!.Kernel = snapshot;
        target.KernelArguments = LastGraphKernelArgumentValues.ToArray();
        GraphNodeUpdateCount++;
        return HipError.Success;
    }

    public HipError GraphAddMemcpyNode1D(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind)
    {
        LastGraphCopyBytes = byteCount.ToUInt64();
        LastGraphCopyKind = kind;
        return AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.MemoryCopy)
        {
            Destination = destination,
            Source = source,
            ByteCount = LastGraphCopyBytes,
            CopyKind = kind,
        });
    }

    public HipError GraphExecMemcpyNodeSetParams1D(IntPtr graphExec, IntPtr node, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind)
    {
        if (!TryGetExecNode(graphExec, node, HipGraphNodeType.MemoryCopy, out FakeGraphNode? target)) return HipError.InvalidValue;
        if (GraphNodeUpdateResult != HipError.Success) return GraphNodeUpdateResult;
        target!.Destination = destination;
        target.Source = source;
        target.ByteCount = byteCount.ToUInt64();
        target.CopyKind = kind;
        LastGraphCopyBytes = target.ByteCount;
        LastGraphCopyKind = kind;
        GraphNodeUpdateCount++;
        return HipError.Success;
    }

    public HipError GraphAddMemsetNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        HipMemsetNodeParameters snapshot = Marshal.PtrToStructure<HipMemsetNodeParameters>(parameters);
        LastGraphMemsetParameters = snapshot;
        return AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.MemorySet) { Memset = snapshot });
    }

    public HipError GraphExecMemsetNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters)
    {
        if (!TryGetExecNode(graphExec, node, HipGraphNodeType.MemorySet, out FakeGraphNode? target)) return HipError.InvalidValue;
        if (GraphNodeUpdateResult != HipError.Success) return GraphNodeUpdateResult;
        target!.Memset = Marshal.PtrToStructure<HipMemsetNodeParameters>(parameters);
        LastGraphMemsetParameters = target.Memset;
        GraphNodeUpdateCount++;
        return HipError.Success;
    }

    public HipError GraphAddMemAllocNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        HipMemoryAllocationNodeParameters snapshot = Marshal.PtrToStructure<HipMemoryAllocationNodeParameters>(parameters);
        LastGraphAllocationBytes = snapshot.ByteCount.ToUInt64();
        LastGraphAllocationAccessCount = checked((int)snapshot.AccessDescriptorCount.ToUInt64());
        LastGraphAllocationDevice = snapshot.PoolProperties.Location.Id;
        IntPtr pointer = Marshal.AllocHGlobal(checked((int)LastGraphAllocationBytes));
        Fill(pointer, checked((int)LastGraphAllocationBytes), 0);
        _graphAllocationPointers[pointer] = checked((int)LastGraphAllocationBytes);
        snapshot.DevicePointer = pointer;
        Marshal.StructureToPtr(snapshot, parameters, false);
        HipError error = AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.MemoryAllocation) { Destination = pointer, ByteCount = LastGraphAllocationBytes });
        if (error != HipError.Success && node == IntPtr.Zero)
        {
            _graphAllocationPointers.Remove(pointer);
            Marshal.FreeHGlobal(pointer);
        }
        return error;
    }

    public HipError GraphAddMemFreeNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr devicePointer)
    {
        if (!_graphAllocationPointers.ContainsKey(devicePointer))
        {
            node = IntPtr.Zero;
            return HipError.InvalidValue;
        }
        return AddGraphNode(out node, graph, dependencies, dependencyCount, new FakeGraphNode(HipGraphNodeType.MemoryFree) { Destination = devicePointer });
    }

    public HipError GraphUpload(IntPtr graphExec, IntPtr stream)
    {
        if (!_graphExecStates.ContainsKey(graphExec) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (GraphUploadResult == HipError.Success) GraphUploadCount++;
        return GraphUploadResult;
    }

    public HipError GraphDestroyNode(IntPtr node)
    {
        foreach (FakeGraphState state in _graphStates.Values)
        {
            if (!state.Nodes.TryGetValue(node, out FakeGraphNode? removed)) continue;
            state.Nodes.Remove(node);
            state.Dependencies.Remove(node);
            foreach (HashSet<IntPtr> values in state.Dependencies.Values) values.Remove(node);
            state.Order.Remove(node);
            if (removed.Type == HipGraphNodeType.MemoryAllocation && _graphAllocationPointers.Remove(removed.Destination)) Marshal.FreeHGlobal(removed.Destination);
            GraphNodeDestroyCount++;
            return HipError.Success;
        }
        return HipError.InvalidValue;
    }

    public HipError GraphDestroy(IntPtr graph)
    {
        if (GraphDestroyResult != HipError.Success) return GraphDestroyResult;
        if (!_graphs.Remove(graph)) return HipError.InvalidValue;
        _graphStates.Remove(graph);
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
                graphExec = new IntPtr(_nextGraphExec++);
                _graphExecs.Add(graphExec);
                _graphExecStates[graphExec] = _graphStates[graph].Clone();
            }
            return GraphInstantiateResult;
        }
        graphExec = new IntPtr(_nextGraphExec++);
        _graphExecs.Add(graphExec);
        _graphExecStates[graphExec] = _graphStates[graph].Clone();
        return HipError.Success;
    }

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream)
    {
        if (!_graphExecStates.TryGetValue(graphExec, out FakeGraphState? state) || !_streams.Contains(stream)) return HipError.InvalidValue;
        if (GraphLaunchResult == HipError.Success)
        {
            GraphLaunchCount++;
            FakeGraphState launchSnapshot = state.Clone();
            QueueStreamAction(stream, () => ExecuteGraph(launchSnapshot));
        }
        return GraphLaunchResult;
    }

    public HipError GraphExecDestroy(IntPtr graphExec)
    {
        if (GraphExecDestroyResult != HipError.Success) return GraphExecDestroyResult;
        if (!_graphExecs.Remove(graphExec)) return HipError.InvalidValue;
        _graphExecStates.Remove(graphExec);
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
        MemcpyCallCount++;
        LastMemcpyKind = kind;
        LastMemcpyByteCount = byteCount.ToUInt64();
        LastMemcpyStream = IntPtr.Zero;
        if (MemcpyResult != HipError.Success) return MemcpyResult;
        int count = checked((int)byteCount.ToUInt64());
        var buffer = new byte[count];
        Marshal.Copy(source, buffer, 0, count);
        Marshal.Copy(buffer, 0, destination, count);
        return HipError.Success;
    }

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream)
    {
        AsyncCopyCount++;
        MemcpyAsyncCallCount++;
        LastMemcpyKind = kind;
        LastMemcpyByteCount = byteCount.ToUInt64();
        LastMemcpyStream = stream;
        if (MemcpyAsyncResult != HipError.Success) return MemcpyAsyncResult;
        int count = checked((int)byteCount.ToUInt64());
        var buffer = new byte[count];
        Marshal.Copy(source, buffer, 0, count);
        Marshal.Copy(buffer, 0, destination, count);
        return HipError.Success;
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

    public HipError ExtStreamGetCUMask(IntPtr stream, uint cuMaskSize, IntPtr cuMask)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success)
        {
            for (int index = 0; index < cuMaskSize; index++) Marshal.WriteInt32(cuMask, index * sizeof(uint), index == 0 ? 1 : 0);
        }
        return ManagedNextResult;
    }

    public HipError StreamGetAttribute(IntPtr stream, int attribute, IntPtr value)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.Copy(new byte[64], 0, value, 64);
        return ManagedNextResult;
    }

    public HipError StreamGetCaptureInfo(IntPtr stream, IntPtr captureStatus, IntPtr identifier)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success)
        {
            Marshal.WriteInt32(captureStatus, _capturingStreams.Contains(stream) ? 1 : 0);
            Marshal.WriteInt64(identifier, 0);
        }
        return ManagedNextResult;
    }

    public HipError StreamGetCaptureInfoV2(IntPtr stream, IntPtr captureStatus, IntPtr identifier, IntPtr graph, IntPtr dependencies, IntPtr dependencyCount)
    {
        HipError result = StreamGetCaptureInfo(stream, captureStatus, identifier);
        if (result != HipError.Success) return result;
        Marshal.WriteIntPtr(graph, IntPtr.Zero);
        Marshal.WriteIntPtr(dependencies, IntPtr.Zero);
        Marshal.WriteIntPtr(dependencyCount, IntPtr.Zero);
        return result;
    }

    public HipError StreamGetDevice(IntPtr stream, IntPtr device)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(device, 0);
        return ManagedNextResult;
    }

    public HipError StreamGetFlags(IntPtr stream, IntPtr flags)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(flags, 0);
        return ManagedNextResult;
    }

    public HipError StreamGetId(IntPtr stream, IntPtr identifier)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt64(identifier, stream.ToInt64());
        return ManagedNextResult;
    }

    public HipError StreamGetPriority(IntPtr stream, IntPtr priority)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        if (ManagedNextResult == HipError.Success) Marshal.WriteInt32(priority, 0);
        return ManagedNextResult;
    }

    public HipError StreamWaitEvent(IntPtr stream, IntPtr eventHandle, uint flags)
    {
        if (!_streams.Contains(stream) || !_events.Contains(eventHandle)) return HipError.InvalidValue;
        return ManagedNextResult;
    }

    public HipError StreamWaitValue32(IntPtr stream, IntPtr pointer, uint value, uint flags, uint mask)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return ManagedNextResult;
    }

    public HipError StreamWaitValue64(IntPtr stream, IntPtr pointer, ulong value, uint flags, ulong mask)
    {
        if (!_streams.Contains(stream)) return HipError.InvalidValue;
        return ManagedNextResult;
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
        module = ModuleLoadResult == HipError.Success ? new IntPtr(_nextModule++) : IntPtr.Zero;
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

    public HipError ModuleGetGlobal(IntPtr module, string symbolName, out IntPtr pointer, out UIntPtr byteCount)
    {
        ModuleGetGlobalCallCount++;
        LastModuleGlobalModule = module;
        LastModuleGlobalName = symbolName;
        pointer = IntPtr.Zero;
        byteCount = UIntPtr.Zero;
        if (ModuleGetGlobalResult != HipError.Success) return ModuleGetGlobalResult;
        if (symbolName == "missing") return HipError.InvalidValue;
        var key = (module, symbolName);
        if (!_moduleGlobals.TryGetValue(key, out (IntPtr Pointer, int Length) global))
        {
            byte[] contents = symbolName == "counter"
                ? new byte[] { 0, 0, 0, 0 }
                : symbolName == "values"
                    ? new byte[16]
                    : new byte[8];
            IntPtr allocation = Marshal.AllocHGlobal(contents.Length);
            Marshal.Copy(contents, 0, allocation, contents.Length);
            global = (allocation, contents.Length);
            _moduleGlobals.Add(key, global);
        }
        pointer = ReturnNullModuleGlobal ? IntPtr.Zero : ReturnOverflowModuleGlobalRange
            ? (IntPtr.Size == 4 ? new IntPtr(unchecked((int)0xFFFFFFFEU)) : new IntPtr(-2))
            : global.Pointer;
        byteCount = ReturnZeroModuleGlobalSize ? UIntPtr.Zero : ReturnOverflowModuleGlobalRange
            ? new UIntPtr(4)
            : new UIntPtr((uint)global.Length);
        return HipError.Success;
    }

    public HipError FuncGetAttribute(out int value, HipFunctionAttributeNative attribute, IntPtr function)
    {
        FunctionAttributeCalls.Add(attribute);
        value = FunctionAttributes.TryGetValue(attribute, out int configured) ? configured : 0;
        return FunctionAttributeResults.TryGetValue(attribute, out HipError result) ? result : FunctionAttributeResult;
    }

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessor(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes)
    {
        RecordOccupancy(blockSize, dynamicSharedMemoryBytes, 0, 0, withFlags: false);
        activeBlocks = ActiveBlocksPerMultiprocessor;
        return OccupancyResult;
    }

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes,
        uint flags)
    {
        RecordOccupancy(blockSize, dynamicSharedMemoryBytes, 0, flags, withFlags: true);
        activeBlocks = ActiveBlocksPerMultiprocessor;
        return OccupancyResult;
    }

    public HipError ModuleOccupancyMaxPotentialBlockSize(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit)
    {
        LastPotentialBlockSizeLimit = blockSizeLimit;
        RecordOccupancy(0, dynamicSharedMemoryBytes, blockSizeLimit, 0, withFlags: false);
        minimumGridSize = PotentialMinimumGridSize;
        blockSize = PotentialBlockSize;
        return OccupancyResult;
    }

    public HipError ModuleOccupancyMaxPotentialBlockSizeWithFlags(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit,
        uint flags)
    {
        LastPotentialBlockSizeLimit = blockSizeLimit;
        RecordOccupancy(0, dynamicSharedMemoryBytes, blockSizeLimit, flags, withFlags: true);
        minimumGridSize = PotentialMinimumGridSize;
        blockSize = PotentialBlockSize;
        return OccupancyResult;
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
        RecordKernelLaunch(function, gridX, gridY, gridZ, blockX, blockY, blockZ, sharedMemoryBytes, stream, kernelParameters);

        return ModuleLaunchResult;
    }

    public HipError ModuleLaunchCooperativeKernel(
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
        CooperativeLaunchCount++;
        RecordKernelLaunch(function, gridX, gridY, gridZ, blockX, blockY, blockZ, sharedMemoryBytes, stream, kernelParameters);
        return CooperativeLaunchResult;
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
        foreach (IntPtr pointer in _graphAllocationPointers.Keys) Marshal.FreeHGlobal(pointer);
        _graphAllocationPointers.Clear();
        _activeGraphAllocations.Clear();
        foreach ((IntPtr pointer, int _) in _moduleGlobals.Values) Marshal.FreeHGlobal(pointer);
        _moduleGlobals.Clear();
        _arrays.Clear();
        _mipmappedArrays.Clear();
        _textureObjects.Clear();
        _surfaceObjects.Clear();
        _textureReferences.Clear();
    }

    private HipError CreateArray(IntPtr output, FakeArrayState state, bool driverStyle)
    {
        if (ArrayTextureResult != HipError.Success && !ReturnArrayHandleOnFailure) return ArrayTextureResult;
        IntPtr handle = new(_nextArray++);
        state.DriverStyle = driverStyle;
        _arrays.Add(handle, state);
        Marshal.WriteIntPtr(output, handle);
        return ArrayTextureResult;
    }

    private HipError ReleaseArray(IntPtr array)
    {
        if (!_arrays.ContainsKey(array)) return HipError.InvalidValue;
        if (ArrayTextureReleaseResult != HipError.Success) return ArrayTextureReleaseResult;
        _arrays.Remove(array);
        ArrayFreeCallCount++;
        return HipError.Success;
    }

    private HipError CreateMipmappedArray(IntPtr output, FakeMipmappedArrayState state, bool driverStyle)
    {
        if (state.LevelCount == 0) return HipError.InvalidValue;
        if (ArrayTextureResult != HipError.Success && !ReturnArrayHandleOnFailure) return ArrayTextureResult;
        IntPtr handle = new(_nextMipmappedArray++);
        state.DriverStyle = driverStyle;
        _mipmappedArrays.Add(handle, state);
        Marshal.WriteIntPtr(output, handle);
        return ArrayTextureResult;
    }

    private HipError ReleaseMipmappedArray(IntPtr mipmappedArray)
    {
        if (!_mipmappedArrays.TryGetValue(mipmappedArray, out FakeMipmappedArrayState? state)) return HipError.InvalidValue;
        if (ArrayTextureReleaseResult != HipError.Success) return ArrayTextureReleaseResult;
        foreach (IntPtr level in state.Levels.Values) _arrays.Remove(level);
        _mipmappedArrays.Remove(mipmappedArray);
        MipmappedArrayFreeCallCount++;
        return HipError.Success;
    }

    private HipError GetMipmappedLevel(IntPtr output, IntPtr mipmappedArray, uint level)
    {
        if (!_mipmappedArrays.TryGetValue(mipmappedArray, out FakeMipmappedArrayState? state) || level >= state.LevelCount) return HipError.InvalidValue;
        if (ArrayTextureResult != HipError.Success) return ArrayTextureResult;
        if (!state.Levels.TryGetValue(level, out IntPtr handle))
        {
            handle = new IntPtr(_nextArray++);
            state.Levels.Add(level, handle);
            _arrays.Add(handle, FakeArrayState.FromRuntime(
                state.ChannelFormat,
                Scale(state.Width, level),
                ScaleOptional(state.Height, level),
                ScaleOptional(state.Depth, level),
                state.Flags));
        }
        Marshal.WriteIntPtr(output, handle);
        return HipError.Success;
    }

    private HipError RecordArrayCopy(bool valid)
    {
        if (!valid) return HipError.InvalidValue;
        if (ArrayTextureResult == HipError.Success) ArrayCopyCallCount++;
        return ArrayTextureResult;
    }

    private bool IsKnownTextureResource(HipResourceDescriptorNative resource) => resource.ResourceType switch
    {
        HipTextureResourceKind.Array => _arrays.ContainsKey(resource.Resource.Handle),
        HipTextureResourceKind.MipmappedArray => _mipmappedArrays.ContainsKey(resource.Resource.Handle),
        HipTextureResourceKind.Linear => _allocations.ContainsKey(resource.Resource.Handle),
        HipTextureResourceKind.Pitch2D => _allocations.ContainsKey(resource.Resource.Handle),
        _ => false,
    };

    private static ulong Scale(ulong value, uint level) => level >= 64 ? 1 : Math.Max(1UL, value >> (int)level);
    private static ulong ScaleOptional(ulong value, uint level) => value == 0 ? 0 : Scale(value, level);

    private void RecordOccupancy(
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit,
        uint flags,
        bool withFlags)
    {
        LastOccupancyBlockSize = blockSize;
        LastOccupancyDynamicSharedMemoryBytes = dynamicSharedMemoryBytes.ToUInt64();
        LastOccupancyBlockSizeLimit = blockSizeLimit;
        LastOccupancyFlags = flags;
        if (withFlags) OccupancyFlagsCallCount++;
        else OccupancyNonFlagsCallCount++;
    }

    private void RecordKernelLaunch(
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
        LastLaunchedFunction = function;
        LastGridX = gridX;
        LastGridY = gridY;
        LastGridZ = gridZ;
        LastBlockX = blockX;
        LastBlockY = blockY;
        LastBlockZ = blockZ;
        LastLaunchSharedMemoryBytes = sharedMemoryBytes;
        LastLaunchStream = stream;
        LastKernelArgumentValues.Clear();
        for (int index = 0; index < ExpectedKernelPointerArguments.Count; index++)
        {
            IntPtr valueStorage = Marshal.ReadIntPtr(kernelParameters, index * IntPtr.Size);
            long value = ExpectedKernelPointerArguments[index]
                ? Marshal.ReadIntPtr(valueStorage).ToInt64()
                : Marshal.ReadInt32(valueStorage);
            LastKernelArgumentValues.Add(value);
        }
    }

    private HipError AddGraphNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, FakeGraphNode value)
    {
        node = IntPtr.Zero;
        if (!_graphStates.TryGetValue(graph, out FakeGraphState? state)) return HipError.InvalidValue;
        HashSet<IntPtr> parsed = ReadDependencies(state, dependencies, dependencyCount);
        if (parsed.Count != checked((int)dependencyCount.ToUInt64())) return HipError.InvalidValue;
        if (GraphNodeAddResult != HipError.Success)
        {
            if (ReturnNodeOnAddFailure)
            {
                node = new IntPtr(_nextGraphNode++);
                state.Add(node, value, parsed);
            }
            return GraphNodeAddResult;
        }
        if (ReturnNullNodeOnAddSuccess) return HipError.Success;
        node = new IntPtr(_nextGraphNode++);
        state.Add(node, value, parsed);
        GraphNodeCreateCount++;
        return HipError.Success;
    }

    private static HashSet<IntPtr> ReadDependencies(FakeGraphState state, IntPtr dependencies, UIntPtr dependencyCount)
    {
        int count = checked((int)dependencyCount.ToUInt64());
        var result = new HashSet<IntPtr>();
        if (count == 0) return result;
        if (dependencies == IntPtr.Zero) return result;
        for (int index = 0; index < count; index++)
        {
            IntPtr dependency = Marshal.ReadIntPtr(dependencies, index * IntPtr.Size);
            if (!state.Nodes.ContainsKey(dependency) || !result.Add(dependency)) return new HashSet<IntPtr>();
        }
        return result;
    }

    private bool TryGetExecNode(IntPtr graphExec, IntPtr node, HipGraphNodeType type, out FakeGraphNode? target)
    {
        target = null;
        return _graphExecStates.TryGetValue(graphExec, out FakeGraphState? state) &&
            state.Nodes.TryGetValue(node, out target) && target.Type == type;
    }

    private void ReadGraphKernelArguments(HipKernelNodeParameters parameters)
    {
        LastGraphKernelArgumentValues.Clear();
        for (int index = 0; index < ExpectedKernelPointerArguments.Count; index++)
        {
            IntPtr storage = Marshal.ReadIntPtr(parameters.KernelParameters, index * IntPtr.Size);
            LastGraphKernelArgumentValues.Add(ExpectedKernelPointerArguments[index] ? Marshal.ReadIntPtr(storage).ToInt64() : Marshal.ReadInt32(storage));
        }
    }

    private void ExecuteGraph(FakeGraphState state)
    {
        LastGraphExecutionTrace.Clear();
        var completed = new HashSet<IntPtr>();
        while (completed.Count != state.Nodes.Count)
        {
            bool progressed = false;
            foreach (IntPtr handle in state.Order)
            {
                if (completed.Contains(handle) || !state.Dependencies[handle].All(completed.Contains)) continue;
                ExecuteGraphNode(handle, state.Nodes[handle]);
                completed.Add(handle);
                progressed = true;
            }
            if (!progressed) throw new InvalidOperationException("Fake graph contains a cycle or missing dependency.");
        }
    }

    private void ExecuteGraphNode(IntPtr handle, FakeGraphNode node)
    {
        LastGraphExecutionTrace.Add(node.Type + ":" + handle.ToInt64().ToString("X", CultureInfo.InvariantCulture));
        switch (node.Type)
        {
            case HipGraphNodeType.MemoryAllocation:
                if (!_activeGraphAllocations.Add(node.Destination)) throw new InvalidOperationException("Graph allocation executed twice without a free.");
                GraphAllocationExecutionCount++;
                break;
            case HipGraphNodeType.MemoryFree:
                if (!_activeGraphAllocations.Remove(node.Destination)) throw new InvalidOperationException("Graph free executed before allocation or more than once.");
                GraphFreeExecutionCount++;
                break;
            case HipGraphNodeType.MemorySet:
                HipMemsetNodeParameters memset = node.Memset;
                EnsureGraphPointerUsable(memset.Destination);
                Fill(memset.Destination, checked((int)memset.Width.ToUInt64()), (byte)memset.Value);
                break;
            case HipGraphNodeType.MemoryCopy:
                EnsureGraphPointerUsable(node.Source);
                EnsureGraphPointerUsable(node.Destination);
                CopyBytes(node.Destination, node.Source, checked((int)node.ByteCount));
                break;
            case HipGraphNodeType.Kernel:
            case HipGraphNodeType.Empty:
                break;
        }
    }

    private void EnsureGraphPointerUsable(IntPtr pointer)
    {
        if (_allocations.ContainsKey(pointer)) return;
        if (_graphAllocationPointers.ContainsKey(pointer) && _activeGraphAllocations.Contains(pointer)) return;
        throw new InvalidOperationException("Graph node used an inactive or unknown pointer.");
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

    private sealed class FakeArrayState
    {
        private FakeArrayState(HipChannelFormatDescriptor channelFormat, ulong width, ulong height, ulong depth, HipArrayFlags flags, HipArrayFormat driverFormat, uint channelCount)
        {
            ChannelFormat = channelFormat;
            Width = width;
            Height = height;
            Depth = depth;
            Flags = flags;
            DriverDescriptor = new HipArrayDescriptorNative(ToUIntPtr(width), ToUIntPtr(height), driverFormat, channelCount);
            Driver3DDescriptor = new HipArray3DDescriptorNative(ToUIntPtr(width), ToUIntPtr(height), ToUIntPtr(depth), driverFormat, channelCount, flags);
        }

        internal HipChannelFormatDescriptor ChannelFormat { get; }
        internal ulong Width { get; }
        internal ulong Height { get; }
        internal ulong Depth { get; }
        internal HipArrayFlags Flags { get; }
        internal HipArrayDescriptorNative DriverDescriptor { get; }
        internal HipArray3DDescriptorNative Driver3DDescriptor { get; }
        internal bool DriverStyle { get; set; }

        internal static FakeArrayState FromRuntime(HipChannelFormatDescriptor format, ulong width, ulong height, ulong depth, HipArrayFlags flags)
        {
            (HipArrayFormat driverFormat, uint channelCount) = ToDriverFormat(format);
            return new FakeArrayState(format, width, height, depth, flags, driverFormat, channelCount);
        }

        internal static FakeArrayState FromDriver(HipArrayDescriptorNative descriptor)
        {
            HipChannelFormatDescriptor channel = ToChannelFormat(descriptor.Format, descriptor.ChannelCount);
            return new FakeArrayState(channel, descriptor.Width.ToUInt64(), descriptor.Height.ToUInt64(), 0, HipArrayFlags.Default, descriptor.Format, descriptor.ChannelCount);
        }

        internal static FakeArrayState FromDriver(HipArray3DDescriptorNative descriptor)
        {
            HipChannelFormatDescriptor channel = ToChannelFormat(descriptor.Format, descriptor.ChannelCount);
            return new FakeArrayState(channel, descriptor.Width.ToUInt64(), descriptor.Height.ToUInt64(), descriptor.Depth.ToUInt64(), descriptor.Flags, descriptor.Format, descriptor.ChannelCount);
        }

        internal static HipChannelFormatDescriptor ToChannelFormat(HipArrayFormat format, uint channels)
        {
            int bits;
            HipChannelFormatKind kind;
            switch (format)
            {
                case HipArrayFormat.UnsignedInt8: bits = 8; kind = HipChannelFormatKind.UnsignedInteger; break;
                case HipArrayFormat.UnsignedInt16: bits = 16; kind = HipChannelFormatKind.UnsignedInteger; break;
                case HipArrayFormat.UnsignedInt32: bits = 32; kind = HipChannelFormatKind.UnsignedInteger; break;
                case HipArrayFormat.SignedInt8: bits = 8; kind = HipChannelFormatKind.SignedInteger; break;
                case HipArrayFormat.SignedInt16: bits = 16; kind = HipChannelFormatKind.SignedInteger; break;
                case HipArrayFormat.SignedInt32: bits = 32; kind = HipChannelFormatKind.SignedInteger; break;
                case HipArrayFormat.Half: bits = 16; kind = HipChannelFormatKind.FloatingPoint; break;
                case HipArrayFormat.FloatingPoint32: bits = 32; kind = HipChannelFormatKind.FloatingPoint; break;
                default: throw new InvalidOperationException("Unsupported fake array format.");
            }
            return new HipChannelFormatDescriptor(bits, channels >= 2 ? bits : 0, channels >= 4 ? bits : 0, channels >= 4 ? bits : 0, kind);
        }

        private static (HipArrayFormat Format, uint Channels) ToDriverFormat(HipChannelFormatDescriptor format)
        {
            int bits = format.XBits;
            uint channels = format.WBits != 0 ? 4U : format.YBits != 0 ? 2U : 1U;
            HipArrayFormat result;
            if (format.Kind == HipChannelFormatKind.UnsignedInteger)
                result = bits == 8 ? HipArrayFormat.UnsignedInt8 : bits == 16 ? HipArrayFormat.UnsignedInt16 : HipArrayFormat.UnsignedInt32;
            else if (format.Kind == HipChannelFormatKind.SignedInteger)
                result = bits == 8 ? HipArrayFormat.SignedInt8 : bits == 16 ? HipArrayFormat.SignedInt16 : HipArrayFormat.SignedInt32;
            else if (format.Kind == HipChannelFormatKind.FloatingPoint)
                result = bits == 16 ? HipArrayFormat.Half : HipArrayFormat.FloatingPoint32;
            else
                throw new InvalidOperationException("Unsupported fake channel format.");
            return (result, channels);
        }
    }

    private sealed class FakeMipmappedArrayState
    {
        internal FakeMipmappedArrayState(HipChannelFormatDescriptor channelFormat, ulong width, ulong height, ulong depth, uint levelCount, HipArrayFlags flags)
        {
            ChannelFormat = channelFormat;
            Width = width;
            Height = height;
            Depth = depth;
            LevelCount = levelCount;
            Flags = flags;
        }

        internal HipChannelFormatDescriptor ChannelFormat { get; }
        internal ulong Width { get; }
        internal ulong Height { get; }
        internal ulong Depth { get; }
        internal uint LevelCount { get; }
        internal HipArrayFlags Flags { get; }
        internal bool DriverStyle { get; set; }
        internal Dictionary<uint, IntPtr> Levels { get; } = new();

        internal static FakeMipmappedArrayState FromDriver(HipArray3DDescriptorNative descriptor, uint levels) => new(
            FakeArrayState.ToChannelFormat(descriptor.Format, descriptor.ChannelCount),
            descriptor.Width.ToUInt64(),
            descriptor.Height.ToUInt64(),
            descriptor.Depth.ToUInt64(),
            levels,
            descriptor.Flags);
    }

    private sealed class FakeTextureState
    {
        internal FakeTextureState(HipResourceDescriptorNative resource, HipTextureDescriptorNative texture, HipResourceViewDescriptorNative resourceView)
        {
            Resource = resource;
            Texture = texture;
            ResourceView = resourceView;
        }

        internal HipResourceDescriptorNative Resource { get; }
        internal HipTextureDescriptorNative Texture { get; }
        internal HipResourceViewDescriptorNative ResourceView { get; }
    }

    private sealed class FakeTextureReferenceState
    {
        internal IntPtr BoundPointer { get; set; }
        internal IntPtr BoundArray { get; set; }
        internal IntPtr BoundMipmappedArray { get; set; }
    }

    private sealed class FakeGraphState
    {
        internal Dictionary<IntPtr, FakeGraphNode> Nodes { get; } = new();
        internal Dictionary<IntPtr, HashSet<IntPtr>> Dependencies { get; } = new();
        internal List<IntPtr> Order { get; } = new();
        internal int EdgeCount => Dependencies.Values.Sum(values => values.Count);

        internal void Add(IntPtr handle, FakeGraphNode node, HashSet<IntPtr> dependencies)
        {
            Nodes.Add(handle, node);
            Dependencies.Add(handle, new HashSet<IntPtr>(dependencies));
            Order.Add(handle);
        }

        internal bool DependsOn(IntPtr node, IntPtr prerequisite)
        {
            var pending = new Stack<IntPtr>();
            var visited = new HashSet<IntPtr>();
            pending.Push(node);
            while (pending.Count != 0)
            {
                IntPtr current = pending.Pop();
                if (!visited.Add(current)) continue;
                foreach (IntPtr dependency in Dependencies[current])
                {
                    if (dependency == prerequisite) return true;
                    pending.Push(dependency);
                }
            }
            return false;
        }

        internal FakeGraphState Clone()
        {
            var clone = new FakeGraphState();
            foreach (IntPtr handle in Order) clone.Add(handle, Nodes[handle].Clone(), Dependencies[handle]);
            return clone;
        }
    }

    private sealed class FakeGraphNode
    {
        internal FakeGraphNode(HipGraphNodeType type) => Type = type;
        internal HipGraphNodeType Type { get; }
        internal IntPtr Destination { get; set; }
        internal IntPtr Source { get; set; }
        internal ulong ByteCount { get; set; }
        internal HipMemoryCopyKind CopyKind { get; set; }
        internal HipKernelNodeParameters Kernel { get; set; }
        internal long[] KernelArguments { get; set; } = Array.Empty<long>();
        internal HipMemsetNodeParameters Memset { get; set; }

        internal FakeGraphNode Clone() => new(Type)
        {
            Destination = Destination,
            Source = Source,
            ByteCount = ByteCount,
            CopyKind = CopyKind,
            Kernel = Kernel,
            KernelArguments = (long[])KernelArguments.Clone(),
            Memset = Memset,
        };
    }
}
