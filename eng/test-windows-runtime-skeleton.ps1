[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$verifier = Join-Path $PSScriptRoot 'verify-windows-runtime.ps1'
$testRoot = Join-Path ([IO.Path]::GetTempPath()) ('hipsharp-windows-audit-' + [Guid]::NewGuid().ToString('N'))

function Write-U16([byte[]] $Bytes, [int] $Offset, [uint16] $Value) {
    [BitConverter]::GetBytes($Value).CopyTo($Bytes, $Offset)
}

function Write-U32([byte[]] $Bytes, [int] $Offset, [uint32] $Value) {
    [BitConverter]::GetBytes($Value).CopyTo($Bytes, $Offset)
}

function Write-TestPe([string] $Path, [uint16] $Machine, [string[]] $Imports, [string[]] $Exports) {
    [byte[]] $bytes = New-Object byte[] 2560
    $peOffset = 0x80
    $optionalOffset = $peOffset + 24
    $sectionOffset = $optionalOffset + 0xf0
    $sectionRawOffset = 0x200
    $sectionRva = 0x1000
    $bytes[0] = 0x4d
    $bytes[1] = 0x5a
    Write-U32 $bytes 0x3c $peOffset
    $bytes[$peOffset] = 0x50
    $bytes[$peOffset + 1] = 0x45
    Write-U16 $bytes ($peOffset + 4) $Machine
    Write-U16 $bytes ($peOffset + 6) 1
    Write-U16 $bytes ($peOffset + 20) 0xf0
    Write-U16 $bytes ($peOffset + 22) 0x2022
    Write-U16 $bytes $optionalOffset 0x20b
    Write-U32 $bytes ($optionalOffset + 32) 0x1000
    Write-U32 $bytes ($optionalOffset + 36) 0x200
    Write-U32 $bytes ($optionalOffset + 56) 0x2000
    Write-U32 $bytes ($optionalOffset + 60) 0x200
    Write-U16 $bytes ($optionalOffset + 68) 3
    Write-U32 $bytes ($optionalOffset + 108) 16
    [Text.Encoding]::ASCII.GetBytes('.rdata').CopyTo($bytes, $sectionOffset)
    Write-U32 $bytes ($sectionOffset + 8) 0x800
    Write-U32 $bytes ($sectionOffset + 12) $sectionRva
    Write-U32 $bytes ($sectionOffset + 16) 0x800
    Write-U32 $bytes ($sectionOffset + 20) $sectionRawOffset
    Write-U32 $bytes ($sectionOffset + 36) 0x40000040

    $cursor = $sectionRawOffset
    if ($Imports.Count -ne 0) {
        $descriptorOffset = $cursor
        $cursor += ($Imports.Count + 1) * 20
        for ($index = 0; $index -lt $Imports.Count; $index++) {
            [byte[]] $nameBytes = [Text.Encoding]::ASCII.GetBytes($Imports[$index] + "`0")
            $nameBytes.CopyTo($bytes, $cursor)
            Write-U32 $bytes ($descriptorOffset + ($index * 20) + 12) ($sectionRva + $cursor - $sectionRawOffset)
            $cursor += $nameBytes.Length
        }
        Write-U32 $bytes ($optionalOffset + 120) ($sectionRva + $descriptorOffset - $sectionRawOffset)
        Write-U32 $bytes ($optionalOffset + 124) (($Imports.Count + 1) * 20)
    }

    $cursor = ($cursor + 3) -band -bnot 3
    if ($Exports.Count -ne 0) {
        $exportOffset = $cursor
        $cursor += 40
        $nameTableOffset = $cursor
        $cursor += $Exports.Count * 4
        for ($index = 0; $index -lt $Exports.Count; $index++) {
            [byte[]] $nameBytes = [Text.Encoding]::ASCII.GetBytes($Exports[$index] + "`0")
            Write-U32 $bytes ($nameTableOffset + ($index * 4)) ($sectionRva + $cursor - $sectionRawOffset)
            $nameBytes.CopyTo($bytes, $cursor)
            $cursor += $nameBytes.Length
        }
        Write-U32 $bytes ($exportOffset + 24) $Exports.Count
        Write-U32 $bytes ($exportOffset + 32) ($sectionRva + $nameTableOffset - $sectionRawOffset)
        Write-U32 $bytes ($optionalOffset + 112) ($sectionRva + $exportOffset - $sectionRawOffset)
        Write-U32 $bytes ($optionalOffset + 116) ($cursor - $exportOffset)
    }

    [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($Path)) | Out-Null
    [IO.File]::WriteAllBytes($Path, $bytes)
}

function New-Case([string] $Name) {
    $caseRoot = Join-Path $testRoot $Name
    $nativeRoot = Join-Path $caseRoot 'runtimes\win-x64\native'
    $licenseRoot = Join-Path $caseRoot 'licenses'
    [IO.Directory]::CreateDirectory($nativeRoot) | Out-Null
    [IO.Directory]::CreateDirectory($licenseRoot) | Out-Null
    $runtimePath = Join-Path $nativeRoot 'amdhip64_7.dll'
    $rtcPath = Join-Path $nativeRoot 'hiprtc0702.dll'
    Write-TestPe $runtimePath 0x8664 @('KERNEL32.dll', 'hiprtc0702.dll') @('hipInit')
    Write-TestPe $rtcPath 0x8664 @('KERNEL32.dll') @('hiprtcVersion')
    $licensePath = Join-Path $licenseRoot 'AMD-HIP-SDK-EULA.txt'
    [IO.File]::WriteAllText($licensePath, 'Synthetic license fixture for static-audit tests.')
    $sbomPath = Join-Path $caseRoot 'win-x64.cdx.json'
    [IO.File]::WriteAllText($sbomPath, '{"bomFormat":"CycloneDX","specVersion":"1.5"}')

    $files = @(
        [ordered]@{ path = 'runtimes/win-x64/native/amdhip64_7.dll'; sha256 = (Get-FileHash $runtimePath -Algorithm SHA256).Hash; size = (Get-Item $runtimePath).Length },
        [ordered]@{ path = 'runtimes/win-x64/native/hiprtc0702.dll'; sha256 = (Get-FileHash $rtcPath -Algorithm SHA256).Hash; size = (Get-Item $rtcPath).Length }
    )
    $manifest = [ordered]@{
        schemaVersion = 2
        packageId = 'JYPPX.ROCm.HipSharp.Runtime.win-x64'
        packageVersion = '7.2.0'
        rid = 'win-x64'
        nativeAssetPath = 'runtimes/win-x64/native'
        packEnabled = $true
        verified = $true
        source = [ordered]@{
            status = 'local-inventory-audited'; sdkVersion = '7.2.0'; architecture = 'x64'
            officialFileNames = [ordered]@{ runtime = 'amdhip64_7.dll'; rtc = 'hiprtc0702.dll' }
            officialDocumentation = @('https://example.test/requirements', 'https://example.test/install', 'https://example.test/deploy')
            buildMetadata = @('https://example.test/runtime-build', 'https://example.test/rtc-build')
            auditPolicy = [ordered]@{ requiresAuthenticode = $true; requiredSigner = 'Advanced Micro Devices'; maximumNativePayloadBytes = 10000; nativePayload = 'DLL only' }
            inventorySha256 = ('a' * 64)
        }
        packages = @([ordered]@{ name = 'AMD HIP SDK'; version = '7.2.0'; url = 'https://example.test/sdk.exe'; sha256 = ('b' * 64); size = 1 })
        files = $files
        licenses = @([ordered]@{ expression = 'LicenseRef-AMD-HIP-SDK'; packagePath = 'licenses/AMD-HIP-SDK-EULA.txt'; sha256 = (Get-FileHash $licensePath -Algorithm SHA256).Hash })
        dependencyClosure = [ordered]@{ decision = 'fixture' }
        systemDependencies = @()
        driverBoundary = [ordered]@{ excludedFromPackage = @('driver') }
        sbom = [ordered]@{ format = 'CycloneDX-1.5'; path = 'win-x64.cdx.json'; sha256 = (Get-FileHash $sbomPath -Algorithm SHA256).Hash }
        verification = [ordered]@{ provenanceVerified = $true; closureVerified = $true; licensesVerified = $true; sbomVerified = $true; packageAuditVerified = $true; gpuValidated = $false }
    }
    return [pscustomobject]@{ Root = $caseRoot; Manifest = $manifest; RuntimePath = $runtimePath; RtcPath = $rtcPath }
}

function Write-Manifest($Case) {
    $path = Join-Path $Case.Root 'manifest.json'
    [IO.File]::WriteAllText($path, ($Case.Manifest | ConvertTo-Json -Depth 20))
    return $path
}

function Assert-Rejected([string] $Name, [scriptblock] $Mutation, [string] $Expected) {
    $case = New-Case $Name
    & $Mutation $case
    $manifestPath = Write-Manifest $case
    try {
        & $verifier -ManifestPath $manifestPath -StagingDirectory $case.Root -SyntheticFixture | Out-Null
        throw "Case '$Name' unexpectedly passed."
    }
    catch {
        if (-not $_.Exception.Message.Contains($Expected, [StringComparison]::OrdinalIgnoreCase)) { throw }
    }
}

try {
    [IO.Directory]::CreateDirectory($testRoot) | Out-Null
    $valid = New-Case 'valid'
    & $verifier -ManifestPath (Write-Manifest $valid) -StagingDirectory $valid.Root -SyntheticFixture | Out-Null

    Assert-Rejected 'missing-source-hash' { param($case) $case.Manifest.packages[0].sha256 = $null } 'source/archive'
    Assert-Rejected 'missing-license' { param($case) $case.Manifest.licenses = @() } 'license inventory'
    Assert-Rejected 'bad-file-hash' { param($case) $case.Manifest.files[0].sha256 = ('0' * 64) } 'hash mismatch'
    Assert-Rejected 'wrong-architecture' {
        param($case)
        Write-TestPe $case.RuntimePath 0x014c @('KERNEL32.dll', 'hiprtc0702.dll') @('hipInit')
        $case.Manifest.files[0].sha256 = (Get-FileHash $case.RuntimePath -Algorithm SHA256).Hash
    } 'not x64'
    Assert-Rejected 'incomplete-closure' {
        param($case)
        Write-TestPe $case.RuntimePath 0x8664 @('KERNEL32.dll', 'hiprtc0702.dll', 'missing-runtime.dll') @('hipInit')
        $case.Manifest.files[0].sha256 = (Get-FileHash $case.RuntimePath -Algorithm SHA256).Hash
    } 'closure is incomplete'
    Assert-Rejected 'driver-boundary' {
        param($case)
        Write-TestPe $case.RuntimePath 0x8664 @('KERNEL32.dll', 'amdkmdag.dll') @('hipInit')
        $case.Manifest.files[0].sha256 = (Get-FileHash $case.RuntimePath -Algorithm SHA256).Hash
    } 'driver-boundary'
    Assert-Rejected 'path-escape' { param($case) $case.Manifest.files[0].path = '../amdhip64_7.dll' } 'unsafe package path'
    Assert-Rejected 'missing-export' {
        param($case)
        Write-TestPe $case.RuntimePath 0x8664 @('KERNEL32.dll', 'hiprtc0702.dll') @()
        $case.Manifest.files[0].sha256 = (Get-FileHash $case.RuntimePath -Algorithm SHA256).Hash
    } 'required export evidence'
    Assert-Rejected 'oversize-payload' { param($case) $case.Manifest.source.auditPolicy.maximumNativePayloadBytes = 1 } 'size gate'
    Assert-Rejected 'missing-sbom' { param($case) $case.Manifest.sbom.sha256 = $null } 'SBOM path or hash'
    Assert-Rejected 'incomplete-verification' { param($case) $case.Manifest.verification.packageAuditVerified = $false } 'verification flags'
    Assert-Rejected 'undeclared-native-payload' {
        param($case)
        [IO.File]::WriteAllText((Join-Path $case.Root 'runtimes\win-x64\native\debug-helper.dll'), 'not declared')
    } 'undeclared or missing native payload'

    Write-Output 'Windows runtime static-audit positive and negative tests passed (12 rejection cases).'
}
finally {
    $resolvedTemp = [IO.Path]::GetFullPath([IO.Path]::GetTempPath())
    $resolvedTest = [IO.Path]::GetFullPath($testRoot)
    if ($resolvedTest.StartsWith($resolvedTemp, [StringComparison]::OrdinalIgnoreCase) -and [IO.Directory]::Exists($resolvedTest)) {
        Remove-Item -LiteralPath $resolvedTest -Recurse -Force
    }
}
