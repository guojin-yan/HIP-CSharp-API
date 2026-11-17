# Diagnosed upstream Runtime failures

The installed local SDK identifies ROCm/rocm-systems revision
`7da43026e4f2ef7600433c38c820732f7a205d22`. The links below refer to that exact
official revision, not the latest branch. The existing local Runtime `.3` packages
are unchanged and still exhibit these failures. HipSharp does not maintain or ship
patches to AMD Runtime source. Confirmed native defects are deferred to upstream;
the Core release documents the affected functionality instead of attempting a
private Runtime rebuild. Passing managed/package tests does not make these native
functions work.

| Failure | Official implementation and diagnosis | Upstream tracking and Core support |
| --- | --- | --- |
| `hipMemsetD2D16/32`, including Async, leave part of each row untouched | [hip_memory.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_memory.cpp#L3705) forwards element width to common byte-width code without scaling. | Track existing [official issue #14846](https://github.com/ROCm/rocm-systems/issues/14846) and [draft PR #14851](https://github.com/ROCm/rocm-systems/pull/14851). The report also covers capture-pattern and kernel-width defects; a boundary-only workaround is insufficient. No duplicate issue or private patch. |
| Driver bitcode linking returns `InvalidValue` | [hip_module.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_module.cpp#L1128) rejects the three legal LLVM input types. `100` is the correct bitcode enum. | Confirmed native defect; filed as [official issue #15966](https://github.com/ROCm/rocm-systems/issues/15966). Use the separately tested HIPRTC linker for LLVM bitcode. Driver-link entry points retain the native error. |
| Windows D3D12 external graph wait crashes | [hip_api_trace.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_api_trace.cpp#L1444) omits eight external graph dispatch assignments. [Export wrappers](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_table_interface.cpp#L3097) call the slots without checking. Native crash/disassembly confirms the null call. | Confirmed native defect; filed as [official issue #15967](https://github.com/ROCm/rocm-systems/issues/15967). External-semaphore graph APIs must not be used with this affected Runtime. Direct stream semaphore operations have separate passing evidence. |
| Windows stream/graph batch reports success but does no work | PAL [VirtualGPU](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/rocclr/device/pal/palvirtual.hpp) has no `submitBatchMemoryOperation` override; the [base implementation](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/rocclr/device/device.hpp#L1355) only calls `ShouldNotReachHere()`. Linux's successful fixture does not prove Windows support. | Confirmed native defect; filed as [official issue #15968](https://github.com/ROCm/rocm-systems/issues/15968). Batch entry points are excluded from Windows support for this Runtime. Scalar stream operations were tested separately. |
| Primary-context retain/state/flags differ from expected lifecycle | [hip_context.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_context.cpp#L335): flags are always zero; release/reset are no-ops; retain returns the device pointer; SetFlags always returns ContextAlreadyInUse. | Native limitation; filed as [official issue #15969](https://github.com/ROCm/rocm-systems/issues/15969). Related [official issue #14328](https://github.com/ROCm/rocm-systems/issues/14328) discusses reset/state observability, which is not identical to this retain/flags reproduction. Do not assume CUDA primary-context lifecycle semantics. |
| Legacy border-color set/get reports success without output | [hip_texture.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_texture.cpp#L1147) contains TODO/assert and no getter write; the setter has the same missing storage. | Confirmed native defect; filed as [official issue #15970](https://github.com/ROCm/rocm-systems/issues/15970). Legacy border-color APIs are excluded from supported functionality on this Runtime. |
| Windows GL RGBA8 mapped image copies fail | [hip_gl.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_gl.cpp#L319) produces a HIP array view; native C++ and C# both fail copying despite valid metadata. | Native reproduction available; exact internal cause unconfirmed. Filed as [official issue #15971](https://github.com/ROCm/rocm-systems/issues/15971). GL image-copy interoperability remains unqualified; passing buffer interop is separate. |

The table explains observed failures; it is not a claim that all Runtime interfaces
are supported or that a stable release is approved. The release scope defers these
native defects to AMD and does not require HipSharp to rebuild AMD binaries. WSL cannot substitute for the
pending current-Core KFD/PRoot matrix. Other open boundaries include the texture
linear-width query, the previously intermittent graph-allocation launch failure,
missing native exports, full negative/concurrency coverage, and redistribution and
production-release requirements. Issue #14846 and PR #14851 were found by duplicate
search and were not created by this project. Issues #15966 through #15971 were
submitted from this project and read back to verify the published report bodies.
Filing an Issue does not establish upstream acceptance, a fix, or a fixed binary.
