[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateSet("linux-x64", "win-x64")][string]$Rid,
    [Parameter(Mandatory = $true)][string]$Version,
    [string]$OutputDirectory = "artifacts/runtime-packages",
    [string]$StagingDirectory = "eng/native-assets/staging/linux-x64"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifest = Join-Path $repositoryRoot "nuget/runtime-manifests/$Rid.json"
if ($Rid -ne "linux-x64") { throw "HIPSHARP1001: Windows runtime packaging is disabled." }
& (Join-Path $PSScriptRoot "prepare-runtime.ps1") -Manifest $manifest -StagingDirectory $StagingDirectory
if ($LASTEXITCODE -ne 0) { throw "Runtime staging failed." }

Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$runtimeManifest = (Get-HipSharpRuntimeManifest $manifest).Value
Assert-HipSharpRuntimeManifest $runtimeManifest -RequirePackable
if ($runtimeManifest.packageVersion -ne $Version) { throw "Runtime package version must equal the verified manifest version." }

$output = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) { [System.IO.Path]::GetFullPath($OutputDirectory) } else { Join-Path $repositoryRoot $OutputDirectory }
$staging = if ([System.IO.Path]::IsPathRooted($StagingDirectory)) { [System.IO.Path]::GetFullPath($StagingDirectory) } else { [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $StagingDirectory)) }
New-Item -ItemType Directory -Force -Path $output | Out-Null
$project = Join-Path $repositoryRoot "pack/JYPPX.HipSharp.Runtime.linux-x64.csproj"
& dotnet pack $project --configuration Release --no-restore --output $output -p:PackageVersion=$Version -p:RuntimeStagingPath=$staging
if ($LASTEXITCODE -ne 0) { throw "Runtime package generation failed." }
