[CmdletBinding()]
param([string]$Manifest = "nuget/runtime-manifests/linux-x64.json")

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repositoryRoot $Manifest }

function New-ManifestCopy {
    return (Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable)
}

function Assert-Rejected([string]$name, [scriptblock]$mutation) {
    $candidate = New-ManifestCopy
    & $mutation $candidate
    try {
        Assert-HipSharpRuntimeManifest $candidate
        throw "Negative runtime manifest test unexpectedly passed: $name"
    } catch {
        if ($_.Exception.Message -like "Negative runtime manifest test unexpectedly passed:*") { throw }
        Write-Host "Rejected as expected: $name"
    }
}

function Assert-RejectedPackable([string]$testName, [scriptblock]$mutation) {
    $candidate = New-ManifestCopy
    $candidate.packEnabled = $true
    $candidate.verified = $true
    $candidate.verification.packageAuditVerified = $true
    $candidate.verification.gpuValidated = $true
    $candidate.verification.validationSha256 = ("11" * 32)
    $candidate.verification.promotionReceipt = [ordered]@{ path = "nuget/runtime-manifests/linux-x64.promotion-receipt.json"; sha256 = ("22" * 32); lockPath = "eng/promotion/m8.7-promotion-lock.json" }
    & $mutation $candidate
    try {
        Assert-HipSharpRuntimeManifest $candidate -RequirePackable
        throw "Negative pack guard test unexpectedly passed: $testName"
    } catch {
        if ($_.Exception.Message -like "Negative pack guard test unexpectedly passed:*") { throw }
        Write-Host "Rejected as expected: $testName"
    }
}

$baseline = New-ManifestCopy
Assert-HipSharpRuntimeManifest $baseline
if ($baseline.packEnabled -or $baseline.verified) {
    throw "The current Runtime manifest must remain unverified until the new package-family identity passes a fresh exact gate."
}
Write-Host "Forward-fix Runtime manifest is intentionally unverified and non-packable."
& (Join-Path $PSScriptRoot "generate-runtime-metadata.ps1") -Manifest $manifestPath -Check

Assert-Rejected "wrong architecture" { param($m) $m.packages[0].architecture = "arm64" }
Assert-Rejected "staging path escape" { param($m) $m.files[0].path = "../libamd_comgr.so.3" }
Assert-Rejected "forbidden header" { param($m) $m.files[0].path = "runtimes/linux-x64/native/hip_runtime.h" }
Assert-Rejected "invalid file hash" { param($m) $m.files[0].sha256 = "00" }
Assert-Rejected "missing ELF dependency" { param($m) $m.files[0].needed += "libundeclared.so.1" }
Assert-Rejected "duplicate SONAME" { param($m) (@($m.files | Where-Object { -not $_.ContainsKey("aliasFor") }))[1].soname = $m.files[0].soname }
Assert-Rejected "missing component license" { param($m) $m.licenses = @($m.licenses | Where-Object sourcePackage -ne "comgr-rpath7.2.1") }
Assert-Rejected "missing driver boundary" { param($m) $m.driverBoundary.deviceNodes = @() }
Assert-Rejected "invalid SBOM hash" { param($m) $m.sbom.sha256 = "not-a-hash" }
Assert-Rejected "ROCm package dependency cycle" { param($m) (@($m.packages | Where-Object name -eq "rocm-core-rpath7.2.1"))[0].depends += "hip-runtime-amd-rpath7.2.1" }
Assert-RejectedPackable "unverified runtime flags" { param($m) $m.verification.validationSha256 = $null }
Assert-RejectedPackable "missing promotion receipt" { param($m) $m.verification.Remove("promotionReceipt") }
Assert-RejectedPackable "promotion receipt hash mismatch" { param($m) $m.verification.promotionReceipt.sha256 = ("00" * 32) }
Assert-RejectedPackable "oversized runtime package" { param($m) $m.size.packageBytes = $m.size.nugetLimitBytes }

$windowsManifest = Get-HipSharpRuntimeManifest (Join-Path $repositoryRoot "nuget/runtime-manifests/win-x64.json")
Assert-HipSharpRuntimeManifest $windowsManifest.Value
try {
    Assert-HipSharpRuntimeManifest $windowsManifest.Value -RequirePackable
    throw "Negative pack guard test unexpectedly passed: Windows runtime manifest"
} catch {
    if ($_.Exception.Message -like "Negative pack guard test unexpectedly passed:*") { throw }
    Write-Host "Rejected as expected: Windows runtime manifest"
}

$digestRoot = Join-Path $repositoryRoot "artifacts/runtime-staging-digest-test"
if (Test-Path -LiteralPath $digestRoot) { Remove-Item -LiteralPath $digestRoot -Recurse -Force }
try {
    New-Item -ItemType Directory -Force -Path (Join-Path $digestRoot "nested") | Out-Null
    [System.IO.File]::WriteAllText((Join-Path $digestRoot "a.txt"), "alpha`n")
    [System.IO.File]::WriteAllText((Join-Path $digestRoot "nested/b.txt"), "beta`n")
    $firstDigest = Get-HipSharpStagingDigest $digestRoot
    $secondDigest = Get-HipSharpStagingDigest $digestRoot
    if ($firstDigest -ne $secondDigest) { throw "Runtime staging digest is not deterministic." }
    [System.IO.File]::WriteAllText((Join-Path $digestRoot "nested/b.txt"), "tampered`n")
    if ($firstDigest -eq (Get-HipSharpStagingDigest $digestRoot)) { throw "Runtime staging digest did not detect tampering." }
    Write-Host "Runtime staging digest determinism/tamper test passed."
} finally {
    if (Test-Path -LiteralPath $digestRoot) { Remove-Item -LiteralPath $digestRoot -Recurse -Force }
}

Write-Host "Runtime supply-chain structural tests passed."
