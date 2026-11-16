#!/usr/bin/env python3
"""Rehash exact packages, tools and logs; recompute all 477 local function rows."""
import argparse
import json
from pathlib import Path
import sys

sys.dont_write_bytecode = True
from evidence import parse, inventory, unique_object
from run import FIXTURES, metadata, sha


def verify(summary_path, core_override=None, runtime_override=None, repository=None):
    repository = repository or Path(__file__).resolve().parents[2]
    summary = json.loads(summary_path.read_text(encoding="utf-8"), object_pairs_hook=unique_object)
    if summary["scope"] != "local-native-function-fixtures" or summary["completed"] is not True or summary["evidenceStable"] is not True:
        raise ValueError("Incomplete or unstable validation")
    if len(summary["validationCommit"]) != 40:
        raise ValueError("Missing validation commit")
    for name, digest in summary["toolSources"].items():
        path = repository / name
        if repository not in path.resolve().parents or sha(path) != digest:
            raise ValueError("Fixture/tool/model source mismatch: " + name)
    core_path = core_override or Path(summary["core"]["path"])
    runtime_path = runtime_override or Path(summary["runtime"]["path"])
    for path, key in ((core_path, "core"), (runtime_path, "runtime")):
        if sha(path) != summary[key]["sha256"]:
            raise ValueError("Exact package mismatch: " + key)
    if metadata(core_path).find("repository").attrib["commit"] != summary["sourceCommit"]:
        raise ValueError("Core source commit mismatch")
    model = json.loads((repository / "eng/interop/complete-api-model.json").read_text(encoding="utf-8-sig"))
    mapping = {"Raw." + f["managedName"]: f["entryPoint"] for f in model["runtimeFunctions"]}
    mapping.update({"Rtc." + f["managedName"]: f["entryPoint"] for f in model["rtcFunctions"]})
    seen = set()
    for result in summary["results"]:
        name = result["fixture"]
        if name not in FIXTURES or name in seen or result["log"] != name + ".log":
            raise ValueError("Unexpected or duplicate fixture/log identity")
        seen.add(name)
        path = summary_path.parent / result["log"]
        if sha(path) != result["logSha256"]:
            raise ValueError("Execution log mismatch: " + name)
        parsed = parse(path.read_text(encoding="utf-8"), result["exitCode"], name, mapping)
        for key in ("passed", "functions", "nativeRestrictions"):
            if parsed[key] != result[key]:
                raise ValueError("Derived fixture evidence mismatch: " + name)
    if summary["fullMatrix"] != (seen == set(FIXTURES)):
        raise ValueError("Full matrix claim mismatch")
    supported = not any(r["nativeRestrictions"] for r in summary["results"])
    if summary["allFunctionsSupported"] != supported or summary["allPassed"] != (supported and all(r["passed"] for r in summary["results"])):
        raise ValueError("Overall outcome mismatch")
    rows = inventory(summary["results"], mapping)
    if len(rows) != 477 or rows != summary["functions"]:
        raise ValueError("477-function inventory mismatch")
    return dict(integrityVerified=True, fullMatrix=summary["fullMatrix"], allPassed=summary["allPassed"],
                sourceTreeDirty=summary["sourceTreeDirty"], positiveFunctions=sum(bool(r["positiveFixtures"]) for r in rows),
                negativeFunctions=sum(bool(r["negativeFixtures"]) for r in rows))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("summary", type=Path)
    parser.add_argument("--core", type=Path)
    parser.add_argument("--runtime", type=Path)
    args = parser.parse_args()
    print(json.dumps(verify(args.summary, args.core, args.runtime)))
