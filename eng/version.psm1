Set-StrictMode -Version Latest

function Get-HipSharpVersion {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet("Core", "Ubuntu2404Runtime", "WindowsRuntime")]
        [string]$Kind,
        [string]$Override,
        [string]$RepositoryRoot = (Split-Path -Parent $PSScriptRoot)
    )

    [xml]$versions = Get-Content -Raw -LiteralPath (Join-Path $RepositoryRoot "eng/Versions.props")
    $propertyName = "HipSharp${Kind}Version"
    $value = [string]$versions.Project.PropertyGroup.$propertyName
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "The central version property '$propertyName' is missing."
    }
    if ($value -notmatch '^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$') {
        throw "The central version property '$propertyName' must be a SemVer 2.0-compatible value."
    }
    if (-not [string]::IsNullOrWhiteSpace($Override) -and $Override -ne $value) {
        throw "Requested $Kind version '$Override' does not match the central version '$value'."
    }
    return $value
}

Export-ModuleMember -Function Get-HipSharpVersion
