#!/usr/bin/env python3
import json
import re
import sys


if len(sys.argv) != 8:
    raise SystemExit(
        "usage: validate-hiprtc-program-linker.py RESULT EXPECTED_COMMIT PACKAGE_SHA256 "
        "ENVIRONMENT ARCHITECTURE LENGTH REPEAT"
    )

result_path, expected_commit, package_sha256, environment, architecture, length_text, repeat_text = sys.argv[1:]
length = int(length_text)
repeat = int(repeat_text)
with open(result_path, encoding="utf-8") as stream:
    lines = [
        line.strip()
        for line in stream
        if line.strip().startswith("{") and '"workload":"hiprtc-program-linker-0.9.3"' in line
    ]

if len(lines) != 1:
    raise SystemExit("Expected exactly one HIPRTC Program/Linker JSON result")

result = json.loads(lines[0])
if result.get("schemaVersion") != 1 or result.get("status") != "passed":
    raise SystemExit("HIPRTC Program/Linker workload did not pass")
if result.get("repositoryCommit") != expected_commit or result.get("packageSha256") != package_sha256:
    raise SystemExit("HIPRTC Program/Linker commit or package hash is stale")
if result.get("environment") != environment or result.get("architecture") != architecture:
    raise SystemExit("HIPRTC Program/Linker environment or architecture is stale")
if not result.get("loweredName"):
    raise SystemExit("HIPRTC Program/Linker lowered name is empty")

for prefix in ("bitcode", "addDataCodeObject", "addFileCodeObject"):
    if not isinstance(result.get(prefix + "Size"), int) or result[prefix + "Size"] <= 0:
        raise SystemExit(prefix + " size is invalid")
    if not re.fullmatch(r"[0-9a-f]{64}", result.get(prefix + "Sha256", "")):
        raise SystemExit(prefix + " SHA-256 is invalid")

if result.get("comparisons") != length * repeat * 2:
    raise SystemExit("HIPRTC Program/Linker CPU/GPU comparison count is invalid")

expected_negatives = {
    "lowered-name-before-compile",
    "name-expression-after-compile",
    "add-after-complete",
    "complete-twice",
    "use-after-dispose",
    "empty-managed-input",
    "missing-linker-file",
}
actual_negatives = {item.split("=", 1)[0] for item in result.get("negatives", [])}
if actual_negatives != expected_negatives or any("=passed(" not in item for item in result.get("negatives", [])):
    raise SystemExit("HIPRTC Program/Linker negative coverage is incomplete")
if result.get("performanceClaim") is not False:
    raise SystemExit("HIPRTC Program/Linker workload must not make a performance claim")

print("HIPRTC Program/Linker exact-package workload evidence passed")
