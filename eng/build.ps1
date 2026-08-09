[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version = "0.0.0-preview.1",
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$solution = Join-Path $repositoryRoot "JYPPX.HipSharp.sln"
$frameworks = @(
    "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
    "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
)

Push-Location $repositoryRoot
try {
    if (-not $NoRestore) {
        & dotnet restore $solution
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
    }

    & dotnet build $solution --configuration $Configuration --no-restore -p:PackageVersion=$Version
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($framework in $frameworks) {
        foreach ($assembly in @("JYPPX.HipSharp.Native", "JYPPX.HipSharp")) {
            $path = Join-Path $repositoryRoot "src/$assembly/bin/$Configuration/$framework/$assembly.dll"
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                $missing.Add("$assembly/$framework")
            }
        }
    }

    if ($missing.Count -ne 0) {
        throw "The multi-target build did not produce: $($missing -join ', ')"
    }

    Write-Host "Build verified: 2 assemblies x 15 target frameworks ($Configuration)."
}
finally {
    Pop-Location
}
