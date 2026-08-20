using System;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

namespace JYPPX.ROCm.HipSharp.Interop;

/// <summary>
/// 定义托管层使用的可替换 HIP Runtime 边界 / Defines the replaceable HIP Runtime boundary used by the managed layer.
/// </summary>
internal interface IHipNativeApi
{
    public HipError Init(uint flags);

    public HipError RuntimeGetVersion(out int runtimeVersion);

    public HipError DriverGetVersion(out int driverVersion);

    public HipError GetDeviceCount(out int count);

    public HipError GetDevice(out int deviceId);

    public HipError SetDevice(int deviceId);

    public HipError DeviceGetName(int deviceId, out string name);

    public HipError DeviceGetAttribute(out int value, HipDeviceAttribute attribute, int deviceId);

    public HipError DeviceComputeCapability(IntPtr major, IntPtr minor, int device);

    public HipError DeviceGet(IntPtr device, int ordinal);

    public HipError DeviceGetByPCIBusId(IntPtr device, IntPtr pciBusId);

    public HipError DeviceGetCacheConfig(IntPtr cacheConfig);

    public HipError DeviceGetGraphMemAttribute(int device, int attribute, IntPtr value);

    public HipError DeviceGetLimit(IntPtr value, int limit);

    public HipError DeviceGetP2PAttribute(IntPtr value, int attribute, int sourceDevice, int destinationDevice);

    public HipError DeviceGetPCIBusId(IntPtr pciBusId, int length, int device);

    public HipError DeviceGetSharedMemConfig(IntPtr config);

    public HipError DeviceGetStreamPriorityRange(IntPtr leastPriority, IntPtr greatestPriority);

    public HipError DeviceGetUuid(IntPtr uuid, int device);

    public HipError DeviceTotalMem(IntPtr bytes, int device);

    public HipError GetSymbolAddress(IntPtr devicePointer, IntPtr symbol);

    public HipError GetSymbolSize(IntPtr size, IntPtr symbol);

    public HipError PointerGetAttribute(IntPtr data, int attribute, IntPtr pointer);

    public HipError PointerGetAttributes(IntPtr attributes, IntPtr pointer);

    public HipError PointerSetAttribute(IntPtr value, int attribute, IntPtr pointer);

    public HipError MemGetInfo(out UIntPtr freeBytes, out UIntPtr totalBytes);

    public HipError MemAddressFree(IntPtr address, UIntPtr size);

    public HipError MemAddressReserve(IntPtr address, UIntPtr size, UIntPtr alignment, IntPtr requestedAddress, ulong flags);

    public HipError MemCreate(IntPtr handle, UIntPtr size, IntPtr properties, ulong flags);

    public HipError MemExportToShareableHandle(IntPtr shareableHandle, IntPtr handle, int handleType, ulong flags);

    public HipError MemGetAccess(IntPtr flags, IntPtr location, IntPtr address);

    public HipError MemImportFromShareableHandle(IntPtr handle, IntPtr operatingSystemHandle, int handleType);

    public HipError MemMap(IntPtr address, UIntPtr size, UIntPtr offset, IntPtr handle, ulong flags);

    public HipError MemMapArrayAsync(IntPtr mapInformation, uint count, IntPtr stream);

    public HipError MemRelease(IntPtr handle);

    public HipError MemRetainAllocationHandle(IntPtr handle, IntPtr address);

    public HipError MemSetAccess(IntPtr address, UIntPtr size, IntPtr descriptors, UIntPtr count);

    public HipError MemUnmap(IntPtr address, UIntPtr size);

    public HipError Array3DCreate(IntPtr array, IntPtr descriptor);

    public HipError Array3DGetDescriptor(IntPtr descriptor, IntPtr array);

    public HipError ArrayCreate(IntPtr array, IntPtr descriptor);

    public HipError ArrayDestroy(IntPtr array);

    public HipError ArrayGetDescriptor(IntPtr descriptor, IntPtr array);

    public HipError ArrayGetInfo(IntPtr descriptor, IntPtr extent, IntPtr flags, IntPtr array);

    public HipError BindTexture(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr size);

    public HipError BindTexture2D(IntPtr offset, IntPtr textureReference, IntPtr devicePointer, IntPtr descriptor, UIntPtr width, UIntPtr height, UIntPtr pitch);

    public HipError BindTextureToArray(IntPtr textureReference, IntPtr array, IntPtr descriptor);

    public HipError BindTextureToMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, IntPtr descriptor);

    public HipError CreateSurfaceObject(IntPtr surfaceObject, IntPtr resourceDescriptor);

    public HipError CreateTextureObject(IntPtr textureObject, IntPtr resourceDescriptor, IntPtr textureDescriptor, IntPtr resourceViewDescriptor);

    public HipError DestroySurfaceObject(ulong surfaceObject);

    public HipError DestroyTextureObject(ulong textureObject);

    public HipError DeviceGetTexture1DLinearMaxWidth(IntPtr maxWidth, IntPtr descriptor, int device);

    public HipError FreeArray(IntPtr array);

    public HipError FreeMipmappedArray(IntPtr mipmappedArray);

    public HipError GetMipmappedArrayLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level);

    public HipError GetTextureAlignmentOffset(IntPtr offset, IntPtr textureReference);

    public HipError GetTextureObjectResourceDesc(IntPtr resourceDescriptor, ulong textureObject);

    public HipError GetTextureObjectResourceViewDesc(IntPtr resourceViewDescriptor, ulong textureObject);

    public HipError GetTextureObjectTextureDesc(IntPtr textureDescriptor, ulong textureObject);

    public HipError GetTextureReference(IntPtr textureReference, IntPtr symbol);

    public HipError Malloc3DArray(IntPtr array, IntPtr descriptor, HipExtent extent, uint flags);

    public HipError MallocArray(IntPtr array, IntPtr descriptor, UIntPtr width, UIntPtr height, uint flags);

    public HipError MallocMipmappedArray(IntPtr mipmappedArray, IntPtr descriptor, HipExtent extent, uint levels, uint flags);

    public HipError Memcpy2DArrayToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind);

    public HipError Memcpy2DFromArray(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind);

    public HipError Memcpy2DFromArrayAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr width, UIntPtr height, int kind, IntPtr stream);

    public HipError Memcpy2DToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind);

    public HipError Memcpy2DToArrayAsync(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr sourcePitch, UIntPtr width, UIntPtr height, int kind, IntPtr stream);

    public HipError MemcpyFromArray(IntPtr destination, IntPtr source, UIntPtr sourceX, UIntPtr sourceY, UIntPtr count, int kind);

    public HipError MemcpyToArray(IntPtr destination, UIntPtr destinationX, UIntPtr destinationY, IntPtr source, UIntPtr count, int kind);

    public HipError MipmappedArrayCreate(IntPtr mipmappedArray, IntPtr descriptor, uint levels);

    public HipError MipmappedArrayDestroy(IntPtr mipmappedArray);

    public HipError MipmappedArrayGetLevel(IntPtr levelArray, IntPtr mipmappedArray, uint level);

    public HipError TexObjectGetTextureDesc(IntPtr textureDescriptor, ulong textureObject);

    public HipError TexRefGetArray(IntPtr array, IntPtr textureReference);

    public HipError TexRefGetMipMappedArray(IntPtr mipmappedArray, IntPtr textureReference);

    public HipError TexRefSetArray(IntPtr textureReference, IntPtr array, uint flags);

    public HipError TexRefSetMipmappedArray(IntPtr textureReference, IntPtr mipmappedArray, uint flags);

    public HipError UnbindTexture(IntPtr textureReference);

    public HipError MallocPitch(out IntPtr pointer, out UIntPtr pitch, UIntPtr widthBytes, UIntPtr height);

    public HipError Malloc3D(out HipPitchedPtr pitchedPointer, HipExtent extent);

    public HipError MallocManaged(out IntPtr pointer, UIntPtr byteCount, uint flags);

    public HipError MemPrefetchAsync(IntPtr pointer, UIntPtr byteCount, int device, IntPtr stream);

    public HipError MemAdvise(IntPtr pointer, UIntPtr byteCount, HipMemoryAdvise advice, int device);

    public HipError MallocAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr stream);

    public HipError FreeAsync(IntPtr pointer, IntPtr stream);

    public HipError DeviceGetDefaultMemPool(out IntPtr memoryPool, int deviceOrdinal);

    public HipError DeviceGetMemPool(out IntPtr memoryPool, int deviceOrdinal);

    public HipError DeviceSetMemPool(int deviceOrdinal, IntPtr memoryPool);

    public HipError MemPoolCreate(out IntPtr memoryPool, ref HipMemoryPoolPropertiesNative properties);

    public HipError MemPoolDestroy(IntPtr memoryPool);

    public HipError MemPoolTrimTo(IntPtr memoryPool, UIntPtr minimumBytesToKeep);

    public HipError MemPoolGetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value);

    public HipError MemPoolSetAttribute(IntPtr memoryPool, HipMemoryPoolAttributeNative attribute, IntPtr value);

    public HipError MemPoolSetAccess(IntPtr memoryPool, HipMemoryPoolAccessDescriptorNative[] descriptors);

    public HipError MemPoolGetAccess(out HipMemoryPoolAccess access, IntPtr memoryPool, ref HipMemLocation location);

    public HipError MallocFromPoolAsync(out IntPtr pointer, UIntPtr byteCount, IntPtr memoryPool, IntPtr stream);

    public HipError DeviceCanAccessPeer(out int canAccessPeer, int deviceId, int peerDeviceId);

    public HipError DeviceEnablePeerAccess(int peerDeviceId, uint flags);

    public HipError DeviceDisablePeerAccess(int peerDeviceId);

    public HipError MemcpyPeerAsync(IntPtr destination, int destinationDevice, IntPtr source, int sourceDevice, UIntPtr byteCount, IntPtr stream);

    public HipError StreamBeginCapture(IntPtr stream, HipStreamCaptureMode mode);

    public HipError StreamEndCapture(IntPtr stream, out IntPtr graph);

    public HipError GraphCreate(out IntPtr graph, uint flags);

    public HipError GraphAddEmptyNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount);

    public HipError GraphAddDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount);

    public HipError GraphRemoveDependencies(IntPtr graph, IntPtr from, IntPtr to, UIntPtr dependencyCount);

    public HipError GraphAddKernelNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters);

    public HipError GraphExecKernelNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters);

    public HipError GraphAddMemcpyNode1D(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError GraphExecMemcpyNodeSetParams1D(IntPtr graphExec, IntPtr node, IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError GraphAddMemsetNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters);

    public HipError GraphExecMemsetNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters);

    public HipError GraphAddMemAllocNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters);

    public HipError GraphAddMemFreeNode(out IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr devicePointer);

    public HipError GraphUpload(IntPtr graphExec, IntPtr stream);

    public HipError GraphDestroyNode(IntPtr node);

    public HipError GraphDestroy(IntPtr graph);

    public HipError GraphInstantiateWithFlags(out IntPtr graphExec, IntPtr graph, ulong flags);

    public HipError GraphLaunch(IntPtr graphExec, IntPtr stream);

    public HipError GraphExecDestroy(IntPtr graphExec);

    public HipError Malloc(out IntPtr pointer, UIntPtr byteCount);

    public HipError Free(IntPtr pointer);

    public HipError Memcpy(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind);

    public HipError MemcpyAsync(IntPtr destination, IntPtr source, UIntPtr byteCount, HipMemoryCopyKind kind, IntPtr stream);

    public HipError Memset(IntPtr destination, int value, UIntPtr byteCount);

    public HipError MemsetAsync(IntPtr destination, int value, UIntPtr byteCount, IntPtr stream);

    public HipError Memset2D(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height);

    public HipError Memset2DAsync(IntPtr destination, UIntPtr pitch, int value, UIntPtr widthBytes, UIntPtr height, IntPtr stream);

    public HipError Memset3D(HipPitchedPtr destination, int value, HipExtent extent);

    public HipError Memset3DAsync(HipPitchedPtr destination, int value, HipExtent extent, IntPtr stream);

    public HipError Memcpy2D(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind);

    public HipError Memcpy2DAsync(IntPtr destination, UIntPtr destinationPitch, IntPtr source, UIntPtr sourcePitch, UIntPtr widthBytes, UIntPtr height, HipMemoryCopyKind kind, IntPtr stream);

    public HipError Memcpy3D(ref HipMemcpy3DParameters parameters);

    public HipError Memcpy3DAsync(ref HipMemcpy3DParameters parameters, IntPtr stream);

    public HipError HostMalloc(out IntPtr pointer, UIntPtr byteCount, uint flags);

    public HipError HostFree(IntPtr pointer);

    public HipError DeviceSynchronize();

    public HipError StreamCreateWithFlags(out IntPtr stream, uint flags);

    public HipError StreamDestroy(IntPtr stream);

    public HipError StreamSynchronize(IntPtr stream);

    public HipError StreamQuery(IntPtr stream);

    public HipError ExtStreamGetCUMask(IntPtr stream, uint cuMaskSize, IntPtr cuMask);

    public HipError StreamGetAttribute(IntPtr stream, int attribute, IntPtr value);

    public HipError StreamGetCaptureInfo(IntPtr stream, IntPtr captureStatus, IntPtr identifier);

    public HipError StreamGetCaptureInfoV2(IntPtr stream, IntPtr captureStatus, IntPtr identifier, IntPtr graph, IntPtr dependencies, IntPtr dependencyCount);

    public HipError StreamGetDevice(IntPtr stream, IntPtr device);

    public HipError StreamGetFlags(IntPtr stream, IntPtr flags);

    public HipError StreamGetId(IntPtr stream, IntPtr identifier);

    public HipError StreamGetPriority(IntPtr stream, IntPtr priority);

    public HipError StreamWaitEvent(IntPtr stream, IntPtr eventHandle, uint flags);

    public HipError StreamWaitValue32(IntPtr stream, IntPtr pointer, uint value, uint flags, uint mask);

    public HipError StreamWaitValue64(IntPtr stream, IntPtr pointer, ulong value, uint flags, ulong mask);

    public HipError EventCreateWithFlags(out IntPtr eventHandle, uint flags);

    public HipError EventDestroy(IntPtr eventHandle);

    public HipError EventRecord(IntPtr eventHandle, IntPtr stream);

    public HipError EventSynchronize(IntPtr eventHandle);

    public HipError EventQuery(IntPtr eventHandle);

    public HipError EventElapsedTime(out float milliseconds, IntPtr start, IntPtr end);

    public HipError ModuleLoadData(byte[] codeObject, out IntPtr module);

    public HipError ModuleUnload(IntPtr module);

    public HipError ModuleGetFunction(IntPtr module, string kernelName, out IntPtr function);

    public HipError ModuleGetGlobal(IntPtr module, string symbolName, out IntPtr pointer, out UIntPtr byteCount);

    public HipError FuncGetAttribute(out int value, HipFunctionAttributeNative attribute, IntPtr function);

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessor(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes);

    public HipError ModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags(
        out int activeBlocks,
        IntPtr function,
        int blockSize,
        UIntPtr dynamicSharedMemoryBytes,
        uint flags);

    public HipError ModuleOccupancyMaxPotentialBlockSize(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit);

    public HipError ModuleOccupancyMaxPotentialBlockSizeWithFlags(
        out int minimumGridSize,
        out int blockSize,
        IntPtr function,
        UIntPtr dynamicSharedMemoryBytes,
        int blockSizeLimit,
        uint flags);

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
        IntPtr kernelParameters);

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
        IntPtr kernelParameters);

    public string GetErrorName(HipError error);

    public string GetErrorString(HipError error);

    // Advanced managed interop: external resources, IPC, callbacks, profiler, and driver compatibility.
    public HipError DestroyExternalMemory(IntPtr externalMemory);
    public HipError DestroyExternalSemaphore(IntPtr externalSemaphore);
    public HipError ExternalMemoryGetMappedBuffer(IntPtr devicePointer, IntPtr externalMemory, IntPtr bufferDescriptor);
    public HipError ExternalMemoryGetMappedMipmappedArray(IntPtr mipmappedArray, IntPtr externalMemory, IntPtr mipmappedArrayDescriptor);
    public HipError GraphAddExternalSemaphoresSignalNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters);
    public HipError GraphAddExternalSemaphoresWaitNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr parameters);
    public HipError GraphExecExternalSemaphoresSignalNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters);
    public HipError GraphExecExternalSemaphoresWaitNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr parameters);
    public HipError GraphExternalSemaphoresSignalNodeGetParams(IntPtr node, IntPtr parameters);
    public HipError GraphExternalSemaphoresSignalNodeSetParams(IntPtr node, IntPtr parameters);
    public HipError GraphExternalSemaphoresWaitNodeGetParams(IntPtr node, IntPtr parameters);
    public HipError GraphExternalSemaphoresWaitNodeSetParams(IntPtr node, IntPtr parameters);
    public HipError GraphicsMapResources(int count, IntPtr resources, IntPtr stream);
    public HipError GraphicsResourceGetMappedPointer(IntPtr devicePointer, IntPtr size, IntPtr resource);
    public HipError GraphicsSubResourceGetMappedArray(IntPtr array, IntPtr resource, uint arrayIndex, uint mipLevel);
    public HipError GraphicsUnmapResources(int count, IntPtr resources, IntPtr stream);
    public HipError GraphicsUnregisterResource(IntPtr resource);
    public HipError ImportExternalMemory(IntPtr externalMemory, IntPtr descriptor);
    public HipError ImportExternalSemaphore(IntPtr externalSemaphore, IntPtr descriptor);
    public HipError IpcCloseMemHandle(IntPtr devicePointer);
    public HipError IpcGetEventHandle(IntPtr handle, IntPtr eventHandle);
    public HipError IpcGetMemHandle(IntPtr handle, IntPtr devicePointer);
    public HipError IpcOpenEventHandle(IntPtr eventHandle, HipIpcEventHandle handle);
    public HipError IpcOpenMemHandle(IntPtr devicePointer, HipIpcMemHandle handle, uint flags);
    public HipError SignalExternalSemaphoresAsync(IntPtr semaphores, IntPtr parameters, uint semaphoreCount, IntPtr stream);
    public HipError WaitExternalSemaphoresAsync(IntPtr semaphores, IntPtr parameters, uint semaphoreCount, IntPtr stream);
    public HipError GraphReleaseUserObject(IntPtr graph, IntPtr userObject, uint count);
    public HipError GraphRetainUserObject(IntPtr graph, IntPtr userObject, uint count, uint flags);
    public HipError ProfilerStart();
    public HipError ProfilerStop();
    public HipError StreamAddCallback(IntPtr stream, IntPtr callback, IntPtr userData, uint flags);
    public HipError UserObjectCreate(IntPtr userObject, IntPtr value, IntPtr destroy, uint initialRefCount, uint flags);
    public HipError UserObjectRelease(IntPtr userObject, uint count);
    public HipError UserObjectRetain(IntPtr userObject, uint count);
    public HipError DrvGetErrorName(HipError error, IntPtr name);
    public HipError DrvGetErrorString(HipError error, IntPtr message);
    public HipError DrvGraphAddMemcpyNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr copyParameters, IntPtr context);
    public HipError DrvGraphAddMemFreeNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr devicePointer);
    public HipError DrvGraphAddMemsetNode(IntPtr node, IntPtr graph, IntPtr dependencies, UIntPtr dependencyCount, IntPtr memsetParameters, IntPtr context);
    public HipError DrvGraphExecMemcpyNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr copyParameters, IntPtr context);
    public HipError DrvGraphExecMemsetNodeSetParams(IntPtr graphExec, IntPtr node, IntPtr memsetParameters, IntPtr context);
    public HipError DrvGraphMemcpyNodeGetParams(IntPtr node, IntPtr copyParameters);
    public HipError DrvGraphMemcpyNodeSetParams(IntPtr node, IntPtr copyParameters);
    public HipError DrvLaunchKernelEx(IntPtr configuration, IntPtr function, IntPtr parameters, IntPtr extra);
    public HipError DrvMemcpy2DUnaligned(IntPtr copyParameters);
    public HipError DrvMemcpy3D(IntPtr copyParameters);
    public HipError DrvMemcpy3DAsync(IntPtr copyParameters, IntPtr stream);
    public HipError DrvPointerGetAttributes(uint count, IntPtr attributes, IntPtr values, IntPtr devicePointer);
}
