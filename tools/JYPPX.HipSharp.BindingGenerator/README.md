# Binding generator

This tool validates the checked-in normalized HIP manifest model and emits the target-specific C# declarations without adding a runtime dependency to the core package. The selected high-level owners continue to come from `interop-manifest.json`; the complete low-level C ABI comes from the checked-in `complete-api-model.json` extracted from the explicitly pinned ROCm 7.2.1 headers.

```text
pwsh ./eng/generate-interop.ps1 generate
pwsh ./eng/generate-interop.ps1 generate --check
pwsh ./eng/generate-interop.ps1 generate -HeaderRoot ./artifacts/hip-headers
pwsh ./eng/generate-interop.ps1 generate -HeaderRoot ./artifacts/hip-headers -Check
pwsh ./eng/generate-interop.ps1 probe-manifest
dotnet run --project tools/JYPPX.HipSharp.BindingGenerator -- probe-manifest
dotnet run --project tools/JYPPX.HipSharp.BindingGenerator -- extract-headers --header-root ./artifacts/hip-headers --check
```

Official headers are accepted only through an explicitly supplied `HeaderRoot`; the generator never searches arbitrary working directories or downloads headers implicitly. Header extraction fails closed unless it finds exactly 459 Runtime and 18 HIPRTC public C functions for the pinned `rocm-7.2.1` model. The generated `HipRuntimeNativeApi` and `HipRtcNativeApi` expose those 477 declarations as raw ABI calls; the existing managed `HipRuntime` and `HipRtc` owners remain the safer lifecycle-oriented surface.
