[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$RuntimePackage,
    [Parameter(Mandatory = $true)][string]$CorePackage,
    [Parameter(Mandatory = $true)][string]$OutputRoot,
    [Parameter(Mandatory = $true)][ValidatePattern('^gfx[0-9a-f]+$')][string]$Architecture,
    [string]$ReferenceAssembliesFeed,
    [switch]$PublishedRuntime,
    [switch]$CompilerOnly
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if ($CompilerOnly -and -not $PublishedRuntime) { throw 'CompilerOnly is restricted to the published Runtime regression.' }
$repository = Split-Path -Parent $PSScriptRoot
$validationCommit = (& git -C $repository rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve validation source commit.' }
$sourceTreeDirty = @(& git -C $repository status --porcelain).Count -ne 0
$output = [IO.Path]::GetFullPath($OutputRoot)
if ($output.StartsWith($repository.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or $output -eq $repository) {
    throw 'OutputRoot must be outside the source repository.'
}
if (Test-Path -LiteralPath $output) { throw 'Use a new OutputRoot for each package validation.' }
New-Item -ItemType Directory -Path $output -Force | Out-Null
$feed = Join-Path $output 'feed'
New-Item -ItemType Directory $feed | Out-Null
Copy-Item -LiteralPath $RuntimePackage, $CorePackage -Destination $feed
if ($IsWindows) {
    $referenceFeed = if ($ReferenceAssembliesFeed) { $ReferenceAssembliesFeed } else {
        $line = (& dotnet nuget locals global-packages --list | Select-Object -First 1)
        if ($LASTEXITCODE -ne 0) { throw 'Cannot locate cached Framework reference assemblies.' }
        ($line -replace '^global-packages:\s*', '').Trim()
    }
    foreach ($id in @('microsoft.netframework.referenceassemblies', 'microsoft.netframework.referenceassemblies.net48')) {
        $candidate = Join-Path $referenceFeed "$id/1.0.3/$id.1.0.3.nupkg"
        if (-not (Test-Path -LiteralPath $candidate)) { $candidate = Join-Path $referenceFeed "$id.1.0.3.nupkg" }
        if (-not (Test-Path -LiteralPath $candidate)) { throw "Restore $id 1.0.3 into a reference-only cache first, or supply ReferenceAssembliesFeed." }
        Copy-Item -LiteralPath $candidate -Destination $feed
    }
}
$runtimePath = (Resolve-Path -LiteralPath $RuntimePackage).Path
$corePath = (Resolve-Path -LiteralPath $CorePackage).Path
$rid = if ($IsWindows) { 'win-x64' } elseif ($IsLinux) { 'linux-x64' } else { throw 'Unsupported host.' }
$archive = [IO.Compression.ZipFile]::OpenRead($runtimePath)
try {
    if ($PublishedRuntime) {
        if (-not $IsLinux) { throw 'The pinned published Runtime is Linux-only.' }
        $publicLock = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'published-runtime-7.2.1.lock.json') -Raw | ConvertFrom-Json
        if ((Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash.ToLowerInvariant() -ne $publicLock.sha256) { throw 'Public Runtime package SHA-256 mismatch.' }
        $publicFiles = @($archive.Entries | Where-Object FullName -Match '^runtimes/linux-x64/native/[^/]+$' | ForEach-Object {
            $stream = $_.Open()
            try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
            [pscustomobject]@{ name = $_.Name; packagePath = $_.FullName; sha256 = $hash }
        })
        if ($publicFiles.Count -ne $publicLock.nativeAssetCount) { throw 'Unexpected public native asset count.' }
        $manifest = [pscustomobject]@{ scope = 'published-runtime-regression'; packageId = $publicLock.packageId; packageVersion = $publicLock.packageVersion; rid = $publicLock.rid; files = $publicFiles }
        $text = $manifest | ConvertTo-Json -Depth 6
    } else {
        $entry = $archive.GetEntry('local-runtime-manifest.json')
        if ($null -eq $entry) { throw 'Expected a local-development Runtime package.' }
        $reader = [IO.StreamReader]::new($entry.Open())
        try { $text = $reader.ReadToEnd() } finally { $reader.Dispose() }
        $manifest = $text | ConvertFrom-Json
        if ($manifest.scope -ne 'local-development' -or $manifest.packageVersion -notmatch '^10\.1\.0-local\.') { throw 'Not a local package.' }
    }
    if ($manifest.rid -ne $rid) { throw 'Runtime package RID does not match this host.' }
    foreach ($file in $manifest.files) {
        $nativeEntry = $archive.GetEntry($file.packagePath)
        if ($null -eq $nativeEntry) { throw "Missing package entry: $($file.packagePath)" }
        $stream = $nativeEntry.Open()
        try { $hash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant() } finally { $stream.Dispose() }
        if ($hash -ne $file.sha256) { throw "Native package hash mismatch: $($file.name)" }
    }
    $manifestPath = Join-Path $output 'local-runtime-manifest.json'
    $text | Set-Content -LiteralPath $manifestPath -Encoding utf8
} finally { $archive.Dispose() }
$coreArchive = [IO.Compression.ZipFile]::OpenRead($corePath)
try {
    $reader = [IO.StreamReader]::new(@($coreArchive.Entries | Where-Object FullName -Like '*.nuspec')[0].Open())
    try { [xml]$spec = $reader.ReadToEnd() } finally { $reader.Dispose() }
    $coreVersion = $spec.package.metadata.version
    $expectedCommit = $spec.package.metadata.repository.commit
} finally { $coreArchive.Dispose() }
$config = Join-Path $output 'NuGet.Config'
$escapedFeed = [Security.SecurityElement]::Escape($feed)
@"
<configuration>
  <packageSources><clear/><add key="local" value="$escapedFeed"/></packageSources>
</configuration>
"@ | Set-Content -LiteralPath $config -Encoding utf8
$saved = @{}
foreach ($name in @('HIP_PATH', 'ROCM_PATH', 'PATH', 'LD_LIBRARY_PATH', 'LD_PRELOAD', 'HIPSHARP_LOCAL_RUNTIME_MANIFEST', 'HIPSHARP_OUTPUT_ROOT', 'NUGET_PACKAGES')) {
    $saved[$name] = [Environment]::GetEnvironmentVariable($name)
}
$results = @()
try {
    foreach ($name in @('HIP_PATH', 'ROCM_PATH', 'LD_LIBRARY_PATH', 'LD_PRELOAD', 'HIPSHARP_OUTPUT_ROOT')) { [Environment]::SetEnvironmentVariable($name, $null) }
    # Keep dotnet/OS tools but remove installed AMD SDK search paths.
    $env:PATH = (($env:PATH -split [IO.Path]::PathSeparator) | Where-Object { $_ -and $_ -notmatch '(?i)(AMD[/\\]ROCm|/opt/rocm)' }) -join [IO.Path]::PathSeparator
    if ($IsLinux -and (Test-Path /dev/dxg)) { $env:LD_LIBRARY_PATH = '/usr/lib/wsl/lib' }
    $env:HIPSHARP_LOCAL_RUNTIME_MANIFEST = $manifestPath
    $env:NUGET_PACKAGES = Join-Path $output 'packages'
    $workloads = @(
        @{ Name = 'EnvironmentAndDevice'; Arguments = @() },
        @{ Name = 'LinearMemoryCopy'; Arguments = @() },
        @{ Name = 'StreamAndEvent'; Arguments = @() },
        @{ Name = 'HipRtcVectorAdd'; Arguments = @('--arch', $Architecture, '--length', '1000', '--repeat', '20') },
        @{ Name = 'HipRtcVectorAdd'; Label = 'HipRtcNegativeCompile'; Arguments = @('--arch', $Architecture, '--negative-compile') },
        @{ Name = 'HipRtcProgramLinker'; Arguments = @($Architecture) },
        @{ Name = 'AsyncVectorAdd'; Arguments = @('--arch', $Architecture) },
        @{ Name = 'PinnedHostMemory'; Arguments = @() },
        @{ Name = 'PitchedMemory2D3D'; Arguments = @() },
        @{ Name = 'ManagedMemory'; Arguments = @() },
        @{ Name = 'AsyncAllocationAndMemoryPool'; Arguments = @() },
        @{ Name = 'VirtualMemory'; Arguments = @() },
        @{ Name = 'GraphCaptureReplay'; Arguments = @() },
        @{ Name = 'ExplicitGraphDag'; Arguments = @() },
        @{ Name = 'KernelOccupancy'; Arguments = @($Architecture) },
        @{ Name = 'ModuleGlobals'; Arguments = @($Architecture) },
        @{ Name = 'ArrayTextureSurface'; Arguments = @($Architecture) },
        @{ Name = 'HipManagedExpansionValidation'; Arguments = @('--arch', $Architecture, '--graph-launch-repeats', '3', '--expected-commit', $expectedCommit, '--environment', 'package-only') }
    )
    if ($CompilerOnly) {
        $workloads = @(
            @{ Name = 'HipRtcCompileOnly'; Source = (Join-Path $PSScriptRoot 'local-runtime/RtcPackageCompileSmoke.cs'); Arguments = @($Architecture) },
            @{ Name = 'HipRtcVectorAdd'; Label = 'HipRtcNegativeCompile'; Arguments = @('--arch', $Architecture, '--negative-compile') }
        )
    }
    foreach ($workload in $workloads) {
        $label = if ($workload.ContainsKey('Label')) { $workload.Label } else { $workload.Name }
        $work = Join-Path $output $label
        New-Item -ItemType Directory $work | Out-Null
        if ($workload.ContainsKey('Source')) {
            $sampleFiles = [Security.SecurityElement]::Escape($workload.Source)
        } else {
            $sample = @(Get-ChildItem -LiteralPath (Join-Path $repository 'samples') -Recurse -Filter "$($workload.Name).csproj")
            if ($sample.Count -ne 1) { throw "Ambiguous sample: $($workload.Name)" }
            $sampleFiles = [Security.SecurityElement]::Escape((Join-Path $sample[0].DirectoryName '*.cs'))
        }
        $runner = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'local-runtime/PackageRunner.cs'))
        $moduleAudit = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'local-runtime/PackageModuleAudit.cs'))
        $project = Join-Path $work 'consumer.csproj'
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems><ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable><AllowUnsafeBlocks>true</AllowUnsafeBlocks><StartupObject>PackageRunner</StartupObject>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$sampleFiles"/><Compile Include="$runner"/><Compile Include="$moduleAudit"/>
    <PackageReference Include="JYPPX.ROCm.HIP.CSharp.API" Version="[$coreVersion]"/>
    <PackageReference Include="$($manifest.packageId)" Version="[$($manifest.packageVersion)]"/>
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath $project -Encoding utf8
        $buildLog = @(& dotnet build $project -c Release --configfile $config --nologo 2>&1)
        $buildLog | Set-Content -LiteralPath (Join-Path $work 'build.log') -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw "Package consumer build failed: $label; see $work/build.log" }
        $arguments = $workload.Arguments
        $runLog = @(& dotnet (Join-Path $work 'bin/Release/net10.0/consumer.dll') @arguments 2>&1)
        $exitCode = $LASTEXITCODE
        $runLog | Set-Content -LiteralPath (Join-Path $work 'run.log') -Encoding utf8
        $status = if ($exitCode -ne 0) { 'failed' } elseif (($runLog -join "`n") -match '(?i)Skipped:') { 'skipped-capability' } else { 'passed' }
        $results += [pscustomobject]@{ workload = $label; status = $status; exitCode = $exitCode }
        Write-Host "$label : $status"
    }
    if (-not $CompilerOnly -and @($results | Where-Object status -eq 'failed').Count -eq 0) {
        $publish = Join-Path $output 'published-vector-add'
        $project = Join-Path $output 'HipRtcVectorAdd/consumer.csproj'
        $publishLog = @(& dotnet publish $project -c Release --no-build --no-restore -o $publish --nologo 2>&1)
        $publishLog | Set-Content -LiteralPath (Join-Path $output 'publish.log') -Encoding utf8
        if ($LASTEXITCODE -ne 0) { throw 'Package consumer publish failed.' }
        $runLog = @(& dotnet (Join-Path $publish 'consumer.dll') --arch $Architecture --length 1000 --repeat 20 2>&1)
        $exitCode = $LASTEXITCODE
        $runLog | Set-Content -LiteralPath (Join-Path $output 'published-vector-add.log') -Encoding utf8
        $results += [pscustomobject]@{ workload = 'PublishedVectorAdd'; status = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' }); exitCode = $exitCode }
        Write-Host "PublishedVectorAdd : $($results[-1].status)"
        # Move one HIPRTC entry away, launch a fresh process, then restore it.
        # A system-SDK fallback must not turn this negative into a success.
        $nativeName = if ($IsWindows) { 'hiprtc0716.dll' } else { 'libhiprtc.so' }
        $nativePath = Join-Path $publish "runtimes/$rid/native/$nativeName"
        $hiddenPath = $nativePath + '.disabled'
        Move-Item -LiteralPath $nativePath -Destination $hiddenPath
        try {
            $negativeLog = @(& dotnet (Join-Path $publish 'consumer.dll') --arch $Architecture --length 1000 --repeat 1 2>&1)
            $negativeCode = $LASTEXITCODE
            $negativeLog | Set-Content -LiteralPath (Join-Path $output 'missing-hiprtc.log') -Encoding utf8
            $rejected = $negativeCode -ne 0 -and (($negativeLog -join "`n") -match 'HipLibraryLoadException|must be loaded from the same user-mode closure')
            $results += [pscustomobject]@{ workload = 'MissingHipRtcRejected'; status = $(if ($rejected) { 'passed' } else { 'failed' }); exitCode = $negativeCode }
            Write-Host "MissingHipRtcRejected : $($results[-1].status)"
        } finally { Move-Item -LiteralPath $hiddenPath -Destination $nativePath }

        foreach ($appHost in @($false, $true)) {
            $label = if ($appHost) { 'RidPublishAppHost' } else { 'RidPublishDotnet' }
            $ridPublish = Join-Path $output $label
            $publishLog = @(& dotnet publish $project -c Release -r $rid --self-contained false "-p:UseAppHost=$($appHost.ToString().ToLowerInvariant())" --configfile $config -o $ridPublish --nologo 2>&1)
            $publishLog | Set-Content -LiteralPath (Join-Path $output "$label-build.log") -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw "RID publish failed: $label" }
            $runLog = if ($appHost) {
                $exe = Join-Path $ridPublish $(if ($IsWindows) { 'consumer.exe' } else { 'consumer' })
                @(& $exe --arch $Architecture --length 1000 --repeat 20 2>&1)
            } else { @(& dotnet (Join-Path $ridPublish 'consumer.dll') --arch $Architecture --length 1000 --repeat 20 2>&1) }
            $exitCode = $LASTEXITCODE
            $runLog | Set-Content -LiteralPath (Join-Path $output "$label.log") -Encoding utf8
            $results += [pscustomobject]@{ workload = $label; status = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' }); exitCode = $exitCode }
            Write-Host "$label : $($results[-1].status)"
        }
        if ($IsWindows) {
            $framework = Join-Path $output 'FrameworkPackage'
            New-Item -ItemType Directory $framework | Out-Null
            $source = [Security.SecurityElement]::Escape((Join-Path $PSScriptRoot 'local-runtime/FrameworkPackageSmoke.cs'))
@"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType><TargetFramework>net48</TargetFramework><LangVersion>latest</LangVersion>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks><PlatformTarget>x64</PlatformTarget><NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
  <ItemGroup>
    <Compile Include="$source"/><Compile Include="$moduleAudit"/>
    <PackageReference Include="JYPPX.ROCm.HIP.CSharp.API" Version="[$coreVersion]"/>
    <PackageReference Include="$($manifest.packageId)" Version="[$($manifest.packageVersion)]"/>
    <PackageReference Include="Microsoft.NETFramework.ReferenceAssemblies" Version="1.0.3" PrivateAssets="all"/>
  </ItemGroup>
</Project>
"@ | Set-Content -LiteralPath (Join-Path $framework 'FrameworkPackage.csproj') -Encoding utf8
            $hashFile = Join-Path $framework 'native-hashes.tsv'
            $manifest.files | ForEach-Object { "$($_.name)`t$($_.sha256)" } | Set-Content -LiteralPath $hashFile -Encoding utf8
            $buildLog = @(& dotnet build (Join-Path $framework 'FrameworkPackage.csproj') -c Release --configfile $config --nologo 2>&1)
            $buildLog | Set-Content -LiteralPath (Join-Path $framework 'build.log') -Encoding utf8
            if ($LASTEXITCODE -ne 0) { throw 'Framework PackageReference consumer build failed.' }
            $runLog = @(& (Join-Path $framework 'bin/Release/net48/FrameworkPackage.exe') $Architecture $hashFile 2>&1)
            $exitCode = $LASTEXITCODE
            $runLog | Set-Content -LiteralPath (Join-Path $framework 'run.log') -Encoding utf8
            $results += [pscustomobject]@{ workload = 'FrameworkPackage'; status = $(if ($exitCode -eq 0) { 'passed' } else { 'failed' }); exitCode = $exitCode }
            Write-Host "FrameworkPackage : $($results[-1].status)"
        }
    }
} finally {
    foreach ($name in $saved.Keys) { [Environment]::SetEnvironmentVariable($name, $saved[$name]) }
    [pscustomobject]@{
        scope = $(if ($CompilerOnly) { 'published-runtime-compiler-only' } elseif ($PublishedRuntime) { 'published-runtime-regression' } else { 'local-package-consumers' }); platform = $rid; architecture = $Architecture
        sourceCommit = $expectedCommit; validationCommit = $validationCommit
        sourceTreeDirty = ($sourceTreeDirty -or @(& git -C $repository status --porcelain).Count -ne 0 -or (& git -C $repository rev-parse HEAD).Trim() -ne $validationCommit)
        host = $(if ($IsWindows) { 'windows' } elseif (Test-Path /dev/dxg) { 'wsl2' } elseif (Test-Path /dev/kfd) { 'linux-kfd' } else { 'linux-other' })
        completed = ($results.Count -eq $(if ($CompilerOnly) { 2 } elseif ($IsWindows) { 23 } else { 22 }))
        runtimePackageSha256 = (Get-FileHash -LiteralPath $runtimePath -Algorithm SHA256).Hash.ToLowerInvariant()
        corePackageSha256 = (Get-FileHash -LiteralPath $corePath -Algorithm SHA256).Hash.ToLowerInvariant()
        workloads = $results
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $output 'summary.json') -Encoding utf8
}
if (@($results | Where-Object status -eq 'failed').Count -ne 0) { throw "Package workloads failed; see $output" }
