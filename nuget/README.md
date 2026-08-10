# JYPPX.HIP.CSharp.API

This local `0.0.0` package is the M2 engineering candidate. It contains the single `JYPPX.HipSharp` assembly for all 15 declared target frameworks, bilingual XML documentation, this README, the project logo, and the Apache-2.0 license file.

The managed API binds HIP Runtime initialization, device, synchronous memory, Module/Launch, and HIPRTC compile/log/code functions. It exposes separate Runtime and HIPRTC error domains and checked ownership for device memory, programs, and modules. The package does not contain ROCm, an AMD driver, a code object, or any native binary. Runtime package candidates remain disabled until provenance, dependency, licensing, and GPU validation gates are complete.
