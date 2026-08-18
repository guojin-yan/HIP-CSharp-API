using System;
using System.IO;
using System.Text.Json;

internal sealed class ResultSummary
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public int SchemaVersion { get; set; }

    public string Workload { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset TimestampUtc { get; set; }

    public string PerformanceScope { get; set; } = string.Empty;

    public string Profile { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int Steps { get; set; }

    public long CellUpdates { get; set; }

    public int CpuWorkers { get; set; }

    public double CpuMilliseconds { get; set; }

    public int GpuRuns { get; set; }

    public double GpuCompileMilliseconds { get; set; }

    public double GpuKernelMilliseconds { get; set; }

    public double GpuEndToEndMilliseconds { get; set; }

    public double ObservedEndToEndSpeedup { get; set; }

    public double GpuCellUpdatesPerSecond { get; set; }

    public string ExecutionMode { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public string DeviceName { get; set; } = string.Empty;

    public ulong DeviceMemoryBytes { get; set; }

    public string HipRuntimeVersion { get; set; } = string.Empty;

    public string HipDriverVersion { get; set; } = string.Empty;

    public string HipRtcVersion { get; set; } = string.Empty;

    public ulong CodeObjectBytes { get; set; }

    public string CodeObjectSha256 { get; set; } = string.Empty;

    public double MaximumAbsoluteError { get; set; }

    public double RootMeanSquareError { get; set; }

    public int NonFiniteValues { get; set; }

    public double MaximumAbsoluteErrorTolerance { get; set; }

    public double RootMeanSquareErrorTolerance { get; set; }

    public string? HeatmapPath { get; set; }

    internal void Write(string path)
    {
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }
}
