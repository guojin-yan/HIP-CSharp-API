#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../.." && pwd)"
parent_dir="$(dirname -- "${repository_root}")"
gpu_architecture="${HIPSHARP_GPU_ARCH:-}"

if [[ ! -e /dev/kfd || ! -d /dev/dri ]]; then
  echo "Radeon GPU devices are unavailable; expected /dev/kfd and /dev/dri." >&2
  exit 1
fi

if ! command -v rocminfo >/dev/null 2>&1; then
  echo "rocminfo is required to identify the target GPU architecture." >&2
  exit 1
fi

if [[ -z "${gpu_architecture}" ]]; then
  gpu_architecture="$(rocminfo | grep -Eo 'gfx[0-9]+' | sed -n '1p')"
fi
if [[ ! "${gpu_architecture}" =~ ^gfx[0-9]+$ ]]; then
  echo "Unable to determine a gfxNNNN target; set HIPSHARP_GPU_ARCH explicitly." >&2
  exit 1
fi

run_id="$(date -u +%Y%m%d-%H%M%S)"
record_directory="${HIPSHARP_TUTORIAL_RECORD:-${repository_root}/artifacts/tutorial-verification/${run_id}}"
mkdir -p "${record_directory}/logs"

if [[ -d /persistent ]]; then
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/persistent/projects/hip-csharp-api/cache/nuget/tutorials}"
else
  export NUGET_PACKAGES="${NUGET_PACKAGES:-/workspace/.nuget/packages/tutorials}"
fi
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

{
  echo "# HIP-CSharp-API tutorial verification"
  echo
  echo "- UTC: $(date -u +%Y-%m-%dT%H:%M:%SZ)"
  echo "- Repository: ${repository_root}"
  echo "- Commit: $(cd "${repository_root}" && git rev-parse HEAD)"
  echo "- GPU architecture: ${gpu_architecture}"
  echo "- Kernel: $(uname -a)"
  echo "- .NET SDK: $(cd "${parent_dir}" && dotnet --version)"
  echo "- NuGet cache: ${NUGET_PACKAGES}"
  echo
  echo "## dotnet --info"
  (cd "${parent_dir}" && dotnet --info)
  echo
  echo "## rocminfo"
  rocminfo
  echo
  echo "## device nodes"
  ls -l /dev/kfd /dev/dri
} > "${record_directory}/environment.md" 2>&1

{
  cd "${parent_dir}"
  dotnet restore "${repository_root}/HipSharp.sln" --locked-mode
  while IFS= read -r project; do
    dotnet build "${project}" --configuration Release --no-restore
  done < <(find "${script_dir}" -mindepth 2 -name '*.csproj' -print | sort)
} > "${record_directory}/build.log" 2>&1

printf 'case,status,exit_code,log\n' > "${record_directory}/results.csv"
failed_count=0

run_case() {
  local name="$1"
  shift
  local log_file="${record_directory}/logs/${name}.log"
  set +e
  dotnet "$@" > "${log_file}" 2>&1
  local exit_code=$?
  set -e
  local status="failed"
  if grep -q '^Skipped:' "${log_file}"; then
    status="skipped"
  elif grep -q '^Usage:' "${log_file}"; then
    status="usage-only"
  elif [[ ${exit_code} -eq 0 ]]; then
    status="passed"
  else
    failed_count=$((failed_count + 1))
  fi
  printf '%s,%s,%s,%s\n' "${name}" "${status}" "${exit_code}" "logs/${name}.log" >> "${record_directory}/results.csv"
}

dll() {
  printf '%s/samples/tutorials/%s/bin/Release/net10.0/%s.dll' "${repository_root}" "$1" "$2"
}

run_case EnvironmentAndDevice "$(dll 01-RuntimeDevice/EnvironmentAndDevice EnvironmentAndDevice)"
run_case LoaderDiagnostics "$(dll 01-RuntimeDevice/LoaderDiagnostics LoaderDiagnostics)"
run_case LinearMemoryCopy "$(dll 02-Memory/LinearMemoryCopy LinearMemoryCopy)"
run_case PinnedHostMemory "$(dll 02-Memory/PinnedHostMemory PinnedHostMemory)"
run_case PitchedMemory2D3D "$(dll 02-Memory/PitchedMemory2D3D PitchedMemory2D3D)"
run_case ManagedMemory "$(dll 02-Memory/ManagedMemory ManagedMemory)"
run_case AsyncAllocationAndMemoryPool "$(dll 02-Memory/AsyncAllocationAndMemoryPool AsyncAllocationAndMemoryPool)"
run_case VirtualMemory "$(dll 02-Memory/VirtualMemory VirtualMemory)"
run_case StreamAndEvent "$(dll 03-Execution/StreamAndEvent StreamAndEvent)"
run_case AsyncVectorAdd "$(dll 03-Execution/AsyncVectorAdd AsyncVectorAdd)" --arch "${gpu_architecture}"
run_case HipRtcProgramLinker "$(dll 04-Kernel/HipRtcProgramLinker HipRtcProgramLinker)" "${gpu_architecture}"
run_case HipRtcVectorAdd "$(dll 04-Kernel/HipRtcVectorAdd HipRtcVectorAdd)" --arch "${gpu_architecture}" --length 1000 --repeat 20
run_case KernelOccupancy "$(dll 04-Kernel/KernelOccupancy KernelOccupancy)" "${gpu_architecture}"
run_case ModuleGlobals "$(dll 04-Kernel/ModuleGlobals ModuleGlobals)" "${gpu_architecture}"

if [[ -n "${HIPSHARP_PRECOMPILED_CODE_OBJECT:-}" ]]; then
  run_case PrecompiledModule "$(dll 04-Kernel/PrecompiledModule PrecompiledModule)" "${HIPSHARP_PRECOMPILED_CODE_OBJECT}" VectorAdd
else
  run_case PrecompiledModule "$(dll 04-Kernel/PrecompiledModule PrecompiledModule)"
fi

run_case ExplicitGraphDag "$(dll 05-Graph/ExplicitGraphDag ExplicitGraphDag)"
run_case GraphCaptureReplay "$(dll 05-Graph/GraphCaptureReplay GraphCaptureReplay)"
run_case PeerToPeerCopy "$(dll 06-MultiDevice/PeerToPeerCopy PeerToPeerCopy)"
run_case ArrayTextureSurface "$(dll 07-DataObjects/ArrayTextureSurface ArrayTextureSurface)"
run_case NativeAbiInterop "$(dll 90-LowLevel/NativeAbiInterop NativeAbiInterop)"

cat "${record_directory}/results.csv"
echo "Tutorial verification record: ${record_directory}"
if [[ ${failed_count} -ne 0 ]]; then
  echo "Tutorial verification failures: ${failed_count}" >&2
  exit 1
fi
