[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("generate", "extract-headers", "probe-manifest")]
    [string]$Command = "generate",
    [Alias("Verify")]
    [switch]$Check,
    [string]$HeaderRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot "eng/interop/interop-manifest.json"
$generatorProject = Join-Path $repositoryRoot "tools/JYPPX.ROCm.HipSharp.BindingGenerator/JYPPX.ROCm.HipSharp.BindingGenerator.csproj"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

function Test-Headers([string]$root) {
    if ([string]::IsNullOrWhiteSpace($root)) { return @() }
    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $rootPrefix = $resolvedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($header in @($manifest.verifiedHeaders)) {
        $resolvedPath = [System.IO.Path]::GetFullPath((Join-Path $resolvedRoot ([string]$header.path)))
        if (-not $resolvedPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Header path escapes the explicitly supplied HeaderRoot: $($header.path)"
        }
        if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
            throw "Official header is missing from the explicitly supplied HeaderRoot: $($header.path)"
        }
        $actual = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actual -ne ([string]$header.sha256).ToUpperInvariant()) {
            throw "Official header SHA-256 mismatch for $($header.path)."
        }
        $results.Add([pscustomobject]@{ path = [string]$header.path; sha256 = $actual })
    }
    return $results.ToArray()
}

if ($Command -eq "probe-manifest") {
    $probeJson = & dotnet run --project $generatorProject -- probe-manifest
    if ($LASTEXITCODE -ne 0) { throw "Binding generator probe failed with exit code $LASTEXITCODE." }
    $probe = $probeJson | ConvertFrom-Json
    $headerResults = @(Test-Headers $HeaderRoot)
    [ordered]@{
        schemaVersion = [int]$probe.schemaVersion
        generatorVersion = [string]$probe.generatorVersion
        rocmTag = [string]$manifest.rocmTag
        hipTag = [string]$manifest.hipTag
        normalizedManifestSha256 = [string]$probe.normalizedManifestSha256
        headerRootSupplied = -not [string]::IsNullOrWhiteSpace($HeaderRoot)
        headers = $headerResults
        functionCount = [int]$probe.functionCount
        completeRuntimeFunctionCount = [int]$probe.completeRuntimeFunctionCount
        completeRtcFunctionCount = [int]$probe.completeRtcFunctionCount
        libraries = @($probe.libraries)
    } | ConvertTo-Json -Depth 10
    exit 0
}

if (-not [string]::IsNullOrWhiteSpace($HeaderRoot)) {
    $null = @(Test-Headers $HeaderRoot)
    $extractArguments = [System.Collections.Generic.List[string]]::new()
    $extractArguments.Add("run")
    $extractArguments.Add("--project")
    $extractArguments.Add($generatorProject)
    $extractArguments.Add("--")
    $extractArguments.Add("extract-headers")
    $extractArguments.Add("--header-root")
    $extractArguments.Add([System.IO.Path]::GetFullPath($HeaderRoot))
    if ($Check) { $extractArguments.Add("--check") }
    & dotnet @extractArguments
    if ($LASTEXITCODE -ne 0) { throw "Official header extraction failed with exit code $LASTEXITCODE." }
    if ($Command -eq "extract-headers") { exit 0 }
}
elseif ($Command -eq "extract-headers") {
    throw "extract-headers requires -HeaderRoot."
}

$arguments = [System.Collections.Generic.List[string]]::new()
$arguments.Add("run")
$arguments.Add("--project")
$arguments.Add($generatorProject)
$arguments.Add("--")
$arguments.Add("generate")
if ($Check) { $arguments.Add("--check") }
& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Binding generator failed with exit code $LASTEXITCODE." }
