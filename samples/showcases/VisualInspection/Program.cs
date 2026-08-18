using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using JYPPX.ROCm.HipSharp;
using JYPPX.ROCm.HipSharp.Graphs;
using JYPPX.ROCm.HipSharp.Memory;
using JYPPX.ROCm.HipSharp.Modules;
using JYPPX.ROCm.HipSharp.Rtc;
using JYPPX.ROCm.HipSharp.Streams;
using JYPPX.ROCm.HipSharp.Types;

internal static class Program
{
    private static readonly JsonSerializerOptions SummaryJsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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

            VisualRecipe recipe = VisualRecipe.Load(options.InputDirectory);
            Directory.CreateDirectory(options.OutputDirectory);
            string masksDirectory = Path.Combine(options.OutputDirectory, "masks");
            Directory.CreateDirectory(masksDirectory);

            var images = new List<(VisualFixture Fixture, PgmImage Image, PgmImage ExpectedMask)>();
            var cpuMasks = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            Stopwatch cpuStopwatch = Stopwatch.StartNew();
            foreach (VisualFixture fixture in recipe.Fixtures)
            {
                string imagePath = Path.Combine(options.InputDirectory, fixture.Image);
                string expectedMaskPath = Path.Combine(options.InputDirectory, fixture.Mask);
                using var imageMat = OpenCvImageTools.ReadGrayscale(imagePath);
                using var expectedMaskMat = OpenCvImageTools.ReadGrayscale(expectedMaskPath);
                PgmImage image = OpenCvImageTools.ToPgm(imageMat);
                PgmImage expectedMask = OpenCvImageTools.ToPgm(expectedMaskMat);
                if (image.Width != recipe.Width || image.Height != recipe.Height ||
                    expectedMask.Width != image.Width || expectedMask.Height != image.Height)
                {
                    throw new InvalidDataException("Fixture dimensions do not match the recipe: " + fixture.Id);
                }

                cpuMasks[fixture.Id] = OpenCvImageTools.SegmentWithOpenCv(
                    imageMat,
                    GpuInspectionSolver.DarkThreshold,
                    GpuInspectionSolver.BrightThreshold);
                images.Add((fixture, image, expectedMask));
            }
            cpuStopwatch.Stop();

            Console.WriteLine("VisualInspection");
            Console.WriteLine(string.Format(
                CultureInfo.InvariantCulture,
                "Fixtures: {0}; image: {1}x{2}; threshold: dark<{3}, bright>{4}",
                images.Count,
                recipe.Width,
                recipe.Height,
                GpuInspectionSolver.DarkThreshold,
                GpuInspectionSolver.BrightThreshold));

            Console.WriteLine("Compiling and running the HIP inspection kernel...");
            using var runtime = new HipRuntime();
            runtime.Initialize();
            IReadOnlyList<HipDevice> devices = runtime.GetDevices();
            if (devices.Count == 0)
            {
                throw new InvalidOperationException("No HIP device is available.");
            }

            HipDevice device = devices[0];
            device.MakeCurrent();
            string kernelPath = Path.Combine(AppContext.BaseDirectory, "Kernels", "visual-inspection.hip");
            string source = File.ReadAllText(kernelPath);
            var rtc = new HipRtc();
            Stopwatch compileStopwatch = Stopwatch.StartNew();
            using HipRtcProgram program = rtc.CreateProgram(source, "visual-inspection.hip");
            HipRtcCompilation compilation = program.Compile(new[] { "--offload-arch=" + options.Architecture, "-O3" });
            compileStopwatch.Stop();
            using HipModule module = runtime.LoadModule(compilation.GetCodeObject());
            HipKernel kernel = module.GetKernel("SegmentDefects");

            using HipStream stream = runtime.CreateStream(HipStreamFlags.NonBlocking);
            using HipEvent start = runtime.CreateEvent();
            using HipEvent end = runtime.CreateEvent();
            int length = checked(recipe.Width * recipe.Height);
            using HipDeviceMemory deviceInput = runtime.Allocate((ulong)length);
            using HipDeviceMemory deviceOutput = runtime.Allocate((ulong)length);
            using HipPinnedMemory pinnedInput = runtime.AllocatePinned((ulong)length);
            using HipPinnedMemory pinnedOutput = runtime.AllocatePinned((ulong)length);

            HipLaunchDimensions block = new(256);
            HipLaunchDimensions grid = new(checked((uint)((length + 255) / 256)));
            HipKernelArgument[] arguments =
            {
                HipKernelArgument.DevicePointer(deviceInput),
                HipKernelArgument.DevicePointer(deviceOutput),
                HipKernelArgument.Scalar32(length),
                HipKernelArgument.Scalar32(GpuInspectionSolver.DarkThreshold),
                HipKernelArgument.Scalar32(GpuInspectionSolver.BrightThreshold),
            };

            HipGraph? graph = null;
            HipGraphExec? executable = GpuInspectionSolver.TryCapture(runtime, stream, kernel, grid, block, arguments, out graph);
            string executionMode = executable is null ? "direct-stream" : "graph-capture";
            var results = new List<VisualFixtureResult>();
            var kernelTimes = new List<double>();
            var endToEndTimes = new List<double>();
            try
            {
                foreach ((VisualFixture fixture, PgmImage image, PgmImage expectedMask) in images)
                {
                    GpuInspectionResult gpu = GpuInspectionSolver.Run(
                        runtime,
                        kernel,
                        stream,
                        start,
                        end,
                        deviceInput,
                        deviceOutput,
                        pinnedInput,
                        pinnedOutput,
                        executable,
                        image,
                        options.GpuRuns);

                    byte[] cpuMask = cpuMasks[fixture.Id];
                    MaskComparison comparison = CompareMasks(cpuMask, gpu.Mask, expectedMask.Pixels);
                    string maskPath = Path.Combine(masksDirectory, fixture.Id + "_gpu.png");
                    OpenCvImageTools.WritePng(maskPath, recipe.Width, recipe.Height, gpu.Mask);
                    kernelTimes.Add(gpu.KernelMilliseconds);
                    endToEndTimes.Add(gpu.EndToEndMilliseconds);
                    results.Add(new VisualFixtureResult
                    {
                        Id = fixture.Id,
                        DefectType = fixture.DefectType,
                        ExpectedDecision = fixture.ExpectedDecision,
                        ActualDecision = comparison.DefectPixels == 0 ? "PASS" : "FAIL",
                        ExpectedDefectPixels = fixture.DefectPixels,
                        CpuDefectPixels = CountMask(cpuMask),
                        GpuDefectPixels = comparison.DefectPixels,
                        IntersectionOverUnion = comparison.IntersectionOverUnion,
                        MaximumByteDifference = comparison.MaximumByteDifference,
                        Passed = comparison.Passed,
                        MaskPath = Path.GetRelativePath(options.OutputDirectory, maskPath),
                    });
                }
            }
            finally
            {
                executable?.Dispose();
                graph?.Dispose();
            }

            bool passed = results.Count == images.Count && results.All(result => result.Passed);
            var summary = new VisualInspectionSummary
            {
                SchemaVersion = 1,
                Workload = "visual-inspection",
                Status = passed ? "passed" : "failed",
                TimestampUtc = DateTimeOffset.UtcNow,
                Architecture = options.Architecture,
                DeviceName = device.Name,
                DeviceMemoryBytes = device.GetTotalMemory(),
                HipRtcVersion = rtc.GetVersion().ToString(),
                CodeObjectBytes = compilation.CodeSize,
                CodeObjectSha256 = compilation.CodeSha256,
                HipRtcCompileMilliseconds = compileStopwatch.Elapsed.TotalMilliseconds,
                CpuMilliseconds = cpuStopwatch.Elapsed.TotalMilliseconds,
                GpuRunsPerFixture = options.GpuRuns,
                GpuKernelMilliseconds = Median(kernelTimes),
                GpuEndToEndMilliseconds = Median(endToEndTimes),
                ExecutionMode = executionMode,
                Fixtures = results,
            };
            string summaryPath = Path.Combine(options.OutputDirectory, "inspection-summary.json");
            File.WriteAllText(summaryPath, JsonSerializer.Serialize(summary, SummaryJsonOptions));
            WriteCsv(Path.Combine(options.OutputDirectory, "inspection-results.csv"), results);

            PrintSummary(summary, summaryPath);
            return passed ? 0 : 1;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static MaskComparison CompareMasks(byte[] cpuMask, byte[] gpuMask, byte[] expectedMask)
    {
        if (cpuMask.Length != gpuMask.Length || cpuMask.Length != expectedMask.Length)
        {
            throw new InvalidDataException("Mask lengths do not match.");
        }

        int intersection = 0;
        int union = 0;
        int defectPixels = 0;
        int maximumDifference = 0;
        bool passed = true;
        for (int index = 0; index < gpuMask.Length; index++)
        {
            int gpu = gpuMask[index];
            int expected = expectedMask[index];
            int cpu = cpuMask[index];
            maximumDifference = Math.Max(maximumDifference, Math.Abs(cpu - gpu));
            if (gpu != expected || cpu != expected)
            {
                passed = false;
            }

            if (gpu != 0)
            {
                defectPixels++;
            }

            if (gpu != 0 && expected != 0)
            {
                intersection++;
            }

            if (gpu != 0 || expected != 0)
            {
                union++;
            }
        }

        return new MaskComparison(passed, defectPixels, union == 0 ? 1.0 : (double)intersection / union, maximumDifference);
    }

    private static int CountMask(byte[] mask) => mask.Count(value => value != 0);

    private static double Median(List<double> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }
        values.Sort();
        int middle = values.Count / 2;
        return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2.0 : values[middle];
    }

    private static void WriteCsv(string path, List<VisualFixtureResult> results)
    {
        using var writer = new StreamWriter(path, false);
        writer.WriteLine("id,defect_type,expected_decision,actual_decision,expected_defect_pixels,cpu_defect_pixels,gpu_defect_pixels,intersection_over_union,maximum_byte_difference,passed,mask_path");
        foreach (VisualFixtureResult result in results)
        {
            writer.WriteLine(string.Join(",", result.Id, result.DefectType, result.ExpectedDecision, result.ActualDecision,
                result.ExpectedDefectPixels.ToString(CultureInfo.InvariantCulture), result.CpuDefectPixels.ToString(CultureInfo.InvariantCulture),
                result.GpuDefectPixels.ToString(CultureInfo.InvariantCulture), result.IntersectionOverUnion.ToString("F6", CultureInfo.InvariantCulture),
                result.MaximumByteDifference.ToString(CultureInfo.InvariantCulture), result.Passed, result.MaskPath));
        }
    }

    private static void PrintSummary(VisualInspectionSummary summary, string summaryPath)
    {
        Console.WriteLine();
        Console.WriteLine("Result: " + summary.Status.ToUpperInvariant());
        Console.WriteLine("Device: " + summary.DeviceName);
        Console.WriteLine("Execution mode: " + summary.ExecutionMode);
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "HIPRTC compile: {0:F2} ms", summary.HipRtcCompileMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "CPU reference: {0:F2} ms", summary.CpuMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "GPU kernel median: {0:F2} ms", summary.GpuKernelMilliseconds));
        Console.WriteLine(string.Format(CultureInfo.InvariantCulture, "GPU end-to-end median: {0:F2} ms", summary.GpuEndToEndMilliseconds));
        Console.WriteLine("Fixtures passed: " + summary.Fixtures.Count(result => result.Passed) + "/" + summary.Fixtures.Count);
        Console.WriteLine("Summary: " + summaryPath);
    }
}

internal sealed class PgmImageWriter
{
    private readonly int _width;
    private readonly int _height;
    private readonly byte[] _pixels;

    internal PgmImageWriter(int width, int height, byte[] pixels)
    {
        _width = width;
        _height = height;
        _pixels = pixels;
    }

    internal void Write(string path)
    {
        using FileStream stream = File.Create(path);
        byte[] header = System.Text.Encoding.ASCII.GetBytes($"P5\n{_width} {_height}\n255\n");
        stream.Write(header, 0, header.Length);
        stream.Write(_pixels, 0, _pixels.Length);
    }
}

internal sealed class MaskComparison
{
    internal MaskComparison(bool passed, int defectPixels, double intersectionOverUnion, int maximumByteDifference)
    {
        Passed = passed;
        DefectPixels = defectPixels;
        IntersectionOverUnion = intersectionOverUnion;
        MaximumByteDifference = maximumByteDifference;
    }

    internal bool Passed { get; }
    internal int DefectPixels { get; }
    internal double IntersectionOverUnion { get; }
    internal int MaximumByteDifference { get; }
}
