using System;
using System.Runtime.InteropServices;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    // Same pinned ABI as HipVirtualMemoryAllocationPropertiesNative.
    [StructLayout(LayoutKind.Sequential)] private struct AllocationProperties
    {
        public int Type, HandleTypes;
        public HipMemLocation Location;
        public IntPtr WindowsMetadata;
        public uint Flags, Reserved;
    }

    private static void VirtualMemory()
    {
        int version = 0; Check(Raw.RuntimeGetVersion((IntPtr)(&version)));
        Require(version >= 71200000, "Raw retain fixture requires HIP >= 7.12 (known native reference-count defect before 7.12).");
        AllocationProperties properties = new() { Type = 1, Location = new HipMemLocation(1, 0) };
        UIntPtr length = 0; Check(Raw.MemGetAllocationGranularity((IntPtr)(&length), (IntPtr)(&properties), 0)); Require(length > 0, "Granularity.");
        IntPtr handle = 0, address = 0, retained = 0; bool mapped = false;
        Check(Raw.MemCreate((IntPtr)(&handle), length, (IntPtr)(&properties), 0));
        try
        {
            AllocationProperties queried = default;
            Check(Raw.MemGetAllocationPropertiesFromHandle((IntPtr)(&queried), handle));
            Require(queried.Type == 1 && queried.Location.Type == 1 && queried.Location.Id == 0, "Allocation properties.");
            Check(Raw.MemAddressReserve((IntPtr)(&address), length, 0, IntPtr.Zero, 0)); Require(address != 0, "Null VA reservation.");
            Check(Raw.MemMap(address, length, 0, handle, 0)); mapped = true;
            HipVirtualMemoryAccessDescriptor access = new(new HipMemLocation(1, 0), HipMemoryAccessFlags.ReadWrite);
            Check(Raw.MemSetAccess(address, length, (IntPtr)(&access), 1));
            HipMemLocation location = new(1, 0); ulong flags = 0;
            Check(Raw.MemGetAccess((IntPtr)(&flags), (IntPtr)(&location), address)); Require(flags == 3, "VA access policy.");
            Check(Raw.MemRetainAllocationHandle((IntPtr)(&retained), address)); Require(retained != 0, "Retain returned null.");
            Check(Raw.MemRelease(handle)); handle = 0;
            Check(Raw.Memset(address, 0x7B, 256)); VerifyFill(address, 1, 0x7B);
            Check(Raw.MemUnmap(address, length)); mapped = false;
            Check(Raw.MemMap(address, length, 0, retained, 0)); mapped = true;
            Check(Raw.MemSetAccess(address, length, (IntPtr)(&access), 1)); VerifyFill(address, 1, 0x7B);
        }
        finally
        {
            if (mapped) Check(Raw.MemUnmap(address, length));
            if (address != 0) Check(Raw.MemAddressFree(address, length));
            if (retained != 0) Check(Raw.MemRelease(retained)); if (handle != 0) Check(Raw.MemRelease(handle));
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct PoolProperties
    {
        public int AllocationType, HandleTypes;
        public HipMemLocation Location;
        public IntPtr Security;
        public UIntPtr MaximumSize;
        public fixed byte Reserved[56];
    }

    private static void MemoryPool()
    {
        PoolProperties properties = new() { AllocationType = 1, Location = new HipMemLocation(1, 0) };
        IntPtr pool = 0, original = 0, defaultPool = 0, pointer = 0, stream = 0;
        Check(Raw.DeviceGetMemPool((IntPtr)(&original), 0)); Check(Raw.DeviceGetDefaultMemPool((IntPtr)(&defaultPool), 0));
        Require(original != 0 && defaultPool != 0, "Missing device pools.");
        Check(Raw.MemPoolCreate((IntPtr)(&pool), (IntPtr)(&properties)));
        try
        {
            ulong threshold = 1048576, queried = 0;
            Check(Raw.MemPoolSetAttribute(pool, 4, (IntPtr)(&threshold))); Check(Raw.MemPoolGetAttribute(pool, 4, (IntPtr)(&queried)));
            Require(queried == threshold, "Pool threshold round trip.");
            HipVirtualMemoryAccessDescriptor access = new(new HipMemLocation(1, 0), HipMemoryAccessFlags.ReadWrite);
            Check(Raw.MemPoolSetAccess(pool, (IntPtr)(&access), 1));
            HipMemLocation location = new(1, 0); ulong flags = 0;
            Check(Raw.MemPoolGetAccess((IntPtr)(&flags), pool, (IntPtr)(&location))); Require(flags == 3, "Pool access policy.");
            Check(Raw.StreamCreate((IntPtr)(&stream)));
            Check(Raw.MallocFromPoolAsync((IntPtr)(&pointer), 256, pool, stream));
            Check(Raw.MemsetAsync(pointer, 0x5C, 256, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x5C);
            Check(Raw.FreeAsync(pointer, stream)); pointer = 0; Check(Raw.StreamSynchronize(stream));
            Check(Raw.DeviceSetMemPool(0, pool)); IntPtr current = 0;
            Check(Raw.DeviceGetMemPool((IntPtr)(&current), 0)); Require(current == pool, "Current pool round trip.");
            Check(Raw.MallocAsync((IntPtr)(&pointer), 256, stream));
            Check(Raw.MemsetAsync(pointer, 0x6D, 256, stream)); Check(Raw.StreamSynchronize(stream)); VerifyFill(pointer, 1, 0x6D);
            Check(Raw.FreeAsync(pointer, stream)); pointer = 0; Check(Raw.StreamSynchronize(stream)); Check(Raw.MemPoolTrimTo(pool, 0));
        }
        finally
        {
            if (pointer != 0) Check(Raw.FreeAsync(pointer, stream));
            if (stream != 0) { Check(Raw.StreamSynchronize(stream)); Check(Raw.StreamDestroy(stream)); }
            Check(Raw.DeviceSetMemPool(0, original)); Check(Raw.MemPoolDestroy(pool));
        }
    }

    private static void ArrayCopy()
    {
        HipChannelFormatDescriptor channel = new(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        IntPtr array = 0, second = 0, host = 0, device = 0, stream = 0;
        if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 0, 0))) return;
        try
        {
            Check(Raw.MallocArray((IntPtr)(&second), (IntPtr)(&channel), 64, 0, 0));
            HipChannelFormatDescriptor queried = default; HipExtent extent = default; uint flags = uint.MaxValue;
            Check(Raw.GetChannelDesc((IntPtr)(&queried), array)); Require(queried.XBits == 8 && queried.Kind == channel.Kind, "Array channel.");
            Check(Raw.ArrayGetInfo((IntPtr)(&queried), (IntPtr)(&extent), (IntPtr)(&flags), array));
            Require(extent.Width == 64 && extent.Height == 0 && flags == 0, "Array dimensions/flags.");
            Check(Raw.HostMalloc((IntPtr)(&host), 64, 0)); Check(Raw.Malloc((IntPtr)(&device), 64)); Check(Raw.StreamCreate((IntPtr)(&stream)));
            for (int i = 0; i < 64; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Check(Raw.MemcpyHtoA(array, 0, host, 64)); new Span<byte>((void*)host, 64).Clear();
            Check(Raw.MemcpyAtoH(host, array, 0, 64)); VerifyPattern(host, 64);
            Check(Raw.MemcpyAtoD(device, array, 0, 64)); Check(Raw.MemcpyDtoA(second, 0, device, 64));
            Check(Raw.MemcpyAtoA(array, 0, second, 0, 64)); new Span<byte>((void*)host, 64).Clear();
            Check(Raw.MemcpyAtoH(host, array, 0, 64)); VerifyPattern(host, 64);
            Check(Raw.MemcpyHtoAAsync(second, 0, host, 64, stream)); Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, 64).Clear();
            Check(Raw.MemcpyAtoHAsync(host, second, 0, 64, stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 64);
            Check(Raw.MemcpyToArray(array, 0, 0, host, 64, 1)); new Span<byte>((void*)host, 64).Clear();
            Check(Raw.MemcpyFromArray(host, array, 0, 0, 64, 2)); VerifyPattern(host, 64);
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream)); if (device != 0) Check(Raw.Free(device)); if (host != 0) Check(Raw.HostFree(host));
            if (second != 0) Check(Raw.FreeArray(second)); Check(Raw.FreeArray(array));
        }
    }

    [StructLayout(LayoutKind.Sequential)] private struct ArrayDescriptor { public UIntPtr Width, Height; public int Format; public uint Channels; }
    [StructLayout(LayoutKind.Sequential)] private struct Array3DDescriptor { public UIntPtr Width, Height, Depth; public int Format; public uint Channels, Flags; }
    private static void ArrayDriver()
    {
        IntPtr array = 0; ArrayDescriptor descriptor = new() { Width = 64, Height = 2, Format = 1, Channels = 1 };
        if (!Capability(Raw.ArrayCreate((IntPtr)(&array), (IntPtr)(&descriptor)))) return;
        try
        {
            ArrayDescriptor query = default; Check(Raw.ArrayGetDescriptor((IntPtr)(&query), array));
            Require(query.Width == 64 && query.Height == 2 && query.Format == 1 && query.Channels == 1, "Driver array descriptor.");
        }
        finally { Check(Raw.ArrayDestroy(array)); }
        Array3DDescriptor three = new() { Width = 64, Height = 2, Depth = 2, Format = 1, Channels = 1 };
        array = 0; Check(Raw.Array3DCreate((IntPtr)(&array), (IntPtr)(&three)));
        try
        {
            Array3DDescriptor query = default; Check(Raw.Array3DGetDescriptor((IntPtr)(&query), array));
            Require(query.Width == 64 && query.Height == 2 && query.Depth == 2 && query.Format == 1 && query.Channels == 1 && query.Flags == 0, "Driver 3D descriptor.");
        }
        finally { Check(Raw.ArrayDestroy(array)); }
    }

    private static void Array2D()
    {
        HipChannelFormatDescriptor channel = new(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        IntPtr array = 0, second = 0, host = 0, stream = 0;
        if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 4, 0))) return;
        try
        {
            Check(Raw.MallocArray((IntPtr)(&second), (IntPtr)(&channel), 64, 4, 0));
            Check(Raw.HostMalloc((IntPtr)(&host), 256, 0)); Check(Raw.StreamCreate((IntPtr)(&stream)));
            for (int i = 0; i < 256; i++) ((byte*)host)[i] = (byte)(i * 37 + 11);
            Check(Raw.Memcpy2DToArray(array, 0, 0, host, 64, 64, 4, 1)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.Memcpy2DFromArray(host, 64, array, 0, 0, 64, 4, 2)); VerifyPattern(host, 256);
            Check(Raw.Memcpy2DArrayToArray(second, 0, 0, array, 0, 0, 64, 4, 3)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.Memcpy2DFromArray(host, 64, second, 0, 0, 64, 4, 2)); VerifyPattern(host, 256);
            Check(Raw.Memcpy2DToArrayAsync(array, 0, 0, host, 64, 64, 4, 1, stream)); Check(Raw.StreamSynchronize(stream)); new Span<byte>((void*)host, 256).Clear();
            Check(Raw.Memcpy2DFromArrayAsync(host, 64, array, 0, 0, 64, 4, 2, stream)); Check(Raw.StreamSynchronize(stream)); VerifyPattern(host, 256);
        }
        finally
        {
            if (stream != 0) Check(Raw.StreamDestroy(stream)); if (host != 0) Check(Raw.HostFree(host));
            if (second != 0) Check(Raw.FreeArray(second)); Check(Raw.FreeArray(array));
        }
    }

    private static void MipmappedArray()
    {
        HipChannelFormatDescriptor channel = new(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
        IntPtr mipmapped = 0, level = 0;
        if (!Capability(Raw.MallocMipmappedArray((IntPtr)(&mipmapped), (IntPtr)(&channel), new HipExtent(64, 32, 0), 3, 0))) return;
        try
        {
            for (uint i = 0; i < 3; i++)
            {
                Check(Raw.GetMipmappedArrayLevel((IntPtr)(&level), mipmapped, i)); Require(level != 0, "Null mip level.");
                HipExtent extent = default; HipChannelFormatDescriptor desc = default; uint flags = 0;
                Check(Raw.ArrayGetInfo((IntPtr)(&desc), (IntPtr)(&extent), (IntPtr)(&flags), level));
                Require(extent.Width == (UIntPtr)(64 >> (int)i) && extent.Height == (UIntPtr)(32 >> (int)i) && desc.XBits == 8, "Mip extent/format.");
            }
            Check(Raw.GetMipmappedArrayLevel((IntPtr)(&level), mipmapped, 3), JYPPX.ROCm.HipSharp.Types.HipError.InvalidValue);
        }
        finally { Check(Raw.FreeMipmappedArray(mipmapped)); }
        IntPtr array = 0; Check(Raw.Malloc3DArray((IntPtr)(&array), (IntPtr)(&channel), new HipExtent(16, 8, 4), 0));
        try
        {
            HipExtent extent = default; HipChannelFormatDescriptor desc = default; uint flags = 0;
            Check(Raw.ArrayGetInfo((IntPtr)(&desc), (IntPtr)(&extent), (IntPtr)(&flags), array));
            Require(extent.Width == 16 && extent.Height == 8 && extent.Depth == 4 && desc.XBits == 8, "3D array dimensions.");
        }
        finally { Check(Raw.FreeArray(array)); }
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)] private struct TextureResource
    {
        [FieldOffset(0)] public int Type;
        [FieldOffset(8)] public IntPtr Handle;
    }
    [StructLayout(LayoutKind.Sequential)] private struct TextureParameters
    {
        public int ModeX, ModeY, ModeZ, Filter, ReadMode, Srgb;
        public float Red, Green, Blue, Alpha;
        public int Normalized;
        public uint Anisotropy;
        public int MipmapFilter;
        public float MipmapBias, MinimumClamp, MaximumClamp;
    }

    private static void TextureQueries()
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
            Check(Raw.CreateSurfaceObject((IntPtr)(&surface), (IntPtr)(&resource))); Require(surface != 0, "Null surface.");
        }
        finally
        {
            if (surface != 0) Check(Raw.DestroySurfaceObject(surface)); if (texture != 0) Check(Raw.DestroyTextureObject(texture)); Check(Raw.FreeArray(array));
        }
    }
}
