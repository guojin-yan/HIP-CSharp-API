[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$sampleRoot = Join-Path $repositoryRoot "samples/validation/HipManagedExpansionValidation"
$project = Join-Path $sampleRoot "HipManagedExpansionValidation.csproj"
$program = Join-Path $sampleRoot "Program.cs"
$resultModel = Join-Path $sampleRoot "ValidationResult.cs"

foreach ($path in @($project, $program, $resultModel, (Join-Path $repositoryRoot "docs/design/m8.7-linux-managed-expansion-validation.md"))) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "M8.7 validation input is missing: $path" }
}

$programText = Get-Content -Raw -LiteralPath $program
$modelText = Get-Content -Raw -LiteralPath $resultModel
$forbidden = @(
    'JYPPX.ROCm.HipSharp.LowLevel',
    'HipRuntimeNativeApi',
    'HipRtcNativeApi',
    'DllImport',
    'LibraryImport',
    'NativeLibrary',
    'System.Reflection',
    'DangerousGetHandle',
    'IntPtr'
)
foreach ($token in $forbidden) {
    if ($programText.Contains($token, [System.StringComparison]::Ordinal) -or $modelText.Contains($token, [System.StringComparison]::Ordinal)) {
        throw "M8.7 sample bypasses the public managed boundary with token: $token"
    }
}

foreach ($required in @(
    'm8.2-pitched-memory',
    'm8.3-memory-pool',
    'm8.4-explicit-graph',
    'm8.5-kernel-occupancy',
    'm8.6-module-globals',
    'GetGlobal<int>("validation_values")',
    'GetGlobal("validation_bytes")',
    'LaunchCooperative',
    'AddMemoryAllocation',
    'CreateMemoryPool',
    'Allocate3D<int>',
    'performanceClaim'
)) {
    if (-not ($programText + $modelText).Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "M8.7 sample is missing required public workflow marker: $required"
    }
}

$runArguments = @("run", "--project", $project, "--configuration", $Configuration)
if ($NoBuild) { $runArguments += "--no-build" }
$runArguments += @("--", "--self-test")
$output = @(& dotnet @runArguments)
if ($LASTEXITCODE -ne 0) { throw "M8.7 sample self-test failed with exit code $LASTEXITCODE." }
$jsonLine = @($output | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })[-1]
$summary = $jsonLine | ConvertFrom-Json
if ($summary.schemaVersion -ne 1 -or $summary.workload -ne "hip-managed-expansion" -or $summary.status -ne "passed") {
    throw "M8.7 self-test summary identity or status is invalid."
}
if ($summary.performanceClaim -ne $false -or $summary.failureIndex -ne -1 -or $summary.failureStage -ne "") {
    throw "M8.7 self-test summary failure/performance contract is invalid."
}
$expectedStages = @("m8.2-pitched-memory", "m8.3-memory-pool", "m8.4-explicit-graph", "m8.5-kernel-occupancy", "m8.6-module-globals")
$actualStages = @($summary.stages | ForEach-Object name)
if (($actualStages -join "|") -ne ($expectedStages -join "|")) { throw "M8.7 stage order is invalid." }
if (@($summary.skippedStages).Count -ne 1 -or $summary.skippedStages[0] -ne "m8.3-memory-pool") {
    throw "M8.7 controlled skip aggregation is invalid."
}
if (@($summary.stages | Where-Object { $_.status -eq "failed" }).Count -ne 0) { throw "M8.7 self-test contains a failed stage." }
if (@($summary.stages | Where-Object { $_.managedNegative -ne $true }).Count -ne 0) { throw "M8.7 self-test is missing a managed negative." }
if (@($summary.stages | Where-Object { [string]::IsNullOrWhiteSpace($_.capability) }).Count -ne 0) { throw "M8.7 self-test is missing a capability classification." }
if (@($summary.stages | Where-Object { $_.status -eq "passed" -and $_.iterations -lt 1 }).Count -ne 0) { throw "M8.7 self-test has an invalid executed-stage iteration count." }
if (@($summary.stages | Where-Object { $_.status -eq "skipped" -and $_.iterations -ne 0 }).Count -ne 0) { throw "M8.7 self-test has an invalid skipped-stage iteration count." }
if ($summary.comparisons -ne 4) { throw "M8.7 self-test comparison count is invalid." }

$failureArguments = @("run", "--project", $project, "--configuration", $Configuration)
if ($NoBuild) { $failureArguments += "--no-build" }
$failureArguments += @("--", "--self-test-failure")
$failureOutput = @(& dotnet @failureArguments 2>&1)
if ($LASTEXITCODE -eq 0) { throw "M8.7 failure-propagation self-test unexpectedly returned zero." }
$failureJson = @($failureOutput | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith("{", [System.StringComparison]::Ordinal) })[-1] | ConvertFrom-Json
if ($failureJson.status -ne "failed" -or $failureJson.failureStage -ne "m8.4-explicit-graph" -or $failureJson.failureIndex -ne 7) {
    throw "M8.7 failure-propagation summary is invalid."
}

foreach ($invalidArguments in @(
    @("--arch", "not-an-architecture", "--expected-commit", ("0" * 40)),
    @("--arch", "gfx1100", "--expected-commit", ("0" * 40), "--graph-launch-repeats", "2"),
    @("--unknown-option")
)) {
    $argumentTest = @("run", "--project", $project, "--configuration", $Configuration)
    if ($NoBuild) { $argumentTest += "--no-build" }
    $argumentTest += "--"
    $argumentTest += $invalidArguments
    $argumentOutput = @(& dotnet @argumentTest 2>&1)
    if ($LASTEXITCODE -eq 0) { throw "M8.7 invalid-argument test unexpectedly returned zero: $($invalidArguments -join ' ')" }
    $argumentJson = @($argumentOutput | Where-Object { $_ -is [string] -and $_.TrimStart().StartsWith("{", [System.StringComparison]::Ordinal) })[-1] | ConvertFrom-Json
    if ($argumentJson.status -ne "failed" -or $argumentJson.failureStage -ne "setup" -or $argumentJson.performanceClaim -ne $false) {
        throw "M8.7 invalid-argument summary is invalid: $($invalidArguments -join ' ')"
    }
}

Write-Host "Managed expansion validation contract passed: 5 ordered stages, controlled skip aggregation, failure propagation, argument rejection, no performance claim."
