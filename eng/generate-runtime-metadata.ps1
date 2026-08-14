[CmdletBinding()]
param(
    [string]$Manifest = "nuget/runtime-manifests/linux-x64.json",
    [switch]$Check
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "runtime-manifest.psm1") -Force
$manifestPath = if ([System.IO.Path]::IsPathRooted($Manifest)) { $Manifest } else { Join-Path $repositoryRoot $Manifest }
$manifestInfo = Get-HipSharpRuntimeManifest $manifestPath
$runtimeManifest = $manifestInfo.Value
Assert-HipSharpRuntimeManifest $runtimeManifest
$outputDirectory = Split-Path -Parent $manifestInfo.Path
$prefix = [System.IO.Path]::GetFileNameWithoutExtension($manifestInfo.Path)

function Write-DeterministicJson([string]$path, [object]$value) {
    $content = (($value | ConvertTo-Json -Depth 20) -replace "`r?`n", "`r`n") + "`r`n"
    if ($Check) {
        if (-not (Test-Path -LiteralPath $path) -or (Get-Content -Raw -LiteralPath $path).TrimEnd() -ne $content.TrimEnd()) { throw "Generated runtime metadata is stale: $path" }
    } else {
        [System.IO.File]::WriteAllText($path, $content, [System.Text.UTF8Encoding]::new($false))
    }
}

$licensesByPackage = @{}
foreach ($license in @($runtimeManifest.licenses)) { $licensesByPackage[$license.sourcePackage] = $license.expression }
$packageComponents = @($runtimeManifest.packages | Sort-Object name | ForEach-Object {
    [ordered]@{
        "bom-ref" = "pkg:deb/$($_.name)@$($_.version)?arch=amd64"
        type = "library"
        name = $_.name
        version = $_.version
        hashes = @([ordered]@{ alg = "SHA-256"; content = $_.sha256.ToUpperInvariant() })
        licenses = @([ordered]@{ expression = $licensesByPackage[$_.name] })
        properties = @(
            [ordered]@{ name = "hipsharp:source-url"; value = $_.url },
            [ordered]@{ name = "hipsharp:included-runtime-payload"; value = [string]($_.name -in $runtimeManifest.dependencyClosure.rocmPayloadPackages) }
        )
    }
})
$fileComponents = @($runtimeManifest.files | Sort-Object path | ForEach-Object {
    [ordered]@{
        "bom-ref" = "file:$($_.path)"
        type = "file"
        name = $_.path
        hashes = @([ordered]@{ alg = "SHA-256"; content = $_.sha256.ToUpperInvariant() })
        properties = @(
            [ordered]@{ name = "hipsharp:source-package"; value = $_.sourcePackage },
            [ordered]@{ name = "hipsharp:size"; value = [string]$_.size }
        )
    }
})
$rootRef = "pkg:nuget/$($runtimeManifest.packageId)@$($runtimeManifest.packageVersion)"
$dependencies = [System.Collections.Generic.List[object]]::new()
$dependencies.Add([ordered]@{ ref = $rootRef; dependsOn = @($runtimeManifest.files | Sort-Object path | ForEach-Object { "file:$($_.path)" }) })
foreach ($file in @($runtimeManifest.files | Sort-Object path)) {
    $sourcePackage = @($runtimeManifest.packages | Where-Object name -eq $file.sourcePackage)[0]
    $dependencies.Add([ordered]@{ ref = "file:$($file.path)"; dependsOn = @("pkg:deb/$($file.sourcePackage)@$($sourcePackage.version)?arch=amd64") })
}
$sbom = [ordered]@{
    bomFormat = "CycloneDX"
    specVersion = "1.5"
    version = 1
    metadata = [ordered]@{
        component = [ordered]@{ "bom-ref" = $rootRef; type = "application"; name = $runtimeManifest.packageId; version = $runtimeManifest.packageVersion; properties = @([ordered]@{ name = "hipsharp:rid"; value = $runtimeManifest.rid }, [ordered]@{ name = "hipsharp:rocm"; value = $runtimeManifest.rocm }) }
    }
    components = @($packageComponents + $fileComponents)
    dependencies = @($dependencies)
}
Write-DeterministicJson (Join-Path $outputDirectory $runtimeManifest.sbom.path) $sbom

$provenance = [ordered]@{
    schemaVersion = 1
    packageId = $runtimeManifest.packageId
    packageVersion = $runtimeManifest.packageVersion
    rid = $runtimeManifest.rid
    source = $runtimeManifest.source
    packages = @($runtimeManifest.packages | Sort-Object name)
}
Write-DeterministicJson (Join-Path $outputDirectory "$prefix.provenance.json") $provenance

$closure = [ordered]@{
    schemaVersion = 1
    packageId = $runtimeManifest.packageId
    dependencyClosure = $runtimeManifest.dependencyClosure
    files = @($runtimeManifest.files | Where-Object { -not $_.ContainsKey("aliasFor") } | Sort-Object path | ForEach-Object { [ordered]@{ path = $_.path; soname = $_.soname; needed = @($_.needed | Sort-Object); rpath = $_.rpath; purpose = $_.purpose } })
    systemDependencies = @($runtimeManifest.systemDependencies | Sort-Object soname)
    driverBoundary = $runtimeManifest.driverBoundary
}
Write-DeterministicJson (Join-Path $outputDirectory "$prefix.dependency-closure.json") $closure

$licenseInventory = [ordered]@{
    schemaVersion = 1
    packageId = $runtimeManifest.packageId
    licenses = @($runtimeManifest.licenses | Sort-Object sourcePackage)
}
Write-DeterministicJson (Join-Path $outputDirectory "$prefix.licenses.json") $licenseInventory

$unpacked = [int64](($runtimeManifest.files | Measure-Object size -Sum).Sum)
if ($runtimeManifest.size.unpackedBytes -ne $unpacked) { throw "Manifest unpacked size is stale. Expected $unpacked, found $($runtimeManifest.size.unpackedBytes)." }
$size = [ordered]@{
    schemaVersion = 1
    packageId = $runtimeManifest.packageId
    unpackedBytes = $unpacked
    packageBytes = $runtimeManifest.size.packageBytes
    nugetLimitBytes = $runtimeManifest.size.nugetLimitBytes
    topology = $runtimeManifest.size.topology
    decision = $runtimeManifest.size.decision
}
Write-DeterministicJson (Join-Path $outputDirectory "$prefix.sizes.json") $size

Write-Host "Runtime metadata generated for $($runtimeManifest.packageId)."
