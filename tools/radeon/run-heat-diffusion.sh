#!/usr/bin/env bash
set -euo pipefail

# Compatibility entry point for existing cloud notes. The implementation lives beside
# the HeatDiffusion project so the showcase can be copied and run as one self-contained unit.
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repository_root="$(cd -- "${script_dir}/../.." && pwd)"
exec bash "${repository_root}/samples/showcases/HeatDiffusion/run-heat-diffusion.sh" "$@"
