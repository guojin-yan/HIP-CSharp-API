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

Export-ModuleMember -Function Get-HipSharpOutputPath, Get-HipSharpAssemblyPath
