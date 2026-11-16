# Pinned upstream HIP fixes

These are proposed source fixes for **ROCm/rocm-systems** commit
[`7da43026e4f2ef7600433c38c820732f7a205d22`](https://github.com/ROCm/rocm-systems/tree/7da43026e4f2ef7600433c38c820732f7a205d22),
the revision recorded by the installed TheRock 10.1.0 SDK manifest.
They have not been accepted upstream or integrated into a rebuilt Runtime package.

`0001` fixes four typed 2D memset entry points to convert element counts to byte
widths before entering the common implementation, rejecting width multiplication
overflow. It also fixes driver linker input validation: the original implementation
rejects all three supported LLVM input types. The patch accepts those three types,
rejects other values, and rejects a null filename before constructing `std::string`.

`0002` initializes eight external-semaphore graph dispatch slots that the original
`UpdateDispatchTable` leaves null. The exported entry points call those slots
unconditionally. A Windows direct C++ D3D12 reproduction reaches address zero from
`hipGraphAddExternalSemaphoresWaitNode + 0x4e`; disassembly shows the indirect call
through the table entry at offset `0xd70` (slot 429, after the size field).

## Offline regression

Cache the three original `.cpp` files listed in `upstream.lock.json` outside this
repository. `validate.py` requires their exact SHA-256 values, applies both patches
to a new external output tree, and verifies the resulting source hashes.

```powershell
python eng/native-runtime-patches/validate.py `
  --sources E:/GitSpace/HIP-CSharp-API/review/2026-11-17-official-runtime `
  --output E:/GitSpace/HIP-CSharp-API/build/official-runtime-fixes/windows-new `
  --hip-root D:/AMD/ROCm/10.1.0/HIP
```

On Linux use the same arguments with Linux paths and the installed HIP SDK root.
The regression currently targets the local `gfx1100` GPU. It performs no downloads
or NuGet restore. The SDK native inputs and the two loaded HIP/HIPRTC module paths
and hashes must match `eng/local-runtime-10.1.0.lock.json`.

The test compiles the **actual original and patched upstream entry-point bodies**.
It reproduces the old failures, then checks pitched and contiguous 16/32-bit GPU
fills, unchanged row padding, width overflow, rejected input types/null arguments,
and real HIPRTC linking from memory/file followed by a GPU kernel producing 42
from 41. The internal common memset and LinkProgram dependencies use the adapters
documented in `regression.cpp`; this is a function-fragment regression, not a full
CLR/PAL/HSA Runtime rebuild. The dispatch regression extracts the real initializer
statements and checks all eight typed slots. It does **not** execute a repaired
external-semaphore graph.

## Runtime integration still required

For a complete official source checkout at the pinned revision, first verify the
source hashes and apply both zero-context patches with `git apply --unidiff-zero
--check` followed by `git apply --unidiff-zero`. The validator performs these steps
and verifies the resulting file hashes. Build the full Runtime using that revision's TheRock dependency and
toolchain closure in a new external build directory. Preserve source/build/license
records and give the patched Runtime a new package version and hash lock. Then run
the complete exact-package Windows/Linux GPU matrices, including the actual D3D12
graph fixture. The existing `.3` Runtime packages contain the original binaries;
these patches do not alter them, the installed SDK, or any sealed delivery.

Other diagnosed limitations are tracked in
[the upstream issue guide](../../docs/guides/upstream-runtime-issues.md).
