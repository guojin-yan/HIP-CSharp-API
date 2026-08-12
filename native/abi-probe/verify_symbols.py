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
    parser.add_argument("--allow-missing", action="append", default=[])
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
    if "functions" in manifest:
        functions = [
            function for function in manifest["functions"]
            if function["library"] == args.library_name
        ]
        complete_model = False
    else:
        model_key = "runtimeFunctions" if args.library_name == "amdhip64" else "rtcFunctions"
        functions = manifest.get(model_key, [])
        complete_model = True

    allowed_missing = set(args.allow_missing)
    declared = {function["entryPoint"] for function in functions}
    unknown_allowances = sorted(allowed_missing - declared)
    if unknown_allowances:
        print(
            "Allowed missing symbols are not declared by the model: " + ", ".join(unknown_allowances),
            file=sys.stderr,
        )
        return 2

    symbols = []
    for function in functions:
        entry_point = function["entryPoint"]
        required = complete_model or args.require_optional or not function.get("optional", False)
        symbols.append(
            {
                "managedName": function["managedName"],
                "entryPoint": entry_point,
                "required": required,
                "allowedMissing": entry_point in allowed_missing,
                "found": entry_point in exported,
            }
        )
    report = {
        "library": str(library),
        "libraryName": args.library_name,
        "manifest": str(manifest_path),
        "completeModel": complete_model,
        "expectedCount": len(symbols),
        "foundCount": sum(symbol["found"] for symbol in symbols),
        "allowedMissing": sorted(allowed_missing),
        "symbols": symbols,
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(report, indent=2) + "\n", encoding="utf-8")
    missing = [
        symbol["entryPoint"]
        for symbol in symbols
        if symbol["required"] and not symbol["found"] and not symbol["allowedMissing"]
    ]
    if missing:
        print("Missing required HIP symbols: " + ", ".join(missing), file=sys.stderr)
        return 1

    found_count = sum(symbol["found"] for symbol in symbols)
    print(f"Verified {found_count}/{len(symbols)} {args.library_name} symbols in {library}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
