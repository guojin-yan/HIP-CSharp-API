# Diagnosed upstream Runtime failures

The installed local SDK identifies ROCm/rocm-systems revision
`7da43026e4f2ef7600433c38c820732f7a205d22`. The links below refer to that exact
official revision, not the latest branch. The existing local Runtime `.3` packages
are unchanged and still exhibit these failures. Native source patches are in
[`eng/native-runtime-patches`](https://github.com/guojin-yan/HIP-CSharp-API/tree/main/eng/native-runtime-patches).

| Failure | Official implementation and diagnosis | Current corrective work |
| --- | --- | --- |
| `hipMemsetD2D16/32`, including Async, leave part of each row untouched | [hip_memory.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_memory.cpp#L3705) forwards element width to common byte-width code without scaling. | Patch `0001`; original/patched function-fragment GPU regressions on Windows and WSL, with actual Runtime backend adapters. Full patched Runtime integration remains. |
| Driver bitcode linking returns `InvalidValue` | [hip_module.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_module.cpp#L1128) rejects the three legal LLVM input types. `100` is the correct bitcode enum. | Patch `0001`; memory/file guard regressions use real HIPRTC linking and GPU execution. Full patched driver linker execution remains. |
| Windows D3D12 external graph wait crashes | [hip_api_trace.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_api_trace.cpp#L1444) omits eight external graph dispatch assignments. [Export wrappers](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_table_interface.cpp#L3097) call the slots without checking. Native crash/disassembly confirms the null call. | Patch `0002`; eight typed initializer assignments checked. Actual corrected graph execution requires a full rebuilt Runtime. |
| Windows stream/graph batch reports success but does no work | PAL [VirtualGPU](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/rocclr/device/pal/palvirtual.hpp) has no `submitBatchMemoryOperation` override; the [base implementation](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/rocclr/device/device.hpp#L1355) only calls `ShouldNotReachHere()`. Linux's successful fixture does not prove Windows support. | Requires PAL batch implementation and error propagation, followed by real stream/graph ordering tests. No data or success is fabricated by the C# wrapper. |
| Primary-context retain/state/flags differ from expected lifecycle | [hip_context.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_context.cpp#L335): flags are always zero; release/reset are no-ops; retain returns the device pointer; SetFlags always returns ContextAlreadyInUse. | Requires native context lifecycle implementation. Changing a managed counter cannot repair the Runtime's state. |
| Legacy border-color set/get reports success without output | [hip_texture.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_texture.cpp#L1147) contains TODO/assert and no getter write; the setter has the same missing storage. | Requires native texture-reference support, or truthful NotSupported instead of success. Storing a managed color alone would not implement GPU sampling. |
| Windows GL RGBA8 mapped image copies fail | [hip_gl.cpp](https://github.com/ROCm/rocm-systems/blob/7da43026e4f2ef7600433c38c820732f7a205d22/projects/clr/hipamd/src/hip_gl.cpp#L319) produces a HIP array view; native C++ and C# both fail copying despite valid metadata. | Exact failing internal validation/backend branch remains under investigation. Buffer interop success is a separate result. |

The table explains observed failures; it is not a claim that all Runtime interfaces
are supported or that a stable release is approved. WSL cannot substitute for the
pending current-Core KFD/PRoot matrix. Other open boundaries include the texture
linear-width query, the previously intermittent graph-allocation launch failure,
missing native exports, full negative/concurrency coverage, and redistribution and
production-release requirements. No official upstream issue or PR has been sent.
