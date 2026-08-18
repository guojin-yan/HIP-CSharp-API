using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            byte[] source = Enumerable.Range(0, 128).Select(value => (byte)value).ToArray();
            byte[] destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();

            using (HipPinnedMemory pinned = runtime.AllocatePinned((ulong)source.Length))
            {
                pinned.CopyFrom(source);
                pinned.CopyTo(destination);
            }

            bool passed = source.SequenceEqual(destination);
            Console.WriteLine(passed ? "Pinned host memory round trip passed." : "Pinned host memory round trip failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: pinned host memory is not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
