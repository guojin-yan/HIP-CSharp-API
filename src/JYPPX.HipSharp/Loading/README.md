# Loading boundary

This directory contains deterministic, component-aware HIP Runtime and HIPRTC discovery and diagnostic loading code. Each logical library has an independent handle and explicit-path constraint while sharing the same candidate-order and redaction rules. Loaded candidates must expose `hipInit` or `hiprtcVersion`; wrong-identity candidates are released through the native backend before discovery continues.
