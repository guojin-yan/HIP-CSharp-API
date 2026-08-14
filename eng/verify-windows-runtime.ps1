[CmdletBinding()]
param(
    [string] $ManifestPath = (Join-Path $PSScriptRoot '..\nuget\runtime-manifests\win-x64.json'),
    [string] $StagingDirectory = (Join-Path $PSScriptRoot '..\artifacts\runtime-staging\win-x64'),
    [switch] $RequirePackable,
    [switch] $SyntheticFixture
)

$ErrorActionPreference = 'Stop'

function Stop-Audit([string] $Message) {
    throw "HIPSHARP-WIN-AUDIT: $Message"
}

function Test-Sha256([string] $Value) {
    return $Value -match '^[0-9a-fA-F]{64}$'
}

function Read-PeUInt16([byte[]] $Bytes, [int] $Offset, [string] $Description) {
    if ($Offset -lt 0 -or $Offset + 2 -gt $Bytes.Length) { Stop-Audit "truncated PE $Description" }
    return [BitConverter]::ToUInt16($Bytes, $Offset)
}

function Read-PeUInt32([byte[]] $Bytes, [int] $Offset, [string] $Description) {
    if ($Offset -lt 0 -or $Offset + 4 -gt $Bytes.Length) { Stop-Audit "truncated PE $Description" }
    return [BitConverter]::ToUInt32($Bytes, $Offset)
}

function Convert-PeRvaToOffset([byte[]] $Bytes, [uint32] $Rva, [uint32] $SizeOfHeaders, [object[]] $Sections) {
    if ($Rva -lt $SizeOfHeaders) {
        if ([uint64]$Rva -ge [uint64]$Bytes.Length) { Stop-Audit "PE RVA 0x$($Rva.ToString('x')) is outside the file" }
        return [int]$Rva
    }
    foreach ($section in $Sections) {
        [uint64]$start = $section.VirtualAddress
        [uint64]$span = [Math]::Max([uint64]$section.VirtualSize, [uint64]$section.RawSize)
        if ([uint64]$Rva -ge $start -and [uint64]$Rva -lt $start + $span) {
            [uint64]$delta = [uint64]$Rva - $start
            if ($delta -ge [uint64]$section.RawSize) { Stop-Audit "PE RVA points into an unbacked virtual section range" }
            [uint64]$offset = [uint64]$section.RawOffset + $delta
            if ($offset -ge [uint64]$Bytes.Length) { Stop-Audit "PE RVA maps outside the file" }
            return [int]$offset
        }
    }
    Stop-Audit "PE RVA 0x$($Rva.ToString('x')) does not map to a section"
}

function Read-PeAsciiZ([byte[]] $Bytes, [int] $Offset, [string] $Description) {
    if ($Offset -lt 0 -or $Offset -ge $Bytes.Length) { Stop-Audit "invalid PE $Description string offset" }
    $end = $Offset
    while ($end -lt $Bytes.Length -and $Bytes[$end] -ne 0 -and $end - $Offset -lt 4096) { $end++ }
    if ($end -ge $Bytes.Length -or $Bytes[$end] -ne 0) { Stop-Audit "unterminated PE $Description string" }
    return [Text.Encoding]::ASCII.GetString($Bytes, $Offset, $end - $Offset)
}

function Get-PeFacts([string] $Path) {
    [byte[]] $bytes = [System.IO.File]::ReadAllBytes($Path)
    if ($bytes.Length -lt 512 -or $bytes[0] -ne 0x4d -or $bytes[1] -ne 0x5a) {
        Stop-Audit "not a PE file: $Path"
    }
    $peOffset = [int](Read-PeUInt32 $bytes 0x3c 'DOS header')
    if ($peOffset -lt 0 -or $peOffset + 24 -gt $bytes.Length -or
        $bytes[$peOffset] -ne 0x50 -or $bytes[$peOffset + 1] -ne 0x45 -or
        $bytes[$peOffset + 2] -ne 0 -or $bytes[$peOffset + 3] -ne 0) {
        Stop-Audit "invalid PE signature: $Path"
    }
    $machine = Read-PeUInt16 $bytes ($peOffset + 4) 'COFF machine'
    $sectionCount = Read-PeUInt16 $bytes ($peOffset + 6) 'COFF section count'
    $optionalSize = Read-PeUInt16 $bytes ($peOffset + 20) 'COFF optional-header size'
    $optionalOffset = $peOffset + 24
    if ($sectionCount -eq 0 -or $sectionCount -gt 96 -or $optionalSize -lt 240 -or $optionalOffset + $optionalSize -gt $bytes.Length) {
        Stop-Audit "invalid PE section or optional-header layout: $Path"
    }
    if ((Read-PeUInt16 $bytes $optionalOffset 'optional-header magic') -ne 0x20b) {
        Stop-Audit "PE optional header is not PE32+: $Path"
    }
    $sizeOfHeaders = Read-PeUInt32 $bytes ($optionalOffset + 60) 'SizeOfHeaders'
    $directoryCount = Read-PeUInt32 $bytes ($optionalOffset + 108) 'data-directory count'
    if ($directoryCount -lt 2) { Stop-Audit "PE export/import data directories are absent: $Path" }

    $sections = @()
    $sectionOffset = $optionalOffset + $optionalSize
    for ($index = 0; $index -lt $sectionCount; $index++) {
        $entryOffset = $sectionOffset + ($index * 40)
        if ($entryOffset + 40 -gt $bytes.Length) { Stop-Audit "truncated PE section table: $Path" }
        $sections += [pscustomobject]@{
            VirtualSize = Read-PeUInt32 $bytes ($entryOffset + 8) 'section virtual size'
            VirtualAddress = Read-PeUInt32 $bytes ($entryOffset + 12) 'section virtual address'
            RawSize = Read-PeUInt32 $bytes ($entryOffset + 16) 'section raw size'
            RawOffset = Read-PeUInt32 $bytes ($entryOffset + 20) 'section raw offset'
        }
    }

    $dataDirectoryOffset = $optionalOffset + 112
    $exportRva = Read-PeUInt32 $bytes $dataDirectoryOffset 'export-directory RVA'
    $importRva = Read-PeUInt32 $bytes ($dataDirectoryOffset + 8) 'import-directory RVA'
    $importSize = Read-PeUInt32 $bytes ($dataDirectoryOffset + 12) 'import-directory size'
    $imports = [Collections.Generic.List[string]]::new()
    if ($importRva -ne 0) {
        $descriptorOffset = Convert-PeRvaToOffset $bytes $importRva $sizeOfHeaders $sections
        $terminated = $false
        $maximumDescriptors = if ($importSize -eq 0) { 4096 } else { [Math]::Min(4096, [int]($importSize / 20) + 1) }
        for ($index = 0; $index -lt $maximumDescriptors; $index++) {
            $entryOffset = $descriptorOffset + ($index * 20)
            $lookupRva = Read-PeUInt32 $bytes $entryOffset 'import descriptor'
            $timeStamp = Read-PeUInt32 $bytes ($entryOffset + 4) 'import descriptor'
            $forwarder = Read-PeUInt32 $bytes ($entryOffset + 8) 'import descriptor'
            $nameRva = Read-PeUInt32 $bytes ($entryOffset + 12) 'import name RVA'
            $addressRva = Read-PeUInt32 $bytes ($entryOffset + 16) 'import descriptor'
            if (($lookupRva -bor $timeStamp -bor $forwarder -bor $nameRva -bor $addressRva) -eq 0) {
                $terminated = $true
                break
            }
            if ($nameRva -eq 0) { Stop-Audit "PE import descriptor has no DLL name: $Path" }
            $nameOffset = Convert-PeRvaToOffset $bytes $nameRva $sizeOfHeaders $sections
            $imports.Add((Read-PeAsciiZ $bytes $nameOffset 'import').ToLowerInvariant())
        }
        if (-not $terminated) { Stop-Audit "PE import descriptor table is not terminated: $Path" }
    }

    $exports = [Collections.Generic.List[string]]::new()
    if ($exportRva -ne 0) {
        $exportOffset = Convert-PeRvaToOffset $bytes $exportRva $sizeOfHeaders $sections
        $nameCount = Read-PeUInt32 $bytes ($exportOffset + 24) 'export name count'
        $nameTableRva = Read-PeUInt32 $bytes ($exportOffset + 32) 'export name table RVA'
        if ($nameCount -gt 100000 -or ($nameCount -ne 0 -and $nameTableRva -eq 0)) { Stop-Audit "invalid PE export name table: $Path" }
        if ($nameCount -ne 0) {
            $nameTableOffset = Convert-PeRvaToOffset $bytes $nameTableRva $sizeOfHeaders $sections
            for ([uint32]$index = 0; $index -lt $nameCount; $index++) {
                $nameRva = Read-PeUInt32 $bytes ($nameTableOffset + ([int]$index * 4)) 'export name RVA'
                $nameOffset = Convert-PeRvaToOffset $bytes $nameRva $sizeOfHeaders $sections
                $exports.Add((Read-PeAsciiZ $bytes $nameOffset 'export'))
            }
        }
    }
    return [pscustomobject]@{
        Machine = $machine
        Imports = @($imports | Sort-Object -Unique)
        Exports = @($exports | Sort-Object -Unique)
    }
}

$resolvedManifest = [IO.Path]::GetFullPath($ManifestPath)
if (-not [IO.File]::Exists($resolvedManifest)) { Stop-Audit "manifest does not exist: $resolvedManifest" }
$manifest = Get-Content -LiteralPath $resolvedManifest -Raw | ConvertFrom-Json

if ($manifest.schemaVersion -ne 2 -or $manifest.rid -ne 'win-x64' -or
    $manifest.packageId -ne 'JYPPX.ROCm.HIP.CSharp.API.Runtime.win-x64') {
    Stop-Audit 'manifest identity or schema is invalid'
}
if ($manifest.source.sdkVersion -ne '7.2.0' -or
    $manifest.source.officialFileNames.runtime -ne 'amdhip64_7.dll' -or
    $manifest.source.officialFileNames.rtc -ne 'hiprtc0702.dll') {
    Stop-Audit 'SDK version and native filenames must match the ROCm 7.2 Windows build metadata'
}
if (@($manifest.source.officialDocumentation).Count -lt 3 -or @($manifest.source.buildMetadata).Count -lt 2) {
    Stop-Audit 'official documentation and build metadata provenance are incomplete'
}
if (-not $manifest.source.auditPolicy.requiresAuthenticode -or
    [string]::IsNullOrWhiteSpace([string]$manifest.source.auditPolicy.requiredSigner) -or
    [int64]$manifest.source.auditPolicy.maximumNativePayloadBytes -le 0) {
    Stop-Audit 'Authenticode signer and payload-size policy are incomplete'
}
if ($SyntheticFixture -and $RequirePackable) { Stop-Audit 'synthetic fixtures cannot satisfy a packable audit' }

if (-not $manifest.packEnabled -or -not $manifest.verified) {
    if ($RequirePackable) { Stop-Audit 'HIPSHARP1001: Windows runtime packaging is disabled or unverified' }
    if ($manifest.files.Count -ne 0) { Stop-Audit 'a disabled skeleton must not carry unaudited native files' }
    if ($manifest.source.status -ne 'local-inventory-unavailable') { Stop-Audit 'disabled skeleton must explain the unavailable local inventory' }
    Write-Output 'Windows runtime static skeleton passed (disabled; no inventory claim).'
    return
}

if ($manifest.source.status -ne 'local-inventory-audited') { Stop-Audit 'enabled manifests require an audited local SDK inventory' }
if (-not (Test-Sha256 $manifest.source.inventorySha256)) { Stop-Audit 'SDK inventory hash is missing or malformed' }
if (@($manifest.packages).Count -lt 1 -or @($manifest.files).Count -lt 2 -or @($manifest.licenses).Count -lt 1) {
    Stop-Audit 'package, file, or license inventory is incomplete'
}
foreach ($package in $manifest.packages) {
    if ([string]::IsNullOrWhiteSpace([string]$package.url) -or
        -not (Test-Sha256 ([string]$package.sha256)) -or [int64]$package.size -le 0) {
        Stop-Audit 'SDK source/archive URL, hash, or size is missing'
    }
}

$stagingRoot = [IO.Path]::GetFullPath($StagingDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar, [IO.Path]::AltDirectorySeparatorChar)
if (-not [IO.Directory]::Exists($stagingRoot)) { Stop-Audit "staging directory does not exist: $stagingRoot" }
$expectedNames = @('amdhip64_7.dll', 'hiprtc0702.dll')
$packagedNames = @($manifest.files | ForEach-Object { [IO.Path]::GetFileName([string]$_.path).ToLowerInvariant() })
foreach ($expected in $expectedNames) {
    if ($packagedNames -notcontains $expected) { Stop-Audit "required SDK file is absent: $expected" }
}
$declaredPayloadSize = [int64]0
foreach ($file in $manifest.files) { $declaredPayloadSize += [int64]$file.size }
if ($declaredPayloadSize -gt [int64]$manifest.source.auditPolicy.maximumNativePayloadBytes) {
    Stop-Audit 'native payload exceeds the configured size gate'
}
$systemAllowList = @(
    'advapi32.dll', 'bcrypt.dll', 'cfgmgr32.dll', 'combase.dll', 'crypt32.dll', 'dbghelp.dll',
    'gdi32.dll', 'kernel32.dll', 'msvcp_win.dll', 'ole32.dll', 'oleaut32.dll', 'rpcrt4.dll',
    'sechost.dll', 'setupapi.dll', 'shell32.dll', 'shlwapi.dll', 'ucrtbase.dll', 'user32.dll',
    'userenv.dll', 'version.dll', 'winmm.dll', 'ws2_32.dll', 'ntdll.dll'
)
$forbiddenDriverImports = @('amdkmdag.dll', 'amdxx64.dll', 'atikmdag.sys', 'amdkmdap.sys')

foreach ($file in $manifest.files) {
    $relativePath = ([string]$file.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
    if ([IO.Path]::IsPathRooted($relativePath) -or $relativePath.Split([IO.Path]::DirectorySeparatorChar) -contains '..') {
        Stop-Audit "unsafe package path: $($file.path)"
    }
    $fullPath = [IO.Path]::GetFullPath((Join-Path $stagingRoot $relativePath))
    if (-not ($fullPath.StartsWith($stagingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase))) {
        Stop-Audit "package path escapes staging: $($file.path)"
    }
    if ([string]$file.path -notmatch '^runtimes/win-x64/native/[^/]+\.dll$') {
        Stop-Audit "native payload must contain DLL files only: $($file.path)"
    }
    if (-not [IO.File]::Exists($fullPath)) { Stop-Audit "inventoried file is missing: $($file.path)" }
    if (-not (Test-Sha256 ([string]$file.sha256))) { Stop-Audit "file hash is malformed: $($file.path)" }
    $actualHash = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actualHash -ne ([string]$file.sha256).ToLowerInvariant()) { Stop-Audit "file hash mismatch: $($file.path)" }
    if ([int64]$file.size -ne (Get-Item -LiteralPath $fullPath).Length) { Stop-Audit "file size mismatch: $($file.path)" }

    $facts = Get-PeFacts $fullPath
    if ($facts.Machine -ne 0x8664) { Stop-Audit "PE architecture is not x64: $($file.path)" }
    $fileName = [IO.Path]::GetFileName($fullPath).ToLowerInvariant()
    $requiredExport = if ($fileName -eq 'amdhip64_7.dll') { 'hipInit' } elseif ($fileName -eq 'hiprtc0702.dll') { 'hiprtcVersion' } else { $null }
    if ($null -ne $requiredExport -and $facts.Exports -notcontains $requiredExport) {
        Stop-Audit "required export evidence is missing: $requiredExport"
    }
    if (-not $SyntheticFixture) {
        $signature = Get-AuthenticodeSignature -LiteralPath $fullPath
        if ($signature.Status -ne 'Valid' -or $null -eq $signature.SignerCertificate -or
            -not $signature.SignerCertificate.Subject.Contains([string]$manifest.source.auditPolicy.requiredSigner, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-Audit "Authenticode signature is invalid or not issued to the required AMD signer: $($file.path)"
        }
    }
    foreach ($import in $facts.Imports) {
        if ($forbiddenDriverImports -contains $import) { Stop-Audit "driver-boundary import is forbidden: $import" }
        if ($import -like 'api-ms-win-*.dll' -or $import -like 'ext-ms-win-*.dll') { continue }
        if ($systemAllowList -contains $import -or $packagedNames -contains $import) { continue }
        Stop-Audit "dependency closure is incomplete for import: $import"
    }
}

$nativeRoot = [IO.Path]::GetFullPath((Join-Path $stagingRoot ([string]$manifest.nativeAssetPath)))
if (-not $nativeRoot.StartsWith($stagingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.Directory]::Exists($nativeRoot)) { Stop-Audit 'native asset directory is missing or escapes staging' }
$declaredNativePaths = @($manifest.files | ForEach-Object { ([string]$_.path).Replace('\', '/').ToLowerInvariant() } | Sort-Object -Unique)
$actualNativePaths = @(Get-ChildItem -LiteralPath $nativeRoot -Recurse -File | ForEach-Object {
    ([IO.Path]::GetRelativePath($stagingRoot, $_.FullName)).Replace('\', '/').ToLowerInvariant()
} | Sort-Object -Unique)
$inventoryDifference = @(Compare-Object -ReferenceObject $declaredNativePaths -DifferenceObject $actualNativePaths)
if ($inventoryDifference.Count -ne 0) {
    $inventoryDetail = ($inventoryDifference | ForEach-Object { $_.SideIndicator + ':' + $_.InputObject }) -join ', '
    Stop-Audit "undeclared or missing native payload was found in staging ($inventoryDetail)"
}

foreach ($license in $manifest.licenses) {
    if (-not (Test-Sha256 ([string]$license.sha256)) -or [string]::IsNullOrWhiteSpace([string]$license.expression)) {
        Stop-Audit 'license evidence is missing an SPDX expression or hash'
    }
    $licensePath = [IO.Path]::GetFullPath((Join-Path $stagingRoot ([string]$license.packagePath)))
    if (-not $licensePath.StartsWith($stagingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        -not [IO.File]::Exists($licensePath)) { Stop-Audit 'license evidence is missing or escapes staging' }
    $licenseHash = (Get-FileHash -LiteralPath $licensePath -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($licenseHash -ne ([string]$license.sha256).ToLowerInvariant()) { Stop-Audit 'license evidence hash mismatch' }
}

if (-not $manifest.verification.provenanceVerified -or -not $manifest.verification.closureVerified -or
    -not $manifest.verification.licensesVerified -or -not $manifest.verification.sbomVerified -or
    -not $manifest.verification.packageAuditVerified) {
    Stop-Audit 'static verification flags are incomplete'
}
$sbomRelativePath = ([string]$manifest.sbom.path).Replace('/', [IO.Path]::DirectorySeparatorChar)
if ([IO.Path]::IsPathRooted($sbomRelativePath) -or $sbomRelativePath.Split([IO.Path]::DirectorySeparatorChar) -contains '..' -or
    -not (Test-Sha256 ([string]$manifest.sbom.sha256))) { Stop-Audit 'SBOM path or hash is missing or unsafe' }
$sbomPath = [IO.Path]::GetFullPath((Join-Path $stagingRoot $sbomRelativePath))
if (-not $sbomPath.StartsWith($stagingRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
    -not [IO.File]::Exists($sbomPath) -or
    (Get-FileHash -LiteralPath $sbomPath -Algorithm SHA256).Hash.ToLowerInvariant() -ne ([string]$manifest.sbom.sha256).ToLowerInvariant()) {
    Stop-Audit 'SBOM file is missing, escapes staging, or has a hash mismatch'
}
if ($RequirePackable -and -not $manifest.verification.gpuValidated) {
    Stop-Audit 'HIPSHARP1001: Windows GPU validation is required before packaging'
}

Write-Output 'Windows runtime static audit passed.'
