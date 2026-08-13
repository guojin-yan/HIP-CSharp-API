[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CorePackage,
    [Parameter(Mandatory = $true)][string]$RuntimePackage,
    [Parameter(Mandatory = $true)][string]$CoreAudit,
    [Parameter(Mandatory = $true)][string]$RuntimeAudit,
    [Parameter(Mandatory = $true)][string]$PayloadEquivalence,
    [string]$PromotionReceipt = "nuget/runtime-manifests/linux-x64.promotion-receipt.json",
    [string]$Manifest = "nuget/runtime-manifests/linux-x64.json",
    [string]$Sbom = "nuget/runtime-manifests/linux-x64.cdx.json",
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64",
    [string]$Output = "artifacts/release-envelope/m8.8-linux-0.9.0.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force

function Resolve-File([string]$Value) {
    $path = if ([System.IO.Path]::IsPathRooted($Value)) { $Value } else { Join-Path $repositoryRoot $Value }
    return (Resolve-Path -LiteralPath $path).Path
}

function File-Identity([string]$Value) {
    $path = Resolve-File $Value
    return [ordered]@{ file = [System.IO.Path]::GetFileName($path); size = (Get-Item -LiteralPath $path).Length; sha256 = Get-HipSharpSha256 $path }
}

$gitSha = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitSha -notmatch '^[0-9a-f]{40}$') { throw "HIPSHARP1001: A lowercase 40-character final Git SHA is required." }
$gitStatus = @(& git -C $repositoryRoot status --porcelain=v1)
if ($LASTEXITCODE -ne 0 -or $gitStatus.Count -ne 0) { throw "HIPSHARP1001: Release envelope generation requires a clean Git worktree." }

$coreAuditPath = Resolve-File $CoreAudit
$runtimeAuditPath = Resolve-File $RuntimeAudit
$diffPath = Resolve-File $PayloadEquivalence
$coreAuditValue = Get-Content -Raw -LiteralPath $coreAuditPath | ConvertFrom-Json -AsHashtable
$runtimeAuditValue = Get-Content -Raw -LiteralPath $runtimeAuditPath | ConvertFrom-Json -AsHashtable
$diffValue = Get-Content -Raw -LiteralPath $diffPath | ConvertFrom-Json -AsHashtable

$coreIdentity = File-Identity $CorePackage
$runtimeIdentity = File-Identity $RuntimePackage
if ([string]$coreAuditValue.repositoryCommit -ne $gitSha -or [string]$coreAuditValue.sha256 -ne $coreIdentity.sha256.ToUpperInvariant()) {
    throw "HIPSHARP1001: Core audit does not bind the final SHA and package."
}
if ([string]$runtimeAuditValue.packageRepositoryCommit -ne $gitSha -or [string]$runtimeAuditValue.sha256 -ne $runtimeIdentity.sha256 -or
    [string]$runtimeAuditValue.mode -ne "verified-final" -or $runtimeAuditValue.publishable -or $runtimeAuditValue.releaseAuthorized) {
    throw "HIPSHARP1001: Runtime audit does not bind a non-publishable verified-final package."
}
if ([string]$diffValue.status -ne "passed" -or -not $diffValue.allowedChangesOnly -or
    [string]$diffValue.core.finalSha256 -ne $coreIdentity.sha256 -or [string]$diffValue.runtime.finalSha256 -ne $runtimeIdentity.sha256) {
    throw "HIPSHARP1001: Payload equivalence does not bind the final packages."
}

$stagingPath = if ([System.IO.Path]::IsPathRooted($StagingDirectory)) { [System.IO.Path]::GetFullPath($StagingDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StagingDirectory)) }
$envelope = [ordered]@{
    schemaVersion = 1
    stage = "m8.8-linux-0.9.0"
    status = "blocked-pending-owner-authorized-final-exact-package-gate"
    finalGitCommit = $gitSha
    packages = [ordered]@{
        core = [ordered]@{ id = "JYPPX.ROCm.HIP.CSharp.API"; version = "0.9.0"; size = $coreIdentity.size; sha256 = $coreIdentity.sha256; repositoryCommit = $gitSha }
        runtime = [ordered]@{ id = "JYPPX.ROCm.HipSharp.Runtime.linux-x64"; version = "7.2.1"; size = $runtimeIdentity.size; sha256 = $runtimeIdentity.sha256; repositoryCommit = $gitSha; mode = "verified-final" }
    }
    evidence = [ordered]@{
        promotionReceipt = File-Identity $PromotionReceipt
        sourceManifest = File-Identity $Manifest
        sbom = File-Identity $Sbom
        coreAudit = File-Identity $coreAuditPath
        runtimeAudit = File-Identity $runtimeAuditPath
        payloadEquivalence = File-Identity $diffPath
        stagingDigestSha256 = Get-HipSharpStagingDigest $stagingPath
    }
    finalExactPackageGate = [ordered]@{
        officialHost = "pending-owner-authorization"
        packageOnlyPRoot = "pending-owner-authorization"
        symbols = "pending-final-exact-package-gate"
        abiSchema = 7
        managedExpansionComparisons = 1127
        reliability = "pending-final-exact-package-gate"
        p2p = "run-or-honest-capability-skip"
        isolatedNegatives = @("missingHsa", "coreOnly", "tamperedPackage", "mixedRuntimeHiprtc")
        performanceClaim = $false
    }
    boundaries = [ordered]@{
        testedCandidateEnvironment = "Ubuntu 24.04.4; ROCm 7.2.1; HIP 7.2; x86_64; gfx1100; single GPU"
        windows = "disabled/unverified/static-only"
        pushed = $false
        tagged = $false
        published = $false
        publishable = $false
        releaseAuthorized = $false
    }
}
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Output)) }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$json = (($envelope | ConvertTo-Json -Depth 20) -replace "`r`n", "`n") + "`n"
[System.IO.File]::WriteAllText($outputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Deterministic M8.8 release envelope written: $outputPath"
Write-Output $outputPath
