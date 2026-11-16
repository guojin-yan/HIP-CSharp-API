#include <hip/hip_runtime.h>
#include <hip/hip_deprecated.h>
#include <cstddef>
// These test-only C# descriptors are intentionally checked against the installed compiler headers.
static_assert(sizeof(hipStreamBatchMemOpParams) == 48);
static_assert(offsetof(hipStreamBatchMemOpParams, writeValue.address) == 8);
static_assert(offsetof(hipStreamBatchMemOpParams, writeValue.value64) == 16);
static_assert(offsetof(hipStreamBatchMemOpParams, writeValue.flags) == 24);
static_assert(sizeof(hipBatchMemOpNodeParams) == 32);
static_assert(offsetof(hipBatchMemOpNodeParams, paramArray) == 16);
static_assert(sizeof(hipMemcpy3DOperand) == 40);
static_assert(sizeof(hipMemcpy3DBatchOp) == 112);
static_assert(sizeof(hipDeviceProp_tR0000) == 792);
static_assert(offsetof(hipDeviceProp_tR0000, totalGlobalMem) == 256);
static_assert(offsetof(hipDeviceProp_tR0000, major) == 328);
static_assert(offsetof(hipDeviceProp_tR0000, minor) == 332);
static_assert(offsetof(hipDeviceProp_tR0000, multiProcessorCount) == 336);
static_assert(sizeof(hipMemcpy3DPeerParms) == 168);
static_assert(offsetof(hipMemcpy3DPeerParms, srcDevice) == 64);
static_assert(offsetof(hipMemcpy3DPeerParms, dstArray) == 72);
static_assert(offsetof(hipMemcpy3DPeerParms, dstDevice) == 136);
static_assert(offsetof(hipMemcpy3DPeerParms, extent) == 144);
static_assert(sizeof(hipExternalMemoryHandleDesc) == 104);
static_assert(offsetof(hipExternalMemoryHandleDesc, handle) == 8);
static_assert(offsetof(hipExternalMemoryHandleDesc, size) == 24);
static_assert(offsetof(hipExternalMemoryHandleDesc, flags) == 32);
static_assert(sizeof(hipExternalMemoryBufferDesc) == 88);
static_assert(sizeof(hipExternalSemaphoreHandleDesc) == 96);
static_assert(sizeof(hipExternalSemaphoreSignalParams) == 144);
static_assert(sizeof(hipExternalSemaphoreWaitParams) == 144);
static_assert(sizeof(hipExternalSemaphoreSignalNodeParams) == 24);
static_assert(sizeof(hipExternalSemaphoreWaitNodeParams) == 24);
#if defined(_WIN32)
#define EXPORTED __declspec(dllexport)
#else
#define EXPORTED __attribute__((visibility("default")))
#endif
extern "C" __global__ void HostIncrement(int* data) { *data += 5; }
__device__ int ValidationGlobal = 91;
texture<unsigned char, hipTextureType2D, hipReadModeElementType> ValidationTexture;
extern "C" __global__ void SampleRegisteredTexture(int* output) { *output = tex2D(ValidationTexture, 0.5f, 0.5f); }
#if !defined(__HIP_DEVICE_COMPILE__)
extern "C" EXPORTED const void* ValidationKernel() { return reinterpret_cast<const void*>(HostIncrement); }
extern "C" EXPORTED const void* ValidationSymbol() { return &ValidationGlobal; }
extern "C" EXPORTED const textureReference* RegisteredTexture() { return &ValidationTexture; }
extern "C" EXPORTED const void* RegisteredTextureKernel() { return reinterpret_cast<const void*>(SampleRegisteredTexture); }
#endif
