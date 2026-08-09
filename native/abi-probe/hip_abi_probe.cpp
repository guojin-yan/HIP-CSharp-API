#define __HIP_DISABLE_CPP_FUNCTIONS__
#include <hip/hip_runtime_api.h>

#include <cstddef>
#include <cstdio>
#include <type_traits>

static_assert(std::is_same<decltype(&hipInit), hipError_t (*)(unsigned int)>::value, "hipInit signature mismatch");
static_assert(std::is_same<decltype(&hipRuntimeGetVersion), hipError_t (*)(int*)>::value, "hipRuntimeGetVersion signature mismatch");
static_assert(std::is_same<decltype(&hipDriverGetVersion), hipError_t (*)(int*)>::value, "hipDriverGetVersion signature mismatch");
static_assert(std::is_same<decltype(&hipGetDeviceCount), hipError_t (*)(int*)>::value, "hipGetDeviceCount signature mismatch");
static_assert(std::is_same<decltype(&hipGetDevice), hipError_t (*)(int*)>::value, "hipGetDevice signature mismatch");
static_assert(std::is_same<decltype(&hipSetDevice), hipError_t (*)(int)>::value, "hipSetDevice signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceGetName), hipError_t (*)(char*, int, int)>::value, "hipDeviceGetName signature mismatch");
static_assert(std::is_same<decltype(&hipMalloc), hipError_t (*)(void**, std::size_t)>::value, "hipMalloc signature mismatch");
static_assert(std::is_same<decltype(&hipFree), hipError_t (*)(void*)>::value, "hipFree signature mismatch");
static_assert(std::is_same<decltype(&hipMemcpy), hipError_t (*)(void*, const void*, std::size_t, hipMemcpyKind)>::value, "hipMemcpy signature mismatch");
static_assert(std::is_same<decltype(&hipDeviceSynchronize), hipError_t (*)()>::value, "hipDeviceSynchronize signature mismatch");
static_assert(std::is_same<decltype(&hipGetErrorName), const char* (*)(hipError_t)>::value, "hipGetErrorName signature mismatch");
static_assert(std::is_same<decltype(&hipGetErrorString), const char* (*)(hipError_t)>::value, "hipGetErrorString signature mismatch");

int main()
{
    std::printf(
        "{\n"
        "  \"hipErrorSize\": %zu,\n"
        "  \"hipErrorAlignment\": %zu,\n"
        "  \"hipMemcpyKindSize\": %zu,\n"
        "  \"hipMemcpyKindAlignment\": %zu,\n"
        "  \"pointerSize\": %zu,\n"
        "  \"pointerAlignment\": %zu,\n"
        "  \"hipSuccess\": %d,\n"
        "  \"hipMemcpyHostToDevice\": %d,\n"
        "  \"hipMemcpyDeviceToHost\": %d,\n"
        "  \"hipMemcpyDeviceToDevice\": %d\n"
        "}\n",
        sizeof(hipError_t),
        alignof(hipError_t),
        sizeof(hipMemcpyKind),
        alignof(hipMemcpyKind),
        sizeof(void*),
        alignof(void*),
        static_cast<int>(hipSuccess),
        static_cast<int>(hipMemcpyHostToDevice),
        static_cast<int>(hipMemcpyDeviceToHost),
        static_cast<int>(hipMemcpyDeviceToDevice));
    return 0;
}
