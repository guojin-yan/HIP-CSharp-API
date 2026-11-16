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
from evidence import parse, inventory, validate_helper, HOST_SYMBOLS, FATBIN_FIXTURES, validate_fatbinary

FIXTURES = ("device", "device-config", "context", "context-flags", "primary-context", "primary-context-flags", "copy-aliases",
            "memset-1d", "memset-2d8", "memset-2d16", "memset-2d32", "memset-2d16-async", "memset-2d32-async", "host", "pointer", "stream-event", "capture",
            "graph-topology", "graph-events", "graph-host", "graph-memory", "graph-child",
            "rtc-code", "rtc-link", "rtc-negative", "module", "virtual-memory", "memory-pool", "array-copy", "array-driver", "negative-parameters",
            "driver-copy", "callbacks", "user-object", "graph-update", "array-2d", "mipmapped-array", "texture-queries", "device-identity",
            "library", "driver-link", "graph-kernel", "pitched-runtime", "extension-stream", "graph-allocation", "generic-graph", "proc-address",
            "driver-texture", "driver-mipmapped", "host-runtime", "host-attributes", "host-symbol",
            "runtime-graph-copy", "driver-graph-memset", "stream-values", "stream-batch", "graph-batch", "capture-to-graph", "primary-lifetime", "device-reset", "batch-copy", "batch-copy-3d",
            "registered-texture-parameters", "registered-texture-array", "registered-texture-linear", "registered-texture-pitched",
            "negative-memory", "negative-streams", "negative-events", "negative-graphs", "selection-diagnostics", "profiler", "module-global",
            "module-texture-array", "module-texture-linear", "module-texture-pitched", "module-texture-mipmapped",
            "registered-texture-mipmapped", "legacy-device-properties", "peer-single-device", "stream-attach", "profiler-stop", "managed-basic", "driver-graph-allocation", "texture-resource-view")
HOST_FIXTURES = tuple(HOST_SYMBOLS) + FATBIN_FIXTURES


def sha(path):
    with open(path, "rb") as stream:
        return hashlib.file_digest(stream, "sha256").hexdigest()


def tool_snapshot(repository):
    directory = repository / "eng/function-validation"
    files = sorted(p for pattern in ("*.py", "*.cs", "*.hip") for p in directory.glob(pattern))
    files += [repository / "eng/local-runtime" / name for name in ("PackageRunner.cs", "PackageModuleAudit.cs")]
    files.append(repository / "eng/interop/complete-api-model.json")
    return {str(p.relative_to(repository)).replace("\\", "/"): sha(p) for p in files}


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
    parser.add_argument("--hip-root", type=Path, help="Installed HIP SDK used only to compile registered host test symbols")
    parser.add_argument("--fixture", action="append", choices=FIXTURES)
    args = parser.parse_args()
    if not re.fullmatch(r"gfx[0-9a-f]+", args.architecture):
        raise ValueError("Invalid architecture")
    requested = args.fixture or FIXTURES
    if set(requested).intersection(HOST_FIXTURES) and not args.hip_root:
        raise ValueError("Host symbol fixtures require --hip-root for the offline native helper build")
    repository = Path(__file__).resolve().parents[2]
    commit = subprocess.check_output(["git", "-C", str(repository), "rev-parse", "HEAD"], text=True).strip()
    dirty = bool(subprocess.check_output(["git", "-C", str(repository), "status", "--porcelain"], text=True).strip())
    snapshot = tool_snapshot(repository)
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
    helper = None
    helper_build = None
    if set(requested).intersection(HOST_FIXTURES):
        sdk = args.hip_root.resolve(strict=True)
        compiler = sdk / "bin" / ("hipcc.exe" if os.name == "nt" else "hipcc")
        helper = output / ("host-kernel.dll" if os.name == "nt" else "host-kernel.so")
        helper_env = os.environ.copy()
        helper_env["HIP_PATH"] = str(sdk)
        helper_env["PATH"] = str(sdk / "bin") + os.pathsep + helper_env["PATH"]
        command = [str(compiler), "-shared", "--offload-arch=" + args.architecture, "-O2", "-std=c++17"]
        if os.name != "nt":
            command += ["-fPIC", "-Wl,-rpath,$ORIGIN/runtimes/linux-x64/native"]
        command += [str(Path(__file__).parent / "host-kernel.hip"), "-o", str(helper)]
        compiled = subprocess.run(command, cwd=output, env=helper_env, capture_output=True, text=True, encoding="utf-8", timeout=180)
        (output / "helper-build.log").write_text(compiled.stdout + compiled.stderr, encoding="utf-8")
        if compiled.returncode or not helper.exists():
            raise RuntimeError("Native helper build failed: " + str(output / "helper-build.log"))
        helper_build = dict(file=helper.name, sha256=sha(helper), compiler=str(compiler), compilerSha256=sha(compiler),
                            source="eng/function-validation/host-kernel.hip", sourceSha256=sha(Path(__file__).parent / "host-kernel.hip"),
                            log="helper-build.log", logSha256=sha(output / "helper-build.log"))
        if set(requested).intersection(FATBIN_FIXTURES):
            object_file = output / ("host-kernel.obj" if os.name == "nt" else "host-kernel.o")
            payload = output / "host-kernel.fatbin"
            extractor = sdk / "lib/llvm/bin" / ("llvm-objcopy.exe" if os.name == "nt" else "llvm-objcopy")
            commands = [[str(compiler), "-c", "--offload-arch=" + args.architecture, "-O2", "-std=c++17",
                         str(Path(__file__).parent / "host-kernel.hip"), "-o", str(object_file)],
                        [str(extractor), "--dump-section", ".hip_fatbin=" + str(payload), str(object_file)]]
            for command in commands:
                compiled = subprocess.run(command, cwd=output, env=helper_env, capture_output=True, text=True, encoding="utf-8", timeout=180)
                with (output / "helper-build.log").open("a", encoding="utf-8") as log:
                    log.write(compiled.stdout + compiled.stderr)
                if compiled.returncode:
                    raise RuntimeError("Native fatbinary build failed: " + str(output / "helper-build.log"))
            helper_build["logSha256"] = sha(output / "helper-build.log")
            helper_build["fatbinary"] = dict(file=payload.name, sha256=sha(payload), objectFile=object_file.name,
                                              objectSha256=sha(object_file), extractor=str(extractor), extractorSha256=sha(extractor))
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
    if helper:
        target = output / "bin/Release/net10.0" / helper.name
        shutil.copy2(helper, target)
        env["HIPSHARP_TEST_HELPER"] = str(target)
        env["HIPSHARP_TEST_HELPER_SHA256"] = helper_build["sha256"]
        if helper_build.get("fatbinary"):
            deployed_payload = target.parent / payload.name
            shutil.copy2(payload, deployed_payload)
            env["HIPSHARP_TEST_FATBIN"] = str(deployed_payload)
            env["HIPSHARP_TEST_FATBIN_SHA256"] = helper_build["fatbinary"]["sha256"]
    model = json.loads((repository / "eng/interop/complete-api-model.json").read_text(encoding="utf-8-sig"))
    mapping = {"Raw." + f["managedName"]: f["entryPoint"] for f in model["runtimeFunctions"]}
    mapping.update({"Rtc." + f["managedName"]: f["entryPoint"] for f in model["rtcFunctions"]})
    results = []
    for fixture in requested:
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
            if evidence["passed"] and fixture in HOST_SYMBOLS:
                validate_helper(log, fixture, target, helper_build["sha256"], bool(evidence["nativeRestrictions"]))
            if evidence["passed"] and fixture in FATBIN_FIXTURES:
                validate_fatbinary(log, deployed_payload, helper_build["fatbinary"]["sha256"])
        except ValueError as error:
            evidence = dict(passed=False, functions={}, nativeRestrictions=[], evidenceError=str(error))
        item = dict(fixture=fixture, exitCode=code, log=log_path.name, logSha256=sha(log_path), **evidence)
        results.append(item)
        status = "FAILED" if not item["passed"] else "completed-with-native-restrictions" if item["nativeRestrictions"] else "passed"
        print(f"{fixture}: {status} ({len(item['functions'])} functions)", flush=True)
    stable = snapshot == tool_snapshot(repository)
    stable = stable and commit == subprocess.check_output(["git", "-C", str(repository), "rev-parse", "HEAD"], text=True).strip()
    full_matrix = set(args.fixture or FIXTURES) == set(FIXTURES)
    all_supported = not any(r["nativeRestrictions"] for r in results)
    summary = dict(scope="local-native-function-fixtures", completed=True, allPassed=all(r["passed"] for r in results) and all_supported,
                   fullMatrix=full_matrix, evidenceStable=stable, allFunctionsSupported=not any(r["nativeRestrictions"] for r in results),
                   rid=rid, wsl=Path("/dev/dxg").exists(), host=platform.platform(), architecture=args.architecture, sourceCommit=source_commit,
                   validationCommit=commit, sourceTreeDirty=dirty or bool(subprocess.check_output(["git", "-C", str(repository), "status", "--porcelain"], text=True).strip()),
                   core=dict(path=str(args.core), sha256=sha(args.core)), runtime=dict(path=str(args.runtime), sha256=sha(args.runtime)),
                   toolSources=snapshot, nativeHelper=helper_build, results=results, functions=inventory(results, mapping))
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    return 0 if summary["allPassed"] and stable and summary["allFunctionsSupported"] else 1


if __name__ == "__main__":
    sys.exit(main())
