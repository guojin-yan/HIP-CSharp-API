[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$HipRoot,
    [Parameter(Mandatory = $true)]
    [string]$Architecture,
    [string]$OutputRoot = (Join-Path (Split-Path -Parent (Split-Path -Parent $PSScriptRoot)) 'build/local-windows'),
    [switch]$NoBuild
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
if (-not $IsWindows) { throw 'This compatibility entry point requires Windows; use test-local-gpu.ps1 on Linux.' }
$PSBoundParameters['OutputRoot'] = $OutputRoot
& (Join-Path $PSScriptRoot 'test-local-gpu.ps1') @PSBoundParameters
