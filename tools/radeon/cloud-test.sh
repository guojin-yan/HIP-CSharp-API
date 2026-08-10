#!/usr/bin/env bash
set -euo pipefail

expected_commit="${1:?usage: cloud-test.sh EXPECTED_COMMIT}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
evidence_dir="${repository_root}/artifacts/radeon-cloud"
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
if [[ -d /persistent ]]; then
  export NUGET_PACKAGES=/persistent/hipsharp/nuget/packages
else
  export NUGET_PACKAGES=/workspace/.nuget/packages
fi
echo "NuGet cache: ${NUGET_PACKAGES}"

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

mkdir -p "${evidence_dir}"
mkdir -p "${NUGET_PACKAGES}"
cd "${repository_root}"
bash ./tools/radeon/env-report.sh | tee "${evidence_dir}/environment.txt"
bash ./eng/build.sh Release 0.0.0 | tee "${evidence_dir}/managed-gate.txt"

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
  --manifest ./eng/interop/interop-manifest.json \
  --output "${evidence_dir}/runtime-symbol-evidence.json"
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hiprtc_library}" \
  --library-name hiprtc \
  --manifest ./eng/interop/interop-manifest.json \
  --output "${evidence_dir}/hiprtc-symbol-evidence.json"
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
  --output "${evidence_dir}/m3-abi-evidence.json"

python3 - <<'PY'
import json
from pathlib import Path
schema = json.loads(Path("native/abi-probe/abi-evidence.schema.json").read_text())
evidence = json.loads(Path("artifacts/radeon-cloud/m3-abi-evidence.json").read_text())
required = schema["required"]
missing = [key for key in required if key not in evidence]
if missing:
    raise SystemExit("ABI evidence is missing: " + ", ".join(missing))
print("M3 ABI evidence schema fields present")
PY

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

echo "Radeon Cloud M3 validation passed for ${actual_commit}."
