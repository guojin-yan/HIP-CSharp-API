[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$CorePackagePath,
    [string]$RuntimePackagePath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
$coreVersion = Get-HipSharpVersion -Kind Core -RepositoryRoot $repositoryRoot
$runtimeVersion = Get-HipSharpVersion -Kind LinuxRuntime -RepositoryRoot $repositoryRoot
$runtimeId = "JYPPX.ROCm.HIP.CSharp.API.Runtime.linux-x64"
$expectedRuntimeSha256 = "21D0A2E511964923DE4BE2C7F1BF02CE19E9ABD9E9BF535CB915C7D7C81B5799"
$resolvedCore = (Resolve-Path -LiteralPath $CorePackagePath).Path
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$testRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "core-runtime-pairing"))
$artifactsPrefix = $artifactsRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
if ($testRoot -eq $artifactsRoot -or -not $testRoot.StartsWith($artifactsPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Runtime pairing output must remain under repository artifacts."
}

if (Test-Path -LiteralPath $testRoot) {
    Remove-Item -LiteralPath $testRoot -Recurse -Force
}
$feed = Join-Path $testRoot "feed"
$packages = Join-Path $testRoot "packages"
New-Item -ItemType Directory -Force -Path $feed, $packages | Out-Null
Copy-Item -LiteralPath $resolvedCore -Destination $feed

if ([string]::IsNullOrWhiteSpace($RuntimePackagePath)) {
    $RuntimePackagePath = Join-Path $feed "$runtimeId.$runtimeVersion.nupkg"
    $runtimeUrl = "https://api.nuget.org/v3-flatcontainer/$($runtimeId.ToLowerInvariant())/$runtimeVersion/$($runtimeId.ToLowerInvariant()).$runtimeVersion.nupkg"
    Invoke-WebRequest -Uri $runtimeUrl -OutFile $RuntimePackagePath -UseBasicParsing
}
$resolvedRuntime = (Resolve-Path -LiteralPath $RuntimePackagePath).Path
$runtimeHash = (Get-FileHash -LiteralPath $resolvedRuntime -Algorithm SHA256).Hash
if ($runtimeHash -ne $expectedRuntimeSha256) {
    throw "Public Runtime package SHA-256 mismatch. Expected $expectedRuntimeSha256; found $runtimeHash."
}
$feedFull = [System.IO.Path]::GetFullPath($feed).TrimEnd([System.IO.Path]::DirectorySeparatorChar)
$feedPrefix = $feedFull + [System.IO.Path]::DirectorySeparatorChar
if (-not $resolvedRuntime.StartsWith($feedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
    Copy-Item -LiteralPath $resolvedRuntime -Destination $feed
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
function Get-PackageMetadata([string]$PackagePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $nuspecEntries = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($nuspecEntries.Count -ne 1) { throw "Expected exactly one nuspec in $PackagePath." }
        $reader = [System.IO.StreamReader]::new($nuspecEntries[0].Open())
        try { [xml]$nuspec = $reader.ReadToEnd() } finally { $reader.Dispose() }
        return [pscustomobject]@{ Id = [string]$nuspec.package.metadata.id; Version = [string]$nuspec.package.metadata.version }
    }
    finally {
        $archive.Dispose()
    }
}

$coreMetadata = Get-PackageMetadata $resolvedCore
if ($coreMetadata.Id -ne "JYPPX.ROCm.HIP.CSharp.API" -or $coreMetadata.Version -ne $coreVersion) {
    throw "Unexpected Core package identity: $($coreMetadata.Id) $($coreMetadata.Version)."
}
$runtimeMetadata = Get-PackageMetadata $resolvedRuntime
if ($runtimeMetadata.Id -ne $runtimeId -or $runtimeMetadata.Version -ne $runtimeVersion) {
    throw "Unexpected Runtime package identity: $($runtimeMetadata.Id) $($runtimeMetadata.Version)."
}

function Get-RuntimeNativeAssets([string]$PackagePath) {
    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)
    try {
        $assets = @($archive.Entries | Where-Object { $_.FullName -match '^runtimes/linux-x64/native/[^/]+$' } | ForEach-Object {
            $stream = $_.Open()
            $sha = [System.Security.Cryptography.SHA256]::Create()
            try {
                $hash = [System.BitConverter]::ToString($sha.ComputeHash($stream)).Replace("-", "")
            }
            finally {
                $sha.Dispose()
                $stream.Dispose()
            }
            [pscustomobject]@{ Name = [System.IO.Path]::GetFileName($_.FullName); Sha256 = $hash; Size = $_.Length }
        })
        return @($assets | Sort-Object Name)
    }
    finally {
        $archive.Dispose()
    }
}

$expectedNativeAssets = @(Get-RuntimeNativeAssets $resolvedRuntime)
if ($expectedNativeAssets.Count -ne 14 -or @($expectedNativeAssets.Name | Select-Object -Unique).Count -ne 14) {
    throw "Public Runtime package must contain exactly 14 uniquely named native assets; found $($expectedNativeAssets.Count)."
}

$escapedFeed = [System.Security.SecurityElement]::Escape($feed)
$nugetConfig = Join-Path $testRoot "NuGet.config"
[System.IO.File]::WriteAllText($nugetConfig, @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="Exact candidate feed" value="$escapedFeed" />
    <add key="nuget.org framework packs" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="Exact candidate feed">
      <package pattern="JYPPX.ROCm.HIP.CSharp.API" />
      <package pattern="$runtimeId" />
    </packageSource>
    <packageSource key="nuget.org framework packs">
      <package pattern="Microsoft.*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
"@)
foreach ($name in @("Directory.Build.props", "Directory.Build.targets", "Directory.Packages.props")) {
    [System.IO.File]::WriteAllText((Join-Path $testRoot $name), "<Project />`n")
}

function New-Consumer([string]$Name, [bool]$IncludeRuntime) {
    $directory = Join-Path $testRoot $Name
    New-Item -ItemType Directory -Force -Path $directory | Out-Null
    $runtimeReference = if ($IncludeRuntime) {
        "    <PackageReference Include=`"$runtimeId`" Version=`"$runtimeVersion`" />"
    } else { "" }
    [System.IO.File]::WriteAllText((Join-Path $directory "Consumer.csproj"), @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JYPPX.ROCm.HIP.CSharp.API" Version="$coreVersion" />
$runtimeReference
  </ItemGroup>
</Project>
"@)
    [System.IO.File]::WriteAllText((Join-Path $directory "Program.cs"), "using JYPPX.ROCm.HipSharp;`nreturn typeof(HipRuntime).Assembly.GetName().Name == `"JYPPX.ROCm.HIP.CSharp.API`" ? 0 : 1;`n")

    & dotnet restore (Join-Path $directory "Consumer.csproj") --configfile $nugetConfig --packages $packages --force --no-cache --verbosity minimal | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Isolated restore failed for $Name." }
    & dotnet build (Join-Path $directory "Consumer.csproj") --configuration $Configuration --no-restore -p:RestorePackagesPath=$packages | Out-Host
    if ($LASTEXITCODE -ne 0) { throw "Isolated build failed for $Name." }
    Write-Output -NoEnumerate $directory
}

$pairing = New-Consumer "package-only" $true
$systemNative = New-Consumer "system-native-core-only" $false
$pairingOutput = Join-Path $pairing "bin/$Configuration/net10.0/linux-x64"
$systemOutput = Join-Path $systemNative "bin/$Configuration/net10.0/linux-x64"
$expectedNames = @($expectedNativeAssets.Name)
$pairingNative = @(Get-ChildItem -LiteralPath $pairingOutput -File -Recurse | Where-Object { $_.Name -in $expectedNames } | Sort-Object Name)
$systemNativeFiles = @(Get-ChildItem -LiteralPath $systemOutput -File -Recurse | Where-Object { $_.Name -in $expectedNames })
if ($pairingNative.Count -ne 14) {
    throw "Package-only consumer must contain exactly 14 Runtime native assets; found $($pairingNative.Count)."
}
if ($systemNativeFiles.Count -ne 0) {
    throw "Core-only system-native consumer unexpectedly contains Runtime native assets."
}

$nativeOutputAudit = @($pairingNative | ForEach-Object {
    $expected = $expectedNativeAssets | Where-Object Name -eq $_.Name
    $actualHash = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
    if ($actualHash -ne $expected.Sha256 -or $_.Length -ne $expected.Size) {
        throw "Runtime output asset differs from the public package: $($_.Name)."
    }
    [ordered]@{ name = $_.Name; size = $_.Length; sha256 = $actualHash }
})

$report = [ordered]@{
    schemaVersion = 1
    coreVersion = $coreVersion
    coreSha256 = (Get-FileHash -LiteralPath $resolvedCore -Algorithm SHA256).Hash
    runtimePackageId = $runtimeId
    runtimeVersion = $runtimeVersion
    runtimePublicSha256 = $runtimeHash
    packageSource = "source-mapped-local-target-packages-plus-nuget-framework-packs"
    packageOnly = [ordered]@{ restore = "passed"; build = "passed"; nativeAssetCount = $nativeOutputAudit.Count; nativeAssets = $nativeOutputAudit; execution = "not-run-on-windows" }
    systemNativeCoreOnly = [ordered]@{ restore = "passed"; build = "passed"; nativeAssetCount = $systemNativeFiles.Count; execution = "requires-compatible-system-rocm" }
    sourceBinFallback = $false
    stagingFallback = $false
    publishable = $false
    releaseAuthorized = $false
}
$reportPath = Join-Path $testRoot "pairing-audit.json"
[System.IO.File]::WriteAllText($reportPath, ($report | ConvertTo-Json -Depth 5) + "`n")
Write-Host "Core/Runtime pairing audit passed: package-only native assets=14; system-native Core-only native assets=0."
Write-Host "Audit report: $reportPath"
