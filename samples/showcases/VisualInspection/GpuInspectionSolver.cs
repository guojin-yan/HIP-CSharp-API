using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class GpuInspectionSolver
{
    internal const int DarkThreshold = 100;
    internal const int BrightThreshold = 190;

    internal static GpuInspectionResult Run(
        HipRuntime runtime,
        HipKernel kernel,
        HipStream stream,
        HipEvent start,
        HipEvent end,
        HipDeviceMemory deviceInput,
        HipDeviceMemory deviceOutput,
        HipPinnedMemory pinnedInput,
        HipPinnedMemory pinnedOutput,
        HipGraphExec? graph,
        PgmImage image,
        int gpuRuns)
    {
        int length = checked(image.Width * image.Height);
        var kernelTimes = new double[gpuRuns];
        var endToEndTimes = new double[gpuRuns];
        var output = new byte[length];
        HipKernelArgument[] arguments =
        {
            HipKernelArgument.DevicePointer(deviceInput),
            HipKernelArgument.DevicePointer(deviceOutput),
            HipKernelArgument.Scalar32(length),
            HipKernelArgument.Scalar32(DarkThreshold),
            HipKernelArgument.Scalar32(BrightThreshold),
        };
        var block = new HipLaunchDimensions(256);
        var grid = new HipLaunchDimensions(checked((uint)((length + 255) / 256)));

        for (int run = 0; run < gpuRuns; run++)
        {
            pinnedInput.CopyFrom(image.Pixels);
            var stopwatch = Stopwatch.StartNew();
            deviceInput.CopyFromAsync(pinnedInput, stream);
            start.Record(stream);
            if (graph is not null)
            {
                graph.Launch(stream);
            }
            else
            {
                kernel.Launch(stream, grid, block, arguments);
            }

            end.Record(stream);
            deviceOutput.CopyToAsync(pinnedOutput, stream);
            stream.Synchronize();
            stopwatch.Stop();
            pinnedOutput.CopyTo(output);
            kernelTimes[run] = HipEvent.ElapsedTime(start, end);
            endToEndTimes[run] = stopwatch.Elapsed.TotalMilliseconds;
        }

        return new GpuInspectionResult(output, Median(kernelTimes), Median(endToEndTimes));
    }

    internal static HipGraphExec? TryCapture(
        HipRuntime runtime,
        HipStream stream,
        HipKernel kernel,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> arguments,
        out HipGraph? graph)
    {
        graph = null;
        try
        {
            graph = runtime.CaptureGraph(stream, capturedStream => kernel.Launch(capturedStream, grid, block, arguments));
            return graph.Instantiate();
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            graph?.Dispose();
            graph = null;
            return null;
        }
    }

    private static double Median(double[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return (values.Length & 1) == 0
            ? (values[middle - 1] + values[middle]) / 2.0
            : values[middle];
    }
}

internal sealed class GpuInspectionResult
{
    internal GpuInspectionResult(byte[] mask, double kernelMilliseconds, double endToEndMilliseconds)
    {
        Mask = mask;
        KernelMilliseconds = kernelMilliseconds;
        EndToEndMilliseconds = endToEndMilliseconds;
    }

    internal byte[] Mask { get; }

    internal double KernelMilliseconds { get; }

    internal double EndToEndMilliseconds { get; }
}
