using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;
internal static unsafe partial class Program
{
    private static void TextureResourceView()
    {
        HipChannelFormatDescriptor channel = new(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        IntPtr array = 0; ulong texture = 0, surface = 0;
        if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 4, 2))) return;
        try
        {
            TextureResource resource = new() { Handle = array };
            TextureParameters parameters = new() { ModeX = 1, ModeY = 1, ModeZ = 1 };
            Check(Raw.CreateTextureObject((IntPtr)(&texture), (IntPtr)(&resource), (IntPtr)(&parameters), IntPtr.Zero)); Require(texture != 0, "Null texture.");
            TextureResource query = default; TextureParameters parameterQuery = default;
            Check(Raw.GetTextureObjectResourceDesc((IntPtr)(&query), texture)); Require(query.Type == 0 && query.Handle == array, "Texture resource query.");
            Check(Raw.GetTextureObjectTextureDesc((IntPtr)(&parameterQuery), texture));
            Require(parameterQuery.ModeX == 1 && parameterQuery.ModeY == 1 && parameterQuery.Filter == 0 && parameterQuery.ReadMode == 0, "Texture sampling query.");
            ResourceView view = default;
            if (Capability(Raw.GetTextureObjectResourceViewDesc((IntPtr)(&view), texture))) Require(view.Format == 0, "Default runtime resource view.");
            Check(Raw.CreateSurfaceObject((IntPtr)(&surface), (IntPtr)(&resource))); Require(surface != 0, "Null surface.");
        }
        finally
        {
            if (surface != 0) Check(Raw.DestroySurfaceObject(surface)); if (texture != 0) Check(Raw.DestroyTextureObject(texture)); Check(Raw.FreeArray(array));
        }
    }
}
