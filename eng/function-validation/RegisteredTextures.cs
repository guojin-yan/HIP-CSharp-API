using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static void RegisteredTextureParameters()
    {
        IntPtr handle = LoadHelperPointer("RegisteredTexture");
        if (!Capability(Raw.TexRefSetFormat(handle, 1, 1))) return;
        int format = 0, channels = 0, mode = -1; uint flags = uint.MaxValue, anisotropy = 0;
        Check(Raw.TexRefGetFormat((IntPtr)(&format), (IntPtr)(&channels), handle)); Require(format == 1 && channels == 1, "Legacy format.");
        Check(Raw.TexRefSetAddressMode(handle, 0, 1)); Check(Raw.TexRefGetAddressMode((IntPtr)(&mode), handle, 0)); Require(mode == 1, "Legacy address mode.");
        Check(Raw.TexRefSetFilterMode(handle, 0)); Check(Raw.TexRefGetFilterMode((IntPtr)(&mode), handle)); Require(mode == 0, "Legacy filter.");
        Check(Raw.TexRefSetFlags(handle, 0)); Check(Raw.TexRefGetFlags((IntPtr)(&flags), handle)); Require(flags == 0, "Legacy flags.");
        Check(Raw.TexRefSetMaxAnisotropy(handle, 1)); Check(Raw.TexRefGetMaxAnisotropy((IntPtr)(&anisotropy), handle)); Require(anisotropy == 1, "Legacy anisotropy.");
    }

    private static void SampleTexture()
    {
        IntPtr kernel = LoadHelperPointer("RegisteredTextureKernel"), pointer = 0;
        Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int)));
        try
        {
            IntPtr argument = (IntPtr)(&pointer);
            Check(Raw.LaunchKernel(kernel, new HipDim3(1), new HipDim3(1), (IntPtr)(&argument), 0, 0));
            Check(Raw.DeviceSynchronize()); int value = 0; Check(Raw.MemcpyDtoH((IntPtr)(&value), pointer, sizeof(int)));
            Require(value == 41, "Registered texture sampling.");
        }
        finally { Check(Raw.Free(pointer)); }
    }
    private static void RegisteredTextureArray()
    {
        IntPtr texture = LoadHelperPointer("RegisteredTexture"), array = 0;
        var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 4, 0))) return;
        bool bound = false;
        try
        {
            byte* data = stackalloc byte[256]; new Span<byte>(data, 256).Fill(41);
            Check(Raw.Memcpy2DToArray(array, 0, 0, (IntPtr)data, 64, 64, 4, 1));
            Check(Raw.BindTextureToArray(texture, array, (IntPtr)(&channel))); bound = true;
            IntPtr query = 0; Check(Raw.GetTextureReference((IntPtr)(&query), texture)); Require(query == texture, "Registered reference identity.");
            UIntPtr alignment = UIntPtr.MaxValue; Check(Raw.GetTextureAlignmentOffset((IntPtr)(&alignment), texture)); Require(alignment == 0, "Array texture alignment.");
            Check(Raw.TexRefGetArray((IntPtr)(&query), texture)); Require(query == array, "Bound array."); SampleTexture();
        }
        finally { if (bound) Check(Raw.UnbindTexture(texture)); Check(Raw.FreeArray(array)); }
    }
    private static void RegisteredTextureLinear(bool pitched)
    {
        IntPtr texture = LoadHelperPointer("RegisteredTexture"), pointer = 0; UIntPtr pitch = 0;
        var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        if (pitched) Check(Raw.MallocPitch((IntPtr)(&pointer), (IntPtr)(&pitch), 64, 4));
        else Check(Raw.Malloc((IntPtr)(&pointer), 256));
        bool bound = false;
        try
        {
            UIntPtr offset = UIntPtr.MaxValue;
            if (pitched) { if (!Capability(Raw.BindTexture2D((IntPtr)(&offset), texture, pointer, (IntPtr)(&channel), 64, 4, pitch))) return; }
            else { if (!Capability(Raw.BindTexture((IntPtr)(&offset), texture, pointer, (IntPtr)(&channel), 256))) return; }
            bound = true; Require(offset == 0, "Linear texture alignment.");
            IntPtr query = 0; Check(Raw.TexRefGetAddress((IntPtr)(&query), texture)); Require(query == pointer, "Bound texture address.");
            if (pitched) { Check(Raw.Memset2D(pointer, pitch, 41, 64, 4)); SampleTexture(); }
        }
        finally { if (bound) Check(Raw.UnbindTexture(texture)); Check(Raw.Free(pointer)); }
    }

    private static void RegisteredTextureMipmapped()
    {
        IntPtr texture = LoadHelperPointer("RegisteredTexture"), mipmapped = 0, array = 0;
        var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        if (!Capability(Raw.MallocMipmappedArray((IntPtr)(&mipmapped), (IntPtr)(&channel), new HipExtent(64, 4, 0), 2, 0))) return;
        bool bound = false;
        try
        {
            Check(Raw.GetMipmappedArrayLevel((IntPtr)(&array), mipmapped, 0));
            byte* data = stackalloc byte[256]; new Span<byte>(data, 256).Fill(41);
            Check(Raw.Memcpy2DToArray(array, 0, 0, (IntPtr)data, 64, 64, 4, 1));
            Check(Raw.TexRefSetFlags(texture, 3)); // Integer reads and normalized coordinates are required.
            if (!Capability(Raw.BindTextureToMipmappedArray(texture, mipmapped, (IntPtr)(&channel)))) return;
            bound = true; IntPtr query = 0;
            Check(Raw.TexRefGetMipMappedArray((IntPtr)(&query), texture)); Require(query == mipmapped, "Registered mipmapped array.");
            SampleTexture();
        }
        finally { if (bound) Check(Raw.UnbindTexture(texture)); Check(Raw.FreeMipmappedArray(mipmapped)); }
    }
}
