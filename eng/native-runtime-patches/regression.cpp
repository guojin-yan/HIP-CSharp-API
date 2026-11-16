// SPDX-License-Identifier: MIT
// Compile the actual pinned upstream entry-point bodies, before and after patching.
// Adapters below deliberately avoid claiming a full CLR/PAL/HSA Runtime rebuild:
// * the byte-oriented memset common function calls the installed typed entry point
//   using its observed byte-width convention (only valid for this pinned Runtime);
// * LinkProgram forwards to the installed HIPRTC linker, which shares the upstream
//   LinkProgram backend but does not contain the driver's incorrect input guard.
#include <hip/hip_runtime.h>
#include <hip/hiprtc.h>
#include <cstdio>
#include <cstdlib>
#include <fstream>
#include <limits>
#include <string>
#include <vector>
#ifdef _WIN32
#define NOMINMAX
#include <windows.h>
#else
#include <dlfcn.h>
#endif

void module(const void* address) {
#ifdef _WIN32
  HMODULE handle = nullptr; char path[32768] = {};
  if (!GetModuleHandleExA(GET_MODULE_HANDLE_EX_FLAG_FROM_ADDRESS | GET_MODULE_HANDLE_EX_FLAG_UNCHANGED_REFCOUNT,
                         reinterpret_cast<LPCSTR>(address), &handle) ||
      !GetModuleFileNameA(handle, path, sizeof(path))) std::exit(2);
  std::printf("MODULE %s\n", path);
#else
  Dl_info info = {};
  if (!dladdr(address, &info) || !info.dli_fname) std::exit(2);
  std::printf("MODULE %s\n", info.dli_fname);
#endif
}

void require(bool condition, const char* message) {
  if (!condition) { std::fprintf(stderr, "FAIL %s\n", message); std::exit(1); }
}
void check(hipError_t result) { require(result == hipSuccess, hipGetErrorString(result)); }
void rtc(hiprtcResult result) { require(result == HIPRTC_SUCCESS, hiprtcGetErrorString(result)); }
void pass(const char* label) { std::printf("PASS %s\n", label); }

hipError_t hipMemset2D_common(void* dst, size_t pitch, int value, size_t bytes, size_t height,
                             hipStream_t stream, size_t element) {
  return element == 2 ? ::hipMemsetD2D16(dst, pitch, value, bytes, height)
                      : ::hipMemsetD2D32(dst, pitch, value, bytes, height);
}
hipError_t hipMemset2DAsync_common(void* dst, size_t pitch, int value, size_t bytes, size_t height,
                                  hipStream_t stream, size_t element) {
  return element == 2 ? ::hipMemsetD2D16Async(dst, pitch, value, bytes, height, stream)
                      : ::hipMemsetD2D32Async(dst, pitch, value, bytes, height, stream);
}
class LinkProgram;
LinkProgram* validLink = nullptr;
class LinkProgram {
 public:
  hiprtcLinkState state = nullptr;
  LinkProgram() { rtc(hiprtcLinkCreate(0, nullptr, nullptr, &state)); validLink = this; }
  ~LinkProgram() { rtc(hiprtcLinkDestroy(state)); validLink = nullptr; }
  static bool isLinkerValid(LinkProgram* p) { return p != nullptr && p == validLink; }
  bool AddLinkerData(void* data, size_t size, std::string name, hipJitInputType type) {
    return hiprtcLinkAddData(state, type, data, size, name.c_str(), 0, nullptr, nullptr) == HIPRTC_SUCCESS;
  }
  bool AddLinkerFile(std::string name, hipJitInputType type) {
    return hiprtcLinkAddFile(state, type, name.c_str(), 0, nullptr, nullptr) == HIPRTC_SUCCESS;
  }
};
#define HIP_INIT_API(...) ((void)0)
#define HIP_RETURN(value) return (value)
#include "fragments.inc"
#include "dispatch.inc"

template<class T> void fills(hipStream_t stream, bool contiguous) {
  constexpr size_t rows = 4, width = sizeof(T) == 2 ? 64 : 32;
  const size_t pitch = contiguous ? width * sizeof(T) : 256;
  void* memory = nullptr; check(hipMalloc(&memory, rows * pitch));
  for (bool async : {false, true}) {
    check(hipMemset(memory, 0xA5, rows * pitch));
    check(hipDeviceSynchronize());
    if (sizeof(T) == 2) check(async ? baseline::hipMemsetD2D16Async(memory, pitch, 0x1234, width, rows, stream)
                                   : baseline::hipMemsetD2D16(memory, pitch, 0x1234, width, rows));
    else check(async ? baseline::hipMemsetD2D32Async(memory, pitch, 0x12345678, width, rows, stream)
                      : baseline::hipMemsetD2D32(memory, pitch, 0x12345678, width, rows));
    check(hipDeviceSynchronize());
    std::vector<unsigned char> host(rows * pitch);
    check(hipMemcpy(host.data(), memory, host.size(), hipMemcpyDeviceToHost));
    if (host[width * sizeof(T) - 1] != 0xA5) std::fprintf(stderr, "baseline element=%zu async=%d last intended byte: %02x\n", sizeof(T), async, host[width * sizeof(T) - 1]);
    require(host[width * sizeof(T) - 1] == 0xA5, "baseline reproduces incomplete typed width");
    check(hipMemset(memory, 0xA5, rows * pitch));
    check(hipDeviceSynchronize());
    if (sizeof(T) == 2) check(async ? patched::hipMemsetD2D16Async(memory, pitch, 0x1234, width, rows, stream)
                                   : patched::hipMemsetD2D16(memory, pitch, 0x1234, width, rows));
    else check(async ? patched::hipMemsetD2D32Async(memory, pitch, 0x12345678, width, rows, stream)
                      : patched::hipMemsetD2D32(memory, pitch, 0x12345678, width, rows));
    check(hipDeviceSynchronize());
    check(hipMemcpy(host.data(), memory, host.size(), hipMemcpyDeviceToHost));
    for (size_t row = 0; row < rows; row++) {
      for (size_t col = 0; col < pitch; col++) {
        unsigned value = sizeof(T) == 2 ? 0x1234 : 0x12345678;
        unsigned char expected = col < width * sizeof(T) ? (value >> (8 * (col % sizeof(T)))) & 255 : 0xA5;
        require(host[row * pitch + col] == expected, "every fill byte and row padding");
      }
    }
    std::string label = sizeof(T) == 2 ? (async ? "D2D16Async" : "D2D16") : (async ? "D2D32Async" : "D2D32");
    pass((label + (contiguous ? " GPU contiguous fill" : " GPU fill and padding")).c_str());
  }
  const auto huge = std::numeric_limits<size_t>::max();
  if (sizeof(T) == 2) {
    require(patched::hipMemsetD2D16(memory, pitch, 0, huge, 1) == hipErrorInvalidValue, "D16 overflow");
    require(patched::hipMemsetD2D16Async(memory, pitch, 0, huge, 1, stream) == hipErrorInvalidValue, "D16 async overflow");
  } else {
    require(patched::hipMemsetD2D32(memory, pitch, 0, huge, 1) == hipErrorInvalidValue, "D32 overflow");
    require(patched::hipMemsetD2D32Async(memory, pitch, 0, huge, 1, stream) == hipErrorInvalidValue, "D32 async overflow");
  }
  if (!contiguous) pass(sizeof(T) == 2 ? "D2D16 overflow rejected sync and async" : "D2D32 overflow rejected sync and async");
  check(hipFree(memory));
}

int main() {
#ifdef _WIN32
  // Taking &hipInit with these headers yields the executable's import thunk.
  // Resolve the loaded DLL export so the audit identifies the actual module.
  module(reinterpret_cast<const void*>(GetProcAddress(GetModuleHandleA("amdhip64_7.dll"), "hipInit")));
  module(reinterpret_cast<const void*>(GetProcAddress(GetModuleHandleA("hiprtc0716.dll"), "hiprtcLinkCreate")));
#else
  module(reinterpret_cast<const void*>(&::hipInit));
  module(reinterpret_cast<const void*>(&::hiprtcLinkCreate));
#endif
  check(hipInit(0));
  dispatch_regression();
  hipStream_t stream = nullptr; check(hipStreamCreate(&stream));
  for (bool contiguous : {false, true}) { fills<unsigned short>(stream, contiguous); fills<unsigned int>(stream, contiguous); }
  hiprtcProgram program = nullptr;
  rtc(hiprtcCreateProgram(&program, "extern \"C\" __global__ void Increment(int* p) { *p += 1; }", "increment.hip", 0, nullptr, nullptr));
  const char* options[] = {"--gpu-architecture=gfx1100", "-fgpu-rdc"};
  rtc(hiprtcCompileProgram(program, 2, options));
  size_t length = 0; rtc(hiprtcGetBitcodeSize(program, &length));
  std::vector<char> bitcode(length); rtc(hiprtcGetBitcode(program, bitcode.data()));
  rtc(hiprtcDestroyProgram(&program));
  std::ofstream file("input.bc", std::ios::binary); file.write(bitcode.data(), bitcode.size()); file.close();
  for (bool fromFile : {false, true}) {
    LinkProgram linker;
    auto state = reinterpret_cast<hipLinkState_t>(&linker);
    auto before = fromFile ? baseline::hipLinkAddFile(state, hipJitInputLLVMBitcode, "input.bc", 0, nullptr, nullptr)
                           : baseline::hipLinkAddData(state, hipJitInputLLVMBitcode, bitcode.data(), bitcode.size(), "input.bc", 0, nullptr, nullptr);
    require(before == hipErrorInvalidValue, "baseline rejects valid bitcode");
    // Reject CUDA, SPIR-V, negative and unknown enum values before entering the backend.
    for (int type : {-1, 0, 1, 2, 3, 4, 5, 6, 99, 103, 0x7fffffff}) {
      auto error = fromFile ? patched::hipLinkAddFile(state, static_cast<hipJitInputType>(type), "input.bc", 0, nullptr, nullptr)
                            : patched::hipLinkAddData(state, static_cast<hipJitInputType>(type), bitcode.data(), bitcode.size(), "input.bc", 0, nullptr, nullptr);
      require(error == hipErrorInvalidValue, "unsupported linker input rejected");
    }
    auto after = fromFile ? patched::hipLinkAddFile(state, hipJitInputLLVMBitcode, "input.bc", 0, nullptr, nullptr)
                          : patched::hipLinkAddData(state, hipJitInputLLVMBitcode, bitcode.data(), bitcode.size(), "input.bc", 0, nullptr, nullptr);
    check(after);
    void* binary = nullptr; size_t bytes = 0; rtc(hiprtcLinkComplete(linker.state, &binary, &bytes));
    require(binary != nullptr && bytes > 16, "linked image");
    hipModule_t module = nullptr; check(hipModuleLoadData(&module, binary));
    hipFunction_t kernel = nullptr; check(hipModuleGetFunction(&kernel, module, "Increment"));
    void* pointer = nullptr; check(hipMalloc(&pointer, sizeof(int))); int value = 41;
    check(hipMemcpy(pointer, &value, sizeof(value), hipMemcpyHostToDevice));
    void* arguments[] = {&pointer};
    check(hipModuleLaunchKernel(kernel, 1, 1, 1, 1, 1, 1, 0, stream, arguments, nullptr));
    check(hipStreamSynchronize(stream));
    check(hipMemcpy(&value, pointer, sizeof(value), hipMemcpyDeviceToHost));
    require(value == 42, "linked kernel independently executed 41 to 42");
    check(hipFree(pointer)); check(hipModuleUnload(module));
    pass(fromFile ? "driver file guard plus real HIPRTC link GPU 41 to 42" : "driver data guard plus real HIPRTC link GPU 41 to 42");
  }
  require(patched::hipLinkAddFile(nullptr, hipJitInputLLVMBitcode, "input.bc", 0, nullptr, nullptr) == hipErrorInvalidHandle, "null state");
  { LinkProgram linker;
    auto state = reinterpret_cast<hipLinkState_t>(&linker);
    require(patched::hipLinkAddFile(state, hipJitInputLLVMBitcode, nullptr, 0, nullptr, nullptr) == hipErrorInvalidValue, "null file");
    require(patched::hipLinkAddData(state, hipJitInputLLVMBitcode, nullptr, 0, nullptr, 0, nullptr, nullptr) == hipErrorInvalidImage, "null image");
  }
  pass("linker null inputs rejected");
  check(hipStreamDestroy(stream));
  return 0;
}
