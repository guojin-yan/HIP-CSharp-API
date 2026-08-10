[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet("generate", "probe-manifest")]
    [string]$Command = "generate",
    [Alias("Verify")]
    [switch]$Check,
    [string]$HeaderRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$manifestPath = Join-Path $repositoryRoot "eng/interop/interop-manifest.json"
$outputPath = Join-Path $repositoryRoot "src/JYPPX.HipSharp/Generated/HipNativeMethods.g.cs"
$normalizedPath = Join-Path $repositoryRoot "eng/interop/normalized-model.json"
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json

function Convert-ToCanonicalObject([object]$value) {
    if ($null -eq $value) { return $null }
    if ($value -is [System.Management.Automation.PSCustomObject]) {
        $ordered = [ordered]@{}
        foreach ($property in @($value.PSObject.Properties | Sort-Object Name)) {
            $ordered[$property.Name] = Convert-ToCanonicalObject $property.Value
        }
        return $ordered
    }
    if ($value -is [System.Collections.IDictionary]) {
        $ordered = [ordered]@{}
        foreach ($key in @($value.Keys | Sort-Object)) {
            $ordered[[string]$key] = Convert-ToCanonicalObject $value[$key]
        }
        return $ordered
    }
    if ($value -is [System.Collections.IEnumerable] -and $value -isnot [string]) {
        $items = [System.Collections.Generic.List[object]]::new()
        foreach ($item in $value) { $items.Add((Convert-ToCanonicalObject $item)) }
        return ,$items.ToArray()
    }
    return $value
}

function Get-NormalizedManifest {
    $canonical = Convert-ToCanonicalObject $manifest
    return ($canonical | ConvertTo-Json -Depth 100 -Compress) + "`n"
}

function Get-Sha256([string]$text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    try {
        $bytes = [System.Text.UTF8Encoding]::new($false).GetBytes($text)
        return ([System.BitConverter]::ToString($sha.ComputeHash($bytes))).Replace("-", "")
    }
    finally { $sha.Dispose() }
}

function Test-Headers([string]$root) {
    if ([string]::IsNullOrWhiteSpace($root)) { return @() }
    $resolvedRoot = [System.IO.Path]::GetFullPath($root)
    $rootPrefix = $resolvedRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    $results = [System.Collections.Generic.List[object]]::new()
    foreach ($header in @($manifest.verifiedHeaders)) {
        $path = Join-Path $resolvedRoot ([string]$header.path)
        $resolvedPath = [System.IO.Path]::GetFullPath($path)
        if ($resolvedPath -ne $resolvedRoot -and -not $resolvedPath.StartsWith($rootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            throw "Header path escapes the explicitly supplied HeaderRoot: $($header.path)"
        }
        if (-not (Test-Path -LiteralPath $resolvedPath -PathType Leaf)) {
            throw "Official header is missing from the explicitly supplied HeaderRoot: $($header.path)"
        }
        $actual = (Get-FileHash -LiteralPath $resolvedPath -Algorithm SHA256).Hash.ToUpperInvariant()
        if ($actual -ne ([string]$header.sha256).ToUpperInvariant()) {
            throw "Official header SHA-256 mismatch for $($header.path)."
        }
        $results.Add([pscustomobject]@{ path = [string]$header.path; sha256 = $actual })
    }
    return $results.ToArray()
}

$normalized = Get-NormalizedManifest
$normalizedHash = Get-Sha256 $normalized

if ($Command -eq "probe-manifest") {
    $headerResults = @(Test-Headers $HeaderRoot)
    $summary = [ordered]@{
        schemaVersion = [int]$manifest.schemaVersion
        generatorVersion = [string]$manifest.generatorVersion
        rocmTag = [string]$manifest.rocmTag
        hipTag = [string]$manifest.hipTag
        normalizedManifestSha256 = $normalizedHash
        headerRootSupplied = -not [string]::IsNullOrWhiteSpace($HeaderRoot)
        headers = $headerResults
        functionCount = @($manifest.functions).Count
        libraries = @($manifest.libraries | ForEach-Object { [string]$_ })
    }
    $summary | ConvertTo-Json -Depth 10
    exit 0
}

if ($Check) {
    if (-not (Test-Path -LiteralPath $outputPath -PathType Leaf)) {
        throw "Generated interop file is missing: $outputPath"
    }
    if (-not (Test-Path -LiteralPath $normalizedPath -PathType Leaf)) {
        throw "Normalized model is missing: $normalizedPath"
    }
    $current = [System.IO.File]::ReadAllText($outputPath).Replace("`r`n", "`n")
    $expectedModel = [System.IO.File]::ReadAllText($normalizedPath).Replace("`r`n", "`n")
    if ($expectedModel -ne $normalized) { throw "Normalized model drift detected; run generate first." }
    if ($current -notlike "*Normalized manifest SHA-256: $normalizedHash*") {
        throw "Generated interop hash header is stale; run generate first."
    }
    Write-Host "Generated interop and normalized model verified (SHA-256 $normalizedHash)."
    exit 0
}

$lines = [System.Collections.Generic.List[string]]::new()
$lines.Add("// <auto-generated />")
$lines.Add("// Source: eng/interop/interop-manifest.json")
$lines.Add("// Generator version: $($manifest.generatorVersion)")
$lines.Add("// Normalized manifest SHA-256: $normalizedHash")
$lines.Add("")
$lines.Add("using System;")
$lines.Add("using System.Runtime.CompilerServices;")
$lines.Add("using System.Runtime.InteropServices;")
$lines.Add("using JYPPX.HipSharp.Memory;")
$lines.Add("using JYPPX.HipSharp.Rtc;")
$lines.Add("using JYPPX.HipSharp.Types;")
$lines.Add("")
$lines.Add("namespace JYPPX.HipSharp.Generated;")
$lines.Add("")
$lines.Add("/// <summary>")
$lines.Add("/// 提供由规范化 manifest 生成的 HIP C ABI 声明 / Provides HIP C ABI declarations generated from the normalized manifest.")
$lines.Add("/// </summary>")
$lines.Add("internal static partial class HipNativeMethods")
$lines.Add("{")

foreach ($function in @($manifest.functions)) {
    $parameters = @($function.parameters | ForEach-Object { $_.declaration }) -join ", "
    $importName = switch ([string]$function.library) {
        "amdhip64" { "HipNativeLibraryNames.RuntimeImportName" }
        "hiprtc" { "HipNativeLibraryNames.RtcImportName" }
        default { throw "Unsupported native library '$($function.library)' for $($function.entryPoint)." }
    }
    $lines.Add("    /// <summary>")
    $lines.Add("    /// $($function.summaryZh) / $($function.summaryEn).")
    $lines.Add("    /// </summary>")
    foreach ($parameter in @($function.parameters)) {
        $lines.Add("    /// <param name=`"$($parameter.name)`">方向：$($parameter.direction)，所有权：$($parameter.ownership) / Direction: $($parameter.direction); ownership: $($parameter.ownership).</param>")
    }
    $lines.Add("    /// <returns>原生返回值 / Native return value.</returns>")
    $lines.Add("#if NET7_0_OR_GREATER")
    $lines.Add("    [LibraryImport($importName, EntryPoint = `"$($function.entryPoint)`")]")
    $lines.Add("    [UnmanagedCallConv(CallConvs = new[] { typeof(CallConvCdecl) })]")
    $lines.Add("    internal static partial $($function.returnType) $($function.managedName)($parameters);")
    $lines.Add("#else")
    $lines.Add("    [DllImport($importName, EntryPoint = `"$($function.entryPoint)`", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]")
    $lines.Add("    internal static extern $($function.returnType) $($function.managedName)($parameters);")
    $lines.Add("#endif")
    $lines.Add("")
}

$lines.Add("}")
$generated = ($lines -join "`n") + "`n"
[System.IO.File]::WriteAllText($normalizedPath, $normalized, [System.Text.UTF8Encoding]::new($false))
[System.IO.File]::WriteAllText($outputPath, $generated, [System.Text.UTF8Encoding]::new($false))
Write-Host "Generated interop and normalized model (SHA-256 $normalizedHash)."
