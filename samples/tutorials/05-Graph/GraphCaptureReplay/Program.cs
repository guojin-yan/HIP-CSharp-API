using System;
using System.Linq;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            byte[] source = Enumerable.Range(0, 256).Select(value => (byte)(255 - value)).ToArray();
            var destination = new byte[source.Length];
            using var runtime = new HipRuntime();
            runtime.Initialize();

            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            using HipDeviceMemory device = runtime.Allocate((ulong)source.Length);
            using HipGraph graph = runtime.CaptureGraph(stream, capturedStream =>
            {
                device.CopyFromAsync(source, capturedStream);
                device.CopyToAsync(destination, capturedStream);
            });
            using HipGraphExec executable = graph.Instantiate();

            executable.Launch(stream);
            stream.Synchronize();

            bool passed = source.SequenceEqual(destination);
            Console.WriteLine(passed ? "Captured graph replay passed." : "Captured graph replay failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: graph capture is not supported by this HIP Runtime or device.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
