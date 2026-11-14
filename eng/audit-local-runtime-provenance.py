#!/usr/bin/env python3
"""Verify local Runtime bytes against pinned AMD wheels and collect source material.

Online checks compare the local wheel's multipart ETag with AMD's HTTPS response.
An ETag is not a publisher signature; this does not authorize redistribution.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import shutil
import sys
import tarfile
import urllib.request
import zipfile

REPOSITORY = Path(__file__).resolve().parent.parent


def require(condition, message):
    if not condition:
        raise ValueError(message)


def digest(path, algorithm="sha256"):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, algorithm).hexdigest()


def multipart_etag(path, part_bytes):
    require(part_bytes >= 5 * 1024 * 1024, "Invalid multipart part size")
    parts = []
    with Path(path).open("rb") as stream:
        while data := stream.read(part_bytes):
            parts.append(hashlib.md5(data, usedforsecurity=False).digest())
    require(bool(parts), "Empty archive")
    return hashlib.md5(b"".join(parts), usedforsecurity=False).hexdigest() + "-" + str(len(parts))


def check_online(item, wheel):
    require(item["url"].startswith("https://stable.repo.amd.com/rocm/whl-next/"), "Unexpected publisher URL")
    with urllib.request.urlopen(urllib.request.Request(item["url"], method="HEAD"), timeout=30) as response:
        canonical = item["url"].replace("/rocm/whl-next/", "/rocm/core/whl-next/", 1)
        require(response.url in (item["url"], canonical), "Unexpected publisher redirect")
        require(int(response.headers["Content-Length"]) == wheel.stat().st_size, "Publisher archive size mismatch")
        expected = multipart_etag(wheel, item["multipartPartBytes"])
        require(response.headers["ETag"].strip('"') == expected, "Publisher archive ETag mismatch")
        return {"method": "HTTPS HEAD plus local multipart MD5 ETag", "matched": True,
                "etag": expected, "url": response.url, "publisherSignatureVerified": False}


def verify_wheel(wheel, hip_root, item, inventory):
    require(wheel.stat().st_size == item["bytes"] and digest(wheel) == item["sha256"], "Wheel hash/size mismatch")
    expected = {f["name"]: f for f in inventory["files"]}
    mapping = {f["name"]: f for f in item["files"]}
    require(len(mapping) == len(item["files"]) and mapping.keys() == expected.keys(), "Incomplete or duplicate wheel mapping")
    require(digest(hip_root / "share/therock/therock_manifest.json") == inventory["sdkManifestSha256"], "Installed SDK manifest mismatch")
    with zipfile.ZipFile(wheel) as archive:
        names = archive.namelist()
        require(len(names) == len(set(names)), "Duplicate wheel entry")
        manifest_bytes = archive.read(item["sdkManifestMember"])
        require(hashlib.sha256(manifest_bytes).hexdigest() == inventory["sdkManifestSha256"], "Wheel SDK manifest mismatch")
        manifest = json.loads(manifest_bytes)
        for name, target in expected.items():
            mapped = mapping[name]
            require(mapped["sha256"] == target["sha256"], "Inventory/mapping hash mismatch")
            member = PurePosixPath(mapped["member"])
            require(not member.is_absolute() and ".." not in member.parts and member.name == name, "Invalid wheel member")
            with archive.open(str(member)) as stream:
                require(hashlib.file_digest(stream, "sha256").hexdigest() == target["sha256"], f"Wheel member hash mismatch: {name}")
            installed = (hip_root / target["source"]).resolve()
            require(installed.is_relative_to(hip_root.resolve()), "Installed path escapes SDK")
            require(digest(installed) == target["sha256"], f"Installed binary differs from wheel: {name}")
    return manifest


def source_materials(build_source, downloads, output, build_lock, runtime_lock, platform):
    require(digest(build_source) == build_lock["sha256"], "TheRock source archive hash mismatch")
    source_dir = output / "source-materials"
    source_dir.mkdir()
    # Preserve full archives, including all source/license text and build patches.
    # Nothing from an untrusted source archive is extracted or executed.
    archives = []
    for item, source in [(build_lock, build_source)] + [
        (i, downloads / (i["sha256"] + "-" + i["name"]))
        for i in runtime_lock["downloads"] if i.get("archive") and platform in i["platforms"]
    ]:
        require(source.is_file() and digest(source) == item["sha256"], f"Source archive hash mismatch: {item['name']}")
        target = source_dir / item["name"]
        require(target.parent == source_dir and not target.exists(), "Invalid or duplicate source archive name")
        shutil.copyfile(source, target)
        archives.append({"name": item["name"], "url": item["url"], "sha256": item["sha256"], "bytes": target.stat().st_size})
    with tarfile.open(build_source) as archive:
        recipe_paths = sorted(m.name.split("/", 1)[1] for m in archive.getmembers() if m.isfile() and "/" in m.name and
                              any(part in m.name for part in ("/third-party/sysdeps/", "/patches/third-party/numactl/",
                                                             "/core/wsl-rocdxg/", "/third-party/msgpack-cxx/")))
    require(any(p.endswith("numactl/0001-configure.ac-drop-unused-libatomic-probe.patch") for p in recipe_paths),
            "Missing numactl build patch")
    return {"archives": archives, "buildRecipePaths": recipe_paths, "completeCorrespondingSource": False,
            "remaining": ["HIP/HSA/COMGR/LLVM full corresponding sources and complete embedded-code inventory",
                          "Exact build configuration and source-provision/relinking review for the distribution",
                          "Windows Authenticode release policy and target-host integration validation"]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--platform", required=True, choices=["win-x64", "ubuntu.24.04-x64"])
    for option in ("wheel", "hip-root", "downloads-cache", "build-source", "output-root"):
        parser.add_argument("--" + option, required=True, type=Path)
    parser.add_argument("--online", action="store_true", help="Compare archive against AMD HTTPS publisher metadata")
    args = parser.parse_args()
    output, hip_root = args.output_root.resolve(), args.hip_root.resolve()
    require(not output.is_relative_to(REPOSITORY) and not output.is_relative_to(hip_root), "Output must be outside source/SDK")
    require(not output.exists(), "Use a new audit output directory")
    provenance_lock_path = REPOSITORY / "eng/local-runtime-provenance-10.1.0.lock.json"
    runtime_lock_path = REPOSITORY / "eng/local-runtime-10.1.0.lock.json"
    lock = json.loads(provenance_lock_path.read_text(encoding="utf-8"))
    runtime_lock = json.loads(runtime_lock_path.read_text(encoding="utf-8"))
    item = lock["platforms"][args.platform]
    manifest = verify_wheel(args.wheel, hip_root, item, runtime_lock["platforms"][args.platform])
    require(manifest["the_rock_commit"] == lock["buildSources"]["commit"], "SDK/build source commit mismatch")
    publisher = check_online(item, args.wheel) if args.online else {"matched": False, "status": "not-checked-offline"}
    output.mkdir(parents=True)
    materials = source_materials(args.build_source, args.downloads_cache, output, lock["buildSources"], runtime_lock, args.platform)
    report = {"schemaVersion": 1, "scope": "local-runtime-provenance", "platform": args.platform,
              "archiveSha256": item["sha256"], "archiveUrl": item["url"], "binaryMappings": item["files"],
              "sdkManifestSha256": runtime_lock["platforms"][args.platform]["sdkManifestSha256"],
              "sourceRevisions": manifest["submodules"], "theRockCommit": manifest["the_rock_commit"],
              "sdkBuildFlags": manifest.get("flags", {}), "publisherArchiveCheck": publisher,
              "sourceMaterials": materials, "provenanceLockSha256": digest(provenance_lock_path),
              "runtimeLockSha256": digest(runtime_lock_path), "publicReleaseValidated": False,
              "notes": ["Multipart MD5 ETag is a publisher transport identity check, not a signature or published SHA-256.",
                        "Submodule patches=[] does not mean no TheRock sysdeps patches; inspect the preserved build tree.",
                        "Local NuGet Linux RPATH rewriting and aliases are additional packaging transformations."]}
    (output / "provenance.json").write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Verified {len(item['files'])} wheel/installed binary mappings; source material collected; publicReleaseValidated=false")


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, OSError, zipfile.BadZipFile, tarfile.TarError) as error:
        print(f"Provenance audit rejected: {error}", file=sys.stderr)
        sys.exit(1)
