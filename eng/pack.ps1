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
$project = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/JYPPX.ROCm.HipSharp.csproj"
$outputPath = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}

New-Item -ItemType Directory -Force -Path $outputPath | Out-Null
$package = Join-Path $outputPath "JYPPX.ROCm.HIP.CSharp.API.$Version.nupkg"

Push-Location $repositoryRoot
try {
    if (-not $NoBuild) {
        & (Join-Path $PSScriptRoot "build.ps1") -Configuration $Configuration -Version $Version
    }

    $repositoryCommit = (& git rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($repositoryCommit)) {
        $repositoryCommit = "0000000000000000000000000000000000000000"
    }

    if (Test-Path -LiteralPath $package -PathType Leaf) {
        [System.IO.File]::Delete($package)
    }

    & dotnet pack $project `
        --configuration $Configuration `
        --no-build `
        --output $outputPath `
        -p:Version=$Version `
        -p:PackageVersion=$Version `
        -p:RepositoryCommit=$repositoryCommit `
        -p:RepositoryBranch=main
    if ($LASTEXITCODE -ne 0) { throw "dotnet pack failed with exit code $LASTEXITCODE." }

    if (-not (Test-Path -LiteralPath $package -PathType Leaf)) {
        throw "Expected package was not generated: $package"
    }

    Write-Host "Core package generated: $package"
    Write-Output $package
}
finally {
    Pop-Location
}
