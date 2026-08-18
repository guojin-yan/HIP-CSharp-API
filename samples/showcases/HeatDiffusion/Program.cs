using System;
using System.Globalization;
using System.IO;

internal static class Program
{
    private const double MaximumAbsoluteErrorTolerance = 0.05;
    private const double RootMeanSquareErrorTolerance = 0.01;

    private static int Main(string[] args)
    {
        try
        {
            Options options = Options.Parse(args);
            if (options.HelpRequested)
            {
                Options.PrintUsage();
                return 0;
            }

            Directory.CreateDirectory(options.OutputDirectory);
            long cellUpdates = checked((long)options.Width * options.Height * options.Steps);
            Console.WriteLine("HeatDiffusion");
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "Grid: {0}x{1}; steps: {2}; cell updates: {3:N0}",
                options.Width,
                options.Height,
                options.Steps,
                cellUpdates));

            float[] fixedField = HeatProblem.CreateFixedField(options.Width, options.Height);
            float[] initialField = HeatProblem.CreateInitialField(fixedField);
            Console.WriteLine("Running the C# CPU reference...");
            CpuSolveResult cpu = CpuHeatSolver.Solve(initialField, fixedField, options.Width, options.Height, options.Steps);

            Console.WriteLine("Compiling and running the HIP GPU solver...");
            GpuSolveResult gpu = GpuHeatSolver.Solve(initialField, fixedField, options);
            if (gpu.KernelMilliseconds <= 0.0 || gpu.EndToEndMilliseconds <= 0.0)
            {
                throw new InvalidOperationException("HIP returned a non-positive timing result.");
            }

            ErrorMetrics metrics = ErrorMetrics.Compare(cpu.Field, gpu.Field);
            bool passed = metrics.NonFiniteValues == 0 &&
                metrics.MaximumAbsoluteError <= MaximumAbsoluteErrorTolerance &&
                metrics.RootMeanSquareError <= RootMeanSquareErrorTolerance;

            string? heatmapPath = null;
            if (options.WriteImage)
            {
                heatmapPath = Path.Combine(options.OutputDirectory, "heatmap.bmp");
                HeatmapBmpWriter.Write(heatmapPath, gpu.Field, options.Width, options.Height);
            }

            double speedup = cpu.ElapsedMilliseconds / gpu.EndToEndMilliseconds;
            double updatesPerSecond = cellUpdates / (gpu.KernelMilliseconds / 1000.0);
            var summary = new ResultSummary
            {
                SchemaVersion = 1,
                Workload = "heat-diffusion",
                Status = passed ? "passed" : "failed",
                TimestampUtc = DateTimeOffset.UtcNow,
                PerformanceScope = "current-session-measurement",
                Profile = options.Profile,
                Width = options.Width,
                Height = options.Height,
                Steps = options.Steps,
                CellUpdates = cellUpdates,
                CpuWorkers = cpu.WorkerCount,
                CpuMilliseconds = cpu.ElapsedMilliseconds,
                GpuRuns = options.GpuRuns,
                GpuCompileMilliseconds = gpu.CompileMilliseconds,
                GpuKernelMilliseconds = gpu.KernelMilliseconds,
                GpuEndToEndMilliseconds = gpu.EndToEndMilliseconds,
                ObservedEndToEndSpeedup = speedup,
                GpuCellUpdatesPerSecond = updatesPerSecond,
                ExecutionMode = gpu.ExecutionMode,
                Architecture = options.Architecture,
                DeviceName = gpu.DeviceName,
                DeviceMemoryBytes = gpu.DeviceMemoryBytes,
                HipRuntimeVersion = gpu.RuntimeVersion,
                HipDriverVersion = gpu.DriverVersion,
                HipRtcVersion = gpu.RtcVersion,
                CodeObjectBytes = gpu.CodeObjectBytes,
                CodeObjectSha256 = gpu.CodeObjectSha256,
                MaximumAbsoluteError = metrics.MaximumAbsoluteError,
                RootMeanSquareError = metrics.RootMeanSquareError,
                NonFiniteValues = metrics.NonFiniteValues,
                MaximumAbsoluteErrorTolerance = MaximumAbsoluteErrorTolerance,
                RootMeanSquareErrorTolerance = RootMeanSquareErrorTolerance,
                HeatmapPath = heatmapPath is null ? null : Path.GetFileName(heatmapPath),
            };
            string summaryPath = Path.Combine(options.OutputDirectory, "summary.json");
            summary.Write(summaryPath);

            PrintResults(cpu, gpu, metrics, speedup, updatesPerSecond, summaryPath, heatmapPath, passed);
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void PrintResults(
        CpuSolveResult cpu,
        GpuSolveResult gpu,
        ErrorMetrics metrics,
        double speedup,
        double updatesPerSecond,
        string summaryPath,
        string? heatmapPath,
        bool passed)
    {
        Console.WriteLine();
        Console.WriteLine("Result: " + (passed ? "PASSED" : "FAILED"));
        Console.WriteLine("Device: " + gpu.DeviceName);
        Console.WriteLine("Execution mode: " + gpu.ExecutionMode);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "HIPRTC compile: {0:F2} ms", gpu.CompileMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "CPU ({0} workers): {1:F2} ms", cpu.WorkerCount, cpu.ElapsedMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "GPU kernel median: {0:F2} ms", gpu.KernelMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "GPU end-to-end median: {0:F2} ms", gpu.EndToEndMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Observed end-to-end speedup: {0:F2}x", speedup));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "GPU throughput: {0:F3} billion cell updates/s", updatesPerSecond / 1_000_000_000.0));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "Maximum absolute error: {0:G6}", metrics.MaximumAbsoluteError));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "RMSE: {0:G6}", metrics.RootMeanSquareError));
        Console.WriteLine("Summary: " + summaryPath);
        if (heatmapPath is not null)
        {
            Console.WriteLine("Heatmap: " + heatmapPath);
        }

        Console.WriteLine("Performance values describe this process run only; HIPRTC compilation is excluded from the speedup.");
    }
}
