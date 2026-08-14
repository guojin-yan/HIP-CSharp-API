[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [switch]$Update
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
$coreVersion = Get-HipSharpVersion -Kind Core -RepositoryRoot $repositoryRoot
$tool = Join-Path $repositoryRoot "tools/JYPPX.ROCm.HipSharp.ApiSurface/JYPPX.ROCm.HipSharp.ApiSurface.csproj"
$categories = Join-Path $PSScriptRoot "public-api/categories.json"
$snapshot = Join-Path $PSScriptRoot "public-api/JYPPX.ROCm.HipSharp.$coreVersion.txt"
$frameworks = @(
    "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
    "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
)

function Invoke-Snapshot([string]$framework, [string]$target, [string]$mode) {
    $assembly = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/bin/$Configuration/$framework/JYPPX.ROCm.HIP.CSharp.API.dll"
    $xml = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/bin/$Configuration/$framework/JYPPX.ROCm.HIP.CSharp.API.xml"
    if (-not (Test-Path -LiteralPath $assembly -PathType Leaf) -or -not (Test-Path -LiteralPath $xml -PathType Leaf)) {
        throw "Public API input is missing for $framework. Build all target frameworks first."
    }
    & dotnet run --project $tool --configuration $Configuration --no-build --no-restore -- `
        --assembly $assembly --xml $xml --snapshot $target --categories $categories $mode
    if ($LASTEXITCODE -ne 0) { throw "Public API snapshot command failed for $framework." }
}

if ($Update) {
    Invoke-Snapshot "net10.0" $snapshot "--write"
}
if (-not (Test-Path -LiteralPath $snapshot -PathType Leaf)) {
    throw "The public API snapshot is missing: $snapshot"
}

Invoke-Snapshot "net10.0" $snapshot "--check"
$comparisonRoot = Join-Path $repositoryRoot "artifacts/public-api"
New-Item -ItemType Directory -Force -Path $comparisonRoot | Out-Null
foreach ($framework in $frameworks | Where-Object { $_ -ne "net10.0" }) {
    $candidate = Join-Path $comparisonRoot "$framework.txt"
    Invoke-Snapshot $framework $candidate "--write"
    if ((Get-Content -Raw -LiteralPath $candidate).Replace("`r`n", "`n") -ne (Get-Content -Raw -LiteralPath $snapshot).Replace("`r`n", "`n")) {
        throw "Public API differs between net10.0 and $framework."
    }
}

Write-Host "Public API compatibility passed: committed $coreVersion snapshot and identical surface across 15 TFMs."
