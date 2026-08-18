#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../../.." && pwd)"
project_path="${script_dir}/VisualInspection.csproj"
runtime_graph_path="${script_dir}/runtime-distro-rid-graph.json"
runtime_identifier="ubuntu.24.04-x64"
target_framework="${HIPSHARP_VISUAL_TFM:-}"

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
if (( sdk_major < 8 )); then
  # shellcheck source=../../../tools/radeon/bootstrap.sh
  source "${repository_root}/tools/radeon/bootstrap.sh"
fi

if [[ -z "${target_framework}" ]]; then
  if dotnet --list-runtimes | grep -q '^Microsoft.NETCore.App 10\.'; then
    target_framework="net10.0"
  elif dotnet --list-runtimes | grep -q '^Microsoft.NETCore.App 8\.'; then
    target_framework="net8.0"
  else
    echo "A .NET 8 or .NET 10 runtime is required to run VisualInspection." >&2
    exit 1
  fi
fi

gpu_architecture="${HIPSHARP_GPU_ARCH:-}"
if [[ -z "${gpu_architecture}" ]]; then
  gpu_architecture="$(rocminfo | grep -Eo 'gfx[0-9]+' | sed -n '1p')"
fi
if [[ ! "${gpu_architecture}" =~ ^gfx[0-9]+$ ]]; then
  echo "Unable to determine a gfxNNNN target from rocminfo; set HIPSHARP_GPU_ARCH explicitly." >&2
  exit 1
fi

run_id="$(date -u +%Y%m%d-%H%M%S)"
output_directory="${HIPSHARP_VISUAL_OUTPUT:-${repository_root}/artifacts/visual-inspection/${run_id}}"

if [[ -d /persistent ]]; then
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/persistent/projects/hip-csharp-api/cache/nuget/visual-inspection}"
else
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages/visual-inspection}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

echo "VisualInspection target: ${gpu_architecture}; OpenCV runtime: ${runtime_identifier}; .NET: ${target_framework}"
(
  # Keep the distro RID graph global so the HIPSharp project reference sees it as well.
  cd "$(dirname -- "${repository_root}")"
  dotnet restore "${project_path}" --locked-mode \
    -p:RuntimeIdentifierGraphPath="${runtime_graph_path}"
  dotnet build "${project_path}" --configuration Release --no-restore \
    -p:RuntimeIdentifierGraphPath="${runtime_graph_path}"
  dotnet run --project "${project_path}" --configuration Release --no-build --no-restore \
    -p:RuntimeIdentifierGraphPath="${runtime_graph_path}" \
    --framework "${target_framework}" -- \
    --arch "${gpu_architecture}" \
    --output "${output_directory}" \
    "$@"
)

echo "VisualInspection artifacts: ${output_directory}"
