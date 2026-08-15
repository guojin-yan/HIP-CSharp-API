[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/JYPPX.ROCm.HipSharp.csproj"
$docfxConfig = Join-Path $repositoryRoot "docs/docfx.json"
$repositoryRootFull = [System.IO.Path]::GetFullPath($repositoryRoot).TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)

function Resolve-RepositoryOutputDirectory([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Documentation output must be a non-empty repository-relative path."
    }

    $resolved = [System.IO.Path]::GetFullPath((Join-Path $repositoryRootFull $RelativePath)).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $repositoryPrefix = $repositoryRootFull + [System.IO.Path]::DirectorySeparatorChar
    if ($resolved -eq $repositoryRootFull -or
        -not $resolved.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Documentation output escapes the repository root: $RelativePath"
    }

    return $resolved
}

function Remove-DocumentationOutput([string]$Path) {
    if ($Path -notin $script:allowedOutputs) {
        throw "Refusing to clean an unapproved documentation output: $Path"
    }
    if (Test-Path -LiteralPath $Path -PathType Leaf) {
        throw "Documentation output is a file, not a directory: $Path"
    }
    if (Test-Path -LiteralPath $Path -PathType Container) {
        Remove-Item -LiteralPath $Path -Recurse -Force
    }
    if (Test-Path -LiteralPath $Path) {
        throw "Documentation output cleanup did not complete: $Path"
    }
}

$apiOutput = Resolve-RepositoryOutputDirectory "docs/api"
$siteOutput = Resolve-RepositoryOutputDirectory "_site"
$script:allowedOutputs = @($apiOutput, $siteOutput)
$legacyNamespace = "JYPPX" + ".HipSharp"

Push-Location $repositoryRoot
try {
    foreach ($output in $script:allowedOutputs) {
        Remove-DocumentationOutput $output
    }

    & dotnet tool restore
    if ($LASTEXITCODE -ne 0) { throw "dotnet tool restore failed with exit code $LASTEXITCODE." }

    & dotnet build $project --configuration $Configuration --framework net10.0
    if ($LASTEXITCODE -ne 0) { throw "Documentation build prerequisite failed with exit code $LASTEXITCODE." }

    & dotnet docfx $docfxConfig
    if ($LASTEXITCODE -ne 0) { throw "DocFX failed with exit code $LASTEXITCODE." }

    $legacyApiFiles = @(Get-ChildItem -LiteralPath $apiOutput, (Join-Path $siteOutput "api") -File -Recurse |
        Where-Object { $_.Name -like "$legacyNamespace*" })
    if ($legacyApiFiles.Count -ne 0) {
        throw "Legacy API namespace pages remain after clean generation: $($legacyApiFiles.FullName -join ', ')"
    }

    $currentApiFiles = @(Get-ChildItem -LiteralPath $apiOutput -File |
        Where-Object { $_.Name -like "JYPPX.ROCm.HipSharp*" })
    if ($currentApiFiles.Count -eq 0) {
        throw "DocFX did not generate the current JYPPX.ROCm.HipSharp API pages."
    }

    $apiPageCount = @(Get-ChildItem -LiteralPath $apiOutput -File -Recurse).Count
    $sitePageCount = @(Get-ChildItem -LiteralPath $siteOutput -Filter "*.html" -File -Recurse).Count
    Write-Host "DocFX site generated under $siteOutput (metadata files=$apiPageCount; HTML pages=$sitePageCount; legacy API pages=0)."
}
catch {
    foreach ($output in $script:allowedOutputs) {
        Remove-DocumentationOutput $output
    }
    throw
}
finally {
    Pop-Location
}
