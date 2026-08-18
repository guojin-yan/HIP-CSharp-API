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
            byte[] source = Enumerable.Range(0, 256).Select(value => (byte)value).ToArray();
            var destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();

            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            using HipEvent completed = runtime.CreateEvent(HipEventFlags.DisableTiming);
            using HipDeviceMemory device = runtime.Allocate((ulong)source.Length);

            device.CopyFromAsync(source, stream);
            device.CopyToAsync(destination, stream);
            completed.Record(stream);
            stream.Synchronize();

            bool passed = completed.Query() && source.SequenceEqual(destination);
            Console.WriteLine(passed
                ? "Stream/event asynchronous memory round trip passed."
                : "Stream/event asynchronous memory round trip failed.");
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
