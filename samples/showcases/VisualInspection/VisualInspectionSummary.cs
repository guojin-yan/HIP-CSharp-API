using System;
using System.Collections.Generic;

internal sealed class VisualInspectionSummary
{
    public int SchemaVersion { get; set; }
    public string Workload { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset TimestampUtc { get; set; }
    public string Architecture { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public ulong DeviceMemoryBytes { get; set; }
    public string HipRtcVersion { get; set; } = string.Empty;
    public ulong CodeObjectBytes { get; set; }
    public string CodeObjectSha256 { get; set; } = string.Empty;
    public double HipRtcCompileMilliseconds { get; set; }
    public double CpuMilliseconds { get; set; }
    public int GpuRunsPerFixture { get; set; }
    public double GpuKernelMilliseconds { get; set; }
    public double GpuEndToEndMilliseconds { get; set; }
    public string ExecutionMode { get; set; } = string.Empty;
    public List<VisualFixtureResult> Fixtures { get; set; } = new();
}

internal sealed class VisualFixtureResult
{
    public string Id { get; set; } = string.Empty;
    public string DefectType { get; set; } = string.Empty;
    public string ExpectedDecision { get; set; } = string.Empty;
    public string ActualDecision { get; set; } = string.Empty;
    public int ExpectedDefectPixels { get; set; }
    public int CpuDefectPixels { get; set; }
    public int GpuDefectPixels { get; set; }
    public double IntersectionOverUnion { get; set; }
    public int MaximumByteDifference { get; set; }
    public bool Passed { get; set; }
    public string MaskPath { get; set; } = string.Empty;
}
