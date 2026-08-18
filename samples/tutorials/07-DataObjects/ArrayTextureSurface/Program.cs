using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Textures;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            const ulong width = 16;
            const ulong height = 4;
            byte[] source = Enumerable.Range(0, checked((int)(width * height))).Select(value => (byte)value).ToArray();
            var destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();

            var channel = new HipChannelFormatDescriptor(8, 0, 0, 0, HipChannelFormatKind.UnsignedInteger);
            using HipArray array = runtime.AllocateArray(channel, width, height, HipArrayFlags.SurfaceLoadStore);
            array.Copy2DFrom(source, width, height);
            array.Copy2DTo(destination, width, height);

            using HipTextureObject texture = runtime.CreateTextureObject(array, new HipTextureDescriptor
            {
                AddressModeX = HipTextureAddressMode.Clamp,
                AddressModeY = HipTextureAddressMode.Clamp,
                FilterMode = HipTextureFilterMode.Point,
                ReadMode = HipTextureReadMode.ElementType,
            });
            using HipSurfaceObject surface = runtime.CreateSurfaceObject(array);

            bool passed = source.SequenceEqual(destination)
                && texture.GetResourceInfo().Kind == HipTextureResourceKind.Array
                && texture.DangerousGetHandle() != 0
                && surface.DangerousGetHandle() != 0;
            Console.WriteLine(passed ? "Array/texture/surface lifecycle passed." : "Array/texture/surface validation failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: array, texture, or surface objects are not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
