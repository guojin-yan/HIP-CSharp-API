using System;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            using var runtime = new HipRuntime();
            runtime.Initialize();
            using HipGraph graph = runtime.CreateGraph();
            HipGraphNode first = graph.AddEmpty();
            HipGraphNode second = graph.AddEmpty(new[] { first });
            using HipGraphExec executable = graph.Instantiate();
            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            executable.Launch(stream);
            stream.Synchronize();
            bool passed = graph.Edges.Count == 1 && second.Dependencies.Count == 1;
            Console.WriteLine(passed ? "Explicit graph DAG passed." : "Explicit graph DAG verification failed.");
            return passed ? 0 : 1;
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            Console.WriteLine("Skipped: explicit graph DAG is not supported by this HIP Runtime.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
