[CmdletBinding()]
param([Parameter(Mandatory = $true)][string]$PackagePath)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$package = (Resolve-Path -LiteralPath $PackagePath).Path
if ([System.IO.Path]::GetExtension($package) -cne ".nupkg") {
    throw "HIPSHARP1001: Deterministic package normalization accepts only .nupkg files."
}

Add-Type -AssemblyName System.IO.Compression.FileSystem
$corePropertiesPattern = '^package/services/metadata/core-properties/[^/]+\.psmdcp$'
$fixedCorePropertiesPath = 'package/services/metadata/core-properties/package.psmdcp'
$fixedTimestamp = [System.DateTimeOffset]::new(2000, 1, 1, 0, 0, 0, [System.TimeSpan]::Zero)
$temporary = "$package.normalized"
if ([System.IO.File]::Exists($temporary)) { [System.IO.File]::Delete($temporary) }

$source = [System.IO.Compression.ZipFile]::OpenRead($package)
try {
    $sourceEntries = @($source.Entries | Where-Object { -not $_.FullName.EndsWith('/') })
    $coreProperties = @($sourceEntries | Where-Object { $_.FullName.Replace('\', '/') -match $corePropertiesPattern })
    $nuspecs = @($sourceEntries | Where-Object { $_.FullName.Replace('\', '/') -match '^[^/]+\.nuspec$' })
    $relationships = @($sourceEntries | Where-Object { $_.FullName.Replace('\', '/') -eq '_rels/.rels' })
    if ($coreProperties.Count -ne 1 -or $nuspecs.Count -ne 1 -or $relationships.Count -ne 1) {
        throw "HIPSHARP1001: NuGet package must contain exactly one nuspec, core-properties part, and root relationships part."
    }

    $relationshipReader = [System.IO.StreamReader]::new($relationships[0].Open())
    try { [xml]$relationshipDocument = $relationshipReader.ReadToEnd() }
    finally { $relationshipReader.Dispose() }
    $relationshipNodes = @($relationshipDocument.DocumentElement.ChildNodes | Where-Object { $_.LocalName -eq 'Relationship' })
    $manifestRelationships = @($relationshipNodes | Where-Object { $_.Type -eq 'http://schemas.microsoft.com/packaging/2010/07/manifest' })
    $metadataRelationships = @($relationshipNodes | Where-Object { $_.Type -eq 'http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties' })
    if ($relationshipNodes.Count -ne 2 -or $manifestRelationships.Count -ne 1 -or $metadataRelationships.Count -ne 1) {
        throw "HIPSHARP1001: Root NuGet relationships are outside the deterministic two-part contract."
    }

    $nuspecName = $nuspecs[0].FullName.Replace('\', '/')
    $corePropertiesName = $coreProperties[0].FullName.Replace('\', '/')
    if ([string]$manifestRelationships[0].Target -ne "/$nuspecName" -or
        [string]$metadataRelationships[0].Target -ne "/$corePropertiesName") {
        throw "HIPSHARP1001: Root NuGet relationships do not bind the package's actual nuspec and core-properties parts."
    }
    $escapedNuspecName = [System.Security.SecurityElement]::Escape($nuspecName)
    $relationshipsText = @(
        '<?xml version="1.0" encoding="utf-8"?>',
        '<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">',
        "  <Relationship Type=`"http://schemas.microsoft.com/packaging/2010/07/manifest`" Target=`"/$escapedNuspecName`" Id=`"R1`" />",
        "  <Relationship Type=`"http://schemas.openxmlformats.org/package/2006/relationships/metadata/core-properties`" Target=`"/$fixedCorePropertiesPath`" Id=`"R2`" />",
        '</Relationships>',
        ''
    ) -join "`n"
    $relationshipsBytes = [System.Text.UTF8Encoding]::new($false).GetBytes($relationshipsText)

    $normalizedEntries = @($sourceEntries | ForEach-Object {
        $sourceName = $_.FullName.Replace('\', '/')
        [pscustomobject]@{
            Source = $_
            Name = if ($sourceName -match $corePropertiesPattern) { $fixedCorePropertiesPath } else { $sourceName }
        }
    } | Sort-Object -Property @{ Expression = 'Name'; Ascending = $true })
    if (@($normalizedEntries.Name | Group-Object | Where-Object Count -ne 1).Count -ne 0) {
        throw "HIPSHARP1001: Deterministic package normalization produced duplicate paths."
    }

    $destination = [System.IO.Compression.ZipFile]::Open($temporary, [System.IO.Compression.ZipArchiveMode]::Create)
    try {
        foreach ($item in $normalizedEntries) {
            $destinationEntry = $destination.CreateEntry($item.Name, [System.IO.Compression.CompressionLevel]::Optimal)
            $destinationEntry.LastWriteTime = $fixedTimestamp
            $destinationStream = $destinationEntry.Open()
            try {
                if ($item.Name -eq '_rels/.rels') {
                    $destinationStream.Write($relationshipsBytes, 0, $relationshipsBytes.Length)
                } else {
                    $sourceStream = $item.Source.Open()
                    try { $sourceStream.CopyTo($destinationStream) }
                    finally { $sourceStream.Dispose() }
                }
            } finally { $destinationStream.Dispose() }
        }
    } finally { $destination.Dispose() }
} catch {
    if ([System.IO.File]::Exists($temporary)) { [System.IO.File]::Delete($temporary) }
    throw
} finally { $source.Dispose() }

[System.IO.File]::Delete($package)
[System.IO.File]::Move($temporary, $package)
Write-Host "Deterministic nupkg normalization passed: $package"
