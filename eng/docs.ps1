[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src/JYPPX.HipSharp/JYPPX.HipSharp.csproj"
$docfxConfig = Join-Path $repositoryRoot "docs/docfx.json"

Push-Location $repositoryRoot
try {
    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }

    & dotnet build $project --configuration $Configuration --framework net10.0
    if ($LASTEXITCODE -ne 0) { throw "Documentation build prerequisite failed with exit code $LASTEXITCODE." }

    & dotnet docfx $docfxConfig
    if ($LASTEXITCODE -ne 0) { throw "DocFX failed with exit code $LASTEXITCODE." }

    Write-Host "DocFX site generated under $repositoryRoot/_site."
}
finally {
    Pop-Location
}
