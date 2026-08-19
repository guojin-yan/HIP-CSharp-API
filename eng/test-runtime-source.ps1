[CmdletBinding()]
param(
    [string]$Manifest = "nuget/runtime-manifests/ubuntu.24.04-x64.json",
    [string]$CacheDirectory,
    [string]$GpgPath = "gpg",
    [string]$GpgvPath = "gpgv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repositoryRoot $Manifest }
$runtimeVersion = (Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json).packageVersion
if ([string]::IsNullOrWhiteSpace($CacheDirectory)) { $CacheDirectory = "eng/native-assets/cache/rocm-$runtimeVersion-noble" }
$cacheRoot = if ([System.IO.Path]::IsPathRooted($CacheDirectory)) { $CacheDirectory } else { Join-Path $repositoryRoot $CacheDirectory }
$testRoot = Join-Path $repositoryRoot "artifacts/runtime-source-tests"

function Write-Manifest([hashtable]$value, [string]$path) {
    $value | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $path -Encoding utf8NoBOM
}

function Copy-Cache([string]$target, [switch]$IncludeDownloads) {
    New-Item -ItemType Directory -Force -Path $target | Out-Null
    foreach ($name in @("rocm.gpg.key", "InRelease", "Packages.gz")) { Copy-Item -LiteralPath (Join-Path $cacheRoot $name) -Destination $target }
    if ($IncludeDownloads) {
        $targetDownloads = Join-Path $target "downloads"
        New-Item -ItemType Directory -Force -Path $targetDownloads | Out-Null
        foreach ($file in Get-ChildItem -LiteralPath (Join-Path $cacheRoot "downloads") -File -Filter "*.deb") {
            New-Item -ItemType HardLink -Path (Join-Path $targetDownloads $file.Name) -Target $file.FullName | Out-Null
        }
    }
}

function Assert-PrepareRejected([string]$name, [string]$candidateManifest, [string]$candidateCache) {
    try {
        & (Join-Path $PSScriptRoot "prepare-runtime.ps1") -Manifest $candidateManifest -CacheDirectory $candidateCache -Offline -VerifyOnly -GpgPath $GpgPath -GpgvPath $GpgvPath
        throw "Negative source test unexpectedly passed: $name"
    } catch {
        if ($_.Exception.Message -like "Negative source test unexpectedly passed:*") { throw }
        Write-Host "Rejected as expected: $name"
    }
}

if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
New-Item -ItemType Directory -Force -Path $testRoot | Out-Null
try {
    & (Join-Path $PSScriptRoot "prepare-runtime.ps1") -Manifest $manifestPath -CacheDirectory $cacheRoot -Offline -VerifyOnly -GpgPath $GpgPath -GpgvPath $GpgvPath

    $hashManifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
    $hashManifest.packages[0].sha256 = "0000000000000000000000000000000000000000000000000000000000000000"
    $hashPath = Join-Path $testRoot "hash-mismatch.json"
    Write-Manifest $hashManifest $hashPath
    Assert-PrepareRejected "signed package hash mismatch" $hashPath $cacheRoot

    $missingCache = Join-Path $testRoot "missing-cache"
    Copy-Cache $missingCache
    Assert-PrepareRejected "offline package cache miss" $manifestPath $missingCache

    $unsignedCache = Join-Path $testRoot "unsigned-cache"
    Copy-Cache $unsignedCache -IncludeDownloads
    $signedText = Get-Content -Raw -LiteralPath (Join-Path $cacheRoot "InRelease")
    $payload = [regex]::Match($signedText, "(?s)^-----BEGIN PGP SIGNED MESSAGE-----\r?\nHash: SHA512\r?\n\r?\n(?<payload>.*?)\r?\n-----BEGIN PGP SIGNATURE-----").Groups["payload"].Value
    $unsignedPath = Join-Path $unsignedCache "InRelease"
    $payload | Set-Content -LiteralPath $unsignedPath -Encoding utf8NoBOM
    $unsignedManifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
    $unsignedManifest.source.inReleaseSha256 = (Get-FileHash -LiteralPath $unsignedPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $unsignedManifestPath = Join-Path $testRoot "unsigned.json"
    Write-Manifest $unsignedManifest $unsignedManifestPath
    Assert-PrepareRejected "unsigned Release metadata" $unsignedManifestPath $unsignedCache

    $corruptCache = Join-Path $testRoot "corrupt-cache"
    Copy-Cache $corruptCache -IncludeDownloads
    $smallPackage = Get-ChildItem -LiteralPath (Join-Path $corruptCache "downloads") -Filter "rocm-core-*.deb" | Select-Object -First 1
    $originalSmallPackage = Join-Path (Join-Path $cacheRoot "downloads") $smallPackage.Name
    [System.IO.File]::Delete($smallPackage.FullName)
    Copy-Item -LiteralPath $originalSmallPackage -Destination $smallPackage.FullName
    $bytes = [System.IO.File]::ReadAllBytes($smallPackage.FullName)
    $bytes[$bytes.Length - 1] = $bytes[$bytes.Length - 1] -bxor 0x01
    [System.IO.File]::WriteAllBytes($smallPackage.FullName, $bytes)
    Assert-PrepareRejected "cached package tamper" $manifestPath $corruptCache
} finally {
    if (Test-Path -LiteralPath $testRoot) { Remove-Item -LiteralPath $testRoot -Recurse -Force }
}

Write-Host "Runtime signed-source integration tests passed."
