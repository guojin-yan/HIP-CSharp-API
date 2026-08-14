[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CandidateCore,
    [Parameter(Mandatory = $true)][string]$FinalCore,
    [Parameter(Mandatory = $true)][string]$CandidateRuntime,
    [Parameter(Mandatory = $true)][string]$FinalRuntime,
    [string]$Receipt = "nuget/runtime-manifests/linux-x64.promotion-receipt.json",
    [string]$Output = "artifacts/release-envelope/payload-equivalence.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$repositoryRoot = Split-Path -Parent $PSScriptRoot
Add-Type -AssemblyName System.IO.Compression.FileSystem

function Resolve-PathValue([string]$Value) {
    $path = if ([System.IO.Path]::IsPathRooted($Value)) { $Value } else { Join-Path $repositoryRoot $Value }
    return (Resolve-Path -LiteralPath $path).Path
}

function Get-Sha([string]$Path) { return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant() }

function Get-Entries([string]$Package) {
    $entries = [ordered]@{}
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Package)
    try {
        foreach ($entry in @($archive.Entries | Where-Object { -not $_.FullName.EndsWith("/") })) {
            $name = $entry.FullName.Replace("\", "/")
            if ($entries.Contains($name)) { throw "HIPSHARP1001: Duplicate package path: $name" }
            $stream = $entry.Open()
            try { $sha = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
            $entries[$name] = [ordered]@{ size = [int64]$entry.Length; sha256 = $sha }
        }
    } finally { $archive.Dispose() }
    return $entries
}

function Extract-Entry([string]$Package, [string]$EntryName, [string]$Destination) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($Package)
    try {
        $entries = @($archive.Entries | Where-Object { $_.FullName.Replace("\", "/") -eq $EntryName })
        if ($entries.Count -ne 1) { throw "HIPSHARP1001: Package must contain exactly one '$EntryName'." }
        New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Destination) | Out-Null
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($entries[0], $Destination, $true)
    } finally { $archive.Dispose() }
}

function Invoke-ApiTool([string[]]$Arguments) {
    $tool = Join-Path $repositoryRoot "tools/JYPPX.ROCm.HipSharp.ApiSurface/JYPPX.ROCm.HipSharp.ApiSurface.csproj"
    & dotnet run --project $tool --configuration Release --no-build --no-restore -- @Arguments | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "HIPSHARP1001: Core API/semantic comparison tool failed." }
}

function Compare-CoreSemantics([string]$CandidatePackage, [string]$FinalPackage, [string]$AssemblyName) {
    $frameworks = @("net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481", "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0")
    $root = Join-Path $repositoryRoot "artifacts/promotion/package-diff"
    $categories = Join-Path $repositoryRoot "eng/public-api/categories.json"
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($framework in $frameworks) {
        $candidateDll = Join-Path $root "candidate/$framework/$AssemblyName.dll"
        $candidateXml = Join-Path $root "candidate/$framework/$AssemblyName.xml"
        $finalDll = Join-Path $root "final/$framework/$AssemblyName.dll"
        $finalXml = Join-Path $root "final/$framework/$AssemblyName.xml"
        Extract-Entry $CandidatePackage "lib/$framework/$AssemblyName.dll" $candidateDll
        Extract-Entry $CandidatePackage "lib/$framework/$AssemblyName.xml" $candidateXml
        Extract-Entry $FinalPackage "lib/$framework/$AssemblyName.dll" $finalDll
        Extract-Entry $FinalPackage "lib/$framework/$AssemblyName.xml" $finalXml

        $candidateApi = Join-Path $root "candidate/$framework/public-api.txt"
        $finalApi = Join-Path $root "final/$framework/public-api.txt"
        $candidateSemantic = Join-Path $root "candidate/$framework/semantic.txt"
        $finalSemantic = Join-Path $root "final/$framework/semantic.txt"
        Invoke-ApiTool @("--assembly", $candidateDll, "--xml", $candidateXml, "--snapshot", $candidateApi, "--categories", $categories, "--write")
        Invoke-ApiTool @("--assembly", $finalDll, "--xml", $finalXml, "--snapshot", $finalApi, "--categories", $categories, "--write")
        Invoke-ApiTool @("--assembly", $candidateDll, "--semantic", $candidateSemantic)
        Invoke-ApiTool @("--assembly", $finalDll, "--semantic", $finalSemantic)
        $apiHash = Get-Sha $candidateApi
        $semanticHash = Get-Sha $candidateSemantic
        if ($apiHash -ne (Get-Sha $finalApi) -or $semanticHash -ne (Get-Sha $finalSemantic)) {
            throw "HIPSHARP1001: Core public API or IL semantic output changed for $framework."
        }
        $results.Add([ordered]@{
            targetFramework = $framework
            assemblyByteIdentical = (Get-Sha $candidateDll) -eq (Get-Sha $finalDll)
            xmlByteIdentical = (Get-Sha $candidateXml) -eq (Get-Sha $finalXml)
            publicApiSha256 = $apiHash
            semanticSha256 = $semanticHash
        })
    }
    return $results.ToArray()
}

function Test-Allowed([string]$Path, [object[]]$Patterns) {
    foreach ($pattern in $Patterns) {
        if ([System.Management.Automation.WildcardPattern]::new([string]$pattern, [System.Management.Automation.WildcardOptions]::CultureInvariant).IsMatch($Path)) { return $true }
    }
    return $false
}

function Compare-Payload([string]$Role, [hashtable]$Candidate, [hashtable]$Final, [object[]]$Allowed, [object[]]$SemanticControlled = @()) {
    $allPaths = @($Candidate.Keys + $Final.Keys | Sort-Object -Unique)
    $metadataChanges = [System.Collections.Generic.List[object]]::new()
    $semanticDifferences = [System.Collections.Generic.List[object]]::new()
    $protected = 0
    foreach ($path in $allPaths) {
        $candidateEntry = if ($Candidate.Contains($path)) { $Candidate[$path] } else { $null }
        $finalEntry = if ($Final.Contains($path)) { $Final[$path] } else { $null }
        $same = $null -ne $candidateEntry -and $null -ne $finalEntry -and
            $candidateEntry.size -eq $finalEntry.size -and $candidateEntry.sha256 -eq $finalEntry.sha256
        if (Test-Allowed $path $SemanticControlled) {
            $protected++
            if (-not $same) {
                if ($null -eq $candidateEntry -or $null -eq $finalEntry) { throw "HIPSHARP1001: $Role semantic-controlled payload is missing at '$path'." }
                $semanticDifferences.Add([ordered]@{ path = $path; candidateSha256 = $candidateEntry.sha256; finalSha256 = $finalEntry.sha256 })
            }
        } elseif (Test-Allowed $path $Allowed) {
            if (-not $same) {
                $metadataChanges.Add([ordered]@{
                    path = $path
                    candidateSha256 = if ($null -eq $candidateEntry) { $null } else { $candidateEntry.sha256 }
                    finalSha256 = if ($null -eq $finalEntry) { $null } else { $finalEntry.sha256 }
                })
            }
        } else {
            $protected++
            if (-not $same) { throw "HIPSHARP1001: $Role protected payload changed at '$path'." }
        }
    }
    return [ordered]@{ protectedPaths = $protected; metadataChanges = $metadataChanges.ToArray(); semanticDifferences = $semanticDifferences.ToArray() }
}

$receiptPath = Resolve-PathValue $Receipt
$receiptValue = Get-Content -Raw -LiteralPath $receiptPath | ConvertFrom-Json -AsHashtable
$candidateCorePath = Resolve-PathValue $CandidateCore
$finalCorePath = Resolve-PathValue $FinalCore
$candidateRuntimePath = Resolve-PathValue $CandidateRuntime
$finalRuntimePath = Resolve-PathValue $FinalRuntime

if ((Get-Sha $candidateCorePath) -ne $receiptValue.candidatePackages.core.sha256 -or
    (Get-Sha $candidateRuntimePath) -ne $receiptValue.candidatePackages.runtime.sha256) {
    throw "HIPSHARP1001: Candidate package hashes do not match the promotion receipt."
}

$coreAssemblyName = if ($receiptValue.candidatePackages.core.version -eq "0.9.0") { "JYPPX.ROCm.HipSharp" } else { [string]$receiptValue.candidatePackages.core.id }
$core = Compare-Payload "Core" (Get-Entries $candidateCorePath) (Get-Entries $finalCorePath) @($receiptValue.allowedMetadataPaths.core) @("lib/*/$coreAssemblyName.dll")
$coreSemantics = Compare-CoreSemantics $candidateCorePath $finalCorePath $coreAssemblyName
$runtimeEntries = Get-Entries $finalRuntimePath
if (-not $runtimeEntries.Contains("promotion-receipt.json") -or $runtimeEntries["promotion-receipt.json"].sha256 -ne (Get-Sha $receiptPath)) {
    throw "HIPSHARP1001: Final runtime package does not embed the exact promotion receipt."
}
$runtime = Compare-Payload "Runtime" (Get-Entries $candidateRuntimePath) $runtimeEntries @($receiptValue.allowedMetadataPaths.runtime)

$report = [ordered]@{
    schemaVersion = 1
    status = "passed"
    promotionReceiptSha256 = Get-Sha $receiptPath
    core = [ordered]@{
        candidateSha256 = Get-Sha $candidateCorePath
        finalSha256 = Get-Sha $finalCorePath
        protectedPaths = $core.protectedPaths
        metadataChanges = $core.metadataChanges
        semanticDifferences = $core.semanticDifferences
        frameworks = $coreSemantics
        publicApiAndSemanticEquivalent = $true
    }
    runtime = [ordered]@{
        candidateSha256 = Get-Sha $candidateRuntimePath
        finalSha256 = Get-Sha $finalRuntimePath
        protectedPaths = $runtime.protectedPaths
        metadataChanges = $runtime.metadataChanges
    }
    allowedChangesOnly = $true
    performanceClaim = $false
    publishable = $false
    releaseAuthorized = $false
}
$outputPath = if ([System.IO.Path]::IsPathRooted($Output)) { [System.IO.Path]::GetFullPath($Output) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $Output)) }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $outputPath) | Out-Null
$json = (($report | ConvertTo-Json -Depth 20) -replace "`r`n", "`n") + "`n"
[System.IO.File]::WriteAllText($outputPath, $json, [System.Text.UTF8Encoding]::new($false))
Write-Host "Promoted package payload equivalence passed: $($core.protectedPaths) Core and $($runtime.protectedPaths) Runtime protected paths."
Write-Output $outputPath
