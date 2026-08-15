[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [string]$ExpectedRepositoryCommit,
    [string]$ReferencePackagePath,
    [string]$OutputDirectory = "artifacts/core-candidate-attestation"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
$coreVersion = Get-HipSharpVersion -Kind Core -RepositoryRoot $repositoryRoot
$packageId = "JYPPX.ROCm.HIP.CSharp.API"
$assemblyName = $packageId
$frameworks = @(
    "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
    "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
)
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts")).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$artifactsPrefix = $artifactsRoot + [System.IO.Path]::DirectorySeparatorChar
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$outputRoot = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
if ($outputRoot -eq $artifactsRoot -or -not $outputRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Core candidate attestation output must remain below the repository artifacts directory."
}
if (-not $resolvedPackage.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Core candidate package must remain below the repository artifacts directory."
}

if ([string]::IsNullOrWhiteSpace($ExpectedRepositoryCommit)) {
    $ExpectedRepositoryCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
}
if ($ExpectedRepositoryCommit -cnotmatch '^[0-9a-f]{40}$') {
    throw "ExpectedRepositoryCommit must be a lowercase 40-character Git SHA."
}
$head = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
$trackedStatus = @(& git -C $repositoryRoot status --porcelain=v1 --untracked-files=no)
if ($LASTEXITCODE -ne 0 -or $head -ne $ExpectedRepositoryCommit -or $trackedStatus.Count -ne 0) {
    throw "Core candidate attestation requires the exact expected SHA and a clean tracked worktree."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-PackageEntries([string]$Path) {
    $entries = [ordered]@{}
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        foreach ($entry in @($archive.Entries | Where-Object { -not $_.FullName.EndsWith("/") } | Sort-Object FullName)) {
            $name = $entry.FullName.Replace("\", "/")
            if ($entries.Contains($name)) { throw "Duplicate package entry: $name" }
            $stream = $entry.Open()
            try {
                $sha256 = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
            }
            finally {
                $stream.Dispose()
            }
            $entries[$name] = [ordered]@{ size = [int64]$entry.Length; sha256 = $sha256 }
        }
    }
    finally {
        $archive.Dispose()
    }
    return $entries
}

function Test-NuGetContainerMetadata([string]$Path) {
    return $Path -eq "_rels/.rels" -or
        $Path -eq "[Content_Types].xml" -or
        $Path -eq ".signature.p7s" -or
        $Path -match '^[^/]+\.nuspec$' -or
        $Path -match '^package/services/metadata/core-properties/[^/]+\.psmdcp$'
}

function Get-EntrySetDigest([System.Collections.IDictionary]$Entries, [bool]$ProtectedOnly) {
    $builder = [System.Text.StringBuilder]::new()
    foreach ($path in @($Entries.Keys | Sort-Object)) {
        if ($ProtectedOnly -and (Test-NuGetContainerMetadata $path)) { continue }
        $entry = $Entries[$path]
        [void]$builder.Append($path).Append("`0").Append($entry.size).Append("`0").Append($entry.sha256).Append("`n")
    }
    $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($builder.ToString())
    return [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-PackageNuspec([string]$Path) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Path)
    try {
        $entries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($entries.Count -ne 1) { throw "Core package must contain exactly one nuspec." }
        $reader = [System.IO.StreamReader]::new($entries[0].Open())
        try { return [xml]$reader.ReadToEnd() } finally { $reader.Dispose() }
    }
    finally {
        $archive.Dispose()
    }
}

function Compare-ReferencePackage([System.Collections.IDictionary]$CandidateEntries, [string]$ReferencePath) {
    $reference = Get-PackageEntries $ReferencePath
    $allPaths = @(@($CandidateEntries.Keys) + @($reference.Keys) | Sort-Object -Unique)
    $metadataDifferences = [System.Collections.Generic.List[object]]::new()
    foreach ($path in $allPaths) {
        $candidateEntry = if ($CandidateEntries.Contains($path)) { $CandidateEntries[$path] } else { $null }
        $referenceEntry = if ($reference.Contains($path)) { $reference[$path] } else { $null }
        $same = $null -ne $candidateEntry -and $null -ne $referenceEntry -and
            $candidateEntry.size -eq $referenceEntry.size -and $candidateEntry.sha256 -eq $referenceEntry.sha256
        if (-not $same -and -not (Test-NuGetContainerMetadata $path)) {
            throw "Protected package content differs from the reference package at '$path'."
        }
        if (-not $same) {
            $metadataDifferences.Add([ordered]@{
                path = $path
                candidateSha256 = if ($null -eq $candidateEntry) { $null } else { $candidateEntry.sha256 }
                referenceSha256 = if ($null -eq $referenceEntry) { $null } else { $referenceEntry.sha256 }
            })
        }
    }
    return [ordered]@{
        referencePackageSha256 = Get-Sha256 $ReferencePath
        zipByteIdentical = (Get-Sha256 $resolvedPackage) -eq (Get-Sha256 $ReferencePath)
        normalizedContentEqual = (Get-EntrySetDigest $CandidateEntries $false) -eq (Get-EntrySetDigest $reference $false)
        protectedContentEqual = $true
        metadataDifferences = $metadataDifferences.ToArray()
    }
}

$entries = Get-PackageEntries $resolvedPackage
$nuspec = Get-PackageNuspec $resolvedPackage
$metadata = $nuspec.package.metadata
if ($metadata.id -ne $packageId -or $metadata.version -ne $coreVersion) {
    throw "Unexpected Core package identity: $($metadata.id) $($metadata.version)."
}
if ($metadata.repository.commit -ne $ExpectedRepositoryCommit) {
    throw "Core package RepositoryCommit does not match the expected clean SHA."
}

$apiSnapshot = Join-Path $repositoryRoot "eng/public-api/JYPPX.ROCm.HipSharp.$coreVersion.txt"
$historicalSnapshot = Join-Path $repositoryRoot "eng/public-api/JYPPX.ROCm.HipSharp.0.9.1.txt"
if (-not (Test-Path -LiteralPath $apiSnapshot -PathType Leaf) -or -not (Test-Path -LiteralPath $historicalSnapshot -PathType Leaf)) {
    throw "Current and historical API snapshots are required."
}
$apiHash = Get-Sha256 $apiSnapshot
$historicalApiHash = Get-Sha256 $historicalSnapshot
if ($apiHash -ne $historicalApiHash) { throw "Core 1.0.0 API snapshot differs from 0.9.1." }
$apiLines = @(Get-Content -LiteralPath $apiSnapshot | Where-Object { -not [string]::IsNullOrWhiteSpace($_) -and -not $_.StartsWith("#") })
$exportedTypes = @($apiLines | Where-Object { $_.StartsWith("T|") }).Count
$members = $apiLines.Count - $exportedTypes
if ($exportedTypes -ne 68 -or $members -ne 1002) {
    throw "Unexpected public API counts: types=$exportedTypes members=$members."
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null
$temporaryRoot = Join-Path $outputRoot "assembly-inspection"
if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
$frameworkEvidence = [System.Collections.Generic.List[object]]::new()
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    foreach ($framework in $frameworks) {
        $dllPath = "lib/$framework/$assemblyName.dll"
        $xmlPath = "lib/$framework/$assemblyName.xml"
        $frameworkEntries = @($entries.Keys | Where-Object { $_.StartsWith("lib/$framework/", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($frameworkEntries.Count -ne 2 -or -not $entries.Contains($dllPath) -or -not $entries.Contains($xmlPath)) {
            throw "Unexpected package asset group for $framework."
        }
        $dllEntry = $archive.GetEntry($dllPath)
        if ($null -eq $dllEntry) { throw "Missing Core assembly for $framework." }
        $inspectionPath = Join-Path $temporaryRoot "$framework/$assemblyName.dll"
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $inspectionPath) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($dllEntry, $inspectionPath, $true)
        $identity = [System.Reflection.AssemblyName]::GetAssemblyName($inspectionPath)
        if ($identity.Name -ne $assemblyName) { throw "Unexpected assembly identity for $framework`: $($identity.FullName)." }
        $frameworkEvidence.Add([ordered]@{
            targetFramework = $framework
            assemblyIdentity = $identity.FullName
            dll = $entries[$dllPath]
            xml = $entries[$xmlPath]
        })
    }
}
finally {
    $archive.Dispose()
    if (Test-Path -LiteralPath $temporaryRoot) { Remove-Item -LiteralPath $temporaryRoot -Recurse -Force }
}

$entryManifest = @($entries.Keys | Sort-Object | ForEach-Object {
    [ordered]@{ path = $_; size = $entries[$_].size; sha256 = $entries[$_].sha256; protected = -not (Test-NuGetContainerMetadata $_) }
})
$referenceComparison = if ([string]::IsNullOrWhiteSpace($ReferencePackagePath)) {
    $null
} else {
    $resolvedReference = (Resolve-Path -LiteralPath $ReferencePackagePath).Path
    Compare-ReferencePackage $entries $resolvedReference
}

$report = [ordered]@{
    schemaVersion = 1
    mode = "linux-core-1.0.0-clean-sha-candidate"
    status = "candidate-built-local"
    generatedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    gitSha = $ExpectedRepositoryCommit
    trackedWorktreeClean = $true
    package = [ordered]@{
        id = $packageId
        version = $coreVersion
        fileName = [System.IO.Path]::GetFileName($resolvedPackage)
        size = (Get-Item -LiteralPath $resolvedPackage).Length
        sha256 = Get-Sha256 $resolvedPackage
        repositoryCommit = [string]$metadata.repository.commit
        normalizedContentSha256 = Get-EntrySetDigest $entries $false
        protectedContentSha256 = Get-EntrySetDigest $entries $true
        entries = $entryManifest
    }
    publicApi = [ordered]@{
        snapshot = "eng/public-api/JYPPX.ROCm.HipSharp.$coreVersion.txt"
        sha256 = $apiHash
        historicalVersion = "0.9.1"
        historicalSha256 = $historicalApiHash
        equalToHistorical = $true
        exportedTypes = $exportedTypes
        members = $members
    }
    frameworks = $frameworkEvidence.ToArray()
    referenceComparison = $referenceComparison
    runtime = [ordered]@{
        packageId = "JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64"
        version = "7.2.1"
        publicSha256 = "21d0a2e511964923de4be2c7f1bf02ce19e9abd9e9bf535cb915c7d7c81b5799"
        repacked = $false
    }
    gpuValidated = $false
    windowsRuntime = "disabled/unverified/static-only"
    performanceClaim = $false
    publishable = $false
    releaseAuthorized = $false
}
$outputPath = Join-Path $outputRoot "core-candidate-attestation.json"
$json = (($report | ConvertTo-Json -Depth 20) -replace "`r`n", "`n") + "`n"
[System.IO.File]::WriteAllText($outputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Core candidate attestation generated: 15 TFMs, 68 types, 1002 members, publishable=false."
Write-Output $outputPath
