[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64",
    [switch]$RequirePackable,
    [string]$CandidateAttestation,
    [string]$CandidateAttestationSha256,
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

if ($RequirePackable -and -not [string]::IsNullOrWhiteSpace($CandidateAttestation)) {
    throw "HIPSHARP1001: Candidate and final-package validation modes are mutually exclusive."
}
if ([string]::IsNullOrWhiteSpace($CandidateAttestation) -ne [string]::IsNullOrWhiteSpace($CandidateAttestationSha256)) {
    throw "HIPSHARP1001: Candidate attestation path and SHA-256 must be supplied together."
}

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

if (-not [string]::IsNullOrWhiteSpace($CandidateAttestation)) {
    if ($SkipStaging) { throw "HIPSHARP1001: Candidate attestation validation cannot skip staging." }
    if ($runtimeManifest.packEnabled -or $runtimeManifest.verified -or
        $runtimeManifest.verification.packageAuditVerified -or $runtimeManifest.verification.gpuValidated) {
        throw "HIPSHARP1001: A candidate package must retain an explicitly unverified manifest."
    }
    $artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
    $attestationPath = if ([System.IO.Path]::IsPathRooted($CandidateAttestation)) { [System.IO.Path]::GetFullPath($CandidateAttestation) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $CandidateAttestation)) }
    if (-not $attestationPath.StartsWith($artifactsRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "HIPSHARP1001: Candidate attestation must remain under the ignored artifacts directory."
    }
    if (-not (Test-Path -LiteralPath $attestationPath -PathType Leaf)) { throw "HIPSHARP1001: Candidate attestation is missing." }
    Assert-HipSharpHash $CandidateAttestationSha256 "candidate attestation SHA-256"
    if ((Get-HipSharpSha256 $attestationPath) -ne $CandidateAttestationSha256) { throw "HIPSHARP1001: Candidate attestation hash mismatch." }
    $attestation = Get-Content -Raw -LiteralPath $attestationPath | ConvertFrom-Json -AsHashtable
    foreach ($name in @("schemaVersion", "mode", "publishable", "gitSha", "packageId", "packageVersion", "rid", "manifestSha256", "sbomSha256", "stagingDigestSha256")) {
        if (-not $attestation.ContainsKey($name)) { throw "HIPSHARP1001: Candidate attestation is missing '$name'." }
    }
    if ($attestation.schemaVersion -ne 1 -or $attestation.mode -ne "isolated-gpu-candidate" -or $attestation.publishable) {
        throw "HIPSHARP1001: Candidate attestation mode is invalid."
    }
    if ([string]$attestation.gitSha -notmatch "^[0-9a-f]{40}$") { throw "HIPSHARP1001: Candidate attestation gitSha must be a lowercase 40-character Git SHA." }
    foreach ($name in @("manifestSha256", "sbomSha256", "stagingDigestSha256")) { Assert-HipSharpHash ([string]$attestation[$name]) "candidate attestation $name" }
    if ($attestation.packageId -ne $runtimeManifest.packageId -or $attestation.packageVersion -ne $runtimeManifest.packageVersion -or $attestation.rid -ne $runtimeManifest.rid) {
        throw "HIPSHARP1001: Candidate attestation package identity does not match the manifest."
    }
    if ($attestation.manifestSha256 -ne (Get-HipSharpSha256 $manifestInfo.Path) -or
        $attestation.sbomSha256 -ne $runtimeManifest.sbom.sha256 -or
        $attestation.stagingDigestSha256 -ne (Get-HipSharpStagingDigest $stagingRoot)) {
        throw "HIPSHARP1001: Candidate attestation does not bind the current manifest, SBOM, and staging content."
    }
    $gitSha = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
    if ($LASTEXITCODE -ne 0 -or $gitSha -ne $attestation.gitSha) { throw "HIPSHARP1001: Candidate attestation does not bind the current Git SHA." }
    $gitStatus = @(& git -C $repositoryRoot status --porcelain=v1)
    if ($LASTEXITCODE -ne 0 -or $gitStatus.Count -ne 0) { throw "HIPSHARP1001: Candidate packaging requires a clean Git worktree." }
}

Write-Host "Runtime manifest validation passed for $($runtimeManifest.packageId)."
