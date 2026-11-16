#!/usr/bin/env python3
"""Direct ctypes control for suspected native defects, without the C# assembly."""
import argparse
import ctypes as c
import hashlib
import json
import os
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native-directory", type=Path, required=True)
    parser.add_argument("--manifest", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    repository = Path(__file__).resolve().parents[2]
    output = args.output.resolve()
    if output == repository or repository in output.parents or output.exists():
        raise ValueError("Use a new file outside the source repository")
    directory = args.native_directory.resolve(strict=True)
    manifest = json.loads(args.manifest.read_text(encoding="utf-8-sig"))
    hashes = {}
    for item in manifest["files"]:
        path = directory / item["name"]
        digest = hashlib.sha256(path.read_bytes()).hexdigest()
        if digest != item["sha256"]:
            raise ValueError("Native file mismatch: " + str(path))
        hashes[path.name] = digest
    cookie = os.add_dll_directory(str(directory)) if os.name == "nt" else None
    library = directory / ("amdhip64_7.dll" if os.name == "nt" else "libamdhip64.so")
    hip = c.CDLL(str(library))
    calls = []

    def call(name, types, *values):
        function = getattr(hip, name)
        function.argtypes = types
        function.restype = c.c_int
        result = function(*values)
        calls.append(dict(function=name, result=result))
        return result

    def success(name, types, *values):
        result = call(name, types, *values)
        if result:
            raise RuntimeError(f"{name}: {result}")

    ptr, size, integer = c.c_void_p, c.c_size_t, c.c_int
    success("hipInit", [c.c_uint], 0)
    context = ptr()
    success("hipDevicePrimaryCtxReset", [integer], 0)
    flags, active = c.c_uint(), integer(-1)
    success("hipDevicePrimaryCtxGetState", [integer, c.POINTER(c.c_uint), c.POINTER(integer)], 0, c.byref(flags), c.byref(active))
    before = active.value
    flag_result = call("hipDevicePrimaryCtxSetFlags", [integer, c.c_uint], 0, flags.value)
    success("hipDevicePrimaryCtxRetain", [c.POINTER(ptr), integer], c.byref(context), 0)
    try:
        success("hipDevicePrimaryCtxGetState", [integer, c.POINTER(c.c_uint), c.POINTER(integer)], 0, c.byref(flags), c.byref(active))
        after = active.value
    finally:
        success("hipDevicePrimaryCtxRelease", [integer], 0)
        success("hipDevicePrimaryCtxReset", [integer], 0)
    device = ptr()
    success("hipMalloc", [c.POINTER(ptr), size], c.byref(device), 1024)
    observations = []
    try:
        for bits, value in ((16, 0x1234), (32, 0x12345678)):
            for asynchronous in (False, True):
                success("hipMemset", [ptr, integer, size], device, 0xCD, 1024)
                entry = "hipMemsetD2D" + str(bits) + ("Async" if asynchronous else "")
                types = [ptr, size, c.c_ushort if bits == 16 else c.c_uint, size, size]
                values = [device, 256, value, 128 // (bits // 8), 4]
                if asynchronous:
                    types.append(ptr)
                    values.append(None)
                success(entry, types, *values)
                success("hipDeviceSynchronize", [])
                host = (c.c_ubyte * 1024)()
                success("hipMemcpyDtoH", [ptr, ptr, size], host, device, 1024)
                expected = b"".join((value.to_bytes(bits // 8, "little") * (128 // (bits // 8))) + bytes([0xCD]) * 128 for _ in range(4))
                actual = bytes(host)
                mismatch = next((i for i in range(1024) if expected[i] != actual[i]), None)
                observations.append(dict(function=entry, pitchBytes=256, widthElements=values[3], height=4,
                                         expectedWrittenBytesPerRow=128, firstMismatch=mismatch,
                                         actualFirstRow=actual[:256].hex(), matched=mismatch is None))
    finally:
        success("hipFree", [ptr], device)
    context = ptr()
    success("hipCtxCreate", [c.POINTER(ptr), c.c_uint, integer], c.byref(context), 0, 0)
    try:
        flags, version = c.c_uint(), c.c_uint()
        call("hipCtxGetFlags", [c.POINTER(c.c_uint)], c.byref(flags))
        call("hipCtxGetApiVersion", [ptr, c.POINTER(c.c_uint)], context, c.byref(version))
    finally:
        success("hipCtxDestroy", [ptr], context)
    result = dict(scope="direct-native-control-no-managed-assembly", library=str(library), librarySha256=hashes[library.name],
                  calls=calls, memset=observations, primaryContext=dict(before=before, afterRetain=after, setFlagsResult=flag_result))
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(memsetMatched=[v["matched"] for v in observations], primaryContext=result["primaryContext"])))
    if cookie:
        cookie.close()


if __name__ == "__main__":
    main()
