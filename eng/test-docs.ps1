[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$legacyNamespace = "JYPPX" + ".HipSharp"
$apiSentinel = Join-Path $repositoryRoot "docs/api/$legacyNamespace.LegacySentinel.yml"
$siteSentinel = Join-Path $repositoryRoot "_site/api/$legacyNamespace.LegacySentinel.html"

New-Item -ItemType Directory -Force -Path (Split-Path -Parent $apiSentinel), (Split-Path -Parent $siteSentinel) | Out-Null
[System.IO.File]::WriteAllText($apiSentinel, "legacy namespace sentinel")
[System.IO.File]::WriteAllText($siteSentinel, "legacy namespace sentinel")

& (Join-Path $PSScriptRoot "docs.ps1") -Configuration $Configuration
if ($LASTEXITCODE -ne 0) { throw "Documentation clean-generation test failed with exit code $LASTEXITCODE." }

if ((Test-Path -LiteralPath $apiSentinel) -or (Test-Path -LiteralPath $siteSentinel)) {
    throw "Documentation clean generation retained a legacy sentinel."
}

$legacyPages = @(Get-ChildItem -LiteralPath (Join-Path $repositoryRoot "docs/api"), (Join-Path $repositoryRoot "_site/api") -File -Recurse |
    Where-Object { $_.Name -like "$legacyNamespace*" })
if ($legacyPages.Count -ne 0) {
    throw "Documentation clean generation retained legacy namespace pages: $($legacyPages.FullName -join ', ')"
}

Write-Host "DocFX clean-generation regression passed: both sentinels removed and legacy API pages=0."
