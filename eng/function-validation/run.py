#!/usr/bin/env python3
"""Offline, exact-package native fixtures. Output never enters the repository."""
import argparse
import hashlib
import json
import os
import platform
from pathlib import Path
import re
import shutil
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

sys.dont_write_bytecode = True
from evidence import parse, inventory

FIXTURES = ("device", "device-config", "context", "context-flags", "primary-context", "primary-context-flags", "copy-aliases",
            "memset-1d", "memset-2d8", "memset-2d16", "memset-2d32", "memset-2d16-async", "memset-2d32-async", "host", "pointer", "stream-event", "capture",
            "graph-topology", "graph-events", "graph-host", "graph-memory", "graph-child",
            "rtc-code", "rtc-link", "rtc-negative", "module", "virtual-memory", "memory-pool", "array-copy", "array-driver", "negative-parameters",
            "driver-copy", "callbacks", "user-object", "graph-update", "array-2d", "mipmapped-array", "texture-queries", "device-identity")


def sha(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def metadata(path):
    with zipfile.ZipFile(path) as package:
        names = [n for n in package.namelist() if n.endswith(".nuspec")]
        if len(names) != 1:
            raise ValueError("Expected exactly one nuspec")
        root = ET.fromstring(package.read(names[0]))
        for element in root.iter():
            element.tag = element.tag.rsplit("}", 1)[-1]
        return root.find("metadata")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--core", type=Path, required=True)
    parser.add_argument("--runtime", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--architecture", default="gfx1100")
    parser.add_argument("--fixture", action="append", choices=FIXTURES)
    args = parser.parse_args()
    if not re.fullmatch(r"gfx[0-9a-f]+", args.architecture):
        raise ValueError("Invalid architecture")
    repository = Path(__file__).resolve().parents[2]
    commit = subprocess.check_output(["git", "-C", str(repository), "rev-parse", "HEAD"], text=True).strip()
    dirty = bool(subprocess.check_output(["git", "-C", str(repository), "status", "--porcelain"], text=True).strip())
    tool_files = sorted(Path(__file__).parent.glob("*.py")) + sorted(Path(__file__).parent.glob("*.cs"))
    tool_files += [repository / "eng/local-runtime" / name for name in ("PackageRunner.cs", "PackageModuleAudit.cs")]
    tool_files.append(repository / "eng/interop/complete-api-model.json")
    snapshot = {str(p.relative_to(repository)).replace("\\", "/"): sha(p) for p in tool_files}
    output = args.output.resolve()
    if output == repository or repository in output.parents:
        raise ValueError("Output must be outside source repository")
    if output.exists():
        raise ValueError("Use a new output root")
    args.core = args.core.resolve(strict=True)
    args.runtime = args.runtime.resolve(strict=True)
    core = metadata(args.core)
    runtime = metadata(args.runtime)
    source_commit = core.find("repository").attrib["commit"]
    if not re.fullmatch("[a-f0-9]{40}", source_commit):
        raise ValueError("Core package does not identify a full source commit")
    rid = "win-x64" if sys.platform == "win32" else "linux-x64"
    with zipfile.ZipFile(args.runtime) as package:
        manifest = json.loads(package.read("local-runtime-manifest.json"))
        if manifest["rid"] != rid or manifest["scope"] != "local-development":
            raise ValueError("Wrong Runtime scope or host RID")
        if runtime.findtext("id") != manifest["packageId"] or runtime.findtext("version") != manifest["packageVersion"]:
            raise ValueError("Runtime manifest and nuspec identity differ")
        for item in manifest["files"]:
            if hashlib.sha256(package.read(item["packagePath"])).hexdigest() != item["sha256"]:
                raise ValueError("Runtime manifest hash mismatch: " + item["name"])
    output.mkdir(parents=True)
    feed = output / "feed"
    feed.mkdir()
    for path in (args.core, args.runtime):
        shutil.copy2(path, feed / path.name)
    (output / "native-manifest.json").write_text(json.dumps(manifest), encoding="utf-8")
    config = ET.Element("configuration")
    sources = ET.SubElement(config, "packageSources")
    ET.SubElement(sources, "clear")
    ET.SubElement(sources, "add", key="local", value=str(feed))
    ET.ElementTree(config).write(output / "NuGet.Config", encoding="utf-8")
    project = ET.Element("Project", Sdk="Microsoft.NET.Sdk")
    props = ET.SubElement(project, "PropertyGroup")
    for key, value in dict(OutputType="Exe", TargetFramework="net10.0", AllowUnsafeBlocks="true",
                           EnableDefaultCompileItems="false", Nullable="enable", NuGetAudit="false",
                           StartupObject="PackageRunner").items():
        ET.SubElement(props, key).text = value
    items = ET.SubElement(project, "ItemGroup")
    fixture_sources = sorted(Path(__file__).parent.glob("*.cs"))
    fixture_sources += [repository / "eng/local-runtime" / name for name in ("PackageRunner.cs", "PackageModuleAudit.cs")]
    for path in fixture_sources:
        ET.SubElement(items, "Compile", Include=str(path))
    for meta in (core, runtime):
        ET.SubElement(items, "PackageReference", Include=meta.findtext("id"), Version="[" + meta.findtext("version") + "]")
    ET.ElementTree(project).write(output / "consumer.csproj", encoding="utf-8")
    env = os.environ.copy()
    for name in ("HIP_PATH", "ROCM_PATH", "LD_LIBRARY_PATH", "LD_PRELOAD", "HIPSHARP_OUTPUT_ROOT", "RestoreSources"):
        env.pop(name, None)
    env["PATH"] = os.pathsep.join(p for p in env["PATH"].split(os.pathsep)
                                  if not re.search(r"AMD[/\\]ROCm|/opt/rocm", p, re.I))
    if Path("/dev/dxg").exists():
        env["LD_LIBRARY_PATH"] = "/usr/lib/wsl/lib"
    env["NUGET_PACKAGES"] = str(output / "cache")
    env["HIPSHARP_LOCAL_RUNTIME_MANIFEST"] = str(output / "native-manifest.json")
    env["NUGET_CERT_REVOCATION_MODE"] = "offline"
    env["HIPSHARP_TEST_ARCH"] = args.architecture
    env["HIPSHARP_TEST_WORK"] = str(output)
    build = subprocess.run(["dotnet", "build", str(output / "consumer.csproj"), "-c", "Release", "--configfile",
                            str(output / "NuGet.Config"), "--nologo"], env=env, capture_output=True, text=True, encoding="utf-8", timeout=180)
    (output / "build.log").write_text(build.stdout + build.stderr, encoding="utf-8")
    if build.returncode:
        raise RuntimeError("Consumer build failed: " + str(output / "build.log"))
    model = json.loads((repository / "eng/interop/complete-api-model.json").read_text(encoding="utf-8-sig"))
    mapping = {"Raw." + f["managedName"]: f["entryPoint"] for f in model["runtimeFunctions"]}
    mapping.update({"Rtc." + f["managedName"]: f["entryPoint"] for f in model["rtcFunctions"]})
    results = []
    for fixture in args.fixture or FIXTURES:
        try:
            run = subprocess.run(["dotnet", str(output / "bin/Release/net10.0/consumer.dll"), fixture],
                                 env=env, capture_output=True, text=True, encoding="utf-8", timeout=90)
            log, code = run.stdout + run.stderr, run.returncode
        except subprocess.TimeoutExpired as error:
            def decode(value):
                return value.decode(errors="replace") if isinstance(value, bytes) else value or ""
            log, code = decode(error.stdout) + decode(error.stderr) + "\nFIXTURE_TIMEOUT\n", -1
        log_path = output / (fixture + ".log")
        log_path.write_text(log, encoding="utf-8")
        try:
            evidence = parse(log, code, fixture, mapping)
        except ValueError as error:
            evidence = dict(passed=False, functions={}, nativeRestrictions=[], evidenceError=str(error))
        item = dict(fixture=fixture, exitCode=code, log=log_path.name, logSha256=sha(log_path), **evidence)
        results.append(item)
        status = "FAILED" if not item["passed"] else "completed-with-native-restrictions" if item["nativeRestrictions"] else "passed"
        print(f"{fixture}: {status} ({len(item['functions'])} functions)", flush=True)
    stable = snapshot == {str(p.relative_to(repository)).replace("\\", "/"): sha(p) for p in tool_files}
    stable = stable and commit == subprocess.check_output(["git", "-C", str(repository), "rev-parse", "HEAD"], text=True).strip()
    full_matrix = set(args.fixture or FIXTURES) == set(FIXTURES)
    all_supported = not any(r["nativeRestrictions"] for r in results)
    summary = dict(scope="local-native-function-fixtures", completed=True, allPassed=all(r["passed"] for r in results) and all_supported,
                   fullMatrix=full_matrix, evidenceStable=stable, allFunctionsSupported=not any(r["nativeRestrictions"] for r in results),
                   rid=rid, wsl=Path("/dev/dxg").exists(), host=platform.platform(), architecture=args.architecture, sourceCommit=source_commit,
                   validationCommit=commit, sourceTreeDirty=dirty or bool(subprocess.check_output(["git", "-C", str(repository), "status", "--porcelain"], text=True).strip()),
                   core=dict(path=str(args.core), sha256=sha(args.core)), runtime=dict(path=str(args.runtime), sha256=sha(args.runtime)),
                   toolSources=snapshot, results=results, functions=inventory(results, mapping))
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return 0 if summary["allPassed"] and stable and summary["allFunctionsSupported"] else 1


if __name__ == "__main__":
    sys.exit(main())
