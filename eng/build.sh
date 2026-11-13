#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
version="${2:-}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
export HIPSHARP_REPOSITORY_ROOT="${repository_root}"

if ! command -v pwsh >/dev/null 2>&1; then
  echo 'PowerShell 7 is required for generation, API, package and consumer verification.' >&2
  exit 1
fi

arguments=(-Configuration "${configuration}")
if [[ -n "${version}" ]]; then arguments+=(-Version "${version}"); fi
pwsh -NoProfile -File "${repository_root}/eng/build.ps1" "${arguments[@]}"
pwsh -NoProfile -File "${repository_root}/eng/test.ps1" "${arguments[@]}" -NoBuild
printf 'Linux core gate passed: restore, 15-TFM build, tests, API, package and consumers.\n'
