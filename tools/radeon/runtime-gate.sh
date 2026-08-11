#!/usr/bin/env bash
set -euo pipefail

expected_commit="${1:?usage: runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression] [RUNTIME_PACKAGE_COMMIT]}"
core_package="${2:?usage: runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression] [RUNTIME_PACKAGE_COMMIT]}"
runtime_package="${3:?usage: runtime-gate.sh EXPECTED_COMMIT CORE_NUPKG RUNTIME_NUPKG [candidate|final|regression] [RUNTIME_PACKAGE_COMMIT]}"
package_mode="${4:-final}"
runtime_package_commit="${5:-}"
repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
evidence_dir="${repository_root}/artifacts/radeon-runtime"

[[ "${package_mode}" == "candidate" || "${package_mode}" == "final" || "${package_mode}" == "regression" ]] || {
  echo "Package mode must be candidate, final, or regression." >&2
  exit 1
}
if [[ "${package_mode}" == "regression" ]]; then
  [[ "${runtime_package_commit}" =~ ^[0-9a-f]{40}$ ]] || {
    echo "Regression mode requires the runtime package's lowercase 40-character Git SHA." >&2
    exit 1
  }
  [[ "${runtime_package_commit}" != "${expected_commit}" ]] || {
    echo "Regression mode requires a historical runtime package commit." >&2
    exit 1
  }
elif [[ -n "${runtime_package_commit}" ]]; then
  echo "A runtime package commit may only be supplied in regression mode." >&2
  exit 1
fi

if [[ "$(git -C "${repository_root}" rev-parse HEAD)" != "${expected_commit}" ]] ||
   git -C "${repository_root}" symbolic-ref -q HEAD >/dev/null ||
   [[ -n "$(git -C "${repository_root}" status --porcelain)" ]]; then
  echo "Runtime validation requires the exact clean detached commit ${expected_commit}." >&2
  exit 1
fi
if [[ "${HIPSHARP_ISOLATED_CONSUMER:-}" != "1" ]]; then
  echo "Refusing runtime validation without HIPSHARP_ISOLATED_CONSUMER=1." >&2
  exit 1
fi
if [[ -e /opt/rocm ]] || find /opt -maxdepth 3 -type f \( -name 'libamdhip64.so*' -o -name 'libhiprtc.so*' \) -print -quit 2>/dev/null | grep -q .; then
  echo "The clean consumer must not expose /opt/rocm or system HIP user-mode libraries." >&2
  exit 1
fi
for device in /dev/kfd /dev/dri; do
  [[ -e "${device}" ]] || { echo "Required AMD device boundary is missing: ${device}" >&2; exit 1; }
done
for tool in dotnet pwsh readelf ldd sha256sum git python3 pgrep; do
  command -v "${tool}" >/dev/null || { echo "Required runtime gate tool is unavailable: ${tool}" >&2; exit 1; }
done

# Persistent build servers keep PRoot tracing sessions alive after every gate has finished.
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1
export DOTNET_CLI_USE_MSBUILD_SERVER=0
export MSBUILDDISABLENODEREUSE=1
export UseSharedCompilation=false

mkdir -p "${evidence_dir}"
sha256sum "${core_package}" "${runtime_package}" | tee "${evidence_dir}/package-hashes.txt"

read_package_version() {
  python3 - "$1" "$2" <<'PY'
import sys
import zipfile
import xml.etree.ElementTree as ET

package, expected_id = sys.argv[1:]
with zipfile.ZipFile(package) as archive:
    names = [name for name in archive.namelist() if name.lower().endswith('.nuspec')]
    if len(names) != 1:
        raise SystemExit('Expected exactly one nuspec in ' + package)
    root = ET.fromstring(archive.read(names[0]))
metadata = next(node for node in root.iter() if node.tag.rsplit('}', 1)[-1] == 'metadata')
values = {node.tag.rsplit('}', 1)[-1]: (node.text or '').strip() for node in metadata}
if values.get('id') != expected_id or not values.get('version'):
    raise SystemExit('Unexpected package identity: ' + package)
print(values['version'])
PY
}

core_version="$(read_package_version "${core_package}" JYPPX.HIP.CSharp.API)"
runtime_version="$(read_package_version "${runtime_package}" JYPPX.HipSharp.Runtime.linux-x64)"
pwsh -NoProfile -File "${repository_root}/eng/verify-package.ps1" -PackagePath "${core_package}" -ExpectedVersion "${core_version}" -ExpectedRepositoryCommit "${expected_commit}" | tee "${evidence_dir}/core-package-audit.txt"
runtime_audit_args=(-NoProfile -File "${repository_root}/eng/verify-runtime-package.ps1" -PackagePath "${runtime_package}")
[[ "${package_mode}" == "candidate" ]] && runtime_audit_args+=(-Candidate)
[[ "${package_mode}" == "regression" ]] && runtime_audit_args+=(-ExpectedRepositoryCommit "${runtime_package_commit}")
pwsh "${runtime_audit_args[@]}" | tee "${evidence_dir}/runtime-package-audit.txt"

runtime_root="${evidence_dir}/consumer"
case "${runtime_root}" in
  "${repository_root}/artifacts/"*) ;;
  *) echo "Unexpected consumer output path: ${runtime_root}" >&2; exit 1 ;;
esac
rm -rf "${runtime_root}"
mkdir -p "${runtime_root}/feed" "${runtime_root}/packages"
cp "${core_package}" "${runtime_package}" "${runtime_root}/feed/"
cat > "${runtime_root}/NuGet.config" <<EOF
<?xml version="1.0" encoding="utf-8"?>
<configuration><packageSources><clear /><add key="M5 local feed" value="${runtime_root}/feed" /></packageSources></configuration>
EOF
cat > "${runtime_root}/Directory.Build.props" <<'EOF'
<Project />
EOF
cat > "${runtime_root}/Directory.Build.targets" <<'EOF'
<Project />
EOF
cat > "${runtime_root}/Directory.Packages.props" <<'EOF'
<Project />
EOF

make_consumer() {
  local name="$1" source="$2"
  local directory="${runtime_root}/${name}"
  mkdir -p "${directory}"
  cp "${repository_root}/samples/${source}/Program.cs" "${directory}/Program.cs"
  cat > "${directory}/Consumer.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType><ImplicitUsings>disable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup>
    <PackageReference Include="JYPPX.HIP.CSharp.API" Version="${core_version}" />
    <PackageReference Include="JYPPX.HipSharp.Runtime.linux-x64" Version="${runtime_version}" />
  </ItemGroup>
</Project>
EOF
  dotnet restore "${directory}/Consumer.csproj" --configfile "${runtime_root}/NuGet.config" --packages "${runtime_root}/packages" --force --no-cache | tee "${evidence_dir}/${name}-restore.txt"
  dotnet build "${directory}/Consumer.csproj" --configuration Release --no-restore -p:RestorePackagesPath="${runtime_root}/packages" | tee "${evidence_dir}/${name}-build.txt"
}

make_consumer device-info DeviceInfo
make_consumer memory-copy MemoryCopy
make_consumer hiprtc-vector-add HipRtcVectorAdd
make_consumer stream-event-vector-add HipStreamEventVectorAdd
make_consumer advanced-features HipAdvancedFeatures

native_directory="$(find "${runtime_root}/stream-event-vector-add/bin/Release/net10.0" -type f -name 'libamdhip64.so' -printf '%h\n' -quit)"
[[ -n "${native_directory}" ]] || { echo "NuGet native assets were not copied to the consumer output." >&2; exit 1; }
for library in "${native_directory}"/*.so*; do
  [[ -f "${library}" ]] || continue
  readelf -d "${library}" | tee -a "${evidence_dir}/native-loader.txt"
  ldd "${library}" | tee -a "${evidence_dir}/native-loader.txt"
done
if grep -E '/opt/rocm|not found' "${evidence_dir}/native-loader.txt"; then
  echo "Native loader evidence contains an undeclared path or unresolved dependency." >&2
  exit 1
fi

pwsh -NoProfile -File "${repository_root}/eng/verify-symbols.ps1" -LibraryPath "${native_directory}/libamdhip64.so" -LibraryName amdhip64 -RequireOptional -OutputPath "${evidence_dir}/runtime-symbols.json"
pwsh -NoProfile -File "${repository_root}/eng/verify-symbols.ps1" -LibraryPath "${native_directory}/libhiprtc.so" -LibraryName hiprtc -OutputPath "${evidence_dir}/hiprtc-symbols.json"

gpu_architecture="${HIP_ARCH:?Set HIP_ARCH to the architecture reported by rocminfo in this isolated environment.}"
for case_name in device-info memory-copy; do
  (cd "${runtime_root}/${case_name}" && LD_DEBUG=libs dotnet run --configuration Release --no-build --no-restore 2>&1) | tee "${evidence_dir}/${case_name}-run.txt"
done
(cd "${runtime_root}/hiprtc-vector-add" && LD_DEBUG=libs dotnet run --configuration Release --no-build --no-restore -- --arch "${gpu_architecture}" --length 256 --repeat 20 2>&1) | tee "${evidence_dir}/hiprtc-vector-add-run.txt"
(cd "${runtime_root}/stream-event-vector-add" && LD_DEBUG=libs dotnet run --configuration Release --no-build --no-restore -- --arch "${gpu_architecture}" --lifecycle-repeats 100 2>&1) | tee "${evidence_dir}/stream-event-vector-add-run.txt"
(cd "${runtime_root}/advanced-features" && LD_DEBUG=libs dotnet run --configuration Release --no-build --no-restore -- --arch "${gpu_architecture}" --graph-launch-repeats 3 --lifecycle-repeats 100 2>&1) | tee "${evidence_dir}/advanced-features-run.txt"
(cd "${runtime_root}/advanced-features" && dotnet run --configuration Release --no-build --no-restore -- --arch "${gpu_architecture}" --graph-launch-repeats 3 --lifecycle-repeats 250 --stress-rounds 10 --stress-streams 4 --stress-length 4194304 2>&1) | tee "${evidence_dir}/advanced-features-stress-run.txt"

while IFS= read -r -d '' evidence_file; do
  if grep -q '/opt/rocm' "${evidence_file}"; then
    echo "A runtime consumer hit /opt/rocm: ${evidence_file}." >&2
    exit 1
  fi
done < <(find "${evidence_dir}" -maxdepth 1 -type f -print0)
maps_file="${evidence_dir}/consumer-maps.txt"
stream_dll="${runtime_root}/stream-event-vector-add/bin/Release/net10.0/Consumer.dll"
dotnet "${stream_dll}" --arch "${gpu_architecture}" --lifecycle-repeats 5000 >/dev/null 2>&1 & stream_pid=$!
for _ in $(seq 1 500); do
  if [[ -r "/proc/${stream_pid}/maps" ]] && grep -q 'libamdhip64' "/proc/${stream_pid}/maps"; then
    cat "/proc/${stream_pid}/maps" > "${maps_file}"
    break
  fi
  kill -0 "${stream_pid}" 2>/dev/null || break
  sleep 0.01
done
wait "${stream_pid}"
[[ -s "${maps_file}" ]] || { echo 'Unable to capture consumer process maps.' >&2; exit 1; }
if grep -E '/opt/rocm|/usr/lib.*/lib(amdhip64|hiprtc)' "${maps_file}"; then
  echo 'Process maps show an undeclared ROCm user-mode path.' >&2
  exit 1
fi
for expected_library in libamdhip64 libhiprtc libhsa-runtime64 libamd_comgr; do
  grep -q "${expected_library}" "${maps_file}" || { echo "Process maps did not capture ${expected_library}." >&2; exit 1; }
done

device_native_directory="$(find "${runtime_root}/device-info/bin/Release/net10.0" -type f -name 'libamdhip64.so' -printf '%h\n' -quit)"
[[ -n "${device_native_directory}" ]] || { echo "Device-info native asset directory is missing." >&2; exit 1; }
hsa_soname="${device_native_directory}/libhsa-runtime64.so.1"
mv "${hsa_soname}" "${hsa_soname}.removed"
set +e
(cd "${runtime_root}/device-info" && dotnet run --configuration Release --no-build --no-restore >"${evidence_dir}/missing-dependency-negative.txt" 2>&1)
missing_dependency_exit=$?
set -e
mv "${hsa_soname}.removed" "${hsa_soname}"
if [[ ${missing_dependency_exit} -eq 0 ]] || ! grep -E 'HipLibraryLoadException|Unable to load' "${evidence_dir}/missing-dependency-negative.txt"; then
  echo 'Missing dependency did not produce a controlled loader failure.' >&2
  exit 1
fi

core_only="${runtime_root}/core-only"
cp -R "${runtime_root}/device-info" "${core_only}"
sed -i '/JYPPX.HipSharp.Runtime.linux-x64/d' "${core_only}/Consumer.csproj"
rm -rf "${core_only}/bin" "${core_only}/obj"
dotnet restore "${core_only}/Consumer.csproj" --configfile "${runtime_root}/NuGet.config" --packages "${runtime_root}/core-only-packages" --force --no-cache >/dev/null
dotnet build "${core_only}/Consumer.csproj" --configuration Release --no-restore -p:RestorePackagesPath="${runtime_root}/core-only-packages" >/dev/null
set +e
(cd "${core_only}" && dotnet run --configuration Release --no-build --no-restore >"${evidence_dir}/core-only-negative.txt" 2>&1)
core_only_exit=$?
set -e
if [[ ${core_only_exit} -eq 0 ]] || ! grep -q 'HipLibraryLoadException' "${evidence_dir}/core-only-negative.txt"; then
  echo 'Core-only isolated consumer did not produce a controlled loader failure.' >&2
  exit 1
fi

tampered_package="${runtime_root}/tampered-runtime.nupkg"
python3 - "${runtime_package}" "${tampered_package}" <<'PY'
import pathlib
import sys
import zipfile

source = pathlib.Path(sys.argv[1])
target = pathlib.Path(sys.argv[2])
with zipfile.ZipFile(source, "r") as archive, zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED) as output:
    for entry in archive.infolist():
        data = archive.read(entry.filename)
        if entry.filename == "runtimes/linux-x64/native/libhsa-runtime64.so.1":
            data = bytes([data[0] ^ 1]) + data[1:]
        output.writestr(entry, data)
PY
set +e
tamper_args=(-NoProfile -File "${repository_root}/eng/verify-runtime-package.ps1" -PackagePath "${tampered_package}")
[[ "${package_mode}" == "candidate" ]] && tamper_args+=(-Candidate)
[[ "${package_mode}" == "regression" ]] && tamper_args+=(-ExpectedRepositoryCommit "${runtime_package_commit}")
pwsh "${tamper_args[@]}" >"${evidence_dir}/tampered-package-negative.txt" 2>&1
tamper_exit=$?
set -e
if [[ ${tamper_exit} -eq 0 ]] || ! grep -q 'hash/size mismatch' "${evidence_dir}/tampered-package-negative.txt"; then
  echo 'Tampered runtime package did not fail the content audit.' >&2
  exit 1
fi

mix_directory="${runtime_root}/closure-mix"
mkdir -p "${mix_directory}/alternate"
cp "${native_directory}/libhiprtc.so" "${mix_directory}/alternate/libhiprtc.so"
cat > "${mix_directory}/ClosureMix.csproj" <<EOF
<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net10.0</TargetFramework><OutputType>Exe</OutputType></PropertyGroup><ItemGroup><PackageReference Include="JYPPX.HIP.CSharp.API" Version="${core_version}" /><PackageReference Include="JYPPX.HipSharp.Runtime.linux-x64" Version="${runtime_version}" /></ItemGroup></Project>
EOF
cat > "${mix_directory}/Program.cs" <<'EOF'
using System;
using JYPPX.HipSharp;
using JYPPX.HipSharp.Rtc;

_ = new HipRuntime(args[0]);
try
{
    _ = new HipRtc(args[1]);
    return 1;
}
catch (InvalidOperationException error) when (error.Message.Contains("same user-mode closure", StringComparison.Ordinal))
{
    Console.WriteLine(error.Message);
    return 0;
}
EOF
dotnet restore "${mix_directory}/ClosureMix.csproj" --configfile "${runtime_root}/NuGet.config" --packages "${runtime_root}/packages" --force --no-cache >/dev/null
dotnet build "${mix_directory}/ClosureMix.csproj" --configuration Release --no-restore -p:RestorePackagesPath="${runtime_root}/packages" >/dev/null
(cd "${mix_directory}" && dotnet run --configuration Release --no-build --no-restore -- "${native_directory}/libamdhip64.so" "${mix_directory}/alternate/libhiprtc.so") | tee "${evidence_dir}/closure-mix-negative.txt"

echo "M8.1 isolated runtime ${package_mode} gate passed for ${expected_commit}."
