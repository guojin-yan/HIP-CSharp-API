#!/usr/bin/env python3
"""Bind an explicitly reviewed Core candidate to exact dual-platform package tests.

The manifest hash is the review anchor, not a cryptographic GPU attestation.
Native Runtime packages in the bundle are test dependencies and are never published.
"""
import argparse
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import shutil
import sys
import xml.etree.ElementTree as ET
import zipfile

CORE_ID = "JYPPX.ROCm.HIP.CSharp.API"
WORKLOADS = {
    "EnvironmentAndDevice", "LinearMemoryCopy", "StreamAndEvent", "HipRtcVectorAdd",
    "HipRtcNegativeCompile", "HipRtcProgramLinker", "AsyncVectorAdd", "PinnedHostMemory",
    "PitchedMemory2D3D", "ManagedMemory", "AsyncAllocationAndMemoryPool", "VirtualMemory",
    "GraphCaptureReplay", "ExplicitGraphDag", "KernelOccupancy", "ModuleGlobals",
    "ArrayTextureSurface", "HipManagedExpansionValidation", "PublishedVectorAdd",
    "MissingHipRtcRejected", "RidPublishDotnet", "RidPublishAppHost",
}
TFMS = "net46 net461 net462 net47 net471 net472 net48 net481 netcoreapp3.1 net5.0 net6.0 net7.0 net8.0 net9.0 net10.0".split()


def require(condition, message):
    if not condition:
        raise ValueError(message)


def sha256(path):
    with Path(path).open("rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def safe_name(name):
    require(isinstance(name, str) and bool(name) and "\\" not in name and ":" not in name,
            "Invalid relative file name")
    parts = name.split("/")
    require(all(p not in ("", ".", "..") and p == p.strip() and not p.endswith(".") for p in parts),
            f"Unsafe relative path: {name}")
    require(not PurePosixPath(name).is_absolute(), f"Absolute path: {name}")
    return name


def bundle_file(root, name):
    path = root / safe_name(name)
    require(path.resolve().is_relative_to(root.resolve()), f"Path escapes bundle: {name}")
    require(path.is_file(), f"Missing bundle file: {name}")
    return path


def package_entries(path):
    entries = {}
    names = set()
    with zipfile.ZipFile(path) as archive:
        require(sum(i.file_size for i in archive.infolist()) < 2_000_000_000, "Oversized package")
        for item in archive.infolist():
            name = safe_name(item.filename.rstrip("/") if item.is_dir() else item.filename)
            require(name.casefold() not in names, f"Duplicate package entry: {name}")
            names.add(name.casefold())
            require((item.external_attr >> 16) & 0o170000 != 0o120000, "Symlink in package")
            if item.is_dir():
                continue
            with archive.open(item) as stream:
                entries[name] = (item.file_size, hashlib.file_digest(stream, "sha256").hexdigest())
    return entries


def package_identity(path):
    entries = package_entries(path)
    specs = [p for p in entries if p.endswith(".nuspec") and "/" not in p]
    require(len(specs) == 1, "Expected exactly one root nuspec")
    with zipfile.ZipFile(path) as archive:
        root = ET.fromstring(archive.read(specs[0]))
    # Ignore the XML namespace, not any of the package content during comparison.
    for element in root.iter():
        element.tag = element.tag.split("}")[-1]
    metadata = root.find("metadata")
    require(metadata is not None, "Missing package metadata")
    repository = metadata.find("repository")
    return (metadata.findtext("id"), metadata.findtext("version"),
            repository.get("commit") if repository is not None else None)


def compare(candidate, published):
    before, after = package_entries(candidate), package_entries(published)
    # NuGet repository signing may add/replace this one entry. All other bytes,
    # including nuspec, OPC metadata, README and licenses, remain protected.
    before.pop(".signature.p7s", None)
    after.pop(".signature.p7s", None)
    require(before == after, "Published package payload differs from reviewed candidate")


def unpack(archive_path, digest, output):
    require(sha256(archive_path) == digest, "Candidate archive SHA-256 mismatch")
    # Validate the complete entry set before extracting any file.
    entries = package_entries(archive_path)
    root = Path(output).resolve()
    require(not root.exists(), "Use a new extraction directory")
    root.mkdir(parents=True)
    with zipfile.ZipFile(archive_path) as archive:
        for name in entries:
            target = root / name
            target.parent.mkdir(parents=True, exist_ok=True)
            with archive.open(name) as source, target.open("xb") as dest:
                shutil.copyfileobj(source, dest)


def validate(root, manifest, expected_commit, expected_version, allow_prerelease=False):
    require(manifest.get("schemaVersion") == 1, "Unsupported candidate schema")
    require(re.fullmatch(r"[0-9a-f]{40}", expected_commit or ""), "Invalid source commit")
    pattern = r"[0-9]+\.[0-9]+\.[0-9]+" + (r"(?:-[0-9A-Za-z.-]+)?" if allow_prerelease else "")
    require(re.fullmatch(pattern, expected_version or ""), "A stable release version is required")
    require(manifest.get("sourceCommit") == expected_commit, "Candidate source commit mismatch")
    require(manifest.get("version") == expected_version, "Candidate version mismatch")
    files = manifest.get("files", {})
    require(isinstance(files, dict) and files, "Missing candidate files")
    require(len({p.casefold() for p in files}) == len(files), "Ambiguous bundle paths")
    for name, digest in files.items():
        require(re.fullmatch(r"[0-9a-f]{64}", digest or ""), "Invalid file SHA-256")
        require(sha256(bundle_file(root, name)) == digest, f"Candidate hash mismatch: {name}")
    core_name = manifest.get("corePackage")
    require(core_name in files, "Core package is not hashed")
    core = bundle_file(root, core_name)
    require(package_identity(core) == (CORE_ID, expected_version, expected_commit), "Core identity mismatch")
    entries = package_entries(core)
    for tfm in TFMS:
        for suffix in ("dll", "xml"):
            require(f"lib/{tfm}/{CORE_ID}.{suffix}" in entries, f"Missing Core asset: {tfm}/{suffix}")
    evidence = manifest.get("reviewEvidence", {})
    require(set(evidence) == {"managed", "api", "abi", "docs", "support"}, "All review evidence roles are required")
    require(all(p in files for p in evidence.values()), "Unhashed review evidence")
    validations = manifest.get("validations", {})
    require(set(validations) == {"win-x64", "linux-x64"}, "Both platform validations are required")
    for rid, validation in validations.items():
        summary_name, runtime_name = validation["summary"], validation["runtimePackage"]
        require(summary_name in files and runtime_name in files, "Unhashed validation input")
        summary = json.loads(bundle_file(root, summary_name).read_text(encoding="utf-8-sig"))
        require(summary.get("scope") == "local-package-consumers" and summary.get("platform") == rid,
                "Incorrect validation platform or scope")
        require(summary.get("completed") is True and summary.get("sourceTreeDirty") is False,
                "Validation must be complete and from a clean worktree")
        require(summary.get("sourceCommit") == expected_commit and summary.get("validationCommit") == expected_commit,
                "Validation source commit mismatch")
        require(summary.get("corePackageSha256") == files[core_name] and
                summary.get("runtimePackageSha256") == files[runtime_name], "Validation package hash mismatch")
        runtime_id, _, _ = package_identity(bundle_file(root, runtime_name))
        expected_id = CORE_ID + ".Runtime." + ("win-x64" if rid == "win-x64" else "ubuntu.24.04-x64")
        require(runtime_id == expected_id, "Runtime dependency identity mismatch")
        require(summary.get("architecture", "").startswith("gfx"), "Missing GPU architecture")
        require(summary.get("host") in ({"windows"} if rid == "win-x64" else {"wsl2", "linux-kfd"}),
                "Unsupported test host")
        workloads = summary.get("workloads", [])
        required = WORKLOADS | ({"FrameworkPackage"} if rid == "win-x64" else set())
        require(len(workloads) == len(required) and {w["workload"] for w in workloads} == required,
                "Missing or duplicate deployment/GPU workloads")
        for workload in workloads:
            name, status, code = workload["workload"], workload["status"], workload["exitCode"]
            if status == "skipped-capability":
                require(name == "ManagedMemory" or (rid == "linux-x64" and name == "ArrayTextureSurface"),
                        f"Required workload cannot be skipped: {name}")
                require(code == 0, "Capability skip must finish successfully")
            else:
                require(status == "passed" and code == (1 if name == "MissingHipRtcRejected" else 0),
                        f"Failed workload: {name}")
    return core_name


def seal(args):
    output = Path(args.output).resolve()
    require(not output.exists(), "Use a new candidate directory")
    output.mkdir(parents=True)
    files = {}

    def copy(source, name):
        source = Path(source).resolve()
        target = output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(source, target)
        files[name] = sha256(target)
        return name

    core = copy(args.core, "packages/" + Path(args.core).name)
    package_id, version, commit = package_identity(output / core)
    require(package_id == CORE_ID, "Not a Core package")
    validations = {}
    for rid, summary, runtime in (("win-x64", args.windows_summary, args.windows_runtime),
                                  ("linux-x64", args.linux_summary, args.linux_runtime)):
        validations[rid] = {"summary": copy(summary, f"validation/{rid}.json"),
                            "runtimePackage": copy(runtime, "packages/" + Path(runtime).name)}
    evidence = {role: copy(getattr(args, role), f"evidence/{role}/" + Path(getattr(args, role)).name)
                for role in ("managed", "api", "abi", "docs", "support")}
    manifest = dict(schemaVersion=1, version=version, sourceCommit=commit, corePackage=core,
                    files=files, validations=validations, reviewEvidence=evidence)
    validate(output, manifest, commit, version, args.allow_prerelease)
    path = output / "candidate.json"
    path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Review candidate.json AND its evidence before approving SHA-256: {sha256(path)}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest="command", required=True)
    p = commands.add_parser("seal")
    for option in ("core", "windows-summary", "windows-runtime", "linux-summary", "linux-runtime",
                   "managed", "api", "abi", "docs", "support", "output"):
        p.add_argument("--" + option, required=True)
    p.add_argument("--allow-prerelease", action="store_true", help="Local rehearsal only")
    p = commands.add_parser("verify")
    for option in ("manifest", "manifest-sha256", "commit", "version"):
        p.add_argument("--" + option, required=True)
    p.add_argument("--allow-prerelease", action="store_true", help="Local rehearsal only")
    p = commands.add_parser("compare")
    p.add_argument("--candidate", required=True)
    p.add_argument("--published", required=True)
    p = commands.add_parser("unpack")
    for option in ("archive", "archive-sha256", "output"):
        p.add_argument("--" + option, required=True)
    args = parser.parse_args()
    if args.command == "seal":
        seal(args)
    elif args.command == "compare":
        compare(args.candidate, args.published)
        print("Published protected payload matches reviewed candidate")
    elif args.command == "unpack":
        unpack(args.archive, args.archive_sha256, args.output)
    else:
        path = Path(args.manifest).resolve()
        require(sha256(path) == args.manifest_sha256, "Reviewed manifest SHA-256 mismatch")
        manifest = json.loads(path.read_text(encoding="utf-8-sig"))
        core = validate(path.parent, manifest, args.commit, args.version, args.allow_prerelease)
        print(core)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"Candidate rejected: {error}", file=sys.stderr)
        sys.exit(1)
