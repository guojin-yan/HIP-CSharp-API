# Managed expansion validation / 高层托管扩展验证

This integration workload validates the M8.2-M8.6 public managed paths on a real Linux/ROCm GPU. It
prints one schema-versioned JSON line and never reports timing or a performance claim.

该集成工作负载在真实 Linux/ROCm GPU 上验证 M8.2-M8.6 的公开高层托管路径。它只输出一行带
schema版本的JSON，不记录计时，也不作性能声明。

```bash
dotnet run --project samples/validation/HipManagedExpansionValidation/HipManagedExpansionValidation.csproj -c Release -- --arch gfx1100 --graph-launch-repeats 3
```

The no-GPU contract check verifies status aggregation and JSON shape without loading HIP:

无GPU契约检查不会加载HIP，用于验证状态聚合和JSON结构：

```bash
dotnet run -c Release -- --self-test
```

`passed` means every required stage passed. Memory-pool NotSupported, optional graph-memory nodes,
and a false cooperative-launch capability are the only controlled skips. Any other stage error
produces `status=failed` and a nonzero exit code.

`passed`表示所有必选阶段通过。仅memory-pool NotSupported、optional graph-memory nodes和
cooperative-launch capability为false允许受控skip；其他阶段错误都会输出`status=failed`并返回非零退出码。
