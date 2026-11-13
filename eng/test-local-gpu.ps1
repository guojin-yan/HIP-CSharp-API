[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HipRoot,
    [Parameter(Mandatory = $true)]
    [string]$Architecture,
    [string]$OutputRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'build/local-gpu'),
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedOutput = [IO.Path]::GetFullPath($OutputRoot)
$sourcePrefix = [IO.Path]::GetFullPath($repositoryRoot).TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
if ($resolvedOutput -eq $repositoryRoot -or $resolvedOutput.StartsWith($sourcePrefix, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'OutputRoot must be outside the source repository.'
}
if (-not ($IsWindows -or $IsLinux)) { throw 'Local GPU validation supports Windows and Linux.' }
$resolvedHip = (Resolve-Path -LiteralPath $HipRoot).Path
$hipBin = Join-Path $resolvedHip 'bin'
$nativeLibrary = if ($IsWindows) { Join-Path $hipBin 'amdhip64_7.dll' } else { Join-Path $resolvedHip 'lib/libamdhip64.so.7' }
if (-not (Test-Path -LiteralPath $nativeLibrary -PathType Leaf)) { throw 'HIP Runtime library is missing.' }
if ($Architecture -notmatch '^gfx[0-9a-f]+$') { throw 'Architecture must be an explicit AMD gfx target.' }
$saved = @{}
foreach ($name in @('HIPSHARP_OUTPUT_ROOT', 'HIPSHARP_REPOSITORY_ROOT', 'HIP_PATH', 'ROCM_PATH', 'PATH', 'LD_LIBRARY_PATH')) { $saved[$name] = [Environment]::GetEnvironmentVariable($name) }
try {
    $env:HIPSHARP_OUTPUT_ROOT = $resolvedOutput
    $env:HIPSHARP_REPOSITORY_ROOT = $repositoryRoot
    $env:HIP_PATH = [IO.Path]::GetFullPath($HipRoot)
    $env:ROCM_PATH = $env:HIP_PATH
    $env:PATH = $hipBin + [IO.Path]::PathSeparator + $env:PATH
    if ($IsLinux) {
        $nativePaths = @((Join-Path $resolvedHip 'lib'), (Join-Path $resolvedHip 'lib/rocm_sysdeps/lib'), '/usr/lib/wsl/lib')
        $env:LD_LIBRARY_PATH = ($nativePaths -join ':') + $(if ($env:LD_LIBRARY_PATH) { ':' + $env:LD_LIBRARY_PATH } else { '' })
    }
    if (-not $NoBuild) { & (Join-Path $PSScriptRoot 'build.ps1') -Configuration Release }
    $platform = if ($IsWindows) { 'windows' } else { 'linux' }
    $evidence = Join-Path $resolvedOutput "artifacts/local-$platform"
    New-Item -ItemType Directory -Force -Path $evidence | Out-Null
    $workloads = @(
        @{ Name = 'EnvironmentAndDevice'; Arguments = @() },
        @{ Name = 'LinearMemoryCopy'; Arguments = @() },
        @{ Name = 'StreamAndEvent'; Arguments = @() },
        @{ Name = 'HipRtcVectorAdd'; Arguments = @('--arch', $Architecture, '--length', '1000', '--repeat', '20') },
        @{ Name = 'HipRtcVectorAdd'; Label = 'HipRtcNegativeCompile'; Arguments = @('--arch', $Architecture, '--negative-compile') },
        @{ Name = 'HipRtcProgramLinker'; Arguments = @($Architecture) },
        @{ Name = 'AsyncVectorAdd'; Arguments = @('--arch', $Architecture) },
        @{ Name = 'PinnedHostMemory'; Arguments = @() },
        @{ Name = 'PitchedMemory2D3D'; Arguments = @() },
        @{ Name = 'ManagedMemory'; Arguments = @() },
        @{ Name = 'AsyncAllocationAndMemoryPool'; Arguments = @() },
        @{ Name = 'VirtualMemory'; Arguments = @() },
        @{ Name = 'GraphCaptureReplay'; Arguments = @() },
        @{ Name = 'ExplicitGraphDag'; Arguments = @() },
        @{ Name = 'KernelOccupancy'; Arguments = @($Architecture) },
        @{ Name = 'ModuleGlobals'; Arguments = @($Architecture) },
        @{ Name = 'ArrayTextureSurface'; Arguments = @($Architecture) },
        @{ Name = 'HipManagedExpansionValidation'; Arguments = @('--arch', $Architecture, '--graph-launch-repeats', '3') }
    )
    $results = @()
    $expectedCommit = (& git -C $repositoryRoot rev-parse HEAD).Trim()
    if ($LASTEXITCODE -ne 0) { throw 'Cannot determine the local validation commit.' }
    $workloads[-1].Arguments += @('--expected-commit', $expectedCommit)
    foreach ($workload in $workloads) {
        $label = if ($workload.ContainsKey('Label')) { $workload.Label } else { $workload.Name }
        $assembly = Join-Path $resolvedOutput "bin/$($workload.Name)/Release/net10.0/$($workload.Name).dll"
        if (-not (Test-Path -LiteralPath $assembly -PathType Leaf)) { throw "Build output is missing: $assembly" }
        $arguments = $workload.Arguments
        $output = @(& dotnet $assembly @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        $output | Set-Content -LiteralPath (Join-Path $evidence "$label.log") -Encoding utf8
        $text = $output -join "`n"
        $results += [pscustomobject]@{ workload = $label; exitCode = $exitCode; status = $(if ($exitCode -ne 0) { 'failed' } elseif ($text -match '(?i)Skipped:') { 'skipped-capability' } else { 'passed' }) }
        Write-Host "$label : $($results[-1].status)"
    }
    [pscustomobject]@{ platform = $platform; architecture = $Architecture; hipRoot = $env:HIP_PATH; baseCommit = $expectedCommit; validationScope = 'local-development-worktree'; performanceClaim = $false; workloads = $results } |
        ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $evidence 'summary.json') -Encoding utf8
    if (@($results | Where-Object status -eq 'failed').Count -ne 0) { throw "Local GPU workloads failed; see $evidence" }
}
finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
}
