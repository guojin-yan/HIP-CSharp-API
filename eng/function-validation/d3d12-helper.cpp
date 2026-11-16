#if defined(_WIN32) && !defined(__HIP_DEVICE_COMPILE__)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <d3d12.h>
#include <dxgi1_6.h>
#include <wrl/client.h>
#include <new>
using Microsoft::WRL::ComPtr;
namespace {
struct Context {
  ComPtr<ID3D12Device> device;
  ComPtr<ID3D12Resource> shared,readback;
  ComPtr<ID3D12Fence> fence;
  ComPtr<ID3D12CommandQueue> queue;
  HANDLE resourceHandle=nullptr,fenceHandle=nullptr;
  bool copied=false;
  ~Context() { if(resourceHandle) CloseHandle(resourceHandle);if(fenceHandle) CloseHandle(fenceHandle); }
};
D3D12_RESOURCE_DESC buffer() {
  D3D12_RESOURCE_DESC desc{};desc.Dimension=D3D12_RESOURCE_DIMENSION_BUFFER;
  desc.Width=65536;desc.Height=1;desc.DepthOrArraySize=1;desc.MipLevels=1;
  desc.SampleDesc.Count=1;desc.Layout=D3D12_TEXTURE_LAYOUT_ROW_MAJOR;return desc;
}
HRESULT complete(Context* context,UINT64 value) {
  HANDLE event=CreateEventW(nullptr,FALSE,FALSE,nullptr);if(!event) return HRESULT_FROM_WIN32(GetLastError());
  HRESULT result=context->fence->SetEventOnCompletion(value,event);
  if(SUCCEEDED(result) && WaitForSingleObject(event,10000)!=WAIT_OBJECT_0) result=HRESULT_FROM_WIN32(WAIT_TIMEOUT);
  CloseHandle(event);return result;
}
#define TRY(call) do { HRESULT result=(call);if(FAILED(result)) return result; } while(0)
HRESULT __cdecl create(void** output) {
  if(!output) return E_POINTER;*output=nullptr;
  ComPtr<IDXGIFactory4> factory;TRY(CreateDXGIFactory1(IID_PPV_ARGS(&factory)));
  ComPtr<IDXGIAdapter1> adapter;
  for(UINT index=0;;index++) {
    ComPtr<IDXGIAdapter1> candidate;HRESULT result=factory->EnumAdapters1(index,&candidate);
    if(result==DXGI_ERROR_NOT_FOUND) break;if(FAILED(result)) return result;
    DXGI_ADAPTER_DESC1 desc{};TRY(candidate->GetDesc1(&desc));
    if(desc.VendorId==0x1002 && !(desc.Flags&DXGI_ADAPTER_FLAG_SOFTWARE)) {adapter=candidate;break;}
  }
  if(!adapter) return DXGI_ERROR_NOT_FOUND;
  Context* allocated=new(std::nothrow) Context;if(!allocated) return E_OUTOFMEMORY;
  struct Guard {Context* pointer;~Guard(){delete pointer;}} guard{allocated};
  TRY(D3D12CreateDevice(adapter.Get(),D3D_FEATURE_LEVEL_11_0,IID_PPV_ARGS(&allocated->device)));
  auto desc=buffer();D3D12_HEAP_PROPERTIES heap{};heap.Type=D3D12_HEAP_TYPE_DEFAULT;
  TRY(allocated->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_SHARED,&desc,D3D12_RESOURCE_STATE_COMMON,nullptr,IID_PPV_ARGS(&allocated->shared)));
  TRY(allocated->device->CreateSharedHandle(allocated->shared.Get(),nullptr,GENERIC_ALL,nullptr,&allocated->resourceHandle));
  TRY(allocated->device->CreateFence(0,D3D12_FENCE_FLAG_SHARED,IID_PPV_ARGS(&allocated->fence)));
  TRY(allocated->device->CreateSharedHandle(allocated->fence.Get(),nullptr,GENERIC_ALL,nullptr,&allocated->fenceHandle));
  D3D12_COMMAND_QUEUE_DESC queue{};TRY(allocated->device->CreateCommandQueue(&queue,IID_PPV_ARGS(&allocated->queue)));
  heap.Type=D3D12_HEAP_TYPE_READBACK;
  TRY(allocated->device->CreateCommittedResource(&heap,D3D12_HEAP_FLAG_NONE,&desc,D3D12_RESOURCE_STATE_COPY_DEST,nullptr,IID_PPV_ARGS(&allocated->readback)));
  *output=allocated;guard.pointer=nullptr;return S_OK;
}
void __cdecl destroy(Context* context) {delete context;}
HRESULT __cdecl handles(Context* context,void** memory,void** fence) {
  if(!context || !memory || !fence) return E_POINTER;
  *memory=context->resourceHandle;*fence=context->fenceHandle;return S_OK;
}
UINT64 __cdecl value(Context* context) {return context->fence->GetCompletedValue();}
HRESULT __cdecl signal(Context* context,UINT64 value) {
  TRY(context->queue->Signal(context->fence.Get(),value));return complete(context,value);
}
HRESULT __cdecl observe(Context* context,UINT64 expectedFence,UINT64 nextFence,unsigned char expectedByte) {
  if(context->copied || context->fence->GetCompletedValue()<expectedFence) return E_FAIL;
  ComPtr<ID3D12CommandAllocator> allocator;ComPtr<ID3D12GraphicsCommandList> commands;
  TRY(context->device->CreateCommandAllocator(D3D12_COMMAND_LIST_TYPE_DIRECT,IID_PPV_ARGS(&allocator)));
  TRY(context->device->CreateCommandList(0,D3D12_COMMAND_LIST_TYPE_DIRECT,allocator.Get(),nullptr,IID_PPV_ARGS(&commands)));
  D3D12_RESOURCE_BARRIER barrier{};barrier.Type=D3D12_RESOURCE_BARRIER_TYPE_TRANSITION;
  barrier.Transition.pResource=context->shared.Get();barrier.Transition.StateBefore=D3D12_RESOURCE_STATE_COMMON;
  barrier.Transition.StateAfter=D3D12_RESOURCE_STATE_COPY_SOURCE;barrier.Transition.Subresource=D3D12_RESOURCE_BARRIER_ALL_SUBRESOURCES;
  commands->ResourceBarrier(1,&barrier);commands->CopyBufferRegion(context->readback.Get(),0,context->shared.Get(),0,4096);
  TRY(commands->Close());ID3D12CommandList* lists[]={commands.Get()};context->queue->ExecuteCommandLists(1,lists);
  TRY(signal(context,nextFence));D3D12_RANGE range{0,4096};void* mapped=nullptr;
  TRY(context->readback->Map(0,&range,&mapped));bool matched=true;
  for(size_t i=0;i<4096;i++) if(static_cast<unsigned char*>(mapped)[i]!=expectedByte) matched=false;
  D3D12_RANGE written{0,0};context->readback->Unmap(0,&written);context->copied=true;return matched?S_OK:E_FAIL;
}
const void* functions[]={reinterpret_cast<void*>(&create),reinterpret_cast<void*>(&destroy),
  reinterpret_cast<void*>(&handles),reinterpret_cast<void*>(&value),reinterpret_cast<void*>(&signal),reinterpret_cast<void*>(&observe)};
}
extern "C" __declspec(dllexport) const void* D3D12Functions() { return functions; }
#endif
