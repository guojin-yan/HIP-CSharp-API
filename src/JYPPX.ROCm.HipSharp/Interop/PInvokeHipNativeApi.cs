using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Generated;
using JYPPX.ROCm.HipSharp.Loading;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 将语义化原生边界连接到生成的 P/Invoke 声明 / Connects the semantic native boundary to generated P/Invoke declarations.
/// </summary>
internal sealed class PInvokeHipNativeApi : IHipNativeApi
{
    private readonly HipRuntimeNativeApi _completeApi;

    internal PInvokeHipNativeApi(string? explicitLibraryPath)
    {
        HipImportResolver.EnsureLoaded(HipNativeLibraryKind.Runtime, explicitLibraryPath);
        _completeApi = new HipRuntimeNativeApi(explicitLibraryPath);
    }

    public HipError Init(uint flags) => HipNativeMethods.Init(flags);

    public HipError RuntimeGetVersion(out int runtimeVersion) => HipNativeMethods.RuntimeGetVersion(out runtimeVersion);

    public HipError DriverGetVersion(out int driverVersion) => HipNativeMethods.DriverGetVersion(out driverVersion);

    public HipError GetDeviceCount(out int count) => HipNativeMethods.GetDeviceCount(out count);

    public HipError GetDevice(out int deviceId) => HipNativeMethods.GetDevice(out deviceId);

    public HipError SetDevice(int deviceId) => HipNativeMethods.SetDevice(deviceId);

    public HipError DeviceGetName(int deviceId, out string name)
    {
        const int bufferLength = 256;
        IntPtr buffer = Marshal.AllocHGlobal(bufferLength);
        try
        {
            Marshal.WriteByte(buffer, 0);
            HipError error = HipNativeMethods.DeviceGetName(buffer, bufferLength, deviceId);
            name = error == HipError.Success ? Marshal.PtrToStringAnsi(buffer) ?? string.Empty : string.Empty;
            return error;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId) =>
        HipNativeMethods.DeviceGetAttribute(out value, attribute, deviceId);

    public HipError DeviceComputeCapability(IntPtr major, IntPtr minor, int device) =>
        Optional(() => _completeApi.DeviceComputeCapability(major, minor, device));

    public HipError DeviceGet(IntPtr device, int ordinal) => Optional(() => _completeApi.DeviceGet(device, ordinal));

    public HipError DeviceGetByPCIBusId(IntPtr device, IntPtr pciBusId) =>
        Optional(() => _completeApi.DeviceGetByPCIBusId(device, pciBusId));

    public HipError DeviceGetCacheConfig(IntPtr cacheConfig) => Optional(() => _completeApi.DeviceGetCacheConfig(cacheConfig));

    public HipError DeviceGetGraphMemAttribute(int device, int attribute, IntPtr value) =>
        Optional(() => _completeApi.DeviceGetGraphMemAttribute(device, attribute, value));

    public HipError DeviceGetLimit(IntPtr value, int limit) => Optional(() => _completeApi.DeviceGetLimit(value, limit));

    public HipError DeviceGetP2PAttribute(IntPtr value, int attribute, int sourceDevice, int destinationDevice) =>
        Optional(() => _completeApi.DeviceGetP2PAttribute(value, attribute, sourceDevice, destinationDevice));

    public HipError DeviceGetPCIBusId(IntPtr pciBusId, int length, int device) =>
        Optional(() => _completeApi.DeviceGetPCIBusId(pciBusId, length, device));

    public HipError DeviceGetSharedMemConfig(IntPtr config) => Optional(() => _completeApi.DeviceGetSharedMemConfig(config));

    public HipError DeviceGetStreamPriorityRange(IntPtr leastPriority, IntPtr greatestPriority) =>
        Optional(() => _completeApi.DeviceGetStreamPriorityRange(leastPriority, greatestPriority));

    public HipError DeviceGetUuid(IntPtr uuid, int device) => Optional(() => _completeApi.DeviceGetUuid(uuid, device));

    public HipError DeviceTotalMem(IntPtr bytes, int device) => Optional(() => _completeApi.DeviceTotalMem(bytes, device));

    public HipError GetSymbolAddress(IntPtr devicePointer, IntPtr symbol) =>
        Optional(() => _completeApi.GetSymbolAddress(devicePointer, symbol));

    public HipError GetSymbolSize(IntPtr size, IntPtr symbol) => Optional(() => _completeApi.GetSymbolSize(size, symbol));

    public HipError PointerGetAttribute(IntPtr data, int attribute, IntPtr pointer) =>
        Optional(() => _completeApi.PointerGetAttribute(data, attribute, pointer));

    public HipError PointerGetAttributes(IntPtr attributes, IntPtr pointer) =>
        Optional(() => _completeApi.PointerGetAttributes(attributes, pointer));

    public HipError PointerSetAttribute(IntPtr value, int attribute, IntPtr pointer) =>
        Optional(() => _completeApi.PointerSetAttribute(value, attribute, pointer));

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes)
    {
        UIntPtr free = UIntPtr.Zero;
        UIntPtr total = UIntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MemGetInfo(out free, out total));
        freeBytes = free;
        totalBytes = total;
        return error;
    }

    public HipError MemAddressFree(IntPtr address, UIntPtr size) => Optional(() => _completeApi.MemAddressFree(address, size));

    public HipError MemAddressReserve(IntPtr address, UIntPtr size, UIntPtr alignment, IntPtr requestedAddress, ulong flags) =>
        Optional(() => _completeApi.MemAddressReserve(address, size, alignment, requestedAddress, flags));

    public HipError MemCreate(IntPtr handle, UIntPtr size, IntPtr properties, ulong flags) =>
        Optional(() => _completeApi.MemCreate(handle, size, properties, flags));

    public HipError MemExportToShareableHandle(IntPtr shareableHandle, IntPtr handle, int handleType, ulong flags) =>
        Optional(() => _completeApi.MemExportToShareableHandle(shareableHandle, handle, handleType, flags));

    public HipError MemGetAccess(IntPtr flags, IntPtr location, IntPtr address) =>
        Optional(() => _completeApi.MemGetAccess(flags, location, address));

    public HipError MemImportFromShareableHandle(IntPtr handle, IntPtr operatingSystemHandle, int handleType) =>
        Optional(() => _completeApi.MemImportFromShareableHandle(handle, operatingSystemHandle, handleType));

    public HipError MemMap(IntPtr address, UIntPtr size, UIntPtr offset, IntPtr handle, ulong flags) =>
        Optional(() => _completeApi.MemMap(address, size, offset, handle, flags));

    public HipError MemMapArrayAsync(IntPtr mapInformation, uint count, IntPtr stream) =>
        Optional(() => _completeApi.MemMapArrayAsync(mapInformation, count, stream));

    public HipError MemRelease(IntPtr handle) => Optional(() => _completeApi.MemRelease(handle));

    public HipError MemRetainAllocationHandle(IntPtr handle, IntPtr address) =>
        Optional(() => _completeApi.MemRetainAllocationHandle(handle, address));

    public HipError MemSetAccess(IntPtr address, UIntPtr size, IntPtr descriptors, UIntPtr count) =>
        Optional(() => _completeApi.MemSetAccess(address, size, descriptors, count));

    public HipError MemUnmap(IntPtr address, UIntPtr size) => Optional(() => _completeApi.MemUnmap(address, size));

    public HipError Array3DCreate(IntPtr array, IntPtr descriptor) => Optional(() => _completeApi.Array3DCreate(array, descriptor));

    public HipError Array3DGetDescriptor(IntPtr descriptor, IntPtr array) => Optional(() => _completeApi.Array3DGetDescriptor(descriptor, array));

    public HipError ArrayCreate(IntPtr array, IntPtr descriptor) => Optional(() => _completeApi.ArrayCreate(array, descriptor));

    public HipError ArrayDestroy(IntPtr array) => Optional(() => _completeApi.ArrayDestroy(array));

    public HipError ArrayGetDescriptor(IntPtr descriptor, IntPtr array) => Optional(() => _completeApi.ArrayGetDescriptor(descriptor, array));

    public HipError ArrayGetInfo(IntPtr descriptor, IntPtr extent, IntPtr flags, IntPtr array) =>
        Optional(() => _completeApi.ArrayGetInfo(descriptor, extent, flags, array));

    public HipError BindTexture(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr size) =>
        Optional(() => _completeApi.BindTexture(offset, textureReference, devicePointer, descriptor, size));

    public HipError BindTexture2D(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr width, UIntPtr height, UIntPtr pitch) =>
        Optional(() => _completeApi.BindTexture2D(offset, textureReference, devicePointer, descriptor, width, height, pitch));

    public HipError BindTextureToArray(IntPtr textureReference, IntPtr array, IntPtr descriptor) =>
        Optional(() => _completeApi.BindTextureToArray(textureReference, array, descriptor));

    public HipError BindTextureToMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, IntPtr descriptor) =>
        Optional(() => _completeApi.BindTextureToMipmappedArray(textureReference, mipmappedArray, descriptor));

    public HipError CreateSurfaceObject(IntPtr surfaceObject, IntPtr resourceDescriptor) =>
        Optional(() => _completeApi.CreateSurfaceObject(surfaceObject, resourceDescriptor));

    public HipError CreateTextureObject(IntPtr textureObject, IntPtr resourceDescriptor, IntPtr textureDescriptor, IntPtr resourceViewDescriptor) =>
        Optional(() => _completeApi.CreateTextureObject(textureObject, resourceDescriptor, textureDescriptor, resourceViewDescriptor));

    public HipError DestroySurfaceObject(ulong surfaceObject) => Optional(() => _completeApi.DestroySurfaceObject(surfaceObject));

    public HipError DestroyTextureObject(ulong textureObject) => Optional(() => _completeApi.DestroyTextureObject(textureObject));

    public HipError DeviceGetTexture1DLinearMaxWidth(IntPtr maxWidth, IntPtr descriptor, int device) =>
        Optional(() => _completeApi.DeviceGetTexture1DLinearMaxWidth(maxWidth, descriptor, device));

    public HipError FreeArray(IntPtr array) => Optional(() => _completeApi.FreeArray(array));

    public HipError FreeMipmappedArray(IntPtr mipmappedArray) => Optional(() => _completeApi.FreeMipmappedArray(mipmappedArray));

    public HipError GetMipmappedArrayLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level) =>
        Optional(() => _completeApi.GetMipmappedArrayLevel(levelArray, mipmappedArray, level));

    public HipError GetTextureAlignmentOffset(IntPtr offset, IntPtr textureReference) =>
        Optional(() => _completeApi.GetTextureAlignmentOffset(offset, textureReference));

    public HipError GetTextureObjectResourceDesc(IntPtr resourceDescriptor, ulong textureObject) =>
        Optional(() => _completeApi.GetTextureObjectResourceDesc(resourceDescriptor, textureObject));

    public HipError GetTextureObjectResourceViewDesc(IntPtr resourceViewDescriptor, ulong textureObject) =>
        Optional(() => _completeApi.GetTextureObjectResourceViewDesc(resourceViewDescriptor, textureObject));

    public HipError GetTextureObjectTextureDesc(IntPtr textureDescriptor, ulong textureObject) =>
        Optional(() => _completeApi.GetTextureObjectTextureDesc(textureDescriptor, textureObject));

    public HipError GetTextureReference(IntPtr textureReference, IntPtr symbol) =>
        Optional(() => _completeApi.GetTextureReference(textureReference, symbol));

    public HipError Malloc3DArray(IntPtr array, IntPtr descriptor, HipExtent extent, uint flags) =>
        Optional(() => _completeApi.Malloc3DArray(array, descriptor, extent, flags));

    public HipError MallocArray(IntPtr array, IntPtr descriptor, UIntPtr width, UIntPtr height, uint flags) =>
        Optional(() => _completeApi.MallocArray(array, descriptor, width, height, flags));

    public HipError MallocMipmappedArray(IntPtr mipmappedArray, IntPtr descriptor, HipExtent extent, uint levels, uint flags) =>
        Optional(() => _completeApi.MallocMipmappedArray(mipmappedArray, descriptor, extent, levels, flags));

    public HipError Memcpy2DArrayToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind) =>
        Optional(() => _completeApi.Memcpy2DArrayToArray(destination, destinationX, destinationY, source, sourceX, sourceY, width, height, kind));

    public HipError Memcpy2DFromArray(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind) =>
        Optional(() => _completeApi.Memcpy2DFromArray(destination, destinationPitch, source, sourceX, sourceY, width, height, kind));

    public HipError Memcpy2DFromArrayAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind, IntPtr stream) =>
        Optional(() => _completeApi.Memcpy2DFromArrayAsync(destination, destinationPitch, source, sourceX, sourceY, width, height, kind, stream));

    public HipError Memcpy2DToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind) =>
        Optional(() => _completeApi.Memcpy2DToArray(destination, destinationX, destinationY, source, sourcePitch, width, height, kind));

    public HipError Memcpy2DToArrayAsync(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind, IntPtr stream) =>
        Optional(() => _completeApi.Memcpy2DToArrayAsync(destination, destinationX, destinationY, source, sourcePitch, width, height, kind, stream));

    public HipError MemcpyFromArray(IntPtr destination, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr count, int kind) =>
        Optional(() => _completeApi.MemcpyFromArray(destination, source, sourceX, sourceY, count, kind));

    public HipError MemcpyToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr count, int kind) =>
        Optional(() => _completeApi.MemcpyToArray(destination, destinationX, destinationY, source, count, kind));

    public HipError MipmappedArrayCreate(IntPtr mipmappedArray, IntPtr descriptor, uint levels) =>
        Optional(() => _completeApi.MipmappedArrayCreate(mipmappedArray, descriptor, levels));

    public HipError MipmappedArrayDestroy(IntPtr mipmappedArray) => Optional(() => _completeApi.MipmappedArrayDestroy(mipmappedArray));

    public HipError MipmappedArrayGetLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level) =>
        Optional(() => _completeApi.MipmappedArrayGetLevel(levelArray, mipmappedArray, level));

    public HipError TexObjectGetTextureDesc(IntPtr textureDescriptor, ulong textureObject) =>
        Optional(() => _completeApi.TexObjectGetTextureDesc(textureDescriptor, textureObject));

    public HipError TexRefGetArray(IntPtr array, IntPtr textureReference) => Optional(() => _completeApi.TexRefGetArray(array, textureReference));

    public HipError TexRefGetMipMappedArray(IntPtr mipmappedArray, IntPtr textureReference) =>
        Optional(() => _completeApi.TexRefGetMipMappedArray(mipmappedArray, textureReference));

    public HipError TexRefSetArray(IntPtr textureReference, IntPtr array, uint flags) =>
        Optional(() => _completeApi.TexRefSetArray(textureReference, array, flags));

    public HipError TexRefSetMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, uint flags) =>
        Optional(() => _completeApi.TexRefSetMipmappedArray(textureReference, mipmappedArray, flags));

    public HipError UnbindTexture(IntPtr textureReference) => Optional(() => _completeApi.UnbindTexture(textureReference));

    public HipError MallocPitch(out IntPtr pointer, out UIntPtr pitch, UIntPtr widthBytes, UIntPtr height)
    {
        IntPtr value = IntPtr.Zero;
        UIntPtr rowPitch = UIntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocPitch(out value, out rowPitch, widthBytes, height));
        pointer = value;
        pitch = rowPitch;
        return error;
    }

    public HipError Malloc3D(out HipPitchedPtr pitchedPointer, HipExtent extent)
    {
        HipPitchedPtr value = default;
        HipError error = Optional(() => HipNativeMethods.Malloc3D(out value, extent));
        pitchedPointer = value;
        return error;
    }

    public HipError MallocManaged(out IntPtr pointer, UIntPtr byteCount, uint flags)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocManaged(out value, byteCount, flags));
        pointer = value;
        return error;
    }

    public HipError MemPrefetchAsync(IntPtr pointer, UIntPtr byteCount, int device, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemPrefetchAsync(pointer, byteCount, device, stream));

    public HipError MemAdvise(IntPtr pointer, UIntPtr byteCount, HipMemoryAdvise advice, int device) =>
        Optional(() => HipNativeMethods.MemAdvise(pointer, byteCount, advice, device));

    public HipError MallocAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr stream)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocAsync(out value, byteCount, stream));
        pointer = value;
        return error;
    }

    public HipError FreeAsync(IntPtr pointer, IntPtr stream) => Optional(() => HipNativeMethods.FreeAsync(pointer, stream));

    public HipError DeviceGetDefaultMemPool(out IntPtr memoryPool, int deviceOrdinal)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.DeviceGetDefaultMemPool(out value, deviceOrdinal));
        memoryPool = value;
        return error;
    }

    public HipError DeviceGetMemPool(out IntPtr memoryPool, int deviceOrdinal)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.DeviceGetMemPool(out value, deviceOrdinal));
        memoryPool = value;
        return error;
    }

    public HipError DeviceSetMemPool(int deviceOrdinal, IntPtr memoryPool) =>
        Optional(() => HipNativeMethods.DeviceSetMemPool(deviceOrdinal, memoryPool));

    public HipError MemPoolCreate(out IntPtr memoryPool, ref HipMemoryPoolPropertiesNative properties)
    {
        IntPtr value = IntPtr.Zero;
        HipMemoryPoolPropertiesNative nativeProperties = properties;
        HipError error = Optional(() => HipNativeMethods.MemPoolCreate(out value, ref nativeProperties));
        properties = nativeProperties;
        memoryPool = value;
        return error;
    }

    public HipError MemPoolDestroy(IntPtr memoryPool) => Optional(() => HipNativeMethods.MemPoolDestroy(memoryPool));

    public HipError MemPoolTrimTo(IntPtr memoryPool, UIntPtr minimumBytesToKeep) =>
        Optional(() => HipNativeMethods.MemPoolTrimTo(memoryPool, minimumBytesToKeep));

    public HipError MemPoolGetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value) =>
        Optional(() => HipNativeMethods.MemPoolGetAttribute(memoryPool, attribute, value));

    public HipError MemPoolSetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value) =>
        Optional(() => HipNativeMethods.MemPoolSetAttribute(memoryPool, attribute, value));

    public unsafe HipError MemPoolSetAccess(IntPtr memoryPool, HipMemoryPoolAccessDescriptorNative[] descriptors)
    {
        fixed (HipMemoryPoolAccessDescriptorNative* pointer = descriptors)
        {
            UIntPtr count = UIntPtr.Size == 4 ? new UIntPtr(checked((uint)descriptors.Length)) : new UIntPtr((ulong)descriptors.Length);
            try
            {
                return HipNativeMethods.MemPoolSetAccess(memoryPool, (IntPtr)pointer, count);
            }
            catch (EntryPointNotFoundException)
            {
                return HipError.NotSupported;
            }
        }
    }

    public unsafe HipError MemPoolGetAccess(out HipMemoryPoolAccess access, IntPtr memoryPool, ref HipMemLocation location)
    {
        HipMemoryPoolAccess value = HipMemoryPoolAccess.None;
        HipMemLocation nativeLocation = location;
        HipError error;
        try
        {
            error = HipNativeMethods.MemPoolGetAccess((IntPtr)(&value), memoryPool, (IntPtr)(&nativeLocation));
        }
        catch (EntryPointNotFoundException)
        {
            error = HipError.NotSupported;
        }
        location = nativeLocation;
        access = value;
        return error;
    }

    public HipError MallocFromPoolAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr memoryPool, IntPtr stream)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.MallocFromPoolAsync(out value, byteCount, memoryPool, stream));
        pointer = value;
        return error;
    }

    public HipError DeviceCanAccessPeer(out int canAccessPeer, int deviceId, int peerDeviceId)
    {
        int value = 0;
        HipError error = Optional(() => HipNativeMethods.DeviceCanAccessPeer(out value, deviceId, peerDeviceId));
        canAccessPeer = value;
        return error;
    }

    public HipError DeviceEnablePeerAccess(int peerDeviceId, uint flags) => Optional(() => HipNativeMethods.DeviceEnablePeerAccess(peerDeviceId, flags));

    public HipError DeviceDisablePeerAccess(int peerDeviceId) => Optional(() => HipNativeMethods.DeviceDisablePeerAccess(peerDeviceId));

    public HipError MemcpyPeerAsync(IntPtr destination, int destinationDevice, IntPtr source, int sourceDevice, UIntPtr byteCount, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemcpyPeerAsync(destination, destinationDevice, source, sourceDevice, byteCount, stream));

    public HipError StreamBeginCapture(IntPtr stream, HipStreamCaptureMode mode) => Optional(() => HipNativeMethods.StreamBeginCapture(stream, mode));

    public HipError StreamEndCapture(IntPtr stream, out IntPtr graph)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.StreamEndCapture(stream, out value));
        graph = value;
        return error;
    }

    public HipError GraphCreate(out IntPtr graph, uint flags)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphCreate(out value, flags));
        graph = value;
        return error;
    }

    public HipError GraphAddEmptyNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddEmptyNode(out value, graph, dependencies, dependencyCount));
        node = value;
        return error;
    }

    public HipError GraphAddDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount) =>
        Optional(() => HipNativeMethods.GraphAddDependencies(graph, from, to, dependencyCount));

    public HipError GraphRemoveDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount) =>
        Optional(() => HipNativeMethods.GraphRemoveDependencies(graph, from, to, dependencyCount));

    public HipError GraphAddKernelNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddKernelNode(out value, graph, dependencies, dependencyCount, parameters));
        node = value;
        return error;
    }

    public HipError GraphExecKernelNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters) =>
        Optional(() => HipNativeMethods.GraphExecKernelNodeSetParams(graphExec, node, parameters));

    public HipError GraphAddMemcpyNode1D(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddMemcpyNode1D(out value, graph, dependencies, dependencyCount, destination, source, byteCount, kind));
        node = value;
        return error;
    }

    public HipError GraphExecMemcpyNodeSetParams1D(IntPtr graphExec, IntPtr node, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind) =>
        Optional(() => HipNativeMethods.GraphExecMemcpyNodeSetParams1D(graphExec, node, destination, source, byteCount, kind));

    public HipError GraphAddMemsetNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddMemsetNode(out value, graph, dependencies, dependencyCount, parameters));
        node = value;
        return error;
    }

    public HipError GraphExecMemsetNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters) =>
        Optional(() => HipNativeMethods.GraphExecMemsetNodeSetParams(graphExec, node, parameters));

    public HipError GraphAddMemAllocNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddMemAllocNode(out value, graph, dependencies, dependencyCount, parameters));
        node = value;
        return error;
    }

    public HipError GraphAddMemFreeNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr devicePointer)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphAddMemFreeNode(out value, graph, dependencies, dependencyCount, devicePointer));
        node = value;
        return error;
    }

    public HipError GraphUpload(IntPtr graphExec, IntPtr stream) => Optional(() => HipNativeMethods.GraphUpload(graphExec, stream));

    public HipError GraphDestroyNode(IntPtr node) => Optional(() => HipNativeMethods.GraphDestroyNode(node));

    public HipError GraphDestroy(IntPtr graph) => Optional(() => HipNativeMethods.GraphDestroy(graph));

    public HipError GraphInstantiateWithFlags(out IntPtr graphExec, IntPtr graph, ulong flags)
    {
        IntPtr value = IntPtr.Zero;
        HipError error = Optional(() => HipNativeMethods.GraphInstantiateWithFlags(out value, graph, flags));
        graphExec = value;
        return error;
    }

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream) => Optional(() => HipNativeMethods.GraphLaunch(graphExec, stream));

    public HipError GraphExecDestroy(IntPtr graphExec) => Optional(() => HipNativeMethods.GraphExecDestroy(graphExec));

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount) => HipNativeMethods.Malloc(out pointer, byteCount);

    public HipError Free(IntPtr pointer) => HipNativeMethods.Free(pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind) =>
        HipNativeMethods.Memcpy(destination, source, byteCount, kind);

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream) =>
        HipNativeMethods.MemcpyAsync(destination, source, byteCount, kind, stream);

    public HipError Memset(IntPtr destination, int value, UIntPtr byteCount) =>
        Optional(() => HipNativeMethods.Memset(destination, value, byteCount));

    public HipError MemsetAsync(IntPtr destination, int value, UIntPtr byteCount, IntPtr stream) =>
        Optional(() => HipNativeMethods.MemsetAsync(destination, value, byteCount, stream));

    public HipError Memset2D(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height) =>
        Optional(() => HipNativeMethods.Memset2D(destination, pitch, value, widthBytes, height));

    public HipError Memset2DAsync(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memset2DAsync(destination, pitch, value, widthBytes, height, stream));

    public HipError Memset3D(HipPitchedPtr destination, int value, HipExtent extent) =>
        Optional(() => HipNativeMethods.Memset3D(destination, value, extent));

    public HipError Memset3DAsync(HipPitchedPtr destination, int value, HipExtent extent, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memset3DAsync(destination, value, extent, stream));

    public HipError Memcpy2D(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind) =>
        Optional(() => HipNativeMethods.Memcpy2D(destination, destinationPitch, source, sourcePitch, widthBytes, height, kind));

    public HipError Memcpy2DAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind, IntPtr stream) =>
        Optional(() => HipNativeMethods.Memcpy2DAsync(destination, destinationPitch, source, sourcePitch, widthBytes, height, kind, stream));

    public HipError Memcpy3D(ref HipMemcpy3DParameters parameters)
    {
        HipMemcpy3DParameters value = parameters;
        HipError error = Optional(() => HipNativeMethods.Memcpy3D(ref value));
        parameters = value;
        return error;
    }

    public HipError Memcpy3DAsync(ref HipMemcpy3DParameters parameters, IntPtr stream)
    {
        HipMemcpy3DParameters value = parameters;
        HipError error = Optional(() => HipNativeMethods.Memcpy3DAsync(ref value, stream));
        parameters = value;
        return error;
    }

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags) =>
        HipNativeMethods.HostMalloc(out pointer, byteCount, flags);

    public HipError HostFree(IntPtr pointer) => HipNativeMethods.HostFree(pointer);

    public HipError DeviceSynchronize() => HipNativeMethods.DeviceSynchronize();

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags) =>
        HipNativeMethods.StreamCreateWithFlags(out stream, flags);

    public HipError StreamDestroy(IntPtr stream) => HipNativeMethods.StreamDestroy(stream);

    public HipError StreamSynchronize(IntPtr stream) => HipNativeMethods.StreamSynchronize(stream);

    public HipError StreamQuery(IntPtr stream) => HipNativeMethods.StreamQuery(stream);

    public HipError ExtStreamGetCUMask(IntPtr stream, uint cuMaskSize, IntPtr cuMask) =>
        Optional(() => _completeApi.ExtStreamGetCUMask(stream, cuMaskSize, cuMask));

    public HipError StreamGetAttribute(IntPtr stream, int attribute, IntPtr value) =>
        Optional(() => _completeApi.StreamGetAttribute(stream, attribute, value));

    public HipError StreamGetCaptureInfo(IntPtr stream, IntPtr captureStatus, IntPtr identifier) =>
        Optional(() => _completeApi.StreamGetCaptureInfo(stream, captureStatus, identifier));

    public HipError StreamGetCaptureInfoV2(IntPtr stream, IntPtr captureStatus, IntPtr identifier, IntPtr graph, IntPtr dependencies, IntPtr dependencyCount) =>
        Optional(() => _completeApi.StreamGetCaptureInfoV2(stream, captureStatus, identifier, graph, dependencies, dependencyCount));

    public HipError StreamGetDevice(IntPtr stream, IntPtr device) => Optional(() => _completeApi.StreamGetDevice(stream, device));

    public HipError StreamGetFlags(IntPtr stream, IntPtr flags) => Optional(() => _completeApi.StreamGetFlags(stream, flags));

    public HipError StreamGetId(IntPtr stream, IntPtr identifier) => Optional(() => _completeApi.StreamGetId(stream, identifier));

    public HipError StreamGetPriority(IntPtr stream, IntPtr priority) => Optional(() => _completeApi.StreamGetPriority(stream, priority));

    public HipError StreamWaitEvent(IntPtr stream, IntPtr eventHandle, uint flags) =>
        Optional(() => _completeApi.StreamWaitEvent(stream, eventHandle, flags));

    public HipError StreamWaitValue32(IntPtr stream, IntPtr pointer, uint value, uint flags, uint mask) =>
        Optional(() => _completeApi.StreamWaitValue32(stream, pointer, value, flags, mask));

    public HipError StreamWaitValue64(IntPtr stream, IntPtr pointer, ulong value, uint flags, ulong mask) =>
        Optional(() => _completeApi.StreamWaitValue64(stream, pointer, value, flags, mask));

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags) =>
        HipNativeMethods.EventCreateWithFlags(out eventHandle, flags);

    public HipError EventDestroy(IntPtr eventHandle) => HipNativeMethods.EventDestroy(eventHandle);

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream) => HipNativeMethods.EventRecord(eventHandle, stream);

    public HipError EventSynchronize(IntPtr eventHandle) => HipNativeMethods.EventSynchronize(eventHandle);

    public HipError EventQuery(IntPtr eventHandle) => HipNativeMethods.EventQuery(eventHandle);

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end) =>
        HipNativeMethods.EventElapsedTime(out milliseconds, start, end);

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module)
    {
        GCHandle pinned = GCHandle.Alloc(codeObject, GCHandleType.Pinned);
        try
        {
            return HipNativeMethods.ModuleLoadData(out module, pinned.AddrOfPinnedObject());
        }
        finally
        {
            pinned.Free();
        }
    }

    public HipError ModuleUnload(IntPtr module) => HipNativeMethods.ModuleUnload(module);

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function)
    {
        using (var nativeName = new Utf8NativeString(kernelName, nameof(kernelName)))
        {
            return HipNativeMethods.ModuleGetFunction(out function, module, nativeName.Pointer);
        }
    }

    public HipError ModuleGetGlobal(IntPtr module, string symbolName, out IntPtr pointer, out UIntPtr byteCount)
    {
        IntPtr resultPointer = IntPtr.Zero;
        UIntPtr resultByteCount = UIntPtr.Zero;
        using (var nativeName = new Utf8NativeString(symbolName, nameof(symbolName)))
        {
            HipError error = Optional(() => HipNativeMethods.ModuleGetGlobal(
                out resultPointer, out resultByteCount, module, nativeName.Pointer));
            pointer = resultPointer;
            byteCount = resultByteCount;
            return error;
        }
    }

    public HipError FuncGetAttribute(out int value, HipFunctionAttributeNative attribute, IntPtr function)
    {
        int result = 0;
        HipError error = Optional(() => HipNativeMethods.FuncGetAttribute(out result, attribute, function));
        value = result;
        return error;
    }

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessor(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes)
    {
        int result = 0;
        HipError error = Optional(() => HipNativeMethods.ModuleOccupancyMaxActiveBlocksPerMultiprocessor(
            out result, function, blockSize, dynamicSharedMemoryBytes));
        activeBlocks = result;
        return error;
    }

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes,
        uint flags)
    {
        int result = 0;
        HipError error = Optional(() => HipNativeMethods.ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
            out result, function, blockSize, dynamicSharedMemoryBytes, flags));
        activeBlocks = result;
        return error;
    }

    public HipError ModuleOccupancyMaxPotentialBlockSize(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit)
    {
        int grid = 0;
        int block = 0;
        HipError error = Optional(() => HipNativeMethods.ModuleOccupancyMaxPotentialBlockSize(
            out grid, out block, function, dynamicSharedMemoryBytes, blockSizeLimit));
        minimumGridSize = grid;
        blockSize = block;
        return error;
    }

    public HipError ModuleOccupancyMaxPotentialBlockSizeWithFlags(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit,
        uint flags)
    {
        int grid = 0;
        int block = 0;
        HipError error = Optional(() => HipNativeMethods.ModuleOccupancyMaxPotentialBlockSizeWithFlags(
            out grid, out block, function, dynamicSharedMemoryBytes, blockSizeLimit, flags));
        minimumGridSize = grid;
        blockSize = block;
        return error;
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
        IntPtr kernelParameters) =>
        HipNativeMethods.ModuleLaunchKernel(
            function,
            gridX,
            gridY,
            gridZ,
            blockX,
            blockY,
            blockZ,
            sharedMemoryBytes,
            stream,
            kernelParameters,
            IntPtr.Zero);

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
        IntPtr kernelParameters) =>
        Optional(() => HipNativeMethods.ModuleLaunchCooperativeKernel(
            function,
            gridX,
            gridY,
            gridZ,
            blockX,
            blockY,
            blockZ,
            sharedMemoryBytes,
            stream,
            kernelParameters));

    public string GetErrorName(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorName(error));

    public string GetErrorString(HipError error) => ReadBorrowedString(HipNativeMethods.GetErrorString(error));

    public HipError DestroyExternalMemory(IntPtr externalMemory) => Optional(() => _completeApi.DestroyExternalMemory(externalMemory));
    public HipError DestroyExternalSemaphore(IntPtr externalSemaphore) => Optional(() => _completeApi.DestroyExternalSemaphore(externalSemaphore));
    public HipError ExternalMemoryGetMappedBuffer(IntPtr devicePointer, IntPtr externalMemory, IntPtr bufferDescriptor) => Optional(() => _completeApi.ExternalMemoryGetMappedBuffer(devicePointer, externalMemory, bufferDescriptor));
    public HipError ExternalMemoryGetMappedMipmappedArray(IntPtr mipmappedArray, IntPtr externalMemory, IntPtr mipmappedArrayDescriptor) => Optional(() => _completeApi.ExternalMemoryGetMappedMipmappedArray(mipmappedArray, externalMemory, mipmappedArrayDescriptor));
    public HipError GraphAddExternalSemaphoresSignalNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters) => Optional(() => _completeApi.GraphAddExternalSemaphoresSignalNode(node, graph, dependencies, dependencyCount, parameters));
    public HipError GraphAddExternalSemaphoresWaitNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters) => Optional(() => _completeApi.GraphAddExternalSemaphoresWaitNode(node, graph, dependencies, dependencyCount, parameters));
    public HipError GraphExecExternalSemaphoresSignalNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExecExternalSemaphoresSignalNodeSetParams(graphExec, node, parameters));
    public HipError GraphExecExternalSemaphoresWaitNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExecExternalSemaphoresWaitNodeSetParams(graphExec, node, parameters));
    public HipError GraphExternalSemaphoresSignalNodeGetParams(IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExternalSemaphoresSignalNodeGetParams(node, parameters));
    public HipError GraphExternalSemaphoresSignalNodeSetParams(IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExternalSemaphoresSignalNodeSetParams(node, parameters));
    public HipError GraphExternalSemaphoresWaitNodeGetParams(IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExternalSemaphoresWaitNodeGetParams(node, parameters));
    public HipError GraphExternalSemaphoresWaitNodeSetParams(IntPtr node, IntPtr parameters) => Optional(() => _completeApi.GraphExternalSemaphoresWaitNodeSetParams(node, parameters));
    public HipError GraphicsMapResources(int count, IntPtr resources, IntPtr stream) => Optional(() => _completeApi.GraphicsMapResources(count, resources, stream));
    public HipError GraphicsResourceGetMappedPointer(IntPtr devicePointer, IntPtr size, IntPtr resource) => Optional(() => _completeApi.GraphicsResourceGetMappedPointer(devicePointer, size, resource));
    public HipError GraphicsSubResourceGetMappedArray(IntPtr array, IntPtr resource, uint arrayIndex, uint mipLevel) => Optional(() => _completeApi.GraphicsSubResourceGetMappedArray(array, resource, arrayIndex, mipLevel));
    public HipError GraphicsUnmapResources(int count, IntPtr resources, IntPtr stream) => Optional(() => _completeApi.GraphicsUnmapResources(count, resources, stream));
    public HipError GraphicsUnregisterResource(IntPtr resource) => Optional(() => _completeApi.GraphicsUnregisterResource(resource));
    public HipError ImportExternalMemory(IntPtr externalMemory, IntPtr descriptor) => Optional(() => _completeApi.ImportExternalMemory(externalMemory, descriptor));
    public HipError ImportExternalSemaphore(IntPtr externalSemaphore, IntPtr descriptor) => Optional(() => _completeApi.ImportExternalSemaphore(externalSemaphore, descriptor));
    public HipError IpcCloseMemHandle(IntPtr devicePointer) => Optional(() => _completeApi.IpcCloseMemHandle(devicePointer));
    public HipError IpcGetEventHandle(IntPtr handle, IntPtr eventHandle) => Optional(() => _completeApi.IpcGetEventHandle(handle, eventHandle));
    public HipError IpcGetMemHandle(IntPtr handle, IntPtr devicePointer) => Optional(() => _completeApi.IpcGetMemHandle(handle, devicePointer));
    public HipError IpcOpenEventHandle(IntPtr eventHandle, HipIpcEventHandle handle) => Optional(() => _completeApi.IpcOpenEventHandle(eventHandle, handle));
    public HipError IpcOpenMemHandle(IntPtr devicePointer, HipIpcMemHandle handle, uint flags) => Optional(() => _completeApi.IpcOpenMemHandle(devicePointer, handle, flags));
    public HipError SignalExternalSemaphoresAsync(IntPtr semaphores, IntPtr parameters, uint semaphoreCount, IntPtr stream) => Optional(() => _completeApi.SignalExternalSemaphoresAsync(semaphores, parameters, semaphoreCount, stream));
    public HipError WaitExternalSemaphoresAsync(IntPtr semaphores, IntPtr parameters, uint semaphoreCount, IntPtr stream) => Optional(() => _completeApi.WaitExternalSemaphoresAsync(semaphores, parameters, semaphoreCount, stream));
    public HipError GraphReleaseUserObject(IntPtr graph, IntPtr userObject, uint count) => Optional(() => _completeApi.GraphReleaseUserObject(graph, userObject, count));
    public HipError GraphRetainUserObject(IntPtr graph, IntPtr userObject, uint count, uint flags) => Optional(() => _completeApi.GraphRetainUserObject(graph, userObject, count, flags));
    public HipError ProfilerStart() => Optional(() => _completeApi.ProfilerStart());
    public HipError ProfilerStop() => Optional(() => _completeApi.ProfilerStop());
    public HipError StreamAddCallback(IntPtr stream, IntPtr callback, IntPtr userData, uint flags) => Optional(() => _completeApi.StreamAddCallback(stream, callback, userData, flags));
    public HipError UserObjectCreate(IntPtr userObject, IntPtr value, IntPtr destroy, uint initialRefCount, uint flags) => Optional(() => _completeApi.UserObjectCreate(userObject, value, destroy, initialRefCount, flags));
    public HipError UserObjectRelease(IntPtr userObject, uint count) => Optional(() => _completeApi.UserObjectRelease(userObject, count));
    public HipError UserObjectRetain(IntPtr userObject, uint count) => Optional(() => _completeApi.UserObjectRetain(userObject, count));
    public HipError DrvGetErrorName(HipError error, IntPtr name) => Optional(() => _completeApi.DrvGetErrorName(error, name));
    public HipError DrvGetErrorString(HipError error, IntPtr message) => Optional(() => _completeApi.DrvGetErrorString(error, message));
    public HipError DrvGraphAddMemcpyNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr copyParameters, IntPtr context) => Optional(() => _completeApi.DrvGraphAddMemcpyNode(node, graph, dependencies, dependencyCount, copyParameters, context));
    public HipError DrvGraphAddMemFreeNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr devicePointer) => Optional(() => _completeApi.DrvGraphAddMemFreeNode(node, graph, dependencies, dependencyCount, devicePointer));
    public HipError DrvGraphAddMemsetNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr memsetParameters, IntPtr context) => Optional(() => _completeApi.DrvGraphAddMemsetNode(node, graph, dependencies, dependencyCount, memsetParameters, context));
    public HipError DrvGraphExecMemcpyNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr copyParameters, IntPtr context) => Optional(() => _completeApi.DrvGraphExecMemcpyNodeSetParams(graphExec, node, copyParameters, context));
    public HipError DrvGraphExecMemsetNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr memsetParameters, IntPtr context) => Optional(() => _completeApi.DrvGraphExecMemsetNodeSetParams(graphExec, node, memsetParameters, context));
    public HipError DrvGraphMemcpyNodeGetParams(IntPtr node, IntPtr copyParameters) => Optional(() => _completeApi.DrvGraphMemcpyNodeGetParams(node, copyParameters));
    public HipError DrvGraphMemcpyNodeSetParams(IntPtr node, IntPtr copyParameters) => Optional(() => _completeApi.DrvGraphMemcpyNodeSetParams(node, copyParameters));
    public HipError DrvLaunchKernelEx(IntPtr configuration, IntPtr function, IntPtr parameters, IntPtr extra) => Optional(() => _completeApi.DrvLaunchKernelEx(configuration, function, parameters, extra));
    public HipError DrvMemcpy2DUnaligned(IntPtr copyParameters) => Optional(() => _completeApi.DrvMemcpy2DUnaligned(copyParameters));
    public HipError DrvMemcpy3D(IntPtr copyParameters) => Optional(() => _completeApi.DrvMemcpy3D(copyParameters));
    public HipError DrvMemcpy3DAsync(IntPtr copyParameters, IntPtr stream) => Optional(() => _completeApi.DrvMemcpy3DAsync(copyParameters, stream));
    public HipError DrvPointerGetAttributes(uint count, IntPtr attributes, IntPtr values, IntPtr devicePointer) => Optional(() => _completeApi.DrvPointerGetAttributes(count, attributes, values, devicePointer));

    private static string ReadBorrowedString(IntPtr pointer) =>
        pointer == IntPtr.Zero ? string.Empty : Marshal.PtrToStringAnsi(pointer) ?? string.Empty;

    private static HipError Optional(Func<HipError> call)
    {
        try
        {
            return call();
        }
        catch (EntryPointNotFoundException)
        {
            return HipError.NotSupported;
        }
    }
}
