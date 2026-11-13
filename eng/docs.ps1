[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "build-paths.psm1") -Force
$env:HIPSHARP_REPOSITORY_ROOT = $repositoryRoot
$project = Join-Path $repositoryRoot "src/JYPPX.ROCm.HipSharp/JYPPX.ROCm.HipSharp.csproj"
$docfxConfig = Join-Path $repositoryRoot "docs/docfx.json"
$outputRootFull = (Get-HipSharpOutputPath $repositoryRoot ".").TrimEnd(
    [System.IO.Path]::DirectorySeparatorChar,
    [System.IO.Path]::AltDirectorySeparatorChar)

function Resolve-RepositoryOutputDirectory([string]$RelativePath) {
    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "Documentation output must be a non-empty repository-relative path."
    }

    $resolved = [System.IO.Path]::GetFullPath((Join-Path $outputRootFull $RelativePath)).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $repositoryPrefix = $outputRootFull + [System.IO.Path]::DirectorySeparatorChar
    if ($resolved -eq $outputRootFull -or
        -not $resolved.StartsWith($repositoryPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Documentation output escapes the configured output root: $RelativePath"
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
$stagedDocs = Resolve-RepositoryOutputDirectory "artifacts/docfx/content"
$script:allowedOutputs = @($apiOutput, $siteOutput, $stagedDocs)
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

    # Keep the tracked config immutable; all generated config/cache/API/site files
    # follow HIPSHARP_OUTPUT_ROOT, including DocFX's relative intermediate paths.
    $configOutput = Resolve-RepositoryOutputDirectory "artifacts/docfx"
    New-Item -ItemType Directory -Force -Path $configOutput | Out-Null
    $config = Get-Content -LiteralPath $docfxConfig -Raw | ConvertFrom-Json
    New-Item -ItemType Directory -Force -Path $stagedDocs | Out-Null
    $docsRoot = Join-Path $repositoryRoot "docs"
    foreach ($file in Get-ChildItem -LiteralPath $docsRoot -File -Recurse) {
        $relative = [IO.Path]::GetRelativePath($docsRoot, $file.FullName)
        if ($relative -match '^api[/\\]') { continue }
        $target = Join-Path $stagedDocs $relative
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $target) | Out-Null
        Copy-Item -LiteralPath $file.FullName -Destination $target
    }
    $config.metadata[0].src[0].src = [IO.Path]::GetRelativePath($configOutput, (Split-Path -Parent $project))
    $config.metadata[0].dest = $apiOutput
    $config.build.content[0].src = [IO.Path]::GetRelativePath($configOutput, (Join-Path $stagedDocs "api"))
    $config.build.content[1] | Add-Member -NotePropertyName src -NotePropertyValue ([IO.Path]::GetRelativePath($configOutput, $stagedDocs)) -Force
    $config.build.dest = $siteOutput
    $generatedConfig = Join-Path $configOutput "docfx.json"
    $config | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $generatedConfig -Encoding utf8
    & dotnet docfx metadata $generatedConfig
    if ($LASTEXITCODE -ne 0) { throw "DocFX metadata failed with exit code $LASTEXITCODE." }
    Copy-Item -LiteralPath $apiOutput -Destination (Join-Path $stagedDocs "api") -Recurse
    & dotnet docfx build $generatedConfig --warningsAsErrors
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
