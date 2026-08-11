[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Manifest,
    [string]$CacheDirectory,
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64",
    [switch]$Offline,
    [switch]$VerifyOnly,
    [string]$GpgPath = "gpg",
    [string]$GpgvPath = "gpgv"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$manifestInfo = Get-HipSharpRuntimeManifest $Manifest
$runtimeManifest = $manifestInfo.Value
Assert-HipSharpRuntimeManifest $runtimeManifest
if ($runtimeManifest.rid -ne "linux-x64") { throw "prepare-runtime.ps1 only prepares linux-x64." }
if ([string]::IsNullOrWhiteSpace($CacheDirectory)) { $CacheDirectory = "eng/native-assets/cache/rocm-$($runtimeManifest.packageVersion)-noble" }

function Resolve-UnderRepository([string]$value) {
    $path = if ([System.IO.Path]::IsPathRooted($value)) { [System.IO.Path]::GetFullPath($value) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $value)) }
    if (-not $path.StartsWith($repositoryRoot + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase)) { throw "Runtime paths must remain under the repository root." }
    return $path
}

function Download-Verified([string]$url, [string]$destination, [string]$expectedHash) {
    if (-not (Test-Path -LiteralPath $destination)) {
        if ($Offline) { throw "Offline cache miss: $destination" }
        $temporary = "$destination.download"
        try {
            Invoke-WebRequest -Uri $url -OutFile $temporary -MaximumRedirection 4
            Move-Item -LiteralPath $temporary -Destination $destination -Force
        } finally {
            if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
        }
    }
    $actualHash = Get-HipSharpSha256 $destination
    if ($actualHash -ne $expectedHash) { throw "SHA-256 mismatch for $destination. Expected $expectedHash, actual $actualHash." }
}

function Get-Executable([string]$candidate, [string]$name) {
    if ([System.IO.Path]::IsPathRooted($candidate)) {
        if (-not (Test-Path -LiteralPath $candidate -PathType Leaf)) { throw "$name executable was not found: $candidate" }
        return (Resolve-Path -LiteralPath $candidate).Path
    }
    $command = Get-Command $candidate -ErrorAction SilentlyContinue
    if ($null -ne $command) { return $command.Source }
    if ($IsWindows -and $candidate -eq $name) {
        $gitRoots = @($env:ProgramFiles, ${env:ProgramFiles(x86)}, $env:LOCALAPPDATA) |
            Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
        foreach ($gitRoot in $gitRoots) {
            $relative = if ($gitRoot -eq $env:LOCALAPPDATA) { "Programs/Git/usr/bin/$name.exe" } else { "Git/usr/bin/$name.exe" }
            $gitExecutable = Join-Path $gitRoot $relative
            if (Test-Path -LiteralPath $gitExecutable -PathType Leaf) { return (Resolve-Path -LiteralPath $gitExecutable).Path }
        }
    }
    throw "$name is required for signed metadata verification; no fallback is permitted."
}

function Convert-GpgPath([string]$path, [string]$gpgExecutable) {
    if ($IsWindows -and $gpgExecutable.EndsWith(".exe", [System.StringComparison]::OrdinalIgnoreCase)) {
        return "/" + $path.Substring(0, 1).ToLowerInvariant() + "/" + $path.Substring(3).Replace("\", "/")
    }
    return $path
}

function Get-IndexPackages([string]$packagesPath) {
    $stream = [System.IO.File]::OpenRead($packagesPath)
    try {
        $gzip = [System.IO.Compression.GZipStream]::new($stream, [System.IO.Compression.CompressionMode]::Decompress)
        try {
            $reader = [System.IO.StreamReader]::new($gzip)
            try { $content = $reader.ReadToEnd() } finally { $reader.Dispose() }
        } finally { $gzip.Dispose() }
    } finally { $stream.Dispose() }
    $items = @{}
    foreach ($stanza in $content -split "\r?\n\r?\n") {
        if ([string]::IsNullOrWhiteSpace($stanza)) { continue }
        $item = @{}
        foreach ($line in $stanza -split "\r?\n") {
            $separator = $line.IndexOf(":")
            if ($separator -gt 0) { $item[$line.Substring(0, $separator)] = $line.Substring($separator + 1).Trim() }
        }
        if ($item.ContainsKey("Package")) { $items[$item.Package] = $item }
    }
    return $items
}

function Extract-DebEntries([string]$debPath, [string]$targetDirectory, [string[]]$entries) {
    $bytes = [System.IO.File]::ReadAllBytes($debPath)
    if ([System.Text.Encoding]::ASCII.GetString($bytes, 0, 8) -ne "!<arch>`n") { throw "Downloaded package is not a Debian archive: $debPath" }
    $offset = 8
    $dataArchive = $null
    while ($offset -lt $bytes.Length) {
        if ($offset + 60 -gt $bytes.Length) { throw "Debian archive header escapes package: $debPath" }
        $name = [System.Text.Encoding]::ASCII.GetString($bytes, $offset, 16).Trim().TrimEnd("/")
        $size = [int64]([System.Text.Encoding]::ASCII.GetString($bytes, $offset + 48, 10).Trim())
        $dataOffset = $offset + 60
        if ($dataOffset + $size -gt $bytes.Length) { throw "Debian archive entry escapes package: $debPath" }
        if ($name -like "data.tar.*") {
            $dataArchive = Join-Path $targetDirectory $name
            [System.IO.File]::WriteAllBytes($dataArchive, $bytes[$dataOffset..($dataOffset + $size - 1)])
            break
        }
        $offset = $dataOffset + $size
        if (($offset % 2) -ne 0) { $offset++ }
    }
    if ($null -eq $dataArchive) { throw "Debian package has no data archive: $debPath" }
    foreach ($entry in $entries) {
        $relative = ConvertTo-HipSharpRelativePath $entry
        & tar -xf $dataArchive -C $targetDirectory -- ("./" + $relative)
        if ($LASTEXITCODE -ne 0) { throw "Unable to extract declared source file '$relative' from $debPath." }
    }
}

$cacheRoot = Resolve-UnderRepository $CacheDirectory
$stagingRoot = Resolve-UnderRepository $StagingDirectory
New-Item -ItemType Directory -Force -Path $cacheRoot | Out-Null
$keyPath = Join-Path $cacheRoot "rocm.gpg.key"
$inReleasePath = Join-Path $cacheRoot "InRelease"
$packagesPath = Join-Path $cacheRoot "Packages.gz"
Download-Verified $runtimeManifest.source.signingKeyUrl $keyPath $runtimeManifest.source.signingKeySha256
Download-Verified $runtimeManifest.source.inReleaseUrl $inReleasePath $runtimeManifest.source.inReleaseSha256
Download-Verified $runtimeManifest.source.packagesIndexUrl $packagesPath $runtimeManifest.source.packagesIndexSha256

$gpg = Get-Executable $GpgPath "gpg"
$gpgv = Get-Executable $GpgvPath "gpgv"
$fingerprint = (& $gpg --show-keys --with-colons $keyPath | Where-Object { $_ -like "fpr:*" } | Select-Object -First 1).Split(":")[9]
if ($fingerprint -ne $runtimeManifest.source.signingKeyFingerprint) { throw "AMD archive key fingerprint mismatch. Expected $($runtimeManifest.source.signingKeyFingerprint), actual $fingerprint." }
$keyringPath = Join-Path $cacheRoot "rocm-archive-keyring.gpg"
& $gpg --batch --yes --dearmor --output $keyringPath $keyPath
if ($LASTEXITCODE -ne 0) { throw "Unable to create a temporary AMD archive keyring." }
& $gpgv --keyring (Convert-GpgPath $keyringPath $gpgv) (Convert-GpgPath $inReleasePath $gpgv)
if ($LASTEXITCODE -ne 0) { throw "AMD InRelease signature verification failed." }

$inRelease = Get-Content -Raw -LiteralPath $inReleasePath
$indexLine = @($inRelease -split "\r?\n" | Where-Object { $_ -match "^\s*$($runtimeManifest.source.packagesIndexSha256)\s+\d+\s+main/binary-amd64/Packages\.gz$" })
if ($indexLine.Count -ne 1) { throw "Signed InRelease metadata does not pin the expected Packages.gz hash." }

$indexPackages = Get-IndexPackages $packagesPath
$downloads = Join-Path $cacheRoot "downloads"
New-Item -ItemType Directory -Force -Path $downloads | Out-Null
foreach ($package in @($runtimeManifest.packages)) {
    if (-not $indexPackages.ContainsKey($package.name)) { throw "Signed package index has no entry for $($package.name)." }
    $index = $indexPackages[$package.name]
    foreach ($pair in (@{ Version = $package.version; Architecture = $package.architecture; SHA256 = $package.sha256; Size = [string]$package.size }).GetEnumerator()) {
        if ([string]$index[$pair.Key] -ne [string]$pair.Value) { throw "Signed metadata mismatch for $($package.name) field $($pair.Key)." }
    }
    $expectedUrl = $runtimeManifest.source.repositoryUrl.TrimEnd("/") + "/" + $index.Filename
    if ($package.url -ne $expectedUrl) { throw "Manifest URL does not match the signed package index for $($package.name)." }
    $packageUri = [Uri]$package.url
    $destination = Join-Path $downloads ([System.IO.Path]::GetFileName($packageUri.LocalPath))
    Download-Verified $package.url $destination $package.sha256
    if ((Get-Item -LiteralPath $destination).Length -ne [int64]$package.size) { throw "Package size mismatch for $($package.name)." }
}

if (-not $VerifyOnly) {
    if (Test-Path -LiteralPath $stagingRoot) { Remove-Item -LiteralPath $stagingRoot -Recurse -Force }
    New-Item -ItemType Directory -Force -Path $stagingRoot | Out-Null
    $extractRoot = Join-Path $stagingRoot ".extract"
    New-Item -ItemType Directory -Force -Path $extractRoot | Out-Null
    foreach ($package in @($runtimeManifest.packages)) {
        $sourceEntries = @($runtimeManifest.files | Where-Object { $_.sourcePackage -eq $package.name } | ForEach-Object { $_.sourcePath }) + @($runtimeManifest.licenses | Where-Object { $_.sourcePackage -eq $package.name } | ForEach-Object { $_.sourcePath })
        if ($sourceEntries.Count -eq 0) { continue }
        $packageRoot = Join-Path $extractRoot $package.name
        New-Item -ItemType Directory -Force -Path $packageRoot | Out-Null
        $debPath = Join-Path $downloads ([System.IO.Path]::GetFileName(([Uri]$package.url).LocalPath))
        Extract-DebEntries $debPath $packageRoot @($sourceEntries | Sort-Object -Unique)
        foreach ($file in @($runtimeManifest.files | Where-Object { $_.sourcePackage -eq $package.name })) {
            $sourcePath = Join-Path $packageRoot ($file.sourcePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf)) { throw "Declared native source file is missing: $($file.sourcePath)" }
            if ((Get-HipSharpSha256 $sourcePath) -ne $file.sha256 -or (Get-Item -LiteralPath $sourcePath).Length -ne [int64]$file.size) { throw "Native source file hash or size mismatch: $($file.sourcePath)" }
            if (-not $file.ContainsKey("aliasFor")) {
                $elf = Get-HipSharpElfDynamicInfo $sourcePath
                if ($elf.Soname -ne $file.soname -or $elf.Rpath -ne $file.rpath -or (@($elf.Needed) -join "|") -ne (@($file.needed | Sort-Object) -join "|")) { throw "ELF metadata mismatch for $($file.path)." }
            }
            $targetPath = Join-Path $stagingRoot ($file.path.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
            Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
        }
        foreach ($license in @($runtimeManifest.licenses | Where-Object { $_.sourcePackage -eq $package.name })) {
            $sourcePath = Join-Path $packageRoot ($license.sourcePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
            if (-not (Test-Path -LiteralPath $sourcePath -PathType Leaf) -or (Get-HipSharpSha256 $sourcePath) -ne $license.sha256) { throw "License hash mismatch: $($license.sourcePath)" }
            $targetPath = Join-Path $stagingRoot ($license.packagePath.Replace("/", [System.IO.Path]::DirectorySeparatorChar))
            New-Item -ItemType Directory -Force -Path (Split-Path -Parent $targetPath) | Out-Null
            Copy-Item -LiteralPath $sourcePath -Destination $targetPath -Force
        }
    }
    Copy-Item -LiteralPath $manifestInfo.Path -Destination (Join-Path $stagingRoot "runtime-manifest.json")
    $sbom = Join-Path (Split-Path -Parent $manifestInfo.Path) $runtimeManifest.sbom.path
    if (-not (Test-Path -LiteralPath $sbom) -or (Get-HipSharpSha256 $sbom) -ne $runtimeManifest.sbom.sha256) { throw "SBOM is missing or does not match its manifest hash." }
    Copy-Item -LiteralPath $sbom -Destination (Join-Path $stagingRoot ([System.IO.Path]::GetFileName($runtimeManifest.sbom.path)))
    Remove-Item -LiteralPath $extractRoot -Recurse -Force
}

Write-Host "Runtime provenance and closure preparation passed for $($runtimeManifest.packageId) $($runtimeManifest.packageVersion)."
