#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../../.." && pwd)"
project_path="${script_dir}/HeatDiffusion.csproj"

if [[ ! -e /dev/kfd || ! -d /dev/dri ]]; then
  echo "Radeon GPU devices are unavailable; expected /dev/kfd and /dev/dri." >&2
  exit 1
fi

if ! command -v rocminfo >/dev/null 2>&1; then
  echo "rocminfo is required to identify the target GPU architecture." >&2
  exit 1
fi

sdk_major=0
if command -v dotnet >/dev/null 2>&1; then
  sdk_version="$(cd "$(dirname -- "${repository_root}")" && dotnet --version)"
  if [[ "${sdk_version}" =~ ^([0-9]+)\. ]]; then
    sdk_major="${BASH_REMATCH[1]}"
  fi
fi
if (( sdk_major < 10 )); then
  # shellcheck source=../../../tools/radeon/bootstrap.sh
  source "${repository_root}/tools/radeon/bootstrap.sh"
fi

gpu_architecture="${HIPSHARP_GPU_ARCH:-}"
if [[ -z "${gpu_architecture}" ]]; then
  gpu_architecture="$(rocminfo | grep -Eo 'gfx[0-9]+' | sed -n '1p')"
fi
if [[ ! "${gpu_architecture}" =~ ^gfx[0-9]+$ ]]; then
  echo "Unable to determine a gfxNNNN target from rocminfo; set HIPSHARP_GPU_ARCH explicitly." >&2
  exit 1
fi

profile="${HIPSHARP_HEAT_PROFILE:-quick}"
run_id="$(date -u +%Y%m%d-%H%M%S)"
output_directory="${HIPSHARP_HEAT_OUTPUT:-${repository_root}/artifacts/heat-diffusion/${run_id}}"

if [[ -d /persistent ]]; then
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/persistent/projects/hip-csharp-api/cache/nuget}"
else
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "HeatDiffusion target: ${gpu_architecture}; profile: ${profile}"
(
  # Run outside the repository root so a compatible cloud SDK is not rejected by the
  # development-only feature-band pin in global.json.
  cd "$(dirname -- "${repository_root}")"
  dotnet restore "${project_path}" --locked-mode
  dotnet build "${project_path}" --configuration Release --no-restore
  dotnet run --project "${project_path}" --configuration Release --no-build --no-restore -- \
    --arch "${gpu_architecture}" \
    --profile "${profile}" \
    --output "${output_directory}" \
    "$@"
)

echo "HeatDiffusion artifacts: ${output_directory}"
