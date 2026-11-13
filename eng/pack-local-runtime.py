#!/usr/bin/env python3
"""Pack the pinned, installed TheRock SDK for local NuGet use (never publish)."""

import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tarfile
import urllib.request
import xml.etree.ElementTree as ET
import zipfile


REPOSITORY = Path(__file__).resolve().parent.parent


def digest(path, algorithm="sha256"):
    with path.open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).hexdigest()


def checked(command, **kwargs):
    return subprocess.run(command, check=True, text=True, **kwargs)


def relative(root, name):
    path = (root / name).resolve()
    if not path.is_relative_to(root.resolve()):
        raise ValueError(f"Path escapes its root: {name}")
    return path


def write_json(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def fetch(item, cache):
    path = cache / (item["sha256"] + "-" + item["name"])
    if not path.exists():
        print(f"Downloading notices/source: {item['name']}", flush=True)
        with urllib.request.urlopen(item["url"], timeout=120) as response, path.open("wb") as target:
            shutil.copyfileobj(response, target)
    if digest(path) != item["sha256"]:
        raise ValueError(f"Download hash mismatch: {item['name']}")
    return path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--hip-root", required=True, type=Path)
    parser.add_argument("--output-root", required=True, type=Path)
    parser.add_argument("--version", required=True, help="10.1.0-local.<unique build identifier>")
    args = parser.parse_args()
    if not re.fullmatch(r"10\.1\.0-local\.[0-9A-Za-z]+(?:[.-][0-9A-Za-z]+)*", args.version):
        raise ValueError("Only an explicit 10.1.0-local.<build> prerelease is allowed.")
    platform = "win-x64" if sys.platform == "win32" else "ubuntu.24.04-x64"
    if sys.platform not in ("win32", "linux"):
        raise ValueError("Only Windows x64 and Ubuntu 24.04 x64 are supported.")
    import platform as host_platform
    if host_platform.machine().lower() not in ("amd64", "x86_64"):
        raise ValueError("The pinned SDK is x64 only.")
    if sys.platform == "linux":
        release = dict(line.strip().split("=", 1) for line in Path("/etc/os-release").read_text().splitlines() if "=" in line)
        if release.get("ID", "").strip('"') != "ubuntu" or release.get("VERSION_ID", "").strip('"') != "24.04":
            raise ValueError("This inventory is restricted to Ubuntu 24.04.")
    root = args.hip_root.resolve(strict=True)
    output = args.output_root.resolve()
    if output.is_relative_to(REPOSITORY) or output.is_relative_to(root):
        raise ValueError("Output must be outside both the source checkout and installed SDK.")
    lock = json.loads((REPOSITORY / "eng/local-runtime-10.1.0.lock.json").read_text(encoding="utf-8"))
    inventory = lock["platforms"][platform]
    manifest_source = root / "share/therock/therock_manifest.json"
    if digest(manifest_source) != inventory["sdkManifestSha256"]:
        raise ValueError("Installed SDK metadata differs from the pinned local inventory.")
    # Validate every input before creating a package or altering staged ELF files.
    for item in inventory["files"] + inventory["notices"]:
        source = relative(root, item["source"])
        if digest(source) != item["sha256"]:
            raise ValueError(f"Installed SDK file hash mismatch: {item['source']}")
    work = output / (platform + "-" + args.version)
    work.mkdir(parents=True, exist_ok=False)
    stage = work / "stage"
    stage.mkdir()
    cache = output / "downloads"
    cache.mkdir(exist_ok=True)
    rid = inventory["rid"]
    native = stage / "runtimes" / rid / "native"
    native.mkdir(parents=True)
    files = []
    for item in inventory["files"]:
        target = native / item["name"]
        if target.exists() or Path(item["name"]).name != item["name"]:
            raise ValueError("Duplicate or non-flat native asset name.")
        shutil.copyfile(relative(root, item["source"]), target)
        transformation = None
        if sys.platform == "linux":
            checked(["patchelf", "--force-rpath", "--set-rpath", "$ORIGIN", str(target)])
            rpath = checked(["patchelf", "--print-rpath", str(target)], capture_output=True).stdout.strip()
            if rpath != "$ORIGIN":
                raise ValueError(f"Unrelocated native library: {target.name}")
            transformation = "patchelf --force-rpath --set-rpath $ORIGIN"
        files.append({**item, "packagePath": target.relative_to(stage).as_posix(), "sha256": digest(target),
                      "sourceSha256": item["sha256"], "bytes": target.stat().st_size, "transformation": transformation})
    for alias, original in inventory.get("aliases", {}).items():
        if Path(alias).name != alias or (native / alias).exists():
            raise ValueError("Invalid native alias.")
        shutil.copyfile(native / original, native / alias)
        files.append({"name": alias, "aliasOf": original, "packagePath": (native / alias).relative_to(stage).as_posix(),
                      "sha256": digest(native / alias), "bytes": (native / alias).stat().st_size})
    # ldd must resolve every shipped ELF dependency without the installed SDK.
    if sys.platform == "linux":
        environment = {k: v for k, v in os.environ.items() if k not in ("HIP_PATH", "ROCM_PATH", "LD_LIBRARY_PATH")}
        for path in sorted(native.iterdir()):
            trace = checked(["ldd", str(path)], capture_output=True, env=environment).stdout
            if "not found" in trace or str(root) in trace or "/opt/rocm/" in trace:
                raise ValueError(f"Unresolved or installed-SDK ELF dependency: {path.name}\n{trace}")
    notices = stage / "licenses"
    notices.mkdir()
    license_sources = []
    for item in inventory["notices"]:
        target = notices / item["name"]
        shutil.copyfile(relative(root, item["source"]), target)
        license_sources.append({**item, "packagePath": target.relative_to(stage).as_posix()})
    for item in lock["downloads"]:
        if platform not in item["platforms"]:
            continue
        downloaded = fetch(item, cache)
        if item.get("archive"):
            found = False
            with tarfile.open(downloaded) as archive:
                for member in archive.getmembers():
                    if not member.isfile():
                        continue
                    is_notice = re.fullmatch(r"(?:LICENSE|COPYING|COPYRIGHT|NOTICE)(?:[._-].*)?", Path(member.name).name, re.IGNORECASE)
                    embedded = item.get("embeddedNotices") and Path(member.name).suffix in (".c", ".h")
                    if not is_notice and not embedded:
                        continue
                    with archive.extractfile(member) as source:
                        contents = source.read()
                    if not is_notice:
                        # libdrm distributes its MIT/BSD notices in source headers.
                        comments = re.findall(rb"/\*.*?\*/", contents, re.DOTALL)
                        contents = b"\n\n".join(comment for comment in comments if b"copyright" in comment.lower() and
                                                 any(term in comment.lower() for term in (b"permission", b"redistribution", b"license")))
                        if not contents:
                            continue
                    # Only extract notice text; no archive path is used as an output path.
                    target = notices / (item["name"] + "-" + member.name.replace("/", "_"))
                    target.write_bytes(contents)
                    found = True
            if not found:
                raise ValueError(f"No license text found in {item['name']}")
        else:
            shutil.copyfile(downloaded, notices / item["name"])
        license_sources.append({k: v for k, v in item.items() if k != "platforms"})
    shutil.copyfile(manifest_source, stage / "therock_manifest.json")
    package_id = "JYPPX.ROCm.HIP.CSharp.API.Runtime." + platform
    commit = checked(["git", "-C", str(REPOSITORY), "rev-parse", "HEAD"], capture_output=True).stdout.strip()
    manifest = {"schemaVersion": 1, "scope": "local-development", "publicReleaseValidated": False,
                "packageId": package_id, "packageVersion": args.version, "rid": rid,
                "sdkVersion": lock["sdkVersion"], "hipRuntimeVersion": "7.16.26385", "hipRtcVersion": "9.0",
                "sourceCommit": commit, "installedSourcePolicy": inventory["sourcePolicy"],
                "packagingScriptSha256": digest(Path(__file__).resolve()),
                "inventoryLockSha256": digest(REPOSITORY / "eng/local-runtime-10.1.0.lock.json"),
                "sourceTreeDirty": bool(checked(["git", "-C", str(REPOSITORY), "status", "--porcelain"], capture_output=True).stdout.strip()),
                "sdkManifestSha256": inventory["sdkManifestSha256"], "files": files,
                "noticeSources": license_sources, "hostPrerequisites": inventory["hostPrerequisites"],
                "excluded": ["GPU driver", "firmware", "OS libraries", "SDK compiler executables", "math libraries"],
                "validation": "Run test-local-runtime-package.ps1 against the exact nupkg; packaging alone is not a GPU validation."}
    write_json(stage / "local-runtime-manifest.json", manifest)
    components = [{"type": "file", "name": item["name"], "hashes": [{"alg": "SHA-256", "content": item["sha256"]}],
                   "properties": [{"name": "packagePath", "value": item["packagePath"]}]} for item in files]
    write_json(stage / "local-runtime.cdx.json", {"bomFormat": "CycloneDX", "specVersion": "1.5", "version": 1,
               "metadata": {"component": {"type": "library", "name": package_id, "version": args.version}}, "components": components})
    (stage / "README.md").write_text(f"# Local HIP Runtime {args.version}\n\n"
        f"{platform}: installed TheRock SDK 10.1.0, HIP Runtime 7.16.26385 / HIPRTC 9.0.\n\n"
        "For local development; this package has not passed the official public-release promotion gate. "
        "Use with Core 0.11.0-dev. Drivers and OS libraries remain host prerequisites. "
        "No HIP_PATH or SDK PATH is required by the tested package workloads.\n\n"
        + "\n".join("- " + entry for entry in inventory["hostPrerequisites"]) + "\n\n"
        "Native hashes, source provenance, ELF changes and dependency boundaries are recorded in local-runtime-manifest.json. "
        "Third-party notices and source archive URLs are included; redistribution/source-offer review remains separate from local packaging.\n",
        encoding="utf-8")
    with (stage / "THIRD-PARTY-NOTICES.txt").open("w", encoding="utf-8") as combined:
        combined.write("Local runtime: components retain their own licenses.\n\n")
        for path in sorted(notices.iterdir()):
            combined.write(f"\n===== {path.name} =====\n" + path.read_text(encoding="utf-8", errors="replace") + "\n")
    # Standard SDK pack, in a generated project outside the source checkout. The
    # audited 7.2.1 projects/manifests and their release gates are not modified.
    nuspec = ET.Element("package")
    metadata = ET.SubElement(nuspec, "metadata")
    for key, value in {"id": package_id, "version": args.version, "authors": "JYPPX",
                       "description": "Local development HIP Runtime and HIPRTC user-mode closure; not a promoted public release.",
                       "readme": "README.md"}.items():
        ET.SubElement(metadata, key).text = value
    ET.SubElement(metadata, "license", {"type": "file"}).text = "THIRD-PARTY-NOTICES.txt"
    ET.SubElement(metadata, "repository", {"type": "git", "url": "https://github.com/guojin-yan/HIP-CSharp-API", "commit": commit})
    payload = ET.SubElement(nuspec, "files")
    for path in sorted(stage.rglob("*")):
        if path.is_file():
            name = path.relative_to(stage).as_posix()
            ET.SubElement(payload, "file", {"src": str(path), "target": name})
    ET.ElementTree(nuspec).write(work / "runtime.nuspec", encoding="utf-8", xml_declaration=True)
    (work / "runtime.csproj").write_text('''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <EnableDefaultItems>false</EnableDefaultItems>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <NuspecFile>runtime.nuspec</NuspecFile>
    <NuGetAudit>false</NuGetAudit>
  </PropertyGroup>
</Project>
''', encoding="utf-8")
    feed = output / "feed"
    checked(["dotnet", "pack", str(work / "runtime.csproj"), "--configuration", "Release", "--output", str(feed)])
    package = feed / f"{package_id}.{args.version}.nupkg"
    # Re-open exact package bytes, reject missing/extra payload and hash mismatches.
    with zipfile.ZipFile(package) as archive:
        names = archive.namelist()
        if len(names) != len(set(names)):
            raise ValueError("Duplicate entries in the NuGet package.")
        for path in stage.rglob("*"):
            if path.is_file() and hashlib.sha256(archive.read(path.relative_to(stage).as_posix())).hexdigest() != digest(path):
                raise ValueError(f"Package payload mismatch: {path.name}")
        native_names = {name for name in names if name.startswith("runtimes/")}
        if native_names != {item["packagePath"] for item in files}:
            raise ValueError("Unexpected native package payload.")
    result = {"scope": "local-development", "package": str(package), "sha256": digest(package), "bytes": package.stat().st_size,
              "nativeBytes": sum(item["bytes"] for item in files), "nativeFiles": len(files), "sourceCommit": commit}
    write_json(work / "package-audit.json", result)
    print(json.dumps(result, indent=2), flush=True)


if __name__ == "__main__":
    main()
