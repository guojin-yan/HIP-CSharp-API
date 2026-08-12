#!/usr/bin/env bash
set -euo pipefail

expected_commit="${1:?usage: cloud-test.sh EXPECTED_COMMIT}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
actual_commit="$(git -C "${repository_root}" rev-parse HEAD)"

if ! command -v dotnet >/dev/null 2>&1; then
  if [[ -x /workspace/.dotnet/dotnet ]]; then
    export DOTNET_ROOT=/workspace/.dotnet
    export PATH="${DOTNET_ROOT}:${PATH}"
  else
    echo ".NET SDK is unavailable. Run: bash ./tools/radeon/bootstrap.sh" >&2
    exit 1
  fi
fi

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
export UseSharedCompilation=false
if [[ -d /persistent ]]; then
  export NUGET_PACKAGES=/persistent/hipsharp/nuget/packages
else
  export NUGET_PACKAGES=/workspace/.nuget/packages
fi
echo "NuGet cache: ${NUGET_PACKAGES}"

if [[ ! "${expected_commit}" =~ ^[0-9a-f]{40}$ ]]; then
  echo "EXPECTED_COMMIT must be a lowercase 40-character Git SHA." >&2
  exit 1
fi
if [[ "${actual_commit}" != "${expected_commit}" ]]; then
  echo "Expected commit ${expected_commit}, found ${actual_commit}." >&2
  exit 1
fi
if git -C "${repository_root}" symbolic-ref -q HEAD >/dev/null; then
  echo "Radeon Cloud validation requires a detached checkout." >&2
  exit 1
fi
if [[ -n "$(git -C "${repository_root}" status --porcelain)" ]]; then
  echo "Radeon Cloud validation requires a clean detached checkout." >&2
  exit 1
fi

run_stamp="$(date -u +%Y%m%dT%H%M%SZ)-$$"
evidence_dir="${repository_root}/artifacts/radeon-cloud/${actual_commit}/${run_stamp}"
mkdir -p "${evidence_dir}"
export HIPSHARP_CLOUD_EVIDENCE_DIR="${evidence_dir}"
mkdir -p "${NUGET_PACKAGES}"
echo "Evidence directory: ${evidence_dir}"
cd "${repository_root}"
bash ./tools/radeon/env-report.sh | tee "${evidence_dir}/environment.txt"
core_version="$(dotnet msbuild ./src/JYPPX.HipSharp/JYPPX.HipSharp.csproj -nologo -getProperty:HipSharpCoreVersion)"
core_version="${core_version//$'\r'/}"
bash ./eng/build.sh Release "${core_version}" | tee "${evidence_dir}/managed-gate.txt"
pwsh -NoProfile -File ./eng/generate-interop.ps1 extract-headers \
  -HeaderRoot /opt/rocm/include \
  -Check \
  | tee "${evidence_dir}/complete-header-coverage.txt"

hip_library="$(readlink -f /opt/rocm/lib/libamdhip64.so)"
hiprtc_candidate=""
for rocm_library_directory in /opt/rocm/lib /opt/rocm/lib64; do
  if [[ -e "${rocm_library_directory}/libhiprtc.so" ]]; then
    hiprtc_candidate="${rocm_library_directory}/libhiprtc.so"
    break
  fi
done
if [[ -z "${hiprtc_candidate}" ]]; then
  echo "Unable to locate libhiprtc.so under the ROCm installation." >&2
  exit 1
fi
hiprtc_library="$(readlink -f "${hiprtc_candidate}")"
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hip_library}" \
  --library-name amdhip64 \
  --require-optional \
  --manifest ./eng/interop/interop-manifest.json \
  --output "${evidence_dir}/runtime-symbol-evidence.json"
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hiprtc_library}" \
  --library-name hiprtc \
  --manifest ./eng/interop/interop-manifest.json \
  --output "${evidence_dir}/hiprtc-symbol-evidence.json"
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hip_library}" \
  --library-name amdhip64 \
  --manifest ./eng/interop/complete-api-model.json \
  --allow-missing hipExternalMemoryGetMappedMipmappedArray \
  --output "${evidence_dir}/complete-runtime-symbol-evidence.json"
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hiprtc_library}" \
  --library-name hiprtc \
  --manifest ./eng/interop/complete-api-model.json \
  --output "${evidence_dir}/complete-hiprtc-symbol-evidence.json"
normalized_manifest_hash="$(sha256sum ./eng/interop/normalized-model.json | awk '{print toupper($1)}')"
hipcc -std=c++14 ./native/abi-probe/hip_abi_probe.cpp \
  "-DHIPSHARP_NORMALIZED_MANIFEST_SHA256=\"${normalized_manifest_hash}\"" \
  -o "${evidence_dir}/hip-abi-probe"
"${evidence_dir}/hip-abi-probe" | tee "${evidence_dir}/abi-evidence.json"
python3 ./native/abi-probe/collect_evidence.py \
  --symbols "${evidence_dir}/runtime-symbol-evidence.json" \
  --symbols "${evidence_dir}/hiprtc-symbol-evidence.json" \
  --types "${evidence_dir}/abi-evidence.json" \
  --header /opt/rocm/include/hip/hip_runtime_api.h \
  --header /opt/rocm/include/hip/hiprtc.h \
  --output "${evidence_dir}/m6-abi-evidence.json"

python3 - <<'PY'
import hashlib
import json
import os
import subprocess
from pathlib import Path
schema = json.loads(Path("native/abi-probe/abi-evidence.schema.json").read_text())
evidence = json.loads((Path(os.environ["HIPSHARP_CLOUD_EVIDENCE_DIR"]) / "m6-abi-evidence.json").read_text())
required = schema["required"]
missing = [key for key in required if key not in evidence]
if missing:
    raise SystemExit("ABI evidence is missing: " + ", ".join(missing))
expected_commit = subprocess.check_output(["git", "rev-parse", "HEAD"], text=True).strip()
expected_manifest_hash = hashlib.sha256(Path("eng/interop/normalized-model.json").read_bytes()).hexdigest().upper()
if evidence["gitCommit"] != expected_commit:
    raise SystemExit("ABI evidence commit does not match the detached checkout")
if evidence["normalizedManifestHash"].upper() != expected_manifest_hash:
    raise SystemExit("ABI evidence normalized manifest hash does not match the checkout")
if evidence["schemaVersion"] != 7 or len(evidence.get("functions", [])) != 100:
    raise SystemExit("ABI evidence must use schema 7 and include all 100 manifest functions")
advanced = {
    "hipMallocManaged", "hipMemPrefetchAsync", "hipMemAdvise", "hipMallocAsync", "hipFreeAsync",
    "hipDeviceCanAccessPeer", "hipDeviceEnablePeerAccess", "hipDeviceDisablePeerAccess", "hipMemcpyPeerAsync",
    "hipStreamBeginCapture", "hipStreamEndCapture", "hipGraphDestroy", "hipGraphInstantiateWithFlags",
    "hipGraphLaunch", "hipGraphExecDestroy",
}
managed_memory = {
    "hipMemGetInfo", "hipMallocPitch", "hipMalloc3D", "hipMemset", "hipMemsetAsync",
    "hipMemset2D", "hipMemset2DAsync", "hipMemset3D", "hipMemset3DAsync",
    "hipMemcpy2D", "hipMemcpy2DAsync", "hipMemcpy3D", "hipMemcpy3DAsync",
}
memory_pool = {
    "hipDeviceGetDefaultMemPool", "hipDeviceGetMemPool", "hipDeviceSetMemPool",
    "hipMemPoolCreate", "hipMemPoolDestroy", "hipMemPoolTrimTo",
    "hipMemPoolGetAttribute", "hipMemPoolSetAttribute", "hipMemPoolSetAccess",
    "hipMemPoolGetAccess", "hipMallocFromPoolAsync",
}
explicit_graph = {
    "hipGraphCreate", "hipGraphAddEmptyNode", "hipGraphAddDependencies", "hipGraphRemoveDependencies",
    "hipGraphAddKernelNode", "hipGraphExecKernelNodeSetParams", "hipGraphAddMemcpyNode1D",
    "hipGraphExecMemcpyNodeSetParams1D", "hipGraphAddMemsetNode", "hipGraphExecMemsetNodeSetParams",
    "hipGraphAddMemAllocNode", "hipGraphAddMemFreeNode", "hipGraphUpload", "hipGraphDestroyNode",
}
managed_module_exports = {
    "hipFuncGetAttribute", "hipModuleOccupancyMaxActiveBlocksPerMultiprocessor",
    "hipModuleOccupancyMaxActiveBlocksPerMultiprocessorWithFlags",
    "hipModuleOccupancyMaxPotentialBlockSize", "hipModuleOccupancyMaxPotentialBlockSizeWithFlags",
    "hipModuleLaunchCooperativeKernel",
    "hipModuleGetGlobal",
}
found = {item["entryPoint"] for item in evidence["functions"] if item["found"]}
missing_advanced = sorted(advanced - found)
if missing_advanced:
    raise SystemExit("M6 advanced exports are missing: " + ", ".join(missing_advanced))
missing_memory = sorted(managed_memory - found)
if missing_memory:
    raise SystemExit("M8.2 managed memory exports are missing: " + ", ".join(missing_memory))
missing_pool = sorted(memory_pool - found)
if missing_pool:
    raise SystemExit("M8.3 memory pool exports are missing: " + ", ".join(missing_pool))
missing_graph = sorted(explicit_graph - found)
if missing_graph:
    raise SystemExit("M8.4 explicit graph exports are missing: " + ", ".join(missing_graph))
missing_managed_module = sorted(managed_module_exports - found)
if missing_managed_module:
    raise SystemExit("M8.5/M8.6 managed module exports are missing: " + ", ".join(missing_managed_module))
if len(evidence["headers"]) != 2 or any(len(item.get("sha256", "")) != 64 for item in evidence["headers"]):
    raise SystemExit("ABI evidence must include both official header hashes")
print("M8.6 ABI evidence schema and managed-owner exports passed")
PY

python3 - <<'PY'
import json
import os
from pathlib import Path

evidence_dir = Path(os.environ["HIPSHARP_CLOUD_EVIDENCE_DIR"])
runtime = json.loads((evidence_dir / "complete-runtime-symbol-evidence.json").read_text())
rtc = json.loads((evidence_dir / "complete-hiprtc-symbol-evidence.json").read_text())
expected_runtime_exception = ["hipExternalMemoryGetMappedMipmappedArray"]
if not runtime.get("completeModel") or runtime.get("expectedCount") != 459:
    raise SystemExit("Complete Runtime evidence must contain all 459 header declarations")
if runtime.get("foundCount") != 458 or runtime.get("allowedMissing") != expected_runtime_exception:
    raise SystemExit("Complete Runtime evidence must contain 458 exports and the one reviewed Linux exception")
missing_runtime = sorted(item["entryPoint"] for item in runtime["symbols"] if not item["found"])
if missing_runtime != expected_runtime_exception:
    raise SystemExit("Unexpected missing Runtime exports: " + ", ".join(missing_runtime))
if not rtc.get("completeModel") or rtc.get("expectedCount") != 18 or rtc.get("foundCount") != 18:
    raise SystemExit("Complete HIPRTC evidence must contain all 18 exports")
if any(not item["found"] for item in rtc["symbols"]):
    raise SystemExit("Complete HIPRTC evidence contains a missing export")
print("Complete HIP 7.2.1 symbol evidence passed: Runtime=458/459 (one reviewed Linux exception), HIPRTC=18/18")
PY

if ! command -v pwsh >/dev/null 2>&1; then
  echo "PowerShell is required for the release package audit (eng/verify-package.ps1)." >&2
  exit 1
fi
pwsh -NoProfile -File ./eng/verify-package.ps1 \
  -PackagePath "${repository_root}/artifacts/packages/JYPPX.HIP.CSharp.API.${core_version}.nupkg" \
  -ExpectedVersion "${core_version}" \
  -ExpectedRepositoryCommit "${actual_commit}" \
  | tee "${evidence_dir}/package-audit.txt"

dotnet run --project ./samples/DeviceInfo/DeviceInfo.csproj -c Release | tee "${evidence_dir}/device-info.txt"
dotnet run --project ./samples/MemoryCopy/MemoryCopy.csproj -c Release | tee "${evidence_dir}/memory-copy.txt"

gpu_architecture="$(rocminfo | grep -Eo 'gfx[0-9]+' | sed -n '1p')"
if [[ -z "${gpu_architecture}" ]]; then
  echo "Unable to determine the GPU architecture from rocminfo." >&2
  exit 1
fi

: > "${evidence_dir}/vector-add.txt"
for length in 1 127 256 1000 1048576; do
  dotnet run --project ./samples/HipRtcVectorAdd/HipRtcVectorAdd.csproj \
    -c Release --no-build -- \
    --arch "${gpu_architecture}" \
    --length "${length}" \
    --repeat 20 2>&1 | tee -a "${evidence_dir}/vector-add.txt"
done

dotnet run --project ./samples/HipRtcVectorAdd/HipRtcVectorAdd.csproj \
  -c Release --no-build -- \
  --arch "${gpu_architecture}" \
  --negative-compile 2>&1 | tee "${evidence_dir}/negative-compile.txt"

dotnet run --project ./samples/HipStreamEventVectorAdd/HipStreamEventVectorAdd.csproj \
  -c Release --no-build -- \
  --arch "${gpu_architecture}" \
  --lifecycle-repeats 100 2>&1 | tee "${evidence_dir}/stream-event-vector-add.txt"

dotnet run --project ./samples/HipAdvancedFeatures/HipAdvancedFeatures.csproj \
  -c Release --no-build -- \
  --arch "${gpu_architecture}" \
  --graph-launch-repeats 3 \
  --lifecycle-repeats 100 2>&1 | tee "${evidence_dir}/advanced-features.txt"

bash ./tools/radeon/cloud-stress.sh "${actual_commit}"

echo "Radeon Cloud complete API validation passed for ${actual_commit}."
echo "Evidence directory: ${evidence_dir}"
