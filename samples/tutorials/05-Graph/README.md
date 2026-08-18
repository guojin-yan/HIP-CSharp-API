# Graph

<p align="center"><strong>English</strong> | <a href="README.zh-CN.md">简体中文</a></p>

Build explicit DAGs and capture/replay asynchronous work after stream and kernel basics.

## Recommended Order

- [ExplicitGraphDag](./ExplicitGraphDag/README.md)
- [GraphCaptureReplay](./GraphCaptureReplay/README.md)

## Reproduce on Radeon Cloud

From the repository root, run the complete tutorial matrix:

```bash
bash ./samples/tutorials/run-cloud-verification.sh
```

The retained Radeon Cloud evidence is [`20260818-161709-tutorials`](../../../../Radeon_Cloud/records/20260818-161709-tutorials).
Read the individual case README for the exact command, expected output, and per-case log.

## Windows Scope

Windows build and GPU execution have not been validated. The individual case guides contain best-effort
PowerShell commands, but actual HIP Runtime/driver compatibility must be verified separately.

## Next Step

Start with the first case above and keep the deterministic correctness check enabled before moving to the
next capability.