using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

internal static class ValidationStatuses
{
    internal const string Passed = "passed";
    internal const string Skipped = "skipped";
    internal const string Failed = "failed";
}

internal sealed class ValidationSubtestResult
{
    internal ValidationSubtestResult(string name, string status, string detail)
    {
        Name = name;
        Status = status;
        Detail = detail;
    }

    public string Name { get; }

    public string Status { get; }

    public string Detail { get; }
}

internal sealed class ValidationStageResult
{
    internal ValidationStageResult(
        string name,
        string status,
        long comparisons,
        bool managedNegative,
        int iterations,
        string capability,
        int failureIndex,
        string detail,
        IReadOnlyList<ValidationSubtestResult>? subtests = null)
    {
        Name = name;
        Status = status;
        Comparisons = comparisons;
        ManagedNegative = managedNegative;
        Iterations = iterations;
        Capability = capability;
        FailureIndex = failureIndex;
        Detail = detail;
        Subtests = subtests ?? Array.Empty<ValidationSubtestResult>();
    }

    public string Name { get; }

    public string Status { get; }

    public long Comparisons { get; }

    public bool ManagedNegative { get; }

    public int Iterations { get; }

    public string Capability { get; }

    public int FailureIndex { get; }

    public string Detail { get; }

    public IReadOnlyList<ValidationSubtestResult> Subtests { get; }

    internal static ValidationStageResult Passed(
        string name,
        long comparisons,
        bool managedNegative,
        int iterations = 1,
        string capability = "required",
        string detail = "validated",
        IReadOnlyList<ValidationSubtestResult>? subtests = null) =>
        new(name, ValidationStatuses.Passed, comparisons, managedNegative, iterations, capability, -1, detail, subtests);

    internal static ValidationStageResult Skipped(string name, string detail, bool managedNegative = false, string capability = "not-supported") =>
        new(name, ValidationStatuses.Skipped, 0, managedNegative, 0, capability, -1, detail);

    internal static ValidationStageResult Failed(string name, int failureIndex, string detail) =>
        new(name, ValidationStatuses.Failed, 0, false, 0, "not-evaluated", failureIndex, detail);
}

internal sealed class ValidationSummary
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private readonly int _schemaVersion = 1;
    private readonly string _workload = "hip-managed-expansion";
    private readonly bool _performanceClaim;

    private ValidationSummary(
        string repositoryCommit,
        string environment,
        string architecture,
        string device,
        IReadOnlyList<ValidationStageResult> stages)
    {
        RepositoryCommit = repositoryCommit;
        Environment = environment;
        Architecture = architecture;
        Device = device;
        Stages = stages;
        _performanceClaim = false;
        ValidationStageResult? failed = stages.FirstOrDefault(stage => stage.Status == ValidationStatuses.Failed);
        Status = failed is null ? ValidationStatuses.Passed : ValidationStatuses.Failed;
        FailureStage = failed?.Name ?? string.Empty;
        FailureIndex = failed?.FailureIndex ?? -1;
        Comparisons = stages.Sum(stage => stage.Comparisons);
        SkippedStages = stages.Where(stage => stage.Status == ValidationStatuses.Skipped).Select(stage => stage.Name).ToArray();
    }

    public int SchemaVersion => _schemaVersion;

    public string Workload => _workload;

    public string RepositoryCommit { get; }

    public string Environment { get; }

    public string Architecture { get; }

    public string Device { get; }

    public string Status { get; }

    public string FailureStage { get; }

    public int FailureIndex { get; }

    public long Comparisons { get; }

    public bool PerformanceClaim => _performanceClaim;

    public IReadOnlyList<string> SkippedStages { get; }

    public IReadOnlyList<ValidationStageResult> Stages { get; }

    internal static ValidationSummary Create(
        string repositoryCommit,
        string environment,
        string architecture,
        string device,
        IReadOnlyList<ValidationStageResult> stages) =>
        new(repositoryCommit, environment, architecture, device, stages);

    internal string ToJson() => JsonSerializer.Serialize(this, SerializerOptions);
}
