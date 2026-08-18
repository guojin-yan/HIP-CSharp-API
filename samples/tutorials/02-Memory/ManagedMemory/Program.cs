using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            byte[] source = Enumerable.Range(0, 128).Select(value => (byte)(value * 3)).ToArray();
            byte[] destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();
            HipDevice device = runtime.GetCurrentDevice();

            using (HipManagedMemory managed = runtime.AllocateManaged((ulong)source.Length))
            using (HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking))
            {
                managed.CopyFromHost(source);
                managed.Advise(HipMemoryAdvise.SetPreferredLocation, device.Ordinal);
                managed.PrefetchAsync(device.Ordinal, stream);
                stream.Synchronize();
                managed.CopyToHost(destination);
            }

            bool passed = source.SequenceEqual(destination);
            Console.WriteLine(passed ? "Managed memory round trip passed." : "Managed memory round trip failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: managed memory or its advice/prefetch operations are not supported.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
