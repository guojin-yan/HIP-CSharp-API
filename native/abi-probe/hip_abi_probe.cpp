#define __HIP_DISABLE_CPP_FUNCTIONS__
#include <hip/hip_runtime_api.h>
#include <hip/hiprtc.h>

#include <cstddef>
#include <cstdio>
#include <type_traits>

#ifndef HIPSHARP_NORMALIZED_MANIFEST_SHA256
#define HIPSHARP_NORMALIZED_MANIFEST_SHA256 "unprovided"
#endif
#ifndef HIPSHARP_HEADER_SHA256
#define HIPSHARP_HEADER_SHA256 "unprovided"
#endif

static_assert(std::is_same<decltype(&hipInit), hipError_t (*)(unsigned int)>::value, "hipInit signature mismatch");
static_assert(std::is_same<decltype(&hipRuntimeGetVersion), hipError_t (*)(int*)>::value, "hipRuntimeGetVersion signature mismatch");
static_assert(std::is_same<decltype(&hipDriverGetVersion), hipError_t (*)(int*)>::value, "hipDriverGetVersion signature mismatch");
static_assert(std::is_same<decltype(&hipGetDeviceCount), hipError_t (*)(int*)>::value, "hipGetDeviceCount signature mismatch");
static_assert(std::is_same<decltype(&hipGetDevice), hipError_t (*)(int*)>::value, "hipGetDevice signature mismatch");
static_assert(std::is_same<decltype(&hipSetDevice), hipError_t (*)(int)>::value, "hipSetDevice signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceGetName), hipError_t (*)(char*, int, int)>::value, "hipDeviceGetName signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceGetAttribute), hipError_t (*)(int*, hipDeviceAttribute_t, int)>::value, "hipDeviceGetAttribute signature mismatch");
static_assert(std::is_same<decltype(&hipMallocManaged), hipError_t (*)(void**, std::size_t, unsigned int)>::value, "hipMallocManaged signature mismatch");
static_assert(std::is_same<decltype(&hipMemPrefetchAsync), hipError_t (*)(const void*, std::size_t, int, hipStream_t)>::value, "hipMemPrefetchAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemAdvise), hipError_t (*)(const void*, std::size_t, hipMemoryAdvise, int)>::value, "hipMemAdvise signature mismatch");
using HipMallocAsyncSignature = hipError_t (*)(void**, std::size_t, hipStream_t);
static_assert(std::is_same<decltype(static_cast<HipMallocAsyncSignature>(&hipMallocAsync)), HipMallocAsyncSignature>::value, "hipMallocAsync signature mismatch");
static_assert(std::is_same<decltype(&hipFreeAsync), hipError_t (*)(void*, hipStream_t)>::value, "hipFreeAsync signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceGetDefaultMemPool), hipError_t (*)(hipMemPool_t*, int)>::value, "hipDeviceGetDefaultMemPool signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceGetMemPool), hipError_t (*)(hipMemPool_t*, int)>::value, "hipDeviceGetMemPool signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceSetMemPool), hipError_t (*)(int, hipMemPool_t)>::value, "hipDeviceSetMemPool signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolCreate), hipError_t (*)(hipMemPool_t*, const hipMemPoolProps*)>::value, "hipMemPoolCreate signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolDestroy), hipError_t (*)(hipMemPool_t)>::value, "hipMemPoolDestroy signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolTrimTo), hipError_t (*)(hipMemPool_t, std::size_t)>::value, "hipMemPoolTrimTo signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolGetAttribute), hipError_t (*)(hipMemPool_t, hipMemPoolAttr, void*)>::value, "hipMemPoolGetAttribute signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolSetAttribute), hipError_t (*)(hipMemPool_t, hipMemPoolAttr, void*)>::value, "hipMemPoolSetAttribute signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolSetAccess), hipError_t (*)(hipMemPool_t, const hipMemAccessDesc*, std::size_t)>::value, "hipMemPoolSetAccess signature mismatch");
static_assert(std::is_same<decltype(&hipMemPoolGetAccess), hipError_t (*)(hipMemAccessFlags*, hipMemPool_t, hipMemLocation*)>::value, "hipMemPoolGetAccess signature mismatch");
static_assert(std::is_same<decltype(&hipMallocFromPoolAsync), hipError_t (*)(void**, std::size_t, hipMemPool_t, hipStream_t)>::value, "hipMallocFromPoolAsync signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceCanAccessPeer), hipError_t (*)(int*, int, int)>::value, "hipDeviceCanAccessPeer signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceEnablePeerAccess), hipError_t (*)(int, unsigned int)>::value, "hipDeviceEnablePeerAccess signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceDisablePeerAccess), hipError_t (*)(int)>::value, "hipDeviceDisablePeerAccess signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpyPeerAsync), hipError_t (*)(void*, int, const void*, int, std::size_t, hipStream_t)>::value, "hipMemcpyPeerAsync signature mismatch");
static_assert(std::is_same<decltype(&hipStreamBeginCapture), hipError_t (*)(hipStream_t, hipStreamCaptureMode)>::value, "hipStreamBeginCapture signature mismatch");
static_assert(std::is_same<decltype(&hipStreamEndCapture), hipError_t (*)(hipStream_t, hipGraph_t*)>::value, "hipStreamEndCapture signature mismatch");
static_assert(std::is_same<decltype(&hipGraphDestroy), hipError_t (*)(hipGraph_t)>::value, "hipGraphDestroy signature mismatch");
static_assert(std::is_same<decltype(&hipGraphInstantiateWithFlags), hipError_t (*)(hipGraphExec_t*, hipGraph_t, unsigned long long)>::value, "hipGraphInstantiateWithFlags signature mismatch");
static_assert(std::is_same<decltype(&hipGraphLaunch), hipError_t (*)(hipGraphExec_t, hipStream_t)>::value, "hipGraphLaunch signature mismatch");
static_assert(std::is_same<decltype(&hipGraphExecDestroy), hipError_t (*)(hipGraphExec_t)>::value, "hipGraphExecDestroy signature mismatch");
static_assert(std::is_same<decltype(&hipMalloc), hipError_t (*)(void**, std::size_t)>::value, "hipMalloc signature mismatch");
static_assert(std::is_same<decltype(&hipMemGetInfo), hipError_t (*)(std::size_t*, std::size_t*)>::value, "hipMemGetInfo signature mismatch");
static_assert(std::is_same<decltype(&hipMallocPitch), hipError_t (*)(void**, std::size_t*, std::size_t, std::size_t)>::value, "hipMallocPitch signature mismatch");
static_assert(std::is_same<decltype(&hipMalloc3D), hipError_t (*)(hipPitchedPtr*, hipExtent)>::value, "hipMalloc3D signature mismatch");
static_assert(std::is_same<decltype(&hipFree), hipError_t (*)(void*)>::value, "hipFree signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy), hipError_t (*)(void*, const void*, std::size_t, hipMemcpyKind)>::value, "hipMemcpy signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpyAsync), hipError_t (*)(void*, const void*, std::size_t, hipMemcpyKind, hipStream_t)>::value, "hipMemcpyAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemset), hipError_t (*)(void*, int, std::size_t)>::value, "hipMemset signature mismatch");
static_assert(std::is_same<decltype(&hipMemsetAsync), hipError_t (*)(void*, int, std::size_t, hipStream_t)>::value, "hipMemsetAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemset2D), hipError_t (*)(void*, std::size_t, int, std::size_t, std::size_t)>::value, "hipMemset2D signature mismatch");
static_assert(std::is_same<decltype(&hipMemset2DAsync), hipError_t (*)(void*, std::size_t, int, std::size_t, std::size_t, hipStream_t)>::value, "hipMemset2DAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemset3D), hipError_t (*)(hipPitchedPtr, int, hipExtent)>::value, "hipMemset3D signature mismatch");
static_assert(std::is_same<decltype(&hipMemset3DAsync), hipError_t (*)(hipPitchedPtr, int, hipExtent, hipStream_t)>::value, "hipMemset3DAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy2D), hipError_t (*)(void*, std::size_t, const void*, std::size_t, std::size_t, std::size_t, hipMemcpyKind)>::value, "hipMemcpy2D signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy2DAsync), hipError_t (*)(void*, std::size_t, const void*, std::size_t, std::size_t, std::size_t, hipMemcpyKind, hipStream_t)>::value, "hipMemcpy2DAsync signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy3D), hipError_t (*)(const hipMemcpy3DParms*)>::value, "hipMemcpy3D signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy3DAsync), hipError_t (*)(const hipMemcpy3DParms*, hipStream_t)>::value, "hipMemcpy3DAsync signature mismatch");
static_assert(std::is_same<decltype(&hipHostMalloc), hipError_t (*)(void**, std::size_t, unsigned int)>::value, "hipHostMalloc signature mismatch");
static_assert(std::is_same<decltype(&hipHostFree), hipError_t (*)(void*)>::value, "hipHostFree signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceSynchronize), hipError_t (*)()>::value, "hipDeviceSynchronize signature mismatch");
static_assert(std::is_same<decltype(&hipStreamCreateWithFlags), hipError_t (*)(hipStream_t*, unsigned int)>::value, "hipStreamCreateWithFlags signature mismatch");
static_assert(std::is_same<decltype(&hipStreamDestroy), hipError_t (*)(hipStream_t)>::value, "hipStreamDestroy signature mismatch");
static_assert(std::is_same<decltype(&hipStreamSynchronize), hipError_t (*)(hipStream_t)>::value, "hipStreamSynchronize signature mismatch");
static_assert(std::is_same<decltype(&hipStreamQuery), hipError_t (*)(hipStream_t)>::value, "hipStreamQuery signature mismatch");
static_assert(std::is_same<decltype(&hipEventCreateWithFlags), hipError_t (*)(hipEvent_t*, unsigned int)>::value, "hipEventCreateWithFlags signature mismatch");
static_assert(std::is_same<decltype(&hipEventDestroy), hipError_t (*)(hipEvent_t)>::value, "hipEventDestroy signature mismatch");
static_assert(std::is_same<decltype(&hipEventRecord), hipError_t (*)(hipEvent_t, hipStream_t)>::value, "hipEventRecord signature mismatch");
static_assert(std::is_same<decltype(&hipEventSynchronize), hipError_t (*)(hipEvent_t)>::value, "hipEventSynchronize signature mismatch");
static_assert(std::is_same<decltype(&hipEventQuery), hipError_t (*)(hipEvent_t)>::value, "hipEventQuery signature mismatch");
static_assert(std::is_same<decltype(&hipEventElapsedTime), hipError_t (*)(float*, hipEvent_t, hipEvent_t)>::value, "hipEventElapsedTime signature mismatch");
static_assert(std::is_same<decltype(&hipGetErrorName), const char* (*)(hipError_t)>::value, "hipGetErrorName signature mismatch");
static_assert(std::is_same<decltype(&hipGetErrorString), const char* (*)(hipError_t)>::value, "hipGetErrorString signature mismatch");
static_assert(std::is_same<decltype(&hipModuleLoadData), hipError_t (*)(hipModule_t*, const void*)>::value, "hipModuleLoadData signature mismatch");
static_assert(std::is_same<decltype(&hipModuleUnload), hipError_t (*)(hipModule_t)>::value, "hipModuleUnload signature mismatch");
static_assert(std::is_same<decltype(&hipModuleGetFunction), hipError_t (*)(hipFunction_t*, hipModule_t, const char*)>::value, "hipModuleGetFunction signature mismatch");
static_assert(
    std::is_same<
        decltype(&hipModuleLaunchKernel),
        hipError_t (*)(hipFunction_t, unsigned int, unsigned int, unsigned int, unsigned int, unsigned int, unsigned int, unsigned int, hipStream_t, void**, void**)>::value,
    "hipModuleLaunchKernel signature mismatch");
static_assert(std::is_same<decltype(&hiprtcVersion), hiprtcResult (*)(int*, int*)>::value, "hiprtcVersion signature mismatch");
static_assert(std::is_same<decltype(&hiprtcGetErrorString), const char* (*)(hiprtcResult)>::value, "hiprtcGetErrorString signature mismatch");
static_assert(std::is_same<decltype(&hiprtcCreateProgram), hiprtcResult (*)(hiprtcProgram*, const char*, const char*, int, const char* const*, const char* const*)>::value, "hiprtcCreateProgram signature mismatch");
static_assert(std::is_same<decltype(&hiprtcDestroyProgram), hiprtcResult (*)(hiprtcProgram*)>::value, "hiprtcDestroyProgram signature mismatch");
static_assert(std::is_same<decltype(&hiprtcCompileProgram), hiprtcResult (*)(hiprtcProgram, int, const char* const*)>::value, "hiprtcCompileProgram signature mismatch");
static_assert(std::is_same<decltype(&hiprtcGetProgramLogSize), hiprtcResult (*)(hiprtcProgram, std::size_t*)>::value, "hiprtcGetProgramLogSize signature mismatch");
static_assert(std::is_same<decltype(&hiprtcGetProgramLog), hiprtcResult (*)(hiprtcProgram, char*)>::value, "hiprtcGetProgramLog signature mismatch");
static_assert(std::is_same<decltype(&hiprtcGetCodeSize), hiprtcResult (*)(hiprtcProgram, std::size_t*)>::value, "hiprtcGetCodeSize signature mismatch");
static_assert(std::is_same<decltype(&hiprtcGetCode), hiprtcResult (*)(hiprtcProgram, char*)>::value, "hiprtcGetCode signature mismatch");

int main()
{
    std::printf(
        "{\n"
        "  \"schemaVersion\": 4,\n"
        "  \"normalizedManifestHash\": \"%s\",\n"
        "  \"headerHash\": \"%s\",\n"
        "  \"staticAssertions\": true,\n"
        "  \"hipErrorSize\": %zu,\n"
        "  \"hipErrorAlignment\": %zu,\n"
        "  \"hipMemcpyKindSize\": %zu,\n"
        "  \"hipMemcpyKindAlignment\": %zu,\n"
        "  \"hipMemoryAdviseSize\": %zu,\n"
        "  \"hipMemoryAdviseAlignment\": %zu,\n"
        "  \"hipStreamCaptureModeSize\": %zu,\n"
        "  \"hipStreamCaptureModeAlignment\": %zu,\n"
        "  \"pointerSize\": %zu,\n"
        "  \"pointerAlignment\": %zu,\n"
        "  \"hiprtcResultSize\": %zu,\n"
        "  \"hiprtcResultAlignment\": %zu,\n"
        "  \"hiprtcProgramSize\": %zu,\n"
        "  \"hipModuleHandleSize\": %zu,\n"
        "  \"hipFunctionHandleSize\": %zu,\n"
        "  \"hipGraphHandleSize\": %zu,\n"
        "  \"hipGraphExecHandleSize\": %zu,\n"
        "  \"sizeTSize\": %zu,\n"
        "  \"sizeTAlignment\": %zu,\n"
        "  \"dim3Size\": %zu,\n"
        "  \"dim3Alignment\": %zu,\n"
        "  \"dim3OffsetX\": %zu,\n"
        "  \"dim3OffsetY\": %zu,\n"
        "  \"dim3OffsetZ\": %zu,\n"
        "  \"hipPosSize\": %zu,\n"
        "  \"hipPosAlignment\": %zu,\n"
        "  \"hipPitchedPtrSize\": %zu,\n"
        "  \"hipPitchedPtrAlignment\": %zu,\n"
        "  \"hipMemcpy3DParmsSize\": %zu,\n"
        "  \"hipMemcpy3DParmsAlignment\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetSrcArray\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetSrcPos\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetSrcPtr\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetDstArray\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetDstPos\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetDstPtr\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetExtent\": %zu,\n"
        "  \"hipMemcpy3DParmsOffsetKind\": %zu,\n"
        "  \"hipMemLocationSize\": %zu,\n"
        "  \"hipMemLocationAlignment\": %zu,\n"
        "  \"hipMemAccessDescSize\": %zu,\n"
        "  \"hipMemAccessDescAlignment\": %zu,\n"
        "  \"hipMemAccessDescOffsetLocation\": %zu,\n"
        "  \"hipMemAccessDescOffsetFlags\": %zu,\n"
        "  \"hipMemPoolPropsSize\": %zu,\n"
        "  \"hipMemPoolPropsAlignment\": %zu,\n"
        "  \"hipMemPoolPropsOffsetAllocType\": %zu,\n"
        "  \"hipMemPoolPropsOffsetHandleTypes\": %zu,\n"
        "  \"hipMemPoolPropsOffsetLocation\": %zu,\n"
        "  \"hipMemPoolPropsOffsetWin32SecurityAttributes\": %zu,\n"
        "  \"hipMemPoolPropsOffsetMaxSize\": %zu,\n"
        "  \"hipMemPoolPropsOffsetReserved\": %zu,\n"
        "  \"hipMemLocationTypeDevice\": %d,\n"
        "  \"hipMemAccessFlagsProtNone\": %d,\n"
        "  \"hipMemAccessFlagsProtReadWrite\": %d,\n"
        "  \"hipMemAllocationTypePinned\": %d,\n"
        "  \"hipMemHandleTypeNone\": %d,\n"
        "  \"hipMemPoolReuseFollowEventDependencies\": %d,\n"
        "  \"hipMemPoolReuseAllowOpportunistic\": %d,\n"
        "  \"hipMemPoolReuseAllowInternalDependencies\": %d,\n"
        "  \"hipMemPoolAttrReleaseThreshold\": %d,\n"
        "  \"hipMemPoolAttrReservedMemCurrent\": %d,\n"
        "  \"hipMemPoolAttrReservedMemHigh\": %d,\n"
        "  \"hipMemPoolAttrUsedMemCurrent\": %d,\n"
        "  \"hipMemPoolAttrUsedMemHigh\": %d,\n"
        "  \"hipSuccess\": %d,\n"
        "  \"hiprtcSuccess\": %d,\n"
        "  \"hiprtcCompilation\": %d,\n"
        "  \"hiprtcInternalError\": %d,\n"
        "  \"hiprtcLinkingError\": %d,\n"
        "  \"hipMemcpyHostToDevice\": %d,\n"
        "  \"hipMemcpyDeviceToHost\": %d,\n"
        "  \"hipMemcpyDeviceToDevice\": %d,\n"
        "  \"hipMemAdviseSetReadMostly\": %d,\n"
        "  \"hipMemAdviseSetCoarseGrain\": %d,\n"
        "  \"hipStreamCaptureModeGlobal\": %d,\n"
        "  \"hipStreamCaptureModeRelaxed\": %d,\n"
        "  \"hipErrorPeerAccessAlreadyEnabled\": %d,\n"
        "  \"hipErrorPeerAccessNotEnabled\": %d,\n"
        "  \"hipErrorNotSupported\": %d,\n"
        "  \"hipMemAttachGlobal\": %u,\n"
        "  \"hipMemAttachHost\": %u,\n"
        "  \"hipCpuDeviceId\": %d\n"
        "}\n",
        HIPSHARP_NORMALIZED_MANIFEST_SHA256,
        HIPSHARP_HEADER_SHA256,
        sizeof(hipError_t),
        alignof(hipError_t),
        sizeof(hipMemcpyKind),
        alignof(hipMemcpyKind),
        sizeof(hipMemoryAdvise),
        alignof(hipMemoryAdvise),
        sizeof(hipStreamCaptureMode),
        alignof(hipStreamCaptureMode),
        sizeof(void*),
        alignof(void*),
        sizeof(hiprtcResult),
        alignof(hiprtcResult),
        sizeof(hiprtcProgram),
        sizeof(hipModule_t),
        sizeof(hipFunction_t),
        sizeof(hipGraph_t),
        sizeof(hipGraphExec_t),
        sizeof(std::size_t),
        alignof(std::size_t),
        sizeof(dim3),
        alignof(dim3),
        offsetof(dim3, x),
        offsetof(dim3, y),
        offsetof(dim3, z),
        sizeof(hipPos),
        alignof(hipPos),
        sizeof(hipPitchedPtr),
        alignof(hipPitchedPtr),
        sizeof(hipMemcpy3DParms),
        alignof(hipMemcpy3DParms),
        offsetof(hipMemcpy3DParms, srcArray),
        offsetof(hipMemcpy3DParms, srcPos),
        offsetof(hipMemcpy3DParms, srcPtr),
        offsetof(hipMemcpy3DParms, dstArray),
        offsetof(hipMemcpy3DParms, dstPos),
        offsetof(hipMemcpy3DParms, dstPtr),
        offsetof(hipMemcpy3DParms, extent),
        offsetof(hipMemcpy3DParms, kind),
        sizeof(hipMemLocation),
        alignof(hipMemLocation),
        sizeof(hipMemAccessDesc),
        alignof(hipMemAccessDesc),
        offsetof(hipMemAccessDesc, location),
        offsetof(hipMemAccessDesc, flags),
        sizeof(hipMemPoolProps),
        alignof(hipMemPoolProps),
        offsetof(hipMemPoolProps, allocType),
        offsetof(hipMemPoolProps, handleTypes),
        offsetof(hipMemPoolProps, location),
        offsetof(hipMemPoolProps, win32SecurityAttributes),
        offsetof(hipMemPoolProps, maxSize),
        offsetof(hipMemPoolProps, reserved),
        static_cast<int>(hipMemLocationTypeDevice),
        static_cast<int>(hipMemAccessFlagsProtNone),
        static_cast<int>(hipMemAccessFlagsProtReadWrite),
        static_cast<int>(hipMemAllocationTypePinned),
        static_cast<int>(hipMemHandleTypeNone),
        static_cast<int>(hipMemPoolReuseFollowEventDependencies),
        static_cast<int>(hipMemPoolReuseAllowOpportunistic),
        static_cast<int>(hipMemPoolReuseAllowInternalDependencies),
        static_cast<int>(hipMemPoolAttrReleaseThreshold),
        static_cast<int>(hipMemPoolAttrReservedMemCurrent),
        static_cast<int>(hipMemPoolAttrReservedMemHigh),
        static_cast<int>(hipMemPoolAttrUsedMemCurrent),
        static_cast<int>(hipMemPoolAttrUsedMemHigh),
        static_cast<int>(hipSuccess),
        static_cast<int>(HIPRTC_SUCCESS),
        static_cast<int>(HIPRTC_ERROR_COMPILATION),
        static_cast<int>(HIPRTC_ERROR_INTERNAL_ERROR),
        static_cast<int>(HIPRTC_ERROR_LINKING),
        static_cast<int>(hipMemcpyHostToDevice),
        static_cast<int>(hipMemcpyDeviceToHost),
        static_cast<int>(hipMemcpyDeviceToDevice),
        static_cast<int>(hipMemAdviseSetReadMostly),
        static_cast<int>(hipMemAdviseSetCoarseGrain),
        static_cast<int>(hipStreamCaptureModeGlobal),
        static_cast<int>(hipStreamCaptureModeRelaxed),
        static_cast<int>(hipErrorPeerAccessAlreadyEnabled),
        static_cast<int>(hipErrorPeerAccessNotEnabled),
        static_cast<int>(hipErrorNotSupported),
        static_cast<unsigned int>(hipMemAttachGlobal),
        static_cast<unsigned int>(hipMemAttachHost),
        static_cast<int>(hipCpuDeviceId));
    return 0;
}
