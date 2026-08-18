[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackagePath,
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    [string]$OutputDirectory = "artifacts/package-audit",
    [string]$ExpectedVersion,
    [string]$ExpectedRepositoryCommit
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "version.psm1") -Force
$ExpectedVersion = Get-HipSharpVersion -Kind Core -Override $ExpectedVersion -RepositoryRoot $repositoryRoot
if ([string]::IsNullOrWhiteSpace($ExpectedRepositoryCommit)) {
    $ExpectedRepositoryCommit = (& git -C $repositoryRoot rev-parse HEAD 2>$null).Trim()
}
if ($ExpectedRepositoryCommit -notmatch '^[0-9a-f]{40}$') { throw "ExpectedRepositoryCommit must be a lowercase 40-character Git SHA." }
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
        $expectedFiles = @("JYPPX.ROCm.HIP.CSharp.API.dll", "JYPPX.ROCm.HIP.CSharp.API.xml")
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

    if ($metadata.id -ne "JYPPX.ROCm.HIP.CSharp.API") { throw "Unexpected package ID: $($metadata.id)" }
    if ($metadata.version -ne $ExpectedVersion) { throw "Package version must be $ExpectedVersion; found $($metadata.version)." }
    if ($metadata.readme -ne "README.md") { throw "Package README metadata is invalid." }
    if ($metadata.icon -ne "logo.jpg") { throw "Package icon metadata is invalid." }
    if ($metadata.license.'#text' -ne "LICENSE" -or $metadata.license.type -ne "file") { throw "Package license metadata is invalid." }
    if ($metadata.repository.url -ne "https://github.com/guojin-yan/HIP-CSharp-API") { throw "Repository URL metadata is invalid." }
    if ($metadata.repository.type -ne "git") { throw "Repository type metadata is invalid." }
    if ($metadata.repository.commit -notmatch '^[0-9a-fA-F]{40}$') { throw "Repository commit metadata must be a 40-character Git SHA." }
    if ($metadata.repository.commit -ne $ExpectedRepositoryCommit) {
        throw "Repository commit metadata is stale. Package: $($metadata.repository.commit); expected: $ExpectedRepositoryCommit."
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
    <PackageReference Include="JYPPX.ROCm.HIP.CSharp.API" Version="$($metadata.version)" Aliases="HipSharp" />
$frameworkReference
  </ItemGroup>
</Project>
"@
    [System.IO.File]::WriteAllText((Join-Path $projectDirectory "Consumer.csproj"), $projectText)
    $consumerProgram = @"
extern alias HipSharp;

using HipRuntime = HipSharp::JYPPX.ROCm.HipSharp.HipRuntime;
using HipModule = HipSharp::JYPPX.ROCm.HipSharp.Modules.HipModule;
using HipRtc = HipSharp::JYPPX.ROCm.HipSharp.Rtc.HipRtc;
using HipRtcJitInputType = HipSharp::JYPPX.ROCm.HipSharp.Rtc.HipRtcJitInputType;
using HipRtcLinker = HipSharp::JYPPX.ROCm.HipSharp.Rtc.HipRtcLinker;
using HipRtcProgram = HipSharp::JYPPX.ROCm.HipSharp.Rtc.HipRtcProgram;
using HipMemoryPool = HipSharp::JYPPX.ROCm.HipSharp.Memory.HipMemoryPool;
using HipMemoryPoolAccess = HipSharp::JYPPX.ROCm.HipSharp.Memory.HipMemoryPoolAccess;
using HipMemoryPoolOptions = HipSharp::JYPPX.ROCm.HipSharp.Memory.HipMemoryPoolOptions;
using HipPooledDeviceMemory = HipSharp::JYPPX.ROCm.HipSharp.Memory.HipPooledDeviceMemory;
using HipStream = HipSharp::JYPPX.ROCm.HipSharp.Streams.HipStream;

internal static class Program
{
    private static int Main() =>
        typeof(HipRuntime).Name.Length + typeof(HipModule).Name.Length + typeof(HipRtc).Name.Length + typeof(HipRtcLinker).Name.Length > 0 ? 0 : 1;

    private static void CompileRtcWorkflow(HipRtc rtc)
    {
        HipRtcProgram program = rtc.CreateProgram("template<class T> __global__ void kernel(T*) {}");
        program.AddNameExpression("kernel<int>");
        byte[] bitcode = program.CompileToBitcode(new[] { "-fgpu-rdc" });
        string loweredName = program.GetLoweredName("kernel<int>");
        HipRtcLinker linker = rtc.CreateLinker();
        linker.AddData(HipRtcJitInputType.LlvmBitcode, bitcode, "kernel.bc");
        byte[] codeObject = linker.Complete();
        linker.Dispose();
        program.Dispose();
        if (loweredName.Length == 0 || codeObject.Length == 0) throw new System.InvalidOperationException();
    }

    private static void CompilePoolWorkflow(HipRuntime runtime)
    {
        HipStream stream = runtime.CreateStream();
        HipMemoryPool pool = runtime.CreateMemoryPool(new HipMemoryPoolOptions(runtime.GetCurrentDevice()) { ReleaseThresholdBytes = 64 });
        pool.SetAccess(runtime.GetCurrentDevice(), HipMemoryPoolAccess.ReadWrite);
        HipPooledDeviceMemory memory = pool.AllocateAsync(16, stream);
        memory.CopyFromAsync(new byte[16]);
        stream.Synchronize();
        memory.Dispose();
        stream.Synchronize();
        pool.TrimTo(0);
        pool.Dispose();
        stream.Dispose();
    }
}
"@
    [System.IO.File]::WriteAllText((Join-Path $projectDirectory "Program.cs"), $consumerProgram)

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
    repositoryCommit = [string]$metadata.repository.commit
    sha256 = $packageHash
    size = (Get-Item -LiteralPath $resolvedPackage).Length
    targetFrameworkAssets = $frameworks
    assets = @($entries | Sort-Object)
    contentAudit = "passed"
    consumers = $consumerResults
    runtimeAndGpuValidation = "core-0.10.0-hiprtc-program-linker; local-package-gates-passed; fresh-exact-package-gpu-validation-required"
    publishable = $false
    releaseAuthorized = $false
}
$reportPath = Join-Path $auditDirectory "package-audit.json"
$report | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $reportPath -Encoding UTF8
Write-Host "Package audit passed: 15 TFM asset groups and 4 clean consumer builds."
Write-Host "Audit report: $reportPath"
