using System;
using System.Globalization;
using System.IO;

internal sealed class Options
{
    private Options(
        string architecture,
        string profile,
        int width,
        int height,
        int steps,
        int gpuRuns,
        string outputDirectory,
        bool writeImage,
        bool helpRequested)
    {
        Architecture = architecture;
        Profile = profile;
        Width = width;
        Height = height;
        Steps = steps;
        GpuRuns = gpuRuns;
        OutputDirectory = outputDirectory;
        WriteImage = writeImage;
        HelpRequested = helpRequested;
    }

    internal string Architecture { get; }

    internal string Profile { get; }

    internal int Width { get; }

    internal int Height { get; }

    internal int Steps { get; }

    internal int GpuRuns { get; }

    internal string OutputDirectory { get; }

    internal bool WriteImage { get; }

    internal bool HelpRequested { get; }

    internal static Options Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string profile = ReadProfile(arguments);
        (int width, int height, int steps) = profile switch
        {
            "tiny" => (256, 256, 50),
            "quick" => (1536, 1536, 600),
            "showcase" => (2048, 2048, 1000),
            _ => throw new ArgumentException("--profile must be tiny, quick, or showcase."),
        };

        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        int gpuRuns = 3;
        bool writeImage = true;
        bool helpRequested = false;
        string outputDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "heat-diffusion",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));

        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--arch":
                    architecture = ReadValue(arguments, ref index, "--arch");
                    break;
                case "--profile":
                    index++;
                    break;
                case "--width":
                    width = ParsePositive(ReadValue(arguments, ref index, "--width"), "--width");
                    break;
                case "--height":
                    height = ParsePositive(ReadValue(arguments, ref index, "--height"), "--height");
                    break;
                case "--steps":
                    steps = ParsePositive(ReadValue(arguments, ref index, "--steps"), "--steps");
                    break;
                case "--gpu-runs":
                    gpuRuns = ParsePositive(ReadValue(arguments, ref index, "--gpu-runs"), "--gpu-runs");
                    break;
                case "--output":
                    outputDirectory = ReadValue(arguments, ref index, "--output");
                    break;
                case "--no-image":
                    writeImage = false;
                    break;
                case "--help":
                case "-h":
                    helpRequested = true;
                    break;
                default:
                    throw new ArgumentException("Unknown argument: " + arguments[index]);
            }
        }

        if (!helpRequested && string.IsNullOrWhiteSpace(architecture))
        {
            throw new ArgumentException("Specify --arch gfxNNNN or set HIPSHARP_GPU_ARCH.");
        }

        ValidateRange(width, 64, 4096, "--width");
        ValidateRange(height, 64, 4096, "--height");
        ValidateRange(steps, 1, 10000, "--steps");
        ValidateRange(gpuRuns, 1, 9, "--gpu-runs");
        _ = checked(width * height * sizeof(float));

        return new Options(
            architecture ?? string.Empty,
            profile,
            width,
            height,
            steps,
            gpuRuns,
            Path.GetFullPath(outputDirectory),
            writeImage,
            helpRequested);
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("HeatDiffusion - CPU/GPU two-dimensional heat equation sample");
        Console.WriteLine("Usage: dotnet run -- --arch gfxNNNN [options]");
        Console.WriteLine("  --profile tiny|quick|showcase  Workload preset (default: quick)");
        Console.WriteLine("  --width N                       Override grid width (64-4096)");
        Console.WriteLine("  --height N                      Override grid height (64-4096)");
        Console.WriteLine("  --steps N                       Override iteration count (1-10000)");
        Console.WriteLine("  --gpu-runs N                    Measured GPU runs (1-9, default: 3)");
        Console.WriteLine("  --output PATH                   Artifact directory");
        Console.WriteLine("  --no-image                      Do not write heatmap.bmp");
    }

    private static string ReadProfile(string[] arguments)
    {
        string profile = "quick";
        for (int index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == "--profile")
            {
                profile = ReadValue(arguments, ref index, "--profile");
            }
        }

        return profile;
    }

    private static string ReadValue(string[] arguments, ref int index, string option)
    {
        index++;
        if (index >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index]))
        {
            throw new ArgumentException("Missing value for " + option + ".");
        }

        return arguments[index];
    }

    private static int ParsePositive(string value, string option)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int parsed) || parsed <= 0)
        {
            throw new ArgumentException(option + " must be a positive 32-bit integer.");
        }

        return parsed;
    }

    private static void ValidateRange(int value, int minimum, int maximum, string option)
    {
        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(option, option + " must be between " + minimum + " and " + maximum + ".");
        }
    }
}
