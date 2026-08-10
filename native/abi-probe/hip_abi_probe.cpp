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
static_assert(std::is_same<decltype(&hipMalloc), hipError_t (*)(void**, std::size_t)>::value, "hipMalloc signature mismatch");
static_assert(std::is_same<decltype(&hipFree), hipError_t (*)(void*)>::value, "hipFree signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy), hipError_t (*)(void*, const void*, std::size_t, hipMemcpyKind)>::value, "hipMemcpy signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpyAsync), hipError_t (*)(void*, const void*, std::size_t, hipMemcpyKind, hipStream_t)>::value, "hipMemcpyAsync signature mismatch");
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
        "  \"schemaVersion\": 2,\n"
        "  \"normalizedManifestHash\": \"%s\",\n"
        "  \"headerHash\": \"%s\",\n"
        "  \"staticAssertions\": true,\n"
        "  \"hipErrorSize\": %zu,\n"
        "  \"hipErrorAlignment\": %zu,\n"
        "  \"hipMemcpyKindSize\": %zu,\n"
        "  \"hipMemcpyKindAlignment\": %zu,\n"
        "  \"pointerSize\": %zu,\n"
        "  \"pointerAlignment\": %zu,\n"
        "  \"hiprtcResultSize\": %zu,\n"
        "  \"hiprtcResultAlignment\": %zu,\n"
        "  \"hiprtcProgramSize\": %zu,\n"
        "  \"hipModuleHandleSize\": %zu,\n"
        "  \"hipFunctionHandleSize\": %zu,\n"
        "  \"sizeTSize\": %zu,\n"
        "  \"sizeTAlignment\": %zu,\n"
        "  \"dim3Size\": %zu,\n"
        "  \"dim3Alignment\": %zu,\n"
        "  \"dim3OffsetX\": %zu,\n"
        "  \"dim3OffsetY\": %zu,\n"
        "  \"dim3OffsetZ\": %zu,\n"
        "  \"hipSuccess\": %d,\n"
        "  \"hiprtcSuccess\": %d,\n"
        "  \"hiprtcCompilation\": %d,\n"
        "  \"hiprtcInternalError\": %d,\n"
        "  \"hiprtcLinkingError\": %d,\n"
        "  \"hipMemcpyHostToDevice\": %d,\n"
        "  \"hipMemcpyDeviceToHost\": %d,\n"
        "  \"hipMemcpyDeviceToDevice\": %d\n"
        "}\n",
        HIPSHARP_NORMALIZED_MANIFEST_SHA256,
        HIPSHARP_HEADER_SHA256,
        sizeof(hipError_t),
        alignof(hipError_t),
        sizeof(hipMemcpyKind),
        alignof(hipMemcpyKind),
        sizeof(void*),
        alignof(void*),
        sizeof(hiprtcResult),
        alignof(hiprtcResult),
        sizeof(hiprtcProgram),
        sizeof(hipModule_t),
        sizeof(hipFunction_t),
        sizeof(std::size_t),
        alignof(std::size_t),
        sizeof(dim3),
        alignof(dim3),
        offsetof(dim3, x),
        offsetof(dim3, y),
        offsetof(dim3, z),
        static_cast<int>(hipSuccess),
        static_cast<int>(HIPRTC_SUCCESS),
        static_cast<int>(HIPRTC_ERROR_COMPILATION),
        static_cast<int>(HIPRTC_ERROR_INTERNAL_ERROR),
        static_cast<int>(HIPRTC_ERROR_LINKING),
        static_cast<int>(hipMemcpyHostToDevice),
        static_cast<int>(hipMemcpyDeviceToHost),
        static_cast<int>(hipMemcpyDeviceToDevice));
    return 0;
}
