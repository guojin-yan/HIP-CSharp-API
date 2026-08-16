[CmdletBinding()]
param(
    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot "../..")).Path,
    [string]$OutputJsonl = (Join-Path $PSScriptRoot "interface-coverage.jsonl"),
    [string]$OutputMarkdown = (Join-Path $PSScriptRoot "interface-coverage.md")
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$record = "Radeon_Cloud/records/20260814-1345-2a89c67-m8.9-assembly-identity-forward-fix"
$historicalSha = "63f33cf2061b6b7ed4b1865e2266bed0a1d707c8"
$model = Get-Content -Raw -Encoding UTF8 (Join-Path $RepositoryRoot "eng/interop/complete-api-model.json") | ConvertFrom-Json
$manifest = Get-Content -Raw -Encoding UTF8 (Join-Path $RepositoryRoot "eng/interop/interop-manifest.json") | ConvertFrom-Json
$review = Get-Content -Raw -Encoding UTF8 (Join-Path $PSScriptRoot "reviewed-classification.json") | ConvertFrom-Json

$workloads = @(
    [ordered]@{ Name = "device-info"; Entries = @("hipInit", "hipRuntimeGetVersion", "hipDriverGetVersion", "hipGetDeviceCount", "hipGetDevice", "hipSetDevice", "hipDeviceGetName", "hipDeviceGetAttribute"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipRuntimeTests.cs"; Cloud = "official-host-device-info"; Topic = "device discovery and diagnostics" },
    [ordered]@{ Name = "m8.2-pitched-memory"; Entries = @("hipMemGetInfo", "hipMallocPitch", "hipMalloc3D", "hipMemset", "hipMemsetAsync", "hipMemset2D", "hipMemset2DAsync", "hipMemset3D", "hipMemset3DAsync", "hipMemcpy2D", "hipMemcpy2DAsync", "hipMemcpy3D", "hipMemcpy3DAsync"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipPitchedMemoryTests.cs"; Cloud = "m8.2-pitched-memory"; Topic = "pitched memory and copy ownership" },
    [ordered]@{ Name = "m8.3-memory-pool"; Entries = @("hipDeviceGetDefaultMemPool", "hipDeviceGetMemPool", "hipDeviceSetMemPool", "hipMemPoolCreate", "hipMemPoolDestroy", "hipMemPoolTrimTo", "hipMemPoolGetAttribute", "hipMemPoolSetAttribute", "hipMemPoolSetAccess", "hipMemPoolGetAccess", "hipMallocFromPoolAsync"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipMemoryPoolTests.cs"; Cloud = "m8.3-memory-pool"; Topic = "pool ownership and stream ordering" },
    [ordered]@{ Name = "m8.4-explicit-graph"; Entries = @("hipStreamBeginCapture", "hipStreamEndCapture", "hipGraphCreate", "hipGraphAddEmptyNode", "hipGraphAddDependencies", "hipGraphRemoveDependencies", "hipGraphAddKernelNode", "hipGraphExecKernelNodeSetParams", "hipGraphAddMemcpyNode1D", "hipGraphExecMemcpyNodeSetParams1D", "hipGraphAddMemsetNode", "hipGraphExecMemsetNodeSetParams", "hipGraphAddMemAllocNode", "hipGraphAddMemFreeNode", "hipGraphUpload", "hipGraphDestroyNode", "hipGraphDestroy", "hipGraphInstantiateWithFlags", "hipGraphLaunch", "hipGraphExecDestroy"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipExplicitGraphTests.cs"; Cloud = "m8.4-explicit-graph"; Topic = "graph node ownership and dependency order" },
    [ordered]@{ Name = "stream-event"; Entries = @("hipStreamCreateWithFlags", "hipStreamDestroy", "hipStreamSynchronize", "hipStreamQuery", "hipEventCreateWithFlags", "hipEventDestroy", "hipEventRecord", "hipEventSynchronize", "hipEventQuery", "hipEventElapsedTime"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipStreamEventMemoryTests.cs"; Cloud = "stream-event-vector-add"; Topic = "stream and event lifecycle" },
    [ordered]@{ Name = "memory-copy"; Entries = @("hipMalloc", "hipFree", "hipMemcpy", "hipMemcpyAsync", "hipHostMalloc", "hipHostFree", "hipDeviceSynchronize"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipStreamEventMemoryTests.cs"; Cloud = "memory-copy"; Topic = "basic allocation, copy, and synchronization" },
    [ordered]@{ Name = "advanced-features"; Entries = @("hipMallocManaged", "hipMemPrefetchAsync", "hipMemAdvise", "hipMallocAsync", "hipFreeAsync", "hipDeviceCanAccessPeer", "hipDeviceEnablePeerAccess", "hipDeviceDisablePeerAccess", "hipMemcpyPeerAsync"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipAdvancedApiTests.cs"; Cloud = "advanced-features"; Topic = "managed memory and peer capability" },
    [ordered]@{ Name = "hiprtc-vector-add"; Entries = @("hipModuleLoadData", "hipModuleUnload", "hipModuleGetFunction", "hipModuleLaunchKernel", "hiprtcVersion", "hiprtcGetErrorString", "hiprtcCreateProgram", "hiprtcDestroyProgram", "hiprtcCompileProgram", "hiprtcGetProgramLogSize", "hiprtcGetProgramLog", "hiprtcGetCodeSize", "hiprtcGetCode"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipRtcTests.cs"; Cloud = "hiprtc-vector-add-and-negative-compile"; Topic = "HIPRTC code-object and module lifetime" },
    [ordered]@{ Name = "m8.5-kernel-occupancy"; Entries = @("hipFuncGetAttribute", "hipModuleOccupancyMaxActiveBlocksPerMultiprocessor", "hipModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags", "hipModuleOccupancyMaxPotentialBlockSize", "hipModuleOccupancyMaxPotentialBlockSizeWithFlags", "hipModuleLaunchCooperativeKernel"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipKernelOccupancyTests.cs"; Cloud = "m8.5-kernel-occupancy"; Topic = "kernel metadata and cooperative launch" },
    [ordered]@{ Name = "module-global"; Entries = @("hipModuleGetGlobal"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipModuleGlobalTests.cs"; Cloud = "m8.6-module-globals"; Topic = "borrowed module-global views" },
    [ordered]@{ Name = "errors"; Entries = @("hipGetErrorName", "hipGetErrorString"); Unit = "tests/JYPPX.ROCm.HipSharp.UnitTests/HipRuntimeTests.cs"; Cloud = "negative-compile-and-error-diagnostics"; Topic = "error identity and diagnostic ownership" }
)

function Get-Workload([string]$EntryPoint) {
    foreach ($workload in $workloads) {
        if ($workload.Entries -contains $EntryPoint) { return $workload }
    }
    return $null
}

function Get-Rule([string]$EntryPoint, [string]$Library) {
    foreach ($rule in $review.rules) {
        if (($rule.PSObject.Properties.Name -contains "library") -and $rule.library -ne $Library) { continue }
        if (($rule.PSObject.Properties.Name -contains "containsAny") -and $rule.containsAny.Count -gt 0) {
            $matched = $false
            foreach ($needle in $rule.containsAny) {
                if ($EntryPoint.Contains($needle, [StringComparison]::Ordinal)) { $matched = $true; break }
            }
            if (-not $matched) { continue }
        }
        return $rule
    }
    throw "No reviewed classification rule matched $EntryPoint"
}

$managedByEntry = @{}
foreach ($item in $manifest.functions) { $managedByEntry[$item.entryPoint] = $item }
$functions = @($model.runtimeFunctions) + @($model.rtcFunctions)
$completeEntrySet = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($function in $functions) { [void]$completeEntrySet.Add([string]$function.entryPoint) }
foreach ($managedEntryPoint in $managedByEntry.Keys) {
    if (-not $completeEntrySet.Contains([string]$managedEntryPoint)) { throw "Managed manifest entry is absent from complete model: $managedEntryPoint" }
}
$entries = [System.Collections.Generic.List[object]]::new()

foreach ($function in $functions) {
    $entryPoint = [string]$function.entryPoint
    $library = [string]$function.library
    $managed = $managedByEntry[$entryPoint]
    $workload = Get-Workload $entryPoint
    if ($null -ne $managed) {
        if ($null -eq $workload) { throw "Managed entry has no workload mapping: $entryPoint" }
        $disposition = [ordered]@{ status = "managed"; manifestName = $managed.managedName; reason = "present in the reviewed 100-entry managed owner manifest" }
        $unit = [ordered]@{ status = "covered"; test = $workload.Unit; workload = $workload.Name }
        $cloud = [ordered]@{ status = "passed-historical"; workload = $workload.Cloud; record = "$record/validation-summary.json"; exactSha = $historicalSha; scope = "historical exact 0.x bytes; not current SHA" }
        $negativeCovered = @("hiprtc-vector-add", "errors", "m8.5-kernel-occupancy", "module-global", "m8.4-explicit-graph", "m8.3-memory-pool", "m8.2-pitched-memory", "stream-event") -contains $workload.Name
        if ($negativeCovered) { $negative = [ordered]@{ status = "covered"; test = $workload.Unit; workload = $workload.Name } } else { $negative = [ordered]@{ status = "not-tested" } }
        if (@("hipDeviceCanAccessPeer", "hipDeviceEnablePeerAccess", "hipDeviceDisablePeerAccess", "hipMemcpyPeerAsync") -contains $entryPoint) { $capability = [ordered]@{ status = "skipped"; reason = "skipped(device-count<2)" } } else { $capability = [ordered]@{ status = "available" } }
        $articleTopic = $workload.Topic
        $abiReturnType = $managed.returnType
        $abiParameterCount = @($managed.parameters).Count
        $binding = [ordered]@{ status = "generated-low-level+managed-owner"; managedName = $managed.managedName }
        $minimumVersion = $managed.minimumHipVersion
    } else {
        $rule = Get-Rule $entryPoint $library
        $disposition = [ordered]@{ status = $rule.disposition; reviewRule = $rule.name; reason = $rule.reason }
        $unit = [ordered]@{ status = "not-tested" }
        $cloud = [ordered]@{ status = "not-tested"; reason = "symbol evidence does not prove function semantics" }
        $negative = [ordered]@{ status = "not-tested" }
        $capabilityStatus = if ($rule.disposition -eq "deferred-capability") { "deferred" } else { "available" }
        $capability = [ordered]@{ status = $capabilityStatus; reason = $rule.reason }
        $articleTopic = "reviewed raw-only: $($rule.name)"
        $abiReturnType = $function.nativeReturnType
        $abiParameterCount = @($function.parameters).Count
        $binding = [ordered]@{ status = "generated-low-level"; managedName = $function.managedName }
        $minimumVersion = "header-model"
    }

    if ($entryPoint -eq "hipExternalMemoryGetMappedMipmappedArray") {
        $export = [ordered]@{ status = "missing-reviewed"; scope = "historical-linux-symbol-scan"; reason = "single reviewed Linux export exception"; record = "$record/validation-summary.json" }
    } else {
        $export = [ordered]@{ status = "found-historical"; scope = "historical-linux-symbol-scan"; record = "$record/validation-summary.json" }
    }
    $header = if ($library -eq "hiprtc") { "hip/hiprtc.h" } else { "hip/hip_runtime_api.h" }
    $abi = [ordered]@{ status = "declared"; header = $header; callingConvention = "cdecl"; returnType = $abiReturnType; parameterCount = $abiParameterCount; minimumHipVersion = $minimumVersion }
    $entries.Add([ordered]@{
        library = $library
        entryPoint = $entryPoint
        binding = $binding
        cloudExport = $export
        abi = $abi
        managedDisposition = $disposition
        unitCoverage = $unit
        cloudFunctionCoverage = $cloud
        negativeCoverage = $negative
        capabilitySkip = $capability
        evidenceRecord = [ordered]@{ record = "$record/validation-summary.json"; exactSha = $historicalSha; currentSha = "not-generated" }
        articleTopic = $articleTopic
    })
}

$entries.Sort([System.Comparison[object]]{
    param($left, $right)
    $libraryComparison = [StringComparer]::Ordinal.Compare([string]$left["library"], [string]$right["library"])
    if ($libraryComparison -ne 0) { return $libraryComparison }
    return [StringComparer]::Ordinal.Compare([string]$left["entryPoint"], [string]$right["entryPoint"])
})
$entries = @($entries)
if ($entries.Count -ne 477) { throw "Expected 477 entries, found $($entries.Count)" }
if ((@($entries.entryPoint | Sort-Object -Unique)).Count -ne 477) { throw "Interface ledger contains duplicate entry points" }
foreach ($entry in $entries) {
    foreach ($field in @("library", "entryPoint", "binding", "cloudExport", "abi", "managedDisposition", "unitCoverage", "cloudFunctionCoverage", "negativeCoverage", "capabilitySkip", "evidenceRecord", "articleTopic")) {
        if ($null -eq $entry[$field]) { throw "Required ledger field is missing: $($entry.entryPoint)/$field" }
    }
}

$jsonLines = [System.Collections.Generic.List[string]]::new()
foreach ($entry in $entries) { $jsonLines.Add(($entry | ConvertTo-Json -Compress -Depth 12)) }
$jsonOutputPath = [System.IO.Path]::GetFullPath($OutputJsonl)
$markdownOutputPath = [System.IO.Path]::GetFullPath($OutputMarkdown)
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($jsonOutputPath)) | Out-Null
[System.IO.Directory]::CreateDirectory([System.IO.Path]::GetDirectoryName($markdownOutputPath)) | Out-Null
[System.IO.File]::WriteAllText($jsonOutputPath, ($jsonLines -join "`n") + "`n", [System.Text.UTF8Encoding]::new($false))

$counts = @{}
foreach ($entry in $entries) { $status = $entry.managedDisposition.status; if (-not $counts.ContainsKey($status)) { $counts[$status] = 0 }; $counts[$status]++ }
$cloudCounts = @{}
foreach ($entry in $entries) { $status = $entry.cloudFunctionCoverage.status; if (-not $cloudCounts.ContainsKey($status)) { $cloudCounts[$status] = 0 }; $cloudCounts[$status]++ }
$markdown = [System.Collections.Generic.List[string]]::new()
$markdown.AddRange([string[]]@(
    "# Interface coverage ledger review", "",
    "Generated from ``eng/interop/complete-api-model.json``, ``eng/interop/interop-manifest.json``, and ``reviewed-classification.json``.",
    "The ledger records evidence boundaries; a symbol export is not a function-level pass. Historical cloud evidence is bound to exact 0.x bytes and is not evidence for the current SHA.", "",
    "## Inventory", "",
    "- Total entries: $($entries.Count) (Runtime $(($entries | Where-Object library -eq 'amdhip64').Count), HIPRTC $(($entries | Where-Object library -eq 'hiprtc').Count)).",
    "- Complete model: $(@($model.runtimeFunctions).Count + @($model.rtcFunctions).Count); managed owner manifest: $(@($manifest.functions).Count).",
    "- Disposition: managed $($counts['managed']), managed-next $($counts['managed-next']), raw-only-reviewed $($counts['raw-only-reviewed']), deferred-capability $($counts['deferred-capability']).",
    "- Cloud function evidence: historical pass $($cloudCounts['passed-historical']), not-tested $($cloudCounts['not-tested']); export scan is tracked separately.", "",
    "## Managed workload mapping", "", "| Workload | Purpose | Unit source | Historical cloud scope |", "| --- | --- | --- | --- |"
))
foreach ($workload in $workloads) {
    $mapped = $workload["Entries"].Count
    $markdown.Add("| ``$($workload["Name"])`` ($mapped) | $($workload["Topic"]) | ``$($workload["Unit"])`` | ``$($workload["Cloud"])`` in ``$record`` |")
}
$markdown.AddRange([string[]]@(
    "", "## Review boundaries", "",
    "- ``managed-next`` is a planned ownership batch, not an implementation or test result.",
    "- ``raw-only-reviewed`` retains the generated low-level declaration because no current managed contract is justified.",
    "- ``deferred-capability`` requires a capability-specific cloud workload before promotion.",
    "- All missing unit, function, or negative evidence is represented as ``not-tested``; no status is inferred from an entry-point name.",
    "- Current state: ``implemented-local / cloud-validation-open``; ``publishable=false``; ``releaseAuthorized=false``.", ""
))
[System.IO.File]::WriteAllText($markdownOutputPath, ($markdown -join "`n"), [System.Text.UTF8Encoding]::new($false))
Write-Output "Generated $($entries.Count) interface ledger entries."
