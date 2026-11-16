#if defined(_WIN32) && !defined(__HIP_DEVICE_COMPILE__)
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <GL/gl.h>
#include <hip/hip_runtime_api.h>
#include <hip/hip_gl_interop.h>
#include <vector>
#include <new>
#include <cstdio>
namespace {
using DeleteBuffers=void(APIENTRY*)(GLsizei,const GLuint*);
struct Context {
  HWND window=nullptr;HDC dc=nullptr;HGLRC context=nullptr;GLuint object=0;
  bool image=false;DeleteBuffers deleteBuffers=nullptr;
  ~Context() {
    if(context) {
      wglMakeCurrent(dc,context);
      if(object) {if(image) glDeleteTextures(1,&object);else if(deleteBuffers) deleteBuffers(1,&object);}
      wglMakeCurrent(nullptr,nullptr);wglDeleteContext(context);
    }
    if(dc) ReleaseDC(window,dc);if(window) DestroyWindow(window);
  }
};
template<class T>T extension(const char* name){return reinterpret_cast<T>(wglGetProcAddress(name));}
int __cdecl create(int image,void** output,void** resource) {
  if(!output||!resource) return -1;*output=nullptr;*resource=nullptr;
  Context* context=new(std::nothrow) Context;if(!context)return -2;
  struct Guard{Context* pointer;~Guard(){delete pointer;}}guard{context};
  WNDCLASSW klass{};klass.style=CS_OWNDC;klass.lpfnWndProc=DefWindowProcW;klass.hInstance=GetModuleHandleW(nullptr);klass.lpszClassName=L"HipSharpHiddenGLHelper";
  if(!RegisterClassW(&klass)&&GetLastError()!=ERROR_CLASS_ALREADY_EXISTS)return -3;
  context->window=CreateWindowW(klass.lpszClassName,L"HIP validation",WS_POPUP,0,0,64,64,nullptr,nullptr,klass.hInstance,nullptr);
  if(!context->window)return -4;context->dc=GetDC(context->window);
  PIXELFORMATDESCRIPTOR pixel{};pixel.nSize=sizeof(pixel);pixel.nVersion=1;
  pixel.dwFlags=PFD_DRAW_TO_WINDOW|PFD_SUPPORT_OPENGL|PFD_DOUBLEBUFFER;pixel.iPixelType=PFD_TYPE_RGBA;pixel.cColorBits=32;
  int format=ChoosePixelFormat(context->dc,&pixel);
  if(!format||!SetPixelFormat(context->dc,format,&pixel))return -5;
  context->context=wglCreateContext(context->dc);
  if(!context->context||!wglMakeCurrent(context->dc,context->context))return -6;
  printf("OPENGL_RENDERER=%s\n",glGetString(GL_RENDERER));fflush(stdout);
  HMODULE hip=GetModuleHandleW(L"amdhip64_7.dll");if(!hip)return -7;
  auto devices=reinterpret_cast<decltype(&hipGLGetDevices)>(GetProcAddress(hip,"hipGLGetDevices"));
  auto registerBuffer=reinterpret_cast<decltype(&hipGraphicsGLRegisterBuffer)>(GetProcAddress(hip,"hipGraphicsGLRegisterBuffer"));
  auto registerImage=reinterpret_cast<decltype(&hipGraphicsGLRegisterImage)>(GetProcAddress(hip,"hipGraphicsGLRegisterImage"));
  if(!devices||!registerBuffer||!registerImage)return -8;
  unsigned count=0;int device[4]={-1,-1,-1,-1};hipError_t error=devices(&count,device,4,hipGLDeviceListAll);
  printf("OPENGL_REGISTRATION=hipGLGetDevices:%d\n",int(error));fflush(stdout);
  if(error!=hipSuccess||count!=1||device[0]!=0)return -9;
  std::vector<unsigned char> bytes(image?1024:4096,0xCD);context->image=image!=0;
  if(image) {
    glGenTextures(1,&context->object);glBindTexture(GL_TEXTURE_2D,context->object);
    glTexImage2D(GL_TEXTURE_2D,0,GL_RGBA8,64,4,0,GL_RGBA,GL_UNSIGNED_BYTE,bytes.data());glFinish();
    if(glGetError()!=GL_NO_ERROR)return -10;
    error=registerImage(reinterpret_cast<hipGraphicsResource_t*>(resource),context->object,GL_TEXTURE_2D,hipGraphicsRegisterFlagsNone);
    printf("OPENGL_REGISTRATION=hipGraphicsGLRegisterImage:%d\n",int(error));
  } else {
    auto gen=extension<void(APIENTRY*)(GLsizei,GLuint*)>("glGenBuffers");
    auto bind=extension<void(APIENTRY*)(GLenum,GLuint)>("glBindBuffer");
    auto data=extension<void(APIENTRY*)(GLenum,ptrdiff_t,const void*,GLenum)>("glBufferData");
    context->deleteBuffers=extension<DeleteBuffers>("glDeleteBuffers");
    if(!gen||!bind||!data||!context->deleteBuffers)return -11;
    gen(1,&context->object);bind(0x8892,context->object);data(0x8892,bytes.size(),bytes.data(),0x88E4);glFinish();
    if(glGetError()!=GL_NO_ERROR)return -12;
    error=registerBuffer(reinterpret_cast<hipGraphicsResource_t*>(resource),context->object,hipGraphicsRegisterFlagsNone);
    printf("OPENGL_REGISTRATION=hipGraphicsGLRegisterBuffer:%d\n",int(error));
  }
  fflush(stdout);if(error!=hipSuccess)return int(error);
  *output=context;guard.pointer=nullptr;return 0;
}
void __cdecl destroy(Context* context){delete context;}
int __cdecl read(Context* context,unsigned char expected) {
  if(!context)return -1;std::vector<unsigned char> bytes(context->image?1024:4096,0);
  if(context->image){glBindTexture(GL_TEXTURE_2D,context->object);glGetTexImage(GL_TEXTURE_2D,0,GL_RGBA,GL_UNSIGNED_BYTE,bytes.data());}
  else {
    auto bind=extension<void(APIENTRY*)(GLenum,GLuint)>("glBindBuffer");
    auto get=extension<void(APIENTRY*)(GLenum,ptrdiff_t,ptrdiff_t,void*)>("glGetBufferSubData");
    if(!bind||!get)return -2;bind(0x8892,context->object);get(0x8892,0,bytes.size(),bytes.data());
  }
  glFinish();if(glGetError()!=GL_NO_ERROR)return -3;
  for(auto value:bytes)if(value!=expected)return -4;return 0;
}
const void* functions[]={reinterpret_cast<void*>(&create),reinterpret_cast<void*>(&destroy),reinterpret_cast<void*>(&read)};
}
extern "C" __declspec(dllexport) const void* OpenGLFunctions(){return functions;}
#endif
