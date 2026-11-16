#include <hip/hip_runtime.h>
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
#if defined(_WIN32)
#define EXPORTED __declspec(dllexport)
#else
#define EXPORTED __attribute__((visibility("default")))
#endif
extern "C" __global__ void HostIncrement(int* data) { *data += 5; }
__device__ int ValidationGlobal = 91;
#if !defined(__HIP_DEVICE_COMPILE__)
extern "C" EXPORTED const void* ValidationKernel() { return reinterpret_cast<const void*>(HostIncrement); }
extern "C" EXPORTED const void* ValidationSymbol() { return &ValidationGlobal; }
#endif
