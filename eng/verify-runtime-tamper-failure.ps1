[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][int]$ExitCode,
    [Parameter(Mandatory = $true)][string]$EvidencePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($ExitCode -eq 0) {
    throw "Tampered Runtime package verification unexpectedly succeeded."
}

$evidence = Get-Content -LiteralPath $EvidencePath -Raw
if ($evidence.Contains("Runtime package hash/size mismatch:", [System.StringComparison]::Ordinal)) {
    Write-Output "Tampered Runtime package rejection accepted: hash/size verification."
    exit 0
}

$signatureRejected =
    $evidence.Contains("Runtime package repository signature verification failed:", [System.StringComparison]::Ordinal) -and
    $evidence.Contains("error: NU3005:", [System.StringComparison]::Ordinal) -and
    $evidence.Contains("Package signature validation failed.", [System.StringComparison]::Ordinal)
if ($signatureRejected) {
    Write-Output "Tampered Runtime package rejection accepted: NuGet signature verification (NU3005)."
    exit 0
}

throw "Tampered Runtime package did not fail through an accepted package verification path."
