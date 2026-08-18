#!/usr/bin/env bash
set -euo pipefail

expected_commit="${1:?usage: cloud-stress.sh EXPECTED_COMMIT}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
actual_commit="$(git -C "${repository_root}" rev-parse HEAD)"

if [[ ! "${expected_commit}" =~ ^[0-9a-f]{40}$ ]]; then
  echo "EXPECTED_COMMIT must be a lowercase 40-character Git SHA." >&2
  exit 1
fi
if [[ "${actual_commit}" != "${expected_commit}" ]]; then
  echo "Expected commit ${expected_commit}, found ${actual_commit}." >&2
  exit 1
fi
if git -C "${repository_root}" symbolic-ref -q HEAD >/dev/null; then
  echo "Radeon Cloud stress validation requires a detached checkout." >&2
  exit 1
fi
if [[ -n "$(git -C "${repository_root}" status --porcelain)" ]]; then
  echo "Radeon Cloud stress validation requires a clean detached checkout." >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  if [[ -x /workspace/.dotnet/dotnet ]]; then
    export DOTNET_ROOT=/workspace/.dotnet
    export PATH="${DOTNET_ROOT}:${PATH}"
  else
    echo ".NET SDK is unavailable. Run: bash ./tools/radeon/bootstrap.sh" >&2
    exit 1
  fi
fi

stress_rounds="${HIPSHARP_STRESS_ROUNDS:-10}"
stress_streams="${HIPSHARP_STRESS_STREAMS:-4}"
stress_length="${HIPSHARP_STRESS_LENGTH:-4194304}"
stress_lifecycles="${HIPSHARP_STRESS_LIFECYCLES:-250}"
for value_name in stress_rounds stress_streams stress_length stress_lifecycles; do
  value="${!value_name}"
  if [[ ! "${value}" =~ ^[0-9]+$ ]]; then
    echo "${value_name} must be an unsigned integer." >&2
    exit 1
  fi
done
if (( stress_rounds < 1 || stress_rounds > 100 )); then
  echo "HIPSHARP_STRESS_ROUNDS must be between 1 and 100." >&2
  exit 1
fi
if (( stress_streams < 2 || stress_streams > 8 )); then
  echo "HIPSHARP_STRESS_STREAMS must be between 2 and 8." >&2
  exit 1
fi
if (( stress_length < 1048576 || stress_length > 16777216 )); then
  echo "HIPSHARP_STRESS_LENGTH must be between 1048576 and 16777216 floats." >&2
  exit 1
fi
if (( stress_lifecycles < 100 || stress_lifecycles > 10000 )); then
  echo "HIPSHARP_STRESS_LIFECYCLES must be between 100 and 10000." >&2
  exit 1
fi

gpu_architecture="${HIP_ARCH:-}"
if [[ -z "${gpu_architecture}" ]]; then
  gpu_architecture="$(rocminfo | grep -Eo 'gfx[0-9]+' | sed -n '1p')"
fi
if [[ ! "${gpu_architecture}" =~ ^gfx[0-9]+$ ]]; then
  echo "Unable to determine a valid GPU architecture." >&2
  exit 1
fi

if [[ -n "${HIPSHARP_CLOUD_EVIDENCE_DIR:-}" ]]; then
  evidence_dir="${HIPSHARP_CLOUD_EVIDENCE_DIR}"
else
  run_stamp="$(date -u +%Y%m%dT%H%M%SZ)"
  evidence_dir="${repository_root}/artifacts/radeon-cloud/${actual_commit}/${run_stamp}"
fi
mkdir -p "${evidence_dir}"
export HIPSHARP_CLOUD_EVIDENCE_DIR="${evidence_dir}"
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
export UseSharedCompilation=false

sample_assembly="${repository_root}/samples/validation/AdvancedReliabilityStress/bin/Release/net10.0/AdvancedReliabilityStress.dll"
if [[ ! -f "${sample_assembly}" ]]; then
  echo "The Release sample is not built. Run cloud-test.sh or eng/build.sh first." >&2
  exit 1
fi

dotnet run --project "${repository_root}/samples/validation/AdvancedReliabilityStress/AdvancedReliabilityStress.csproj" \
  --configuration Release --no-build --no-restore \
  -p:UseSharedCompilation=false -- \
  --arch "${gpu_architecture}" \
  --graph-launch-repeats 3 \
  --lifecycle-repeats "${stress_lifecycles}" \
  --stress-rounds "${stress_rounds}" \
  --stress-streams "${stress_streams}" \
  --stress-length "${stress_length}" 2>&1 \
  | tee "${evidence_dir}/advanced-features-stress.txt"

export HIPSHARP_STRESS_COMMIT="${actual_commit}"
export HIPSHARP_STRESS_ARCH="${gpu_architecture}"
export HIPSHARP_STRESS_ROUNDS_VALUE="${stress_rounds}"
export HIPSHARP_STRESS_STREAMS_VALUE="${stress_streams}"
export HIPSHARP_STRESS_LENGTH_VALUE="${stress_length}"
export HIPSHARP_STRESS_LIFECYCLES_VALUE="${stress_lifecycles}"
python3 - <<'PY'
import json
import os
from pathlib import Path

length = int(os.environ["HIPSHARP_STRESS_LENGTH_VALUE"])
streams = int(os.environ["HIPSHARP_STRESS_STREAMS_VALUE"])
summary = {
    "schemaVersion": 1,
    "gitCommit": os.environ["HIPSHARP_STRESS_COMMIT"],
    "gpuArchitecture": os.environ["HIPSHARP_STRESS_ARCH"],
    "rounds": int(os.environ["HIPSHARP_STRESS_ROUNDS_VALUE"]),
    "streams": streams,
    "vectorLength": length,
    "bytesPerBuffer": length * 4,
    "maximumInFlightDeviceBytes": length * 4 * 3 * streams,
    "lifecycleRepeats": int(os.environ["HIPSHARP_STRESS_LIFECYCLES_VALUE"]),
    "cpuGpuCompared": True,
    "performanceClaim": False,
    "result": "passed",
}
target = Path(os.environ["HIPSHARP_CLOUD_EVIDENCE_DIR"]) / "cloud-stress-summary.json"
target.write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
PY

echo "Radeon Cloud stress validation passed for ${actual_commit}."
echo "Evidence directory: ${evidence_dir}"
