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
            byte[] source = Enumerable.Range(0, 64).Select(value => (byte)value).ToArray();
            byte[] destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();
            HipDevice device = runtime.GetCurrentDevice();
            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);

            using (HipAsyncDeviceMemory memory = runtime.AllocateAsync((ulong)source.Length, stream))
            {
                memory.CopyFromAsync(source);
                memory.CopyToAsync(destination);
                stream.Synchronize();
            }

            bool asyncPassed = source.SequenceEqual(destination);
            bool poolPassed = false;
            using (HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(device)))
            {
                HipPooledDeviceMemory pooled = pool.AllocateAsync((ulong)source.Length, stream);
                try
                {
                    Array.Clear(destination, 0, destination.Length);
                    pooled.CopyFromAsync(source);
                    pooled.CopyToAsync(destination);
                    stream.Synchronize();
                    poolPassed = source.SequenceEqual(destination);
                }
                finally
                {
                    pooled.Dispose();
                    stream.Synchronize();
                }
            }

            bool passed = asyncPassed && poolPassed;
            Console.WriteLine(passed ? "Async allocation and memory-pool round trip passed." : "Async allocation and memory-pool round trip failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: stream-ordered allocation or memory pools are not supported.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
