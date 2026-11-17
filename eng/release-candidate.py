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
import stat
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


def read_json(path):
    def unique_object(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, f"Duplicate JSON key: {key}")
            result[key] = value
        return result
    return json.loads(Path(path).read_text(encoding="utf-8-sig"), object_pairs_hook=unique_object)


def safe_name(name):
    require(isinstance(name, str) and bool(name) and not re.search(r'[<>:"\\|?*\x00-\x1f\x7f]', name),
            "Invalid relative file name")
    parts = name.split("/")
    require(all(p not in ("", ".", "..") and p == p.strip() and not p.endswith(".") for p in parts),
            f"Unsafe relative path: {name}")
    require(not PurePosixPath(name).is_absolute(), f"Absolute path: {name}")
    require(not any(re.fullmatch(r"(?:CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])(?:\..*)?", p, re.IGNORECASE)
                    for p in parts), f"Reserved Windows path: {name}")
    return name


def check_path_tree(paths):
    # Windows merges A/one and a/two into one directory. Reject that ambiguity
    # on both hosts, as well as a file also used as another entry's parent.
    nodes = {}
    for path, directory in paths:
        safe_name(path)
        parts = path.split("/")
        for length in range(1, len(parts) + 1):
            prefix = "/".join(parts[:length])
            kind = "directory" if length < len(parts) or directory else "file"
            previous = nodes.get(prefix.casefold())
            require(previous is None or previous == (prefix, kind), f"Conflicting archive path: {prefix}")
            nodes[prefix.casefold()] = (prefix, kind)


def check_bundle_files(root, files):
    expected = set(files) | {"candidate.json"}
    actual = set()
    paths = []
    for path in root.rglob("*"):
        attributes = path.lstat()
        require(not path.is_symlink() and not (getattr(attributes, "st_file_attributes", 0) &
                getattr(stat, "FILE_ATTRIBUTE_REPARSE_POINT", 0)), f"Linked candidate entry: {path.name}")
        name = path.relative_to(root).as_posix()
        paths.append((name, path.is_dir()))
        if path.is_file():
            actual.add(name)
        else:
            require(path.is_dir(), f"Unsupported candidate entry: {name}")
    check_path_tree(paths)
    # seal validates just before writing candidate.json; verify requires it.
    require(actual - {"candidate.json"} == expected - {"candidate.json"},
            "Candidate directory contains missing or unlisted files")


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
        require(len(archive.infolist()) <= 50000, "Too many archive entries")
        check_path_tree([(i.filename.rstrip("/") if i.is_dir() else i.filename, i.is_dir()) for i in archive.infolist()])
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
    require(isinstance(manifest, dict), "Candidate manifest must be an object")
    schema = manifest.get("schemaVersion")
    require(schema in (1, 2), "Unsupported candidate schema")
    require(re.fullmatch(r"[0-9a-f]{40}", expected_commit or ""), "Invalid source commit")
    pattern = r"[0-9]+\.[0-9]+\.[0-9]+" + (r"(?:-[0-9A-Za-z.-]+)?" if allow_prerelease else "")
    require(re.fullmatch(pattern, expected_version or ""), "A stable release version is required")
    require(manifest.get("sourceCommit") == expected_commit, "Candidate source commit mismatch")
    require(manifest.get("version") == expected_version, "Candidate version mismatch")
    files = manifest.get("files", {})
    require(isinstance(files, dict) and files, "Missing candidate files")
    require("candidate.json" not in files, "Candidate manifest cannot hash itself")
    require(len({p.casefold() for p in files}) == len(files), "Ambiguous bundle paths")
    for name, digest in files.items():
        require(re.fullmatch(r"[0-9a-f]{64}", digest or ""), "Invalid file SHA-256")
        require(sha256(bundle_file(root, name)) == digest, f"Candidate hash mismatch: {name}")
    check_bundle_files(root, files)
    origin = None
    if schema == 2:
        require(manifest.get("runtimePayloadsIncluded") is False, "Public Core candidate cannot include Runtime payloads")
        origin_name = manifest.get("localCandidateManifest")
        require(origin_name in files and files[origin_name] == manifest.get("localCandidateManifestSha256"),
                "Unbound local candidate manifest")
        origin = read_json(bundle_file(root, origin_name))
        require(isinstance(origin, dict) and origin.get("schemaVersion") == 1, "Expected original local candidate")
        for key in ("version", "sourceCommit", "corePackage", "reviewEvidence"):
            require(origin.get(key) == manifest.get(key), f"Original candidate {key} mismatch")
        require(set(origin.get("validations", {})) == {"win-x64", "linux-x64"}, "Missing original validations")
        omitted = {v["runtimePackage"] for v in origin["validations"].values()}
        retained = {name: digest for name, digest in origin["files"].items() if name not in omitted}
        identities = {v["runtimeIdentity"] for v in manifest["validations"].values()}
        require(len(identities) == 2 and set(files) == set(retained) | identities | {origin_name},
                "Public candidate file inventory mismatch")
        require(all(files[name] == digest for name, digest in retained.items()), "Original candidate file hash mismatch")
        require(not (set(files) & omitted), "Runtime payload cannot be exported")
        require([name for name in files if name.lower().endswith(".nupkg")] == [manifest["corePackage"]],
                "Only the Core package may be exported")
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
        summary_name = validation["summary"]
        runtime_name = validation["runtimePackage" if schema == 1 else "runtimeIdentity"]
        require(summary_name in files and runtime_name in files, "Unhashed validation input")
        summary = read_json(bundle_file(root, summary_name))
        require(isinstance(summary, dict), "Validation summary must be an object")
        require(summary.get("scope") == "local-package-consumers" and summary.get("platform") == rid,
                "Incorrect validation platform or scope")
        require(summary.get("completed") is True and summary.get("sourceTreeDirty") is False,
                "Validation must be complete and from a clean worktree")
        require(summary.get("sourceCommit") == expected_commit and summary.get("validationCommit") == expected_commit,
                "Validation source commit mismatch")
        if schema == 1:
            runtime_sha = files[runtime_name]
            runtime_id, _, _ = package_identity(bundle_file(root, runtime_name))
        else:
            original_validation = origin["validations"][rid]
            require(summary_name == original_validation["summary"], "Original validation summary mismatch")
            identity = read_json(bundle_file(root, runtime_name))
            require(isinstance(identity, dict) and set(identity) == {"packageId", "version", "sourceCommit", "sha256", "size", "localPackage"},
                    "Invalid Runtime identity record")
            runtime_sha = origin["files"][original_validation["runtimePackage"]]
            require(re.fullmatch(r"[0-9a-f]{64}", runtime_sha or "") and identity["sha256"] == runtime_sha and
                    identity["localPackage"] == original_validation["runtimePackage"], "Original Runtime identity mismatch")
            require(isinstance(identity["version"], str) and bool(identity["version"]) and
                    isinstance(identity["sourceCommit"], str) and
                    type(identity["size"]) is int and identity["size"] > 0, "Invalid Runtime identity metadata")
            runtime_id = identity["packageId"]
        require(summary.get("corePackageSha256") == files[core_name] and
                summary.get("runtimePackageSha256") == runtime_sha, "Validation package hash mismatch")
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


def export_core(manifest_path, expected_sha, output):
    manifest_path, output = Path(manifest_path).resolve(), Path(output).resolve()
    require(sha256(manifest_path) == expected_sha, "Reviewed manifest SHA-256 mismatch")
    original = read_json(manifest_path)
    require(original.get("schemaVersion") == 1, "Export requires a complete local candidate")
    validate(manifest_path.parent, original, original["sourceCommit"], original["version"])
    require(not output.exists(), "Use a new public candidate directory")
    output.mkdir(parents=True)
    omitted = {v["runtimePackage"] for v in original["validations"].values()}
    files = {}
    for name, digest in original["files"].items():
        if name in omitted:
            continue
        target = output / name
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(bundle_file(manifest_path.parent, name), target)
        files[name] = digest
    origin_name = "review/local-candidate.json"
    (output / "review").mkdir(exist_ok=True)
    shutil.copyfile(manifest_path, output / origin_name)
    files[origin_name] = expected_sha
    validations = {}
    for rid, validation in original["validations"].items():
        name = validation["runtimePackage"]
        package = bundle_file(manifest_path.parent, name)
        package_id, version, commit = package_identity(package)
        identity_name = f"review/{rid}-runtime.json"
        identity = dict(packageId=package_id, version=version, sourceCommit=commit,
                        sha256=sha256(package), size=package.stat().st_size, localPackage=name)
        (output / identity_name).write_text(json.dumps(identity, indent=2) + "\n", encoding="utf-8")
        files[identity_name] = sha256(output / identity_name)
        validations[rid] = dict(summary=validation["summary"], runtimeIdentity=identity_name)
    manifest = dict(original, schemaVersion=2, files=files, validations=validations,
                    runtimePayloadsIncluded=False, localCandidateManifest=origin_name,
                    localCandidateManifestSha256=expected_sha)
    validate(output, manifest, original["sourceCommit"], original["version"])
    (output / "candidate.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
    print(f"Core-only candidate manifest SHA-256: {sha256(output / 'candidate.json')}")


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
    p = commands.add_parser("export-core")
    for option in ("manifest", "manifest-sha256", "output"):
        p.add_argument("--" + option, required=True)
    p = commands.add_parser("verify")
    for option in ("manifest", "manifest-sha256", "commit", "version"):
        p.add_argument("--" + option, required=True)
    p.add_argument("--allow-prerelease", action="store_true", help="Local rehearsal only")
    p.add_argument("--public-only", action="store_true", help="Reject bundles containing Runtime packages")
    p = commands.add_parser("compare")
    p.add_argument("--candidate", required=True)
    p.add_argument("--published", required=True)
    p = commands.add_parser("unpack")
    for option in ("archive", "archive-sha256", "output"):
        p.add_argument("--" + option, required=True)
    args = parser.parse_args()
    if args.command == "seal":
        seal(args)
    elif args.command == "export-core":
        export_core(args.manifest, args.manifest_sha256, args.output)
    elif args.command == "compare":
        compare(args.candidate, args.published)
        print("Published protected payload matches reviewed candidate")
    elif args.command == "unpack":
        unpack(args.archive, args.archive_sha256, args.output)
    else:
        path = Path(args.manifest).resolve()
        require(sha256(path) == args.manifest_sha256, "Reviewed manifest SHA-256 mismatch")
        manifest = read_json(path)
        if args.public_only:
            require(manifest.get("schemaVersion") == 2, "Publication requires a Core-only candidate")
        core = validate(path.parent, manifest, args.commit, args.version, args.allow_prerelease)
        print(core)


if __name__ == "__main__":
    try:
        main()
    except (ValueError, KeyError, TypeError, OSError, zipfile.BadZipFile, ET.ParseError) as error:
        print(f"Candidate rejected: {error}", file=sys.stderr)
        sys.exit(1)
