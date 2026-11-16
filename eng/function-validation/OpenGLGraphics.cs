using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GLCreate(int image, out IntPtr context, out IntPtr resource);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate void GLDestroy(IntPtr context);
    [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GLRead(IntPtr context, byte expected);
    private sealed class GLContext : IDisposable
    {
        internal IntPtr Context, Resource;
        internal readonly GLRead Read;
        private readonly GLDestroy _destroy;
        internal GLContext(bool image)
        {
            Require(OperatingSystem.IsWindows(), "Windows WGL resource producer required.");
            IntPtr functions = LoadHelperPointer("OpenGLFunctions");
            GLCreate create = Marshal.GetDelegateForFunctionPointer<GLCreate>(Marshal.ReadIntPtr(functions));
            _destroy = Marshal.GetDelegateForFunctionPointer<GLDestroy>(Marshal.ReadIntPtr(functions, IntPtr.Size));
            Read = Marshal.GetDelegateForFunctionPointer<GLRead>(Marshal.ReadIntPtr(functions, 2 * IntPtr.Size));
            int result = create(image ? 1 : 0, out Context, out Resource);
            Require(result == 0 && Context != 0 && Resource != 0, $"Create/register actual OpenGL resource: {result}.");
        }
        // HIP owns registration cleanup; destroy the GL storage/context only after unregistration.
        public void Dispose() { if (Context != 0) { _destroy(Context); Context = 0; } }
    }
    private static void OpenGLGraphics(bool image)
    {
        using var gl = new GLContext(image); IntPtr resource = gl.Resource; bool mapped = false;
        try
        {
            Check(Raw.GraphicsMapResources(1, (IntPtr)(&resource), 0)); mapped = true;
            if (image)
            {
                IntPtr array = 0;
                Check(Raw.GraphicsSubResourceGetMappedArray((IntPtr)(&array), resource, 0, 0));
                Require(array != 0, "Actual mapped GL image array.");
                HipChannelFormatDescriptor descriptor = default;
                Check(Raw.GetChannelDesc((IntPtr)(&descriptor), array));
                Require(descriptor.XBits == 8 && descriptor.YBits == 8 && descriptor.ZBits == 8 && descriptor.WBits == 8 && descriptor.Kind == HipChannelFormatKind.UnsignedInteger, "OpenGL RGBA8 channel layout.");
                HipExtent extent = default; uint flags = uint.MaxValue;
                Check(Raw.ArrayGetInfo((IntPtr)(&descriptor), (IntPtr)(&extent), (IntPtr)(&flags), array));
                Require(extent.Width == 64 && extent.Height == 4 && extent.Depth == 1 && flags == 0, "OpenGL mapped array extent and flags.");
                byte* bytes = stackalloc byte[1024]; new Span<byte>(bytes, 1024).Fill(0x2A);
                Check(Raw.Memcpy2DToArray(array, 0, 0, (IntPtr)bytes, 256, 256, 4, 1));
            }
            else
            {
                IntPtr pointer = 0; UIntPtr size = 0;
                Check(Raw.GraphicsResourceGetMappedPointer((IntPtr)(&pointer), (IntPtr)(&size), resource));
                Require(pointer != 0 && size == 4096, "Actual mapped GL buffer pointer/size.");
                Check(Raw.Memset(pointer, 0x5B, 4096)); Check(Raw.DeviceSynchronize());
            }
            Check(Raw.GraphicsUnmapResources(1, (IntPtr)(&resource), 0)); mapped = false;
            Require(gl.Read(gl.Context, image ? (byte)0x2A : (byte)0x5B) == 0, "Independent OpenGL data readback.");
            Console.WriteLine("OPENGL_SHARED_BYTES=passed");
        }
        finally
        {
            if (mapped) Check(Raw.GraphicsUnmapResources(1, (IntPtr)(&resource), 0));
            Check(Raw.GraphicsUnregisterResource(resource));
        }
    }
    private static void OpenGLManagedBuffer()
    {
        using var gl = new GLContext(false);
        using var runtime = new HipRuntime(); runtime.Initialize();
        using var stream = runtime.CreateStream();
        using var resource = runtime.AdvancedInterop.OwnGraphicsResource(gl.Resource);
        using var mapping = runtime.AdvancedInterop.MapGraphicsResources(new[] { resource }, stream);
        var pointer = mapping.GetMappedPointer(resource);
        Require(pointer.Pointer != 0 && pointer.ByteLength == 4096, "Managed mapped OpenGL buffer.");
        resource.Dispose(); Require(resource.IsDisposed, "Graphics owner disposal requested while mapped.");
        Check(Raw.Memset(pointer.Pointer, 0x6C, 4096)); Check(Raw.DeviceSynchronize());
        mapping.Dispose(); stream.Synchronize();
        Require(gl.Read(gl.Context, 0x6C) == 0, "Independent GL readback after deferred unmap/unregister.");
        bool rejected = false;
        try { mapping.GetMappedPointer(resource); } catch (ObjectDisposedException) { rejected = true; }
        Require(rejected, "Disposed graphics mapping rejects use.");
        Console.WriteLine("OPENGL_MANAGED_LIFETIME=passed");
    }
}
