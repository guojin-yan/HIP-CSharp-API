# JYPPX.ROCm.HIP.CSharp.API

This `0.9.1` forward-fix candidate is managed-only. It contains the single `JYPPX.ROCm.HIP.CSharp.API` assembly for all 15 declared target frameworks while its public API remains under the `JYPPX.ROCm.HipSharp` namespace. It also contains bilingual XML documentation, this README, the project logo, and the Apache-2.0 license file.

The managed API binds HIP Runtime, memory, stream/event, stream-ordered allocation, managed memory, peer access, graph capture, Module/Launch, and HIPRTC functions. The package does not contain ROCm, an AMD driver, a code object, or any native binary, and it does not force a dependency on a runtime package. The published `0.9.0` package used the unintended `JYPPX.ROCm.HipSharp` assembly identity; its exact validation evidence does not transfer to this candidate. This package remains non-publishable until a fresh Owner-authorized exact-package GPU gate passes.
