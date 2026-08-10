#!/usr/bin/env bash
set -euo pipefail

dotnet_version="${HIPSHARP_DOTNET_SDK_VERSION:-10.0.301}"
install_dir="${HIPSHARP_DOTNET_ROOT:-/workspace/.dotnet}"
bootstrap_dir="${HIPSHARP_BOOTSTRAP_DIR:-/workspace/bootstrap}"
installer="${bootstrap_dir}/dotnet-install.sh"

mkdir -p "${install_dir}" "${bootstrap_dir}"

if [[ -x "${install_dir}/dotnet" ]] && \
   "${install_dir}/dotnet" --list-sdks | grep -q "^${dotnet_version} "; then
  echo ".NET SDK ${dotnet_version} is already installed in ${install_dir}."
else
  echo "Downloading the official dotnet-install script with TLS verification enabled."
  curl --fail --location --proto '=https' --tlsv1.2 \
    https://dot.net/v1/dotnet-install.sh \
    --output "${installer}"
  sha256sum "${installer}"
  bash "${installer}" \
    --version "${dotnet_version}" \
    --install-dir "${install_dir}" \
    --no-path
fi

export DOTNET_ROOT="${install_dir}"
export PATH="${install_dir}:${PATH}"
dotnet --version

if [[ -d /persistent ]]; then
  echo "Persistent storage detected; cloud-test.sh will use it for the NuGet cache."
else
  echo "/persistent is not mounted; cloud-test.sh will use /workspace/.nuget/packages."
fi
