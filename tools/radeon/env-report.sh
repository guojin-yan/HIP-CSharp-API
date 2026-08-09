#!/usr/bin/env bash
set -euo pipefail

echo "== Session =="
date -Is
hostname
echo "== Kernel =="
uname -a
echo "== Operating system =="
cat /etc/os-release
echo "== CPU and memory cgroup limits =="
cat /sys/fs/cgroup/cpu.max
cat /sys/fs/cgroup/memory.max
echo "== Storage =="
findmnt -T /workspace
df -hT /workspace
if [[ -d /persistent ]]; then
  findmnt -T /persistent
  df -hT /persistent
else
  echo "/persistent is not mounted."
fi
echo "== Git =="
git rev-parse HEAD
git status --short
echo "== .NET =="
dotnet --info
echo "== Compilers =="
hipcc --version
gcc --version | head -n 1
python3 --version
echo "== HIP =="
hipconfig --full
echo "== ROCm SMI =="
rocm-smi --showproductname --showuniqueid --showmeminfo vram --showdriverversion
echo "== ROCm agents =="
rocminfo | grep -E '^[[:space:]]*(Name:|Marketing Name:)' | head -n 20
echo "== HIP library =="
hip_library="$(readlink -f /opt/rocm/lib/libamdhip64.so)"
echo "${hip_library}"
sha256sum "${hip_library}"
readelf -d "${hip_library}" | grep SONAME
