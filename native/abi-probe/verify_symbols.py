#!/usr/bin/env python3
import argparse
import json
import pathlib
import subprocess
import sys


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--library", required=True)
    parser.add_argument("--library-name", required=True, choices=("amdhip64", "hiprtc"))
    parser.add_argument("--manifest", required=True)
    parser.add_argument("--output", required=True)
    parser.add_argument("--require-optional", action="store_true")
    args = parser.parse_args()

    library = pathlib.Path(args.library).resolve(strict=True)
    manifest_path = pathlib.Path(args.manifest).resolve(strict=True)
    output_path = pathlib.Path(args.output).resolve()
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    nm_output = subprocess.run(
        ["nm", "-D", "--defined-only", str(library)],
        check=True,
        capture_output=True,
        text=True,
    ).stdout
    exported = {line.split()[-1].split("@@", 1)[0] for line in nm_output.splitlines() if line.split()}
    symbols = [
        {
            "managedName": function["managedName"],
            "entryPoint": function["entryPoint"],
            "required": not function["optional"],
            "found": function["entryPoint"] in exported,
        }
        for function in manifest["functions"]
        if function["library"] == args.library_name
    ]
    report = {
        "library": str(library),
        "libraryName": args.library_name,
        "manifest": str(manifest_path),
        "symbols": symbols,
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    missing = [
        symbol["entryPoint"]
        for symbol in symbols
        if (symbol["required"] or args.require_optional) and not symbol["found"]
    ]
    if missing:
        print("Missing required HIP symbols: " + ", ".join(missing), file=sys.stderr)
        return 1

    print(f"Verified {len(symbols)} {args.library_name} symbols in {library}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
