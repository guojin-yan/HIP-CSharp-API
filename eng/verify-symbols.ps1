[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LibraryPath,
    [Parameter(Mandatory = $true)]
    [ValidateSet("amdhip64", "hiprtc")]
    [string]$LibraryName,
    [string]$OutputPath = "artifacts/abi/hip-runtime-symbols.json",
    [switch]$RequireOptional,
    [switch]$CompleteModel,
    [string[]]$AllowMissing = @()
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
Import-Module (Join-Path $PSScriptRoot "build-paths.psm1") -Force
$resolvedLibrary = (Resolve-Path -LiteralPath $LibraryPath).Path
$manifestName = if ($CompleteModel) { 'complete-api-model.json' } else { 'interop-manifest.json' }
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "eng/interop/$manifestName") | ConvertFrom-Json
$functions = if ($CompleteModel) {
    if ($LibraryName -eq 'amdhip64') { @($manifest.runtimeFunctions) } else { @($manifest.rtcFunctions) }
} else { @($manifest.functions | Where-Object library -EQ $LibraryName) }
foreach ($name in $AllowMissing) {
    if ($name -cnotin @($functions.entryPoint)) { throw "Allowed missing export is not declared by this model: $name" }
}
$results = [System.Collections.Generic.List[object]]::new()
$handle = [System.Runtime.InteropServices.NativeLibrary]::Load($resolvedLibrary)

try {
    foreach ($function in $functions) {
        $address = [IntPtr]::Zero
        $found = [System.Runtime.InteropServices.NativeLibrary]::TryGetExport($handle, [string]$function.entryPoint, [ref]$address)
        $results.Add([pscustomobject]@{
            entryPoint = [string]$function.entryPoint
            required = $CompleteModel -or $RequireOptional -or -not [bool]$function.optional
            allowedMissing = [string]$function.entryPoint -cin $AllowMissing
            found = $found
        })
    }
}
finally {
    [System.Runtime.InteropServices.NativeLibrary]::Free($handle)
}

$missing = @($results | Where-Object { $_.required -and -not $_.found -and -not $_.allowedMissing })
$absoluteOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Get-HipSharpOutputPath $repositoryRoot $OutputPath }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $absoluteOutput) | Out-Null
[pscustomobject]@{
    library = $resolvedLibrary
    libraryName = $LibraryName
    manifest = $manifestName
    manifestVersion = $(if ($CompleteModel) { 'ROCm-7.2.1-complete' } else { $manifest.headerVersion })
    completeModel = [bool]$CompleteModel
    expectedCount = $results.Count
    foundCount = @($results | Where-Object found).Count
    allowedMissing = $AllowMissing
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    symbols = $results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $absoluteOutput -Encoding UTF8

if ($missing.Count -ne 0) {
    throw "Required HIP exports are missing: $($missing.entryPoint -join ', ')"
}

Write-Host "Exports found=$(@($results | Where-Object found).Count)/$($results.Count), reviewed missing=$(@($results | Where-Object { -not $_.found -and $_.allowedMissing }).Count) for $LibraryName. Report: $absoluteOutput"
