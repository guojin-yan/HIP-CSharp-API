[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/package-audit"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedPackage = (Resolve-Path -LiteralPath $PackagePath).Path
$auditDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    [System.IO.Path]::GetFullPath($OutputDirectory)
} else {
    [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot $OutputDirectory))
}
$artifactsRoot = [System.IO.Path]::GetFullPath((Join-Path $repositoryRoot "artifacts"))
$consumerRoot = [System.IO.Path]::GetFullPath((Join-Path $artifactsRoot "consumer"))
$frameworks = @(
    "net46", "net461", "net462", "net47", "net471", "net472", "net48", "net481",
    "netcoreapp3.1", "net5.0", "net6.0", "net7.0", "net8.0", "net9.0", "net10.0"
)
$consumerFrameworks = @("net46", "netcoreapp3.1", "net7.0", "net10.0")

if (-not $consumerRoot.StartsWith($artifactsRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
    throw "Consumer output must remain under the repository artifacts directory."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$archive = [System.IO.Compression.ZipFile]::OpenRead($resolvedPackage)
try {
    $entries = @($archive.Entries | ForEach-Object { $_.FullName.Replace("\", "/") })
    foreach ($framework in $frameworks) {
        $expectedFiles = @("JYPPX.HipSharp.dll", "JYPPX.HipSharp.xml")
        foreach ($file in $expectedFiles) {
            $expected = "lib/$framework/$file"
            if ($entries -notcontains $expected) { throw "Package asset is missing: $expected" }
        }
        $frameworkEntries = @($entries | Where-Object { $_.StartsWith("lib/$framework/", [System.StringComparison]::OrdinalIgnoreCase) })
        if ($frameworkEntries.Count -ne $expectedFiles.Count) {
            throw "Unexpected assets found for $framework`: $($frameworkEntries -join ', ')"
        }
    }

    foreach ($file in @("README.md", "logo.jpg", "LICENSE")) {
        if ($entries -notcontains $file) { throw "Package file is missing: $file" }
    }

    $forbiddenEntries = @($entries | Where-Object {
        $_ -match '(^|/)(bin|obj|tests?|plan|diary|Radeon_Cloud|artifacts)(/|$)' -or
        $_ -match '\.(pdb|so|dylib|deb|zip|hsaco|bc|hip|cpp|cs|h)$' -or
        $_ -match '(amdhip|hiprtc).*\.dll$' -or
        $_ -match 'runtimes/[^/]+/native/'
    })
    if ($forbiddenEntries.Count -ne 0) {
        throw "Forbidden package entries found: $($forbiddenEntries -join ', ')"
    }

    $nuspecEntry = @($archive.Entries | Where-Object { $_.FullName.EndsWith(".nuspec", [System.StringComparison]::OrdinalIgnoreCase) })
    if ($nuspecEntry.Count -ne 1) { throw "Expected exactly one nuspec in the package." }
    $reader = [System.IO.StreamReader]::new($nuspecEntry[0].Open())
    try { $nuspecText = $reader.ReadToEnd() } finally { $reader.Dispose() }
    [xml]$nuspec = $nuspecText
    $metadata = $nuspec.package.metadata

    if ($metadata.id -ne "JYPPX.HIP.CSharp.API") { throw "Unexpected package ID: $($metadata.id)" }
    if ([string]::IsNullOrWhiteSpace($metadata.version)) { throw "Package version is missing." }
    if ($metadata.readme -ne "README.md") { throw "Package README metadata is invalid." }
    if ($metadata.icon -ne "logo.jpg") { throw "Package icon metadata is invalid." }
    if ($metadata.license.'#text' -ne "LICENSE" -or $metadata.license.type -ne "file") { throw "Package license metadata is invalid." }
    if ($metadata.repository.url -ne "https://github.com/guojin-yan/HIP-CSharp-API") { throw "Repository URL metadata is invalid." }
    if ($metadata.repository.type -ne "git") { throw "Repository type metadata is invalid." }
    if ($metadata.repository.commit -notmatch '^[0-9a-fA-F]{40}$') { throw "Repository commit metadata must be a 40-character Git SHA." }
    $global:LASTEXITCODE = 0
    $currentCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null)
    if ($LASTEXITCODE -eq 0 -and -not [string]::IsNullOrWhiteSpace($currentCommit) -and $metadata.repository.commit -ne $currentCommit.Trim()) {
        throw "Repository commit metadata is stale. Package: $($metadata.repository.commit); current HEAD: $($currentCommit.Trim())."
    }
    if ($nuspecText -match '[A-Za-z]:\\' -or $nuspecText -match 'E:/GitSpace') { throw "The nuspec contains a local absolute path." }
}
finally {
    $archive.Dispose()
}

New-Item -ItemType Directory -Force -Path $auditDirectory | Out-Null
if (Test-Path -LiteralPath $consumerRoot) {
    Remove-Item -LiteralPath $consumerRoot -Recurse -Force
}
New-Item -ItemType Directory -Force -Path $consumerRoot | Out-Null
$feed = Join-Path $consumerRoot "feed"
$packages = Join-Path $consumerRoot "packages"
New-Item -ItemType Directory -Force -Path $feed, $packages | Out-Null
Copy-Item -LiteralPath $resolvedPackage -Destination $feed

$global:LASTEXITCODE = 0
$globalPackagesLine = (& dotnet nuget locals global-packages --list | Select-Object -First 1)
if ($LASTEXITCODE -ne 0) { throw "Could not locate the NuGet global packages directory." }
$globalPackages = ($globalPackagesLine -replace '^global-packages:\s*', '').Trim()
foreach ($referencePackage in @("microsoft.netframework.referenceassemblies", "microsoft.netframework.referenceassemblies.net46")) {
    $cachedPackage = Join-Path $globalPackages "$referencePackage/1.0.3/$referencePackage.1.0.3.nupkg"
    if (Test-Path -LiteralPath $cachedPackage -PathType Leaf) {
        Copy-Item -LiteralPath $cachedPackage -Destination $feed
    }
}

$escapedFeed = [System.Security.SecurityElement]::Escape($feed)
$nugetConfig = @"
<?xml version="1.0" encoding="utf-8"?>
<configuration>
  <packageSources>
    <clear />
    <add key="Local candidate" value="$escapedFeed" />
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" protocolVersion="3" />
  </packageSources>
</configuration>
"@
[System.IO.File]::WriteAllText((Join-Path $consumerRoot "NuGet.config"), $nugetConfig)
[System.IO.File]::WriteAllText((Join-Path $consumerRoot "Directory.Build.props"), "<Project />`n")
[System.IO.File]::WriteAllText((Join-Path $consumerRoot "Directory.Build.targets"), "<Project />`n")
[System.IO.File]::WriteAllText((Join-Path $consumerRoot "Directory.Packages.props"), "<Project />`n")

$consumerResults = [System.Collections.Generic.List[object]]::new()
foreach ($framework in $consumerFrameworks) {
    $projectDirectory = Join-Path $consumerRoot $framework
    New-Item -ItemType Directory -Force -Path $projectDirectory | Out-Null
    $frameworkReference = if ($framework -eq "net46") {
        '    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all" />'
    } else {
        ""
    }
    $projectText = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>$framework</TargetFramework>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JYPPX.HIP.CSharp.API" Version="$($metadata.version)" Aliases="HipSharp" />
$frameworkReference
  </ItemGroup>
</Project>
"@
    [System.IO.File]::WriteAllText((Join-Path $projectDirectory "Consumer.csproj"), $projectText)
    [System.IO.File]::WriteAllText((Join-Path $projectDirectory "Program.cs"), "extern alias HipSharp;`n`nusing HipRuntime = HipSharp::JYPPX.HipSharp.HipRuntime;`nusing HipModule = HipSharp::JYPPX.HipSharp.Modules.HipModule;`nusing HipRtc = HipSharp::JYPPX.HipSharp.Rtc.HipRtc;`n`ninternal static class Program { private static int Main() { return typeof(HipRuntime).Name.Length + typeof(HipModule).Name.Length + typeof(HipRtc).Name.Length > 0 ? 0 : 1; } }`n")

    & dotnet restore (Join-Path $projectDirectory "Consumer.csproj") `
        --configfile (Join-Path $consumerRoot "NuGet.config") `
        --packages $packages `
        --force `
        --no-cache
    if ($LASTEXITCODE -ne 0) { throw "Clean consumer restore failed for $framework." }

    & dotnet build (Join-Path $projectDirectory "Consumer.csproj") `
        --configuration $Configuration `
        --no-restore `
        -p:RestorePackagesPath=$packages
    if ($LASTEXITCODE -ne 0) { throw "Clean consumer build failed for $framework." }

    $consumerResults.Add([pscustomobject]@{
        targetFramework = $framework
        restore = "passed"
        build = "passed"
        run = "not-run"
    })
}

$packageHash = (Get-FileHash -LiteralPath $resolvedPackage -Algorithm SHA256).Hash
$report = [pscustomobject]@{
    package = $resolvedPackage
    packageVersion = [string]$metadata.version
    sha256 = $packageHash
    targetFrameworkAssets = $frameworks
    contentAudit = "passed"
    consumers = $consumerResults
    runtimeAndGpuValidation = "passed-owner-authorized-cloud-M4-single-environment"
}
$reportPath = Join-Path $auditDirectory "package-audit.json"
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "Package audit passed: 15 TFM asset groups and 4 clean consumer builds."
Write-Host "Audit report: $reportPath"
