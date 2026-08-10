Set-StrictMode -Version Latest

function Get-HipSharpSha256 {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-HipSharpRelativePath {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $normalized = $Path.Replace("\", "/")
    if ([string]::IsNullOrWhiteSpace($normalized) -or
        $normalized.StartsWith("/", [System.StringComparison]::Ordinal) -or
        $normalized -match "^[A-Za-z]:" -or
        $normalized.Split("/") -contains "..") {
        throw "Runtime manifest path must be a non-rooted, traversal-free path: $Path"
    }

    return $normalized
}

function Assert-HipSharpHash {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Value, [Parameter(Mandatory = $true)][string]$Name)

    if ($Value -notmatch "^[0-9a-f]{64}$") {
        throw "$Name must be a lowercase SHA-256 value."
    }
}

function Get-HipSharpRuntimeManifest {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$ManifestPath)

    $resolved = (Resolve-Path -LiteralPath $ManifestPath).Path
    $manifest = Get-Content -Raw -LiteralPath $resolved | ConvertFrom-Json -AsHashtable
    return [pscustomobject]@{ Path = $resolved; Value = $manifest }
}

function Assert-HipSharpOfficialUrl {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Value, [Parameter(Mandatory = $true)][string]$Name)

    $uri = [Uri]$Value
    if ($uri.Scheme -ne "https" -or -not [string]::Equals($uri.Host, "repo.radeon.com", [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Name must be an HTTPS URL hosted by repo.radeon.com."
    }
}

function Assert-HipSharpRuntimeManifest {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)][hashtable]$Manifest,
        [switch]$RequirePackable
    )

    foreach ($name in @("schemaVersion", "packageId", "packageVersion", "rid", "nativeAssetPath", "source", "packages", "files", "licenses", "dependencyClosure", "systemDependencies", "driverBoundary", "sbom", "verification")) {
        if (-not $Manifest.ContainsKey($name)) { throw "Runtime manifest is missing '$name'." }
    }

    if ($Manifest.schemaVersion -ne 2) { throw "Runtime manifest schemaVersion must be 2." }
    if ($Manifest.packageId -notmatch "^JYPPX\.HipSharp\.Runtime\.(linux|win)-x64$") { throw "Runtime packageId is invalid." }
    if ($Manifest.packageId -match "rocm") { throw "Runtime packageId must not encode the ROCm version." }
    if ([string]::IsNullOrWhiteSpace($Manifest.packageVersion)) { throw "Runtime packageVersion is required." }
    if ($Manifest.nativeAssetPath -ne "runtimes/$($Manifest.rid)/native") { throw "nativeAssetPath must match the package RID." }
    ConvertTo-HipSharpRelativePath $Manifest.nativeAssetPath | Out-Null

    if ($Manifest.rid -eq "win-x64") {
        if ($Manifest.packEnabled -or $Manifest.verified) { throw "Windows runtime packaging remains disabled." }
        if ($RequirePackable) { throw "HIPSHARP1001: Windows runtime packaging remains disabled for M5." }
        return
    }
    if ($Manifest.rid -ne "linux-x64") { throw "Only linux-x64 and win-x64 manifests are supported." }

    $source = $Manifest.source
    foreach ($name in @("repositoryUrl", "inReleaseUrl", "packagesIndexUrl", "signingKeyUrl", "signingKeyFingerprint", "signingKeySha256", "inReleaseSha256", "packagesIndexSha256")) {
        if (-not $source.ContainsKey($name) -or [string]::IsNullOrWhiteSpace($source[$name])) { throw "source.$name is required." }
    }
    foreach ($name in @("repositoryUrl", "inReleaseUrl", "packagesIndexUrl", "signingKeyUrl")) { Assert-HipSharpOfficialUrl $source[$name] "source.$name" }
    foreach ($name in @("signingKeySha256", "inReleaseSha256", "packagesIndexSha256")) { Assert-HipSharpHash $source[$name] "source.$name" }
    if ($source.signingKeyFingerprint -notmatch "^[0-9A-F]{40}$") { throw "source.signingKeyFingerprint must be a 40-character uppercase OpenPGP fingerprint." }

    $packageNames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($package in @($Manifest.packages)) {
        foreach ($name in @("name", "version", "architecture", "url", "sha256", "size", "depends")) {
            if (-not $package.ContainsKey($name)) { throw "Package metadata is missing '$name'." }
        }
        if ($package.architecture -ne "amd64") { throw "Only AMD64 source packages are allowed." }
        Assert-HipSharpOfficialUrl $package.url "package $($package.name) URL"
        Assert-HipSharpHash $package.sha256 "package $($package.name) SHA-256"
        if ([int64]$package.size -le 0) { throw "Package $($package.name) must have a positive size." }
        if (-not $packageNames.Add([string]$package.name)) { throw "Duplicate source package: $($package.name)" }
    }

    $packageEdges = @{}
    foreach ($package in @($Manifest.packages)) {
        $dependencies = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
        foreach ($declaration in @($package.depends)) {
            foreach ($match in [regex]::Matches([string]$declaration, "[a-z0-9.+-]+-rpath7\.2\.1")) {
                if (-not $packageNames.Contains($match.Value)) { throw "Package $($package.name) has an undeclared ROCm dependency: $($match.Value)" }
                $dependencies.Add($match.Value) | Out-Null
            }
        }
        $packageEdges[$package.name] = $dependencies
    }
    $indegree = @{}
    foreach ($name in $packageNames) { $indegree[$name] = 0 }
    foreach ($name in $packageEdges.Keys) { foreach ($dependency in $packageEdges[$name]) { $indegree[$dependency]++ } }
    $queue = [System.Collections.Generic.Queue[string]]::new()
    foreach ($name in $packageNames) { if ($indegree[$name] -eq 0) { $queue.Enqueue($name) } }
    $visited = 0
    while ($queue.Count -gt 0) {
        $name = $queue.Dequeue()
        $visited++
        foreach ($dependency in $packageEdges[$name]) {
            $indegree[$dependency]--
            if ($indegree[$dependency] -eq 0) { $queue.Enqueue($dependency) }
        }
    }
    if ($visited -ne $packageNames.Count) { throw "ROCm package dependency graph contains a cycle." }

    $filePaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $canonicalFiles = @()
    foreach ($file in @($Manifest.files)) {
        foreach ($name in @("path", "sourcePath", "sourcePackage", "sha256", "size", "purpose")) {
            if (-not $file.ContainsKey($name)) { throw "Runtime file metadata is missing '$name'." }
        }
        $path = ConvertTo-HipSharpRelativePath $file.path
        $sourcePath = ConvertTo-HipSharpRelativePath $file.sourcePath
        if (-not $path.StartsWith($Manifest.nativeAssetPath + "/", [System.StringComparison]::Ordinal)) { throw "Runtime file escapes native asset directory: $path" }
        if ($path -match "\.(h|hpp|a|bc|hsaco|deb|ddeb|pdb)$" -or $path -match "(^|/)(include|cmake|bin|libexec)/") { throw "Forbidden runtime payload path: $path" }
        if (-not $filePaths.Add($path)) { throw "Duplicate runtime package path: $path" }
        if (-not $packageNames.Contains([string]$file.sourcePackage)) { throw "Runtime file references undeclared package: $($file.sourcePackage)" }
        Assert-HipSharpHash $file.sha256 "runtime file $path SHA-256"
        if ([int64]$file.size -le 0) { throw "Runtime file $path must have a positive size." }
        if ($file.ContainsKey("aliasFor")) {
            ConvertTo-HipSharpRelativePath $file.aliasFor | Out-Null
        } else {
            foreach ($name in @("soname", "needed", "rpath")) { if (-not $file.ContainsKey($name)) { throw "Canonical runtime file $path is missing '$name'." } }
            $canonicalFiles += $file
        }
    }
    if ($canonicalFiles.Count -eq 0) { throw "Linux runtime manifest must declare canonical ELF files." }

    $canonicalPaths = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    $sonames = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in $canonicalFiles) {
        $canonicalPaths.Add((ConvertTo-HipSharpRelativePath $file.path)) | Out-Null
        if (-not $sonames.Add([string]$file.soname)) { throw "Duplicate canonical SONAME: $($file.soname)" }
    }
    foreach ($file in @($Manifest.files | Where-Object { $_.ContainsKey("aliasFor") })) {
        $aliasFor = ConvertTo-HipSharpRelativePath $file.aliasFor
        if (-not $canonicalPaths.Contains($aliasFor)) { throw "Runtime alias points to a missing canonical file: $aliasFor" }
        $target = @($Manifest.files | Where-Object { (ConvertTo-HipSharpRelativePath $_.path) -eq $aliasFor })[0]
        if ($file.sha256 -ne $target.sha256 -or [int64]$file.size -ne [int64]$target.size -or $file.sourcePath -ne $target.sourcePath) { throw "Runtime alias must retain the canonical file identity: $($file.path)" }
    }

    $licensePackages = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($license in @($Manifest.licenses)) {
        foreach ($name in @("sourcePackage", "expression", "sourcePath", "packagePath", "sha256")) {
            if (-not $license.ContainsKey($name)) { throw "License metadata is missing '$name'." }
        }
        if (-not $packageNames.Contains([string]$license.sourcePackage)) { throw "License references undeclared package: $($license.sourcePackage)" }
        ConvertTo-HipSharpRelativePath $license.sourcePath | Out-Null
        $licensePath = ConvertTo-HipSharpRelativePath $license.packagePath
        if (-not $licensePath.StartsWith("licenses/", [System.StringComparison]::Ordinal)) { throw "License must be packaged under licenses/: $licensePath" }
        Assert-HipSharpHash $license.sha256 "license $licensePath SHA-256"
        $licensePackages.Add([string]$license.sourcePackage) | Out-Null
    }
    foreach ($file in $canonicalFiles) {
        if (-not $licensePackages.Contains([string]$file.sourcePackage)) { throw "Runtime source package has no packaged license: $($file.sourcePackage)" }
    }

    $provided = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($file in $canonicalFiles) { $provided.Add([string]$file.soname) | Out-Null }
    $system = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    foreach ($dependency in @($Manifest.systemDependencies)) {
        foreach ($name in @("soname", "minimumVersion", "ubuntuPackage")) { if (-not $dependency.ContainsKey($name)) { throw "System dependency is missing '$name'." } }
        $system.Add([string]$dependency.soname) | Out-Null
    }
    foreach ($file in $canonicalFiles) {
        foreach ($needed in @($file.needed)) {
            if (-not $provided.Contains([string]$needed) -and -not $system.Contains([string]$needed)) { throw "Unresolved ELF dependency '$needed' from $($file.path)." }
        }
    }
    foreach ($name in @("deviceNodes", "kernelDriver", "excludedFromPackage")) {
        if (-not $Manifest.driverBoundary.ContainsKey($name) -or @($Manifest.driverBoundary[$name]).Count -eq 0) { throw "driverBoundary.$name is required." }
    }

    foreach ($name in @("format", "path", "sha256")) { if (-not $Manifest.sbom.ContainsKey($name)) { throw "sbom.$name is required." } }
    if ($Manifest.sbom.format -ne "CycloneDX-1.5") { throw "Only deterministic CycloneDX-1.5 SBOM metadata is supported." }
    ConvertTo-HipSharpRelativePath $Manifest.sbom.path | Out-Null
    Assert-HipSharpHash $Manifest.sbom.sha256 "SBOM SHA-256"

    foreach ($name in @("provenanceVerified", "closureVerified", "licensesVerified", "sbomVerified", "packageAuditVerified", "gpuValidated")) {
        if (-not $Manifest.verification.ContainsKey($name)) { throw "verification.$name is required." }
    }
    if ($RequirePackable) {
        if (-not $Manifest.packEnabled -or -not $Manifest.verified -or
            -not $Manifest.verification.provenanceVerified -or -not $Manifest.verification.closureVerified -or
            -not $Manifest.verification.licensesVerified -or -not $Manifest.verification.sbomVerified -or
            -not $Manifest.verification.packageAuditVerified -or -not $Manifest.verification.gpuValidated) {
            throw "HIPSHARP1001: Runtime package generation is disabled until provenance, closure, licenses, SBOM, package audit, and isolated GPU validation are all verified."
        }
        if (-not $Manifest.verification.ContainsKey("validationSha256") -or
            [string]::IsNullOrWhiteSpace([string]$Manifest.verification.validationSha256)) {
            throw "HIPSHARP1001: Runtime validation evidence must include a non-empty validationSha256."
        }
        Assert-HipSharpHash ([string]$Manifest.verification.validationSha256) "verification.validationSha256"
        if (-not $Manifest.verification.ContainsKey("environment") -or $null -eq $Manifest.verification.environment) {
            throw "HIPSHARP1001: Runtime validation evidence must include an isolated consumer environment."
        }
        foreach ($name in @("os", "architecture", "gpu", "isolation")) {
            if (-not $Manifest.verification.environment.ContainsKey($name) -or
                [string]::IsNullOrWhiteSpace([string]$Manifest.verification.environment[$name])) {
                throw "HIPSHARP1001: verification.environment.$name is required for runtime packaging."
            }
        }
        if (-not $Manifest.ContainsKey("size") -or
            [int64]$Manifest.size.packageBytes -le 0 -or
            [int64]$Manifest.size.packageBytes -ge [int64]$Manifest.size.nugetLimitBytes) {
            throw "HIPSHARP1001: Runtime package size must be positive and below the configured NuGet limit."
        }
    }
}

function Get-HipSharpElfDynamicInfo {
    [CmdletBinding()]
    param([Parameter(Mandatory = $true)][string]$Path)

    $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 64 -or $bytes[0] -ne 0x7F -or $bytes[1] -ne [byte][char]'E' -or $bytes[2] -ne [byte][char]'L' -or $bytes[3] -ne [byte][char]'F') { throw "Not an ELF file: $Path" }
    if ($bytes[4] -ne 2 -or $bytes[5] -ne 1) { throw "Only little-endian ELF64 is supported: $Path" }
    if ([BitConverter]::ToUInt16($bytes, 18) -ne 62) { throw "Only x86_64 ELF is supported: $Path" }

    $programOffset = [int64][BitConverter]::ToUInt64($bytes, 32)
    $programSize = [int][BitConverter]::ToUInt16($bytes, 54)
    $programCount = [int][BitConverter]::ToUInt16($bytes, 56)
    $loads = @()
    $dynamicOffset = -1L
    $dynamicSize = 0L
    for ($index = 0; $index -lt $programCount; $index++) {
        $offset = $programOffset + ([int64]$index * $programSize)
        if ($offset -lt 0 -or $offset + 56 -gt $bytes.Length) { throw "ELF program header escapes file: $Path" }
        $type = [BitConverter]::ToUInt32($bytes, [int]$offset)
        $fileOffset = [int64][BitConverter]::ToUInt64($bytes, [int]$offset + 8)
        $virtualAddress = [int64][BitConverter]::ToUInt64($bytes, [int]$offset + 16)
        $fileSize = [int64][BitConverter]::ToUInt64($bytes, [int]$offset + 32)
        if ($type -eq 1) { $loads += [pscustomobject]@{ FileOffset = $fileOffset; VirtualAddress = $virtualAddress; FileSize = $fileSize } }
        if ($type -eq 2) { $dynamicOffset = $fileOffset; $dynamicSize = $fileSize }
    }
    if ($dynamicOffset -lt 0) { throw "ELF has no dynamic section: $Path" }

    $entries = @()
    for ($offset = $dynamicOffset; $offset + 16 -le $dynamicOffset + $dynamicSize; $offset += 16) {
        $tag = [BitConverter]::ToInt64($bytes, [int]$offset)
        $value = [BitConverter]::ToUInt64($bytes, [int]$offset + 8)
        if ($tag -eq 0) { break }
        $entries += [pscustomobject]@{ Tag = $tag; Value = [int64]$value }
    }
    $stringAddress = @($entries | Where-Object { $_.Tag -eq 5 } | Select-Object -First 1).Value
    if ($null -eq $stringAddress) { throw "ELF dynamic section has no string table: $Path" }
    $segment = @($loads | Where-Object { $stringAddress -ge $_.VirtualAddress -and $stringAddress -lt $_.VirtualAddress + $_.FileSize } | Select-Object -First 1)
    if ($segment.Count -ne 1) { throw "ELF dynamic string table cannot be mapped: $Path" }
    $stringOffset = $segment[0].FileOffset + ($stringAddress - $segment[0].VirtualAddress)
    $readString = {
        param([int64]$RelativeOffset)
        $start = $stringOffset + $RelativeOffset
        if ($start -lt 0 -or $start -ge $bytes.Length) { throw "ELF string escapes file: $Path" }
        $end = $start
        while ($end -lt $bytes.Length -and $bytes[$end] -ne 0) { $end++ }
        return [System.Text.Encoding]::ASCII.GetString($bytes, [int]$start, [int]($end - $start))
    }
    $needed = @($entries | Where-Object { $_.Tag -eq 1 } | ForEach-Object { & $readString $_.Value })
    $sonameEntry = @($entries | Where-Object { $_.Tag -eq 14 } | Select-Object -First 1)
    $rpathEntry = @($entries | Where-Object { $_.Tag -eq 15 } | Select-Object -First 1)
    $runpathEntry = @($entries | Where-Object { $_.Tag -eq 29 } | Select-Object -First 1)
    return [pscustomobject]@{
        Soname = if ($sonameEntry.Count -eq 1) { & $readString $sonameEntry[0].Value } else { "" }
        Needed = @($needed | Sort-Object -Unique)
        Rpath = if ($rpathEntry.Count -eq 1) { & $readString $rpathEntry[0].Value } elseif ($runpathEntry.Count -eq 1) { & $readString $runpathEntry[0].Value } else { "" }
    }
}

Export-ModuleMember -Function Get-HipSharpSha256, ConvertTo-HipSharpRelativePath, Get-HipSharpRuntimeManifest, Assert-HipSharpRuntimeManifest, Get-HipSharpElfDynamicInfo
