[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$LibraryPath,
    [string]$OutputPath = "artifacts/abi/hip-runtime-symbols.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$resolvedLibrary = (Resolve-Path -LiteralPath $LibraryPath).Path
$manifest = Get-Content -Raw -LiteralPath (Join-Path $repositoryRoot "eng/interop/interop-manifest.json") | ConvertFrom-Json
$results = [System.Collections.Generic.List[object]]::new()
$handle = [System.Runtime.InteropServices.NativeLibrary]::Load($resolvedLibrary)

try {
    foreach ($function in $manifest.functions) {
        $address = [IntPtr]::Zero
        $found = [System.Runtime.InteropServices.NativeLibrary]::TryGetExport($handle, [string]$function.entryPoint, [ref]$address)
        $results.Add([pscustomobject]@{
            entryPoint = [string]$function.entryPoint
            required = -not [bool]$function.optional
            found = $found
        })
    }
}
finally {
    [System.Runtime.InteropServices.NativeLibrary]::Free($handle)
}

$missing = @($results | Where-Object { $_.required -and -not $_.found })
$absoluteOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) { $OutputPath } else { Join-Path $repositoryRoot $OutputPath }
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $absoluteOutput) | Out-Null
[pscustomobject]@{
    library = $resolvedLibrary
    manifestVersion = $manifest.headerVersion
    generatedAtUtc = [DateTime]::UtcNow.ToString("O")
    symbols = $results
} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $absoluteOutput -Encoding UTF8

if ($missing.Count -ne 0) {
    throw "Required HIP exports are missing: $($missing.entryPoint -join ', ')"
}

Write-Host "Verified $($results.Count) HIP Runtime exports. Report: $absoluteOutput"
