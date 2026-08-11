[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$PackagePath,
    [string]$Manifest = "nuget/runtime-manifests/linux-x64.json",
    [string]$OutputDirectory = "artifacts/runtime-package-audit",
    [switch]$Candidate,
    [string]$ExpectedRepositoryCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$package = (Resolve-Path -LiteralPath $PackagePath).Path
$audit = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repositoryRoot $OutputDirectory }
New-Item -ItemType Directory -Force -Path $audit | Out-Null

if ($Candidate) {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $candidateManifestPath = Join-Path $audit "embedded-candidate-manifest.json"
    $candidateArchive = [System.IO.Compression.ZipFile]::OpenRead($package)
    try {
        $candidateEntry = @($candidateArchive.Entries | Where-Object { $_.FullName.Replace("\", "/") -eq "runtime-manifest.json" })
        if ($candidateEntry.Count -ne 1) { throw "Candidate package must contain exactly one runtime-manifest.json." }
        [System.IO.Compression.ZipFileExtensions]::ExtractToFile($candidateEntry[0], $candidateManifestPath, $true)
    } finally { $candidateArchive.Dispose() }
    $manifestPath = $candidateManifestPath
} else {
    $manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repositoryRoot $Manifest }
}
$manifestInfo = Get-HipSharpRuntimeManifest $manifestPath
$runtimeManifest = $manifestInfo.Value
$gitSha = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitSha -notmatch "^[0-9a-f]{40}$") {
    throw "Unable to resolve the current Git SHA."
}
$packageRepositoryCommit = if ([string]::IsNullOrWhiteSpace($ExpectedRepositoryCommit)) { $gitSha } else { $ExpectedRepositoryCommit }
if ($packageRepositoryCommit -notmatch "^[0-9a-f]{40}$") {
    throw "ExpectedRepositoryCommit must be a lowercase 40-character Git SHA."
}
$isRegression = $packageRepositoryCommit -ne $gitSha
if ($Candidate -and $isRegression) {
    throw "Candidate audits cannot target a historical repository commit."
}
if ($isRegression) {
    & git -C $repositoryRoot cat-file -e "$packageRepositoryCommit^{commit}" 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw "The historical runtime package commit is not present in this repository."
    }
    & git -C $repositoryRoot merge-base --is-ancestor $packageRepositoryCommit $gitSha
    if ($LASTEXITCODE -ne 0) {
        throw "The historical runtime package commit must be an ancestor of the current Git SHA."
    }
}
Assert-HipSharpRuntimeManifest $runtimeManifest -RequirePackable:(-not $Candidate)
if ($Candidate -and ($runtimeManifest.packEnabled -or $runtimeManifest.verified -or $runtimeManifest.verification.packageAuditVerified -or $runtimeManifest.verification.gpuValidated)) {
    throw "A candidate audit requires an explicitly unverified runtime manifest."
}
if ($Candidate -and ($runtimeManifest.candidate.gitSha -ne $gitSha -or $runtimeManifest.candidate.publishable -or $runtimeManifest.candidate.status -ne "local-unverified-internal-candidate")) {
    throw "Candidate manifest does not bind the current Git SHA and non-publishable status."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $entries = @($archive.Entries | Where-Object { -not $_.FullName.EndsWith("/") })
    $entryNames = @($entries | ForEach-Object { $_.FullName.Replace("\", "/") })
    if (@($entryNames | Group-Object | Where-Object Count -gt 1).Count -ne 0) { throw "Runtime package has duplicate paths." }
    $expected = @($runtimeManifest.files | ForEach-Object path) + @($runtimeManifest.licenses | ForEach-Object packagePath) + @("runtime-manifest.json", [System.IO.Path]::GetFileName($runtimeManifest.sbom.path), "README.md", "LICENSE", "logo.jpg")
    foreach ($path in $expected) { if ($entryNames -notcontains $path) { throw "Runtime package is missing $path." } }
    $unexpected = @($entryNames | Where-Object {
        $_ -notin $expected -and
        $_ -ne "_rels/.rels" -and
        $_ -ne "[Content_Types].xml" -and
        $_ -notmatch "^[^/]+\.nuspec$" -and
        $_ -notmatch "^package/services/metadata/core-properties/[^/]+\.psmdcp$"
    })
    if ($unexpected.Count -gt 0) { throw "Runtime package contains files outside the exact allowlist: $($unexpected -join ', ')" }
    $forbidden = @($entryNames | Where-Object { $_ -match "(^|/)(include|cmake|bin|libexec|tests?|artifacts|Radeon_Cloud|plan|diary)(/|$)" -or $_ -match "\.(a|h|hpp|deb|ddeb|pdb|hsaco|bc)$" -or $_ -match "^[A-Za-z]:" })
    if ($forbidden.Count -gt 0) { throw "Runtime package contains forbidden payload: $($forbidden -join ', ')" }
    foreach ($file in @($runtimeManifest.files)) {
        $entry = @($entries | Where-Object { $_.FullName.Replace("\", "/") -eq $file.path })[0]
        $stream = $entry.Open()
        try { $hash = [System.Security.Cryptography.SHA256]::HashData($stream); $actual = [Convert]::ToHexString($hash).ToLowerInvariant() } finally { $stream.Dispose() }
        if ($actual -ne $file.sha256 -or $entry.Length -ne [int64]$file.size) { throw "Runtime package hash/size mismatch: $($file.path)" }
    }
    foreach ($license in @($runtimeManifest.licenses)) {
        $entry = @($entries | Where-Object { $_.FullName.Replace("\", "/") -eq $license.packagePath })[0]
        $stream = $entry.Open()
        try { $actual = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
        if ($actual -ne $license.sha256) { throw "Runtime package license hash mismatch: $($license.packagePath)" }
    }
    foreach ($metadataFile in @{ "runtime-manifest.json" = (Get-HipSharpSha256 $manifestInfo.Path); ([System.IO.Path]::GetFileName($runtimeManifest.sbom.path)) = $runtimeManifest.sbom.sha256 }.GetEnumerator()) {
        $entry = @($entries | Where-Object { $_.FullName.Replace("\", "/") -eq $metadataFile.Key })[0]
        $stream = $entry.Open()
        try { $actual = [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
        if ($actual -ne $metadataFile.Value) { throw "Runtime package metadata hash mismatch: $($metadataFile.Key)" }
    }
    $nuspecEntry = @($entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
    if ($nuspecEntry.Count -ne 1) { throw "Runtime package must contain exactly one nuspec." }
    $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
    try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $metadata = $nuspec.package.metadata
    if ($metadata.id -ne $runtimeManifest.packageId -or $metadata.version -ne $runtimeManifest.packageVersion) { throw "Runtime nuspec ID/version does not match the manifest." }
    if ($metadata.readme -ne "README.md" -or $metadata.icon -ne "logo.jpg" -or $metadata.license.type -ne "file") { throw "Runtime nuspec README/icon/license metadata is invalid." }
    if ($metadata.repository.url -ne "https://github.com/guojin-yan/HIP-CSharp-API" -or $metadata.repository.type -ne "git") { throw "Runtime nuspec repository metadata is invalid." }
    if ($metadata.repository.commit -ne $packageRepositoryCommit) { throw "Runtime nuspec repository commit does not match the expected Git SHA." }
    $packageDependencies = @($metadata.SelectNodes("*[local-name()='dependencies']/*[local-name()='group']/*[local-name()='dependency']"))
    if ($packageDependencies.Count -gt 0) { throw "Single-package runtime candidate must not have NuGet package dependencies." }
    $managed = @($entryNames | Where-Object { $_ -match "\.dll$" })
    if ($managed.Count -gt 0) { throw "Runtime package must not contain managed assemblies: $($managed -join ', ')" }
} finally { $archive.Dispose() }

if ((Get-Item -LiteralPath $package).Length -ge [int64]$runtimeManifest.size.nugetLimitBytes) { throw "Runtime nupkg meets or exceeds the configured NuGet package-size gate." }

$auditMode = if ($Candidate) { "isolated-gpu-candidate" } elseif ($isRegression) { "historical-regression" } else { "verified-final" }
$report = [ordered]@{
    package = [System.IO.Path]::GetFileName($package)
    packageVersion = $runtimeManifest.packageVersion
    sha256 = Get-HipSharpSha256 $package
    size = (Get-Item -LiteralPath $package).Length
    contentAudit = "passed"
    mode = $auditMode
    publishable = (-not $Candidate) -and (-not $isRegression)
    currentGitCommit = $gitSha
    packageRepositoryCommit = $packageRepositoryCommit
    rid = $runtimeManifest.rid
    packageId = $runtimeManifest.packageId
    manifestSha256 = Get-HipSharpSha256 $manifestInfo.Path
    sbomSha256 = $runtimeManifest.sbom.sha256
    sourceManifestSha256 = if ($Candidate) { $runtimeManifest.candidate.sourceManifestSha256 } else { $null }
}
$report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $audit "runtime-package-audit.json") -Encoding utf8NoBOM
Write-Host "Runtime package audit passed: $package"
