using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JYPPX.ROCm.HipSharp.Types;

internal static unsafe partial class Program
{
    private static byte[] LoadFatbinary()
    {
        string path = Environment.GetEnvironmentVariable("HIPSHARP_TEST_FATBIN") ?? throw new InvalidOperationException("Missing fatbinary payload.");
        string expected = Environment.GetEnvironmentVariable("HIPSHARP_TEST_FATBIN_SHA256") ?? throw new InvalidOperationException("Missing fatbinary hash.");
        byte[] bytes = File.ReadAllBytes(path);
        string digest = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
        Require(digest == expected && Path.GetDirectoryName(Path.GetFullPath(path)) == Path.TrimEndingDirectorySeparator(AppContext.BaseDirectory), "Fatbinary path/hash mismatch.");
        Require(bytes.Length > 32 && Encoding.ASCII.GetString(bytes, 0, 24).StartsWith("__CLANG_OFFLOAD_BUNDLE__"), "Expected compiler fatbinary bundle.");
        Console.WriteLine("NATIVE_FATBIN=" + JsonSerializer.Serialize(new { path, sha256 = digest }));
        return bytes;
    }

    private static void ModuleTexture(int mode)
    {
        byte[] bytes = LoadFatbinary(); IntPtr module = 0, texture = 0, array = 0, mipmapped = 0, pointer = 0; UIntPtr pitch = 0;
        fixed (byte* image = bytes) Check(Raw.ModuleLoadFatBinary((IntPtr)(&module), (IntPtr)image));
        bool bound = false;
        try
        {
            using var name = new Utf8("ValidationTexture");
            if (!Capability(Raw.ModuleGetTexRef((IntPtr)(&texture), module, name.Pointer))) return;
            Require(texture != 0, "Module texture reference.");
            if (!Capability(Raw.TexRefSetFormat(texture, 1, 1))) return;
            Check(Raw.TexRefSetFlags(texture, 1)); // HIP_TRSF_READ_AS_INTEGER for unsigned-byte sampling.
            Check(Raw.TexRefSetAddressMode(texture, 0, 1)); Check(Raw.TexRefSetAddressMode(texture, 1, 1));
            Check(Raw.TexRefSetFilterMode(texture, 0));
            if (mode == 3)
            {
                var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
                if (!Capability(Raw.MallocMipmappedArray((IntPtr)(&mipmapped), (IntPtr)(&channel), new HipExtent(64, 4, 0), 2, 0))) return;
                Check(Raw.GetMipmappedArrayLevel((IntPtr)(&array), mipmapped, 0));
                byte* host = stackalloc byte[256]; new Span<byte>(host, 256).Fill(41);
                Check(Raw.Memcpy2DToArray(array, 0, 0, (IntPtr)host, 64, 64, 4, 1));
                Check(Raw.TexRefSetFlags(texture, 3));
                if (!Capability(Raw.TexRefSetMipmappedArray(texture, mipmapped, 1))) return;
                bound = true; IntPtr query = 0;
                Check(Raw.TexRefGetMipMappedArray((IntPtr)(&query), texture)); Require(query == mipmapped, "Driver module mipmapped reference.");
                SampleModuleTexture(module);
            }
            else if (mode == 0)
            {
                var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
                if (!Capability(Raw.MallocArray((IntPtr)(&array), (IntPtr)(&channel), 64, 4, 0))) return;
                byte* host = stackalloc byte[256]; new Span<byte>(host, 256).Fill(41);
                Check(Raw.Memcpy2DToArray(array, 0, 0, (IntPtr)host, 64, 64, 4, 1));
                Check(Raw.TexRefSetArray(texture, array, 1)); bound = true;
                IntPtr query = 0; Check(Raw.TexRefGetArray((IntPtr)(&query), texture)); Require(query == array, "Driver-bound module array.");
                SampleModuleTexture(module);
            }
            else
            {
                UIntPtr offset = UIntPtr.MaxValue;
                if (mode == 1)
                {
                    Check(Raw.Malloc((IntPtr)(&pointer), 256)); Check(Raw.Memset(pointer, 41, 256));
                    Check(Raw.TexRefSetAddress((IntPtr)(&offset), texture, pointer, 256)); Require(offset == 0, "Module linear texture alignment.");
                }
                else
                {
                    Check(Raw.MallocPitch((IntPtr)(&pointer), (IntPtr)(&pitch), 64, 4)); Check(Raw.Memset2D(pointer, pitch, 41, 64, 4));
                    ArrayDescriptor descriptor = new() { Width = 64, Height = 4, Format = 1, Channels = 1 };
                    Check(Raw.TexRefSetAddress2D(texture, (IntPtr)(&descriptor), pointer, pitch));
                }
                bound = true; IntPtr query = 0;
                Check(Raw.TexRefGetAddress((IntPtr)(&query), texture)); Require(query == pointer, "Module texture address.");
                if (mode == 2) SampleModuleTexture(module);
            }
        }
        finally
        {
            if (bound) Check(Raw.UnbindTexture(texture));
            if (mipmapped != 0) Check(Raw.FreeMipmappedArray(mipmapped)); else if (array != 0) Check(Raw.FreeArray(array));
            if (pointer != 0) Check(Raw.Free(pointer));
            Check(Raw.ModuleUnload(module));
        }
    }
    private static void SampleModuleTexture(IntPtr module)
    {
        IntPtr function = 0, pointer = 0; using var name = new Utf8("SampleRegisteredTexture");
        Check(Raw.ModuleGetFunction((IntPtr)(&function), module, name.Pointer)); Check(Raw.Malloc((IntPtr)(&pointer), sizeof(int)));
        try
        {
            IntPtr argument = (IntPtr)(&pointer);
            Check(Raw.ModuleLaunchKernel(function, 1, 1, 1, 1, 1, 1, 0, 0, (IntPtr)(&argument), 0)); Check(Raw.DeviceSynchronize());
            int result = 0; Check(Raw.MemcpyDtoH((IntPtr)(&result), pointer, sizeof(int))); Require(result == 41, $"Driver module texture sampling: expected 41, actual {result}.");
        }
        finally { Check(Raw.Free(pointer)); }
    }
}
