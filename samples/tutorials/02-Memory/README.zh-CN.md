# Memory / 内存

<p align="center"><a href="README.md">English</a> | <strong>简体中文</strong></p>

从线性 Copy 逐步学习锁页、Pitched、Managed、Stream-ordered、Memory Pool 和 Virtual Memory。

## 推荐顺序

- [AsyncAllocationAndMemoryPool](./AsyncAllocationAndMemoryPool/README.zh-CN.md)
- [LinearMemoryCopy](./LinearMemoryCopy/README.zh-CN.md)
- [ManagedMemory](./ManagedMemory/README.zh-CN.md)
- [PinnedHostMemory](./PinnedHostMemory/README.zh-CN.md)
- [PitchedMemory2D3D](./PitchedMemory2D3D/README.zh-CN.md)
- [VirtualMemory](./VirtualMemory/README.zh-CN.md)

## 在 Radeon Cloud 中复现

在仓库根目录运行完整教程矩阵：

```bash
bash ./samples/tutorials/run-cloud-verification.sh
```

保留的 Radeon Cloud 证据位于 [`20260818-161709-tutorials`](../../../../Radeon_Cloud/records/20260818-161709-tutorials)。
每个案例 README 都给出了精确命令、预期输出和对应日志。

## Windows 范围

Windows 构建和 GPU 运行尚未验证。各案例提供尽力而为的 PowerShell 命令，但实际 HIP Runtime/驱动兼容性
仍需要单独验证。

## 下一步

从上面的第一个案例开始，保持确定性正确性校验，再进入下一个功能模块。