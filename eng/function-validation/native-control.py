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
    parser.add_argument("--bitcode", type=Path)
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
    class BatchOperation(c.Structure):
        _fields_ = [("operation", integer), ("address", ptr), ("value", c.c_uint64),
                    ("flags", c.c_uint), ("alias", ptr), ("padding", c.c_uint64)]
    class BatchNode(c.Structure):
        _fields_ = [("context", ptr), ("count", c.c_uint), ("parameters", ptr), ("flags", c.c_uint)]
    assert c.sizeof(BatchOperation) == 48 and BatchOperation.address.offset == 8 and BatchOperation.value.offset == 16
    assert c.sizeof(BatchNode) == 32 and BatchNode.parameters.offset == 16
    batch_observations = []
    signal, stream = ptr(), ptr()
    success("hipExtMallocWithFlags", [c.POINTER(ptr), size, c.c_uint], c.byref(signal), 8, 2)
    try:
        success("hipStreamCreate", [c.POINTER(ptr)], c.byref(stream))
        for graph_mode in (False, True):
            graph, executable, node, context = ptr(), ptr(), ptr(), ptr()
            success("hipStreamWriteValue64", [ptr, ptr, c.c_uint64, c.c_uint], stream, signal, 0, 0)
            success("hipStreamSynchronize", [ptr], stream)
            operations = (BatchOperation * 2)()
            operations[0].operation, operations[0].address, operations[0].value = 5, signal.value, 0x123456789ABCDEF0
            operations[1].operation, operations[1].address, operations[1].value, operations[1].flags = 4, signal.value, operations[0].value, 1
            try:
                if graph_mode:
                    success("hipCtxGetCurrent", [c.POINTER(ptr)], c.byref(context))
                    success("hipGraphCreate", [c.POINTER(ptr), c.c_uint], c.byref(graph), 0)
                    parameters = BatchNode(context, 2, c.cast(operations, ptr), 0)
                    success("hipGraphAddBatchMemOpNode", [c.POINTER(ptr), ptr, ptr, size, c.POINTER(BatchNode)], c.byref(node), graph, None, 0, c.byref(parameters))
                    success("hipGraphInstantiateWithFlags", [c.POINTER(ptr), ptr, c.c_uint64], c.byref(executable), graph, 0)
                    success("hipGraphLaunch", [ptr, ptr], executable, stream)
                else:
                    success("hipStreamBatchMemOp", [ptr, c.c_uint, ptr, c.c_uint], stream, 2, operations, 0)
                success("hipStreamSynchronize", [ptr], stream)
                actual = c.c_uint64()
                success("hipMemcpyDtoH", [ptr, ptr, size], c.byref(actual), signal, 8)
                batch_observations.append(dict(graph=graph_mode, expected=operations[0].value, actual=actual.value, matched=actual.value == operations[0].value))
            finally:
                if executable: success("hipGraphExecDestroy", [ptr], executable)
                if graph: success("hipGraphDestroy", [ptr], graph)
    finally:
        if stream: success("hipStreamDestroy", [ptr], stream)
        success("hipFree", [ptr], signal)
    class Channel(c.Structure):
        _fields_ = [(name, integer) for name in ("x", "y", "z", "w", "kind")]
    channel = Channel(32, 0, 0, 0, 2)
    texture_width = size()
    success("hipDeviceGetTexture1DLinearMaxWidth", [c.POINTER(size), c.POINTER(Channel), integer], c.byref(texture_width), c.byref(channel), 0)
    driver_link = []
    if args.bitcode:
        image = args.bitcode.read_bytes()
        for mode in ("data", "file"):
            state = ptr()
            success("hipLinkCreate", [c.c_uint, ptr, ptr, c.POINTER(ptr)], 0, None, None, c.byref(state))
            try:
                if mode == "data":
                    storage = c.create_string_buffer(image)
                    result = call("hipLinkAddData", [ptr, integer, ptr, size, c.c_char_p, c.c_uint, ptr, ptr], state, 100, storage, len(image), b"input.bc", 0, None, None)
                else:
                    result = call("hipLinkAddFile", [ptr, integer, c.c_char_p, c.c_uint, ptr, ptr], state, 100, os.fsencode(args.bitcode.resolve()), 0, None, None)
                driver_link.append(dict(mode=mode, inputSha256=hashlib.sha256(image).hexdigest(), inputType=100, result=result))
            finally:
                success("hipLinkDestroy", [ptr], state)
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
                  calls=calls, memset=observations, primaryContext=dict(before=before, afterRetain=after, setFlagsResult=flag_result),
                  textureLinearMaximumWidth=texture_width.value, driverLink=driver_link, batchMemory=batch_observations)
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(dict(memsetMatched=[v["matched"] for v in observations], primaryContext=result["primaryContext"])))
    if cookie:
        cookie.close()


if __name__ == "__main__":
    main()
