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
python3 ./native/abi-probe/verify_symbols.py \
  --library "${hip_library}" \
  --manifest ./eng/interop/interop-manifest.json \
  --output "${evidence_dir}/symbol-evidence.json"
hipcc -std=c++14 ./native/abi-probe/hip_abi_probe.cpp -o "${evidence_dir}/hip-abi-probe"
"${evidence_dir}/hip-abi-probe" | tee "${evidence_dir}/abi-evidence.json"
python3 ./native/abi-probe/collect_evidence.py \
  --symbols "${evidence_dir}/symbol-evidence.json" \
  --types "${evidence_dir}/abi-evidence.json" \
  --header /opt/rocm/include/hip/hip_runtime_api.h \
  --output "${evidence_dir}/m1-abi-evidence.json"

dotnet run --project ./samples/DeviceInfo/DeviceInfo.csproj -c Release | tee "${evidence_dir}/device-info.txt"
dotnet run --project ./samples/MemoryCopy/MemoryCopy.csproj -c Release | tee "${evidence_dir}/memory-copy.txt"

echo "Radeon Cloud M1 validation passed for ${actual_commit}."
