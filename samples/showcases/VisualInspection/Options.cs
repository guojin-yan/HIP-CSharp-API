using System;
using System.Globalization;
using System.IO;

internal sealed class Options
{
    private Options(string architecture, string inputDirectory, string outputDirectory, int gpuRuns, bool helpRequested)
    {
        Architecture = architecture;
        InputDirectory = inputDirectory;
        OutputDirectory = outputDirectory;
        GpuRuns = gpuRuns;
        HelpRequested = helpRequested;
    }

    internal string Architecture { get; }

    internal string InputDirectory { get; }

    internal string OutputDirectory { get; }

    internal int GpuRuns { get; }

    internal bool HelpRequested { get; }

    internal static Options Parse(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        string? architecture = Environment.GetEnvironmentVariable("HIPSHARP_GPU_ARCH");
        string inputDirectory = Path.Combine(AppContext.BaseDirectory, "assets");
        string outputDirectory = Path.Combine(
            Environment.CurrentDirectory,
            "artifacts",
            "visual-inspection",
            DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture));
        int gpuRuns = 3;
        bool helpRequested = false;

        for (int index = 0; index < arguments.Length; index++)
        {
            switch (arguments[index])
            {
                case "--arch":
                    architecture = ReadValue(arguments, ref index, "--arch");
                    break;
                case "--input":
                    inputDirectory = ReadValue(arguments, ref index, "--input");
                    break;
                case "--output":
                    outputDirectory = ReadValue(arguments, ref index, "--output");
                    break;
                case "--gpu-runs":
                    gpuRuns = ParsePositive(ReadValue(arguments, ref index, "--gpu-runs"), "--gpu-runs");
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

        if (gpuRuns is < 1 or > 9)
        {
            throw new ArgumentException("--gpu-runs must be between 1 and 9.");
        }

        return new Options(
            architecture ?? string.Empty,
            Path.GetFullPath(inputDirectory),
            Path.GetFullPath(outputDirectory),
            gpuRuns,
            helpRequested);
    }

    internal static void PrintUsage()
    {
        Console.WriteLine("VisualInspection - deterministic GPU defect-mask showcase");
        Console.WriteLine("Usage: dotnet run -- --arch gfxNNNN [options]");
        Console.WriteLine("  --input PATH       Fixture asset directory (default: bundled assets)");
        Console.WriteLine("  --output PATH      Artifact directory");
        Console.WriteLine("  --gpu-runs N       Measured GPU runs per fixture (1-9, default: 3)");
        Console.WriteLine("  --help             Print this usage text");
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
}
