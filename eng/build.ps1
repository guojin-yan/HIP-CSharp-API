[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$Version,
    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$versionModule = Join-Path $PSScriptRoot "version.psm1"
Import-Module $versionModule -Force
$Version = Get-HipSharpVersion -Kind Core -Override $Version -RepositoryRoot $repositoryRoot
$solution = Join-Path $repositoryRoot "HipSharp.sln"
$frameworks = @(
    "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
    "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
)

Push-Location $repositoryRoot
try {
    & (Join-Path $PSScriptRoot "generate-interop.ps1") -Verify

    if (-not $NoRestore) {
        & dotnet restore $solution --locked-mode
        if ($LASTEXITCODE -ne 0) { throw "dotnet restore failed with exit code $LASTEXITCODE." }
    }

    & dotnet build $solution --configuration $Configuration --no-restore `
        -p:Version=$Version `
        -p:PackageVersion=$Version `
        -p:ContinuousIntegrationBuild=true
    if ($LASTEXITCODE -ne 0) { throw "dotnet build failed with exit code $LASTEXITCODE." }

    $missing = [System.Collections.Generic.List[string]]::new()
    foreach ($framework in $frameworks) {
        foreach ($file in @("JYPPX.ROCm.HIP.CSharp.API.dll", "JYPPX.ROCm.HIP.CSharp.API.xml")) {
            $path = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/bin/$Configuration/$framework/$file"
            if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
                $missing.Add("$framework/$file")
            }
        }
    }

    if ($missing.Count -ne 0) {
        throw "The multi-target build did not produce: $($missing -join ', ')"
    }

    Write-Host "Build verified: JYPPX.ROCm.HIP.CSharp.API DLL and XML documentation x 15 target frameworks ($Configuration)."
}
finally {
    Pop-Location
}
