# M6 advanced API validation

M6 adds stream-ordered allocation/free, managed-memory advice and prefetch, explicit P2P capability/enable/copy ownership, and stream-captured graphs. Captured wrapper operations automatically retain memory, module, managed-array, and pinned-buffer leases across the source graph and every executable. P2P copies use allocation and stream creation-device metadata instead of caller-supplied ordinals. The normalized interop model contains 55 functions and drives both `LibraryImport` and `DllImport` branches. ABI evidence schema 3 adds advanced signatures, enum widths/values, graph handle sizes, and required real-library symbol records.

The Windows x64 work is static only: official ROCm 7.2 SDK names and loader locations are pinned, a fail-closed PE/provenance/license/closure verifier is present, and packaging stays disabled because no local SDK inventory or Windows AMD GPU evidence exists.

The candidate is not published. An Owner-authorized Radeon Cloud session verified all 55 manifest functions against official ROCm 7.2.1 headers/libraries, ran the advanced async-allocation, managed-memory, graph, error, and lifecycle paths, and repeated the immutable M5 package-only gate in an Ubuntu Base + PRoot environment without host `/opt/rocm`. Only one GPU was visible, so P2P was explicitly skipped. Windows remains static-only and GPU-unvalidated.
