[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$LockFile,
    [Parameter(Mandatory = $true)][string]$ExpectedReceipt
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$lockPath = if ([System.IO.Path]::IsPathRooted($LockFile)) { [System.IO.Path]::GetFullPath($LockFile) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $LockFile)) }
$root = Join-Path $repositoryRoot "artifacts/promotion/self-test"
$caseRoot = Join-Path $root ([Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Force -Path $caseRoot | Out-Null

function Write-Json([object]$Value, [string]$Path) {
    $json = (($Value | ConvertTo-Json -Depth 40) -replace "`r`n", "`n") + "`n"
    [System.IO.File]::WriteAllText($Path, $json, [System.Text.UTF8Encoding]::new($false))
}

function Invoke-Verifier([string]$CaseLock, [string]$CaseReceipt = "", [switch]$TrackedReceiptOnly) {
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new("pwsh")
    $startInfo.WorkingDirectory = $repositoryRoot
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @("-NoProfile", "-File", (Join-Path $PSScriptRoot "verify-promotion.ps1"), "-LockFile", $CaseLock)) {
        $startInfo.ArgumentList.Add($argument)
    }
    if (-not [string]::IsNullOrWhiteSpace($CaseReceipt)) {
        $startInfo.ArgumentList.Add("-ExpectedReceipt")
        $startInfo.ArgumentList.Add($CaseReceipt)
    }
    if ($TrackedReceiptOnly) { $startInfo.ArgumentList.Add("-TrackedReceiptOnly") }
    $process = [System.Diagnostics.Process]::Start($startInfo)
    try {
        $output = $process.StandardOutput.ReadToEnd() + $process.StandardError.ReadToEnd()
        $process.WaitForExit()
        return [pscustomobject]@{ ExitCode = $process.ExitCode; Output = $output }
    } finally { $process.Dispose() }
}

function Assert-Rejected([string]$Name, [scriptblock]$Mutation, [switch]$KeepWrongHash) {
    $caseDirectory = Join-Path $caseRoot ($Name -replace '[^A-Za-z0-9.-]', '-')
    New-Item -ItemType Directory -Force -Path $caseDirectory | Out-Null
    $caseLock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json -AsHashtable
    $summarySource = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $caseLock.inputs.validationSummary.path))
    $summary = Get-Content -Raw -LiteralPath $summarySource | ConvertFrom-Json -AsHashtable
    & $Mutation $summary $caseLock
    $summaryPath = Join-Path $caseDirectory "validation-summary.json"
    Write-Json $summary $summaryPath
    $caseLock.inputs.validationSummary.path = $summaryPath
    $caseLock.inputs.validationSummary.size = (Get-Item -LiteralPath $summaryPath).Length
    if (-not $KeepWrongHash) {
        $caseLock.inputs.validationSummary.sha256 = (Get-FileHash -LiteralPath $summaryPath -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    $caseLockPath = Join-Path $caseDirectory "promotion-lock.json"
    Write-Json $caseLock $caseLockPath
    $result = Invoke-Verifier $caseLockPath
    if ($result.ExitCode -eq 0 -or $result.Output -notmatch "HIPSHARP1001") {
        throw "Promotion negative unexpectedly passed or did not fail closed: $Name`n$($result.Output)"
    }
    Write-Host "Rejected as expected: $Name"
}

try {
    & (Join-Path $PSScriptRoot "verify-promotion.ps1") -LockFile $lockPath -ExpectedReceipt $ExpectedReceipt
    if ($LASTEXITCODE -ne 0) { throw "Positive promotion fixture failed." }

    $trackedLock = Get-Content -Raw -LiteralPath $lockPath | ConvertFrom-Json -AsHashtable
    foreach ($role in @($trackedLock.inputs.Keys)) {
        $trackedLock.inputs[$role].path = "artifacts/promotion/missing-ci-fixture/$role.bin"
    }
    $trackedLockPath = Join-Path $caseRoot "tracked-receipt-lock.json"
    Write-Json $trackedLock $trackedLockPath
    $trackedResult = Invoke-Verifier $trackedLockPath $ExpectedReceipt -TrackedReceiptOnly
    if ($trackedResult.ExitCode -ne 0) { throw "Tracked receipt unexpectedly required ignored candidate artifacts.`n$($trackedResult.Output)" }
    Write-Host "Tracked receipt passed with all ignored candidate artifact paths absent."

    $wrongSizeLock = Get-Content -Raw -LiteralPath $trackedLockPath | ConvertFrom-Json -AsHashtable
    $wrongSizeLock.inputs.runtimeCandidate.size = [int64]$wrongSizeLock.inputs.runtimeCandidate.size + 1
    $wrongSizeLockPath = Join-Path $caseRoot "tracked-wrong-size-lock.json"
    Write-Json $wrongSizeLock $wrongSizeLockPath
    $wrongSizeResult = Invoke-Verifier $wrongSizeLockPath $ExpectedReceipt -TrackedReceiptOnly
    if ($wrongSizeResult.ExitCode -eq 0 -or $wrongSizeResult.Output -notmatch "HIPSHARP1001") {
        throw "Tracked receipt accepted a changed Runtime candidate size.`n$($wrongSizeResult.Output)"
    }
    Write-Host "Rejected as expected: tracked Runtime candidate size mismatch"

    $unauthorizedReceipt = Get-Content -Raw -LiteralPath $ExpectedReceipt | ConvertFrom-Json -AsHashtable
    $unauthorizedReceipt.boundaries.releaseAuthorized = $true
    $unauthorizedReceiptPath = Join-Path $caseRoot "tracked-unauthorized-receipt.json"
    Write-Json $unauthorizedReceipt $unauthorizedReceiptPath
    $unauthorizedResult = Invoke-Verifier $trackedLockPath $unauthorizedReceiptPath -TrackedReceiptOnly
    if ($unauthorizedResult.ExitCode -eq 0 -or $unauthorizedResult.Output -notmatch "HIPSHARP1001") {
        throw "Tracked receipt accepted a forged release authorization.`n$($unauthorizedResult.Output)"
    }
    Write-Host "Rejected as expected: tracked release authorization forgery"

    Assert-Rejected "wrong input hash" { param($s, $l) $s.status = "tampered" } -KeepWrongHash
    Assert-Rejected "wrong Git SHA" { param($s, $l) $s.finalGitCommit = ("0" * 40) }
    Assert-Rejected "package hash mismatch" { param($s, $l) $s.packages.core.sha256 = ("0" * 64) }
    Assert-Rejected "official host gate nonzero" { param($s, $l) $s.officialHostGateExitCode = 1 }
    Assert-Rejected "symbol coverage incomplete" { param($s, $l) $s.symbols.managedRuntime = "90/91" }
    Assert-Rejected "ABI schema incomplete" { param($s, $l) $s.symbols.abiSchema = 6 }
    Assert-Rejected "managed stage skipped" { param($s, $l) $s.managedExpansion.skippedStages = @("m8.4-explicit-graph") }
    Assert-Rejected "managed stage failed" { param($s, $l) $s.managedExpansion.stages["m8.5-kernel-occupancy"].status = "failed" }
    Assert-Rejected "comparison total mismatch" { param($s, $l) $s.managedExpansion.comparisons = 1126 }
    Assert-Rejected "performance claim enabled" { param($s, $l) $s.managedExpansion.performanceClaim = $true }
    Assert-Rejected "reliability failure" { param($s, $l) $s.reliability.status = "failed" }
    Assert-Rejected "isolated negative missing" { param($s, $l) $s.isolatedNegatives.Remove("tamperedPackage") }
    Assert-Rejected "candidate flag promoted early" { param($s, $l) $s.candidateFlags.packEnabled = $true }
    Assert-Rejected "single GPU P2P claimed" { param($s, $l) $s.p2p = "passed" }
    Assert-Rejected "sensitive endpoint field" { param($s, $l) $s.endpoint = "forbidden.example" }

    Write-Host "Promotion receipt self-test passed: positive fixture and 15 fail-closed mutations."
} finally {
    $resolvedCaseRoot = [System.IO.Path]::GetFullPath($caseRoot)
    $resolvedArtifacts = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
    if ($resolvedCaseRoot.StartsWith($resolvedArtifacts + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) -and
        (Test-Path -LiteralPath $resolvedCaseRoot)) {
        Remove-Item -LiteralPath $resolvedCaseRoot -Recurse -Force
    }
}
