#!/usr/bin/env python3
"""Apply pinned upstream fixes outside this repository and compile GPU regression fragments.

This does not build or replace the complete HIP Runtime. Unchanged internal dependencies
are represented by the adapters documented in regression.cpp.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def function(source, name):
    start = source.index("hipError_t " + name + "(")
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end] + "\n"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--sources", type=Path, required=True, help="Cached upstream .cpp files")
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--hip-root", type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    repository = here.parents[1]
    output = args.output.resolve()
    if output == repository or repository in output.parents or output.exists():
        raise ValueError("Use a new output directory outside the source repository")
    lock = json.loads((here / "upstream.lock.json").read_text(encoding="utf-8"))
    # Validate every input before creating output or applying a patch.
    for item in lock["files"]:
        if sha(args.sources / Path(item["path"]).name) != item["sha256"]:
            raise ValueError("Upstream source hash mismatch: " + item["path"])
    output.mkdir(parents=True)
    for item in lock["files"]:
        target = output / item["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_bytes((args.sources / target.name).read_bytes())
    git = ["git", "-c", "core.autocrlf=false", "-c", "core.eol=lf", "apply"]
    for patch in sorted(here.glob("*.patch")):
        for command in (git + ["--check", str(patch)], git + [str(patch)]):
            subprocess.run(command, cwd=output, check=True, capture_output=True)
    groups = {"hip_memory.cpp": ("hipMemsetD2D16", "hipMemsetD2D16Async", "hipMemsetD2D32", "hipMemsetD2D32Async"),
              "hip_module.cpp": ("hipLinkAddData", "hipLinkAddFile")}
    fragments = []
    for item in lock["files"]:
        path = output / item["path"]
        if sha(path) != item["patchedSha256"]:
            raise ValueError("Unexpected patched bytes: " + item["path"])
    for label, root in (("baseline", args.sources), ("patched", output / "projects/clr/hipamd/src")):
        fragments.append("namespace " + label + " {\n")
        for name, symbols in groups.items():
            source = (root / name).read_text(encoding="utf-8")
            fragments.extend(function(source, symbol) for symbol in symbols)
        fragments.append("}\n")
    (output / "fragments.inc").write_text("".join(fragments), encoding="utf-8", newline="\n")
    names = ("hipGraphAddExternalSemaphoresWaitNode", "hipGraphAddExternalSemaphoresSignalNode",
             "hipGraphExternalSemaphoresSignalNodeSetParams", "hipGraphExternalSemaphoresWaitNodeSetParams",
             "hipGraphExternalSemaphoresSignalNodeGetParams", "hipGraphExternalSemaphoresWaitNodeGetParams",
             "hipGraphExecExternalSemaphoresSignalNodeSetParams", "hipGraphExecExternalSemaphoresWaitNodeSetParams")
    dispatch = ["namespace hip {\n"] + ["using ::" + n + ";\n" for n in names] + ["}\nstruct SelectedDispatch {\n"]
    dispatch += ["decltype(&::" + n + ") " + n + "_fn = nullptr;\n" for n in names]
    dispatch += ["};\nvoid dispatch_regression() {\nSelectedDispatch before, after;\n"]
    # Extract the selected initializer statements from the real before/after
    # UpdateDispatchTable bodies. This tests assignment/signature compatibility,
    # not execution of a rebuilt Runtime or graphics graph.
    for label, root in (("before", args.sources), ("after", output / "projects/clr/hipamd/src")):
        source = (root / "hip_api_trace.cpp").read_text(encoding="utf-8")
        start = source.index("void UpdateDispatchTable(HipDispatchTable*")
        end = source.index("\n}", start)
        for statement in source[start:end].splitlines():
            if any("->" + n + "_fn =" in statement for n in names):
                dispatch.append(statement.replace("ptrDispatchTable->", label + ".") + "\n")
    for name in names:
        dispatch.append('require(before.' + name + '_fn == nullptr && after.' + name + '_fn == &::' + name + ', "dispatch slot ' + name + '");\n')
    dispatch.append('pass("eight external graph dispatch initializer slots (no graph execution)");\n}\n')
    (output / "dispatch.inc").write_text("".join(dispatch), encoding="utf-8", newline="\n")
    sdk = args.hip_root.resolve(strict=True)
    runtime_lock = json.loads((repository / "eng/local-runtime-10.1.0.lock.json").read_text(encoding="utf-8"))
    platform = runtime_lock["platforms"]["win-x64" if os.name == "nt" else "ubuntu.24.04-x64"]
    native = {}
    for item in platform["files"]:
        path = (sdk / item["source"]).resolve(strict=True)
        if sha(path) != item["sha256"]:
            raise ValueError("Installed SDK differs from pinned Runtime: " + item["source"])
        native[str(path)] = item["sha256"]
    compiler = sdk / "bin" / ("hipcc.exe" if os.name == "nt" else "hipcc")
    executable = output / ("regression.exe" if os.name == "nt" else "regression")
    command = [str(compiler), "-std=c++17", "-O2", "--offload-arch=gfx1100", "-I" + str(output),
               str(here / "regression.cpp"), "-L" + str(sdk / "lib"), "-lamdhip64", "-lhiprtc", "-o", str(executable)]
    env = os.environ.copy()
    env["HIP_PATH"] = str(sdk)
    env["PATH"] = str(sdk / "bin") + os.pathsep + env["PATH"]
    if os.name != "nt":
        env["LD_LIBRARY_PATH"] = str(sdk / "lib")
    compiled = subprocess.run(command, env=env, cwd=output, capture_output=True, text=True, timeout=180)
    (output / "build.log").write_text(compiled.stdout + compiled.stderr, encoding="utf-8")
    if compiled.returncode:
        raise RuntimeError("Fragment build failed: " + str(output / "build.log"))
    result = subprocess.run([str(executable)], env=env, cwd=output, capture_output=True, text=True, timeout=90)
    (output / "regression.log").write_text(result.stdout + result.stderr, encoding="utf-8")
    modules = [Path(p).resolve(strict=True) for p in re.findall(r"^MODULE (.+)$", result.stdout, re.MULTILINE)]
    audited = len(modules) == 2 and all(str(p) in native and sha(p) == native[str(p)] for p in modules)
    summary = dict(upstreamCommit=lock["commit"], completeRuntimeRebuilt=False,
                   scope="compiled upstream function fragments with installed HIP/HIPRTC backend adapters",
                   sourceTreeDirty=bool(subprocess.check_output(["git", "status", "--porcelain"], cwd=repository)),
                   validationCommit=subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repository, text=True).strip(),
                   tools={p.name: sha(p) for p in here.iterdir() if p.is_file()},
                   inputs=lock["files"], executableSha256=sha(executable),
                   buildLogSha256=sha(output / "build.log"), logSha256=sha(output / "regression.log"),
                   nativeInputs=native, loadedModules=[str(p) for p in modules], moduleAuditPassed=audited,
                   passed=result.returncode == 0 and audited, exitCode=result.returncode,
                   checks=re.findall(r"^PASS (.+)$", result.stdout, re.MULTILINE))
    (output / "summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(result.stdout, result.stderr, sep="")
    if result.returncode or not audited:
        raise RuntimeError("Fragment GPU regression failed")


if __name__ == "__main__":
    main()
