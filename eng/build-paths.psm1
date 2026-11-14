Set-StrictMode -Version Latest

function Get-HipSharpOutputPath {
    param([string]$RepositoryRoot, [string]$RelativePath)
    $root = if ([string]::IsNullOrWhiteSpace($env:HIPSHARP_OUTPUT_ROOT)) { $RepositoryRoot } else { [IO.Path]::GetFullPath($env:HIPSHARP_OUTPUT_ROOT) }
    return [IO.Path]::GetFullPath((Join-Path $root $RelativePath))
}

function Get-HipSharpAssemblyPath {
    param([string]$RepositoryRoot, [string]$ProjectName, [string]$Configuration, [string]$Framework, [string]$FileName, [string]$ProjectDirectory)
    if ([string]::IsNullOrWhiteSpace($env:HIPSHARP_OUTPUT_ROOT)) {
        return Join-Path $RepositoryRoot "$ProjectDirectory/bin/$Configuration/$Framework/$FileName"
    }
    return Join-Path ([IO.Path]::GetFullPath($env:HIPSHARP_OUTPUT_ROOT)) "bin/$ProjectName/$Configuration/$Framework/$FileName"
}

function Get-HipSharpSupportFeed {
    if ([string]::IsNullOrWhiteSpace($env:HIPSHARP_SUPPORT_FEED)) { return 'https://api.nuget.org/v3/index.json' }
    $path = [IO.Path]::GetFullPath($env:HIPSHARP_SUPPORT_FEED)
    if (-not (Test-Path -LiteralPath $path -PathType Container)) { throw "HIPSHARP_SUPPORT_FEED must be an existing local package directory." }
    return $path
}

Export-ModuleMember -Function Get-HipSharpOutputPath, Get-HipSharpAssemblyPath, Get-HipSharpSupportFeed
