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

internal static class GpuHeatSolver
{
    internal static GpuSolveResult Solve(float[] initialField, float[] fixedField, Options options)
    {
        ArgumentNullException.ThrowIfNull(initialField);
        ArgumentNullException.ThrowIfNull(fixedField);
        if (fixedField.Length != initialField.Length)
        {
            throw new ArgumentException("The initial and fixed fields must have the same length.", nameof(fixedField));
        }

        string kernelPath = Path.Combine(AppContext.BaseDirectory, "Kernels", "heat-diffusion.hip");
        string kernelSource = File.ReadAllText(kernelPath);

        using var runtime = new HipRuntime();
        runtime.Initialize();
        IReadOnlyList<HipDevice> devices = runtime.GetDevices();
        if (devices.Count == 0)
        {
            throw new InvalidOperationException("No HIP device is available.");
        }

        HipDevice device = devices[0];
        device.MakeCurrent();
        HipRuntimeVersionInfo runtimeVersion = runtime.GetVersionInfo();

        var rtc = new HipRtc();
        HipRtcVersion rtcVersion = rtc.GetVersion();
        var compileStopwatch = Stopwatch.StartNew();
        using HipRtcProgram program = rtc.CreateProgram(kernelSource, "heat-diffusion.hip");
        HipRtcCompilation compilation = program.Compile(new[]
        {
            "--offload-arch=" + options.Architecture,
            "-O3",
        });
        compileStopwatch.Stop();

        using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
        HipKernel kernel = module.GetKernel("HeatStep");
        using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
        using HipEvent start = runtime.CreateEvent();
        using HipEvent end = runtime.CreateEvent();

        byte[] initialBytes = ToBytes(initialField);
        byte[] fixedBytes = ToBytes(fixedField);
        var outputBytes = new byte[initialBytes.Length];
        ulong byteLength = (ulong)initialBytes.LongLength;
        using HipDeviceMemory deviceCurrent = runtime.Allocate(byteLength);
        using HipDeviceMemory deviceNext = runtime.Allocate(byteLength);
        using HipDeviceMemory deviceFixed = runtime.Allocate(byteLength);

        var block = new HipLaunchDimensions(16, 16);
        var grid = new HipLaunchDimensions(
            checked((uint)((options.Width + 15) / 16)),
            checked((uint)((options.Height + 15) / 16)));
        HipKernelArgument[] forwardArguments = CreateArguments(deviceCurrent, deviceNext, deviceFixed, options);
        HipKernelArgument[] reverseArguments = CreateArguments(deviceNext, deviceCurrent, deviceFixed, options);

        deviceCurrent.CopyFrom(initialBytes);
        deviceFixed.CopyFrom(fixedBytes);
        kernel.Launch(stream, grid, block, forwardArguments);
        stream.Synchronize();

        HipGraph? graph = null;
        HipGraphExec? executable = null;
        string executionMode = "direct-stream";
        if (options.Steps >= 2)
        {
            TryCreateGraph(runtime, stream, kernel, grid, block, forwardArguments, reverseArguments, out graph, out executable);
            if (executable is not null)
            {
                executionMode = "graph-capture";
                executable.Launch(stream);
                stream.Synchronize();
            }
        }

        try
        {
            var kernelTimes = new double[options.GpuRuns];
            var endToEndTimes = new double[options.GpuRuns];
            for (int run = 0; run < options.GpuRuns; run++)
            {
                var endToEndStopwatch = Stopwatch.StartNew();
                deviceCurrent.CopyFromAsync(initialBytes, stream);
                start.Record(stream);
                QueueSteps(
                    kernel,
                    stream,
                    executable,
                    grid,
                    block,
                    forwardArguments,
                    reverseArguments,
                    options.Steps);
                end.Record(stream);

                HipDeviceMemory finalMemory = options.Steps % 2 == 0 ? deviceCurrent : deviceNext;
                finalMemory.CopyToAsync(outputBytes, stream);
                stream.Synchronize();
                endToEndStopwatch.Stop();

                kernelTimes[run] = HipEvent.ElapsedTime(start, end);
                endToEndTimes[run] = endToEndStopwatch.Elapsed.TotalMilliseconds;
            }

            var output = new float[initialField.Length];
            Buffer.BlockCopy(outputBytes, 0, output, 0, outputBytes.Length);
            return new GpuSolveResult(
                output,
                Median(kernelTimes),
                Median(endToEndTimes),
                compileStopwatch.Elapsed.TotalMilliseconds,
                executionMode,
                device.Name,
                device.GetTotalMemory(),
                runtimeVersion.RuntimeVersion.ToString(),
                runtimeVersion.DriverVersion.ToString(),
                rtcVersion.ToString(),
                compilation.CodeSize,
                compilation.CodeSha256);
        }
        finally
        {
            executable?.Dispose();
            graph?.Dispose();
        }
    }

    private static HipKernelArgument[] CreateArguments(
        HipDeviceMemory source,
        HipDeviceMemory destination,
        HipDeviceMemory fixedField,
        Options options) => new[]
    {
        HipKernelArgument.DevicePointer(source),
        HipKernelArgument.DevicePointer(destination),
        HipKernelArgument.DevicePointer(fixedField),
        HipKernelArgument.Scalar32(options.Width),
        HipKernelArgument.Scalar32(options.Height),
    };

    private static void TryCreateGraph(
        HipRuntime runtime,
        HipStream stream,
        HipKernel kernel,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> forwardArguments,
        IReadOnlyList<HipKernelArgument> reverseArguments,
        out HipGraph? graph,
        out HipGraphExec? executable)
    {
        graph = null;
        executable = null;
        try
        {
            graph = runtime.CaptureGraph(stream, capturedStream =>
            {
                kernel.Launch(capturedStream, grid, block, forwardArguments);
                kernel.Launch(capturedStream, grid, block, reverseArguments);
            });
            executable = graph.Instantiate();
        }
        catch (HipException exception) when (exception.Error == HipError.NotSupported)
        {
            executable?.Dispose();
            graph?.Dispose();
            executable = null;
            graph = null;
        }
    }

    private static void QueueSteps(
        HipKernel kernel,
        HipStream stream,
        HipGraphExec? executable,
        HipLaunchDimensions grid,
        HipLaunchDimensions block,
        IReadOnlyList<HipKernelArgument> forwardArguments,
        IReadOnlyList<HipKernelArgument> reverseArguments,
        int steps)
    {
        int pairs = steps / 2;
        for (int pair = 0; pair < pairs; pair++)
        {
            if (executable is not null)
            {
                executable.Launch(stream);
            }
            else
            {
                kernel.Launch(stream, grid, block, forwardArguments);
                kernel.Launch(stream, grid, block, reverseArguments);
            }
        }

        if ((steps & 1) != 0)
        {
            kernel.Launch(stream, grid, block, forwardArguments);
        }
    }

    private static byte[] ToBytes(float[] values)
    {
        var bytes = new byte[checked(values.Length * sizeof(float))];
        Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    private static double Median(double[] values)
    {
        var ordered = (double[])values.Clone();
        Array.Sort(ordered);
        int middle = ordered.Length / 2;
        return (ordered.Length & 1) == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2.0
            : ordered[middle];
    }
}

internal sealed class GpuSolveResult
{
    internal GpuSolveResult(
        float[] field,
        double kernelMilliseconds,
        double endToEndMilliseconds,
        double compileMilliseconds,
        string executionMode,
        string deviceName,
        ulong deviceMemoryBytes,
        string runtimeVersion,
        string driverVersion,
        string rtcVersion,
        ulong codeObjectBytes,
        string codeObjectSha256)
    {
        Field = field;
        KernelMilliseconds = kernelMilliseconds;
        EndToEndMilliseconds = endToEndMilliseconds;
        CompileMilliseconds = compileMilliseconds;
        ExecutionMode = executionMode;
        DeviceName = deviceName;
        DeviceMemoryBytes = deviceMemoryBytes;
        RuntimeVersion = runtimeVersion;
        DriverVersion = driverVersion;
        RtcVersion = rtcVersion;
        CodeObjectBytes = codeObjectBytes;
        CodeObjectSha256 = codeObjectSha256;
    }

    internal float[] Field { get; }

    internal double KernelMilliseconds { get; }

    internal double EndToEndMilliseconds { get; }

    internal double CompileMilliseconds { get; }

    internal string ExecutionMode { get; }

    internal string DeviceName { get; }

    internal ulong DeviceMemoryBytes { get; }

    internal string RuntimeVersion { get; }

    internal string DriverVersion { get; }

    internal string RtcVersion { get; }

    internal ulong CodeObjectBytes { get; }

    internal string CodeObjectSha256 { get; }
}
