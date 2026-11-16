using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    [StructLayout(LayoutKind.Explicit, Size = 144)] private struct DriverResource
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public IntPtr Array;
        [FieldOffset(136)] public uint Flags;
    }
    [StructLayout(LayoutKind.Explicit, Size = 104)] private struct DriverTexture
    {
        [FieldOffset(0)] public int ModeX;
        [FieldOffset(4)] public int ModeY;
        [FieldOffset(8)] public int ModeZ;
        [FieldOffset(12)] public int Filter;
    }
    [StructLayout(LayoutKind.Sequential)] private struct ResourceView
    {
        public int Format;
        public UIntPtr Width, Height, Depth;
        public uint FirstMip, LastMip, FirstLayer, LastLayer;
        public fixed uint Reserved[16];
    }
    private static void DriverTextureObject()
    {
        var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        IntPtr array = 0; ulong texture = 0;
        if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 4, 0))) return;
        try
        {
            DriverResource resource = new() { Array = array };
            DriverTexture parameters = new() { ModeX = 1, ModeY = 1, ModeZ = 1 };
            if (!Capability(Raw.TexObjectCreate((IntPtr)(&texture), (IntPtr)(&resource), (IntPtr)(&parameters), 0))) return;
            Require(texture != 0, "Driver texture.");
            DriverResource queried = default; DriverTexture textureQuery = default;
            Check(Raw.TexObjectGetResourceDesc((IntPtr)(&queried), texture)); Require(queried.Array == array && queried.Type == 0 && queried.Flags == 0, "Driver texture resource.");
            Check(Raw.TexObjectGetTextureDesc((IntPtr)(&textureQuery), texture)); Require(textureQuery.ModeX == 1 && textureQuery.ModeY == 1 && textureQuery.Filter == 0, "Driver texture parameters.");
            ResourceView view = default;
            if (Capability(Raw.TexObjectGetResourceViewDesc((IntPtr)(&view), texture))) Require(view.Format == 0, "Default driver resource view.");
        }
        finally { if (texture != 0) Check(Raw.TexObjectDestroy(texture)); Check(Raw.FreeArray(array)); }
    }

    private static void DriverMipmapped()
    {
        Array3DDescriptor descriptor = new() { Width = 64, Height = 32, Format = 1, Channels = 1 };
        IntPtr mipmapped = 0, array = 0;
        if (!Capability(Raw.MipmappedArrayCreate((IntPtr)(&mipmapped), (IntPtr)(&descriptor), 3))) return;
        try
        {
            for (uint i = 0; i != 3; i++)
            {
                Check(Raw.MipmappedArrayGetLevel((IntPtr)(&array), mipmapped, i));
                ArrayDescriptor query = default; Check(Raw.ArrayGetDescriptor((IntPtr)(&query), array));
                Require(query.Width == (UIntPtr)(64 >> (int)i) && query.Height == (UIntPtr)(32 >> (int)i), "Driver mip extent.");
            }
        }
        finally { Check(Raw.MipmappedArrayDestroy(mipmapped)); }
    }
}
