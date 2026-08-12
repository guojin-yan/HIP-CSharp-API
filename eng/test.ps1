[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version,
    [string]$OutputDirectory = "artifacts/packages",
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
$Version = Get-HipSharpVersion -Kind Core -Override $Version -RepositoryRoot $repositoryRoot
$solution = Join-Path $repositoryRoot "HipSharp.sln"
$resultsDirectory = Join-Path $repositoryRoot "artifacts/test-results"

if (-not $NoBuild) {
    & (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration -Version $Version
}

& (Join-Path $PSScriptRoot "pack.ps1") `
    -Configuration $Configuration `
    -Version $Version `
    -OutputDirectory $OutputDirectory `
    -NoBuild | Out-Host

$packageDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$packagePath = Join-Path $packageDirectory "JYPPX.HIP.CSharp.API.$Version.nupkg"
$previousPackagePath = $env:HIPSHARP_PACKAGE_PATH
$env:HIPSHARP_PACKAGE_PATH = $packagePath

New-Item -ItemType Directory -Force -Path $resultsDirectory | Out-Null
try {
    & dotnet test $solution `
        --configuration $Configuration `
        --no-build `
        --no-restore `
        --results-directory $resultsDirectory `
        --logger "trx;LogFilePrefix=hipsharp"
    if ($LASTEXITCODE -ne 0) { throw "dotnet test failed with exit code $LASTEXITCODE." }
}
finally {
    $env:HIPSHARP_PACKAGE_PATH = $previousPackagePath
}

& (Join-Path $PSScriptRoot "verify-public-api.ps1") -Configuration $Configuration
& (Join-Path $PSScriptRoot "verify-managed-expansion.ps1") -Configuration $Configuration -NoBuild
& (Join-Path $PSScriptRoot "verify-package.ps1") -PackagePath $packagePath -Configuration $Configuration -ExpectedVersion $Version
Write-Host "Tests, managed expansion contract, package audit, and clean consumer builds passed."
