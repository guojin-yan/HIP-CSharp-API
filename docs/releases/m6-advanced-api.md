# M6 advanced API local candidate

M6 adds stream-ordered allocation/free, managed-memory advice and prefetch, explicit P2P capability/enable/copy ownership, and stream-captured graphs. The normalized interop model contains 55 functions and drives both `LibraryImport` and `DllImport` branches. ABI evidence schema 3 adds advanced signatures, enum widths/values, graph handle sizes, and required real-library symbol records.

The Windows x64 work is static only: official ROCm 7.2 SDK names and loader locations are pinned, a fail-closed PE/provenance/license/closure verifier is present, and packaging stays disabled because no local SDK inventory or Windows AMD GPU evidence exists.

The local candidate is not published. M5 Linux package evidence remains a regression baseline, while M6 advanced API execution and ABI evidence require a newly Owner-authorized real Radeon Cloud session. Until that session is authorized and passes, M6 is blocked rather than complete.
