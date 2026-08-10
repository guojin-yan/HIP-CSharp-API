[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("linux-x64", "win-x64")][string]$Rid,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutputDirectory = "artifacts/runtime-packages",
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64",
    [switch]$Candidate,
    [switch]$Offline
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $repositoryRoot "nuget/runtime-manifests/$Rid.json"
if ($Rid -ne "linux-x64") { throw "HIPSHARP1001: Windows runtime packaging is disabled." }
& (Join-Path $PSScriptRoot "prepare-runtime.ps1") -Manifest $manifest -StagingDirectory $StagingDirectory -Offline:$Offline
if ($LASTEXITCODE -ne 0) { throw "Runtime staging failed." }

Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$runtimeManifest = (Get-HipSharpRuntimeManifest $manifest).Value
Assert-HipSharpRuntimeManifest $runtimeManifest -RequirePackable:(-not $Candidate)
if ($runtimeManifest.packageVersion -ne $Version) { throw "Runtime package version must equal the verified manifest version." }

$output = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repositoryRoot $OutputDirectory }
$staging = if ([System.IO.Path]::IsPathRooted($StagingDirectory)) { [System.IO.Path]::GetFullPath($StagingDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StagingDirectory)) }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = Join-Path $repositoryRoot "pack/JYPPX.HipSharp.Runtime.linux-x64.csproj"
$gitSha = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $gitSha -notmatch "^[0-9a-f]{40}$") { throw "A 40-character Git SHA is required for runtime packaging." }
$gitStatus = @(& git -C $repositoryRoot status --porcelain=v1)
if ($LASTEXITCODE -ne 0 -or $gitStatus.Count -ne 0) { throw "Runtime packaging requires a clean Git worktree." }

$package = Join-Path $output "$($runtimeManifest.packageId).$Version.nupkg"
if (Test-Path -LiteralPath $package -PathType Leaf) { [System.IO.File]::Delete($package) }
$arguments = @("pack", $project, "--configuration", "Release", "--no-restore", "--output", $output, "-p:PackageVersion=$Version", "-p:RuntimeStagingPath=$staging", "-p:RepositoryCommit=$gitSha", "-p:RepositoryBranch=main")
if ($Candidate) {
    $attestationDirectory = Join-Path $repositoryRoot "artifacts/runtime-candidate"
    New-Item -ItemType Directory -Force -Path $attestationDirectory | Out-Null
    $attestationPath = Join-Path $attestationDirectory "candidate-attestation.json"
    $attestation = [ordered]@{
        schemaVersion = 1
        mode = "isolated-gpu-candidate"
        publishable = $false
        gitSha = $gitSha
        packageId = $runtimeManifest.packageId
        packageVersion = $runtimeManifest.packageVersion
        rid = $runtimeManifest.rid
        manifestSha256 = Get-HipSharpSha256 $manifest
        sbomSha256 = $runtimeManifest.sbom.sha256
        stagingDigestSha256 = Get-HipSharpStagingDigest $staging
    }
    $attestation | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath $attestationPath -Encoding utf8NoBOM
    $attestationSha256 = Get-HipSharpSha256 $attestationPath
    $arguments += "-p:RuntimeCandidateAttestationPath=$attestationPath", "-p:RuntimeCandidateAttestationSha256=$attestationSha256"
}

& dotnet @arguments
if ($LASTEXITCODE -ne 0) { throw "Runtime package generation failed." }
if (-not (Test-Path -LiteralPath $package -PathType Leaf)) { throw "Expected runtime package was not generated: $package" }
& (Join-Path $PSScriptRoot "verify-runtime-package.ps1") -PackagePath $package -Manifest "nuget/runtime-manifests/$Rid.json" -Candidate:$Candidate
if ($LASTEXITCODE -ne 0) { throw "Runtime package audit failed." }
Write-Output $package
