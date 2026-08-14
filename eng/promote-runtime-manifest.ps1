[CmdletBinding()]
param(
    [string]$LockFile = "eng/promotion/m8.7-promotion-lock.json",
    [string]$Manifest = "nuget/runtime-manifests/linux-x64.json",
    [string]$Receipt = "nuget/runtime-manifests/linux-x64.promotion-receipt.json",
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force

function Resolve-RepositoryPath([string]$Value) {
    if ([System.IO.Path]::IsPathRooted($Value)) { return [System.IO.Path]::GetFullPath($Value) }
    return [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Value))
}

$lockPath = Resolve-RepositoryPath $LockFile
$manifestPath = Resolve-RepositoryPath $Manifest
$receiptPath = Resolve-RepositoryPath $Receipt
$lock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json -AsHashtable

if (-not (Test-Path -LiteralPath $receiptPath -PathType Leaf)) {
    throw "HIPSHARP1001: The tracked promotion receipt is missing."
}
& (Join-Path $PSScriptRoot "verify-promotion.ps1") -LockFile $lockPath -ExpectedReceipt $receiptPath
if ($LASTEXITCODE -ne 0) { throw "HIPSHARP1001: M8.7 promotion evidence did not reproduce the tracked receipt." }

$receiptHash = Get-HipSharpSha256 $receiptPath
$manifestValue = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
$verification = $manifestValue["verification"]
$isPromoted = $manifestValue["packEnabled"] -and $manifestValue["verified"] -and
    $verification["packageAuditVerified"] -and $verification["gpuValidated"]

if (-not $isPromoted) {
    if ($Check) { throw "HIPSHARP1001: The Linux runtime manifest has not been promoted." }
    if ((Get-HipSharpSha256 $manifestPath) -ne $lock["inputs"]["sourceManifest"]["sha256"]) {
        throw "HIPSHARP1001: The source manifest no longer matches the immutable M8.7 promotion input."
    }

    $manifestValue["packEnabled"] = $true
    $manifestValue["verified"] = $true
    $verification["packageAuditVerified"] = $true
    $verification["gpuValidated"] = $true
    $verification["validationSha256"] = $receiptHash
    $verification["environment"] = [ordered]@{
        os = "Ubuntu 24.04.4"
        architecture = "x86_64"
        gpu = "gfx1100"
        isolation = "official-host + PRoot package-only"
    }
    $verification["promotionReceipt"] = [ordered]@{
        path = "nuget/runtime-manifests/linux-x64.promotion-receipt.json"
        sha256 = $receiptHash
        lockPath = "eng/promotion/m8.7-promotion-lock.json"
    }
    $verification["reason"] = "M8.7 validated the exact JYPPX.ROCm 0.9.0/7.2.1 candidate through official-host and PRoot package-only paths. The deterministic promotion receipt binds the package audits, symbol and ABI completeness, 1,127 managed comparisons, reliability run, four fail-closed negatives, and the unchanged native payload. Publication remains unauthorized."
    $manifestValue["size"]["topology"] = "single-package"
    $manifestValue["size"]["decision"] = "The receipt-locked single runtime package remains below the 262144000-byte gate. Component splitting remains rejected because HIP, HSA, and COMGR share one lockstep ROCm release and loader closure; every final nupkg must still pass the exact-package audit and payload-equivalence gate."
    $manifestJson = (($manifestValue | ConvertTo-Json -Depth 30) -replace "`r?`n", "`r`n") + "`r`n"
    [System.IO.File]::WriteAllText($manifestPath, $manifestJson, [System.Text.UTF8Encoding]::new($false))
    $manifestValue = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
    $verification = $manifestValue["verification"]
}

if (-not $verification.ContainsKey("promotionReceipt")) {
    throw "HIPSHARP1001: The promoted manifest does not bind the tracked M8.7 receipt."
}
$promotionReceipt = $verification["promotionReceipt"]
if ($promotionReceipt["path"] -ne "nuget/runtime-manifests/linux-x64.promotion-receipt.json" -or
    $promotionReceipt["lockPath"] -ne "eng/promotion/m8.7-promotion-lock.json" -or
    $promotionReceipt["sha256"] -ne $receiptHash -or
    $verification["validationSha256"] -ne $receiptHash) {
    throw "HIPSHARP1001: The promoted manifest does not bind the tracked M8.7 receipt."
}

Assert-HipSharpRuntimeManifest $manifestValue -RequirePackable
Write-Host "Linux runtime manifest promotion is valid: $receiptHash"
