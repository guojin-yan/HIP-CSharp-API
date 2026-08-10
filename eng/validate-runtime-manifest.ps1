[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64",
    [switch]$RequirePackable,
    [switch]$SkipStaging
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repositoryRoot $Manifest }
$manifestInfo = Get-HipSharpRuntimeManifest $manifestPath
$runtimeManifest = $manifestInfo.Value
Assert-HipSharpRuntimeManifest $runtimeManifest -RequirePackable:$RequirePackable

if (-not $SkipStaging -and $runtimeManifest.rid -eq "linux-x64") {
    $stagingRoot = if ([System.IO.Path]::IsPathRooted($StagingDirectory)) { [System.IO.Path]::GetFullPath($StagingDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StagingDirectory)) }
    if (-not $stagingRoot.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Runtime staging path must remain under the repository root." }
    if (-not (Test-Path -LiteralPath $stagingRoot -PathType Container)) { throw "Runtime staging directory is missing: $stagingRoot" }
    $allowed = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in @($runtimeManifest.files)) {
        $relative = ConvertTo-HipSharpRelativePath $file.path
        $allowed.Add($relative) | Out-Null
        $path = Join-Path $stagingRoot ($relative.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { throw "Staging is missing $relative." }
        if ((Get-HipSharpSha256 $path) -ne $file.sha256 -or (Get-Item -LiteralPath $path).Length -ne [int64]$file.size) { throw "Staging hash/size mismatch: $relative" }
    }
    foreach ($license in @($runtimeManifest.licenses)) {
        $relative = ConvertTo-HipSharpRelativePath $license.packagePath
        $allowed.Add($relative) | Out-Null
        $path = Join-Path $stagingRoot ($relative.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
        if (-not (Test-Path -LiteralPath $path -PathType Leaf) -or (Get-HipSharpSha256 $path) -ne $license.sha256) { throw "Staging license is missing or changed: $relative" }
    }
    foreach ($relative in @("runtime-manifest.json", $runtimeManifest.sbom.path)) { $allowed.Add((ConvertTo-HipSharpRelativePath $relative)) | Out-Null }
    $actual = @(Get-ChildItem -LiteralPath $stagingRoot -File -Recurse | ForEach-Object { $_.FullName.Substring($stagingRoot.Length + 1).Replace("\", "/") })
    $unexpected = @($actual | Where-Object { -not $allowed.Contains($_) })
    if ($unexpected.Count -gt 0) { throw "Staging contains files outside the manifest allowlist: $($unexpected -join ', ')" }
    if ($actual.Count -ne $allowed.Count) { throw "Staging allowlist and actual file count differ." }
    $stagedManifest = Join-Path $stagingRoot "runtime-manifest.json"
    if ((Get-HipSharpSha256 $stagedManifest) -ne (Get-HipSharpSha256 $manifestInfo.Path)) { throw "Staged runtime manifest is stale." }
    $sbom = Join-Path $stagingRoot $runtimeManifest.sbom.path
    if ((Get-HipSharpSha256 $sbom) -ne $runtimeManifest.sbom.sha256) { throw "Staged SBOM hash mismatch." }
}

Write-Host "Runtime manifest validation passed for $($runtimeManifest.packageId)."
