#!/usr/bin/env bash
set -euo pipefail

configuration="${1:-Release}"
version="${2:-0.0.0}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
solution="${repository_root}/HipSharp.sln"
package_dir="${repository_root}/artifacts/packages"

cd "${repository_root}"
if command -v pwsh >/dev/null 2>&1; then
  pwsh -NoProfile -File "${repository_root}/eng/generate-interop.ps1" -Verify
fi
dotnet restore "${solution}" --locked-mode
dotnet build "${solution}" --configuration "${configuration}" --no-restore -p:PackageVersion="${version}"
dotnet pack "src/JYPPX.HipSharp/JYPPX.HipSharp.csproj" \
  --configuration "${configuration}" \
  --no-build \
  --output "${package_dir}" \
  -p:PackageVersion="${version}"

export HIPSHARP_PACKAGE_PATH="${package_dir}/JYPPX.HIP.CSharp.API.${version}.nupkg"
dotnet test "${solution}" \
  --configuration "${configuration}" \
  --no-build \
  --no-restore \
  --results-directory "${repository_root}/artifacts/test-results" \
  --logger "trx;LogFilePrefix=hipsharp-linux"

printf 'Linux core gate passed: restore, 15-TFM build, pack, and tests.\n'
