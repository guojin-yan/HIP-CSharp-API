#!/usr/bin/env python3
import argparse
import hashlib
import json
import pathlib
import platform
import subprocess


def command(*arguments: str) -> str:
    return subprocess.run(arguments, check=True, capture_output=True, text=True).stdout.strip()


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--symbols", required=True)
    parser.add_argument("--types", required=True)
    parser.add_argument("--header", required=True)
    parser.add_argument("--output", required=True)
    args = parser.parse_args()

    header = pathlib.Path(args.header).resolve(strict=True)
    symbols = json.loads(pathlib.Path(args.symbols).read_text(encoding="utf-8"))
    type_values = json.loads(pathlib.Path(args.types).read_text(encoding="utf-8"))
    os_release = pathlib.Path("/etc/os-release").read_text(encoding="utf-8")
    pretty_name = next(
        (line.split("=", 1)[1].strip().strip('"') for line in os_release.splitlines() if line.startswith("PRETTY_NAME=")),
        platform.platform(),
    )
    rocm_version_path = pathlib.Path("/opt/rocm/.info/version")
    rocm_version = rocm_version_path.read_text(encoding="utf-8").strip() if rocm_version_path.exists() else "reported-by-hipconfig"
    report = {
        "gitCommit": command("git", "rev-parse", "HEAD"),
        "environment": {
            "os": pretty_name,
            "architecture": platform.machine(),
            "rocmVersion": rocm_version,
            "hipVersion": command("hipconfig", "--version"),
        },
        "header": {
            "path": str(header),
            "sha256": hashlib.sha256(header.read_bytes()).hexdigest(),
        },
        "symbols": [item["entryPoint"] for item in symbols["symbols"] if item["found"]],
        "types": [{"name": name, "value": value} for name, value in type_values.items()],
    }
    output = pathlib.Path(args.output).resolve()
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    print(f"Wrote ABI evidence for {report['gitCommit']} to {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
