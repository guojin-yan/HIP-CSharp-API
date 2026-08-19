[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$LockFile,
    [string]$OutputReceipt,
    [string]$ExpectedReceipt,
    [switch]$TrackedReceiptOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$lockPath = if ([System.IO.Path]::IsPathRooted($LockFile)) { [System.IO.Path]::GetFullPath($LockFile) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $LockFile)) }

function Fail([string]$message) { throw "HIPSHARP1001: $message" }

function Get-Sha256([string]$path) {
    return (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Assert-Equal([object]$actual, [object]$expected, [string]$name) {
    if ([string]$actual -cne [string]$expected) { Fail "$name mismatch. Expected '$expected', actual '$actual'." }
}

function Assert-False([object]$value, [string]$name) {
    if ($value -ne $false) { Fail "$name must be false for the validated candidate." }
}

function Assert-KeySet([object]$value, [string[]]$expected, [string]$name) {
    $actualKeys = @($value.Keys | Sort-Object)
    $expectedKeys = @($expected | Sort-Object)
    if (($actualKeys -join "`n") -cne ($expectedKeys -join "`n")) {
        Fail "$name fields mismatch. Expected '$($expectedKeys -join ', ')', actual '$($actualKeys -join ', ')'."
    }
}

function Resolve-Input([hashtable]$item, [string]$role) {
    foreach ($name in @("size", "sha256")) {
        if (-not $item.ContainsKey($name)) { Fail "Promotion lock input '$role' is missing '$name'." }
    }
    if ([string]$item.sha256 -notmatch '^[0-9a-f]{64}$') { Fail "Promotion lock input '$role' has an invalid SHA-256." }
    if ($item.ContainsKey("gitObject")) {
        if ([string]$item.gitObject -notmatch '^[0-9a-f]{40}:[A-Za-z0-9._/-]+$') { Fail "Promotion input '$role' has an invalid immutable Git object." }
        $cacheRoot = Join-Path $repositoryRoot "artifacts/promotion/input-cache"
        New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
        $path = Join-Path $cacheRoot "$role.json"
        $startInfo = [System.Diagnostics.ProcessStartInfo]::new("git")
        $startInfo.WorkingDirectory = $repositoryRoot
        $startInfo.UseShellExecute = $false
        $startInfo.RedirectStandardOutput = $true
        $startInfo.RedirectStandardError = $true
        foreach ($argument in @("cat-file", "blob", [string]$item.gitObject)) { $startInfo.ArgumentList.Add($argument) }
        $process = [System.Diagnostics.Process]::Start($startInfo)
        try {
            $stream = [System.IO.File]::Create($path)
            try { $process.StandardOutput.BaseStream.CopyTo($stream) } finally { $stream.Dispose() }
            $errorText = $process.StandardError.ReadToEnd()
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) { Fail "Promotion input '$role' Git object cannot be read: $errorText" }
        } finally { $process.Dispose() }
    } elseif ($item.ContainsKey("path")) {
        $path = if ([System.IO.Path]::IsPathRooted([string]$item.path)) {
            [System.IO.Path]::GetFullPath([string]$item.path)
        } else {
            [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot ([string]$item.path)))
        }
    } else {
        Fail "Promotion lock input '$role' must declare path or gitObject."
    }
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) { Fail "Promotion input '$role' is missing: $path" }
    $actualSize = (Get-Item -LiteralPath $path).Length
    $actualHash = Get-Sha256 $path
    if ($actualSize -ne [int64]$item.size -or $actualHash -ne [string]$item.sha256) {
        Fail "Promotion input '$role' failed its pre-parse size/SHA-256 lock."
    }
    return $path
}

function Read-Json([string]$path, [string]$role) {
    try { return (Get-Content -Raw -LiteralPath $path | ConvertFrom-Json -AsHashtable) }
    catch { Fail "Promotion input '$role' is not valid JSON: $($_.Exception.Message)" }
}

function Assert-NoSensitiveFields([string]$path, [string]$role) {
    $text = Get-Content -Raw -LiteralPath $path
    $forbidden = '(?i)"(endpoint|port|hostname|username|ssh(alias|key)?|privateKey|token|credential|password|gpuUuid|gpuSku|cloudUnique(Path|Directory)|rawLog)"\s*:'
    if ($text -match $forbidden) { Fail "Promotion input '$role' contains a forbidden sensitive field '$($Matches[1])'." }
}

function Get-ZipEntries([string]$path) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $result = [ordered]@{}
        foreach ($entry in @($archive.Entries | Where-Object { -not $_.FullName.EndsWith('/') })) {
            $name = $entry.FullName.Replace('\', '/')
            if ($result.Contains($name)) { Fail "Package contains duplicate path '$name'." }
            $stream = $entry.Open()
            try { $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() }
            finally { $stream.Dispose() }
            $result[$name] = [ordered]@{ size = [int64]$entry.Length; sha256 = $hash }
        }
        return $result
    } finally { $archive.Dispose() }
}

function Get-ZipText([string]$path, [string]$entryPattern, [string]$role) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $archive = [System.IO.Compression.ZipFile]::OpenRead($path)
    try {
        $matches = @($archive.Entries | Where-Object { $_.FullName.Replace('\', '/') -match $entryPattern })
        if ($matches.Count -ne 1) { Fail "$role must contain exactly one entry matching '$entryPattern'." }
        $reader = [System.IO.StreamReader]::new($matches[0].Open())
        try { return $reader.ReadToEnd() } finally { $reader.Dispose() }
    } finally { $archive.Dispose() }
}

function Get-EntrySetDigest([hashtable]$entries, [scriptblock]$filter) {
    $lines = [System.Collections.Generic.List[string]]::new()
    foreach ($name in $entries.Keys) {
        if (& $filter $name) {
            $lines.Add("$name`t$($entries[$name].size)`t$($entries[$name].sha256)")
        }
    }
    $values = $lines.ToArray()
    [System.Array]::Sort($values, [System.StringComparer]::Ordinal)
    $content = ($values -join "`n") + "`n"
    $hash = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData([System.Text.Encoding]::UTF8.GetBytes($content))).ToLowerInvariant()
    return [ordered]@{ sha256 = $hash; paths = $values.Count }
}

function Get-Nuspec([string]$package, [string]$role) {
    [xml]$document = Get-ZipText $package '^[^/]+\.nuspec$' $role
    return $document.package.metadata
}

function ConvertTo-CanonicalJson([object]$value) {
    return (($value | ConvertTo-Json -Depth 40) -replace "`r`n", "`n") + "`n"
}

if (-not (Test-Path -LiteralPath $lockPath -PathType Leaf)) { Fail "Promotion lock is missing: $lockPath" }
$lock = Read-Json $lockPath "promotionLock"
foreach ($name in @("schemaVersion", "promotionId", "validatedGitCommit", "generatorVersion", "inputs", "stagingDigestSha256", "candidatePayloadDigests")) {
    if (-not $lock.ContainsKey($name)) { Fail "Promotion lock is missing '$name'." }
}
Assert-Equal $lock.schemaVersion 1 "promotion lock schemaVersion"
if ([string]$lock.validatedGitCommit -notmatch '^[0-9a-f]{40}$') { Fail "validatedGitCommit must be a lowercase 40-character SHA." }

if (-not $lock.ContainsKey("expectations")) {
    Fail "Promotion lock must declare explicit expectations for the distribution-specific package."
}
$expectations = $lock.expectations
foreach ($name in @(
    "summaryTopic", "pushed", "corePackageId", "corePackageVersion", "runtimePackageId",
    "runtimePackageVersion", "candidateMode", "candidateStatus", "coreNuspec", "runtimeNuspec"
)) {
    if (-not $expectations.ContainsKey($name)) { Fail "Promotion expectations are missing '$name'." }
}
Assert-Equal $expectations.runtimePackageId "JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64" "Runtime expectation package ID"
Assert-Equal $expectations.runtimeNuspec "JYPPX.ROCm.HIP.CSharp.API.Runtime.ubuntu.24.04-x64.nuspec" "Runtime expectation nuspec"

$requiredRoles = @(
    "validationSummary", "gitBundle", "transferManifest", "coreCandidate", "runtimeCandidate",
    "coreAudit", "runtimeAudit", "sourceManifest", "candidateManifest", "sbom", "candidateAttestation"
)

if ($TrackedReceiptOnly) {
    if ([string]::IsNullOrWhiteSpace($ExpectedReceipt)) {
        Fail "Tracked-receipt validation requires ExpectedReceipt."
    }
    $expectedPath = if ([System.IO.Path]::IsPathRooted($ExpectedReceipt)) {
        [System.IO.Path]::GetFullPath($ExpectedReceipt)
    } else {
        [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ExpectedReceipt))
    }
    if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) { Fail "Expected promotion receipt is missing." }
    Assert-NoSensitiveFields $expectedPath "promotionReceipt"
    $receipt = Read-Json $expectedPath "promotionReceipt"
    Assert-KeySet $receipt @(
        "schemaVersion", "promotionId", "validatedGitCommit", "generatorVersion", "inputs",
        "candidatePackages", "payload", "allowedMetadataPaths", "validationScope", "boundaries"
    ) "promotion receipt"
    Assert-Equal $receipt.schemaVersion 1 "promotion receipt schemaVersion"
    Assert-Equal $receipt.promotionId $lock.promotionId "promotion receipt promotionId"
    Assert-Equal $receipt.validatedGitCommit $lock.validatedGitCommit "promotion receipt Git SHA"
    Assert-Equal $receipt.generatorVersion $lock.generatorVersion "promotion receipt generatorVersion"

    foreach ($role in $requiredRoles) {
        if (-not $lock.inputs.ContainsKey($role)) { Fail "Promotion lock is missing input '$role'." }
        $item = $lock.inputs[$role]
        foreach ($name in @("size", "sha256")) {
            if (-not $item.ContainsKey($name)) { Fail "Promotion lock input '$role' is missing '$name'." }
        }
        if ([int64]$item.size -le 0 -or [string]$item.sha256 -notmatch '^[0-9a-f]{64}$') {
            Fail "Promotion lock input '$role' has an invalid size or SHA-256."
        }
        if (-not $item.ContainsKey("path") -or [System.IO.Path]::IsPathRooted([string]$item.path) -or
            [string]$item.path -match '(^|/)\.\.(/|$)' -or
            [string]$item.path -notmatch '^(artifacts/promotion/|nuget/runtime-manifests/)') {
            Fail "Promotion lock input '$role' must use a repository-relative promotion or manifest path."
        }
        if (-not $receipt.inputs.ContainsKey($role)) { Fail "Promotion receipt is missing input '$role'." }
        Assert-KeySet $receipt.inputs[$role] @("size", "sha256") "promotion receipt input $role"
        Assert-Equal $receipt.inputs[$role].size $item.size "promotion receipt input $role size"
        Assert-Equal $receipt.inputs[$role].sha256 $item.sha256 "promotion receipt input $role SHA-256"
    }

    $coreReceipt = $receipt.candidatePackages.core
    $runtimeReceipt = $receipt.candidatePackages.runtime
    Assert-Equal $coreReceipt.id $expectations.corePackageId "tracked Core package ID"
    Assert-Equal $coreReceipt.version $expectations.corePackageVersion "tracked Core package version"
    Assert-Equal $coreReceipt.size $lock.inputs.coreCandidate.size "tracked Core package size"
    Assert-Equal $coreReceipt.sha256 $lock.inputs.coreCandidate.sha256 "tracked Core package hash"
    Assert-Equal $coreReceipt.repositoryCommit $lock.validatedGitCommit "tracked Core package commit"
    Assert-Equal $runtimeReceipt.id $expectations.runtimePackageId "tracked Runtime package ID"
    Assert-Equal $runtimeReceipt.version $expectations.runtimePackageVersion "tracked Runtime package version"
    Assert-Equal $runtimeReceipt.size $lock.inputs.runtimeCandidate.size "tracked Runtime package size"
    Assert-Equal $runtimeReceipt.sha256 $lock.inputs.runtimeCandidate.sha256 "tracked Runtime package hash"
    Assert-Equal $runtimeReceipt.repositoryCommit $lock.validatedGitCommit "tracked Runtime package commit"

    $payloadPathCounts = [ordered]@{
        coreManagedLicenseAndLogo = 32
        runtimeNative = 14
        runtimeLicenses = 7
        runtimeSbom = 1
        runtimeProtectedPayload = 23
    }
    foreach ($name in $payloadPathCounts.Keys) {
        Assert-Equal $receipt.payload[$name].sha256 $lock.candidatePayloadDigests[$name] "tracked payload $name hash"
        Assert-Equal $receipt.payload[$name].paths $payloadPathCounts[$name] "tracked payload $name path count"
    }
    Assert-Equal $receipt.payload.stagingDigestSha256 $lock.stagingDigestSha256 "tracked staging digest"
    Assert-Equal (@($receipt.allowedMetadataPaths.core) -join "`n") (@("_rels/.rels", "[Content_Types].xml", $expectations.coreNuspec, "package/services/metadata/core-properties/*.psmdcp", "README.md") -join "`n") "tracked Core metadata allowlist"
    Assert-Equal (@($receipt.allowedMetadataPaths.runtime) -join "`n") (@("_rels/.rels", "[Content_Types].xml", $expectations.runtimeNuspec, "package/services/metadata/core-properties/*.psmdcp", "README.md", "runtime-manifest.json", "promotion-receipt.json") -join "`n") "tracked Runtime metadata allowlist"

    Assert-Equal $receipt.validationScope.environment.os "Ubuntu 24.04.4" "tracked validation OS"
    Assert-Equal $receipt.validationScope.environment.architecture "x86_64" "tracked validation architecture"
    Assert-Equal $receipt.validationScope.environment.gpuArchitecture "gfx1100" "tracked validation GPU"
    Assert-Equal $receipt.validationScope.environment.isolation "official-host + PRoot package-only" "tracked validation isolation"
    Assert-Equal $receipt.validationScope.symbols.managedRuntime "91/91" "tracked managed Runtime symbols"
    Assert-Equal $receipt.validationScope.symbols.managedHiprtc "9/9" "tracked managed HIPRTC symbols"
    Assert-Equal $receipt.validationScope.symbols.completeRuntime "458/459" "tracked complete Runtime symbols"
    Assert-Equal (@($receipt.validationScope.symbols.completeRuntimeAllowedMissing) -join "`n") "hipExternalMemoryGetMappedMipmappedArray" "tracked allowed missing Runtime symbol"
    Assert-Equal $receipt.validationScope.symbols.completeHiprtc "18/18" "tracked complete HIPRTC symbols"
    Assert-Equal $receipt.validationScope.abiSchema 7 "tracked ABI schema"
    Assert-Equal $receipt.validationScope.managedExpansion.comparisons 1127 "tracked comparison count"
    Assert-Equal (@($receipt.validationScope.managedExpansion.stages) -join "`n") (@("m8.2-pitched-memory", "m8.3-memory-pool", "m8.4-explicit-graph", "m8.5-kernel-occupancy", "m8.6-module-globals") -join "`n") "tracked managed stages"
    Assert-Equal (@($receipt.validationScope.managedExpansion.skippedStages).Count) 0 "tracked skipped stage count"
    Assert-False $receipt.validationScope.managedExpansion.performanceClaim "tracked managed performanceClaim"
    Assert-Equal $receipt.validationScope.reliability.status "passed" "tracked reliability status"
    Assert-Equal $receipt.validationScope.reliability.rounds 10 "tracked reliability rounds"
    Assert-Equal $receipt.validationScope.reliability.streams 4 "tracked reliability streams"
    Assert-Equal $receipt.validationScope.reliability.vectorLength 4194304 "tracked reliability vector length"
    Assert-Equal $receipt.validationScope.reliability.maximumInFlightDeviceBytes 201326592 "tracked reliability maximum device bytes"
    Assert-Equal $receipt.validationScope.reliability.cpuGpuCompared $true "tracked reliability CPU/GPU comparison"
    Assert-False $receipt.validationScope.reliability.performanceClaim "tracked reliability performanceClaim"
    Assert-Equal (@($receipt.validationScope.negatives) -join "`n") (@("missingHsa", "coreOnly", "tamperedPackage", "mixedRuntimeHiprtc") -join "`n") "tracked negative gates"
    Assert-Equal $receipt.boundaries.p2p "skipped(device-count<2)" "tracked P2P boundary"
    Assert-Equal $receipt.boundaries.windows "disabled/unverified/static-only" "tracked Windows boundary"
    Assert-False $receipt.boundaries.performanceClaim "tracked performanceClaim boundary"
    Assert-False $receipt.boundaries.publishable "tracked publishable boundary"
    Assert-False $receipt.boundaries.releaseAuthorized "tracked release authorization boundary"
    Write-Host "Tracked promotion receipt passed without ignored candidate artifacts: $($lock.promotionId)."
    return
}

$paths = @{}
foreach ($role in $requiredRoles) {
    if (-not $lock.inputs.ContainsKey($role)) { Fail "Promotion lock is missing input '$role'." }
    $paths[$role] = Resolve-Input $lock.inputs[$role] $role
}
Assert-NoSensitiveFields $paths.validationSummary "validationSummary"

$summary = Read-Json $paths.validationSummary "validationSummary"
$transfer = Read-Json $paths.transferManifest "transferManifest"
$coreAudit = Read-Json $paths.coreAudit "coreAudit"
$runtimeAudit = Read-Json $paths.runtimeAudit "runtimeAudit"
$sourceManifest = Read-Json $paths.sourceManifest "sourceManifest"
$candidateManifest = Read-Json $paths.candidateManifest "candidateManifest"
$attestation = Read-Json $paths.candidateAttestation "candidateAttestation"

Assert-Equal $summary.schemaVersion 1 "validation summary schemaVersion"
Assert-Equal $summary.topic $expectations.summaryTopic "validation summary topic"
Assert-Equal $summary.status "completed" "validation summary status"
Assert-Equal $summary.finalGitCommit $lock.validatedGitCommit "validation summary Git SHA"
Assert-Equal $summary.validatedCheckout "clean-detached" "validation checkout"
Assert-Equal $summary.officialHostGateExitCode 0 "official host gate exit code"
Assert-Equal $summary.isolatedPRootGateExitCode 0 "PRoot gate exit code"
Assert-Equal $summary.pushed $expectations.pushed "validation summary pushed"
foreach ($name in @("published", "publishable")) { Assert-False $summary[$name] "validation summary $name" }

$corePackage = $summary.packages.core
$runtimePackage = $summary.packages.runtime
Assert-Equal $corePackage.id $expectations.corePackageId "Core package ID"
Assert-Equal $corePackage.version $expectations.corePackageVersion "Core package version"
Assert-Equal ([string]$corePackage.sha256).ToLowerInvariant() $lock.inputs.coreCandidate.sha256 "Core package hash"
Assert-Equal $corePackage.size $lock.inputs.coreCandidate.size "Core package size"
Assert-Equal $corePackage.repositoryCommit $lock.validatedGitCommit "Core repository commit"
Assert-Equal $runtimePackage.id $expectations.runtimePackageId "Runtime package ID"
Assert-Equal $runtimePackage.version $expectations.runtimePackageVersion "Runtime package version"
Assert-Equal ([string]$runtimePackage.sha256).ToLowerInvariant() $lock.inputs.runtimeCandidate.sha256 "Runtime package hash"
Assert-Equal $runtimePackage.size $lock.inputs.runtimeCandidate.size "Runtime package size"
Assert-Equal $runtimePackage.repositoryCommit $lock.validatedGitCommit "Runtime repository commit"
Assert-Equal $runtimePackage.mode $expectations.candidateMode "Runtime candidate mode"

Assert-Equal $summary.symbols.managedRuntime "91/91" "managed Runtime symbols"
Assert-Equal $summary.symbols.managedHiprtc "9/9" "managed HIPRTC symbols"
Assert-Equal $summary.symbols.completeRuntime "458/459" "complete Runtime symbols"
Assert-Equal (@($summary.symbols.completeRuntimeAllowedMissing).Count) 1 "complete Runtime allowed-missing count"
Assert-Equal $summary.symbols.completeRuntimeAllowedMissing[0] "hipExternalMemoryGetMappedMipmappedArray" "complete Runtime allowed missing"
Assert-Equal $summary.symbols.completeHiprtc "18/18" "complete HIPRTC symbols"
Assert-Equal $summary.symbols.abiSchema 7 "ABI schema"

$expansion = $summary.managedExpansion
Assert-Equal $expansion.schemaVersion 1 "managed expansion schemaVersion"
Assert-Equal $expansion.status "passed" "managed expansion status"
Assert-Equal $expansion.comparisons 1127 "managed expansion comparisons"
Assert-Equal (@($expansion.skippedStages).Count) 0 "managed expansion skippedStages"
Assert-False $expansion.performanceClaim "managed expansion performanceClaim"
$expectedStages = [ordered]@{
    "m8.2-pitched-memory" = 135
    "m8.3-memory-pool" = 256
    "m8.4-explicit-graph" = 256
    "m8.5-kernel-occupancy" = 64
    "m8.6-module-globals" = 416
}
foreach ($stageName in $expectedStages.Keys) {
    if (-not $expansion.stages.ContainsKey($stageName)) { Fail "Managed expansion stage '$stageName' is missing." }
    Assert-Equal $expansion.stages[$stageName].status "passed" "$stageName status"
    Assert-Equal $expansion.stages[$stageName].comparisons $expectedStages[$stageName] "$stageName comparisons"
}
Assert-Equal $expansion.stages["m8.4-explicit-graph"].iterations 3 "M8.4 iterations"

Assert-Equal $summary.reliability.status "passed" "reliability status"
Assert-Equal $summary.reliability.rounds 10 "reliability rounds"
Assert-Equal $summary.reliability.streams 4 "reliability streams"
Assert-Equal $summary.reliability.vectorLength 4194304 "reliability vector length"
Assert-Equal $summary.reliability.maximumInFlightDeviceBytes 201326592 "reliability maximum device bytes"
Assert-Equal $summary.reliability.cpuGpuCompared $true "reliability CPU/GPU comparison"
Assert-False $summary.reliability.performanceClaim "reliability performanceClaim"
Assert-Equal $summary.p2p "skipped(device-count<2)" "single-GPU P2P result"
foreach ($name in @("missingHsa", "coreOnly", "tamperedPackage", "mixedRuntimeHiprtc")) {
    if (-not $summary.isolatedNegatives.ContainsKey($name)) { Fail "Isolated negative '$name' is missing." }
    Assert-Equal $summary.isolatedNegatives[$name] "fail-closed-passed" "isolated negative $name"
}
foreach ($name in @("packEnabled", "verified", "packageAuditVerified", "gpuValidated", "publishable")) {
    if (-not $summary.candidateFlags.ContainsKey($name)) { Fail "Candidate flag '$name' is missing." }
    Assert-False $summary.candidateFlags[$name] "candidate flag $name"
}

& git bundle verify $paths.gitBundle *> $null
if ($LASTEXITCODE -ne 0) { Fail "Git bundle verification failed." }
$bundleHeads = @(& git bundle list-heads $paths.gitBundle)
if ($LASTEXITCODE -ne 0 -or -not @($bundleHeads | Where-Object { $_ -match "^$($lock.validatedGitCommit)\s" }).Count) {
    Fail "Git bundle does not contain the validated commit."
}

$coreNuspec = Get-Nuspec $paths.coreCandidate "Core candidate"
$runtimeNuspec = Get-Nuspec $paths.runtimeCandidate "Runtime candidate"
Assert-Equal $coreNuspec.id $corePackage.id "Core nuspec ID"
Assert-Equal $coreNuspec.version $corePackage.version "Core nuspec version"
Assert-Equal $coreNuspec.repository.commit $lock.validatedGitCommit "Core nuspec repository commit"
Assert-Equal $runtimeNuspec.id $runtimePackage.id "Runtime nuspec ID"
Assert-Equal $runtimeNuspec.version $runtimePackage.version "Runtime nuspec version"
Assert-Equal $runtimeNuspec.repository.commit $lock.validatedGitCommit "Runtime nuspec repository commit"

Assert-Equal ([string]$coreAudit.sha256).ToLowerInvariant() $lock.inputs.coreCandidate.sha256 "Core audit package hash"
Assert-Equal $coreAudit.size $lock.inputs.coreCandidate.size "Core audit package size"
Assert-Equal $coreAudit.packageVersion $expectations.corePackageVersion "Core audit version"
Assert-Equal $coreAudit.repositoryCommit $lock.validatedGitCommit "Core audit repository commit"
Assert-Equal $coreAudit.contentAudit "passed" "Core content audit"
Assert-False $coreAudit.publishable "Core audit publishable"
Assert-Equal (@($coreAudit.targetFrameworkAssets).Count) 15 "Core TFM asset count"
Assert-Equal (@($coreAudit.consumers).Count) 4 "Core clean consumer count"
foreach ($consumer in @($coreAudit.consumers)) {
    Assert-Equal $consumer.restore "passed" "Core consumer restore"
    Assert-Equal $consumer.build "passed" "Core consumer build"
}

Assert-Equal ([string]$runtimeAudit.sha256).ToLowerInvariant() $lock.inputs.runtimeCandidate.sha256 "Runtime audit package hash"
Assert-Equal $runtimeAudit.size $lock.inputs.runtimeCandidate.size "Runtime audit package size"
Assert-Equal $runtimeAudit.packageId $runtimePackage.id "Runtime audit package ID"
Assert-Equal $runtimeAudit.packageVersion $expectations.runtimePackageVersion "Runtime audit version"
Assert-Equal $runtimeAudit.contentAudit "passed" "Runtime content audit"
Assert-Equal $runtimeAudit.mode $expectations.candidateMode "Runtime audit mode"
Assert-False $runtimeAudit.publishable "Runtime audit publishable"
Assert-Equal $runtimeAudit.currentGitCommit $lock.validatedGitCommit "Runtime audit current commit"
Assert-Equal $runtimeAudit.packageRepositoryCommit $lock.validatedGitCommit "Runtime audit repository commit"

foreach ($manifest in @($sourceManifest, $candidateManifest)) {
    Assert-Equal $manifest.packageId $runtimePackage.id "Runtime manifest package ID"
    Assert-Equal $manifest.packageVersion $expectations.runtimePackageVersion "Runtime manifest version"
    foreach ($name in @("packEnabled", "verified")) { Assert-False $manifest[$name] "Runtime manifest $name" }
    foreach ($name in @("packageAuditVerified", "gpuValidated")) { Assert-False $manifest.verification[$name] "Runtime manifest verification.$name" }
}
Assert-Equal $candidateManifest.candidate.gitSha $lock.validatedGitCommit "candidate manifest Git SHA"
Assert-False $candidateManifest.candidate.publishable "candidate manifest publishable"
Assert-Equal $candidateManifest.candidate.status $expectations.candidateStatus "candidate manifest status"
Assert-Equal $candidateManifest.candidate.sourceManifestSha256 $lock.inputs.sourceManifest.sha256 "candidate source manifest hash"
Assert-Equal $candidateManifest.sbom.sha256 $lock.inputs.sbom.sha256 "candidate manifest SBOM hash"
Assert-Equal $runtimeAudit.manifestSha256 $lock.inputs.candidateManifest.sha256 "Runtime audit candidate manifest hash"
Assert-Equal $runtimeAudit.sbomSha256 $lock.inputs.sbom.sha256 "Runtime audit SBOM hash"
Assert-Equal $runtimeAudit.sourceManifestSha256 $lock.inputs.sourceManifest.sha256 "Runtime audit source manifest hash"

Assert-Equal $attestation.schemaVersion 1 "candidate attestation schemaVersion"
Assert-Equal $attestation.mode $expectations.candidateMode "candidate attestation mode"
Assert-False $attestation.publishable "candidate attestation publishable"
Assert-Equal $attestation.gitSha $lock.validatedGitCommit "candidate attestation Git SHA"
Assert-Equal $attestation.coreVersion $expectations.corePackageVersion "candidate attestation Core version"
Assert-Equal $attestation.packageId $runtimePackage.id "candidate attestation package ID"
Assert-Equal $attestation.packageVersion $expectations.runtimePackageVersion "candidate attestation package version"
Assert-Equal $attestation.rid "linux-x64" "candidate attestation RID"
Assert-Equal $attestation.sourceManifestSha256 $lock.inputs.sourceManifest.sha256 "attestation source manifest hash"
Assert-Equal $attestation.manifestSha256 $lock.inputs.candidateManifest.sha256 "attestation candidate manifest hash"
Assert-Equal $attestation.sbomSha256 $lock.inputs.sbom.sha256 "attestation SBOM hash"
Assert-Equal $attestation.stagingDigestSha256 $lock.stagingDigestSha256 "attestation staging digest"

Assert-Equal $transfer.gitSha $lock.validatedGitCommit "transfer manifest Git SHA"
Assert-Equal $transfer.execution.officialHostExitCode 0 "transfer official host exit code"
Assert-Equal $transfer.execution.packageOnlyPRootExitCode 0 "transfer PRoot exit code"
Assert-Equal $transfer.execution.managedExpansionComparisons 1127 "transfer managed comparisons"
Assert-Equal (@($transfer.execution.managedExpansionSkippedStages).Count) 0 "transfer skipped stages"
Assert-Equal $transfer.execution.reliability "passed" "transfer reliability"
Assert-Equal $transfer.execution.p2p "skipped(device-count<2)" "transfer P2P"
$transferRoles = @{ "git-bundle" = "gitBundle"; "core-nupkg" = "coreCandidate"; "runtime-candidate-nupkg" = "runtimeCandidate" }
foreach ($entry in @($transfer.requiredTransferFiles)) {
    if (-not $transferRoles.ContainsKey([string]$entry.role)) { Fail "Transfer manifest contains unexpected role '$($entry.role)'." }
    $role = $transferRoles[[string]$entry.role]
    Assert-Equal $entry.size $lock.inputs[$role].size "transfer $role size"
    Assert-Equal ([string]$entry.sha256).ToLowerInvariant() $lock.inputs[$role].sha256 "transfer $role hash"
}
Assert-Equal (@($transfer.requiredTransferFiles).Count) 3 "transfer file count"
$evidenceRoles = @{ coreAudit = "coreAudit"; runtimeAudit = "runtimeAudit"; sourceManifest = "sourceManifest"; candidateManifest = "candidateManifest"; sbom = "sbom"; candidateAttestation = "candidateAttestation" }
foreach ($name in $evidenceRoles.Keys) {
    Assert-Equal ([string]$transfer.localEvidenceLocks[$name].sha256).ToLowerInvariant() $lock.inputs[$evidenceRoles[$name]].sha256 "transfer evidence $name hash"
}
Assert-Equal ([string]$transfer.localEvidenceLocks.stagingDigestSha256).ToLowerInvariant() $lock.stagingDigestSha256 "transfer staging digest"

$coreEntries = Get-ZipEntries $paths.coreCandidate
$runtimeEntries = Get-ZipEntries $paths.runtimeCandidate
Assert-Equal $runtimeEntries["runtime-manifest.json"].sha256 $lock.inputs.candidateManifest.sha256 "embedded candidate manifest hash"
Assert-Equal $runtimeEntries["ubuntu.24.04-x64.cdx.json"].sha256 $lock.inputs.sbom.sha256 "embedded SBOM hash"
$corePayload = Get-EntrySetDigest $coreEntries { param($name) $name -match '^lib/.+\.(dll|xml)$' -or $name -in @('LICENSE', 'logo.jpg') }
$runtimeNative = Get-EntrySetDigest $runtimeEntries { param($name) $name -match '^runtimes/linux-x64/native/' }
$runtimeLicenses = Get-EntrySetDigest $runtimeEntries { param($name) $name -match '^licenses/' -or $name -eq 'LICENSE' }
$runtimeSbom = Get-EntrySetDigest $runtimeEntries { param($name) $name -eq 'ubuntu.24.04-x64.cdx.json' }
$runtimeProtected = Get-EntrySetDigest $runtimeEntries { param($name) $name -match '^runtimes/linux-x64/native/' -or $name -match '^licenses/' -or $name -in @('LICENSE', 'ubuntu.24.04-x64.cdx.json', 'logo.jpg') }
Assert-Equal $corePayload.sha256 $lock.candidatePayloadDigests.coreManagedLicenseAndLogo "Core candidate payload digest"
Assert-Equal $runtimeNative.sha256 $lock.candidatePayloadDigests.runtimeNative "Runtime native payload digest"
Assert-Equal $runtimeLicenses.sha256 $lock.candidatePayloadDigests.runtimeLicenses "Runtime license payload digest"
Assert-Equal $runtimeSbom.sha256 $lock.candidatePayloadDigests.runtimeSbom "Runtime SBOM payload digest"
Assert-Equal $runtimeProtected.sha256 $lock.candidatePayloadDigests.runtimeProtectedPayload "Runtime protected payload digest"

$receiptInputs = [ordered]@{}
foreach ($role in $requiredRoles) {
    $receiptInputs[$role] = [ordered]@{ size = [int64]$lock.inputs[$role].size; sha256 = [string]$lock.inputs[$role].sha256 }
}
$receipt = [ordered]@{
    schemaVersion = 1
    promotionId = [string]$lock.promotionId
    validatedGitCommit = [string]$lock.validatedGitCommit
    generatorVersion = [string]$lock.generatorVersion
    inputs = $receiptInputs
    candidatePackages = [ordered]@{
        core = [ordered]@{ id = [string]$expectations.corePackageId; version = [string]$expectations.corePackageVersion; size = [int64]$lock.inputs.coreCandidate.size; sha256 = [string]$lock.inputs.coreCandidate.sha256; repositoryCommit = [string]$lock.validatedGitCommit }
        runtime = [ordered]@{ id = [string]$expectations.runtimePackageId; version = [string]$expectations.runtimePackageVersion; size = [int64]$lock.inputs.runtimeCandidate.size; sha256 = [string]$lock.inputs.runtimeCandidate.sha256; repositoryCommit = [string]$lock.validatedGitCommit }
    }
    payload = [ordered]@{
        coreManagedLicenseAndLogo = $corePayload
        runtimeNative = $runtimeNative
        runtimeLicenses = $runtimeLicenses
        runtimeSbom = $runtimeSbom
        runtimeProtectedPayload = $runtimeProtected
        stagingDigestSha256 = [string]$lock.stagingDigestSha256
    }
    allowedMetadataPaths = [ordered]@{
        core = @("_rels/.rels", "[Content_Types].xml", [string]$expectations.coreNuspec, "package/services/metadata/core-properties/*.psmdcp", "README.md")
        runtime = @("_rels/.rels", "[Content_Types].xml", [string]$expectations.runtimeNuspec, "package/services/metadata/core-properties/*.psmdcp", "README.md", "runtime-manifest.json", "promotion-receipt.json")
    }
    validationScope = [ordered]@{
        environment = [ordered]@{ os = "Ubuntu 24.04.4"; architecture = "x86_64"; gpuArchitecture = "gfx1100"; isolation = "official-host + PRoot package-only" }
        symbols = [ordered]@{ managedRuntime = "91/91"; managedHiprtc = "9/9"; completeRuntime = "458/459"; completeRuntimeAllowedMissing = @("hipExternalMemoryGetMappedMipmappedArray"); completeHiprtc = "18/18" }
        abiSchema = 7
        managedExpansion = [ordered]@{ comparisons = 1127; stages = @($expectedStages.Keys); skippedStages = @(); performanceClaim = $false }
        reliability = [ordered]@{ status = "passed"; rounds = 10; streams = 4; vectorLength = 4194304; maximumInFlightDeviceBytes = 201326592; cpuGpuCompared = $true; performanceClaim = $false }
        negatives = @("missingHsa", "coreOnly", "tamperedPackage", "mixedRuntimeHiprtc")
    }
    boundaries = [ordered]@{
        p2p = "skipped(device-count<2)"
        windows = "disabled/unverified/static-only"
        performanceClaim = $false
        publishable = $false
        releaseAuthorized = $false
    }
}
$receiptJson = ConvertTo-CanonicalJson $receipt
if ($receiptJson -match '(?i)"(endpoint|port|hostname|username|ssh(alias|key)?|privateKey|token|credential|password|gpuUuid|gpuSku|cloudUnique(Path|Directory)|rawLog)"\s*:') {
    Fail "Generated promotion receipt contains a forbidden sensitive field."
}

if (-not [string]::IsNullOrWhiteSpace($ExpectedReceipt)) {
    $expectedPath = if ([System.IO.Path]::IsPathRooted($ExpectedReceipt)) { [System.IO.Path]::GetFullPath($ExpectedReceipt) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $ExpectedReceipt)) }
    if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) { Fail "Expected promotion receipt is missing." }
    $actualReceipt = (Get-Content -Raw -LiteralPath $expectedPath) -replace "`r`n", "`n"
    if ($actualReceipt -cne $receiptJson) { Fail "Promotion receipt does not match the deterministic receipt reconstructed from the locked inputs." }
    Assert-NoSensitiveFields $expectedPath "promotionReceipt"
}
if (-not [string]::IsNullOrWhiteSpace($OutputReceipt)) {
    $outputPath = if ([System.IO.Path]::IsPathRooted($OutputReceipt)) { [System.IO.Path]::GetFullPath($OutputReceipt) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputReceipt)) }
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
    [System.IO.File]::WriteAllText($outputPath, $receiptJson, [System.Text.UTF8Encoding]::new($false))
    Write-Host "Promotion receipt: $outputPath"
}

Write-Host "Promotion evidence passed for $($lock.promotionId): exact candidate, 100/477 symbols, ABI schema 7, 1127 comparisons, reliability, and four fail-closed negatives."
