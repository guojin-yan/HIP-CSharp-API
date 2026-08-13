[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("linux-x64", "win-x64")][string]$Rid,
    [string]$Version,
    [string]$OutputDirectory = "artifacts/runtime-packages",
    [string]$StagingDirectory,
    [switch]$Candidate,
    [switch]$Offline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
if ($Rid -ne "linux-x64") { throw "HIPSHARP1001: Windows runtime packaging is disabled." }

Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$Version = Get-HipSharpVersion -Kind LinuxRuntime -Override $Version -RepositoryRoot $repositoryRoot
$sourceManifestPath = Join-Path $repositoryRoot "nuget/runtime-manifests/$Rid.json"
$sourceManifestInfo = Get-HipSharpRuntimeManifest $sourceManifestPath
if ($sourceManifestInfo.Value.packageVersion -ne $Version) { throw "Runtime package version must equal the central and manifest version." }

$gitSha = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitSha -notmatch "^[0-9a-f]{40}$") { throw "A 40-character Git SHA is required for runtime packaging." }
$gitStatus = @(& git -C $repositoryRoot status --porcelain=v1)
if ($LASTEXITCODE -ne 0 -or $gitStatus.Count -ne 0) { throw "Runtime packaging requires a clean Git worktree." }

$manifestPath = $sourceManifestPath
$stagingValue = if ([string]::IsNullOrWhiteSpace($StagingDirectory)) { "eng/native-assets/staging/linux-x64" } else { $StagingDirectory }
if ($Candidate) {
    $candidateDirectory = Join-Path $repositoryRoot "artifacts/runtime-candidate"
    New-Item -ItemType Directory -Force -Path $candidateDirectory | Out-Null
    $manifestPath = Join-Path $candidateDirectory "linux-x64.json"
    $candidateManifest = Get-Content -Raw -LiteralPath $sourceManifestPath | ConvertFrom-Json -AsHashtable
    $candidateManifest.packEnabled = $false
    $candidateManifest.verified = $false
    $candidateManifest.verification.packageAuditVerified = $false
    $candidateManifest.verification.gpuValidated = $false
    $candidateManifest.verification.validationSha256 = $null
    $candidateManifest.verification.environment = $null
    $candidateManifest.verification.Remove("promotionReceipt")
    $candidateManifest.verification.reason = "M8.1 internal candidate: local source, closure, license, SBOM, content, and size gates must pass again; Owner-authorized exact-package GPU validation is pending."
    $candidateManifest.candidate = [ordered]@{
        schemaVersion = 1
        gitSha = $gitSha
        coreVersion = Get-HipSharpVersion -Kind Core -RepositoryRoot $repositoryRoot
        packageVersion = $Version
        publishable = $false
        status = "local-unverified-internal-candidate"
        sourceManifestSha256 = Get-HipSharpSha256 $sourceManifestPath
    }
    $candidateManifest | ConvertTo-Json -Depth 30 | Set-Content -LiteralPath $manifestPath -Encoding utf8NoBOM
    Copy-Item -LiteralPath (Join-Path (Split-Path -Parent $sourceManifestPath) $sourceManifestInfo.Value.sbom.path) `
        -Destination (Join-Path $candidateDirectory $sourceManifestInfo.Value.sbom.path) -Force
    if ([string]::IsNullOrWhiteSpace($StagingDirectory)) { $stagingValue = "eng/native-assets/staging/m8.1-linux-x64-candidate" }
}

$staging = if ([System.IO.Path]::IsPathRooted($stagingValue)) {
    [System.IO.Path]::GetFullPath($stagingValue)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $stagingValue))
}
& (Join-Path $PSScriptRoot "prepare-runtime.ps1") -Manifest $manifestPath -StagingDirectory $staging -Offline:$Offline
if ($LASTEXITCODE -ne 0) { throw "Runtime staging failed." }

$runtimeManifest = (Get-HipSharpRuntimeManifest $manifestPath).Value
Assert-HipSharpRuntimeManifest $runtimeManifest -RequirePackable:(-not $Candidate)
$output = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repositoryRoot $OutputDirectory }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = Join-Path $repositoryRoot "pack/JYPPX.ROCm.HipSharp.Runtime.linux-x64.csproj"
$package = Join-Path $output "$($runtimeManifest.packageId).$Version.nupkg"
if (Test-Path -LiteralPath $package -PathType Leaf) { [System.IO.File]::Delete($package) }

$arguments = @(
    "pack", $project, "--configuration", "Release", "--no-restore", "--output", $output,
    "-p:PackageVersion=$Version", "-p:RuntimeManifestPath=$manifestPath", "-p:RuntimeStagingPath=$staging",
    "-p:RepositoryCommit=$gitSha", "-p:RepositoryBranch=main"
)
if ($Candidate) {
    $attestationPath = Join-Path (Split-Path -Parent $manifestPath) "candidate-attestation.json"
    $attestation = [ordered]@{
        schemaVersion = 1
        mode = "isolated-gpu-candidate"
        publishable = $false
        gitSha = $gitSha
        coreVersion = Get-HipSharpVersion -Kind Core -RepositoryRoot $repositoryRoot
        packageId = $runtimeManifest.packageId
        packageVersion = $runtimeManifest.packageVersion
        rid = $runtimeManifest.rid
        sourceManifestSha256 = Get-HipSharpSha256 $sourceManifestPath
        manifestSha256 = Get-HipSharpSha256 $manifestPath
        sbomSha256 = $runtimeManifest.sbom.sha256
        stagingDigestSha256 = Get-HipSharpStagingDigest $staging
    }
    $attestation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $attestationPath -Encoding utf8NoBOM
    $attestationSha256 = Get-HipSharpSha256 $attestationPath
    $arguments += "-p:RuntimeCandidateAttestationPath=$attestationPath", "-p:RuntimeCandidateAttestationSha256=$attestationSha256"
} else {
    $receiptPath = Join-Path $repositoryRoot $runtimeManifest.verification.promotionReceipt.path
    $receiptSha256 = Get-HipSharpSha256 $receiptPath
    $finalDirectory = Join-Path $repositoryRoot "artifacts/runtime-final"
    New-Item -ItemType Directory -Force -Path $finalDirectory | Out-Null
    $finalAttestationPath = Join-Path $finalDirectory "final-pack-attestation.json"
    $finalAttestation = [ordered]@{
        schemaVersion = 1
        mode = "verified-final-local"
        publishable = $false
        releaseAuthorized = $false
        gitSha = $gitSha
        packageId = $runtimeManifest.packageId
        packageVersion = $runtimeManifest.packageVersion
        rid = $runtimeManifest.rid
        manifestSha256 = Get-HipSharpSha256 $manifestPath
        promotionReceiptSha256 = $receiptSha256
        sbomSha256 = $runtimeManifest.sbom.sha256
        stagingDigestSha256 = Get-HipSharpStagingDigest $staging
    }
    $finalAttestation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $finalAttestationPath -Encoding utf8NoBOM
    $finalAttestationSha256 = Get-HipSharpSha256 $finalAttestationPath
    $arguments += `
        "-p:RuntimePromotionReceiptPath=$receiptPath", "-p:RuntimePromotionReceiptSha256=$receiptSha256", `
        "-p:RuntimeFinalAttestationPath=$finalAttestationPath", "-p:RuntimeFinalAttestationSha256=$finalAttestationSha256"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Runtime package generation failed." }
if (-not (Test-Path -LiteralPath $package -PathType Leaf)) { throw "Expected runtime package was not generated: $package" }
& (Join-Path $PSScriptRoot "verify-runtime-package.ps1") -PackagePath $package -Candidate:$Candidate
if ($LASTEXITCODE -ne 0) { throw "Runtime package audit failed." }
Write-Output $package
